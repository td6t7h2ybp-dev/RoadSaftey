using System;
using UnityEngine;
using BDR = BehaviorDesigner.Runtime;
using BDT = BehaviorDesigner.Runtime.Tasks;

namespace RoadSafety.AI
{
    /// <summary>
    /// Behavior Designer (Opsive) runs every AI of the game: the other cars, the school kid, the
    /// townspeople, the player's actions and the traffic light.
    /// Each of them has a Behavior Designer <c>BehaviorTree</c> component saved in Main.unity
    /// (RoadSafety > AI > Attach Behavior Designer trees). Its tasks are Behavior Designer
    /// composites (Selector, Sequence, UtilitySelector) with RoadSafety leaves
    /// (<see cref="AgentCondition"/>, <see cref="AgentAction"/>) that call the agent by name.
    /// The agent ticks its own tree with <see cref="Tick"/> (Update / FixedUpdate), so the timing
    /// stays the same; if an object has no saved tree, the same tree is built when Play starts.
    /// </summary>
    public static class BDBridge
    {
        /// <summary>The agent's Behavior Designer tree (the one saved in the scene, or a new one).</summary>
        public static BDR.BehaviorTree Ensure(MonoBehaviour agent, Node root, string treeName)
        {
            var bt = agent.GetComponent<BDR.BehaviorTree>();
            if (bt == null)
            {
                bt = agent.gameObject.AddComponent<BDR.BehaviorTree>();
                Fill(bt, root, treeName);
                bt.GetBehaviorSource().HasSerialized = true; // built in code: nothing to load
            }
            return bt; // StartWhenEnabled: Behavior Designer starts it on its own
        }

        /// <summary>Runs one pass of the agent's Behavior Designer tree.</summary>
        public static void Tick(BDR.BehaviorTree bt)
        {
            if (bt == null || !bt.isActiveAndEnabled) return;
            BDR.BehaviorManager manager = BDR.BehaviorManager.instance;
            if (manager == null) return;
            if (manager.UpdateInterval != BDR.UpdateIntervalType.Manual)
                manager.UpdateInterval = BDR.UpdateIntervalType.Manual; // each agent ticks its own tree
            manager.Tick(bt);
        }

        /// <summary>Writes the tree into a BehaviorTree component (also used by the Editor bake).</summary>
        public static void Fill(BDR.BehaviorTree bt, Node root, string treeName)
        {
            bt.StartWhenEnabled = true;
            bt.RestartWhenComplete = true;   // one full pass per tick, like a reactive tree
            bt.ResetValuesOnRestart = false;
            bt.BehaviorName = treeName;
            BDR.BehaviorSource source = bt.GetBehaviorSource();
            nextId = 1;
            BDT.Task rootTask = Convert(root);
            Layout(rootTask, Vector2.zero);
            var entry = new BDT.EntryTask { FriendlyName = "Entry" };
            entry.ID = 0;
            entry.NodeData = new BDR.NodeData { Offset = new Vector2(0f, -140f) };
            source.EntryTask = entry;
            source.DetachedTasks = new System.Collections.Generic.List<BDT.Task>();
            source.RootTask = rootTask;
        }

        private static int nextId;

        private static void Layout(BDT.Task task, Vector2 offset)
        {
            task.ID = nextId++;
            task.NodeData = new BDR.NodeData { Offset = offset };
            if (task is BDT.ParentTask parent && parent.Children != null)
            {
                int n = parent.Children.Count;
                for (int i = 0; i < n; i++)
                    Layout(parent.Children[i], new Vector2((i - (n - 1) * 0.5f) * 220f, 140f));
            }
        }

        private static BDT.Task Convert(Node node)
        {
            switch (node)
            {
                case Selector selector:
                {
                    var task = new BDT.Selector { FriendlyName = selector.Name };
                    for (int i = 0; i < selector.Children.Count; i++)
                        task.AddChild(Convert(selector.Children[i]), i);
                    return task;
                }
                case Sequence sequence:
                {
                    var task = new BDT.Sequence { FriendlyName = sequence.Name };
                    for (int i = 0; i < sequence.Children.Count; i++)
                        task.AddChild(Convert(sequence.Children[i]), i);
                    return task;
                }
                case UtilitySelector utility:
                {
                    // fonction utilitaire: Behavior Designer picks the child with the highest GetUtility()
                    var task = new BDT.UtilitySelector { FriendlyName = utility.Name };
                    for (int i = 0; i < utility.Options.Count; i++)
                        task.AddChild(Convert(utility.Options[i].Node), i);
                    return task;
                }
                case Condition condition:
                    return new AgentCondition { conditionName = condition.Name, FriendlyName = condition.Name };
                default:
                    return new AgentAction { actionName = node.Name, FriendlyName = node.Name };
            }
        }
    }
}
