using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using RoadSafety.Player;

namespace RoadSafety.World
{
    /// <summary>
    /// Small things that make the street feel lived in, added at runtime (no scene rebuild):
    ///   - flocks of pigeons pecking on the pavements; they fly up when the car comes close
    ///     and land again a bit further on;
    ///   - seagulls circling high above (the town is by the sea);
    ///   - palm leaves moving in the breeze.
    /// Nothing here touches the road, the rules or the lessons.
    /// </summary>
    public static class StreetLife
    {
        public static void Apply()
        {
            if (GameObject.Find("StreetLife") != null)
                return;
            var root = new GameObject("StreetLife").transform;

            Material grey = Mat(new Color(0.55f, 0.56f, 0.6f));
            Material dark = Mat(new Color(0.32f, 0.33f, 0.38f));
            Material white = Mat(new Color(0.96f, 0.96f, 0.94f));
            Material beak = Mat(new Color(0.95f, 0.62f, 0.2f));

            // pigeons: pavements at |x| 5..10, away from the side road (z 23..37)
            var rng = new System.Random(7);
            float[] zs = { -50f, -20f, 8f, 52f, 78f, 118f, 140f, 165f, 196f, 222f };
            for (int i = 0; i < zs.Length; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                var flock = new GameObject("Pigeons_" + i).transform;
                flock.SetParent(root, false);
                flock.position = new Vector3(side * (6.2f + (float)rng.NextDouble() * 2.5f), 0.2f, zs[i]);
                var f = flock.gameObject.AddComponent<PigeonFlock>();
                int n = 4 + rng.Next(4);
                for (int k = 0; k < n; k++)
                    f.Add(Bird(flock, (k % 3 == 0) ? dark : grey, beak, 0.32f));
            }

            // seagulls circling over the street
            for (int i = 0; i < 6; i++)
            {
                var g = Bird(root, white, beak, 0.55f);
                var c = g.gameObject.AddComponent<Gull>();
                c.centre = new Vector3((float)rng.NextDouble() * 30f - 15f, 22f + (float)rng.NextDouble() * 10f, i * 45f - 20f);
                c.radius = 10f + (float)rng.NextDouble() * 12f;
                c.speed = 0.25f + (float)rng.NextDouble() * 0.2f;
                c.phase = (float)rng.NextDouble() * 6.28f;
            }

            // palms sway
            foreach (GameObject r in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (Transform t in r.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Palm" && t.GetComponent<PalmSway>() == null)
                        t.gameObject.AddComponent<PalmSway>();
        }

        private static Material Mat(Color c)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(s);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            m.color = c;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
            m.enableInstancing = true;
            return m;
        }

        /// <summary>A little low-poly bird: body, head, beak and two wings that can flap.</summary>
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

        private static void Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material m)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            Object.Destroy(g.GetComponent<Collider>());
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
    }

    /// <summary>Pigeons peck around; when the car gets close they fly off and land again further on.</summary>
    public class PigeonFlock : MonoBehaviour
    {
        private class P
        {
            public Transform t;
            public Vector3 home, target, vel;
            public float next, phase;
        }

        private readonly List<P> birds = new List<P>();
        private bool flying;
        private float flyTime;
        private Vector3 landing;

        public void Add(Transform b)
        {
            var p = new P { t = b, phase = Random.value * 10f };
            p.home = new Vector3(Random.Range(-1.4f, 1.4f), 0f, Random.Range(-1.4f, 1.4f));
            b.localPosition = p.home;
            b.localRotation = Quaternion.Euler(0f, Random.value * 360f, 0f);
            p.target = p.home;
            birds.Add(p);
        }

