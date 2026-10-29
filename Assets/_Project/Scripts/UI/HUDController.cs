using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using RoadSafety.Core;
using RoadSafety.Player;

namespace RoadSafety.UI
{
    /// <summary>
    /// The in-game HUD (Day 1, Ranim):
    ///   - Go / Stop / left / right buttons -> DriverControls (US 1.1, 1.2)
    ///   - seatbelt button + avatar with / without seatbelt + voice reminder (US 1.3)
    ///   - horn button with a soft sound (US 1.4)
    ///   - star counter with a flying star, "pop" and joyful sound (US 3.1)
    ///   - 3 hearts and a gentle "let's try again" screen before the restart (US 3.4)
    /// The HUD only talks to DriverControls and ProgressionManager; it never touches the car.
    /// It is built in code by HUDBuilder (no scene editing needed), or from a prefab made with
    /// the menu RoadSafety > UI > Create HUD prefab.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        /// <summary>Everything the HUD needs on screen. Filled by HUDBuilder.</summary>
        [Serializable]
        public class Refs
        {
            public Font font;
            public AudioSource audioSource;

            [Header("Driving buttons")]
            public TouchButton go;
            public TouchButton stop;
            public TouchButton left;
            public TouchButton right;
            public TouchButton horn;

            [Header("Seatbelt")]
            public TouchButton belt;
            public Image beltIcon;
            public Sprite beltOff;
            public Sprite beltOn;
            public RectTransform avatar;
            public Image avatarImage;
            public Sprite avatarNoBelt;
            public Sprite avatarBelt;

            [Header("Speech bubble")]
            public RectTransform bubble;
            public CanvasGroup bubbleGroup;
            public Text bubbleText;

            [Header("Stars & hearts")]
            public RectTransform starCounter;
            public Image starIcon;
            public Text starCount;
            public Sprite starSprite;
            public RectTransform flyLayer;
            public Image[] hearts = new Image[0];
            public Sprite heartFull;
            public Sprite heartEmpty;

            [Header("Restart screen")]
            public CanvasGroup restartOverlay;
            public RectTransform restartHeart;
            public Text restartText;
        }

        // Modern Standard Arabic (not Tunisian dialect), as asked by the client.
        public const string BeltReminderText = "ضع حزام الأمان\nقبل أن تنطلق!";
        public const string BeltDoneText = "أحسنت! حزام الأمان يحميك";
        public const string RestartMessage = "لا بأس! لنحاول مرة أخرى";

        [SerializeField] private Refs ui = new Refs();

        [Header("Sounds (leave empty to use the built-in cartoon sounds)")]
        [SerializeField] private AudioClip hornSound;
        [SerializeField] private AudioClip starSound;
        [SerializeField] private AudioClip seatbeltSound;
        [SerializeField] private AudioClip reminderSound;
        [SerializeField] private AudioClip starLostSound;
        [SerializeField] private AudioClip heartLostSound;

        [Tooltip("Recorded Arabic voice: \"ضع حزام الأمان\". If empty, Resources/Audio/VO_Seatbelt_ar is used when it exists.")]
        [SerializeField] private AudioClip seatbeltVoice;

        [Header("Timing")]
        [SerializeField, Min(0.5f)] private float bubbleDuration = 2.8f;
        [SerializeField, Min(0f)] private float firstBeltHintDelay = 1.2f;

        public Refs UI => ui;

        private DriverControls controls;
        private ProgressionManager progression;
        private int shownStars;
        private int shownHearts;
        private Coroutine bubbleRoutine;
        private readonly Dictionary<Transform, Coroutine> scaleAnimations = new Dictionary<Transform, Coroutine>();
        private readonly Dictionary<RectTransform, Coroutine> shakeAnimations = new Dictionary<RectTransform, Coroutine>();
        private readonly Dictionary<RectTransform, Vector2> restPositions = new Dictionary<RectTransform, Vector2>();

        private void Start()
        {
            controls = DriverControls.Instance;
            progression = ProgressionManager.Instance;
            if (controls == null || progression == null)
            {
                Debug.LogError("[HUD] DriverControls or ProgressionManager is missing. GameplayBootstrap adds them to scenes that have a GameManager.", this);
                enabled = false;
                return;
            }

            EnsureEventSystem();
            LoadSounds();

            ui.go.HeldChanged += controls.SetAccelerate;
            ui.stop.HeldChanged += controls.SetBrake;
            ui.left.HeldChanged += controls.SetSteerLeft;
            ui.right.HeldChanged += controls.SetSteerRight;
            ui.horn.Pressed += controls.Honk;
            ui.belt.Pressed += OnBeltButton;

            controls.SeatbeltChanged += OnSeatbeltChanged;
            controls.Honked += OnHonked;
            controls.SeatbeltReminder += OnSeatbeltReminder;

            progression.StarGained += OnStarGained;
            progression.StarLost += OnStarLost;
            progression.HeartsChanged += OnHeartsChanged;
            progression.OutOfHearts += OnOutOfHearts;

            if (ui.hearts.Length != progression.MaxHearts)
                Debug.LogWarning($"[HUD] The HUD shows {ui.hearts.Length} hearts but ProgressionManager has {progression.MaxHearts}.", this);

            shownStars = progression.Stars;
            shownHearts = progression.Hearts;
            RefreshStars();
            RefreshHearts();
            RefreshSeatbelt();
            SetVisible(ui.bubbleGroup, false);
            SetVisible(ui.restartOverlay, false);
            if (ui.restartText != null)
                ui.restartText.text = ArabicText.Fix(RestartMessage);

            StartCoroutine(SeatbeltHint());
        }

