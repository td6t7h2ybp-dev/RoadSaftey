using System.Collections.Generic;
using UnityEngine;

namespace RoadSafety.AI
{
    /// <summary>
    /// US 5.3 "Les actions passent par une fonction utilitaire centralisée".
    /// The ONE place where every AI of the game (other cars, pedestrians, townspeople, café
    /// visitors, animals, the player's hint tree) scores its options and runs its actions:
    /// <code>
    ///     // in a Behavior Designer task under a Utility Selector
    ///     public override float GetUtility() => UtilityAI.Score(agent, FriendlyName, raw);
    ///     // in a task's OnUpdate
    ///     return Status.Of(UtilityAI.Act(agent, FriendlyName, agent.Cruise()));
    /// </code>
    /// The Behavior Designer trees decide WHEN (structure of the tree), this class decides
    /// HOW MUCH an option is worth (always 0..1, the same rules for everybody) and keeps a
    /// record of what each agent chose, for the debug view and the gizmo labels.
    /// </summary>
    public static class UtilityAI
    {
        private class Record
        {
            public readonly Dictionary<string, float> scores = new Dictionary<string, float>();
            public string lastAction = "-";
            public NodeState lastResult = NodeState.Running;
            public float lastActionTime;
        }

        private static readonly Dictionary<int, Record> records = new Dictionary<int, Record>();

        // ------------------------------------------------------------------ the utility function

        /// <summary>
        /// Central utility function: every option of every agent goes through here.
        /// Clamps the raw score to 0..1 (NaN = 0) and remembers it for the agent.
        /// </summary>
        public static float Score(Component agent, string option, float raw)
        {
            float u = float.IsNaN(raw) ? 0f : Mathf.Clamp01(raw);
            if (agent != null)
                Get(agent).scores[option ?? "?"] = u;
            return u;
        }

        /// <summary>Central action gateway: every action step of every agent goes through here.</summary>
        public static NodeState Act(Component agent, string action, NodeState result)
        {
            if (agent != null)
            {
                Record r = Get(agent);
                if (r.lastAction != action)
                    r.lastActionTime = Time.time;
                r.lastAction = action ?? "?";
                r.lastResult = result;
            }
            return result;
        }

        /// <summary>Same as <see cref="Act(Component,string,NodeState)"/> for actions that answer yes / no.</summary>
        public static NodeState Act(Component agent, string action, bool ok) =>
            Act(agent, action, ok ? NodeState.Success : NodeState.Failure);

        // ------------------------------------------------------------------ shared curves

        /// <summary>0 at <paramref name="zeroAt"/>, 1 at <paramref name="oneAt"/>, linear between (works both ways).</summary>
        public static float Ramp(float value, float zeroAt, float oneAt)
        {
            if (Mathf.Approximately(zeroAt, oneAt))
                return value >= oneAt ? 1f : 0f;
            return Mathf.Clamp01((value - zeroAt) / (oneAt - zeroAt));
        }

        /// <summary>1 when the condition is true, else 0 (the "×" factors of the utility formulas).</summary>
        public static float If(bool condition) => condition ? 1f : 0f;

        // ------------------------------------------------------------------ read back (debug)

        /// <summary>Last action the agent ran ("-" if none yet).</summary>
        public static string LastAction(Component agent) =>
            agent != null && records.TryGetValue(agent.GetInstanceID(), out Record r) ? r.lastAction : "-";

        /// <summary>Last score of every option of the agent.</summary>
        public static IReadOnlyDictionary<string, float> Scores(Component agent) =>
            agent != null && records.TryGetValue(agent.GetInstanceID(), out Record r) ? r.scores : null;

        /// <summary>Option with the highest last score ("-" if none).</summary>
        public static string Best(Component agent)
        {
            IReadOnlyDictionary<string, float> s = Scores(agent);
            if (s == null) return "-";
            string best = "-";
            float max = -1f;
            foreach (KeyValuePair<string, float> kv in s)
                if (kv.Value > max) { max = kv.Value; best = kv.Key; }
            return best;
        }

        /// <summary>One line for labels: "Cruise (0.60)".</summary>
        public static string Describe(Component agent)
        {
            string action = LastAction(agent);
            IReadOnlyDictionary<string, float> s = Scores(agent);
            return s != null && s.TryGetValue(action, out float u) ? $"{action} ({u:0.00})" : action;
        }

        private static Record Get(Component agent)
        {
            int id = agent.GetInstanceID();
            if (!records.TryGetValue(id, out Record r))
            {
                r = new Record();
                records[id] = r;
            }
            return r;
        }

        // "Enter Play Mode" without domain reload keeps statics alive between two Play sessions.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => records.Clear();
    }
}
