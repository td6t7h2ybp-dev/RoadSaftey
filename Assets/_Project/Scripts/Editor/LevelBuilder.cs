using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using RoadSafety.AI;
using RoadSafety.Player;
using RoadSafety.World;

namespace RoadSafety.EditorTools
{
    /// <summary>
    /// Menu RoadSafety > Level > Build Level 01: builds the whole Tunisian street in Main.unity
    /// (deletes and rebuilds "Environment", "Traffic", "Pedestrians" and "Rules"; sets up the
    /// player's car and the camera). Run it again after changing this file.
    ///
    /// The street (the player drives north, +Z, in the right lane x = +2.5):
    ///   z = -45   start, white houses with blue doors, palm trees
    ///   z =  30   crossroads with traffic lights and a side road (US 2.1)
    ///   z = 100   zebra crossing in front of the school "مدرسة" (US 2.2)
    ///   z = 172   garden "حديقة": children play behind a parked louage, a ball rolls out (US 2.3)
    ///   z = 200   mosque with a square minaret
    ///   z = 238   city gate (bab) "وصلت بالسلامة!" = finish (US 3.3)
    /// Oncoming taxis and louages drive in the other lane, cars cross at the lights.
    /// Townspeople walk, chat, sit at the café and wave at the car (LevelBuilder.Town.cs),
    /// and the HUD is saved in the scene under "UI" so it is visible without pressing Play.
    /// </summary>
    public static partial class LevelBuilder
    {
        private const string MaterialDir = "Assets/_Project/Materials/Generated";
        private const string MeshDir = "Assets/_Project/Models/Generated";
        private const string PrefabDir = "Assets/_Project/Prefabs/Vehicles";
        private const string SignDir = "Assets/_Project/Textures/Signs";

        private const float RoadStart = -75f;
        private const float RoadEnd = 265f;
        private const float SideRoadZ = 30f;
        private const float SchoolZ = 100f;
        private const float BallZ = 172f;
        private const float FinishZ = 238f;

        private static Dictionary<string, Material> materials;
        private static Dictionary<string, Mesh> meshes;
        private static System.Random random;

        // ===================================================================== entry

        [MenuItem("RoadSafety/Level/Build Level 01 (Tunisian street)")]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "Main" &&
                !EditorUtility.DisplayDialog("Build Level 01", $"The open scene is \"{scene.name}\", not Main. Build the level in it anyway?", "Build", "Cancel"))
                return;

            materials = new Dictionary<string, Material>();
            meshes = new Dictionary<string, Mesh>();
            shopFronts = new Dictionary<string, Vector2>();
            random = new System.Random(2026);
            EnsureFolder(MaterialDir);
            EnsureFolder(MeshDir);
            EnsureFolder(PrefabDir);

            CreateMaterials();
            CreateTownMaterials();
            Dictionary<string, GameObject> models = CreateVehicleModels();

            foreach (string rootName in new[] { "Environment", "Traffic", "Pedestrians", "Rules" })
                foreach (GameObject root in scene.GetRootGameObjects())
                    if (root.name == rootName)
                        Object.DestroyImmediate(root);

            Transform env = new GameObject("Environment").transform;
            BuildGround(env);
            BuildRoads(env);
            BuildMarkings(env);
            BuildBoundaries(env);
            BuildHouses(env);
            BuildPalms(env);
            BuildSchool(env);
            BuildGarden(env);
            BuildMosque(env);
            BuildGate(env);
            BuildRoadSigns(env);

            Transform traffic = new GameObject("Traffic").transform;
            TrafficLight light = BuildTrafficLights(traffic);

            Transform pedestrians = new GameObject("Pedestrians").transform;
            Pedestrian kid = BuildSchoolKid(pedestrians);
            BuildTownsfolk(pedestrians);

            Transform rules = new GameObject("Rules").transform;
            CrosswalkRule crosswalk = BuildRules(rules, light, kid, models);

            BuildOtherCars(traffic, models, light, crosswalk);
            SetUpPlayer(models["Car_Player"]);
            SetUpCameraAndLight();
            BuildHUD(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[LevelBuilder] Level 01 built and Main.unity saved.");
        }

        // ===================================================================== materials

        private static void CreateMaterials()
        {
            Mat("Asphalt", new Color32(66, 70, 78, 255), 0.1f);
            Mat("Sidewalk", new Color32(222, 208, 182, 255), 0.05f);
            Mat("Ground", new Color32(214, 196, 150, 255), 0f);
            Mat("Lawn", new Color32(122, 196, 74, 255), 0f);
            Mat("Hedge", new Color32(70, 140, 60, 255), 0f);
            Mat("Paint", new Color32(245, 245, 240, 255), 0.1f);
            Mat("PaintRed", new Color32(214, 58, 52, 255), 0.1f);
            Mat("Wall", new Color32(250, 247, 238, 255), 0.05f);
            Mat("WallCream", new Color32(244, 236, 214, 255), 0.05f);
            Mat("WallBlueish", new Color32(236, 244, 250, 255), 0.05f);
            Mat("Stone", new Color32(214, 186, 138, 255), 0.05f);
            Mat("StoneDark", new Color32(186, 156, 108, 255), 0.05f);
            Mat("SchoolWall", new Color32(246, 214, 140, 255), 0.05f);
            Mat("Door", new Color32(28, 92, 170, 255), 0.25f);
            Mat("Window", new Color32(46, 120, 196, 255), 0.25f);
            Mat("Frame", new Color32(255, 255, 255, 255), 0.1f);
            Mat("Dome", new Color32(250, 250, 250, 255), 0.2f);
            Mat("DomeGreen", new Color32(60, 150, 90, 255), 0.3f);
            Mat("Bougainvillea", new Color32(214, 52, 138, 255), 0f);
            Mat("Terracotta", new Color32(204, 98, 60, 255), 0.05f);
            Mat("Trunk", new Color32(140, 102, 64, 255), 0f);
            Mat("PalmLeaf", new Color32(80, 156, 64, 255), 0.05f);
            Mat("OliveLeaf", new Color32(120, 158, 92, 255), 0f);
            Mat("Metal", new Color32(70, 80, 92, 255), 0.4f);
            Mat("MetalLight", new Color32(196, 204, 214, 255), 0.5f);
            Mat("Black", new Color32(30, 32, 38, 255), 0.2f);
            Mat("Glass", new Color32(170, 214, 240, 255), 0.85f);
            Mat("Skin", new Color32(222, 170, 128, 255), 0.1f);
            Mat("Hair", new Color32(44, 30, 26, 255), 0.1f);
            Mat("SmockBlue", new Color32(70, 120, 210, 255), 0.1f);
            Mat("SmockPink", new Color32(236, 120, 170, 255), 0.1f);
            Mat("Trousers", new Color32(40, 52, 88, 255), 0.05f);
            Mat("Bag", new Color32(222, 70, 60, 255), 0.1f);
            Mat("Ball", new Color32(255, 255, 255, 255), 0.4f);
            Mat("BallPatch", new Color32(30, 32, 38, 255), 0.4f);
            Mat("Chechia", new Color32(196, 30, 40, 255), 0.1f);

            // car paints (charte: bleu ciel, vert pomme, jaune soleil)
            Mat("Paint_Player", new Color32(92, 186, 240, 255), 0.55f);
            Mat("Paint_Taxi", new Color32(255, 204, 40, 255), 0.55f);
            Mat("Paint_Green", new Color32(122, 196, 74, 255), 0.55f);
            Mat("Paint_White", new Color32(248, 248, 246, 255), 0.55f);
            Mat("Paint_Orange", new Color32(250, 150, 70, 255), 0.55f);
            Mat("Headlight", new Color32(255, 248, 210, 255), 0.6f, new Color(1f, 0.95f, 0.75f) * 0.6f);
            Mat("Taillight", new Color32(220, 40, 40, 255), 0.6f, new Color(0.9f, 0.1f, 0.1f) * 0.5f);

            // traffic lights
            Mat("Lamp_Off", new Color32(48, 52, 58, 255), 0.6f);
            Mat("Lamp_Red", new Color32(255, 60, 50, 255), 0.6f, new Color(1f, 0.15f, 0.1f) * 3f);
            Mat("Lamp_Amber", new Color32(255, 180, 40, 255), 0.6f, new Color(1f, 0.6f, 0.05f) * 3f);
            Mat("Lamp_Green", new Color32(60, 230, 110, 255), 0.6f, new Color(0.1f, 1f, 0.35f) * 3f);
        }

