using UnityEngine;
using UnityEngine.SceneManagement;
using RoadSafety.Player;

namespace RoadSafety.World
{
    /// <summary>
    /// Life in the street, every creature run by its own Behavior Designer tree:
    ///   - pigeon flocks on the pavements (<see cref="PigeonFlock"/>);
    ///   - seagulls over the street (<see cref="Gull"/>);
    ///   - stray cats along the walls (<see cref="StreetCat"/>);
    ///   - palm leaves moving in the breeze (<see cref="PalmSway"/>).
    /// RoadSafety > Level > Add street life saves all of it in Main.unity (with their trees); if the
    /// scene has none, <see cref="Apply"/> builds it when Play starts. Nothing touches the road rules.
    /// </summary>
    public static class StreetLife
    {
        public const string RootName = "StreetLife";

        public static void Apply()
        {
            if (GameObject.Find(RootName) != null)
                return;
            Build();
        }

        /// <summary>Creates the creatures (Play mode or Editor).</summary>
        public static GameObject Build()
        {
            var root = new GameObject(RootName).transform;

            Material grey = Mat("M_Pigeon", new Color(0.55f, 0.56f, 0.6f));
            Material dark = Mat("M_PigeonDark", new Color(0.32f, 0.33f, 0.38f));
            Material white = Mat("M_Gull", new Color(0.96f, 0.96f, 0.94f));
            Material beak = Mat("M_Beak", new Color(0.95f, 0.62f, 0.2f));
            Material ginger = Mat("M_CatGinger", new Color(0.93f, 0.55f, 0.22f));
            Material tabby = Mat("M_CatGrey", new Color(0.5f, 0.48f, 0.46f));
            Material black = Mat("M_CatBlack", new Color(0.12f, 0.11f, 0.12f));
            Material pink = Mat("M_CatNose", new Color(0.95f, 0.6f, 0.65f));

            var rng = new System.Random(7);

            // pigeons: pavements at |x| 6..9, away from the side road (z 23..37)
            Transform pigeons = Group("Pigeons", root);
            float[] zs = { -50f, -20f, 8f, 52f, 78f, 118f, 140f, 165f, 196f, 222f };
            for (int i = 0; i < zs.Length; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                Transform flock = Group("PigeonFlock_" + (i + 1), pigeons);
                flock.position = new Vector3(side * (6.2f + (float)rng.NextDouble() * 2.5f), 0.2f, zs[i]);
                int n = 4 + rng.Next(4);
                for (int k = 0; k < n; k++)
                {
                    Transform bird = Bird(flock, k % 3 == 0 ? dark : grey, beak, 0.32f);
                    bird.localPosition = new Vector3((float)rng.NextDouble() * 2.8f - 1.4f, 0f, (float)rng.NextDouble() * 2.8f - 1.4f);
                    bird.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                }
                flock.gameObject.AddComponent<PigeonFlock>();
            }

            // seagulls circling over the street (the town is by the sea)
            Transform gulls = Group("Seagulls", root);
            for (int i = 0; i < 6; i++)
            {
                Transform g = Bird(gulls, white, beak, 0.55f);
                g.name = "Seagull_" + (i + 1);
                var gull = g.gameObject.AddComponent<Gull>();
                gull.centre = new Vector3((float)rng.NextDouble() * 30f - 15f, 22f + (float)rng.NextDouble() * 10f, i * 45f - 20f);
                gull.radius = 10f + (float)rng.NextDouble() * 12f;
                gull.speed = 0.25f + (float)rng.NextDouble() * 0.2f;
                gull.phase = (float)rng.NextDouble() * 6.28f;
                g.position = gull.centre + new Vector3(gull.radius, 0f, 0f);
            }

            // stray cats along the walls
            Transform cats = Group("Cats", root);
            var catRoutes = new (float side, float a, float b)[]
            {
                (-1f, -40f, -5f), (1f, 45f, 80f), (-1f, 110f, 150f), (1f, 160f, 200f), (-1f, 200f, 228f),
            };
            Material[] furs = { ginger, tabby, black, ginger, tabby };
            for (int i = 0; i < catRoutes.Length; i++)
            {
                var r = catRoutes[i];
                Transform cat = Cat(cats, "Cat_" + (i + 1), furs[i], pink);
                var c = cat.gameObject.AddComponent<StreetCat>();
                c.pathA = new Vector3(r.side * 9.6f, 0.2f, r.a);
                c.pathB = new Vector3(r.side * 9.6f, 0.2f, r.b);
                cat.position = Vector3.Lerp(c.pathA, c.pathB, (float)rng.NextDouble());
            }

            // palms sway
            foreach (GameObject r in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (Transform t in r.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Palm" && t.GetComponent<PalmSway>() == null)
                        t.gameObject.AddComponent<PalmSway>();

            return root.gameObject;
        }

        private static Transform Group(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        private static Material Mat(string name, Color c)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Standard");
            var m = new Material(s) { name = name };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            m.color = c;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
            m.enableInstancing = true;
            return m;
        }

        /// <summary>A little low-poly bird: body, head, beak, tail and two wings that can flap.</summary>
        private static Transform Bird(Transform parent, Material body, Material beak, float size)
        {
            var root = new GameObject("Bird").transform;
            root.SetParent(parent, false);
            Part(PrimitiveType.Sphere, root, new Vector3(0f, 0.5f, 0f), new Vector3(0.55f, 0.5f, 1f), body);
            Part(PrimitiveType.Sphere, root, new Vector3(0f, 0.85f, 0.42f), Vector3.one * 0.38f, body);
            Part(PrimitiveType.Cube, root, new Vector3(0f, 0.82f, 0.65f), new Vector3(0.08f, 0.06f, 0.16f), beak);
            Part(PrimitiveType.Cube, root, new Vector3(0f, 0.5f, -0.6f), new Vector3(0.3f, 0.06f, 0.4f), body);
            foreach (float s in new[] { -1f, 1f })
            {
                var pivot = new GameObject(s < 0 ? "WingL" : "WingR").transform;
                pivot.SetParent(root, false);
                pivot.localPosition = new Vector3(s * 0.22f, 0.62f, 0f);
                Part(PrimitiveType.Cube, pivot, new Vector3(s * 0.38f, 0f, -0.05f), new Vector3(0.76f, 0.05f, 0.5f), body);
            }
            root.localScale = Vector3.one * size;
            return root;
        }

        /// <summary>A round cartoon cat: body, head, ears, nose, legs and a tail that sways.</summary>
        private static Transform Cat(Transform parent, string name, Material fur, Material nose)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            var body = new GameObject("Body").transform;
            body.SetParent(root, false);
            Part(PrimitiveType.Sphere, body, new Vector3(0f, 0.2f, 0f), new Vector3(0.24f, 0.22f, 0.46f), fur);
            Part(PrimitiveType.Sphere, body, new Vector3(0f, 0.36f, 0.24f), Vector3.one * 0.22f, fur);
            Part(PrimitiveType.Cube, body, new Vector3(-0.06f, 0.48f, 0.24f), new Vector3(0.06f, 0.09f, 0.04f), fur);
            Part(PrimitiveType.Cube, body, new Vector3(0.06f, 0.48f, 0.24f), new Vector3(0.06f, 0.09f, 0.04f), fur);
            Part(PrimitiveType.Sphere, body, new Vector3(0f, 0.35f, 0.35f), Vector3.one * 0.04f, nose);
            foreach (float x in new[] { -0.07f, 0.07f })
                foreach (float z in new[] { -0.14f, 0.14f })
                    Part(PrimitiveType.Capsule, body, new Vector3(x, 0.07f, z), new Vector3(0.06f, 0.07f, 0.06f), fur);
            var tail = new GameObject("Tail").transform;
            tail.SetParent(body, false);
            tail.localPosition = new Vector3(0f, 0.26f, -0.22f);
            Part(PrimitiveType.Capsule, tail, new Vector3(0f, 0.12f, -0.04f), new Vector3(0.05f, 0.14f, 0.05f), fur);
            return root;
        }

        private static void Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material m)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            Collider col = g.GetComponent<Collider>();
            if (Application.isPlaying) Object.Destroy(col); else Object.DestroyImmediate(col);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        internal static void Flap(Transform bird, float angle)
        {
            Transform l = bird.Find("WingL"), r = bird.Find("WingR");
            if (l != null) l.localRotation = Quaternion.Euler(0f, 0f, angle);
            if (r != null) r.localRotation = Quaternion.Euler(0f, 0f, -angle);
        }

        internal static float CarDistance(Vector3 p)
        {
            CarController car = CarController.Player;
            if (car == null) return float.MaxValue;
            Vector3 c = car.transform.position;
            return Vector2.Distance(new Vector2(c.x, c.z), new Vector2(p.x, p.z));
        }
    }
}
