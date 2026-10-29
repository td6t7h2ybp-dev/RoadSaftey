using System;
using UnityEngine;
using RoadSafety.Core;

namespace RoadSafety.Player
{
    /// <summary>
    /// The one place every action of the child goes through (US 4.3: "les actions du joueur
    /// passent par une fonction utilitaire centralisée").
    ///
    /// The HUD buttons (and the keyboard, for testing in the Editor) CALL the methods below.
    /// The car controller only READS the values, for example in its FixedUpdate:
    /// <code>
    ///     DriverControls input = DriverControls.Instance;
    ///     float throttle = input.Throttle;   // 0..1, stays 0 until the seatbelt is on (US 1.3)
    ///     bool  braking  = input.Braking;    // Stop button held (US 1.1)
    ///     float steer    = input.Steer;      // -1 (left) .. +1 (right), ramps up while held (US 1.2)
    /// </code>
    /// One DriverControls is added automatically to every scene that has a GameManager
    /// (see GameplayBootstrap), so read it in Start/Update, not in Awake.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class DriverControls : MonoBehaviour
    {
        public static DriverControls Instance { get; private set; }

        [Header("Steering (US 1.2)")]
        [Tooltip("Seconds to go from straight to full turn while an arrow is held.")]
        [SerializeField, Min(0.01f)] private float steerRampTime = 0.6f;

        [Tooltip("Seconds to come back to straight after the arrow is released.")]
        [SerializeField, Min(0.01f)] private float steerReturnTime = 0.25f;

        [Header("Seatbelt (US 1.3)")]
        [Tooltip("Minimum seconds between two 'put your seatbelt on' reminders.")]
        [SerializeField, Min(0f)] private float reminderCooldown = 3f;

        [Header("Horn (US 1.4)")]
        [Tooltip("Minimum seconds between two honks, so the horn can't be spammed.")]
        [SerializeField, Min(0f)] private float hornCooldown = 0.45f;

        [Header("Testing")]
        [Tooltip("Editor keyboard: arrows / WASD drive, Space brake, B seatbelt, K horn.")]
        [SerializeField] private bool keyboardControls = true;

        [Tooltip("Show the control values under the behaviour-tree box in the Game view.")]
        [SerializeField] private bool showDebugOverlay = true;

        private bool accelerateHeld, brakeHeld, leftHeld, rightHeld;
        private bool keyAccelerate, keyBrake, keyLeft, keyRight;
        private float steer;
        private float lastReminderTime = float.NegativeInfinity;
        private float lastHornTime = float.NegativeInfinity;

        /// <summary>True once the child has fastened the seatbelt (it stays on for the level).</summary>
        public bool SeatbeltOn { get; private set; }

        /// <summary>False while the seatbelt is off or the level is restarting.</summary>
        public bool CanDrive => SeatbeltOn && !IsLocked;

        /// <summary>Go button held (even if the car is not allowed to move yet).</summary>
        public bool AccelerateHeld => accelerateHeld || keyAccelerate;

        /// <summary>0..1. The Go button gives 1 while held; 0 when released, braking, or no seatbelt.</summary>
        public float Throttle => CanDrive && AccelerateHeld && !Braking ? 1f : 0f;

        /// <summary>Stop button held.</summary>
        public bool Braking => !IsLocked && (brakeHeld || keyBrake);

        /// <summary>-1 = full left, 0 = straight, +1 = full right. Changes progressively.</summary>
        public float Steer => IsLocked ? 0f : steer;

        private bool IsLocked => ProgressionManager.Instance != null && ProgressionManager.Instance.IsRestarting;

        /// <summary>Raised when the seatbelt is fastened (true).</summary>
        public event Action<bool> SeatbeltChanged;

        /// <summary>Raised when the horn sounds (the HUD plays the sound).</summary>
        public event Action Honked;

        /// <summary>Raised when the child must be told to fasten the seatbelt (voice + bubble on the HUD).</summary>
        public event Action SeatbeltReminder;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[DriverControls] More than one DriverControls in the scene; removing the extra one.", this);
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                ReleaseAll();
        }

