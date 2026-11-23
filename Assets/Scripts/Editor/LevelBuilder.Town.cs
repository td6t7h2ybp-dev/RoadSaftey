using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using RoadSafety.AI;
using RoadSafety.UI;
using RoadSafety.World;

namespace RoadSafety.EditorTools
{
    /// <summary>
    /// Life in the street: townspeople run by behaviour trees (Townsperson.cs), and the HUD saved
    /// in the scene so it is visible without pressing Play.
    ///   - walkers on both pavements (they stop to chat with neighbours, rest, walk on)
    ///   - friends talking in front of the bakery, a shop and the mosque
    ///   - men drinking tea at the café terrace
    ///   - in the garden: two people on the bench, two mothers watching the children,
    ///     a bambalouni (Tunisian doughnut) seller with customers
    /// Nobody walks on the road, so they never get in the way of the lessons.
    /// </summary>
    public static partial class LevelBuilder
    {
        private enum Headwear
        {
            Hair,
            GreyHair,
            Chechia,
            Scarf,
            Cap
        }

        private struct Body
        {
            public Transform Root;
            public Transform LegL, LegR, ArmL, ArmR, Upper;
            public GameObject Bubble;
        }

        private const float PavementTop = 0.2f;
        private const float WalkLineX = 7.4f; // wide pavements (6 m): people stroll in the middle

        private static Dictionary<string, Vector2> shopFronts;

        private static void CreateTownMaterials()
        {
            Mat("Cloth_Cream", new Color32(238, 228, 204, 255), 0.05f);
            Mat("Cloth_Olive", new Color32(126, 140, 82, 255), 0.05f);
            Mat("Cloth_Mustard", new Color32(222, 172, 62, 255), 0.05f);
            Mat("Cloth_Teal", new Color32(42, 148, 150, 255), 0.05f);
            Mat("Cloth_Burgundy", new Color32(150, 44, 62, 255), 0.05f);
            Mat("Cloth_Denim", new Color32(72, 102, 152, 255), 0.05f);
            Mat("Cloth_Grey", new Color32(128, 130, 140, 255), 0.05f);
            Mat("Cloth_Brown", new Color32(122, 84, 54, 255), 0.05f);
            Mat("Cloth_Lilac", new Color32(170, 140, 206, 255), 0.05f);
            Mat("Scarf_White", new Color32(246, 244, 238, 255), 0.05f);
            Mat("Scarf_Rose", new Color32(232, 150, 168, 255), 0.05f);
            Mat("Skin_Tan", new Color32(198, 146, 104, 255), 0.1f);
            Mat("Skin_Dark", new Color32(150, 104, 72, 255), 0.1f);
            Mat("Hair_Grey", new Color32(170, 170, 172, 255), 0.1f);
            Mat("Wood", new Color32(170, 116, 70, 255), 0.15f);
            Mat("Tea", new Color32(176, 70, 26, 255), 0.6f);
            Mat("Dough", new Color32(232, 172, 84, 255), 0.2f);
        }

        // ===================================================================== townspeople

        private static void BuildTownsfolk(Transform pedestrians)
        {
            Transform town = Group("Townsfolk", pedestrians, Vector3.zero);
            BuildWalkers(town);
            BuildPavementGroups(town);
            BuildCafe(town);
            BuildGardenLife(town);
        }

