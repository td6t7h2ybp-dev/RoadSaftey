using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using RoadSafety.Core;

namespace RoadSafety.Player
{
    /// <summary>
    /// Drives a real rigged character (Resources/Models/rs_boy_rigged, a Character Creator rig)
    /// in code, without an Animator: seated pose, arms that reach for a target (two-bone IK),
    /// fingers that grip, head that turns, jaw that opens with the voice.
    /// Works whatever the bones' local axes are: every bone is aimed in world space at its child.
    /// Used for the friend (whole body) and for the driver (head hidden: the camera is the eyes).
    /// </summary>
    internal sealed class RiggedKid
    {
        public const string ModelPath = "Models/rs_boy_rigged";

        public Transform Root;      // the instance (scaled, placed)
        public Transform Seat;      // parent frame: +Z = where the kid faces, +Y = up
        private Transform hip, spine, head, jaw;
        private Transform lUpper, lFore, lHand, rUpper, rFore, rHand;
        private Transform lThigh, lCalf, lFoot, rThigh, rCalf, rFoot;
        private readonly List<Transform[]> lFingers = new List<Transform[]>(), rFingers = new List<Transform[]>();
        private readonly Dictionary<Transform, Quaternion> rest = new Dictionary<Transform, Quaternion>();
        private float upperLen, lowerLen;

        // inputs for this frame (Seat space)
        public Vector3 HandTargetL, HandTargetR;
        public float GripL, GripR;          // 0 open .. 1 fist
        public float HeadYaw, HeadPitch;    // degrees, relative to the seat
        public float JawOpen;               // 0..1
        public float Lean, Roll;            // degrees
        public float Bounce;                // metres
        public float SpineYaw;              // degrees: the upper body turns (towards the driver)

        private Vector3 hipRest;

        /// <summary>Instantiates the rigged model, or returns null if it is not in the project.</summary>
        public static RiggedKid Spawn(Transform seat, float standingHeight, bool hideHead, Texture2D topTexture = null, Color? topColor = null)
        {
            var prefab = Resources.Load<GameObject>(ModelPath);
            if (prefab == null)
                return null;
            GameObject go = Object.Instantiate(prefab, seat, false);
            go.name = hideHead ? "Driver_Rigged" : "Friend_Rigged";
            var kid = new RiggedKid { Root = go.transform, Seat = seat };
            if (!kid.FindBones())
            {
                EditorPreviewLog.Kill(go);
                return null;
            }

            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.name.ToLowerInvariant();
                Mesh mesh = r is SkinnedMeshRenderer sk ? sk.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh != null) n += " " + mesh.name.ToLowerInvariant();
                foreach (Material m in r.sharedMaterials)
                    if (m != null) n += " " + m.name.ToLowerInvariant();
                // the driver: hair, eyes and teeth would float in front of the camera
                if (hideHead && (n.Contains("hair") || n.Contains("eye") || n.Contains("teeth") || n.Contains("tongue")))
                    r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                if (n.Contains("skate") || n.Contains("ring")) r.enabled = false; // props of the original model
                if (n.Contains("hoodie") || n.Contains("lambert22"))
                {
                    // own copies of the materials (works in the Editor preview too)
                    Material[] mats = r.sharedMaterials;
                    for (int mi = 0; mi < mats.Length; mi++)
                        if (mats[mi] != null) mats[mi] = new Material(mats[mi]);
                    r.sharedMaterials = mats;
                    foreach (Material m in mats)
                    {
                        if (m == null) continue;
                        // the top: striped T-shirt for the friend, bare skin for the driver's arms
                        Color c = topColor ?? new Color(0.45f, 0.45f, 0.48f);
                        foreach (string prop in new[] { "baseColorFactor", "_BaseColor", "_Color" })
                            if (m.HasProperty(prop)) m.SetColor(prop, c);
                        if (topTexture != null)
                            foreach (string prop in new[] { "baseColorTexture", "_BaseMap", "_MainTex" })
                                if (m.HasProperty(prop)) m.SetTexture(prop, topTexture);
                    }
                }
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
                else if (r.shadowCastingMode != ShadowCastingMode.ShadowsOnly) r.shadowCastingMode = ShadowCastingMode.On;
            }

            // scale to a child's height, measured from the feet to the top of the head
            float feet = Mathf.Min(kid.lFoot.position.y, kid.rFoot.position.y);
            float top = kid.head.position.y;
            float modelHeight = Mathf.Max(0.1f, (top - feet) * 1.12f);
            go.transform.localScale *= standingHeight / modelHeight;

            // turn the model so that it faces the seat's +Z
            Vector3 across = kid.rThigh.position - kid.lThigh.position; // the kid's left -> right
            Vector3 facing = Vector3.Cross(across, seat.up).normalized;
            if (facing.sqrMagnitude > 0.01f)
                go.transform.rotation = Quaternion.FromToRotation(facing, seat.forward) * go.transform.rotation;

            // hips on the seat origin
            go.transform.position += seat.position - kid.hip.position;

            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                kid.rest[t] = t.localRotation;
            kid.hipRest = kid.hip.localPosition;
            kid.upperLen = Vector3.Distance(kid.lUpper.position, kid.lFore.position);
            kid.lowerLen = Vector3.Distance(kid.lFore.position, kid.lHand.position);

