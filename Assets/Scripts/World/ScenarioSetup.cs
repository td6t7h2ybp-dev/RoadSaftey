using System;
using System.Collections.Generic;
using UnityEngine;
using RoadSafety.AI.Pedestrians;

namespace RoadSafety.World
{
    /// <summary>
    /// Puts the evaluation pedestrians (PNJ behaviours 1, 2 and 4) into the level:
    ///   1 - a distracted pedestrian with a smartphone, at the traffic lights (before the crossroads);
    ///   2 - an unpredictable child, just after the crossroads;
    ///   4 - a parked bus with a pedestrian hidden in front of it.
    /// Saved in Main.unity by RoadSafety > AI > Build NPC scenarios (1, 2, 4) (which also attaches
    /// their Behavior Designer trees). If the scene has none, they are built when Play starts.
    /// </summary>
    public static class ScenarioSetup
    {
        public const string RootName = "NPC Scenarios";

        // the road runs along +Z, the player drives north in the lane x ≈ +2.5
        public const float LightsCrossZ = 24.6f;  // between the player's red-light stop line (22.3) and the crossroads (30)
        public const float ChildCrossZ = 35.6f;   // just after the crossroads
        public const float ChildOnlyZ = 50f;      // child's crossing (behaviour 2)
        public const float BusZ = 68f;            // bus centre (10 m long, parked on the player's side)
        private const float KerbX = 7.2f;

        /// <summary>Editor only: saves a generated material as an asset (so the scene keeps it).</summary>
        public static Func<Material, string, Material> SaveMaterial;

        private static Dictionary<string, Material> mats;

        /// <summary>Play mode: builds the scenarios if Main.unity does not have them.</summary>
        public static void EnsureInScene()
        {
            // the pedestrians and their Behavior Designer trees are authored in the scene
            // (RoadSafety > AI > Build NPC scenarios); nothing is generated at runtime
            if (!Application.isPlaying || GameObject.Find(RootName) != null) return;
            if (UnityEngine.Object.FindFirstObjectByType<TrafficLight>() == null) return;
            Debug.LogWarning("[Scenarios] Main.unity has no NPC scenarios: run RoadSafety > AI > Build NPC scenarios (1, 2, 4).");
        }

        public static GameObject Build()
        {
            mats = new Dictionary<string, Material>();
            var root = new GameObject(RootName).transform;

            // ---------- 1: distracted pedestrian at the lights
            Transform lights = Group("1_DistractedPedestrian", root, new Vector3(0f, 0f, LightsCrossZ));
            Transform east = Group("Kerb_East", lights, new Vector3(KerbX, 0.2f, 0f));
            Transform west = Group("Kerb_West", lights, new Vector3(-KerbX, 0.2f, 0f));
            GameObject adult = Person(lights, "Scenario_PhoneUser", new Vector3(KerbX + 0.6f, 0.2f, 0.4f), -90f, "Shirt_Teal", "Trousers_Dark", 1f);
            adult.AddComponent<PedestrianAgent>().Configure(PedestrianKind.DistractedPhone, east, west);

            // ---------- 3: slow old man after the crossroads (replaces the unpredictable child)
            Transform oldCross = Group("3_OldMan", root, new Vector3(0f, 0f, ChildCrossZ));
            Transform ow = Group("Kerb_West", oldCross, new Vector3(-KerbX, 0.2f, 0f));
            Transform oe = Group("Kerb_East", oldCross, new Vector3(KerbX, 0.2f, 0f));
            GameObject oldMan = Person(oldCross, "Scenario_OldMan", new Vector3(-KerbX - 0.6f, 0.2f, 0f), 90f, "Shirt_Teal", "Trousers_Dark", 1f);
            oldMan.AddComponent<PedestrianAgent>().Configure(PedestrianKind.OldMan, ow, oe);

            // ---------- 2: unpredictable child, between the old man and the bus
            Transform childCross = Group("2_UnpredictableChild", root, new Vector3(0f, 0f, ChildOnlyZ));
            Transform ce = Group("Kerb_East", childCross, new Vector3(KerbX, 0.2f, 0f));
            Transform cw = Group("Kerb_West", childCross, new Vector3(-KerbX, 0.2f, 0f));
            GameObject child = Person(childCross, "Scenario_Child", new Vector3(KerbX + 0.6f, 0.2f, 0f), -90f, "Smock_Pink", "Trousers_Blue", 0.65f);
            child.AddComponent<PedestrianAgent>().Configure(PedestrianKind.UnpredictableChild, ce, cw);

            // ---------- 4: bus and the pedestrian hidden in front of it
            Transform busArea = Group("4_PedestrianBehindBus", root, new Vector3(0f, 0f, BusZ));
            Bus(busArea, new Vector3(4.9f, 0f, 0f));
            Transform hide = Group("HideSpot_FrontOfBus", busArea, new Vector3(4.4f, 0.2f, 5.9f));
            Transform far = Group("Kerb_West", busArea, new Vector3(-KerbX, 0.2f, 5.9f));
            Transform exit = Group("Exit", busArea, new Vector3(-KerbX - 2.5f, 0.2f, 9.5f));
            GameObject hidden = Person(busArea, "Scenario_BusPedestrian", hide.localPosition, -90f, "Shirt_Orange", "Trousers_Blue", 1f);
            hidden.AddComponent<PedestrianAgent>().Configure(PedestrianKind.BehindTheBus, hide, far, hide, exit);

            mats = null;
            return root.gameObject;
        }