        private static void BuildWalkers(Transform town)
        {
            Transform walkers = Group("Walkers", town, Vector3.zero);
            // (side, from z, to z, start fraction) - the pavements stop at the side road (z 23..37)
            var routes = new (float side, float a, float b, float t)[]
            {
                (-1f, -62f, 16f, 0.2f), (-1f, -62f, 16f, 0.75f), (-1f, 44f, 94f, 0.4f),
                (-1f, 108f, 182f, 0.1f), (-1f, 120f, 230f, 0.6f),
                (1f, -62f, 16f, 0.5f), (1f, 42f, 95f, 0.3f), (1f, 42f, 95f, 0.85f),
                (1f, 104f, 146f, 0.5f), (1f, 192f, 232f, 0.4f),
                // a busy street: the pavements are never empty, wherever the child looks
                (-1f, -62f, 16f, 0.45f), (-1f, -62f, 16f, 0.95f), (-1f, 44f, 94f, 0.08f),
                (-1f, 44f, 94f, 0.8f), (-1f, 108f, 182f, 0.55f), (-1f, 108f, 182f, 0.88f),
                (-1f, 120f, 230f, 0.25f), (-1f, 150f, 230f, 0.85f),
                (1f, -62f, 16f, 0.15f), (1f, -62f, 16f, 0.88f), (1f, 42f, 95f, 0.6f),
                (1f, 104f, 146f, 0.12f), (1f, 104f, 146f, 0.82f), (1f, 150f, 232f, 0.15f),
                (1f, 192f, 232f, 0.72f),
            };
            string[] tops = { "Cloth_Teal", "Cloth_Mustard", "Cloth_Burgundy", "Cloth_Cream", "Cloth_Olive", "Cloth_Lilac", "Cloth_Denim", "Cloth_Brown" };
            string[] skins = { "Skin", "Skin_Tan", "Skin_Dark" };
            for (int i = 0; i < routes.Length; i++)
            {
                var r = routes[i];
                Vector3 a = new Vector3(r.side * WalkLineX, PavementTop, r.a);
                Vector3 b = new Vector3(r.side * WalkLineX, PavementTop, r.b);
                Vector3 start = Vector3.Lerp(a, b, r.t);
                Headwear head = i % 4 == 0 ? Headwear.Scarf : i % 4 == 1 ? Headwear.Chechia : i % 4 == 2 ? Headwear.Cap : Headwear.Hair;
                bool robe = head == Headwear.Scarf || (head == Headwear.Chechia && Chance(0.5f));
                string scarf = Chance(0.5f) ? "Scarf_White" : "Scarf_Rose";
                Body body = Person(walkers, $"Walker_{i + 1}", start, Chance(0.5f) ? 0f : 180f,
                    tops[i % tops.Length], Chance(0.5f) ? "Trousers" : "Cloth_Grey", head, scarf, robe, skins[i % skins.Length], 0f);
                Configure(body, Townsperson.Kind.Walker, a, b, null, 0, 1);
            }
        }

        private static void BuildPavementGroups(Transform town)
        {
            Transform groups = Group("FriendsTalking", town, Vector3.zero);

            // two neighbours in front of the bakery
            Vector2 bakery = ShopFront("shop_bakery", -1f, 60f);
            TalkingGroup(groups, "Group_Bakery", new Vector3(bakery.x * 9.6f, PavementTop, bakery.y + 1.5f), 0.65f, new[]
            {
                (Headwear.Scarf, "Cloth_Lilac", "Scarf_White", true, "Skin"),
                (Headwear.Scarf, "Cloth_Teal", "Scarf_Rose", true, "Skin_Tan"),
            });

            // three men in front of the grocery
            Vector2 grocery = ShopFront("shop_grocery", 1f, 70f);
            TalkingGroup(groups, "Group_Grocery", new Vector3(grocery.x * 9.6f, PavementTop, grocery.y - 1.5f), 0.7f, new[]
            {
                (Headwear.Chechia, "Cloth_Cream", "", true, "Skin_Tan"),
                (Headwear.Cap, "Cloth_Denim", "", false, "Skin"),
                (Headwear.GreyHair, "Cloth_Brown", "", false, "Skin_Dark"),
            });

            // old men on the little square in front of the mosque
            TalkingGroup(groups, "Group_Mosque", new Vector3(-10.2f, 0f, 194f), 0.75f, new[]
            {
                (Headwear.Chechia, "Cloth_Cream", "", true, "Skin_Tan"),
                (Headwear.Chechia, "Scarf_White", "", true, "Skin"),
                (Headwear.GreyHair, "Cloth_Olive", "", true, "Skin_Dark"),
            });
        }

