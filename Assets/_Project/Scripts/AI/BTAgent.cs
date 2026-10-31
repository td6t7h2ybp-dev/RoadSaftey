using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoadSafety.AI
{
    /// <summary>
    /// Something run by a Behavior Designer tree (other car, school kid, townsperson, player,
    /// traffic light). It describes its tree (<see cref="DescribeTree"/>) and gives the tree's
    /// tasks access to its conditions and actions by name (<see cref="Bindings"/>).
    /// </summary>
    public interface IBTAgent
    {
        /// <summary>The tree: Selector / Sequence / UtilitySelector / Condition / ActionNode.</summary>
        Node DescribeTree();

        /// <summary>The agent's conditions, actions and utility scores, by task name.</summary>
        BTBindings Bindings { get; }
    }

    /// <summary>Looks up an agent's conditions, actions and utility scores by name.</summary>
    public sealed class BTBindings
    {
        private readonly Dictionary<string, Func<bool>> conditions = new Dictionary<string, Func<bool>>();
        private readonly Dictionary<string, Func<NodeState>> actions = new Dictionary<string, Func<NodeState>>();
        private readonly Dictionary<string, (UtilitySelector owner, Node node, Func<float> score)> utilities =
            new Dictionary<string, (UtilitySelector, Node, Func<float>)>();

        public static BTBindings From(Node root)
        {
            var b = new BTBindings();
            b.Add(root);
            return b;
        }

        private void Add(Node node)
        {
            switch (node)
            {
                case Selector s: foreach (Node c in s.Children) Add(c); break;
                case Sequence q: foreach (Node c in q.Children) Add(c); break;
                case UtilitySelector u:
                    foreach (UtilityOption o in u.Options)
                    {
                        utilities[o.Node.Name] = (u, o.Node, o.Score);
                        Add(o.Node);
                    }
                    break;
                case Condition c: conditions[c.Name] = c.Check; break;
                case ActionNode a: actions[a.Name] = a.Run; break;
            }
        }

        public bool Check(string name) =>
            conditions.TryGetValue(name, out Func<bool> f) && f != null && f();

        public NodeState Act(string name)
        {
            if (utilities.TryGetValue(name, out var u) && u.owner != null)
                u.owner.Report(u.node, u.score != null ? u.score() : 0f);
            return actions.TryGetValue(name, out Func<NodeState> f) && f != null ? f() : NodeState.Failure;
        }

        public float Utility(string name) =>
            utilities.TryGetValue(name, out var u) && u.score != null ? u.score() : 0f;
    }
}
