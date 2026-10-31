namespace RoadSafety.AI
{
    /// <summary>
    /// A Behavior Designer BehaviorTree whose tasks are built in code (BDBridge).
    /// Same as BehaviorDesigner.Runtime.BehaviorTree; it only skips Behavior Designer's scene-view
    /// gizmo, which is written for trees made in the Behavior Designer window.
    /// </summary>
    public class CodeBehaviorTree : BehaviorDesigner.Runtime.BehaviorTree
    {
        private void OnDrawGizmos() { }

        private void OnDrawGizmosSelected() { }
    }
}
