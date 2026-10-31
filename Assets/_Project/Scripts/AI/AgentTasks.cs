using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

namespace RoadSafety.AI
{
    /// <summary>
    /// Behavior Designer conditional: asks the agent on this GameObject (car, kid, townsperson,
    /// player, traffic light) a yes / no question, by name.
    /// </summary>
    [TaskCategory("RoadSafety")]
    [TaskDescription("Asks the RoadSafety agent on this object a yes/no question (e.g. \"Red light or pedestrians?\").")]
    public class AgentCondition : Conditional
    {
        [UnityEngine.Tooltip("Name of the agent's condition.")]
        public string conditionName;

        private IBTAgent agent;

        public override void OnAwake() => agent = gameObject.GetComponent<IBTAgent>();

        private IBTAgent Agent => agent ?? (agent = gameObject.GetComponent<IBTAgent>());

        public override TaskStatus OnUpdate()
        {
            if (Agent == null || Agent.Bindings == null) return TaskStatus.Failure;
            return agent.Bindings.Check(conditionName) ? TaskStatus.Success : TaskStatus.Failure;
        }
    }

    /// <summary>
    /// Behavior Designer action: runs one frame of the agent's action, by name. Under a
    /// Utility Selector it also gives Behavior Designer the action's utility score (GetUtility).
    /// </summary>
    [TaskCategory("RoadSafety")]
    [TaskDescription("Runs one frame of the RoadSafety agent's action (e.g. \"Stop at the line\"). Its utility comes from the agent's utility function.")]
    public class AgentAction : Action
    {
        [UnityEngine.Tooltip("Name of the agent's action.")]
        public string actionName;

        private IBTAgent agent;

        public override void OnAwake() => agent = gameObject.GetComponent<IBTAgent>();

        private IBTAgent Agent => agent ?? (agent = gameObject.GetComponent<IBTAgent>());

        public override TaskStatus OnUpdate()
        {
            if (Agent == null || Agent.Bindings == null) return TaskStatus.Failure;
            // one pass of the tree per tick: an action that is still going has done this frame's work
            return agent.Bindings.Act(actionName) == NodeState.Failure ? TaskStatus.Failure : TaskStatus.Success;
        }

        public override float GetUtility() =>
            Agent != null && Agent.Bindings != null ? Agent.Bindings.Utility(actionName) : 0f;
    }
}
