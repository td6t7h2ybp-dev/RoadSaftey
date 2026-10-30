using System;
using UnityEngine;

namespace RoadSafety.Core
{
    /// <summary>
    /// Music and sound-effect switches of the settings menu (US 4.4), saved on the tablet.
    /// </summary>
    public static class AudioPrefs
    {
        private const string MusicKey = "rs_music_on";
        private const string SfxKey = "rs_sfx_on";

        public static event Action Changed;

        public static bool MusicOn
        {
            get => PlayerPrefs.GetInt(MusicKey, 1) == 1;
            set { PlayerPrefs.SetInt(MusicKey, value ? 1 : 0); PlayerPrefs.Save(); Changed?.Invoke(); }
        }

        public static bool SfxOn
        {
            get => PlayerPrefs.GetInt(SfxKey, 1) == 1;
            set { PlayerPrefs.SetInt(SfxKey, value ? 1 : 0); PlayerPrefs.Save(); Changed?.Invoke(); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Changed = null;
    }
}