        private static void BuildCafe(Transform town)
        {
            Vector2 cafe = ShopFront("shop_cafe", -1f, 52f);
            Transform terrace = Group("CafeTerrace", town, new Vector3(cafe.x * 9.7f, PavementTop, cafe.y));
            string[] tops = { "Cloth_Cream", "Cloth_Grey", "Cloth_Burgundy", "Cloth_Olive" };
            string[] skins = { "Skin_Tan", "Skin", "Skin_Dark", "Skin" };
            int n = 0;
            foreach (float dz in new[] { -1.5f, 1.5f })
            {
                Transform table = Group(dz < 0f ? "Table_A" : "Table_B", terrace, new Vector3(0f, 0f, dz));
                Cylinder("Top", table, new Vector3(0f, 0.74f, 0f), 0.36f, 0.04f, "Paint_White");
                Cylinder("Leg", table, new Vector3(0f, 0.37f, 0f), 0.05f, 0.72f, "Metal");
                Cylinder("Foot", table, new Vector3(0f, 0.02f, 0f), 0.22f, 0.04f, "Metal");
                Cylinder("TeaGlass_A", table, new Vector3(0.1f, 0.81f, -0.15f), 0.035f, 0.1f, "Tea");
                Cylinder("TeaGlass_B", table, new Vector3(-0.08f, 0.81f, 0.16f), 0.035f, 0.1f, "Tea");
                for (int k = 0; k < 2; k++)
                {
                    float side = k == 0 ? -1f : 1f;
                    float yaw = side < 0f ? 0f : 180f; // facing the table
                    Transform chair = Group("Chair_" + (k + 1), table, new Vector3(0f, 0f, side * 0.72f), yaw);
                    Box("Seat", chair, new Vector3(0f, 0.445f, -0.05f), new Vector3(0.42f, 0.05f, 0.42f), "Wood");
                    Box("Back", chair, new Vector3(0f, 0.72f, -0.25f), new Vector3(0.42f, 0.5f, 0.05f), "Wood");
                    Box("Base", chair, new Vector3(0f, 0.21f, -0.05f), new Vector3(0.06f, 0.42f, 0.06f), "Metal");
                    Body man = Person(terrace, $"CafeCustomer_{n + 1}", chair.position, yaw, tops[n], "Trousers",
                        n % 2 == 0 ? Headwear.Chechia : Headwear.GreyHair, "", false, skins[n], 0.47f);
                    Configure(man, Townsperson.Kind.Seated, Vector3.zero, Vector3.zero, table, k, 2);
                    n++;
                }
            }
        }

        private static void BuildGardenLife(Transform town)
        {
            Transform garden = Group("GardenLife", town, Vector3.zero);
            const float lawn = 0.16f;

            // two friends on the bench (bench seat at x 12, z BallZ + 4, its back towards +x)
            for (int i = 0; i < 2; i++)
            {
                Vector3 at = new Vector3(11.95f, lawn, BallZ + 3.45f + i * 1.1f);
                Body friend = Person(garden, "BenchFriend_" + (i + 1), at, -90f,
                    i == 0 ? "Cloth_Mustard" : "Cloth_Teal", "Cloth_Grey",
                    i == 0 ? Headwear.GreyHair : Headwear.Scarf, "Scarf_White", false, i == 0 ? "Skin_Tan" : "Skin", 0.39f);
                Configure(friend, Townsperson.Kind.Seated, Vector3.zero, Vector3.zero, null, i, 2);
            }

            // two mothers watching the children play
            TalkingGroup(garden, "Group_Mothers", new Vector3(16f, lawn, BallZ - 3f), 0.6f, new[]
            {
                (Headwear.Scarf, "Cloth_Burgundy", "Scarf_Rose", true, "Skin"),
                (Headwear.Hair, "Cloth_Lilac", "", false, "Skin_Tan"),
            });

            // bambalouni seller (Tunisian doughnuts) with a customer and her son
            Transform cart = Group("BambalouniCart", garden, new Vector3(17f, lawn, BallZ + 12f));
            RBox("Cart", cart, new Vector3(0f, 0.85f, 0f), new Vector3(1.5f, 0.75f, 0.85f), 0.12f, "Paint_White", true);
            Box("Stripe", cart, new Vector3(0f, 0.85f, 0.43f), new Vector3(1.5f, 0.16f, 0.02f), "PaintRed");
            foreach (float s in new[] { -1f, 1f })
                Cylinder("Wheel", cart, new Vector3(s * 0.5f, 0.28f, 0.47f), 0.26f, 0.1f, "Black", Quaternion.Euler(90f, 0f, 0f));
            Cylinder("ParasolPole", cart, new Vector3(0f, 1.8f, 0f), 0.03f, 1.9f, "MetalLight");
            Sphere("Parasol", cart, new Vector3(0f, 2.75f, 0f), new Vector3(2f, 0.4f, 2f), "PaintRed");
            for (int i = 0; i < 5; i++)
                Cylinder("Bambalouni", cart, new Vector3(-0.5f + i * 0.25f, 1.27f, -0.1f + (i % 2) * 0.18f), 0.11f, 0.05f, "Dough");

            Vector3 centre = cart.position;
            Body seller = Person(garden, "BambalouniSeller", centre + new Vector3(0f, 0f, -0.95f), 0f,
                "Scarf_White", "Trousers", Headwear.Cap, "", false, "Skin_Tan", 0f);
            Body customer = Person(garden, "BambalouniCustomer", centre + new Vector3(-0.2f, 0f, 1.1f), 180f,
                "Cloth_Olive", "Cloth_Grey", Headwear.Scarf, "Scarf_White", true, "Skin", 0f);
            Transform cartCentre = Group("TalkCentre", garden, centre);
            Configure(seller, Townsperson.Kind.GroupTalker, Vector3.zero, Vector3.zero, cartCentre, 0, 2);
            Configure(customer, Townsperson.Kind.GroupTalker, Vector3.zero, Vector3.zero, cartCentre, 1, 2);
            Kid(garden, "HungryKid", centre + new Vector3(0.55f, 0f, 1.05f), 180f, "SmockBlue", false);
        }

