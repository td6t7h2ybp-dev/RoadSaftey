using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RoadSafety.AI;
using RoadSafety.World;

namespace RoadSafety.EditorTools
{
    /// <summary>
    /// RoadSafety > Level > Add cafés
    /// Turns a few houses along the street into cafés: a striped awning over the door, two tables
    /// with parasols and chairs on the pavement, customers sitting and talking (Townsperson, seated)
    /// and visitors who walk in through the door, stay inside a while and come out again
    /// (<see cref="CafeVisitor"/>). It also removes the pigeons and cats of the street life.
    /// Every person gets a Behavior Designer tree (BTBake) and the scene is saved.
    /// </summary>
    public static partial class LevelBuilder
    {
        private const string CafesRoot = "Cafes";

        [MenuItem("RoadSafety/Level/Add cafés")]
        public static void AddCafes()
        {
            ScenePreview.HideMenu();
            var scene = EditorSceneManager.GetActiveScene();

            materials = new Dictionary<string, Material>();
            meshes = new Dictionary<string, Mesh>();
            random = new System.Random(4242);
            EnsureFolder(MaterialDir);
            EnsureFolder(MeshDir);
            CreateMaterials();
            CreateTownMaterials();
            Mat("Parasol_Red", new Color32(214, 70, 60, 255), 0.1f);
            Mat("Parasol_Cream", new Color32(246, 236, 214, 255), 0.1f);
            Mat("Parasol_Blue", new Color32(40, 110, 180, 255), 0.1f);
            Mat("Parasol_Green", new Color32(90, 160, 80, 255), 0.1f);

            // tidy: no pigeons or cats, no old cafés
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == CafesRoot)
                    Object.DestroyImmediate(root);
                else if (root.name == StreetLife.RootName)
                    foreach (string child in new[] { "Pigeons", "Cats" })
                    {
                        Transform t = root.transform.Find(child);
                        if (t != null) Object.DestroyImmediate(t.gameObject);
                    }
            }

            Transform cafes = new GameObject(CafesRoot).transform;
            int built = 0;
            string[] parasols = { "Parasol_Red", "Parasol_Blue", "Parasol_Green", "Parasol_Cream" };
            foreach (Transform door in PickCafeDoors())
            {
                BuildCafe(cafes, door, built, parasols[built % parasols.Length]);
                built++;
            }

