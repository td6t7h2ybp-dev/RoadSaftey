using UnityEditor;

namespace RoadSafety.EditorTools
{
    /// <summary>
    /// Our Behavior Designer trees are built in code (BDBridge). Behavior Designer's scene-view
    /// gizmo expects trees made in its editor and throws errors for these, so its gizmo is
    /// switched off (Scene view > Gizmos > Scripts > BehaviorTree). The trees still run and can be
    /// watched in the Behavior Designer window in Play mode.
    /// </summary>
    [InitializeOnLoad]
    public static class BDGizmoOff
    {
        static BDGizmoOff()
        {
            EditorApplication.delayCall += () =>
            {
                GizmoUtility.SetGizmoEnabled(typeof(BehaviorDesigner.Runtime.BehaviorTree), false, true);
                GizmoUtility.SetIconEnabled(typeof(BehaviorDesigner.Runtime.BehaviorTree), false);
            };
        }
    }
}