            if (hideHead)
            {
                // the camera is the driver's eyes: hide the head and neck (scale their bones to nothing)
                kid.head.localScale = Vector3.one * 0.0001f;
                foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("CC_Base_NeckTwist01_") && !t.name.EndsWith("_0")) t.localScale = Vector3.one * 0.0001f;
            }

            kid.HandTargetL = seat.InverseTransformPoint(kid.lHand.position);
            kid.HandTargetR = seat.InverseTransformPoint(kid.rHand.position);
            return kid;
        }

        public Vector3 HeadPosition => head.position;
        public Vector3 ChestPosition => spine != null ? spine.position : hip.position;
        public Vector3 ShoulderSeatSpace(bool left) => Seat.InverseTransformPoint((left ? lUpper : rUpper).position);

        private bool FindBones()
        {
            var all = Root.GetComponentsInChildren<Transform>(true);
            Transform F(string key)
            {
                foreach (Transform t in all)
                    if (t.name.StartsWith("CC_Base_" + key + "_") && !t.name.EndsWith("_0") && !t.name.EndsWith("_1"))
                        return t;
                return null;
            }
            hip = F("Hip"); spine = F("Spine02"); head = F("Head"); jaw = F("JawRoot");
            lUpper = F("L_Upperarm"); lFore = F("L_Forearm"); lHand = F("L_Hand");
            rUpper = F("R_Upperarm"); rFore = F("R_Forearm"); rHand = F("R_Hand");
            lThigh = F("L_Thigh"); lCalf = F("L_Calf"); lFoot = F("L_Foot");
            rThigh = F("R_Thigh"); rCalf = F("R_Calf"); rFoot = F("R_Foot");
            foreach (string f in new[] { "Index", "Mid", "Ring", "Pinky" })
            {
                lFingers.Add(new[] { F("L_" + f + "1"), F("L_" + f + "2"), F("L_" + f + "3") });
                rFingers.Add(new[] { F("R_" + f + "1"), F("R_" + f + "2"), F("R_" + f + "3") });
            }
            return hip != null && head != null && lUpper != null && lFore != null && lHand != null && rUpper != null
                   && rFore != null && rHand != null && lThigh != null && lCalf != null && rThigh != null && rCalf != null
                   && lFoot != null && rFoot != null;
        }

        private static void Aim(Transform bone, Transform child, Vector3 worldDir)
        {
            if (bone == null || child == null || worldDir.sqrMagnitude < 1e-8f) return;
            Vector3 current = child.position - bone.position;
            if (current.sqrMagnitude < 1e-10f) return;
            bone.rotation = Quaternion.FromToRotation(current, worldDir) * bone.rotation;
        }

        /// <summary>Applies the pose for this frame. Call from LateUpdate.</summary>
        public void Pose()
        {
            foreach (var kv in rest)
                if (kv.Key != null) kv.Key.localRotation = kv.Value;
            hip.localPosition = hipRest;
            hip.position += Seat.up * Bounce;

            Vector3 up = Seat.up, fwd = Seat.forward, right = Seat.right;

            // seated: thighs forward, shins down
            foreach (var (thigh, calf, foot) in new[] { (lThigh, lCalf, lFoot), (rThigh, rCalf, rFoot) })
            {
                Aim(thigh, calf, fwd * 0.97f - up * 0.25f);
                Aim(calf, foot, -up * 0.9f + fwd * 0.35f);
            }

            // upper body lean (speeding up / braking, turns)
            Quaternion lean = Quaternion.AngleAxis(Lean, right) * Quaternion.AngleAxis(Roll, fwd);
            if (spine != null) spine.rotation = Quaternion.AngleAxis(SpineYaw, up) * lean * spine.rotation;

            // arms
            Reach(lUpper, lFore, lHand, Seat.TransformPoint(HandTargetL), -right);
            Reach(rUpper, rFore, rHand, Seat.TransformPoint(HandTargetR), right);
            Curl(lFingers, lHand, GripL, 1f);
            Curl(rFingers, rHand, GripR, -1f);

            // head and jaw
            head.rotation = Quaternion.AngleAxis(HeadYaw, up) * Quaternion.AngleAxis(HeadPitch, Quaternion.AngleAxis(HeadYaw, up) * right) * head.rotation;
            if (jaw != null)
                jaw.rotation = Quaternion.AngleAxis(JawOpen * 16f, Quaternion.AngleAxis(HeadYaw, up) * right) * jaw.rotation;
        }

        private void Reach(Transform upper, Transform fore, Transform hand, Vector3 target, Vector3 outward)
        {
            Vector3 s = upper.position;
            float scale = Root.lossyScale.x / Mathf.Max(1e-4f, Root.localScale.x) * Root.localScale.x;
            float a = upperLen, b = lowerLen;
            Vector3 d = target - s;
            float reach = (a + b) * 0.999f;
            if (d.magnitude > reach) target = s + d.normalized * reach;
            d = target - s;
            float dist = Mathf.Max(1e-4f, d.magnitude);
            // law of cosines: distance from the shoulder to the elbow's projection, and the bend height
            float x = (a * a - b * b + dist * dist) / (2f * dist);
            float h = Mathf.Sqrt(Mathf.Max(0f, a * a - x * x));
            Vector3 dir = d / dist;
            Vector3 bendDir = Vector3.ProjectOnPlane(-Seat.up * 0.8f + outward * 0.6f, dir).normalized;
            Vector3 elbow = s + dir * x + bendDir * h;
            Aim(upper, fore, elbow - s);
            Aim(fore, hand, target - fore.position);
        }

        private static void Curl(List<Transform[]> fingers, Transform hand, float grip, float sign)
        {
            if (grip <= 0.01f) return;
            Transform index = fingers[0][0], pinky = fingers[3][0];
            if (index == null || pinky == null) return;
            Vector3 across = (index.position - pinky.position).normalized;
            for (int f = 0; f < fingers.Count; f++)
                for (int j = 0; j < fingers[f].Length; j++)
                {
                    Transform bone = fingers[f][j];
                    if (bone == null) continue;
                    bone.rotation = Quaternion.AngleAxis(sign * grip * (j == 0 ? 55f : 70f), across) * bone.rotation;
                }
        }
    }
}
