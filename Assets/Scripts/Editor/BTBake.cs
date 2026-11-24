using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using RoadSafety.AI;
using RoadSafety.AI.BehaviorDesignerTasks;
using RoadSafety.Player;
using RoadSafety.World;
using static RoadSafety.EditorTools.ScenarioBuilderMenu;
using Object = UnityEngine.Object;
using Task = BehaviorDesigner.Runtime.Tasks.Task;

namespace RoadSafety.EditorTools
{
    /// <summary>
    /// RoadSafety > AI > Attach Behavior Designer trees
    /// Gives every AI of the open scene (traffic light, other cars, school kid, townspeople, player's
    /// action hints) a Behavior Designer tree made of Behavior Designer composites and the custom
    /// tasks of AI/BehaviorDesignerTasks/GameTasks.cs, and saves the scene. From then on they are
    /// ordinary Behavior Designer trees: select an object and open Tools > Behavior Designer > Editor
    /// to see, edit or watch (in Play mode) its tree. Running this menu again rebuilds them.
    /// The evaluation pedestrians have their own menu (Build NPC scenarios).
    /// </summary>
    public static class BTBake
    {
        [MenuItem("RoadSafety/AI/Attach Behavior Designer trees")]
        public static void Bake()
        {
            if (Application.isPlaying) { Debug.LogWarning("[BTBake] Stop Play mode first."); return; }
            ScenePreview.HideMenu(); // only the real scene is saved
            MethodInfo save = FindSave();
            if (save == null) { Debug.LogError("[BTBake] Behavior Designer's serializer was not found."); return; }

            int count = 0;
            void Put(Component c, Task tree, string name)
            {
                var bt = c.GetComponent<BehaviorTree>();
                if (bt == null) bt = Undo.AddComponent<BehaviorTree>(c.gameObject);
                else Undo.RecordObject(bt, "Behavior Designer tree");
                Write(bt, tree, name);
                save.Invoke(null, new object[] { bt.GetBehaviorSource() });
                EditorUtility.SetDirty(bt);
                PrefabUtility.RecordPrefabInstancePropertyModifications(bt);
                count++;
            }

            var scene = EditorSceneManager.GetActiveScene();
            foreach (TrafficLight l in Object.FindObjectsByType<TrafficLight>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (l.gameObject.scene == scene) Put(l, TrafficLightTree(), "Traffic light");
            foreach (NpcCar c in Object.FindObjectsByType<NpcCar>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c.gameObject.scene == scene) Put(c, OtherCarTree(), "Other car");
            foreach (Pedestrian p in Object.FindObjectsByType<Pedestrian>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (p.gameObject.scene == scene) Put(p, SchoolKidTree(), "School kid (zebra crossing)");
            foreach (Townsperson t in Object.FindObjectsByType<Townsperson>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.gameObject.scene == scene) Put(t, TownspersonTree(), "Townsperson");
            foreach (CafeVisitor v in Object.FindObjectsByType<CafeVisitor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (v.gameObject.scene == scene) Put(v, CafeVisitorTree(), "Café visitor");
            foreach (PigeonFlock f in Object.FindObjectsByType<PigeonFlock>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (f.gameObject.scene == scene) Put(f, PigeonsTree(), "Pigeons");
            foreach (StreetCat c in Object.FindObjectsByType<StreetCat>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c.gameObject.scene == scene) Put(c, CatTree(), "Cat");
            foreach (Gull g in Object.FindObjectsByType<Gull>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (g.gameObject.scene == scene) Put(g, GullTree(), "Seagull");
            foreach (PlayerActions a in Object.FindObjectsByType<PlayerActions>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (a.gameObject.scene == scene) Put(a, PlayerTree(), "Player actions");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            ScenePreview.RefreshMenu();
            Debug.Log($"[BTBake] {count} Behavior Designer trees attached and saved in {scene.name}.");
        }

        // ===================================================================== utility root (US 5.3)

        /// <summary>
        /// The top of every tree is a Utility Selector (the utility function): each former branch is
        /// wrapped in a UtilityBranch with a fixed score, 1.0, 0.9, 0.8... from top to bottom, so the
        /// old priority order is kept. A branch scores 0 while its first condition is false.
        /// </summary>
        private static Task Root(string name, params Task[] branches)
        {
            var wrapped = new Task[branches.Length];
            for (int i = 0; i < branches.Length; i++)
            {
                float score = Mathf.Max(0.1f, 1f - 0.1f * i);
                string label = branches[i].FriendlyName ?? branches[i].GetType().Name;
                int colon = label.IndexOf(": ");
                if (colon >= 0) label = label.Substring(colon + 2);
                var branch = new UtilityBranch { utility = score, FriendlyName = "Utility Branch " + score.ToString("0.0") + ": " + label };
                branch.AddChild(branches[i], 0);
                wrapped[i] = branch;
            }
            return Util(name + " (fonction d'utilité)", wrapped);
        }

        // ===================================================================== the trees

        private static Task TrafficLightTree() =>
            Root("Traffic light",
                Phase("Main road green", 0, "Keep main green, then amber"),
                Phase("Main road amber", 1, "Keep main amber, then red"),
                Phase("All red (1)", 2, "Keep all red, then side green"),
                Phase("Side road green", 3, "Keep side green, then amber"),
                Phase("Side road amber", 4, "Keep side amber, then red"),
                Phase("All red (2)", 5, "Keep all red, then main green"));

        private static Task Phase(string name, int p, string action) =>
            Seq(name,
                new LightPhaseIs { phase = p, FriendlyName = "Phase " + p + "?" },
                new HoldPhase { phase = p, FriendlyName = action });

        private static Task OtherCarTree() =>
            Root("Other car",
                Seq("Obstacle",
                    new ObstacleAhead { FriendlyName = "Something in front?" },
                    new BrakeBehindObstacle { FriendlyName = "Brake" }),
                Seq("Stop line",
                    new BlockedLineAhead { FriendlyName = "Red light or pedestrians?" },
                    new StopAtTheLine { FriendlyName = "Stop at the line" }),
                Util("Choose speed (fonctions d'utilité)",
                    new Cruise { FriendlyName = "Cruise" },
                    new SlowDown { FriendlyName = "Slow down" }));

        private static Task SchoolKidTree() =>
            Root("School kid",
                Seq("Danger",
                    new CarRushesWhileCrossing { FriendlyName = "Crossing and a car rushes?" },
                    new StartSteppingBack { FriendlyName = "Step back" }),
                Seq("Stepping back",
                    new KidStateIs { state = PedestrianState.SteppingBack, FriendlyName = "Stepping back?" },
                    new RunBackToKerb { FriendlyName = "Run to the kerb" }),
                Seq("Crossing",
                    new KidStateIs { state = PedestrianState.Crossing, FriendlyName = "Crossing?" },
                    new CrossTheRoad { FriendlyName = "Cross" }),
                Seq("Safe to cross",
                    new ReadyToCross { FriendlyName = "Waited and all cars stopped?" },
                    new StartCrossing { FriendlyName = "Start crossing" }),
                Seq("Go to the kerb",
                    new KidStateIs { state = PedestrianState.WalkingToKerb, FriendlyName = "Walking to the kerb?" },
                    new WalkToTheKerb { FriendlyName = "Walk to the kerb" }),
                Seq("After crossing",
                    new HasCrossed { FriendlyName = "Crossed?" },
                    new ThankAndLeave { FriendlyName = "Thank and leave" }),
                new WaitHandUp { FriendlyName = "Wait, hand up" });

        private static Task TownspersonTree() =>
            Root("Townsperson",
                Seq("Horn heard",
                    new HornHeard { FriendlyName = "Car honked nearby?" },
                    new WaveAtTheCar { FriendlyName = "Wave at the car" }),
                Seq("Seated",
                    new IsSeated { FriendlyName = "Has a chair?" },
                    new SitTalkDrinkTea { FriendlyName = "Sit, talk, drink tea" }),
                Seq("In a group",
                    new IsGroupTalker { FriendlyName = "With friends?" },
                    new TalkInTurn { FriendlyName = "Talk in turn" }),
                Seq("Chatting",
                    new HasChatPartner { FriendlyName = "Has a partner?" },
                    new ChatWithNeighbour { FriendlyName = "Chat" }),
                Util("What next? (fonctions d'utilité)",
                    new WalkOn { FriendlyName = "Walk on" },
                    new GreetNeighbour { FriendlyName = "Greet a neighbour" },
                    new RestAMoment { FriendlyName = "Rest a moment" }));

        private static Task CafeVisitorTree() =>
            Root("Café visitor",
                Seq("Horn heard", new VisitorHornHeard { FriendlyName = "Car honked nearby?" }, new VisitorWave { FriendlyName = "Wave at the car" }),
                Seq("Inside the café", new IsInsideCafe { FriendlyName = "Inside?" }, new HaveCoffeeInside { FriendlyName = "Have a coffee inside" }),
                Seq("Leaving", new IsLeavingCafe { FriendlyName = "Just had a coffee?" }, new WalkOutAndAway { FriendlyName = "Walk out and away" }),
                Seq("Going in", new WantsCoffee { FriendlyName = "Wants a coffee?" }, new WalkInThroughDoor { FriendlyName = "Walk in through the door" }),
                Util("On the pavement (fonctions d'utilité)",
                    new VisitorStroll { FriendlyName = "Stroll" },
                    new LookAtShops { FriendlyName = "Look at the shops" }));

        private static Task PigeonsTree() =>
            Root("Pigeons",
                Seq("Car coming", new PigeonsCarComing { FriendlyName = "Car within 9 m?" }, new PigeonsTakeOff { FriendlyName = "Fly off" }),
                Seq("In the air", new PigeonsFlying { FriendlyName = "Flying?" }, new PigeonsFly { FriendlyName = "Fly and land further on" }),
                Util("On the ground (fonctions d'utilité)",
                    new PigeonsPeck { FriendlyName = "Peck at crumbs" },
                    new PigeonsHop { FriendlyName = "Hop around" },
                    new PigeonsRest { FriendlyName = "Rest" }));

        private static Task CatTree() =>
            Root("Cat",
                Seq("Danger", new CatCarClose { FriendlyName = "Car within 7 m?" }, new CatDash { FriendlyName = "Dash to the wall" }),
                Util("Cat life (fonctions d'utilité)",
                    new CatStroll { FriendlyName = "Stroll" },
                    new CatWash { FriendlyName = "Sit and wash" },
                    new CatNap { FriendlyName = "Nap in the sun" }));

        private static Task GullTree() =>
            Util("Seagull (fonctions d'utilité)",
                new GullGlide { FriendlyName = "Glide in circles" },
                new GullFlap { FriendlyName = "Flap to climb" });

        private static Task PlayerTree() =>
            Root("Player actions",
                Seq("Damaged",
                    new HealthLow { FriendlyName = "Health low?" },
                    new RepairCar { FriendlyName = "Repair" }),
                Seq("Tired",
                    new StaminaLow { FriendlyName = "Stamina low?" },
                    new RestDriver { FriendlyName = "Rest" }),
                Util("Best action now (fonctions d'utilité)",
                    new AdviseSeatbelt { FriendlyName = "Fasten the seatbelt" },
                    new AdviseBrake { FriendlyName = "Brake" },
                    new AdviseHonk { FriendlyName = "Honk (friendly)" },
                    new AdviseDriveOn { FriendlyName = "Drive on" }));
    }
}