        // ------------------------------------------------------------------ builders

        private static Transform Group(string name, Transform parent, Vector3 localPos, float yaw = 0f)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return t;
        }

        /// <summary>A person with the primitive rig RiggedNpc reads (Leg_L/R, Upper, Upper/Arm_L/R).</summary>
        private static GameObject Person(Transform parent, string name, Vector3 at, float yaw, string top, string bottom, float scale)
        {
            Transform root = Group(name, parent, at, yaw);
            root.localScale = Vector3.one * scale;
            foreach (float s in new[] { -1f, 1f })
            {
                Transform leg = Group(s < 0 ? "Leg_L" : "Leg_R", root, new Vector3(s * 0.12f, 0.85f, 0f));
                Prim(PrimitiveType.Capsule, "Leg", leg, new Vector3(0f, -0.42f, 0f), new Vector3(0.18f, 0.43f, 0.18f), bottom, new Color(0.15f, 0.17f, 0.22f));
            }
            Transform upper = Group("Upper", root, Vector3.zero);
            Prim(PrimitiveType.Capsule, "Torso", upper, new Vector3(0f, 1.2f, 0f), new Vector3(0.48f, 0.4f, 0.32f), top, TopColor(top));
            Prim(PrimitiveType.Sphere, "Head", upper, new Vector3(0f, 1.72f, 0f), Vector3.one * 0.3f, "Skin", new Color(0.78f, 0.58f, 0.44f));
            foreach (float s in new[] { -1f, 1f })
            {
                Transform arm = Group(s < 0 ? "Arm_L" : "Arm_R", upper, new Vector3(s * 0.3f, 1.48f, 0f));
                Prim(PrimitiveType.Capsule, "Sleeve", arm, new Vector3(0f, -0.3f, 0f), new Vector3(0.13f, 0.32f, 0.13f), top, TopColor(top));
            }
            return root.gameObject;
        }

        private static Color TopColor(string id)
        {
            switch (id)
            {
                case "Shirt_Teal": return new Color(0.1f, 0.55f, 0.58f);
                case "Smock_Pink": return new Color(0.95f, 0.55f, 0.7f);
                case "Shirt_Orange": return new Color(0.95f, 0.5f, 0.15f);
                default: return Color.gray;
            }
        }

        /// <summary>A Tunis city bus (white with a green band), parked facing north. Solid: cars can't drive through.</summary>
        private static void Bus(Transform parent, Vector3 at)
        {
            Transform bus = Group("ParkedBus", parent, at);
            var col = bus.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.65f, 0f);
            col.size = new Vector3(2.5f, 3f, 10.5f);

            Color white = new Color(0.94f, 0.94f, 0.92f), green = new Color(0.1f, 0.5f, 0.3f), glass = new Color(0.12f, 0.16f, 0.2f), black = new Color(0.08f, 0.08f, 0.08f);
            Prim(PrimitiveType.Cube, "Body", bus, new Vector3(0f, 1.75f, 0f), new Vector3(2.5f, 2.7f, 10.5f), "Bus_White", white);
            Prim(PrimitiveType.Cube, "Band", bus, new Vector3(0f, 1.05f, 0f), new Vector3(2.52f, 0.35f, 10.52f), "Bus_Green", green);
            Prim(PrimitiveType.Cube, "Windows", bus, new Vector3(0f, 2.35f, 0f), new Vector3(2.54f, 0.9f, 9.6f), "Bus_Glass", glass);
            Prim(PrimitiveType.Cube, "Windscreen", bus, new Vector3(0f, 2.1f, 5.24f), new Vector3(2.3f, 1.4f, 0.06f), "Bus_Glass", glass);
            Prim(PrimitiveType.Cube, "Bumper", bus, new Vector3(0f, 0.55f, 5.28f), new Vector3(2.5f, 0.3f, 0.08f), "Bus_Black", black);
            foreach (float z in new[] { -3.6f, 3.4f })
                foreach (float x in new[] { -1.2f, 1.2f })
                {
                    var w = Prim(PrimitiveType.Cylinder, "Wheel", bus, new Vector3(x, 0.5f, z), new Vector3(1f, 0.15f, 1f), "Bus_Black", black);
                    w.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }
        }

        private static Transform Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, string matId, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = Mat(matId, color);
            return go.transform;
        }

        private static Material Mat(string id, Color color)
        {
            if (mats.TryGetValue(id, out Material m)) return m;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            m = new Material(shader) { name = "M_Scenario_" + id };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.25f);
            if (SaveMaterial != null) m = SaveMaterial(m, m.name);
            mats[id] = m;
            return m;
        }
    }
}