        private static Vector2 ShopFront(string shop, float fallbackSide, float fallbackZ)
        {
            Vector2 front = shopFronts != null && shopFronts.TryGetValue(shop, out Vector2 found) ? found : new Vector2(fallbackSide, fallbackZ);
            // keep the school kid's corner free
            if (Mathf.Abs(front.y - SchoolZ) < 7f)
                front.y = SchoolZ - 9f;
            return front;
        }

        /// <summary>People standing in a small circle around "at" (on the pavement: in a line along the houses).</summary>
        private static void TalkingGroup(Transform parent, string name, Vector3 at, float radius,
            (Headwear head, string top, string scarf, bool robe, string skin)[] people)
        {
            Transform group = Group(name, parent, at);
            Transform centre = Group("TalkCentre", group, Vector3.zero);
            for (int i = 0; i < people.Length; i++)
            {
                float angle = (i / (float)people.Length) * Mathf.PI * 2f + 0.5f;
                Vector3 offset = people.Length == 2
                    ? new Vector3(0f, 0f, i == 0 ? -radius : radius)               // face to face along the pavement
                    : new Vector3(Mathf.Cos(angle) * radius * 0.6f, 0f, Mathf.Sin(angle) * radius);
                Vector3 position = at + offset;
                float yaw = Quaternion.LookRotation(-offset.normalized, Vector3.up).eulerAngles.y;
                var p = people[i];
                Body body = Person(group, $"Talker_{i + 1}", position, yaw, p.top, "Trousers", p.head, p.scarf, p.robe, p.skin, 0f);
                Configure(body, Townsperson.Kind.GroupTalker, Vector3.zero, Vector3.zero, centre, i, people.Length);
            }
        }

        private static void Configure(Body body, Townsperson.Kind kind, Vector3 a, Vector3 b, Transform lookAt, int turn, int groupSize)
        {
            var person = body.Root.gameObject.AddComponent<Townsperson>();
            person.Configure(kind, a, b, lookAt, turn, groupSize, body.LegL, body.LegR, body.ArmL, body.ArmR, body.Upper, body.Bubble);
        }

