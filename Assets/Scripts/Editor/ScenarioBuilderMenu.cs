using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using RoadSafety.AI.Pedestrians;
using RoadSafety.World;
using Unity.AI.Navigation;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace RoadSafety.EditorTools
{
    /// <summary>
    /// RoadSafety > AI > Build NPC scenarios (1, 2, 4)
    /// Puts the three evaluation pedestrians in the open scene and gives each one a Behavior Designer
    /// tree made of Behavior Designer composites (Selector, Sequence, Utility Selector, conditional
    /// aborts) and the custom pedestrian tasks (PedestrianTasks.cs). The trees are saved in the scene:
    /// from then on they are ordinary Behavior Designer trees, viewed and edited in
    /// Tools > Behavior Designer > Editor. Running this menu again rebuilds them from scratch.
    /// </summary>
    public static class ScenarioBuilderMenu
    {
        private const string MatDir = "Assets/_Project/Materials/Generated/Scenario";

        [MenuItem("RoadSafety/AI/Build NPC scenarios (1, 2, 3, 4)")]
        public static void Build()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[Scenarios] Stop Play mode first.");
                return;
            }
            MethodInfo save = FindSave();
            if (save == null)
            {
                Debug.LogError("[Scenarios] Behavior Designer's serializer was not found.");
                return;
            }

            GameObject old = GameObject.Find(ScenarioSetup.RootName);
            if (old != null) Object.DestroyImmediate(old);

            if (AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.DeleteAsset(MatDir);
            string parent = "Assets";
            foreach (string part in MatDir.Substring("Assets/".Length).Split('/'))
            {
                if (!AssetDatabase.IsValidFolder(parent + "/" + part)) AssetDatabase.CreateFolder(parent, part);
                parent += "/" + part;
            }
            ScenarioSetup.SaveMaterial = (m, id) =>
            {
                AssetDatabase.CreateAsset(m, $"{MatDir}/{id}.mat");
                return m;
            };
            GameObject root;
            try { root = ScenarioSetup.Build(); }
            finally { ScenarioSetup.SaveMaterial = null; }
            AssetDatabase.SaveAssets();

            BakePedestrianNavMesh(root);

            foreach (PedestrianAgent agent in root.GetComponentsInChildren<PedestrianAgent>(true))
            {
                // NavMesh only: the pedestrian walks with a NavMeshAgent
                var nav = agent.GetComponent<NavMeshAgent>();
                if (nav == null) nav = agent.gameObject.AddComponent<NavMeshAgent>();
                nav.radius = 0.3f; nav.height = 1.8f; nav.baseOffset = 0f;
                nav.acceleration = 30f; nav.angularSpeed = 540f; nav.stoppingDistance = 0.05f;
                nav.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

                var bt = agent.GetComponent<BehaviorTree>();
                if (bt == null) bt = agent.gameObject.AddComponent<BehaviorTree>();
                Task tree;
                string treeName;
                switch (agent.kind)
                {
                    case PedestrianKind.DistractedPhone: tree = DistractedTree(); treeName = "Distracted pedestrian (1)"; break;
                    case PedestrianKind.UnpredictableChild: tree = ChildTree(); treeName = "Unpredictable child (2)"; break;
                    case PedestrianKind.OldMan: tree = OldManTree(); treeName = "Old man (3)"; break;
                    default: tree = BusTree(); treeName = "Pedestrian behind the bus (4)"; break;
                }
                Write(bt, tree, treeName);
                save.Invoke(null, new object[] { bt.GetBehaviorSource() });
                EditorUtility.SetDirty(bt);
            }

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Scenarios] 4 pedestrians added with NavMeshAgents, the pedestrian NavMesh baked and their Behavior Designer trees saved. Now run RoadSafety > AI > Bake rigged people into the scene for their characters.");
        }

        // ===================================================================== the trees

        // 1 - "Le piéton distrait": smartphone in hand, does not look before crossing.
        //     Conditions: feu vert + piéton dans zone + aléatoire + vitesse player > 50 + temps écoulé
        private static Task DistractedTree() =>
            Sel("Distracted pedestrian",
                Abort(Seq("Danger: a car is coming at me",
                    new IsOnRoad(),
                    new CarAboutToHitMe(),
                    new MoveToPoint { point = PedestrianPoint.StartKerb, speed = 3.4f, startled = true, FriendlyName = "Jump back to the kerb" },
                    new LeaveRoad { swapKerbs = false }), AbortType.LowerPriority),
                Seq("Crossing",
                    new IsOnRoad(),
                    new MoveToPoint { point = PedestrianPoint.FarKerb, speed = 1.15f, FriendlyName = "Walk across, eyes on the phone" },
                    new LeaveRoad { swapKerbs = true }),
                Abort(Seq("Steps out without looking",
                    new LightIs { color = LightColor.Green, FriendlyName = "Feu vert?" },
                    new PedestrianInZone { FriendlyName = "Piéton dans zone?" },
                    new RandomChance { chance = 0.5f, FriendlyName = "Aléatoire?" },
                    new PlayerSpeedAbove { percent = 50f, FriendlyName = "Vitesse player > 50?" },
                    new WaitedAtLeast { seconds = 4f, FriendlyName = "Temps écoulé?" },
                    new StepOutWithoutLooking { FriendlyName = "Step out WITHOUT looking" }), AbortType.LowerPriority),
                Util("On the kerb (fonctions d'utilité)",
                    new CrossOnRedForCars { FriendlyName = "Cross on the red for cars" },
                    new WaitScrollingPhone { FriendlyName = "Wait, scrolling the phone" }));

        // 2 - "Enfant imprévisible", may turn back while crossing.
        //     Conditions: le feu passe du rouge au vert + player éloigné + vitesse > 50
        private static Task ChildTree() =>
            Sel("Unpredictable child",
                Abort(Seq("Danger: a car is coming at me",
                    new IsOnRoad(),
                    new CarAboutToHitMe(),
                    new MoveToPoint { point = PedestrianPoint.NearestKerb, speed = 3f, startled = true, FriendlyName = "Run to the nearest kerb" },
                    new LeaveRoad { swapIfAtFarKerb = true }), AbortType.LowerPriority),
                Abort(Seq("Demi-tour",
                    new IsOnRoad(),
                    new LightJustTurnedGreen { window = 1f, FriendlyName = "Feu passe du rouge au vert?" },
                    new PlayerIsFar { FriendlyName = "Player éloigné?" },
                    new PlayerSpeedAbove { percent = 50f, FriendlyName = "Vitesse > 50?" },
                    Util("Turn back or hurry? (fonctions d'utilité)",
                        new TurnBack { FriendlyName = "Turn back (demi-tour)" },
                        new HurryToFarKerb { FriendlyName = "Hurry to the far kerb" })), AbortType.LowerPriority),
                Seq("Crossing",
                    new IsOnRoad(),
                    new MoveToPoint { point = PedestrianPoint.FarKerb, speed = 0.7f, FriendlyName = "Dawdle across, skipping" },
                    new LeaveRoad { swapKerbs = true }),
                Seq("Start crossing",
                    new LightIs { color = LightColor.Red },
                    new WaitedAtLeast { seconds = 1.2f },
                    new StepOntoRoad { FriendlyName = "Step onto the road" }),
                new WaitOnKerb { walkSpeed = 1f, FriendlyName = "Wait on the kerb, fidgeting" });

        // 3 - "Personne âgée (traverse lentement)".
        //     Conditions: le feu passe au vert et toujours pas passé + si klaxon faire remarque
        private static Task OldManTree() =>
            Sel("Old man",
                Abort(Seq("Danger: a car is coming at me",
                    new IsOnRoad(),
                    new CarAboutToHitMe(),
                    new MoveToPoint { point = PedestrianPoint.NearestKerb, speed = 1.3f, startled = true, FriendlyName = "Step back as fast as he can" },
                    new LeaveRoad { swapIfAtFarKerb = true }), AbortType.LowerPriority),
                Abort(Seq("Feu vert et toujours pas passé",
                    new IsOnRoad { FriendlyName = "Toujours pas passé?" },
                    new LightIs { color = LightColor.Green, FriendlyName = "Feu passé au vert?" },
                    Sel("Klaxon?",
                        Abort(Seq("Si klaxon: faire remarque",
                            new WasHonkedAt { FriendlyName = "Klaxon?" },
                            new MakeRemark { FriendlyName = "Faire remarque" }), AbortType.LowerPriority),
                        Util("Keep crossing (fonctions d'utilité)",
                            new ShuffleOnSlowly { FriendlyName = "Shuffle on at his own pace" },
                            new RaiseHandToCars { FriendlyName = "Raise his hand: wait for me!" }))), AbortType.LowerPriority),
                Seq("Crossing",
                    new IsOnRoad(),
                    new MoveToPoint { point = PedestrianPoint.FarKerb, speed = 0.45f, FriendlyName = "Cross very slowly" },
                    new LeaveRoad { swapKerbs = true }),
                Seq("Start crossing",
                    new LightIs { color = LightColor.Red },
                    new WaitedAtLeast { seconds = 1.5f },
                    new StepOntoRoad { FriendlyName = "Step onto the road" }),
                new WaitOnKerb { walkSpeed = 0.5f, FriendlyName = "Wait on the kerb" });

        // 4 - "Un piéton surgit de derrière le bus"
        private static Task BusTree() =>
            Sel("Pedestrian behind the bus",
                Abort(Seq("Danger: a car is coming at me",
                    new IsOnRoad(),
                    new CarAboutToHitMe(),
                    new MoveToPoint { point = PedestrianPoint.HideSpot, speed = 3.4f, startled = true, FriendlyName = "Jump back behind the bus" },
                    new LeaveRoad { swapKerbs = false }), AbortType.LowerPriority),
                Seq("Surging across",
                    new IsOnRoad(),
                    new MoveToPoint { point = PedestrianPoint.FarKerb, speed = 1.9f, FriendlyName = "Hurry across the road" },
                    new MarkGone()),
                Seq("Gone",
                    new IsGone(),
                    new MoveToPoint { point = PedestrianPoint.Exit, speed = 1.3f, FriendlyName = "Walk away" },
                    new RespawnBehindBus { FriendlyName = "Come back behind the bus later" }),
                Util("Step out now? (fonctions d'utilité)",
                    new StayHiddenBehindBus { FriendlyName = "Stay hidden behind the bus" },
                    new SurgeOutFromBus { FriendlyName = "Surge out from behind the bus" }));

        // ===================================================================== NavMesh

        private const string NavMeshAsset = "Assets/_Project/Materials/Generated/Scenario/PedestrianNavMesh.asset";

        /// <summary>Bakes a NavMeshSurface over the streets of the 3 scenarios (road, kerbs, bus as obstacle).</summary>
        private static void BakePedestrianNavMesh(GameObject root)
        {
            var go = new GameObject("Pedestrian NavMesh");
            go.transform.SetParent(root.transform, false);
            var surface = go.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            surface.center = new Vector3(0f, 0f, 50f);
            surface.size = new Vector3(44f, 12f, 110f);
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            // moving things (player car, other cars) must not leave holes in the NavMesh
            var off = new System.Collections.Generic.List<Collider>();
            foreach (Collider c in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
                if (c.enabled && c.attachedRigidbody != null) { c.enabled = false; off.Add(c); }
            try { surface.BuildNavMesh(); }
            finally { foreach (Collider c in off) c.enabled = true; }

            if (surface.navMeshData != null)
            {
                AssetDatabase.CreateAsset(surface.navMeshData, NavMeshAsset);
                AssetDatabase.SaveAssets();
            }
            else Debug.LogError("[Scenarios] The pedestrian NavMesh could not be baked (no colliders under the scenarios?).");
        }

        // ===================================================================== helpers

        internal static Task Sel(string name, params Task[] children) => Parent(new Selector(), "Selector: " + name, children);
        internal static Task Seq(string name, params Task[] children) => Parent(new Sequence(), "Sequence: " + name, children);
        internal static Task Util(string name, params Task[] children) => Parent(new UtilitySelector(), "Utility Selector: " + name, children);

        private static Task Parent(ParentTask parent, string name, Task[] children)
        {
            parent.FriendlyName = name;
            for (int i = 0; i < children.Length; i++)
                parent.AddChild(children[i], i);
            return parent;
        }

        /// <summary>Conditional abort: the composite's conditions are re-checked while lower branches run.</summary>
        internal static Task Abort(Task composite, AbortType type)
        {
            for (Type t = composite.GetType(); t != null; t = t.BaseType)
            {
                FieldInfo f = t.GetField("abortType", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                if (f != null) { f.SetValue(composite, type); break; }
            }
            return composite;
        }

        private static int nextId;

        internal static void Write(BehaviorTree bt, Task root, string treeName)
        {
            bt.StartWhenEnabled = true;
            bt.RestartWhenComplete = true;
            bt.ResetValuesOnRestart = false;
            bt.BehaviorName = treeName;
            BehaviorSource source = bt.GetBehaviorSource();
            nextId = 1;
            NameLeaves(root);
            Layout(root, new Vector2(0f, 150f));
            var entry = new EntryTask { FriendlyName = "Entry" };
            entry.ID = 0;
            entry.NodeData = new NodeData { Offset = Vector2.zero };
            source.EntryTask = entry;
            source.RootTask = root;
            source.DetachedTasks = new System.Collections.Generic.List<Task>();
        }

        private static void NameLeaves(Task t)
        {
            if (string.IsNullOrEmpty(t.FriendlyName))
                t.FriendlyName = ObjectNames.NicifyVariableName(t.GetType().Name);
            if (t is ParentTask p && p.Children != null)
                foreach (Task c in p.Children) NameLeaves(c);
        }

        private static void Layout(Task task, Vector2 offset)
        {
            task.ID = nextId++;
            task.NodeData = new NodeData { Offset = offset };
            if (task is ParentTask parent && parent.Children != null)
            {
                const float column = 230f;
                float total = Width(task), x = -total * 0.5f;
                foreach (Task child in parent.Children)
                {
                    float w = Width(child);
                    Layout(child, new Vector2((x + w * 0.5f) * column, 150f));
                    x += w;
                }
            }
        }

        private static float Width(Task task)
        {
            if (!(task is ParentTask p) || p.Children == null || p.Children.Count == 0) return 1f;
            return p.Children.Sum(Width);
        }

        internal static MethodInfo FindSave()
        {
            foreach (string typeName in new[] { "BehaviorDesigner.Editor.BinarySerialization", "BehaviorDesigner.Editor.JSONSerialization" })
            {
                Type t = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(typeName, false)).FirstOrDefault(x => x != null);
                MethodInfo m = t?.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .FirstOrDefault(x => x.Name == "Save" && x.GetParameters().Length == 1 && x.GetParameters()[0].ParameterType == typeof(BehaviorSource));
                if (m != null) return m;
            }
            return null;
        }
    }
}
