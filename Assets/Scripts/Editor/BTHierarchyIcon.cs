using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using BehaviorDesigner.Runtime;

namespace RoadSafety.EditorTools
{
    /// <summary>
    /// Shows the Behavior Designer icon in the Hierarchy on every object that runs a Behavior Tree
    /// AND on every parent that contains some (with how many), so you can see at a glance where the
    /// behaviour trees are, even with the groups collapsed.
    /// Behavior Designer itself already draws the icon on the objects holding a tree.
    /// </summary>
    [InitializeOnLoad]
    public static class BTHierarchyIcon
    {
        private const string IconPath = "Assets/Gizmos/Behavior Designer Hier Icon.png";
        private static Texture2D icon;

        static BTHierarchyIcon()
        {
            EditorApplication.hierarchyWindowItemOnGUI += OnItem;
        }

        private static void OnItem(int id, Rect row)
        {
            var go = EditorUtility.InstanceIDToObject(id) as GameObject;
            if (go == null) return;

            // checked on every repaint (no cache), so the icon is never stale
            if (go.GetComponentInChildren<BehaviorTree>(true) == null) return;

            if (icon == null) icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);

            // left of Behavior Designer's own icon slot
            var r = new Rect(row.xMax - 20f, row.y, 16f, 16f);
            // clicking the icon selects the first character inside that runs a tree,
            // so Behavior Designer opens its graph
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
            {
                foreach (BehaviorTree bt in go.GetComponentsInChildren<BehaviorTree>(true))
                {
                    Selection.activeGameObject = bt.gameObject;
                    EditorGUIUtility.PingObject(bt.gameObject);
                    EditorApplication.ExecuteMenuItem("Tools/Behavior Designer/Editor");
                    break;
                }
                e.Use();
            }
            if (icon != null) GUI.DrawTexture(r, icon, ScaleMode.ScaleToFit);
            else GUI.Label(r, "BT");
        }
    }
}
