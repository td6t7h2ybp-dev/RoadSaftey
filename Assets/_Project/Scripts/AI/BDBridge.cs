using System;
using UnityEngine;
using BDR = BehaviorDesigner.Runtime;
using BDT = BehaviorDesigner.Runtime.Tasks;

namespace RoadSafety.AI
{
    /// <summary>
    /// Runs the game's behaviour trees on Behavior Designer (Opsive).
    /// Every AI (other cars, the school kid, the townspeople, the player's actions) describes its
    /// tree with the small builder in BehaviorTree.cs; <see cref="Create"/> turns that description
    /// into a real Behavior Designer <c>BehaviorTree</c> component made of Behavior Designer tasks:
    ///   Selector   → BehaviorDesigner Selector
    ///   Sequence   → BehaviorDesigner Sequence
    ///   UtilitySelector (fonction utilitaire) → BehaviorDesigner UtilitySelector (GetUtility)
    ///   Condition  → a Conditional task,  ActionNode → an Action task.
    /// Behavior Designer executes the tree (open Tools > Behavior Designer > Editor and select the
    /// object in Play mode to watch it run). The AI ticks its tree with <see cref="Tick"/> from its
    /// own Update / FixedUpdate, so the timing is the same as before.
    /// </summary>
    public static class BDBridge
    {
        public static BDR.BehaviorTree Create(MonoBehaviour owner, Node root, string treeName)
        {
            var bt = owner.gameObject.AddComponent<BDR.BehaviorTree>();
            bt.StartWhenEnabled = false;
            bt.RestartWhenComplete = true;   // one full pass per tick, like a reactive tree
            bt.ResetValuesOnRestart = false;
            bt.BehaviorName = treeName;
            BDR.BehaviorSource source = bt.GetBehaviorSource();
            nextId = 1;
            BDT.Task rootTask = Convert(root);
            Layout(rootTask, Vector2.zero);
            // the "Entry" node Behavior Designer draws above every tree
            var entry = new BDT.EntryTask { FriendlyName = "Entry" };
            entry.ID = 0;
            entry.NodeData = new BDR.NodeData { Offset = new Vector2(0f, -140f) };
            source.EntryTask = entry;
            source.DetachedTasks = new System.Collections.Generic.List<BDT.Task>();
            source.RootTask = rootTask;
            source.HasSerialized = true;     // built in code: nothing to deserialize
            HideGizmos(bt);
            bt.EnableBehavior();
            if (BDR.BehaviorManager.instance != null)
                BDR.BehaviorManager.instance.UpdateInterval = BDR.UpdateIntervalType.Manual; // each AI ticks its own tree
            return bt;
        }

        /// <summary>Runs one pass of the Behavior Designer tree.</summary>
        public static void Tick(BDR.BehaviorTree bt)
        {
            if (bt == null || !bt.isActiveAndEnabled || BDR.BehaviorManager.instance == null)
                return;
            BDR.BehaviorManager.instance.Tick(bt);
        }

        private static int nextId;

        /// <summary>
        /// Trees built in code have no editor data: give every task an ID and a position so the
        /// Behavior Designer window draws the tree (children spread under their parent).
        /// </summary>
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

        /// <summary>Behavior Designer's scene-view icon is not needed for code-built trees.</summary>
        private static void HideGizmos(BDR.BehaviorTree bt)
        {
            const System.Reflection.BindingFlags all = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            for (Type t = bt.GetType(); t != null; t = t.BaseType)
            {
                var mode = t.GetField("gizmoViewMode", all);
                if (mode != null && mode.FieldType.IsEnum)
                    mode.SetValue(bt, Enum.Parse(mode.FieldType, "Never"));
                var show = t.GetField("showBehaviorDesignerGizmo", all);
                if (show != null && show.FieldType == typeof(bool))
                    show.SetValue(bt, false);
            }
        }

        private static BDT.Task Convert(Node node)
        {
            switch (node)
            {
                case Selector selector:
                {
                    var task = new BDT.Selector();
                    task.FriendlyName = selector.Name;
                    for (int i = 0; i < selector.Children.Count; i++)
                        task.AddChild(Convert(selector.Children[i]), i);
                    return task;
                }
                case Sequence sequence:
                {
                    var task = new BDT.Sequence();
                    task.FriendlyName = sequence.Name;
                    for (int i = 0; i < sequence.Children.Count; i++)
                        task.AddChild(Convert(sequence.Children[i]), i);
                    return task;
                }
                case UtilitySelector utility:
                {
                    var task = new BDT.UtilitySelector();
                    task.FriendlyName = utility.Name;
                    for (int i = 0; i < utility.Options.Count; i++)
                    {
                        UtilityOption option = utility.Options[i];
                        var branch = new UtilityBranch { Score = option.Score, UtilityOwner = utility, Chosen = option.Node };
                        branch.FriendlyName = option.Node.Name;
                        branch.AddChild(Convert(option.Node), 0);
                        task.AddChild(branch, i);
                    }
                    return task;
                }
                case Condition condition:
                    return new LambdaConditional { Check = condition.Check, FriendlyName = condition.Name };
                case ActionNode action:
                    return new LambdaAction { Run = action.Run, FriendlyName = action.Name };
                default:
                    return new LambdaAction { Run = node.Evaluate, FriendlyName = node.Name };
            }
        }

        private static BDT.TaskStatus ToStatus(NodeState state) =>
            // one pass per tick: a "running" action has done this frame's work, so the pass succeeds
            state == NodeState.Failure ? BDT.TaskStatus.Failure : BDT.TaskStatus.Success;

        /// <summary>A Behavior Designer conditional task that asks the AI a yes / no question.</summary>
        public class LambdaConditional : BDT.Conditional
        {
            public Func<bool> Check;

            public override BDT.TaskStatus OnUpdate() =>
                Check != null && Check() ? BDT.TaskStatus.Success : BDT.TaskStatus.Failure;
        }

        /// <summary>A Behavior Designer action task that runs one frame of an AI action.</summary>
        public class LambdaAction : BDT.Action
        {
            public Func<NodeState> Run;

            public override BDT.TaskStatus OnUpdate() =>
                Run != null ? ToStatus(Run()) : BDT.TaskStatus.Failure;
        }

        /// <summary>
        /// One branch of a utility selector: gives Behavior Designer the branch's utility score
        /// (GetUtility) and runs the branch.
        /// </summary>
        public class UtilityBranch : BDT.Decorator
        {
            public Func<float> Score;
            public UtilitySelector UtilityOwner;
            public Node Chosen;
            private BDT.TaskStatus executionStatus = BDT.TaskStatus.Inactive;

            public override float GetUtility() => Score != null ? Score() : 0f;

            public override void OnStart() => UtilityOwner?.Report(Chosen, GetUtility());

            public override bool CanExecute() =>
                executionStatus == BDT.TaskStatus.Inactive || executionStatus == BDT.TaskStatus.Running;

            public override void OnChildExecuted(BDT.TaskStatus childStatus) => executionStatus = childStatus;

            public override void OnEnd() => executionStatus = BDT.TaskStatus.Inactive;
        }
    }
}