        // ---------- Actions (called by the HUD buttons) ----------

        /// <summary>Go button pressed (true) or released (false).</summary>
        public void SetAccelerate(bool held)
        {
            bool pressedNow = held && !AccelerateHeld;
            accelerateHeld = held;
            if (pressedNow)
                OnAcceleratePressed();
        }

        /// <summary>Stop button pressed (true) or released (false).</summary>
        public void SetBrake(bool held) => brakeHeld = held;

        /// <summary>Left arrow pressed (true) or released (false).</summary>
        public void SetSteerLeft(bool held) => leftHeld = held;

        /// <summary>Right arrow pressed (true) or released (false).</summary>
        public void SetSteerRight(bool held) => rightHeld = held;

        /// <summary>Seatbelt button: fastens the belt, gives the seatbelt star (once per level).</summary>
        public void FastenSeatbelt()
        {
            if (SeatbeltOn || IsLocked)
                return;
            SeatbeltOn = true;
            SeatbeltChanged?.Invoke(true);
            if (ProgressionManager.Instance != null)
                ProgressionManager.Instance.AddStarOnce("seatbelt", GoodAction.Seatbelt);
        }

        /// <summary>Horn button: a short, friendly honk. Never hurts anyone (US 1.4).</summary>
        public void Honk()
        {
            if (Time.unscaledTime - lastHornTime < hornCooldown)
                return;
            lastHornTime = Time.unscaledTime;
            Honked?.Invoke();
        }

        /// <summary>Asks the HUD to remind the child about the seatbelt (with a cooldown).</summary>
        public void RemindSeatbelt()
        {
            if (SeatbeltOn || Time.unscaledTime - lastReminderTime < reminderCooldown)
                return;
            lastReminderTime = Time.unscaledTime;
            SeatbeltReminder?.Invoke();
        }

        /// <summary>Lets go of every held button (focus lost, level restarting...).</summary>
        public void ReleaseAll()
        {
            accelerateHeld = brakeHeld = leftHeld = rightHeld = false;
            keyAccelerate = keyBrake = keyLeft = keyRight = false;
        }

        private void OnAcceleratePressed()
        {
            // The car can't start without the seatbelt: a voice asks the child to put it on (US 1.3).
            if (!SeatbeltOn)
                RemindSeatbelt();
        }

        // ---------- Update ----------

        private void Update()
        {
            ReadKeyboard();

            int direction = (leftHeld || keyLeft ? -1 : 0) + (rightHeld || keyRight ? 1 : 0);
            float rate = direction != 0 ? 1f / steerRampTime : 1f / steerReturnTime;
            steer = Mathf.MoveTowards(steer, direction, rate * Time.deltaTime);
        }

        private void ReadKeyboard()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (!keyboardControls)
                return;

            bool up = Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W);
            if (up && !keyAccelerate && !accelerateHeld)
                OnAcceleratePressed();
            keyAccelerate = up;
            keyBrake = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S);
            keyLeft = Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A);
            keyRight = Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D);

            if (Input.GetKeyDown(KeyCode.B)) FastenSeatbelt();
            if (Input.GetKeyDown(KeyCode.K)) Honk();
#endif
        }

        private void OnGUI()
        {
            if (!showDebugOverlay)
                return;
            GUI.Box(new Rect(10, 140, 240, 96), "Driver controls");
            GUI.Label(new Rect(20, 162, 220, 20), $"Seatbelt: {(SeatbeltOn ? "ON" : "off")}   Throttle: {Throttle:0}");
            GUI.Label(new Rect(20, 180, 220, 20), $"Brake: {(Braking ? "yes" : "no")}   Steer: {Steer:+0.00;-0.00;0}");
            GUI.Label(new Rect(20, 200, 220, 20), "Arrows drive | Space brake");
            GUI.Label(new Rect(20, 216, 220, 20), "B belt | K horn | 1/2 star | 3 heart");
        }
    }
}
