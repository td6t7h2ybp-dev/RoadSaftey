using UnityEngine;
using UnityEngine.SceneManagement;
using RoadSafety.Core;
using RoadSafety.Player;

namespace RoadSafety.UI
{
    /// <summary>
    /// When a scene with a GameManager loads (Main.unity, and the same scene again after a
    /// restart), this adds what the gameplay needs if it isn't already in the scene:
    ///   _Managers/ProgressionManager, _Managers/DriverControls and UI/HUD.
    /// So Main.unity doesn't need to be edited for the HUD to work. If you place your own
    /// HUD prefab (RoadSafety > UI > Create HUD prefab) in the scene, it is used instead.
    /// </summary>
    public static class GameplayBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // The project uses "Enter Play Mode Options" without scene reload, so in the Editor
        // sceneLoaded is not raised for the scene that is already open when Play is pressed.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SetUpFirstScene() => SetUp(SceneManager.GetActiveScene());

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single)
                SetUp(scene);
        }

        /// <summary>Adds what is missing; does nothing if it is already there (safe to call twice).</summary>
        private static void SetUp(Scene scene)
        {
            GameManager game = Object.FindAnyObjectByType<GameManager>();
            if (game == null)
                return; // not a gameplay scene (menu, credits...)

            Transform managers = game.transform.parent != null ? game.transform.parent : game.transform;

            if (Object.FindAnyObjectByType<ProgressionManager>() == null)
                CreateChild<ProgressionManager>("ProgressionManager", managers);

            if (Object.FindAnyObjectByType<DriverControls>() == null)
                CreateChild<DriverControls>("DriverControls", managers);

            if (Object.FindAnyObjectByType<HUDController>() == null)
            {
                HUDController hud = HUDBuilder.Build();
                GameObject uiRoot = GameObject.Find("UI");
                if (uiRoot != null && uiRoot.scene == scene)
                    hud.transform.SetParent(uiRoot.transform, false);
            }
        }

        private static T CreateChild<T>(string name, Transform parent) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<T>();
        }
    }
}
