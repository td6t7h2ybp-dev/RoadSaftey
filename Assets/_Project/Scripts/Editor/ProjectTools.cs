using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using RoadSafety.Core;

namespace RoadSafety.EditorTools
{
    /// <summary>Small project menus: Android tablet settings and resetting the saved badges.</summary>
    public static class ProjectTools
    {
        /// <summary>Landscape only (left or right), app id and names for the Android tablet build.</summary>
        [MenuItem("RoadSafety/Setup/Android tablet settings")]
        public static void SetUpAndroid()
        {
            PlayerSettings.companyName = "ISAMM Equipe 01";
            PlayerSettings.productName = "RoadSafety";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.isamm.roadsafety");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            AssetDatabase.SaveAssets();
            Debug.Log("[RoadSafety] Android settings: landscape only, com.isamm.roadsafety.");
        }

        [MenuItem("RoadSafety/Reset badges and stars")]
        public static void ResetBadges()
        {
            Badges.ResetAll();
            Debug.Log("[RoadSafety] Badges and total stars reset.");
        }
    }
}
