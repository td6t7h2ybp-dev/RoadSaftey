using System;
using System.Collections.Generic;

namespace RoadSafety.AI
{
    /// <summary>One choice of a <see cref="UtilitySelector"/>: a branch and how useful it is right now.</summary>
    public readonly struct UtilityOption
    {
        public readonly Node Node;
        public readonly Func<float> Score;

        public UtilityOption(Node node, Func<float> score)
        {
            Node = node;
            Score = score;
        }
    }

    /// <summary>
    /// Utility-AI composite ("fonction utilitaire", slide 4): it scores every child with its
    /// utility function (0..1) and runs the child with the highest score.
    /// Used inside the behaviour trees of the other cars to choose their speed.
    /// </summary>
    public class UtilitySelector : Node
    {
        private readonly UtilityOption[] options;

        public UtilitySelector(string name, params UtilityOption[] options) : base(name)
        {
            this.options = options;
        }

        /// <summary>The branch chosen at the last evaluation (for debugging).</summary>
        public Node LastChoice { get; private set; }

        public float LastScore { get; private set; }

        public IReadOnlyList<UtilityOption> Options => options;

        /// <summary>Called by the Behavior Designer tree when it picks a branch (BDBridge).</summary>
        public void Report(Node chosen, float score)
        {
            LastChoice = chosen;
            LastScore = score;
        }

        public override NodeState Evaluate()
        {
            Node best = null;
            float bestScore = float.NegativeInfinity;
            foreach (UtilityOption option in options)
            {
                float score = option.Score != null ? option.Score() : 0f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = option.Node;
                }
            }
            LastChoice = best;
            LastScore = bestScore;
            return best != null ? best.Evaluate() : NodeState.Failure;
        }
    }
}
