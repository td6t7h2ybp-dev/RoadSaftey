using System;
using System.Collections.Generic;

namespace RoadSafety.AI
{
    /// <summary>Result of evaluating a behaviour-tree node.</summary>
    public enum NodeState
    {
        Running,
        Success,
        Failure
    }

    /// <summary>Base class for every behaviour-tree node.</summary>
    public abstract class Node
    {
        public string Name { get; }

        protected Node(string name)
        {
            Name = name;
        }

        public abstract NodeState Evaluate();
    }

    /// <summary>
    /// Selector ("OR"): evaluates its children in order and stops at the first
    /// one that does not fail. Put the most important branch first.
    /// </summary>
    public class Selector : Node
    {
        private readonly List<Node> children;

        public Selector(string name, params Node[] children) : base(name)
        {
            this.children = new List<Node>(children);
        }

        public IReadOnlyList<Node> Children => children;

        public override NodeState Evaluate()
        {
            foreach (Node child in children)
            {
                NodeState state = child.Evaluate();
                if (state != NodeState.Failure)
                    return state;
            }
            return NodeState.Failure;
        }
    }

    /// <summary>
    /// Sequence ("AND"): evaluates its children in order and stops at the first
    /// one that does not succeed. Typical use: Condition, then Action.
    /// </summary>
    public class Sequence : Node
    {
        private readonly List<Node> children;

        public Sequence(string name, params Node[] children) : base(name)
        {
            this.children = new List<Node>(children);
        }

        public IReadOnlyList<Node> Children => children;

        public override NodeState Evaluate()
        {
            foreach (Node child in children)
            {
                NodeState state = child.Evaluate();
                if (state != NodeState.Success)
                    return state;
            }
            return NodeState.Success;
        }
    }

    /// <summary>Leaf that checks something: Success if true, Failure if false.</summary>
    public class Condition : Node
    {
        private readonly Func<bool> check;

        public Condition(string name, Func<bool> check) : base(name)
        {
            this.check = check;
        }

        public Func<bool> Check => check;

        public override NodeState Evaluate()
        {
            return check() ? NodeState.Success : NodeState.Failure;
        }
    }

    /// <summary>Leaf that does something and reports how it went.</summary>
    public class ActionNode : Node
    {
        private readonly Func<NodeState> action;

        public ActionNode(string name, Func<NodeState> action) : base(name)
        {
            this.action = action;
        }

        public Func<NodeState> Run => action;

        public override NodeState Evaluate()
        {
            return action();
        }
    }
}
