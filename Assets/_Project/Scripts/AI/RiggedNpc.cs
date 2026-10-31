using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace RoadSafety.AI
{
    /// <summary>
    /// Gives every NPC (townspeople, cafe visitors, the school kid, the kids in the garden) the same
    /// rigged character as the friend in the car, instead of the primitive capsules.
    ///
    /// The rigged body is saved in Main.unity as a child called "Body_Rigged" (RoadSafety > AI >
    /// Bake rigged people into the scene). The primitive capsules are switched off in the scene
    /// file too, so the scene looks right in the Editor, when Play starts and when Play stops.
    /// At runtime this component only poses the saved body from the AI's primitive limbs
    /// (walk, wave, sit, sip tea, nod...). If a body was never baked, it is built when Play starts.
    /// </summary>
    [DisallowMultipleComponent]
    public class RiggedNpc : MonoBehaviour
    {
        public const string ModelPath = "Models/rs_boy_rigged";
        public const string BodyName = "Body_Rigged";
        private const float PoseDistance = 70f;   // closer than this: posed every frame
        private const float HideDistance = 140f;  // further than this: not drawn at all

        /// <summary>Editor bake only: saves a material as an asset and returns it.</summary>
        public static Func<Material, string, Material> BakeSave;

        /// <summary>Editor bake only: returns the saved stripes texture asset.</summary>
        public static Func<Texture2D> BakeStripes;

        private static Texture2D stripes;

        // the NPC whose primitive rig the AI animates (this object)
        private Transform src;

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

        // ------------------------------------------------------------------ setup (Play)

        /// <summary>Height of a person in metres, from their name (the same in Play and in the bake).</summary>
        public static float HeightFor(GameObject go)
        {
            if (go.name == "SchoolKid") return 1.32f;
            if (go.name.StartsWith("PlayingKid_")) return 1.26f + Hash01(go.name) * 0.12f;
            return 1.62f + Hash01(go.name) * 0.2f;
        }

        /// <summary>Play mode: dresses every NPC that has no baked body yet.</summary>
        public static void DressAll()
        {
            if (!Application.isPlaying || Resources.Load<GameObject>(ModelPath) == null)
                return;
            foreach (Townsperson t in FindObjectsByType<Townsperson>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Dress(t.gameObject);
            foreach (Pedestrian p in FindObjectsByType<Pedestrian>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Dress(p.gameObject);
            foreach (CafeVisitor v in FindObjectsByType<CafeVisitor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Dress(v.gameObject);
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("PlayingKid_"))
                        Dress(t.gameObject);
        }

        private static void Dress(GameObject go)
        {
            if (go.GetComponent<RiggedNpc>() != null)
                return;
            var npc = go.AddComponent<RiggedNpc>();
            npc.src = go.transform;
            if (!npc.Build(HeightFor(go), false))
                Destroy(npc);
        }

        /// <summary>Editor bake: builds and saves this NPC's rigged body into the scene.</summary>
        public bool BakeInto(float height)
        {
            src = transform;
            return Build(height, true);
        }

        /// <summary>
        /// Sets up the body and the bones. bake = true builds the saved body (no pose, no play-time
        /// changes); bake = false uses the saved body, or builds one if the scene has none.
        /// </summary>
        public bool Build(float height, bool bake)
        {
            if (src == null) src = transform;
            legL = src.Find("Leg_L");
            legR = src.Find("Leg_R");
            upper = src.Find("Upper");
            armL = src.Find("Upper/Arm_L");
            armR = src.Find("Upper/Arm_R");
            if (legL == null || legR == null || armL == null || armR == null)
                return false;
            seated = legL.Find("Thigh") != null;
            if (upper != null) upperRest = upper.localPosition;

            Transform existing = src.Find(BodyName);
            bool baked = existing != null;
            GameObject body;
            if (baked)
            {
                body = existing.gameObject;
            }
            else
            {
                var prefab = Resources.Load<GameObject>(ModelPath);
                if (prefab == null)
                    return false;
                body = Instantiate(prefab, transform, false);
                body.name = BodyName;
            }
            model = body.transform;
            if (!FindBones())
            {
                if (!baked) Destroy(body);
                return false;
            }

            if (!baked)
            {
                // hide the capsules (not the speech bubble): saved in the scene when baking
                foreach (Renderer r in src.GetComponentsInChildren<Renderer>(true))
                    if (!r.transform.IsChildOf(model) && !InBubble(r.transform))
                        r.enabled = false;

                Clothe(body, bake);

                // height: feet to the top of the head
                float feet = Mathf.Min(lFoot.position.y, rFoot.position.y);
                float modelHeight = Mathf.Max(0.1f, (head.position.y - feet) * 1.12f);
                model.localScale *= height / modelHeight;

                // face the NPC's forward
                Vector3 across = rThigh.position - lThigh.position;
                Vector3 facing = Vector3.Cross(across, Vector3.up).normalized;
                if (facing.sqrMagnitude > 0.01f)
                    model.rotation = Quaternion.FromToRotation(facing, transform.forward) * model.rotation;

                // place: seated → hips on the chair; standing → feet on the ground
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
            }

            bodyRenderers = model.GetComponentsInChildren<Renderer>(true);
            posed = new[] { spine, head, lThigh, lCalf, rThigh, rCalf, lUpper, lFore, rUpper, rFore };
            posedRest = new Quaternion[posed.Length];
            for (int i = 0; i < posed.Length; i++)
                if (posed[i] != null) posedRest[i] = posed[i].localRotation;
            hipRest = hip.localPosition;
            phase = Hash01(src.name) * 10f;
            if (!bake) Pose();
            return true;
        }

        private static float Hash01(string s)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char c in s) h = (h ^ c) * 16777619;
                return (h % 1000) / 1000f;
            }
        }

        private bool InBubble(Transform t)
        {
            for (; t != null && t != src; t = t.parent)
                if (t.name == "TalkBubble") return true;
            return false;
        }

        private Color ColourOf(string a, string b, Color fallback)
        {
            foreach (Renderer r in src.GetComponentsInChildren<Renderer>(true))
                if ((r.name == a || r.name == b) && r.sharedMaterial != null)
                {
                    Material m = r.sharedMaterial;
                    if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
                    if (m.HasProperty("_Color")) return m.color;
                }
            return fallback;
        }

        private void Clothe(GameObject body, bool bake)
        {
            Color top = ColourOf("Body", "Smock", new Color(0.3f, 0.5f, 0.7f));
            Color bottom = ColourOf("Leg", "Thigh", new Color(0.25f, 0.25f, 0.3f));
            bool striped = Hash01(src.name + "s") < 0.3f;
            Texture tex = striped ? (bake && BakeStripes != null ? BakeStripes() : Stripes()) : Texture2D.whiteTexture;
            int counter = 0;

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
                        Material m = new Material(mats[i]);
                        Color c = isTop ? top : Color.Lerp(bottom, Color.white, 0.15f);
                        foreach (string p in new[] { "baseColorFactor", "_BaseColor", "_Color" })
                            if (m.HasProperty(p)) m.SetColor(p, c);
                        if (isTop)
                            foreach (string p in new[] { "baseColorTexture", "_BaseMap", "_MainTex" })
                                if (m.HasProperty(p)) m.SetTexture(p, tex);
                        if (bake && BakeSave != null)
                            m = BakeSave(m, src.name + "_" + (counter++));
                        mats[i] = m;
                    }
                    r.sharedMaterials = mats;
                }
                if (r is SkinnedMeshRenderer smr)
                {
                    smr.updateWhenOffscreen = false;
                    smr.quality = SkinQuality.Bone2; // lighter skinning: there are dozens of people
                }
                r.shadowCastingMode = ShadowCastingMode.On;
            }
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

        // ------------------------------------------------------------------ posing (Play)

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

            // the body follows the AI's little jumps (the primitive upper body moves)
            if (upper != null)
                hip.position += transform.TransformVector(upper.localPosition - upperRest);

            // upper body: a breath so nobody stands frozen
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

            // arms: the AI's arm limbs (wave, tea, gesture) are copied to the body
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
            // elbow: bent most when the hand comes to the mouth (tea), straighter when raised high (waving)
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
