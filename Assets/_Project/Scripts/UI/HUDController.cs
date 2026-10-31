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
    /// The in-game HUD:
    ///   - Go / Stop / left / right buttons -> DriverControls (US 1.1, 1.2)
    ///   - seatbelt button + avatar with / without seatbelt + voice reminder (US 1.3)
    ///   - horn button with a soft sound (US 1.4)
    ///   - star counter with a flying star, "pop" and joyful sound (US 3.1)
    ///   - 3 hearts and a gentle "let's try again" screen before the restart (US 3.4)
    ///   - speech bubble + voice for every message of the game (GameMessages.Say, US 4.1)
    ///   - danger sign when the behaviour tree sees something in front of the car
    ///   - settings (US 4.4), badges (US 3.2) and the end-of-level screen (US 3.3)
    /// The HUD only talks to DriverControls, ProgressionManager and GameMessages; it never
    /// touches the car. It is built in code by HUDBuilder (no scene editing needed).
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        /// <summary>Everything the HUD needs on screen. Filled by HUDBuilder.</summary>
        [Serializable]
        public class Refs
        {
            public Font font;
            public AudioSource audioSource;
            public AudioSource voiceSource;

            [Header("Driving buttons")]
            public TouchButton go;
            public TouchButton stop;
            public TouchButton left;
            public TouchButton right;
            public TouchButton horn;

            [Header("Menu buttons")]
            public TouchButton settingsButton;
            public TouchButton badgesButton;
            public Text levelText;

            [Header("Seatbelt")]
            public TouchButton belt;
            public Image beltIcon;
            public Sprite beltOff;
            public Sprite beltOn;
            public RectTransform avatar;
            public Image avatarImage;
            public Sprite avatarNoBelt;
            public Sprite avatarBelt;
            public RectTransform danger;

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

            [Header("Menus")]
            public BadgePopup badgePopup;
            public SettingsMenu settingsMenu;
            public BadgeMenu badgeMenu;
            public EndScreen endScreen;
        }

        [SerializeField] private Refs ui = new Refs();

        [Header("Sounds (leave empty to use the built-in cartoon sounds)")]
        [SerializeField] private AudioClip hornSound;
        [SerializeField] private AudioClip starSound;
        [SerializeField] private AudioClip seatbeltSound;
        [SerializeField] private AudioClip reminderSound;
        [SerializeField] private AudioClip starLostSound;
        [SerializeField] private AudioClip heartLostSound;
        [SerializeField] private AudioClip dangerSound;

        [Header("Timing")]
        [SerializeField, Min(0.5f)] private float bubbleDuration = 3.5f;
        [SerializeField, Min(0f)] private float firstBeltHintDelay = 1.2f;
        [Tooltip("At the start of the level, the voice asks for the seatbelt after this delay.")]
        [SerializeField, Min(0f)] private float startReminderDelay = 2f;
        [SerializeField, Min(0f)] private float endScreenDelay = 1.6f;

        public Refs UI => ui;

        private DriverControls controls;
        private ProgressionManager progression;
        private GameManager game;
        private int shownStars;
        private int shownHearts;
        private Coroutine bubbleRoutine;
        private Coroutine dangerRoutine;
        private readonly Dictionary<Transform, Coroutine> scaleAnimations = new Dictionary<Transform, Coroutine>();
        private readonly Dictionary<RectTransform, Coroutine> shakeAnimations = new Dictionary<RectTransform, Coroutine>();
        private readonly Dictionary<RectTransform, Vector2> restPositions = new Dictionary<RectTransform, Vector2>();

        private void Start()
        {
            controls = DriverControls.Instance;
            progression = ProgressionManager.Instance;
            game = GameManager.Instance;
            if (controls == null || progression == null)
            {
                Debug.LogError("[HUD] DriverControls or ProgressionManager is missing. GameplayBootstrap adds them to scenes that have a GameManager.", this);
                enabled = false;
                return;
            }

            EnsureEventSystem();
            LoadSounds();
            HudRestyle.Apply(ui); // 3D buttons in the charte colours, whatever built the HUD

            ui.go.HeldChanged += controls.SetAccelerate;
            ui.stop.HeldChanged += controls.SetBrake;
            ui.left.HeldChanged += controls.SetSteerLeft;
            ui.right.HeldChanged += controls.SetSteerRight;
            ui.horn.Pressed += controls.Honk;
            ui.belt.Pressed += OnBeltButton;
            if (ui.settingsButton != null) ui.settingsButton.Clicked += OpenSettings;
            if (ui.badgesButton != null) ui.badgesButton.Clicked += OpenBadges;

            controls.SeatbeltChanged += OnSeatbeltChanged;
            controls.Honked += OnHonked;
            controls.SeatbeltReminder += OnSeatbeltReminder;

            progression.StarGained += OnStarGained;
            progression.StarLost += OnStarLost;
            progression.HeartsChanged += OnHeartsChanged;
            progression.OutOfHearts += OnOutOfHearts;
            progression.LevelEnded += OnLevelEnded;

            GameMessages.Said += OnSaid;
            GameMessages.SfxRequested += Play;
            Badges.Unlocked += OnBadgeUnlocked;
            if (game != null)
                game.StateChanged += OnGameStateChanged;

            if (ui.hearts.Length != progression.MaxHearts)
                Debug.LogWarning($"[HUD] The HUD shows {ui.hearts.Length} hearts but ProgressionManager has {progression.MaxHearts}.", this);

            shownStars = progression.Stars;
            shownHearts = progression.Hearts;
            RefreshStars();
            RefreshHearts();
            RefreshSeatbelt();
            UIKit.SetVisible(ui.bubbleGroup, false);
            UIKit.SetVisible(ui.restartOverlay, false);
            if (ui.restartText != null)
                ui.restartText.text = ArabicText.Fix(Lines.TryAgain);
            if (ui.levelText != null)
                ui.levelText.text = ArabicText.Fix("المستوى " + LevelSession.Level);

            StartCoroutine(SeatbeltHint());
            StartCoroutine(StartReminder());
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
                progression.LevelEnded -= OnLevelEnded;
            }
            if (game != null)
                game.StateChanged -= OnGameStateChanged;
            GameMessages.Said -= OnSaid;
            GameMessages.SfxRequested -= Play;
            Badges.Unlocked -= OnBadgeUnlocked;
            if (Time.timeScale == 0f)
                Time.timeScale = 1f;
        }

        // ---------- Messages (bubble + voice) ----------

        private void OnSaid(string line)
        {
            ShowBubble(line);
            PlayScale(ui.avatar, 1.1f, 0.35f);
            AudioClip voice = Lines.VoiceFor(line);
            // the friend in the car says it (same recorded voices, from their mouth)
            if (PassengerFriend.Current != null && PassengerFriend.Current.isActiveAndEnabled)
            {
                PassengerFriend.Current.Say(line, voice);
                return;
            }
            if (voice != null && ui.voiceSource != null && AudioPrefs.SfxOn)
            {
                ui.voiceSource.Stop();
                ui.voiceSource.clip = voice;
                ui.voiceSource.Play();
            }
        }

        /// <summary>
        /// The friend now sits in the car and the seatbelt is a real one (CarInterior):
        /// hides the 2D avatar, its bubble tail and the seatbelt button. The bubble stays for the text.
        /// </summary>
        public void UseInCarFriend()
        {
            HudRestyle.Apply(ui); // also used by the Editor preview, which never calls Start()
            // The friend sits in the car and the seatbelt is a real one you pull (CarInterior):
            // no 2D avatar thumbnail and no seatbelt button on the HUD.
            HideAvatarAndBeltButton(ui);
            if (ui.bubble != null)
            {
                Transform tail = ui.bubble.Find("Tail");
                if (tail != null) tail.gameObject.SetActive(false);
                // the bubble now floats next to the friend's head, with a pointer to them
                SpeechBubbleFollow.Attach(ui.bubble);
            }
        }

        /// <summary>Hides the 2D avatar and the seatbelt button (the friend and the belt are in the car).</summary>
        public static void HideAvatarAndBeltButton(Refs refs)
        {
            if (refs == null) return;
            if (refs.avatar != null) refs.avatar.gameObject.SetActive(false);
            if (refs.belt != null)
            {
                refs.belt.ForceRelease();
                refs.belt.gameObject.SetActive(false);
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
            GameMessages.Say(Lines.SeatbeltDone);
        }

        private void OnSeatbeltReminder()
        {
            Play(reminderSound);
            GameMessages.Say(Lines.SeatbeltFirst);
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

        /// <summary>The voice asks for the seatbelt at the start of the level (US 1.3).</summary>
        private IEnumerator StartReminder()
        {
            yield return new WaitForSeconds(startReminderDelay);
            if (controls != null && !controls.SeatbeltOn)
                controls.RemindSeatbelt();
        }

        // ---------- Horn (US 1.4) ----------

        private void OnHonked()
        {
            Play(hornSound);
            PlayScale(ui.horn.transform, 1.12f, 0.25f);
        }

        // ---------- Danger sign (behaviour tree "Avoid") ----------

        private void OnGameStateChanged()
        {
            bool show = game != null && game.HasEnemies;
            if (ui.danger == null || ui.danger.gameObject.activeSelf == show)
                return;
            ui.danger.gameObject.SetActive(show);
            if (show)
            {
                Play(dangerSound);
                if (dangerRoutine != null)
                    StopCoroutine(dangerRoutine);
                dangerRoutine = StartCoroutine(PulseDanger());
            }
        }

        private IEnumerator PulseDanger()
        {
            while (ui.danger != null && ui.danger.gameObject.activeSelf)
            {
                float s = 1f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f)) * 0.15f;
                ui.danger.localScale = Vector3.one * s;
                yield return null;
            }
            dangerRoutine = null;
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

            yield return UIKit.Animate(0.3f, k => rect.localScale = Vector3.one * UIKit.EaseOutBack(k) * 1.2f);
            yield return UIKit.Animate(0.2f, k => rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(k * Mathf.PI * 2f) * 10f));

            Vector3 from = rect.localPosition;
            Vector3 to = ui.flyLayer.InverseTransformPoint(ui.starIcon.rectTransform.position);
            yield return UIKit.Animate(0.45f, k =>
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
            yield return UIKit.Animate(0.15f, k => t.localScale = Vector3.one * (1f + 0.3f * k));
            heart.sprite = ui.heartEmpty;
            yield return UIKit.Animate(0.25f, k => t.localScale = Vector3.one * Mathf.Lerp(1.3f, 1f, UIKit.EaseOutBack(k)));
        }

        /// <summary>No hearts left: a calm, encouraging screen. ProgressionManager restarts the level.</summary>
        private void OnOutOfHearts()
        {
            controls.ReleaseAll();
            GameMessages.Say(Lines.TryAgain);
            StartCoroutine(ShowRestartScreen());
        }

        private IEnumerator ShowRestartScreen()
        {
            CanvasGroup group = ui.restartOverlay;
            if (group == null)
                yield break;
            group.blocksRaycasts = true;
            yield return UIKit.Animate(0.5f, k => group.alpha = k);
            while (group != null)
            {
                if (ui.restartHeart != null)
                    PlayScale(ui.restartHeart, 1.15f, 0.6f);
                yield return new WaitForSecondsRealtime(0.8f);
            }
        }

        // ---------- Badges, settings, end of level ----------

        private void OnBadgeUnlocked(BadgeInfo badge)
        {
            if (ui.badgePopup != null)
                ui.badgePopup.Show(badge, () => Play(HUDSounds.Fanfare()));
            if (ui.endScreen != null)
                ui.endScreen.RememberBadge(badge);
        }

        private void OpenSettings()
        {
            if (ui.settingsMenu != null && !IsMenuOpen())
            {
                controls.ReleaseAll();
                Play(HUDSounds.Click());
                ui.settingsMenu.Open();
            }
        }

        private void OpenBadges()
        {
            if (ui.badgeMenu != null && !IsMenuOpen())
            {
                controls.ReleaseAll();
                Play(HUDSounds.Click());
                ui.badgeMenu.Open();
            }
        }

        private bool IsMenuOpen() =>
            (ui.settingsMenu != null && ui.settingsMenu.IsOpen) ||
            (ui.badgeMenu != null && ui.badgeMenu.IsOpen) ||
            (ui.endScreen != null && ui.endScreen.IsOpen);

        private void OnLevelEnded()
        {
            controls.ReleaseAll();
            StartCoroutine(ShowEndScreen());
        }

        private IEnumerator ShowEndScreen()
        {
            yield return new WaitForSecondsRealtime(endScreenDelay);
            Play(HUDSounds.Fanfare());
            if (ui.settingsMenu != null && ui.settingsMenu.IsOpen)
                ui.settingsMenu.Close();
            if (ui.badgeMenu != null && ui.badgeMenu.IsOpen)
                ui.badgeMenu.Close();
            if (ui.endScreen != null)
                ui.endScreen.Open(progression);
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
            yield return UIKit.Animate(0.2f, k =>
            {
                group.alpha = Mathf.Lerp(startAlpha, 1f, k);
                ui.bubble.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, UIKit.EaseOutBack(k));
            });
            yield return new WaitForSecondsRealtime(bubbleDuration);
            yield return UIKit.Animate(0.3f, k => group.alpha = 1f - k);
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
            if (dangerSound == null) dangerSound = HUDSounds.Danger();
        }

        /// <summary>Plays a sound effect (respects the SFX switch of the settings).</summary>
        public void Play(AudioClip clip)
        {
            if (clip != null && ui.audioSource != null && AudioPrefs.SfxOn)
                ui.audioSource.PlayOneShot(clip);
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
            scaleAnimations[target] = StartCoroutine(UIKit.Animate(duration,
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
            shakeAnimations[target] = StartCoroutine(UIKit.Animate(duration, k =>
                target.anchoredPosition = rest + Vector2.right * Mathf.Sin(k * Mathf.PI * 6f) * amount * (1f - k)));
        }
    }
}
