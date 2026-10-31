using UnityEngine;
using UnityEngine.SceneManagement;
using RoadSafety.Core;
using RoadSafety.Player;

namespace RoadSafety.UI
{
    /// <summary>
    /// When a scene with a GameManager loads (Main.unity, and the same scene again after a
    /// restart), this adds what the gameplay needs if it isn't already in the scene:
    ///   _Managers/ProgressionManager, DriverControls, BadgeTracker, the music, UI/HUD and the
    ///   inside of the player's car (CarInterior: first-person view, friend, seatbelt).
    /// If the scene's player car has no CarController yet (level not built with
    /// RoadSafety > Level > Build Level 01), a simple one is added so the car still drives.
    /// </summary>
    public static class GameplayBootstrap
    {
        public const string EditorCoverName = "EditorCover";

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
            // Editor-only still picture of the new look, saved in the scene so Unity never shows the
            // bare level while Play mode stops (RoadSafety > Preview > Bake stop-screen picture).
            // The game hides it before its first frame.
            GameObject cover = GameObject.Find(EditorCoverName);
            if (cover != null && cover.scene == scene)
                cover.SetActive(false);

            GameManager game = Object.FindAnyObjectByType<GameManager>();
            if (game == null)
                return; // not a gameplay scene (menu, credits...)

            Time.timeScale = 1f;
            Transform managers = game.transform.parent != null ? game.transform.parent : game.transform;

            if (Object.FindAnyObjectByType<ProgressionManager>() == null)
                CreateChild<ProgressionManager>("ProgressionManager", managers);

            if (Object.FindAnyObjectByType<DriverControls>() == null)
                CreateChild<DriverControls>("DriverControls", managers);

            if (Object.FindAnyObjectByType<BadgeTracker>() == null)
                CreateChild<BadgeTracker>("BadgeTracker", managers);

            MusicPlayer.Ensure();
            EnsurePlayerCar();
            CarInterior.Ensure(); // first-person cockpit, driver, friend and the real seatbelt
            RoadSafety.World.StreetLook.Apply(); // golden-hour light, paved road, balconies (art of the reference)
            RoadSafety.AI.RiggedNpc.DressAll();  // every NPC gets the same rigged body as the friend
            RoadSafety.World.StreetLife.Apply(); // pigeons, seagulls, palms in the breeze

            if (Object.FindAnyObjectByType<HUDController>() == null)
            {
                HUDController hud = HUDBuilder.Build();
                GameObject uiRoot = GameObject.Find("UI");
                if (uiRoot != null && uiRoot.scene == scene)
                    hud.transform.SetParent(uiRoot.transform, false);
            }
        }

        private static void EnsurePlayerCar()
        {
            if (Object.FindAnyObjectByType<CarController>() != null)
                return;
            GameObject car = GameObject.Find("PlayerCar");
            if (car == null)
                return;
            var body = car.GetComponent<Rigidbody>();
            if (body == null)
            {
                body = car.AddComponent<Rigidbody>();
                body.mass = 1000f;
            }
            if (car.GetComponent<Collider>() == null)
            {
                var box = car.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.8f, 0f);
                box.size = new Vector3(1.8f, 1.2f, 4f);
            }
            car.AddComponent<CarController>();
            car.AddComponent<CrashFeedback>();
            car.AddComponent<ThreatSensor>();
            car.AddComponent<EngineSound>();
            Camera cam = Camera.main;
            if (cam != null && cam.GetComponent<CameraFollow>() == null)
                cam.gameObject.AddComponent<CameraFollow>().Target = car.transform;
        }

        private static T CreateChild<T>(string name, Transform parent) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<T>();
        }
    }
}