        private void Update()
        {
            CarController car = CarController.Player;
            Vector3 carPos = car != null ? car.transform.position : new Vector3(0f, 0f, -1000f);
            float d = Vector3.Distance(new Vector3(carPos.x, 0f, carPos.z), new Vector3(transform.position.x, 0f, transform.position.z));

            if (!flying && d < 9f)
            {
                flying = true;
                flyTime = 0f;
                landing = transform.position + new Vector3(0f, 0f, Random.Range(18f, 30f));
                foreach (P p in birds) p.vel = new Vector3(Random.Range(-1f, 1f), Random.Range(3f, 5f), Random.Range(1f, 3f));
            }

            if (flying)
            {
                flyTime += Time.deltaTime;
                foreach (P p in birds)
                {
                    Vector3 goal = transform.InverseTransformPoint(landing) + p.home + Vector3.up * Mathf.Clamp01(3.5f - flyTime) * 4f;
                    p.vel = Vector3.Lerp(p.vel, (goal - p.t.localPosition) * 0.9f, Time.deltaTime * 1.4f);
                    p.t.localPosition += p.vel * Time.deltaTime;
                    if (p.vel.sqrMagnitude > 0.01f)
                        p.t.localRotation = Quaternion.Slerp(p.t.localRotation, Quaternion.LookRotation(p.vel), Time.deltaTime * 6f);
                    StreetLife.Flap(p.t, Mathf.Sin((Time.time + p.phase) * 28f) * 55f);
                }
                if (flyTime > 5.5f)
                {
                    // landed: the flock lives here now
                    Vector3 shift = landing - transform.position;
                    transform.position = landing;
                    foreach (P p in birds)
                    {
                        Vector3 lp = p.t.localPosition - shift; // the flock root moved: keep the birds where they are
                        p.t.localPosition = new Vector3(lp.x, lp.y, lp.z);
                        p.target = p.home;
                        StreetLife.Flap(p.t, 0f);
                    }
                    flying = false;
                }
                return;
            }

            // on the ground: hop around and peck
            foreach (P p in birds)
            {
                if (Time.time > p.next)
                {
                    p.next = Time.time + Random.Range(0.8f, 3f);
                    p.target = p.home + new Vector3(Random.Range(-0.7f, 0.7f), 0f, Random.Range(-0.7f, 0.7f));
                }
                Vector3 to = p.target - p.t.localPosition;
                to.y = 0f;
                if (to.magnitude > 0.05f)
                {
                    p.t.localPosition += Vector3.ClampMagnitude(to, Time.deltaTime * 0.6f);
                    p.t.localRotation = Quaternion.Slerp(p.t.localRotation, Quaternion.LookRotation(to), Time.deltaTime * 8f);
                }
                Vector3 lp = p.t.localPosition;
                lp.y = Mathf.Lerp(lp.y, 0f, Time.deltaTime * 6f);
                p.t.localPosition = lp;
                float peck = Mathf.Max(0f, Mathf.Sin((Time.time + p.phase) * 3.1f)) > 0.92f ? 25f : 0f;
                p.t.localRotation = Quaternion.Euler(peck, p.t.localEulerAngles.y, 0f);
            }
        }
    }

    /// <summary>A seagull gliding in wide circles, flapping now and then.</summary>
    public class Gull : MonoBehaviour
    {
        public Vector3 centre;
        public float radius = 12f, speed = 0.3f, phase;

        private void Update()
        {
            float a = Time.time * speed + phase;
            Vector3 p = centre + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a * 0.7f) * 1.5f, Mathf.Sin(a) * radius);
            Vector3 dir = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
            transform.position = p;
            transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 0f, 18f);
            float flap = Mathf.Repeat(a * 2f, 6.28f) < 1.2f ? Mathf.Sin(Time.time * 14f) * 35f : 8f; // mostly gliding
            StreetLife.Flap(transform, flap);
        }
    }

    /// <summary>Palm leaves swaying gently in the sea breeze.</summary>
    public class PalmSway : MonoBehaviour
    {
        private readonly List<(Transform t, Quaternion rest, float phase)> leaves = new List<(Transform, Quaternion, float)>();

        private void Start()
        {
            foreach (Transform c in transform)
                if (c.name == "Leaf")
                    leaves.Add((c, c.localRotation, Random.value * 6.28f));
        }

        private void Update()
        {
            float wind = Mathf.Sin(Time.time * 0.6f) * 0.5f + 0.5f;
            foreach (var (t, rest, phase) in leaves)
                t.localRotation = rest * Quaternion.Euler(Mathf.Sin(Time.time * 1.7f + phase) * (2f + wind * 3f), 0f, Mathf.Sin(Time.time * 1.1f + phase) * 2f);
        }
    }
}
