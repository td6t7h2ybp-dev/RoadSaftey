using UnityEditor;
using UnityEngine;
using RoadSafety.UI;

namespace RoadSafety.EditorTools
{
    /// <summary>
    /// Optional: saves the code-built HUD as a prefab so it can be tweaked by hand in the Editor.
    /// Drop the prefab under "UI" in Main.unity and GameplayBootstrap will use it instead of
    /// building a new HUD. (Without a prefab in the scene, the HUD is built in code at Play.)
    /// </summary>
    public static class HUDPrefabMenu
    {
        private const string Folder = "Assets/_Project/Prefabs/UI";
        private const string PrefabPath = Folder + "/HUD.prefab";

        [MenuItem("RoadSafety/UI/Create HUD prefab")]
        private static void CreatePrefab()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "UI");

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null &&
                !EditorUtility.DisplayDialog("HUD prefab", "HUD.prefab already exists. Replace it?", "Replace", "Cancel"))
                return;

            HUDController hud = HUDBuilder.Build();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(hud.gameObject, PrefabPath);
            Object.DestroyImmediate(hud.gameObject);

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"[HUD] Saved {PrefabPath}");
        }
    }
}