        /// <summary>
        /// A grown-up about 1.8 m tall, in the same rounded style as the children.
        /// seatHeight > 0 builds a seated person (thighs forward, shins down) whose hips are at that height.
        /// </summary>
        private static Body Person(Transform parent, string name, Vector3 worldPosition, float yaw, string top, string bottom,
            Headwear head, string scarf, bool robe, string skin, float seatHeight)
        {
            bool seated = seatHeight > 0f;
            var root = new GameObject(name).transform;
            root.SetParent(parent, true);
            root.position = worldPosition;
            root.rotation = Quaternion.Euler(0f, yaw, 0f);
            var body = new Body { Root = root };
            float hip = seated ? seatHeight : 0.9f;

            foreach (float s in new[] { -1f, 1f })
            {
                Transform leg = Group(s < 0f ? "Leg_L" : "Leg_R", root, new Vector3(s * 0.11f, hip, 0f));
                if (seated)
                {
                    Prim(PrimitiveType.Capsule, "Thigh", leg, new Vector3(0f, 0f, 0.2f), new Vector3(0.19f, 0.24f, 0.19f), bottom, Quaternion.Euler(90f, 0f, 0f), false, false);
                    Prim(PrimitiveType.Capsule, "Shin", leg, new Vector3(0f, -hip * 0.5f, 0.42f), new Vector3(0.17f, hip * 0.5f, 0.17f), bottom, null, false, false);
                    Sphere("Shoe", leg, new Vector3(0f, -hip + 0.05f, 0.48f), new Vector3(0.18f, 0.1f, 0.28f), "Black", false);
                }
                else
                {
                    Prim(PrimitiveType.Capsule, "Leg", leg, new Vector3(0f, -0.43f, 0f), new Vector3(0.19f, 0.43f, 0.19f), bottom, null, false, false);
                    Sphere("Shoe", leg, new Vector3(0f, -0.85f, 0.06f), new Vector3(0.19f, 0.1f, 0.3f), "Black", false);
                    if (s < 0f) body.LegL = leg;
                    else body.LegR = leg;
                }
            }

            Transform upper = Group("Upper", root, new Vector3(0f, hip - 0.9f, 0f));
            body.Upper = upper;
            Prim(PrimitiveType.Capsule, "Body", upper, new Vector3(0f, 1.22f, 0f), new Vector3(0.5f, 0.37f, 0.34f), top, null, false, false);
            if (robe && !seated)
                Prim(PrimitiveType.Capsule, "Robe", upper, new Vector3(0f, 0.76f, 0f), new Vector3(0.54f, 0.44f, 0.42f), top, null, false, false);
            Sphere("Head", upper, new Vector3(0f, 1.67f, 0f), Vector3.one * 0.3f, skin, false);
            Sphere("Eye_L", upper, new Vector3(-0.06f, 1.69f, 0.135f), Vector3.one * 0.045f, "Black", false);
            Sphere("Eye_R", upper, new Vector3(0.06f, 1.69f, 0.135f), Vector3.one * 0.045f, "Black", false);
            switch (head)
            {
                case Headwear.Scarf:
                    Sphere("Scarf", upper, new Vector3(0f, 1.69f, -0.045f), new Vector3(0.35f, 0.37f, 0.33f), string.IsNullOrEmpty(scarf) ? "Scarf_White" : scarf, false);
                    break;
                case Headwear.Chechia:
                    Sphere("Hair", upper, new Vector3(0f, 1.72f, -0.03f), new Vector3(0.31f, 0.2f, 0.31f), "Hair", false);
                    Cylinder("Chechia", upper, new Vector3(0f, 1.82f, 0f), 0.125f, 0.13f, "Chechia", null, false);
                    break;
                case Headwear.Cap:
                    Sphere("Hair", upper, new Vector3(0f, 1.72f, -0.03f), new Vector3(0.31f, 0.2f, 0.31f), "Hair", false);
                    Sphere("Cap", upper, new Vector3(0f, 1.77f, 0f), new Vector3(0.32f, 0.17f, 0.32f), "PaintRed", false);
                    Box("Visor", upper, new Vector3(0f, 1.77f, 0.17f), new Vector3(0.24f, 0.03f, 0.14f), "PaintRed", false, false);
                    break;
                case Headwear.GreyHair:
                    Sphere("Hair", upper, new Vector3(0f, 1.73f, -0.03f), new Vector3(0.31f, 0.21f, 0.31f), "Hair_Grey", false);
                    break;
                default:
                    Sphere("Hair", upper, new Vector3(0f, 1.73f, -0.03f), new Vector3(0.32f, 0.24f, 0.32f), "Hair", false);
                    break;
            }
            foreach (float s in new[] { -1f, 1f })
            {
                Transform arm = Group(s < 0f ? "Arm_L" : "Arm_R", upper, new Vector3(s * 0.3f, 1.46f, 0f));
                Prim(PrimitiveType.Capsule, "Sleeve", arm, new Vector3(0f, -0.27f, 0f), new Vector3(0.13f, 0.27f, 0.13f), top, null, false, false);
                Sphere("Hand", arm, new Vector3(0f, -0.57f, 0f), Vector3.one * 0.11f, skin, false);
                if (s < 0f) body.ArmL = arm;
                else body.ArmR = arm;
            }

            GameObject bubble = Sign("TalkBubble", root, new Vector3(0.35f, hip - 0.9f + 2.25f, 0f), Quaternion.identity, "talk_bubble", 0.55f);
            bubble.AddComponent<Billboard>();
            bubble.SetActive(false);
            body.Bubble = bubble;
            return body;
        }

        // ===================================================================== HUD in the scene

        /// <summary>
        /// Saves the HUD under "UI" in the scene, so it can be seen (and checked) in the Game view
        /// without pressing Play. GameplayBootstrap uses it instead of building a new one.
        /// </summary>
        private static void BuildHUD(Scene scene)
        {
            GameObject uiRoot = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == "UI")
                    uiRoot = root;
            if (uiRoot == null)
                uiRoot = new GameObject("UI");

            foreach (HUDController old in uiRoot.GetComponentsInChildren<HUDController>(true))
                Object.DestroyImmediate(old.gameObject);
            HUDController hud = HUDBuilder.Build();
            hud.transform.SetParent(uiRoot.transform, false);

            if (Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_LEGACY_INPUT_MANAGER
                eventSystem.AddComponent<StandaloneInputModule>();
#elif ENABLE_INPUT_SYSTEM
                eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().AssignDefaultActions();
#endif
                eventSystem.transform.SetParent(uiRoot.transform, false);
            }
        }

        [MenuItem("RoadSafety/UI/Rebuild HUD in the scene")]
        private static void RebuildHUDOnly()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            BuildHUD(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[LevelBuilder] HUD saved in the scene under UI.");
        }
    }
}
