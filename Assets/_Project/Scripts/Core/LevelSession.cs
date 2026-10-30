using UnityEngine;

namespace RoadSafety.Core
{
    /// <summary>
    /// Which level is being played. The same street is used for every level; each new level
    /// adds traffic and makes the traffic light a little faster ("Niveau suivant", US 3.3).
    /// </summary>
    public static class LevelSession
    {
        public const int MaxLevel = 3;

        public static int Level { get; private set; } = 1;

        /// <summary>How many oncoming cars drive on the main road.</summary>
        public static int OncomingCars => Mathf.Clamp(Level + 1, 1, 4);

        /// <summary>Speed of the other cars (m/s).</summary>
        public static float TrafficSpeed => 5.5f + 0.75f * (Level - 1);

        /// <summary>Multiplier on the traffic-light durations (smaller = faster changes).</summary>
        public static float LightTimeScale => 1f - 0.12f * (Level - 1);

        public static void NextLevel() => Level = Level >= MaxLevel ? 1 : Level + 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Level = 1;
    }
}