        private void OnDestroy()
        {
            if (controls != null)
            {
                controls.SeatbeltChanged -= OnSeatbeltChanged;
                controls.Honked -= OnHonked;
                controls.SeatbeltReminder -= OnSeatbeltReminder;
            }
            if (progression != null)
            {
                progression.StarGained -= OnStarGained;
                progression.StarLost -= OnStarLost;
                progression.HeartsChanged -= OnHeartsChanged;
                progression.OutOfHearts -= OnOutOfHearts;
            }
        }

        // ---------- Seatbelt (US 1.3) ----------

        private void OnBeltButton()
        {
            if (controls.SeatbeltOn)
                PlayScale(ui.avatar, 1.12f, 0.3f); // already on: a little happy bounce
            else
                controls.FastenSeatbelt();
        }

        private void OnSeatbeltChanged(bool on)
        {
            RefreshSeatbelt();
            if (!on)
                return;
            Play(seatbeltSound);
            PlayScale(ui.avatar, 1.2f, 0.4f);
            PlayScale(ui.belt.transform, 1.15f, 0.3f);
            ShowBubble(BeltDoneText);
        }

        private void OnSeatbeltReminder()
        {
            Play(seatbeltVoice != null ? seatbeltVoice : reminderSound);
            ShowBubble(BeltReminderText);
            PlayScale(ui.belt.transform, 1.3f, 0.5f);
            PlayShake(ui.avatar, 14f, 0.45f);
        }

        private void RefreshSeatbelt()
        {
            bool on = controls != null && controls.SeatbeltOn;
            if (ui.beltIcon != null) ui.beltIcon.sprite = on ? ui.beltOn : ui.beltOff;
            if (ui.avatarImage != null) ui.avatarImage.sprite = on ? ui.avatarBelt : ui.avatarNoBelt;
        }

        /// <summary>Until the belt is on, the belt button gently pulses to show where to tap.</summary>
        private IEnumerator SeatbeltHint()
        {
            yield return new WaitForSecondsRealtime(firstBeltHintDelay);
            while (controls != null && !controls.SeatbeltOn)
            {
                PlayScale(ui.belt.transform, 1.12f, 0.5f);
                yield return new WaitForSecondsRealtime(1.4f);
            }
        }

        // ---------- Horn (US 1.4) ----------

        private void OnHonked()
        {
            Play(hornSound);
            PlayScale(ui.horn.transform, 1.12f, 0.25f);
        }

        // ---------- Stars (US 3.1) ----------

        private void OnStarGained(int total, string reason)
        {
            Play(starSound);
            StartCoroutine(FlyingStar());
        }

        private void OnStarLost(int total, string reason)
        {
            shownStars = total;
            RefreshStars();
            Play(starLostSound);
            PlayShake(ui.starCounter, 18f, 0.4f);
        }

        private void RefreshStars()
        {
            if (ui.starCount != null)
                ui.starCount.text = shownStars.ToString();
        }

        /// <summary>A big star pops in the middle of the screen, then flies into the counter.</summary>
        private IEnumerator FlyingStar()
        {
            var go = new GameObject("FlyingStar", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(ui.flyLayer, false);
            rect.sizeDelta = new Vector2(220f, 220f);
            rect.anchoredPosition = new Vector2(0f, 80f);
            var image = go.GetComponent<Image>();
            image.sprite = ui.starSprite;
            image.raycastTarget = false;

            yield return Animate(0.3f, k => rect.localScale = Vector3.one * EaseOutBack(k) * 1.2f);
            yield return Animate(0.2f, k => rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(k * Mathf.PI * 2f) * 10f));

            Vector3 from = rect.localPosition;
            Vector3 to = ui.flyLayer.InverseTransformPoint(ui.starIcon.rectTransform.position);
            yield return Animate(0.45f, k =>
            {
                float e = k * k;
                rect.localPosition = Vector3.Lerp(from, to, e) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 120f;
                rect.localScale = Vector3.one * Mathf.Lerp(1.2f, 0.4f, e);
            });
            Destroy(go);

            shownStars = Mathf.Min(shownStars + 1, progression.Stars);
            RefreshStars();
            PlayScale(ui.starCounter, 1.3f, 0.3f);
        }

        // ---------- Hearts (US 3.4) ----------

        private void OnHeartsChanged(int remaining)
        {
            for (int i = Mathf.Max(0, remaining); i < shownHearts && i < ui.hearts.Length; i++)
                StartCoroutine(EmptyHeart(ui.hearts[i]));
            shownHearts = remaining;
            Play(heartLostSound);
        }