            BTBake.Bake(); // Behavior Designer trees for everyone (cafés included) + save
            Debug.Log($"[LevelBuilder] {built} cafés added (tables, parasols, customers, visitors). Pigeons and cats removed.");
        }

        /// <summary>Doors of houses along the street, away from the lessons and the existing groups.</summary>
        private static List<Transform> PickCafeDoors()
        {
            var doors = new List<Transform>();
            foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == "Environment")
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                        if (t.name == "Door" && t.parent != null && t.parent.name == "House")
                            doors.Add(t);
            doors.Sort((a, b) => a.position.z.CompareTo(b.position.z));

            // keep clear of: red light (z 22), side road (23..37), the old café (52), groups (60, 70),
            // school (100), ball (172), finish (238)
            float[] busy = { 22f, 30f, 52f, 60f, 70f, 100f, 172f, 238f };
            var picked = new List<Transform>();
            var lastZ = new Dictionary<float, float> { { -1f, -999f }, { 1f, -999f } };
            foreach (Transform d in doors)
            {
                float z = d.position.z, side = Mathf.Sign(d.position.x);
                if (z < -55f || z > 228f) continue;
                bool clear = true;
                foreach (float b in busy)
                    if (Mathf.Abs(z - b) < 12f) clear = false;
                if (!clear || z - lastZ[side] < 45f) continue;
                picked.Add(d);
                lastZ[side] = z;
                if (picked.Count >= 6) break;
            }
            return picked;
        }

        private static void BuildCafe(Transform parent, Transform door, int index, string parasol)
        {
            float side = Mathf.Sign(door.position.x);       // -1 west, +1 east
            Vector3 roadward = new Vector3(-side, 0f, 0f);
            Vector3 front = new Vector3(door.position.x, PavementTop, door.position.z);
            Transform cafe = Group("Cafe_" + (index + 1), parent, front);

            // striped awning over the door (on the house)
            for (int s = 0; s < 6; s++)
                Box("Awning_" + s, cafe, new Vector3(-side * 0.55f, 2.62f, (s - 2.5f) * 0.55f), new Vector3(1.1f, 0.08f, 0.55f), s % 2 == 0 ? parasol : "Parasol_Cream");

            // two tables with parasols, chairs and customers (the path to the door stays free)
            string[] tops = { "Cloth_Teal", "Cloth_Mustard", "Cloth_Burgundy", "Cloth_Cream", "Cloth_Olive", "Cloth_Lilac", "Cloth_Denim", "Cloth_Brown" };
            string[] skins = { "Skin", "Skin_Tan", "Skin_Dark" };
            int person = 0;
            foreach (float dz in new[] { -2.8f, 2.8f })
            {
                Vector3 at = roadward * 2.1f + new Vector3(0f, 0f, dz);
                Transform table = Group(dz < 0f ? "Table_A" : "Table_B", cafe, at);
                Cylinder("Top", table, new Vector3(0f, 0.74f, 0f), 0.4f, 0.04f, "Paint_White");
                Cylinder("Leg", table, new Vector3(0f, 0.37f, 0f), 0.05f, 0.72f, "Metal");
                Cylinder("Foot", table, new Vector3(0f, 0.02f, 0f), 0.24f, 0.04f, "Metal");
                Cylinder("Cup_A", table, new Vector3(0.12f, 0.8f, -0.14f), 0.045f, 0.08f, "Paint_White");
                Cylinder("Cup_B", table, new Vector3(-0.1f, 0.8f, 0.16f), 0.045f, 0.08f, "Paint_White");
                Cylinder("ParasolPole", table, new Vector3(0f, 1.3f, 0f), 0.03f, 2.6f, "Metal");
                Prim(PrimitiveType.Sphere, "Parasol", table, new Vector3(0f, 2.55f, 0f), new Vector3(2.4f, 0.45f, 2.4f), parasol);

                for (int k = 0; k < 2; k++)
                {
                    float s = k == 0 ? -1f : 1f;
                    float yaw = s < 0f ? 0f : 180f; // facing the table
                    Transform chair = Group("Chair_" + (k + 1), table, new Vector3(0f, 0f, s * 0.75f), yaw);
                    Box("Seat", chair, new Vector3(0f, 0.445f, -0.05f), new Vector3(0.42f, 0.05f, 0.42f), "Wood");
                    Box("Back", chair, new Vector3(0f, 0.72f, -0.25f), new Vector3(0.42f, 0.5f, 0.05f), "Wood");
                    Box("Base", chair, new Vector3(0f, 0.21f, -0.05f), new Vector3(0.06f, 0.42f, 0.06f), "Metal");
                    if (dz > 0f && k == 1 && index % 2 == 1) continue; // a free chair here and there
                    int n = index * 4 + person;
                    Headwear head = n % 4 == 0 ? Headwear.Scarf : n % 4 == 1 ? Headwear.Chechia : n % 4 == 2 ? Headwear.Hair : Headwear.GreyHair;
                    Body customer = Person(cafe, $"CafeCustomer_{person + 1}", chair.position, yaw, tops[n % tops.Length], "Trousers",
                        head, n % 2 == 0 ? "Scarf_White" : "Scarf_Rose", false, skins[n % skins.Length], 0.47f);
                    Configure(customer, Townsperson.Kind.Seated, Vector3.zero, Vector3.zero, table, k, 2);
                    person++;
                }
            }

            // visitors: walk in, have a coffee inside, come out, stroll, come back
            for (int v = 0; v < 2; v++)
            {
                int n = index * 2 + v;
                float walkX = side * WalkLineX;
                Vector3 a = new Vector3(walkX, PavementTop, front.z - 14f - v * 3f);
                Vector3 b = new Vector3(walkX, PavementTop, front.z + 14f + v * 3f);
                Vector3 start = Vector3.Lerp(a, b, v == 0 ? 0.25f : 0.75f);
                Headwear head = n % 3 == 0 ? Headwear.Cap : n % 3 == 1 ? Headwear.Hair : Headwear.Scarf;
                Body body = Person(cafe, $"CafeVisitor_{v + 1}", start, v == 0 ? 0f : 180f, tops[(n + 3) % tops.Length],
                    n % 2 == 0 ? "Trousers" : "Cloth_Grey", head, "Scarf_White", head == Headwear.Scarf, skins[(n + 1) % skins.Length], 0f);
                var visitor = body.Root.gameObject.AddComponent<CafeVisitor>();
                visitor.door = front + roadward * 0.4f;
                visitor.inside = front - roadward * 1.8f;
                visitor.strollA = a;
                visitor.strollB = b;
                visitor.legL = body.LegL;
                visitor.legR = body.LegR;
                visitor.armL = body.ArmL;
                visitor.armR = body.ArmR;
                visitor.upper = body.Upper;
            }
        }
    }
}
