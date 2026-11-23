namespace RoadSafety.AI
{
    /// <summary>Result of one step of an agent's action (mapped to Behavior Designer's TaskStatus by the tasks).</summary>
    public enum NodeState
    {
        Running,
        Success,
        Failure
    }
}
