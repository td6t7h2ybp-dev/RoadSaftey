using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RoadSafety.World;

namespace RoadSafety.EditorTools
{
    /// <summary>
    /// RoadSafety > Level > Add street life
    /// Puts the pigeons, seagulls, cats and swaying palms in Main.unity (replacing any older
    /// street life), gives each of them its Behavior Designer tree and saves the scene.
    /// </summary>
    public static class StreetLifeBake
    {
        [MenuItem("RoadSafety/Level/Add street life")]
        public static void Add()
        {
            ScenePreview.HideMenu();
            foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == StreetLife.RootName)
                    Object.DestroyImmediate(root);
            StreetLife.Build();
            BTBake.Bake(); // attaches every Behavior Designer tree (street life included) and saves
            Debug.Log("[StreetLife] Pigeons, seagulls, cats and palms added with their Behavior Designer trees.");
        }
    }
}
