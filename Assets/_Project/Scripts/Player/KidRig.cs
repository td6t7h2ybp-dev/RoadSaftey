using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using RoadSafety.Core;

namespace RoadSafety.Player
{
    /// <summary>
    /// Builds low-poly, flat-shaded parts at runtime (the faceted look of the reference art),
    /// with no colliders, so nothing added inside the car ever touches the car's physics.
    /// </summary>
    internal static class CockpitKit
    {
        private static readonly Dictionary<PrimitiveType, Mesh> Meshes = new Dictionary<PrimitiveType, Mesh>();
        private static readonly Dictionary<int, Mesh> Spheres = new Dictionary<int, Mesh>();
        private static Mesh capsule;
        private static Material template;

        public static void SetTemplate(Material material)
        {
            if (material != null)
                template = material;
        }

        public static Material Mat(string name, Color color, float smoothness = 0.15f)
        {
            Material m;
            if (template != null)
            {
                m = new Material(template);
            }
            else
            {
                Shader lit = Shader.Find("Universal Render Pipeline/Lit");
                m = new Material(lit != null ? lit : Shader.Find("Standard"));
            }
            m.name = "Cockpit_" + name;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", null);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", null);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            m.DisableKeyword("_EMISSION");
            return m;
        }

        /// <summary>A material that shows a texture and glows a little (screens, gauges).</summary>
        public static Material Screen(string name, Texture2D texture, float glow)
        {
            Material m = Mat(name, Color.white, 0.6f);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", texture);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", texture);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetTexture("_EmissionMap", texture);
                m.SetColor("_EmissionColor", Color.white * glow);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            return m;
        }

        public static void SetColor(Material m, Color color)
        {
            if (m == null) return;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        }

        /// <summary>Same mesh, but every triangle has its own vertices: hard facets, low-poly look.</summary>
        public static Mesh Faceted(Mesh source)
        {
            Vector3[] v = source.vertices;
            int[] t = source.triangles;
            var verts = new Vector3[t.Length];
            var tris = new int[t.Length];
            for (int i = 0; i < t.Length; i++)
            {
                verts[i] = v[t[i]];
                tris[i] = i;
            }
            var mesh = new Mesh { name = source.name + "_Faceted" };
            if (verts.Length > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Faceted icosphere, diameter 1. detail 1 = 80 faces, 2 = 320 faces.</summary>
        public static Mesh Ico(int detail)
        {
            if (Spheres.TryGetValue(detail, out Mesh cached) && cached != null)
                return cached;
            float p = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var verts = new List<Vector3>
            {
                new Vector3(-1, p, 0), new Vector3(1, p, 0), new Vector3(-1, -p, 0), new Vector3(1, -p, 0),
                new Vector3(0, -1, p), new Vector3(0, 1, p), new Vector3(0, -1, -p), new Vector3(0, 1, -p),
                new Vector3(p, 0, -1), new Vector3(p, 0, 1), new Vector3(-p, 0, -1), new Vector3(-p, 0, 1),
            };
            var faces = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized * 0.5f;
            for (int d = 0; d < detail; d++)
            {
                var next = new List<int>();
                var mid = new Dictionary<long, int>();
                int Mid(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) + b : ((long)b << 32) + a;
                    if (mid.TryGetValue(key, out int idx)) return idx;
                    verts.Add(((verts[a] + verts[b]) * 0.5f).normalized * 0.5f);
                    mid[key] = verts.Count - 1;
                    return verts.Count - 1;
                }
                for (int i = 0; i < faces.Count; i += 3)
                {
                    int a = faces[i], b = faces[i + 1], c = faces[i + 2];
                    int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                faces = next;
            }
            var smooth = new Mesh { vertices = verts.ToArray(), triangles = faces.ToArray() };
            Mesh mesh = Faceted(smooth);
            mesh.name = "Ico" + detail;
            Spheres[detail] = mesh;
            return mesh;
        }

        private static Mesh MeshOf(PrimitiveType type)
        {
            if (type == PrimitiveType.Sphere)
                return Ico(2);
            if (Meshes.TryGetValue(type, out Mesh mesh) && mesh != null)
                return mesh;
            GameObject temp = GameObject.CreatePrimitive(type);
            temp.SetActive(false); // its collider must never touch anything
            mesh = Faceted(temp.GetComponent<MeshFilter>().sharedMesh);
            EditorPreviewLog.Kill(temp);
            Meshes[type] = mesh;
            if (type == PrimitiveType.Capsule) capsule = mesh;
            return mesh;
        }

        public static Transform Group(string name, Transform parent, Vector3 localPosition)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            return t;
        }

        public static Transform Part(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 localScale,
            Material material, Quaternion? localRotation = null, ShadowCastingMode shadows = ShadowCastingMode.On) =>
            PartMesh(MeshOf(type), name, parent, localPosition, localScale, material, localRotation, shadows);

        public static Transform PartMesh(Mesh mesh, string name, Transform parent, Vector3 localPosition, Vector3 localScale,
            Material material, Quaternion? localRotation = null, ShadowCastingMode shadows = ShadowCastingMode.On)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation ?? Quaternion.identity;
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows;
            return go.transform;
        }

        /// <summary>Places a capsule or cube (local Y = its length) between two points of its parent's space.</summary>
        public static void Stretch(Transform segment, Vector3 a, Vector3 b, float thickness, float depth = -1f)
        {
            Vector3 d = b - a;
            float length = d.magnitude;
            segment.localPosition = (a + b) * 0.5f;
            segment.localRotation = length > 1e-4f ? Quaternion.FromToRotation(Vector3.up, d / length) : Quaternion.identity;
            Mesh m = segment.GetComponent<MeshFilter>().sharedMesh;
            bool isCapsule = (capsule != null && m == capsule) || (Meshes.TryGetValue(PrimitiveType.Cylinder, out Mesh cyl) && m == cyl);
            segment.localScale = new Vector3(thickness, isCapsule ? length * 0.5f : length, depth > 0f ? depth : thickness);
        }

        /// <summary>Paints a small square texture in code (map screen, gauge faces...).</summary>
        public static Texture2D Paint(int size, System.Func<float, float, Color> pixel)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = pixel((x + 0.5f) / size, (y + 0.5f) / size);
            tex.SetPixels(pixels);
            tex.Apply(true);
            return tex;
        }
    }

    /// <summary>
    /// A low-poly hand: palm, four fingers and a thumb. Grip (fingers curled round the wheel)
    /// or open (waving, pointing).
    /// </summary>
    internal sealed class LowPolyHand
    {
        public Transform Root;
        private readonly Transform[] fingers = new Transform[4];
        private Transform thumb;
        private float openness = -1f;

        public static LowPolyHand Build(Transform parent, string name, Material skin, bool left)
        {
            var hand = new LowPolyHand { Root = CockpitKit.Group(name, parent, Vector3.zero) };
            float side = left ? -1f : 1f;
            CockpitKit.Part(PrimitiveType.Sphere, "Palm", hand.Root, new Vector3(0f, 0f, 0.035f), new Vector3(0.08f, 0.035f, 0.09f), skin);
            for (int i = 0; i < 4; i++)
            {
                Transform knuckle = CockpitKit.Group("Finger" + i, hand.Root, new Vector3((i - 1.5f) * 0.02f, 0f, 0.072f));
                float length = i == 0 || i == 3 ? 0.04f : 0.048f;
                CockpitKit.Part(PrimitiveType.Capsule, "Bone", knuckle, new Vector3(0f, 0f, length * 0.5f), new Vector3(0.019f, length * 0.5f, 0.019f), skin, Quaternion.Euler(90f, 0f, 0f));
                hand.fingers[i] = knuckle;
            }
            hand.thumb = CockpitKit.Group("Thumb", hand.Root, new Vector3(-side * 0.04f, -0.008f, 0.02f));
            CockpitKit.Part(PrimitiveType.Capsule, "Bone", hand.thumb, new Vector3(0f, 0f, 0.02f), new Vector3(0.021f, 0.022f, 0.021f), skin, Quaternion.Euler(90f, 0f, 0f));
            hand.thumb.localRotation = Quaternion.Euler(20f, -side * 45f, 0f);
            hand.SetOpen(0f);
            return hand;
        }

        /// <summary>0 = gripping (fingers curled), 1 = open hand.</summary>
        public void SetOpen(float open)
        {
            if (Mathf.Abs(open - openness) < 0.01f) return;
            openness = open;
            float curl = Mathf.Lerp(95f, 5f, open);
            for (int i = 0; i < fingers.Length; i++)
                fingers[i].localRotation = Quaternion.Euler(curl, (i - 1.5f) * 6f * open, 0f);
        }

        /// <summary>Places the hand at the wrist, pointing along the forearm.</summary>
        public void Place(Vector3 wrist, Vector3 forward, Vector3 up)
        {
            Root.localPosition = wrist;
            if (forward.sqrMagnitude > 1e-6f)
                Root.localRotation = Quaternion.LookRotation(forward, up);
        }
    }

    /// <summary>
    /// A seated child in the low-poly style of the reference: bare forearms, hands with fingers,
    /// and (for the friend) a big friendly face. Positions are in the car interior's space.
    /// </summary>
    internal sealed class KidRig
    {
        private const float UpperArm = 0.27f;
        private const float LowerArm = 0.27f;

        public Transform Root;
        public Transform Upper;
        public Transform HeadPivot;
        public Transform Mouth;
        public Transform EyeL, EyeR;

        private Vector3 shoulderL, shoulderR;
        private Transform upperArmL, lowerArmL, upperArmR, lowerArmR;
        private LowPolyHand handL, handR;
        private Vector3 handPosL, handPosR;
        private bool hasHands;

        public static KidRig Build(Transform root, string name, Vector3 hip, Material top, Material bottom, Material skin,
            Material hair, Material dark, bool headVisible, bool bodyVisible = true, Material sleeve = null)
        {
            var rig = new KidRig { Root = root };
            ShadowCastingMode bodyMode = bodyVisible ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
            Transform body = CockpitKit.Group(name, root, Vector3.zero);

            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 h = hip + new Vector3(s * 0.09f, -0.01f, 0f);
                Vector3 knee = h + new Vector3(s * 0.02f, 0.02f, 0.3f);
                Vector3 foot = knee + new Vector3(0f, -0.12f, 0.15f);
                CockpitKit.Stretch(CockpitKit.Part(PrimitiveType.Capsule, "Thigh", body, Vector3.zero, Vector3.one, bottom, null, bodyMode), h, knee, 0.13f);
                CockpitKit.Stretch(CockpitKit.Part(PrimitiveType.Capsule, "Shin", body, Vector3.zero, Vector3.one, skin, null, bodyMode), knee, foot, 0.09f);
                CockpitKit.Part(PrimitiveType.Sphere, "Shoe", body, foot + new Vector3(0f, -0.02f, 0.04f), new Vector3(0.1f, 0.07f, 0.16f), dark, null, bodyMode);
            }

            rig.Upper = CockpitKit.Group("Upper", body, hip);
            CockpitKit.Part(PrimitiveType.Capsule, "Torso", rig.Upper, new Vector3(0f, 0.14f, 0f), new Vector3(0.3f, 0.15f, 0.2f), top, null, bodyMode);
            CockpitKit.Part(PrimitiveType.Cylinder, "Neck", rig.Upper, new Vector3(0f, 0.3f, 0f), new Vector3(0.07f, 0.03f, 0.07f), skin, null, headVisible ? bodyMode : ShadowCastingMode.ShadowsOnly);

            rig.HeadPivot = CockpitKit.Group("Head", rig.Upper, new Vector3(0f, 0.38f, 0f));
            if (headVisible)
            {
                rig.BuildFace(skin, hair, dark);
            }
            else
            {
                CockpitKit.Part(PrimitiveType.Sphere, "Head", rig.HeadPivot, Vector3.zero, Vector3.one * 0.2f, skin, null, ShadowCastingMode.ShadowsOnly);
            }

            rig.shoulderL = new Vector3(-0.15f, 0.25f, 0.02f);
            rig.shoulderR = new Vector3(0.15f, 0.25f, 0.02f);
            Material upperMat = sleeve != null ? sleeve : skin;
            // seen from the driver's eyes the upper arms are right under the camera: only their shadow
            ShadowCastingMode upperMode = headVisible ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
            rig.upperArmL = CockpitKit.Part(PrimitiveType.Capsule, "UpperArm_L", root, Vector3.zero, Vector3.one, upperMat, null, upperMode);
            rig.lowerArmL = CockpitKit.Part(PrimitiveType.Capsule, "Forearm_L", root, Vector3.zero, Vector3.one, skin);
            rig.upperArmR = CockpitKit.Part(PrimitiveType.Capsule, "UpperArm_R", root, Vector3.zero, Vector3.one, upperMat, null, upperMode);
            rig.lowerArmR = CockpitKit.Part(PrimitiveType.Capsule, "Forearm_R", root, Vector3.zero, Vector3.one, skin);
            rig.handL = LowPolyHand.Build(root, "Hand_L", skin, true);
            rig.handR = LowPolyHand.Build(root, "Hand_R", skin, false);
            return rig;
        }

        /// <summary>A big round low-poly face: hair, ears, brows, eyes with highlights, nose and a wide smile.</summary>
        private void BuildFace(Material skin, Material hair, Material dark)
        {
            Material white = CockpitKit.Mat("EyeWhite", new Color(0.97f, 0.96f, 0.94f), 0.5f);
            Material lip = CockpitKit.Mat("Mouth", new Color(0.45f, 0.13f, 0.12f));
            Transform h = HeadPivot;
            CockpitKit.Part(PrimitiveType.Sphere, "Face", h, Vector3.zero, new Vector3(0.24f, 0.26f, 0.24f), skin);
            CockpitKit.Part(PrimitiveType.Sphere, "Jaw", h, new Vector3(0f, -0.06f, 0.025f), new Vector3(0.2f, 0.16f, 0.19f), skin);
            // short dark hair with a fringe
            CockpitKit.Part(PrimitiveType.Sphere, "Hair", h, new Vector3(0f, 0.045f, -0.015f), new Vector3(0.255f, 0.22f, 0.25f), hair);
            CockpitKit.Part(PrimitiveType.Sphere, "Fringe", h, new Vector3(0.02f, 0.085f, 0.07f), new Vector3(0.2f, 0.08f, 0.12f), hair, Quaternion.Euler(-15f, 0f, -8f));
            foreach (float s in new[] { -1f, 1f })
            {
                CockpitKit.Part(PrimitiveType.Sphere, "Ear", h, new Vector3(s * 0.12f, -0.005f, 0f), new Vector3(0.04f, 0.065f, 0.045f), skin);
                Transform eye = CockpitKit.Group(s < 0 ? "Eye_L" : "Eye_R", h, new Vector3(s * 0.048f, 0.01f, 0.108f));
                CockpitKit.Part(PrimitiveType.Sphere, "White", eye, Vector3.zero, new Vector3(0.042f, 0.046f, 0.02f), white);
                CockpitKit.Part(PrimitiveType.Sphere, "Iris", eye, new Vector3(0f, -0.002f, 0.007f), new Vector3(0.03f, 0.034f, 0.014f), dark);
                CockpitKit.Part(PrimitiveType.Sphere, "Shine", eye, new Vector3(0.006f, 0.008f, 0.014f), new Vector3(0.008f, 0.008f, 0.004f), white);
                if (s < 0) EyeL = eye; else EyeR = eye;
                CockpitKit.Part(PrimitiveType.Cube, "Brow", h, new Vector3(s * 0.05f, 0.05f, 0.112f), new Vector3(0.045f, 0.011f, 0.012f), hair, Quaternion.Euler(0f, 0f, s * -10f));
            }
            CockpitKit.Part(PrimitiveType.Sphere, "Nose", h, new Vector3(0f, -0.022f, 0.122f), new Vector3(0.032f, 0.026f, 0.03f), skin);
            Mouth = CockpitKit.Group("Mouth", h, new Vector3(0f, -0.07f, 0.1f));
            CockpitKit.Part(PrimitiveType.Sphere, "Lips", Mouth, Vector3.zero, new Vector3(0.075f, 0.03f, 0.03f), lip);
            CockpitKit.Part(PrimitiveType.Cube, "Teeth", Mouth, new Vector3(0f, 0.006f, 0.011f), new Vector3(0.05f, 0.01f, 0.01f), white);
        }

        public Vector3 ShoulderPosition(bool left) =>
            Root.InverseTransformPoint(Upper.TransformPoint(left ? shoulderL : shoulderR));

        /// <summary>Moves a hand towards a target (interior space), smoothly, with a bent elbow.</summary>
        public void ReachArm(bool left, Vector3 target, float sharpness, float dt, float open = 0f)
        {
            if (!hasHands)
            {
                handPosL = handPosR = target;
                hasHands = true;
            }
            float k = sharpness <= 0f ? 1f : 1f - Mathf.Exp(-sharpness * dt);
            if (left) handPosL = Vector3.Lerp(handPosL, target, k);
            else handPosR = Vector3.Lerp(handPosR, target, k);
            SolveArm(left, open);
        }

        private void SolveArm(bool left, float open)
        {
            Vector3 s = ShoulderPosition(left);
            Vector3 h = left ? handPosL : handPosR;
            Vector3 d = h - s;
            float reach = UpperArm + LowerArm - 0.005f;
            if (d.magnitude > reach)
                h = s + d.normalized * reach;
            d = h - s;
            float half = d.magnitude * 0.5f;
            float bend = Mathf.Sqrt(Mathf.Max(0f, UpperArm * UpperArm - half * half));
            Vector3 outward = new Vector3(left ? -1f : 1f, 0f, 0f);
            Vector3 dir = d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.forward;
            Vector3 bendDir = Vector3.ProjectOnPlane(Vector3.down * 0.8f + outward * 0.6f, dir);
            if (bendDir.sqrMagnitude < 1e-6f) bendDir = Vector3.down;
            Vector3 elbow = (s + h) * 0.5f + bendDir.normalized * bend;

            CockpitKit.Stretch(left ? upperArmL : upperArmR, s, elbow, 0.075f);
            CockpitKit.Stretch(left ? lowerArmL : lowerArmR, elbow, h, 0.055f);
            LowPolyHand hand = left ? handL : handR;
            Vector3 forearm = (h - elbow).normalized;
            hand.Place(h, forearm, Mathf.Abs(Vector3.Dot(forearm, Vector3.up)) > 0.9f ? Vector3.back : Vector3.up);
            hand.SetOpen(open);
        }

        public void Lean(float pitchDeg, float rollDeg)
        {
            Upper.localRotation = Quaternion.Euler(pitchDeg, 0f, rollDeg);
        }
    }
}