        private void RefreshHearts()
        {
            for (int i = 0; i < ui.hearts.Length; i++)
                ui.hearts[i].sprite = i < shownHearts ? ui.heartFull : ui.heartEmpty;
        }

        private IEnumerator EmptyHeart(Image heart)
        {
            Transform t = heart.transform;
            yield return Animate(0.15f, k => t.localScale = Vector3.one * (1f + 0.3f * k));
            heart.sprite = ui.heartEmpty;
            yield return Animate(0.25f, k => t.localScale = Vector3.one * Mathf.Lerp(1.3f, 1f, EaseOutBack(k)));
        }

        /// <summary>No hearts left: a calm, encouraging screen. ProgressionManager restarts the level.</summary>
        private void OnOutOfHearts()
        {
            controls.ReleaseAll();
            StartCoroutine(ShowRestartScreen());
        }

        private IEnumerator ShowRestartScreen()
        {
            CanvasGroup group = ui.restartOverlay;
            if (group == null)
                yield break;
            group.blocksRaycasts = true;
            yield return Animate(0.5f, k => group.alpha = k);
            while (group != null)
            {
                if (ui.restartHeart != null)
                    PlayScale(ui.restartHeart, 1.15f, 0.6f);
                yield return new WaitForSecondsRealtime(0.8f);
            }
        }

        // ---------- Speech bubble ----------

        private void ShowBubble(string text)
        {
            if (ui.bubble == null)
                return;
            ui.bubbleText.text = ArabicText.Fix(text, 26);
            if (bubbleRoutine != null)
                StopCoroutine(bubbleRoutine);
            bubbleRoutine = StartCoroutine(BubbleRoutine());
        }

        private IEnumerator BubbleRoutine()
        {
            CanvasGroup group = ui.bubbleGroup;
            float startAlpha = group.alpha;
            yield return Animate(0.2f, k =>
            {
                group.alpha = Mathf.Lerp(startAlpha, 1f, k);
                ui.bubble.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, EaseOutBack(k));
            });
            yield return new WaitForSecondsRealtime(bubbleDuration);
            yield return Animate(0.3f, k => group.alpha = 1f - k);
            bubbleRoutine = null;
        }

        // ---------- Helpers ----------

        private void LoadSounds()
        {
            if (hornSound == null) hornSound = HUDSounds.Horn();
            if (starSound == null) starSound = HUDSounds.StarWon();
            if (seatbeltSound == null) seatbeltSound = HUDSounds.SeatbeltClick();
            if (reminderSound == null) reminderSound = HUDSounds.Reminder();
            if (starLostSound == null) starLostSound = HUDSounds.StarLost();
            if (heartLostSound == null) heartLostSound = HUDSounds.HeartLost();
            if (seatbeltVoice == null) seatbeltVoice = Resources.Load<AudioClip>("Audio/VO_Seatbelt_ar");
        }

        private void Play(AudioClip clip)
        {
            if (clip != null && ui.audioSource != null)
                ui.audioSource.PlayOneShot(clip);
        }

        private static void SetVisible(CanvasGroup group, bool visible)
        {
            if (group == null)
                return;
            group.alpha = visible ? 1f : 0f;
            group.blocksRaycasts = visible;
            group.interactable = visible;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null)
                return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            // The project's Active Input Handling is "Both": the classic module handles mouse and
            // multi-touch and was the one tested. If the project switches to the new Input System
            // only, the Input System module is used instead.
#if ENABLE_LEGACY_INPUT_MANAGER
            go.AddComponent<StandaloneInputModule>();
#elif ENABLE_INPUT_SYSTEM
            var module = go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            module.AssignDefaultActions();
#endif
        }

        /// <summary>Scale up and back to 1 (only one scale animation per object at a time).</summary>
        private void PlayScale(Transform target, float peak, float duration)
        {
            if (target == null)
                return;
            if (scaleAnimations.TryGetValue(target, out Coroutine running) && running != null)
                StopCoroutine(running);
            target.localScale = Vector3.one;
            scaleAnimations[target] = StartCoroutine(Animate(duration,
                k => target.localScale = Vector3.one * (1f + (peak - 1f) * Mathf.Sin(k * Mathf.PI))));
        }

        /// <summary>Little side-to-side wiggle (only one per object at a time).</summary>
        private void PlayShake(RectTransform target, float amount, float duration)
        {
            if (target == null)
                return;
            if (!restPositions.TryGetValue(target, out Vector2 rest))
                restPositions[target] = rest = target.anchoredPosition;
            if (shakeAnimations.TryGetValue(target, out Coroutine running) && running != null)
                StopCoroutine(running);
            shakeAnimations[target] = StartCoroutine(Animate(duration, k =>
                target.anchoredPosition = rest + Vector2.right * Mathf.Sin(k * Mathf.PI * 6f) * amount * (1f - k)));
        }

        private static IEnumerator Animate(float duration, Action<float> step)
        {
            float time = 0f;
            while (time < duration)
            {
                step(time / duration);
                yield return null;
                time += Time.unscaledDeltaTime;
            }
            step(1f);
        }

        private static float EaseOutBack(float k)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float x = k - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }
    }
}
