using UnityEngine;
using BDR = BehaviorDesigner.Runtime;

namespace RoadSafety.AI
{
    /// <summary>
    /// Every AI of the game runs on its Behavior Designer tree (BehaviorTree component saved in
    /// Main.unity, custom tasks in AI/BehaviorDesignerTasks). The agents tick their own tree from
    /// Update / FixedUpdate so the timing stays the same as their movement code.
    /// </summary>
    public static class BDBridge
    {
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
    }
}
