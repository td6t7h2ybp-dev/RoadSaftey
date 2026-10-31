using UnityEditor;

namespace RoadSafety.EditorTools
{
    /// <summary>
    /// Behavior Designer's scene-view gizmo is switched off for BehaviorTree components (it threw
    /// errors for trees made from code), but its icon stays on, so objects with a tree show the
    /// Behavior Designer logo.
    /// </summary>
    [InitializeOnLoad]
    public static class BDGizmoOff
    {
        static BDGizmoOff()
        {
            EditorApplication.delayCall += () =>
            {
                GizmoUtility.SetGizmoEnabled(typeof(BehaviorDesigner.Runtime.BehaviorTree), false, true);
                GizmoUtility.SetIconEnabled(typeof(BehaviorDesigner.Runtime.BehaviorTree), true);
            };
        }
    }
}

// Behavior Designer hierarchy / scene icons: Assets/Gizmos/Behavior Designer Hier Icon.png and Scene Icon.png
