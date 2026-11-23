using UnityEngine;
using UnityEngine.UI;
using RoadSafety.Core;

namespace RoadSafety.UI
{
    /// <summary>
    /// Settings (US 4.4): icons only, no text needed. Music on/off and sound effects on/off,
    /// separately, saved on the tablet. The game is paused while it is open.
    /// </summary>
    public class SettingsMenu : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform panel;
        [SerializeField] private TouchButton musicButton;
        [SerializeField] private TouchButton sfxButton;
        [SerializeField] private TouchButton closeButton;
        [SerializeField] private TouchButton backgroundButton;
        [SerializeField] private Image musicIcon;
        [SerializeField] private Image sfxIcon;
        [SerializeField] private Sprite musicOn;
        [SerializeField] private Sprite musicOff;
        [SerializeField] private Sprite sfxOn;
        [SerializeField] private Sprite sfxOff;

        public bool IsOpen { get; private set; }

        public static SettingsMenu Build(Transform parent)
        {
            CanvasGroup modal = UIKit.MakeModal("SettingsMenu", parent, UIKit.Dim);
            var menu = modal.gameObject.AddComponent<SettingsMenu>();
            menu.group = modal;
            menu.backgroundButton = modal.gameObject.AddComponent<TouchButton>();

            menu.panel = UIKit.MakePanel("Panel", modal.transform, UIKit.Middle, Vector2.zero, new Vector2(860f, 560f));
            menu.panel.GetComponent<Image>().raycastTarget = true; // taps on the panel don't close the menu
            RectTransform title = UIKit.MakeRect("Title", menu.panel, UIKit.Middle, new Vector2(0f, 175f), new Vector2(130f, 130f));
            UIKit.AddImage(title.gameObject, UIKit.LoadSprite("btn_settings"), false);

            menu.musicOn = UIKit.LoadSprite("icon_music_on");
            menu.musicOff = UIKit.LoadSprite("icon_music_off");
            menu.sfxOn = UIKit.LoadSprite("icon_sfx_on");
            menu.sfxOff = UIKit.LoadSprite("icon_sfx_off");

            // RTL: music (first) on the right, sound effects on the left.
            menu.musicButton = UIKit.MakeButton("Btn_Music", menu.panel, menu.musicOn, UIKit.Middle, new Vector2(180f, -55f), 250f);
            menu.musicIcon = menu.musicButton.Visual.GetComponent<Image>();
            menu.sfxButton = UIKit.MakeButton("Btn_Sfx", menu.panel, menu.sfxOn, UIKit.Middle, new Vector2(-180f, -55f), 250f);
            menu.sfxIcon = menu.sfxButton.Visual.GetComponent<Image>();
            menu.closeButton = UIKit.MakeButton("Btn_Close", menu.panel, UIKit.LoadSprite("btn_close"), UIKit.Middle, new Vector2(-385f, 235f), 120f);
            return menu;
        }

        // Here and not in Build: events are not saved in the scene, and the HUD saved in Main.unity skips Build.
        private void Start()
        {
            musicButton.Clicked += ToggleMusic;
            sfxButton.Clicked += ToggleSfx;
            closeButton.Clicked += Close;
            backgroundButton.Clicked += Close;
        }

        public void Open()
        {
            IsOpen = true;
            Refresh();
            UIKit.SetVisible(group, true);
            Time.timeScale = 0f;
            StartCoroutine(UIKit.Animate(0.25f, k => panel.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, UIKit.EaseOutBack(k))));
        }

        public void Close()
        {
            if (!IsOpen)
                return;
            IsOpen = false;
            UIKit.SetVisible(group, false);
            Time.timeScale = 1f;
        }

        private void ToggleMusic()
        {
            AudioPrefs.MusicOn = !AudioPrefs.MusicOn;
            Refresh();
            Bounce(musicButton.transform);
        }

        private void ToggleSfx()
        {
            AudioPrefs.SfxOn = !AudioPrefs.SfxOn;
            Refresh();
            Bounce(sfxButton.transform);
        }

        private void Refresh()
        {
            if (musicIcon != null) musicIcon.sprite = AudioPrefs.MusicOn ? musicOn : musicOff;
            if (sfxIcon != null) sfxIcon.sprite = AudioPrefs.SfxOn ? sfxOn : sfxOff;
        }

        private void Bounce(Transform target)
        {
            StartCoroutine(UIKit.Animate(0.25f, k => target.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(k * Mathf.PI))));
        }
    }
}
