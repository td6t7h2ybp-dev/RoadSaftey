using UnityEngine;
using RoadSafety.Core;

namespace RoadSafety.Player
{
    /// <summary>
    /// The soft cartoon engine of the child's car, played by the AudioSource on PlayerCar
    /// (Resources/Audio/Engine_Loop). Pitch and volume follow the speed. The engine starts
    /// when the seatbelt is on (US 1.3), pauses with the menus and follows the sound-effects
    /// switch (US 4.4).
    /// </summary>
    [RequireComponent(typeof(CarController), typeof(AudioSource))]
    public class EngineSound : MonoBehaviour
    {
        private const string ClipPath = "Audio/Engine_Loop";

        [Tooltip("Volume at top speed. Keep it under the voices.")]
        [SerializeField, Range(0f, 1f)] private float maxVolume = 0.3f;
        [Tooltip("Part of the volume kept when the car stands still.")]
        [SerializeField, Range(0f, 1f)] private float idleVolume = 0.5f;
        [Tooltip("Pitch at top speed (1 = when the car stands still).")]
        [SerializeField, Min(1f)] private float maxPitch = 2f;
        [Tooltip("How fast the sound follows the car (higher = faster).")]
        [SerializeField, Min(0.1f)] private float response = 8f;

        private CarController car;
        private AudioSource source;

        private void Awake()
        {
            car = GetComponent<CarController>();
            source = GetComponent<AudioSource>();
            if (source.clip == null)
                source.clip = Resources.Load<AudioClip>(ClipPath);
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            Apply();
            if (source.clip != null)
                source.Play();
            AudioPrefs.Changed += Apply;
        }

        private void OnDestroy() => AudioPrefs.Changed -= Apply;

        private void Apply() => source.mute = !AudioPrefs.SfxOn;

        private void Update()
        {
            if (Time.timeScale == 0f)
            {
                source.Pause();
                return;
            }
            source.UnPause();

            float k = Mathf.Clamp01(Mathf.Abs(car.Speed) / car.MaxSpeed);
            float smooth = 1f - Mathf.Exp(-response * Time.deltaTime);
            float volume = EngineRunning() ? maxVolume * Mathf.Lerp(idleVolume, 1f, k) : 0f;
            source.volume = Mathf.Lerp(source.volume, volume, smooth);
            source.pitch = Mathf.Lerp(source.pitch, Mathf.Lerp(1f, maxPitch, k), smooth);
        }

        private static bool EngineRunning()
        {
            DriverControls controls = DriverControls.Instance;
            ProgressionManager progression = ProgressionManager.Instance;
            bool levelStopped = progression != null && (progression.IsRestarting || progression.IsLevelOver);
            return controls != null && controls.SeatbeltOn && !levelStopped;
        }
    }
}