        private static Material Mat(string name, Color color, float smoothness, Color? emission = null)
        {
            string path = $"{MaterialDir}/M_{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = material == null;
            if (isNew)
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
            }
            if (isNew)
                AssetDatabase.CreateAsset(material, path);
            else
                EditorUtility.SetDirty(material);
            materials[name] = material;
            return material;
        }

        private static Material M(string name) => materials[name];

        // ===================================================================== primitive helpers

        private static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, string material, Quaternion? rotation = null, bool collider = false, bool isStatic = true)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rotation ?? Quaternion.identity;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = M(material);
            if (!collider)
                Object.DestroyImmediate(go.GetComponent<Collider>());
            if (isStatic)
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        private static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, string material, bool collider = false, bool isStatic = true)
            => Prim(PrimitiveType.Cube, name, parent, center, size, material, null, collider, isStatic);

        private static GameObject Sphere(string name, Transform parent, Vector3 center, Vector3 size, string material, bool isStatic = true)
            => Prim(PrimitiveType.Sphere, name, parent, center, size, material, null, false, isStatic);

        private static GameObject Cylinder(string name, Transform parent, Vector3 center, float radius, float height, string material, Quaternion? rotation = null, bool isStatic = true)
            => Prim(PrimitiveType.Cylinder, name, parent, center, new Vector3(radius * 2f, height * 0.5f, radius * 2f), material, rotation, false, isStatic);

        /// <summary>Rounded box made with MeshFactory (mesh assets are shared by size).</summary>
        private static GameObject RBox(string name, Transform parent, Vector3 center, Vector3 size, float radius, string material, bool isStatic = false)
        {
            string key = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "RBox_{0:0.00}x{1:0.00}x{2:0.00}_r{3:0.00}", size.x, size.y, size.z, radius).Replace('.', '_');
            if (!meshes.TryGetValue(key, out Mesh mesh))
            {
                mesh = MeshFactory.Save(MeshFactory.RoundedBox(key, size, radius), $"{MeshDir}/{key}.asset");
                meshes[key] = mesh;
            }
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = M(material);
            if (isStatic)
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        private static GameObject Sign(string name, Transform parent, Vector3 localPos, Quaternion rotation, string sprite, float scale)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rotation;
            go.transform.localScale = Vector3.one * scale;
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SignDir}/{sprite}.png");
            if (renderer.sprite == null)
                Debug.LogWarning($"[LevelBuilder] Sign sprite {SignDir}/{sprite}.png not found.");
            return go;
        }

        /// <summary>Rotation that makes a sign face drivers coming along this direction.</summary>
        private static Quaternion FacingTravellers(Vector3 travelDirection) => Quaternion.LookRotation(travelDirection, Vector3.up);

        private static Transform Group(string name, Transform parent, Vector3 position, float yaw = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return go.transform;
        }

        private static float Rand(float min, float max) => min + (float)random.NextDouble() * (max - min);

        private static bool Chance(float p) => random.NextDouble() < p;

        // ===================================================================== ground and roads

        private static void BuildGround(Transform env)
        {
            float length = RoadEnd - RoadStart + 60f;
            Box("Ground", env, new Vector3(0f, -0.06f, (RoadStart + RoadEnd) * 0.5f), new Vector3(200f, 0.1f, length), "Ground", true);
        }

        private static void BuildRoads(Transform env)
        {
            Transform roads = Group("Roads", env, Vector3.zero);
            float length = RoadEnd - RoadStart;
            float mid = (RoadStart + RoadEnd) * 0.5f;
            Box("MainRoad", roads, new Vector3(0f, 0.01f, mid), new Vector3(10f, 0.02f, length), "Asphalt", true);

            // side road (crossroads with the lights)
            Box("SideRoad_West", roads, new Vector3(-37.5f, 0.01f, SideRoadZ), new Vector3(65f, 0.02f, 8f), "Asphalt", true);
            Box("SideRoad_East", roads, new Vector3(37.5f, 0.01f, SideRoadZ), new Vector3(65f, 0.02f, 8f), "Asphalt", true);

            // pavements of the main road (open at the side road)
            float southEnd = SideRoadZ - 7f, northStart = SideRoadZ + 7f;
            foreach (float side in new[] { -1f, 1f })
            {
                string s = side < 0 ? "West" : "East";
                Box($"Sidewalk_{s}_South", roads, new Vector3(side * 6.5f, 0.1f, (RoadStart + southEnd) * 0.5f), new Vector3(3f, 0.2f, southEnd - RoadStart), "Sidewalk", true);
                Box($"Sidewalk_{s}_North", roads, new Vector3(side * 6.5f, 0.1f, (northStart + RoadEnd) * 0.5f), new Vector3(3f, 0.2f, RoadEnd - northStart), "Sidewalk", true);
                Box($"SideSidewalk_{s}_South", roads, new Vector3(side * 37.5f, 0.1f, SideRoadZ - 5.5f), new Vector3(65f, 0.2f, 3f), "Sidewalk", true);
                Box($"SideSidewalk_{s}_North", roads, new Vector3(side * 37.5f, 0.1f, SideRoadZ + 5.5f), new Vector3(65f, 0.2f, 3f), "Sidewalk", true);
            }
        }

        private static void BuildMarkings(Transform env)
        {
            Transform marks = Group("RoadMarkings", env, Vector3.zero);
            const float y = 0.026f;

            // centre dashes
            for (float z = RoadStart + 5f; z < RoadEnd - 3f; z += 10f)
            {
                if (Mathf.Abs(z - SideRoadZ) < 9f || Mathf.Abs(z - SchoolZ) < 5f || Mathf.Abs(z - FinishZ) < 3f)
                    continue;
                Box("Dash", marks, new Vector3(0f, y, z), new Vector3(0.2f, 0.01f, 4f), "Paint");
            }
            // side-road dashes
            for (float x = 10f; x < 68f; x += 8f)
                foreach (float side in new[] { -1f, 1f })
                    Box("SideDash", marks, new Vector3(side * x, y, SideRoadZ), new Vector3(3f, 0.01f, 0.18f), "Paint");

            // edge lines
            foreach (float side in new[] { -1f, 1f })
            {
                Box("EdgeLine", marks, new Vector3(side * 4.75f, y, (RoadStart + SideRoadZ - 7f) * 0.5f), new Vector3(0.15f, 0.01f, SideRoadZ - 7f - RoadStart), "Paint");
                Box("EdgeLine", marks, new Vector3(side * 4.75f, y, (SideRoadZ + 7f + RoadEnd) * 0.5f), new Vector3(0.15f, 0.01f, RoadEnd - SideRoadZ - 7f), "Paint");
            }

            // stop lines
            Box("StopLine_North", marks, new Vector3(2.5f, y, SideRoadZ - 7.7f), new Vector3(4.8f, 0.01f, 0.45f), "Paint");
            Box("StopLine_South", marks, new Vector3(-2.5f, y, SideRoadZ + 7.7f), new Vector3(4.8f, 0.01f, 0.45f), "Paint");
            Box("StopLine_SideEast", marks, new Vector3(-5.6f, y, SideRoadZ - 2f), new Vector3(0.45f, 0.01f, 3.8f), "Paint");
            Box("StopLine_School_North", marks, new Vector3(2.5f, y, SchoolZ - 2.6f), new Vector3(4.8f, 0.01f, 0.45f), "Paint");
            Box("StopLine_School_South", marks, new Vector3(-2.5f, y, SchoolZ + 2.6f), new Vector3(4.8f, 0.01f, 0.45f), "Paint");

            // zebra crossings
            Zebra(marks, SideRoadZ - 5.5f, 2f);
            Zebra(marks, SideRoadZ + 5.5f, 2f);
            Zebra(marks, SchoolZ, 3f);

            // finish line (checkered)
            for (int i = 0; i < 20; i++)
                for (int j = 0; j < 2; j++)
                    if ((i + j) % 2 == 0)
                        Box("Finish", marks, new Vector3(-4.75f + i * 0.5f, y, FinishZ + 0.25f - j * 0.5f), new Vector3(0.5f, 0.01f, 0.5f), "Paint");

            // red / white painted kerbs near the crossroads and the school (common in Tunisia)
            foreach (var range in new[] { new Vector2(10f, SideRoadZ - 7f), new Vector2(SideRoadZ + 7f, 50f), new Vector2(SchoolZ - 14f, SchoolZ + 14f) })
            {
                int k = 0;
                for (float z = range.x; z < range.y; z += 1f, k++)
                    foreach (float side in new[] { -1f, 1f })
                        Box("Kerb", marks, new Vector3(side * 5.12f, 0.205f, z + 0.5f), new Vector3(0.24f, 0.012f, 1f), k % 2 == 0 ? "PaintRed" : "Paint");
            }
        }

        private static void Zebra(Transform parent, float z, float length)
        {
            for (float x = -4.2f; x <= 4.21f; x += 1.2f)
                Box("Zebra", parent, new Vector3(x, 0.026f, z), new Vector3(0.6f, 0.01f, length), "Paint");
        }

        private static void BuildBoundaries(Transform env)
        {
            Transform walls = Group("RoadEdges", env, Vector3.zero);
            void Wall(string name, Vector3 center, Vector3 size)
            {
                GameObject go = Box(name, walls, center, size, "Paint", true, false);
                Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
                Object.DestroyImmediate(go.GetComponent<MeshFilter>());
                go.layer = 2; // Ignore Raycast: the other cars' sensors don't see these walls
                go.AddComponent<Boundary>();
            }
            foreach (float side in new[] { -1f, 1f })
            {
                Wall("Edge", new Vector3(side * 5.35f, 1f, (RoadStart + RoadEnd) * 0.5f), new Vector3(0.4f, 2f, RoadEnd - RoadStart));
            }
            Wall("End_South", new Vector3(0f, 1f, RoadStart + 0.5f), new Vector3(11f, 2f, 0.4f));
            Wall("End_North", new Vector3(0f, 1f, RoadEnd - 0.5f), new Vector3(11f, 2f, 0.4f));
        }

        // ===================================================================== houses

        private static bool InFreeZone(float side, float z0, float z1)
        {
            bool Overlaps(float a, float b) => z1 > a && z0 < b;
            if (Overlaps(SideRoadZ - 9f, SideRoadZ + 9f)) return false;
            if (Overlaps(FinishZ - 5f, FinishZ + 5f)) return false;
            if (side > 0f && Overlaps(SchoolZ - 1f, SchoolZ + 30f)) return false;   // school
            if (side > 0f && Overlaps(BallZ - 26f, BallZ + 18f)) return false;      // garden
            if (side < 0f && Overlaps(186f, 214f)) return false;                     // mosque
            return true;
        }

        private static void BuildHouses(Transform env)
        {
            Transform houses = Group("Houses", env, Vector3.zero);
            string[] shops = { "shop_bakery", "shop_cafe", "shop_pharmacy", "shop_grocery" };
            int shopIndex = 0;
            foreach (float side in new[] { -1f, 1f })
            {
                float z = RoadStart + 2f;
                int n = 0;
                while (z < RoadEnd - 4f)
                {
                    float width = Rand(7f, 10f);
                    if (!InFreeZone(side, z, z + width))
                    {
                        z += 2f;
                        continue;
                    }
                    bool shop = n % 4 == 1 && shopIndex < 8;
                    House(houses, side, z + width * 0.5f, width, shop ? shops[shopIndex++ % shops.Length] : null);
                    z += width + Rand(0.6f, 2.2f);
                    n++;
                }
            }
        }

        /// <summary>Lime-washed house with a blue arched door and blue windows (Sidi Bou Said style).</summary>
        private static void House(Transform parent, float side, float zCenter, float width, string shopSign)
        {
            if (shopSign != null && !shopFronts.ContainsKey(shopSign))
                shopFronts[shopSign] = new Vector2(side, zCenter);
            float depth = Rand(9f, 12f);
            bool twoFloors = Chance(0.55f);
            float height = twoFloors ? 6.6f : 3.8f;
            string wall = Chance(0.15f) ? "WallCream" : Chance(0.15f) ? "WallBlueish" : "Wall";
            // local frame: +Z of the house group points to the road (front face at z = 0)
            Transform house = Group("House", parent, new Vector3(side * 8.6f, 0f, zCenter), side < 0f ? 90f : -90f);
            Box("Walls", house, new Vector3(0f, height * 0.5f, -depth * 0.5f), new Vector3(width, height, depth), wall);
            Box("Parapet", house, new Vector3(0f, height + 0.18f, -depth * 0.5f), new Vector3(width + 0.15f, 0.36f, depth + 0.15f), wall);
            if (Chance(0.4f))
                Box("BlueBand", house, new Vector3(0f, height - 0.15f, 0.02f), new Vector3(width, 0.18f, 0.06f), "Door");

            // arched blue door
            float doorX = Rand(-width * 0.3f, width * 0.3f);
            Door(house, new Vector3(doorX, 0f, 0f), shopSign != null ? 1.8f : 1.25f);

            // windows (ground floor)
            float wx = doorX > 0f ? doorX - 2.2f : doorX + 2.2f;
            if (Mathf.Abs(wx) < width * 0.5f - 0.8f)
                Window(house, new Vector3(wx, 1.6f, 0f), false);
            // upper floor
            if (twoFloors)
            {
                int count = width > 8.5f ? 2 : 1;
                for (int i = 0; i < count; i++)
                {
                    float x = count == 1 ? Rand(-1.5f, 1.5f) : (i == 0 ? -width * 0.25f : width * 0.25f);
                    Window(house, new Vector3(x, 4.6f, 0f), Chance(0.5f));
                }
            }
            if (shopSign != null)
            {
                Box("Awning", house, new Vector3(doorX, 2.85f, 0.45f), new Vector3(3.2f, 0.12f, 0.9f), "Door");
                Sign("ShopSign", house, new Vector3(doorX, 3.35f, 0.08f), Quaternion.Euler(0f, 180f, 0f), shopSign, 1.4f);
            }
            if (Chance(0.3f))
                Sphere("Dome", house, new Vector3(Rand(-width * 0.2f, width * 0.2f), height + 0.2f, -depth * 0.5f), new Vector3(2.6f, 2.0f, 2.6f), Chance(0.7f) ? "Dome" : "Door");
            if (Chance(0.35f))
            {
                // bougainvillea falling over a corner of the wall
                float cx = Chance(0.5f) ? -width * 0.5f + 0.6f : width * 0.5f - 0.6f;
                for (int i = 0; i < 5; i++)
                    Sphere("Bougainvillea", house, new Vector3(cx + Rand(-0.7f, 0.7f), height - Rand(0f, 1.6f), Rand(0.1f, 0.4f)), Vector3.one * Rand(0.6f, 1.0f), "Bougainvillea");
            }
            if (Chance(0.4f))
            {
                // terracotta pot with a plant next to the door
                float px = doorX + (Chance(0.5f) ? 1.2f : -1.2f);
                Cylinder("Pot", house, new Vector3(px, 0.2f, 0.45f), 0.28f, 0.45f, "Terracotta");
                Sphere("Plant", house, new Vector3(px, 0.75f, 0.45f), new Vector3(0.75f, 0.7f, 0.75f), "Hedge");
            }
        }

        private static void Door(Transform house, Vector3 at, float doorWidth)
        {
            const float doorHeight = 2.2f;
            Box("DoorFrame", house, at + new Vector3(0f, doorHeight * 0.5f, 0.03f), new Vector3(doorWidth + 0.3f, doorHeight + 0.15f, 0.08f), "StoneDark");
            Box("Door", house, at + new Vector3(0f, doorHeight * 0.5f, 0.09f), new Vector3(doorWidth, doorHeight, 0.08f), "Door");
            Cylinder("DoorArch", house, at + new Vector3(0f, doorHeight, 0.09f), doorWidth * 0.5f, 0.08f, "Door", Quaternion.Euler(90f, 0f, 0f));
            Cylinder("DoorArchFrame", house, at + new Vector3(0f, doorHeight, 0.03f), doorWidth * 0.5f + 0.15f, 0.08f, "StoneDark", Quaternion.Euler(90f, 0f, 0f));
            // black studs (clous) in an arch on the door
            for (int i = 0; i < 7; i++)
            {
                float a = Mathf.PI * i / 6f;
                float r = doorWidth * 0.32f;
                Sphere("Stud", house, at + new Vector3(Mathf.Cos(a) * r, doorHeight - 0.05f + Mathf.Sin(a) * r * 0.9f, 0.15f), Vector3.one * 0.07f, "Black");
            }
        }

        private static void Window(Transform house, Vector3 at, bool mashrabiya)
        {
            Box("WindowFrame", house, at + new Vector3(0f, 0f, 0.03f), new Vector3(1.25f, 1.45f, 0.08f), "Frame");
            Box("Window", house, at + new Vector3(0f, 0f, 0.07f), new Vector3(1.0f, 1.2f, 0.06f), "Window");
            if (mashrabiya)
            {
                // blue wooden balcony box (moucharabieh)
                Box("Mashrabiya", house, at + new Vector3(0f, -0.1f, 0.35f), new Vector3(1.5f, 1.6f, 0.6f), "Door");
                Box("MashrabiyaTop", house, at + new Vector3(0f, 0.78f, 0.4f), new Vector3(1.7f, 0.12f, 0.75f), "Door");
                for (int i = -2; i <= 2; i++)
                    Box("Slat", house, at + new Vector3(i * 0.28f, -0.1f, 0.67f), new Vector3(0.06f, 1.5f, 0.04f), "Frame");
            }
            else
            {
                Box("Shutter_L", house, at + new Vector3(-0.85f, 0f, 0.08f), new Vector3(0.45f, 1.25f, 0.06f), "Window");
                Box("Shutter_R", house, at + new Vector3(0.85f, 0f, 0.08f), new Vector3(0.45f, 1.25f, 0.06f), "Window");
            }
        }

        // ===================================================================== trees

        private static void BuildPalms(Transform env)
        {
            Transform palms = Group("PalmTrees", env, Vector3.zero);
            for (float z = RoadStart + 12f; z < RoadEnd - 8f; z += 20f)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    float zz = z + (side > 0f ? 10f : 0f);
                    if (Mathf.Abs(zz - SideRoadZ) < 10f || Mathf.Abs(zz - SchoolZ) < 6f || Mathf.Abs(zz - FinishZ) < 6f || Mathf.Abs(zz - BallZ) < 8f)
                        continue;
                    Palm(palms, new Vector3(side * 7.4f, 0.2f, zz));
                }
            }
        }

        private static void Palm(Transform parent, Vector3 at)
        {
            Transform palm = Group("Palm", parent, at, Rand(0f, 360f));
            float lean = Rand(-4f, 4f);
            float h = 0f;
            for (int i = 0; i < 6; i++)
            {
                float r = 0.22f - i * 0.015f;
                Cylinder("Trunk", palm, new Vector3(lean * 0.02f * i, h + 0.5f, 0f), r, 1.05f, "Trunk", Quaternion.Euler(0f, 0f, lean));
                h += 0.95f;
            }
            Vector3 top = new Vector3(lean * 0.12f, h + 0.1f, 0f);
            Sphere("Crown", palm, top, Vector3.one * 0.6f, "Trunk");
            for (int i = 0; i < 8; i++)
            {
                float yaw = i * 45f + Rand(-10f, 10f);
                Quaternion rot = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(Rand(18f, 32f), 0f, 0f);
                Vector3 offset = rot * new Vector3(0f, 0f, 1.25f);
                Prim(PrimitiveType.Sphere, "Leaf", palm, top + offset, new Vector3(0.55f, 0.1f, 2.7f), "PalmLeaf", rot);
            }
        }

        private static void OliveTree(Transform parent, Vector3 at)
        {
            Transform tree = Group("OliveTree", parent, at, Rand(0f, 360f));
            Cylinder("Trunk", tree, new Vector3(0f, 0.8f, 0f), 0.18f, 1.6f, "Trunk", Quaternion.Euler(0f, 0f, Rand(-8f, 8f)));
            for (int i = 0; i < 4; i++)
                Sphere("Leaves", tree, new Vector3(Rand(-0.6f, 0.6f), 2.0f + Rand(0f, 0.6f), Rand(-0.6f, 0.6f)), Vector3.one * Rand(1.3f, 1.8f), "OliveLeaf");
        }

        // ===================================================================== school, garden, mosque, gate

        private static void BuildSchool(Transform env)
        {
            Transform school = Group("School", env, new Vector3(0f, 0f, SchoolZ));
            // building (two floors, ochre) facing the road, behind a low wall
            Box("Building", school, new Vector3(19f, 3.6f, 15f), new Vector3(16f, 7.2f, 22f), "SchoolWall");
            Box("Roof", school, new Vector3(19f, 7.35f, 15f), new Vector3(16.3f, 0.3f, 22.3f), "Wall");
            for (int floor = 0; floor < 2; floor++)
                for (int i = 0; i < 5; i++)
                {
                    float z = 6f + i * 4.2f;
                    Box("WindowFrame", school, new Vector3(10.96f, 1.9f + floor * 3.3f, z), new Vector3(0.08f, 1.6f, 1.6f), "Frame");
                    Box("Window", school, new Vector3(10.92f, 1.9f + floor * 3.3f, z), new Vector3(0.08f, 1.35f, 1.35f), "Window");
                }
            Box("Entrance", school, new Vector3(10.95f, 1.3f, 4.5f), new Vector3(0.1f, 2.6f, 2.2f), "Door");
            // low wall with a gate at the crossing
            Box("Wall_North", school, new Vector3(8.4f, 0.7f, 17.5f), new Vector3(0.4f, 1.4f, 25f), "Wall");
            Box("GatePillar_A", school, new Vector3(8.4f, 1.3f, 2.4f), new Vector3(0.7f, 2.6f, 0.7f), "Stone");
            Box("GatePillar_B", school, new Vector3(8.4f, 1.3f, 5.2f), new Vector3(0.7f, 2.6f, 0.7f), "Stone");
            Box("GateArch", school, new Vector3(8.4f, 2.75f, 3.8f), new Vector3(0.7f, 0.5f, 3.5f), "Stone");
            Sign("SchoolSign", school, new Vector3(8.0f, 3.55f, 3.8f), Quaternion.Euler(0f, 90f, 0f), "banner_school", 1.3f);
            // Tunisian flag
            Cylinder("FlagPole", school, new Vector3(9.4f, 4f, 9f), 0.06f, 8f, "MetalLight");
            Transform flag = Group("Flag", school, new Vector3(9.4f, 7.3f, 9f), -90f);
            Sign("FlagCloth", flag, new Vector3(0.75f, 0f, 0f), Quaternion.identity, "flag_tunisia", 0.75f);
            flag.gameObject.AddComponent<FlagWave>();
        }

        private static void BuildGarden(Transform env)
        {
            Transform garden = Group("Garden", env, new Vector3(0f, 0f, BallZ));
            Box("Lawn", garden, new Vector3(19f, 0.08f, -4f), new Vector3(20f, 0.16f, 42f), "Lawn", true);
            // hedge along the pavement, open at the gate
            Box("Hedge_South", garden, new Vector3(8.7f, 0.55f, -15f), new Vector3(0.9f, 1.1f, 20f), "Hedge");
            Box("Hedge_North", garden, new Vector3(8.7f, 0.55f, 8f), new Vector3(0.9f, 1.1f, 16f), "Hedge");
            Sign("GardenSign", garden, new Vector3(8.4f, 2.3f, -4f), Quaternion.Euler(0f, 90f, 0f), "banner_garden", 1.2f);
            Cylinder("SignPole_A", garden, new Vector3(8.6f, 0.9f, -5.3f), 0.06f, 1.8f, "MetalLight");
            Cylinder("SignPole_B", garden, new Vector3(8.6f, 0.9f, -2.7f), 0.06f, 1.8f, "MetalLight");
            OliveTree(garden, new Vector3(14f, 0.15f, -14f));
            OliveTree(garden, new Vector3(22f, 0.15f, -8f));
            OliveTree(garden, new Vector3(16f, 0.15f, 9f));
            OliveTree(garden, new Vector3(25f, 0.15f, 4f));
            Palm(garden, new Vector3(27f, 0.15f, -18f));
            // bench
            Box("BenchSeat", garden, new Vector3(12f, 0.5f, 4f), new Vector3(0.6f, 0.1f, 2.2f), "Door");
            Box("BenchBack", garden, new Vector3(12.3f, 0.85f, 4f), new Vector3(0.1f, 0.6f, 2.2f), "Door");
            // children playing (they are why the ball comes out)
            Kid(garden, "PlayingKid_A", new Vector3(11.5f, 0.16f, 1.5f), -100f, "SmockPink", false);
            Kid(garden, "PlayingKid_B", new Vector3(13.5f, 0.16f, -1.5f), -70f, "SmockBlue", false);
        }

        private static void BuildMosque(Transform env)
        {
            Transform mosque = Group("Mosque", env, new Vector3(0f, 0f, 200f));
            Box("PrayerHall", mosque, new Vector3(-20f, 3.2f, 0f), new Vector3(16f, 6.4f, 22f), "Wall");
            Box("HallParapet", mosque, new Vector3(-20f, 6.55f, 0f), new Vector3(16.3f, 0.3f, 22.3f), "Wall");
            Sphere("Dome", mosque, new Vector3(-20f, 6.6f, 0f), new Vector3(7f, 5f, 7f), "DomeGreen");
            for (int i = 0; i < 4; i++)
            {
                float z = -7.5f + i * 5f;
                Door(Group("Arch", mosque, new Vector3(-11.95f, 0f, z), 90f), Vector3.zero, 1.6f);
            }
            // square minaret (Tunisian style), next to the road
            Transform minaret = Group("Minaret", mosque, new Vector3(-13.5f, 0f, -9.5f));
            Box("Tower", minaret, new Vector3(0f, 8f, 0f), new Vector3(3.4f, 16f, 3.4f), "Stone");
            Box("Band", minaret, new Vector3(0f, 12.5f, 0f), new Vector3(3.6f, 0.4f, 3.6f), "StoneDark");
            Box("Gallery", minaret, new Vector3(0f, 16.2f, 0f), new Vector3(3.9f, 0.5f, 3.9f), "StoneDark");
            for (int i = 0; i < 4; i++)
            {
                Quaternion r = Quaternion.Euler(0f, i * 90f, 0f);
                Box("Crenel", minaret, r * new Vector3(0f, 16.75f, 1.75f), new Vector3(3.6f, 0.6f, 0.25f), "Stone");
            }
            Box("Lantern", minaret, new Vector3(0f, 18f, 0f), new Vector3(2f, 3f, 2f), "Stone");
            Sphere("LanternDome", minaret, new Vector3(0f, 19.5f, 0f), new Vector3(2f, 1.6f, 2f), "DomeGreen");
            Cylinder("Finial", minaret, new Vector3(0f, 20.6f, 0f), 0.06f, 1.2f, "MetalLight");
        }

        private static void BuildGate(Transform env)
        {
            Transform gate = Group("Bab_Gate", env, new Vector3(0f, 0f, FinishZ));
            foreach (float side in new[] { -1f, 1f })
            {
                Box("Pillar", gate, new Vector3(side * 7.6f, 3.5f, 0f), new Vector3(2.4f, 7f, 2.6f), "Stone");
                Box("PillarTop", gate, new Vector3(side * 7.6f, 7.2f, 0f), new Vector3(2.8f, 0.4f, 3f), "StoneDark");
                // city walls (sour) going away from the gate
                Box("CityWall", gate, new Vector3(side * 28f, 3f, 0f), new Vector3(38f, 6f, 2f), "Stone");
                for (float x = 10f; x < 47f; x += 2f)
                    Box("Crenel", gate, new Vector3(side * x, 6.4f, 0f), new Vector3(1f, 0.8f, 2f), "Stone");
            }
            // arch over the road, made of stones on a half ellipse
            const int stones = 15;
            for (int i = 0; i < stones; i++)
            {
                float a0 = Mathf.PI * i / stones, a1 = Mathf.PI * (i + 1) / stones, a = (a0 + a1) * 0.5f;
                Vector3 p = new Vector3(Mathf.Cos(a) * 7.6f, 7f + Mathf.Sin(a) * 3.2f, 0f);
                Vector3 tangent = new Vector3(-Mathf.Sin(a) * 7.6f, Mathf.Cos(a) * 3.2f, 0f).normalized;
                float length = Vector3.Distance(new Vector3(Mathf.Cos(a0) * 7.6f, Mathf.Sin(a0) * 3.2f, 0f), new Vector3(Mathf.Cos(a1) * 7.6f, Mathf.Sin(a1) * 3.2f, 0f)) + 0.15f;
                Prim(PrimitiveType.Cube, "ArchStone", gate, p, new Vector3(length, 1.1f, 2.6f), i % 2 == 0 ? "Stone" : "StoneDark", Quaternion.FromToRotation(Vector3.right, tangent));
            }
            Box("ArchWall", gate, new Vector3(0f, 10.6f, 0f), new Vector3(17.6f, 1.6f, 2.4f), "Stone");
            Sign("FinishSign", gate, new Vector3(0f, 8.6f, -1.35f), Quaternion.identity, "banner_finish", 2.4f);
        }

        // ===================================================================== signs

        private static void BuildRoadSigns(Transform env)
        {
            Transform signs = Group("RoadSigns", env, Vector3.zero);
            SignPost(signs, new Vector3(6.9f, 0.2f, SchoolZ - 22f), Vector3.forward, "sign_school");
            SignPost(signs, new Vector3(6.9f, 0.2f, SchoolZ - 4f), Vector3.forward, "sign_crossing");
            SignPost(signs, new Vector3(-6.9f, 0.2f, SchoolZ + 4f), Vector3.back, "sign_crossing");
            SignPost(signs, new Vector3(6.9f, 0.2f, BallZ - 30f), Vector3.forward, "sign_children");
            SignPost(signs, new Vector3(6.9f, 0.2f, SideRoadZ - 26f), Vector3.forward, "sign_lights");
        }

        private static void SignPost(Transform parent, Vector3 at, Vector3 travel, string sprite)
        {
            Transform post = Group("Sign_" + sprite, parent, at);
            Cylinder("Pole", post, new Vector3(0f, 1.25f, 0f), 0.05f, 2.5f, "MetalLight");
            // the sign faces the drivers: it looks back along their travel direction
            Sign("Plate", post, new Vector3(0f, 2.65f, 0f), Quaternion.LookRotation(travel, Vector3.up), sprite, 0.6f);
        }

        // ===================================================================== traffic lights

        private static TrafficLight BuildTrafficLights(Transform traffic)
        {
            Transform root = Group("TrafficLights", traffic, new Vector3(0f, 0f, SideRoadZ));
            var light = root.gameObject.AddComponent<TrafficLight>();
            var main = new List<TrafficLight.LampSet>();
            var side = new List<TrafficLight.LampSet>();

            // northbound (the player): mast arm over the lane + small light on the pole
            main.Add(Mast(root, new Vector3(6.2f, 0.2f, -8.3f), 1f, Vector3.forward, out TrafficLight.LampSet poleNorth));
            main.Add(poleNorth);
            // southbound (oncoming cars)
            main.Add(Mast(root, new Vector3(-6.2f, 0.2f, 8.3f), -1f, Vector3.back, out TrafficLight.LampSet poleSouth));
            main.Add(poleSouth);
            // side road, eastbound
            Transform sidePole = Group("Pole_SideEast", root, new Vector3(-6.9f, 0.2f, -4.6f));
            Cylinder("Pole", sidePole, new Vector3(0f, 1.6f, 0f), 0.09f, 3.2f, "Metal", null, false);
            side.Add(Head(sidePole, new Vector3(0f, 3.5f, 0f), Vector3.right));
            // side road, westbound
            Transform sidePoleWest = Group("Pole_SideWest", root, new Vector3(6.9f, 0.2f, 4.6f));
            Cylinder("Pole", sidePoleWest, new Vector3(0f, 1.6f, 0f), 0.09f, 3.2f, "Metal", null, false);
            side.Add(Head(sidePoleWest, new Vector3(0f, 3.5f, 0f), Vector3.left));

            light.Configure(main.ToArray(), side.ToArray(), M("Lamp_Off"), M("Lamp_Red"), M("Lamp_Amber"), M("Lamp_Green"));
            return light;
        }

        /// <summary>Pole on the pavement with an arm over the lane; the heads face drivers travelling along "travel".</summary>
        private static TrafficLight.LampSet Mast(Transform parent, Vector3 at, float side, Vector3 travel, out TrafficLight.LampSet poleHead)
        {
            Transform mast = Group("Mast", parent, at);
            Cylinder("Pole", mast, new Vector3(0f, 2.8f, 0f), 0.12f, 5.6f, "Metal", null, false);
            Box("Arm", mast, new Vector3(-side * 2.1f, 5.5f, 0f), new Vector3(4.4f, 0.14f, 0.14f), "Metal", false, false);
            poleHead = Head(mast, new Vector3(0f, 3.2f, -travel.z * 0.2f), travel);
            return Head(mast, new Vector3(-side * 3.7f, 4.85f, 0f), travel);
        }

        private static TrafficLight.LampSet Head(Transform parent, Vector3 at, Vector3 travel)
        {
            // lamps on the side that drivers see: opposite to their travel direction
            Transform head = Group("Head", parent, at);
            head.localRotation = Quaternion.LookRotation(-travel, Vector3.up);
            RBox("Box", head, Vector3.zero, new Vector3(0.5f, 1.3f, 0.4f), 0.12f, "Black");
            var set = new TrafficLight.LampSet();
            set.red = Sphere("Red", head, new Vector3(0f, 0.38f, 0.17f), Vector3.one * 0.3f, "Lamp_Off", false).GetComponent<Renderer>();
            set.amber = Sphere("Amber", head, new Vector3(0f, 0f, 0.17f), Vector3.one * 0.3f, "Lamp_Off", false).GetComponent<Renderer>();
            set.green = Sphere("Green", head, new Vector3(0f, -0.38f, 0.17f), Vector3.one * 0.3f, "Lamp_Off", false).GetComponent<Renderer>();
            for (int i = -1; i <= 1; i++)
                Box("Visor", head, new Vector3(0f, i * 0.38f + 0.16f, 0.27f), new Vector3(0.36f, 0.04f, 0.18f), "Black", false, false);
            return set;
        }

        // ===================================================================== people

        private static Pedestrian BuildSchoolKid(Transform parent)
        {
            Transform walk = Group("SchoolCrossing", parent, new Vector3(0f, 0f, SchoolZ));
            Transform kerb = Group("Kerb_West", walk, new Vector3(-5.5f, 0.2f, 0f));
            Transform farKerb = Group("Kerb_East", walk, new Vector3(5.5f, 0.2f, 0f));
            Transform exit = Group("SchoolGate", walk, new Vector3(9.5f, 0.2f, 3.8f));
            GameObject kid = Kid(walk, "SchoolKid", new Vector3(-7.4f, 0.2f, -3f), 0f, "SmockBlue", true);
            var pedestrian = kid.AddComponent<Pedestrian>();
            pedestrian.Configure(kerb, farKerb, exit,
                kid.transform.Find("Leg_L"), kid.transform.Find("Leg_R"),
                kid.transform.Find("Upper/Arm_L"), kid.transform.Find("Upper/Arm_R"), kid.transform.Find("Upper"));
            var capsule = kid.AddComponent<CapsuleCollider>();
            capsule.isTrigger = true;
            capsule.center = new Vector3(0f, 0.75f, 0f);
            capsule.height = 1.5f;
            capsule.radius = 0.35f;
            return pedestrian;
        }

        /// <summary>A child about 1.4 m tall, in the school smock Tunisian pupils wear (blue or pink).</summary>
        private static GameObject Kid(Transform parent, string name, Vector3 at, float yaw, string smock, bool backpack)
        {
            Transform root = Group(name, parent, at, yaw);
            foreach (float s in new[] { -1f, 1f })
            {
                Transform leg = Group(s < 0 ? "Leg_L" : "Leg_R", root, new Vector3(s * 0.1f, 0.55f, 0f));
                Prim(PrimitiveType.Capsule, "Leg", leg, new Vector3(0f, -0.27f, 0f), new Vector3(0.16f, 0.28f, 0.16f), "Trousers", null, false, false);
                Sphere("Shoe", leg, new Vector3(0f, -0.52f, 0.05f), new Vector3(0.17f, 0.1f, 0.24f), "Black", false);
            }
            Transform upper = Group("Upper", root, Vector3.zero);
            Prim(PrimitiveType.Capsule, "Smock", upper, new Vector3(0f, 0.82f, 0f), new Vector3(0.44f, 0.34f, 0.32f), smock, null, false, false);
            Sphere("Head", upper, new Vector3(0f, 1.27f, 0f), Vector3.one * 0.36f, "Skin", false);
            Sphere("Hair", upper, new Vector3(0f, 1.34f, -0.03f), new Vector3(0.38f, 0.3f, 0.38f), "Hair", false);
            Sphere("Eye_L", upper, new Vector3(-0.07f, 1.29f, 0.16f), Vector3.one * 0.05f, "Black", false);
            Sphere("Eye_R", upper, new Vector3(0.07f, 1.29f, 0.16f), Vector3.one * 0.05f, "Black", false);
            if (backpack)
                RBox("Backpack", upper, new Vector3(0f, 0.88f, -0.22f), new Vector3(0.34f, 0.4f, 0.16f), 0.06f, "Bag");
            foreach (float s in new[] { -1f, 1f })
            {
                Transform arm = Group(s < 0 ? "Arm_L" : "Arm_R", upper, new Vector3(s * 0.27f, 1.02f, 0f));
                Prim(PrimitiveType.Capsule, "Sleeve", arm, new Vector3(0f, -0.2f, 0f), new Vector3(0.12f, 0.22f, 0.12f), smock, null, false, false);
                Sphere("Hand", arm, new Vector3(0f, -0.43f, 0f), Vector3.one * 0.11f, "Skin", false);
            }
            return root.gameObject;
        }

        // ===================================================================== rules

        private static CrosswalkRule BuildRules(Transform rules, TrafficLight light, Pedestrian kid, Dictionary<string, GameObject> models)
        {
            // red light (US 2.1)
            Transform redLine = Group("StopLine_Lights_North", rules, new Vector3(2.5f, 0f, SideRoadZ - 7.7f));
            var redRule = redLine.gameObject.AddComponent<RedLightRule>();
            redRule.Configure(light, redLine);
            Transform southLine = Group("StopLine_Lights_South", rules, new Vector3(-2.5f, 0f, SideRoadZ + 7.7f), 180f);
            southLine.gameObject.AddComponent<StopLine>().Configure(light, false, null, 2.6f);
            Transform eastLine = Group("StopLine_Lights_SideEast", rules, new Vector3(-5.6f, 0f, SideRoadZ - 2f), 90f);
            eastLine.gameObject.AddComponent<StopLine>().Configure(light, true, null, 2.2f);
            Transform westLine = Group("StopLine_Lights_SideWest", rules, new Vector3(5.6f, 0f, SideRoadZ + 2f), -90f);
            westLine.gameObject.AddComponent<StopLine>().Configure(light, true, null, 2.2f);

            // zebra crossing (US 2.2)
            Transform crossing = Group("SchoolCrossing", rules, new Vector3(0f, 0f, SchoolZ));
            var crosswalk = crossing.gameObject.AddComponent<CrosswalkRule>();
            Transform playerLine = Group("StopLine_School_North", crossing, new Vector3(2.5f, 0f, -2.6f));
            crosswalk.Configure(kid, playerLine);
            Transform schoolSouth = Group("StopLine_School_South", rules, new Vector3(-2.5f, 0f, SchoolZ + 2.6f), 180f);
            schoolSouth.gameObject.AddComponent<StopLine>().Configure(null, false, crosswalk, 2.6f);

            // ball behind the parked louage (US 2.3)
            Transform ballEvent = Group("BallEvent", rules, new Vector3(2.5f, 0f, BallZ));
            GameObject van = (GameObject)PrefabUtility.InstantiatePrefab(models["Louage"], ballEvent);
            van.name = "ParkedLouage";
            van.transform.localPosition = new Vector3(3.0f, 0.05f, -4f);
            var vanBox = van.AddComponent<BoxCollider>();
            vanBox.center = new Vector3(0f, 1.05f, 0f);
            vanBox.size = new Vector3(2.0f, 2.0f, 5.1f);

            GameObject ballGo = Prim(PrimitiveType.Sphere, "Ball", ballEvent, new Vector3(2.9f, 0.37f, -0.7f), Vector3.one * 0.7f, "Ball", null, true, false);
            for (int i = 0; i < 6; i++)
            {
                Vector3 dir = Quaternion.Euler(i * 60f, i * 37f, 0f) * Vector3.forward;
                Sphere("Patch", ballGo.transform, dir * 0.47f, Vector3.one * 0.22f, "BallPatch", false);
            }
            var ballBody = ballGo.AddComponent<Rigidbody>();
            ballBody.isKinematic = true;
            ballBody.mass = 0.4f;
            var ball = ballGo.AddComponent<Ball>();
            Transform target = Group("BallTarget", ballEvent, new Vector3(-7.2f, 0.37f, -0.7f));
            GameObject warning = Sign("WarningSign", ballEvent, new Vector3(4.6f, 3.7f, -2f), Quaternion.identity, "sign_warning", 0.9f);
            warning.AddComponent<Billboard>();
            ballEvent.gameObject.AddComponent<BallEvent>().Configure(ball, target, warning);

            // finish under the gate
            Group("FinishLine", rules, new Vector3(0f, 0f, FinishZ)).gameObject.AddComponent<FinishLine>();
            return crosswalk;
        }

        // ===================================================================== cars

        private static Dictionary<string, GameObject> CreateVehicleModels()
        {
            var models = new Dictionary<string, GameObject>
            {
                ["Car_Player"] = SaveModel("Car_Player", Car("Paint_Player", false)),
                ["Taxi"] = SaveModel("Taxi", Car("Paint_Taxi", true)),
                ["Car_Green"] = SaveModel("Car_Green", Car("Paint_Green", false)),
                ["Car_Orange"] = SaveModel("Car_Orange", Car("Paint_Orange", false)),
                ["Louage"] = SaveModel("Louage", Louage()),
            };
            return models;
        }

        private static GameObject SaveModel(string name, GameObject model)
        {
            model.name = name;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(model, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(model);
            return prefab;
        }

        /// <summary>Small rounded cartoon car, about 4 m long. Origin on the ground, front = +Z.</summary>
        private static GameObject Car(string paint, bool taxi)
        {
            var root = new GameObject("Car").transform;
            RBox("Body", root, new Vector3(0f, 0.64f, 0f), new Vector3(1.8f, 0.74f, 3.9f), 0.32f, paint);
            RBox("Cabin", root, new Vector3(0f, 1.24f, -0.25f), new Vector3(1.56f, 0.62f, 2.1f), 0.26f, "Glass");
            RBox("Roof", root, new Vector3(0f, 1.57f, -0.25f), new Vector3(1.62f, 0.14f, 2.0f), 0.07f, paint);
            RBox("Bumper_F", root, new Vector3(0f, 0.38f, 1.93f), new Vector3(1.84f, 0.22f, 0.28f), 0.1f, "Metal");
            RBox("Bumper_B", root, new Vector3(0f, 0.38f, -1.93f), new Vector3(1.84f, 0.22f, 0.28f), 0.1f, "Metal");
            foreach (float x in new[] { -0.82f, 0.82f })
                foreach (float z in new[] { -1.25f, 1.25f })
                    Wheel(root, new Vector3(x, 0.36f, z), 0.36f);
            foreach (float x in new[] { -0.58f, 0.58f })
            {
                Sphere("Headlight", root, new Vector3(x, 0.72f, 1.9f), new Vector3(0.34f, 0.3f, 0.16f), "Headlight", false);
                Sphere("Taillight", root, new Vector3(x, 0.74f, -1.92f), new Vector3(0.3f, 0.2f, 0.1f), "Taillight", false);
            }
            if (taxi)
            {
                RBox("TaxiSign", root, new Vector3(0f, 1.78f, -0.2f), new Vector3(0.8f, 0.3f, 0.36f), 0.08f, "Paint_White");
                Sign("TaxiText_F", root, new Vector3(0f, 1.78f, -0.01f), Quaternion.Euler(0f, 180f, 0f), "taxi_sign", 0.33f);
                Sign("TaxiText_B", root, new Vector3(0f, 1.78f, -0.39f), Quaternion.identity, "taxi_sign", 0.33f);
            }
            return root.gameObject;
        }

        /// <summary>White louage (Tunisian shared minibus) with its red stripe. Front = +Z.</summary>
        private static GameObject Louage()
        {
            var root = new GameObject("Louage").transform;
            RBox("Body", root, new Vector3(0f, 1.1f, 0f), new Vector3(2.0f, 1.55f, 5.0f), 0.38f, "Paint_White");
            RBox("Windows", root, new Vector3(0f, 1.45f, -0.1f), new Vector3(2.04f, 0.55f, 4.2f), 0.2f, "Glass");
            RBox("Windscreen", root, new Vector3(0f, 1.42f, 2.2f), new Vector3(1.7f, 0.6f, 0.65f), 0.2f, "Glass");
            RBox("Stripe", root, new Vector3(0f, 0.95f, 0f), new Vector3(2.05f, 0.16f, 4.7f), 0.06f, "PaintRed");
            RBox("Bumper_F", root, new Vector3(0f, 0.42f, 2.5f), new Vector3(2.0f, 0.24f, 0.3f), 0.1f, "Metal");
            RBox("Bumper_B", root, new Vector3(0f, 0.42f, -2.5f), new Vector3(2.0f, 0.24f, 0.3f), 0.1f, "Metal");
            RBox("RoofRack", root, new Vector3(0f, 1.95f, -0.3f), new Vector3(1.6f, 0.1f, 3.2f), 0.04f, "Metal");
            foreach (float x in new[] { -0.9f, 0.9f })
                foreach (float z in new[] { -1.7f, 1.7f })
                    Wheel(root, new Vector3(x, 0.4f, z), 0.4f);
            foreach (float x in new[] { -0.65f, 0.65f })
            {
                Sphere("Headlight", root, new Vector3(x, 0.72f, 2.47f), new Vector3(0.34f, 0.3f, 0.16f), "Headlight", false);
                Sphere("Taillight", root, new Vector3(x, 0.8f, -2.48f), new Vector3(0.3f, 0.22f, 0.1f), "Taillight", false);
            }
            return root.gameObject;
        }

        private static void Wheel(Transform parent, Vector3 at, float radius)
        {
            Cylinder("Wheel", parent, at, radius, 0.28f, "Black", Quaternion.Euler(0f, 0f, 90f), false);
            Cylinder("Hub", parent, at + new Vector3(Mathf.Sign(at.x) * 0.01f, 0f, 0f), radius * 0.5f, 0.29f, "MetalLight", Quaternion.Euler(0f, 0f, 90f), false);
        }

        private static void BuildOtherCars(Transform traffic, Dictionary<string, GameObject> models, TrafficLight light, CrosswalkRule crosswalk)
        {
            Transform cars = Group("OtherCars", traffic, Vector3.zero);
            string[] oncomingModels = { "Taxi", "Louage", "Car_Green", "Taxi", "Car_Orange" };
            float[] startZ = { 205f, 135f, 62f, -15f, 168f };
            var oncoming = new List<NpcCar>();
            for (int i = 0; i < oncomingModels.Length; i++)
            {
                NpcCar car = NpcCarFrom(cars, models[oncomingModels[i]], $"Oncoming_{i + 1}_{oncomingModels[i]}",
                    new Vector3(-2.5f, 0.05f, startZ[i]), new Vector3(-2.5f, 0.05f, RoadEnd - 3f), new Vector3(-2.5f, 0.05f, RoadStart + 3f),
                    oncomingModels[i] == "Louage");
                oncoming.Add(car);
            }
            NpcCar cross = NpcCarFrom(cars, models["Car_Orange"], "Crossing_Car_Orange",
                new Vector3(-30f, 0.05f, SideRoadZ - 2f), new Vector3(-68f, 0.05f, SideRoadZ - 2f), new Vector3(68f, 0.05f, SideRoadZ - 2f), false);
            NpcCar crossWest = NpcCarFrom(cars, models["Car_Green"], "Crossing_Car_Green",
                new Vector3(40f, 0.05f, SideRoadZ + 2f), new Vector3(68f, 0.05f, SideRoadZ + 2f), new Vector3(-68f, 0.05f, SideRoadZ + 2f), false);
            NpcCar crossTaxi = NpcCarFrom(cars, models["Taxi"], "Crossing_Taxi",
                new Vector3(-62f, 0.05f, SideRoadZ - 2f), new Vector3(-68f, 0.05f, SideRoadZ - 2f), new Vector3(68f, 0.05f, SideRoadZ - 2f), false);
            traffic.gameObject.AddComponent<TrafficDirector>().Configure(oncoming.ToArray(), new[] { cross, crossWest, crossTaxi });
        }

        private static NpcCar NpcCarFrom(Transform parent, GameObject model, string name, Vector3 position, Vector3 pathStart, Vector3 pathEnd, bool isLouage)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.transform.rotation = Quaternion.LookRotation(pathEnd - pathStart, Vector3.up);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            visual.name = "Model";
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, isLouage ? 1.05f : 0.8f, 0f);
            box.size = isLouage ? new Vector3(2.0f, 2.0f, 5.1f) : new Vector3(1.8f, 1.3f, 4.0f);
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            var car = root.AddComponent<NpcCar>();
            car.Configure(pathStart, pathEnd, 6f);
            return car;
        }

        // ===================================================================== player, camera, light

        private static void SetUpPlayer(GameObject model)
        {
            GameObject car = GameObject.Find("PlayerCar");
            if (car == null)
            {
                GameObject player = GameObject.Find("Player");
                if (player == null)
                    player = new GameObject("Player");
                car = new GameObject("PlayerCar");
                car.transform.SetParent(player.transform, false);
            }
            for (int i = car.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(car.transform.GetChild(i).gameObject);
            car.transform.SetPositionAndRotation(new Vector3(2.5f, 0.05f, -45f), Quaternion.identity);

            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, car.transform);
            visual.name = "Model";

            var body = car.GetComponent<Rigidbody>();
            if (body == null)
                body = car.AddComponent<Rigidbody>();
            body.mass = 1000f;
            body.useGravity = true;
            var box = car.GetComponent<BoxCollider>();
            if (box == null)
                box = car.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.85f, 0f);
            box.size = new Vector3(1.8f, 1.3f, 3.95f);
            string matPath = MaterialDir + "/Car_NoFriction.physicMaterial";
            var slippery = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(matPath);
            if (slippery == null)
            {
                slippery = new PhysicsMaterial("Car_NoFriction") { dynamicFriction = 0f, staticFriction = 0f, frictionCombine = PhysicsMaterialCombine.Minimum, bounciness = 0f };
                AssetDatabase.CreateAsset(slippery, matPath);
            }
            box.sharedMaterial = slippery;
            if (car.GetComponent<CarController>() == null) car.AddComponent<CarController>();
            if (car.GetComponent<CrashFeedback>() == null) car.AddComponent<CrashFeedback>();
            if (car.GetComponent<ThreatSensor>() == null) car.AddComponent<ThreatSensor>();
            if (car.GetComponent<EngineSound>() == null) car.AddComponent<EngineSound>();
        }

        private static void SetUpCameraAndLight()
        {
            Camera cam = Camera.main;
            GameObject car = GameObject.Find("PlayerCar");
            if (cam != null && car != null)
            {
                var follow = cam.GetComponent<CameraFollow>();
                if (follow == null)
                    follow = cam.gameObject.AddComponent<CameraFollow>();
                follow.Target = car.transform;
                cam.farClipPlane = 400f;
            }

            GameObject sun = GameObject.Find("Directional Light");
            if (sun != null)
            {
                var light = sun.GetComponent<Light>();
                light.color = new Color(1f, 0.95f, 0.86f);
                light.intensity = 1.35f;
                light.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            }

            string skyPath = MaterialDir + "/Sky_Tunisia.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            sky.SetFloat("_SunSize", 0.05f);
            sky.SetFloat("_AtmosphereThickness", 0.8f);
            sky.SetColor("_SkyTint", new Color(0.35f, 0.62f, 1f));
            sky.SetColor("_GroundColor", new Color(0.85f, 0.8f, 0.7f));
            sky.SetFloat("_Exposure", 1.35f);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.72f, 0.82f, 0.98f);
            RenderSettings.ambientEquatorColor = new Color(0.86f, 0.82f, 0.74f);
            RenderSettings.ambientGroundColor = new Color(0.52f, 0.47f, 0.4f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.8f, 0.88f, 0.97f);
            RenderSettings.fogStartDistance = 90f;
            RenderSettings.fogEndDistance = 300f;
        }

        // ===================================================================== utils

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
