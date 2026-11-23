using UnityEngine;

namespace RoadSafety.Core
{
    /// <summary>
    /// The grey debug boxes (behaviour tree, driver controls) shown in the Game view.
    /// Hidden at start so the child only sees the game; F1 shows / hides them
    /// (to show the behaviour tree to the teacher).
    /// They start under the settings / badges buttons of the HUD.
    /// </summary>
    public static class DebugView
    {
        public static bool Visible { get; set; }

        /// <summary>Top of the first box, in pixels (just under the top-left HUD buttons).</summary>
        public static float Top
        {
            get
            {
                float scale = Mathf.Sqrt((Screen.width / 1920f) * (Screen.height / 1080f));
                return 190f * scale;
            }
        }

        public static void Toggle() => Visible = !Visible;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Visible = false;
    }
}
