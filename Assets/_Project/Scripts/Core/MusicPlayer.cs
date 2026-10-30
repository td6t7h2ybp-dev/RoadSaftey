using UnityEngine;

namespace RoadSafety.Core
{
    /// <summary>
    /// Joyful background music (charte graphique: "musique joyeuse"), made for the game
    /// (no copyright): Resources/Audio/Music_Loop. Keeps playing when the level restarts.
    /// Switched on / off in the settings menu (US 4.4).
    /// </summary>
    public class MusicPlayer : MonoBehaviour
    {
        private const string ClipPath = "Audio/Music_Loop";

        private static MusicPlayer instance;
        private AudioSource source;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        public static void Ensure()
        {
            if (instance != null)
                return;
            var go = new GameObject("Music");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<MusicPlayer>();
        }

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.clip = Resources.Load<AudioClip>(ClipPath);
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0.32f;
            source.ignoreListenerPause = true;
            Apply();
            if (source.clip != null)
                source.Play();
            AudioPrefs.Changed += Apply;
        }

        private void OnDestroy()
        {
            AudioPrefs.Changed -= Apply;
            if (instance == this)
                instance = null;
        }

        private void Apply()
        {
            if (source != null)
                source.mute = !AudioPrefs.MusicOn;
        }
    }
}
