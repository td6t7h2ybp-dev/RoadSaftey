using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace RoadSafety.AI
{
    /// <summary>
    /// Gives every NPC (townspeople, the school kid, the kids in the garden) the same real rigged
    /// character as the friend in the car (Resources/Models/rs_boy_rigged), instead of capsules.
    ///
    /// Nothing in the AI changes: Townsperson and Pedestrian keep animating their invisible
    /// primitive limbs (Leg_L, Arm_R, Upper...) in Update; in LateUpdate this component reads those
    /// limbs and aims the matching bones of the rigged body the same way (walk, wave, sit, sip tea,
    /// nod, step back...). The primitive renderers are only hidden, so if the model is missing the
    /// old look comes back on its own.
    ///
    /// Each person gets their own clothes (colour from their old outfit, sometimes stripes like the
    /// friend's T-shirt) and their own height, so the street is full of different people.
    /// </summary>
    [DisallowMultipleComponent]
    public class RiggedNpc : MonoBehaviour
    {
        private const string ModelPath = "Models/rs_boy_rigged";
        private const float PoseDistance = 70f;   // closer than this: posed every frame
        private const float HideDistance = 140f;  // further than this: not drawn at all

        private static Texture2D stripes;

        // the primitive rig the AI animates
        private Transform legL, legR, armL, armR, upper;
        private bool seated;
        private Vector3 upperRest;

        // the rigged body
        private Transform model, hip, spine, head;
        private Transform lThigh, lCalf, lFoot, rThigh, rCalf, rFoot;
        private Transform lUpper, lFore, lHand, rUpper, rFore, rHand;
        private Transform[] posed;
        private Quaternion[] posedRest;
        private Vector3 hipRest;
        private Renderer[] bodyRenderers;
        private bool visible = true;
        private float phase;

        // ------------------------------------------------------------------ setup

        /// <summary>Dresses every NPC of the loaded scene. Safe to call twice.</summary>
        public static void DressAll()
        {
            if (Resources.Load<GameObject>(ModelPath) == null)
                return;
            foreach (Townsperson t in Object.FindObjectsByType<Townsperson>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Dress(t.gameObject, AdultHeight(t.name));
            foreach (Pedestrian p in Object.FindObjectsByType<Pedestrian>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Dress(p.gameObject, 1.32f);
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("PlayingKid_"))
                        Dress(t.gameObject, 1.26f + Hash01(t.name) * 0.12f);
        }

        private static float AdultHeight(string name) => 1.62f + Hash01(name) * 0.2f;

        private static float Hash01(string s)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char c in s) h = (h ^ c) * 16777619;
                return (h % 1000) / 1000f;
            }
        }

        private static void Dress(GameObject go, float height)
        {
            if (go.GetComponent<RiggedNpc>() != null)
                return;
            var npc = go.AddComponent<RiggedNpc>();
            if (!npc.Build(height))
                Destroy(npc);
        }

        private bool Build(float height)
        {
            legL = transform.Find("Leg_L");
            legR = transform.Find("Leg_R");
            upper = transform.Find("Upper");
            armL = transform.Find("Upper/Arm_L");
            armR = transform.Find("Upper/Arm_R");
            if (legL == null || legR == null || armL == null || armR == null)
                return false;
            seated = legL.Find("Thigh") != null;
            if (upper != null) upperRest = upper.localPosition;

            Color top = ColourOf("Body", "Smock", new Color(0.3f, 0.5f, 0.7f));
            Color bottom = ColourOf("Leg", "Thigh", new Color(0.25f, 0.25f, 0.3f));

            var prefab = Resources.Load<GameObject>(ModelPath);
            GameObject body = Instantiate(prefab, transform, false);
            body.name = "Body_Rigged";
            model = body.transform;
            if (!FindBones())
            {
                Destroy(body);
                return false;
            }

            // hide the capsules (not the speech bubble)
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                if (!r.transform.IsChildOf(model) && !InBubble(r.transform))
                    r.enabled = false;

            Clothe(body, top, bottom);

            // height: feet to the top of the head
            float feet = Mathf.Min(lFoot.position.y, rFoot.position.y);
            float modelHeight = Mathf.Max(0.1f, (head.position.y - feet) * 1.12f);
            model.localScale *= height / modelHeight;

            // face the NPC's forward
            Vector3 across = rThigh.position - lThigh.position;
            Vector3 facing = Vector3.Cross(across, Vector3.up).normalized;
            if (facing.sqrMagnitude > 0.01f)
                model.rotation = Quaternion.FromToRotation(facing, transform.forward) * model.rotation;

            // place: standing → feet on the ground; seated → hips on the chair
            if (seated)
            {
                Vector3 seat = transform.TransformPoint(0f, legL.localPosition.y, -0.05f);
                model.position += seat - hip.position;
            }
            else
            {
                feet = Mathf.Min(lFoot.position.y, rFoot.position.y);
                Vector3 shift = transform.position - hip.position;
                shift.y = transform.position.y - feet;
                model.position += shift;
            }

            posed = new[] { spine, head, lThigh, lCalf, rThigh, rCalf, lUpper, lFore, rUpper, rFore };
            posedRest = new Quaternion[posed.Length];
            for (int i = 0; i < posed.Length; i++)
                if (posed[i] != null) posedRest[i] = posed[i].localRotation;
            hipRest = hip.localPosition;
            phase = Hash01(name) * 10f;
            Pose();
            return true;
        }

        private bool InBubble(Transform t)
        {
            for (; t != null && t != transform; t = t.parent)
                if (t.name == "TalkBubble") return true;
            return false;
        }

        private Color ColourOf(string a, string b, Color fallback)
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                if ((r.name == a || r.name == b) && r.sharedMaterial != null)
                {
                    Material m = r.sharedMaterial;
                    if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
                    if (m.HasProperty("_Color")) return m.color;
                }
            return fallback;
        }

        private void Clothe(GameObject body, Color top, Color bottom)
        {
            bool striped = Hash01(name + "s") < 0.3f;
            var list = new List<Renderer>();
            foreach (Renderer r in body.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.name.ToLowerInvariant();
                Mesh mesh = r is SkinnedMeshRenderer sk ? sk.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh != null) n += " " + mesh.name.ToLowerInvariant();
                foreach (Material m in r.sharedMaterials)
                    if (m != null) n += " " + m.name.ToLowerInvariant();

                if (n.Contains("skate") || n.Contains("ring")) { r.enabled = false; continue; } // props of the original model
                bool isTop = n.Contains("hoodie") || n.Contains("lambert22");
                bool isBottom = n.Contains("pant") || n.Contains("jean") || n.Contains("trouser") || n.Contains("short");
                if (isTop || isBottom)
                {
                    Material[] mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] == null) continue;
                        mats[i] = new Material(mats[i]);
                        Color c = isTop ? top : Color.Lerp(bottom, Color.white, 0.15f);
                        foreach (string p in new[] { "baseColorFactor", "_BaseColor", "_Color" })
                            if (mats[i].HasProperty(p)) mats[i].SetColor(p, c);
                        if (isTop)
                        {
                            Texture tex = striped ? Stripes() : Texture2D.whiteTexture;
                            foreach (string p in new[] { "baseColorTexture", "_BaseMap", "_MainTex" })
                                if (mats[i].HasProperty(p)) mats[i].SetTexture(p, tex);
                        }
                    }
                    r.sharedMaterials = mats;
                }
                if (r is SkinnedMeshRenderer smr)
                {
                    smr.updateWhenOffscreen = false;
                    smr.quality = SkinQuality.Bone2; // lighter skinning: there are dozens of people
                }
                r.shadowCastingMode = ShadowCastingMode.On;
                list.Add(r);
            }
            bodyRenderers = list.ToArray();
        }

        private static Texture2D Stripes()
        {
            if (stripes != null) return stripes;
            stripes = new Texture2D(8, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 8; x++)
                    stripes.SetPixel(x, y, (y / 8) % 2 == 0 ? Color.white : new Color(0.82f, 0.82f, 0.8f));
            stripes.Apply();
            return stripes;
        }

        private bool FindBones()
        {
            var all = model.GetComponentsInChildren<Transform>(true);
            Transform F(string key)
            {
                foreach (Transform t in all)
                    if (t.name.StartsWith("CC_Base_" + key + "_") && !t.name.EndsWith("_0") && !t.name.EndsWith("_1"))
                        return t;
                return null;
            }
            hip = F("Hip"); spine = F("Spine02"); head = F("Head");
            lUpper = F("L_Upperarm"); lFore = F("L_Forearm"); lHand = F("L_Hand");
            rUpper = F("R_Upperarm"); rFore = F("R_Forearm"); rHand = F("R_Hand");
            lThigh = F("L_Thigh"); lCalf = F("L_Calf"); lFoot = F("L_Foot");
            rThigh = F("R_Thigh"); rCalf = F("R_Calf"); rFoot = F("R_Foot");
            return hip != null && head != null && lUpper != null && lFore != null && lHand != null && rUpper != null
                   && rFore != null && rHand != null && lThigh != null && lCalf != null && rThigh != null && rCalf != null
                   && lFoot != null && rFoot != null;
        }

        // ------------------------------------------------------------------ posing

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            float d = cam != null ? Vector3.Distance(cam.transform.position, transform.position) : 0f;
            bool show = d < HideDistance;
            if (show != visible)
            {
                visible = show;
                foreach (Renderer r in bodyRenderers) if (r != null) r.enabled = show;
            }
            if (!show) return;
            if (d > PoseDistance && (Time.frameCount + GetInstanceID()) % 4 != 0) return; // far away: pose less often
            Pose();
        }

        private void Pose()
        {
            for (int i = 0; i < posed.Length; i++)
                if (posed[i] != null) posed[i].localRotation = posedRest[i];
            hip.localPosition = hipRest;

            Vector3 up = transform.up, fwd = transform.forward;
            float t = Time.time + phase;

            // the body bob / little jump of the old rig
            if (upper != null)
                hip.position += transform.TransformVector(upper.localPosition - upperRest);

            // upper body: nods, gestures; plus a breath so nobody stands frozen
            if (spine != null)
            {
                Quaternion lean = upper != null ? transform.rotation * upper.localRotation * Quaternion.Inverse(transform.rotation) : Quaternion.identity;
                Quaternion breath = Quaternion.AngleAxis(Mathf.Sin(t * 1.6f) * 1.2f, transform.right);
                spine.rotation = lean * breath * spine.rotation;
            }

            // legs
            if (seated)
            {
                foreach (var (thigh, calf, foot) in new[] { (lThigh, lCalf, lFoot), (rThigh, rCalf, rFoot) })
                {
                    Aim(thigh, calf, fwd * 0.97f - up * 0.12f);
                    Aim(calf, foot, -up * 0.95f + fwd * 0.2f);
                }
            }
            else
            {
                Leg(legL, lThigh, lCalf, lFoot);
                Leg(legR, rThigh, rCalf, rFoot);
            }

            // arms, hanging a little away from the hips
            Arm(armL, lUpper, lFore, lHand, -1f);
            Arm(armR, rUpper, rFore, rHand, 1f);

            // the head looks around a little
            head.rotation = Quaternion.AngleAxis(Mathf.Sin(t * 0.37f) * 9f, up) * head.rotation;
        }

        private void Leg(Transform limb, Transform thigh, Transform calf, Transform foot)
        {
            float pitch = Mathf.DeltaAngle(0f, limb.localEulerAngles.x); // > 0: the leg swings back
            Vector3 dir = limb.rotation * Vector3.down;
            float bend = 4f + Mathf.Max(0f, pitch) * 1.3f;              // the knee bends on the back swing
            Aim(thigh, calf, dir);
            Aim(calf, foot, Quaternion.AngleAxis(bend, limb.right) * dir);
        }

        private void Arm(Transform limb, Transform upperArm, Transform fore, Transform hand, float side)
        {
            float pitch = Mathf.DeltaAngle(0f, limb.localEulerAngles.x); // < 0: the arm comes forward / up
            Vector3 dir = limb.rotation * Vector3.down;
            if (pitch > -20f) // hanging: keep it clear of the body
                dir = Quaternion.AngleAxis(side * 9f, transform.forward) * dir;
            // elbow: bent most when the hand comes to the mouth (tea), straighter when raised high (waving, "I want to cross")
            float bend = 12f + Mathf.Sin(Mathf.Clamp01((-pitch - 20f) / 140f) * Mathf.PI) * 55f;
            Vector3 foreDir = limb.rotation * Quaternion.Euler(-bend, 0f, 0f) * Vector3.down;
            if (pitch > -20f)
                foreDir = Quaternion.AngleAxis(side * 9f, transform.forward) * foreDir;
            Aim(upperArm, fore, dir);
            Aim(fore, hand, foreDir);
        }

        private static void Aim(Transform bone, Transform child, Vector3 worldDir)
        {
            if (bone == null || child == null || worldDir.sqrMagnitude < 1e-8f) return;
            Vector3 current = child.position - bone.position;
            if (current.sqrMagnitude < 1e-10f) return;
            bone.rotation = Quaternion.FromToRotation(current, worldDir) * bone.rotation;
        }
    }
}
