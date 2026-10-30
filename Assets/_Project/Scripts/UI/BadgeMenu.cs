using UnityEngine;
using UnityEngine.UI;
using RoadSafety.Core;

namespace RoadSafety.UI
{
    /// <summary>
    /// "شاراتي" (my badges), US 3.2: the 6 badges in a simple grid. Unlocked badges are in
    /// colour, the others grey with a lock. Tapping a badge says how to get it.
    /// The game is paused while it is open.
    /// </summary>
    public class BadgeMenu : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform panel;
        [SerializeField] private Image[] icons = new Image[0];
        [SerializeField] private GameObject[] locks = new GameObject[0];
        [SerializeField] private Text[] titles = new Text[0];
        [SerializeField] private Text count;
        [SerializeField] private Text howTo;
        [SerializeField] private TouchButton closeButton;

        public bool IsOpen { get; private set; }

        public static BadgeMenu Build(Transform parent)
        {
            CanvasGroup modal = UIKit.MakeModal("BadgeMenu", parent, UIKit.Dim);
            var menu = modal.gameObject.AddComponent<BadgeMenu>();
            menu.group = modal;
            menu.panel = UIKit.MakePanel("Panel", modal.transform, UIKit.Middle, Vector2.zero, new Vector2(1320f, 900f));
            menu.panel.GetComponent<Image>().raycastTarget = true;

            Text title = UIKit.MakeText("Title", menu.panel, 70, new Vector2(0f, 360f), new Vector2(700f, 100f));
            title.text = ArabicText.Fix("شاراتي");
            menu.count = UIKit.MakeText("Count", menu.panel, 52, new Vector2(470f, 360f), new Vector2(220f, 90f));
            menu.closeButton = UIKit.MakeButton("Btn_Close", menu.panel, UIKit.LoadSprite("btn_close"), UIKit.Middle, new Vector2(-560f, 365f), 120f);
            menu.closeButton.Clicked += menu.Close;

            int n = Badges.All.Length;
            menu.icons = new Image[n];
            menu.locks = new GameObject[n];
            menu.titles = new Text[n];
            for (int i = 0; i < n; i++)
            {
                BadgeInfo badge = Badges.All[i];
                int col = i % 3, row = i / 3;
                // RTL: the first badge is top-right.
                var position = new Vector2(400f - col * 400f, 155f - row * 320f);
                TouchButton button = UIKit.MakeButton("Badge_" + badge.Id, menu.panel, UIKit.LoadSprite(badge.Sprite), UIKit.Middle, position, 210f);
                menu.icons[i] = button.Visual.GetComponent<Image>();
                RectTransform lockRect = UIKit.MakeRect("Lock", button.transform, UIKit.Middle, new Vector2(72f, -72f), new Vector2(90f, 90f));
                UIKit.AddImage(lockRect.gameObject, UIKit.LoadSprite("icon_lock"), false);
                menu.locks[i] = lockRect.gameObject;
                menu.titles[i] = UIKit.MakeText("Title", menu.panel, 40, position + new Vector2(0f, -140f), new Vector2(380f, 60f));
                menu.titles[i].text = ArabicText.Fix(badge.Title);
                BadgeInfo captured = badge;
                button.Clicked += () => menu.ShowHowTo(captured);
            }
            menu.howTo = UIKit.MakeText("HowTo", menu.panel, 42, new Vector2(0f, -398f), new Vector2(1200f, 70f));
            return menu;
        }

        public void Open()
        {
            IsOpen = true;
            Refresh();
            howTo.text = ArabicText.Fix("اضغط على شارة لتعرف كيف تربحها");
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

        private void Refresh()
        {
            for (int i = 0; i < Badges.All.Length && i < icons.Length; i++)
            {
                bool unlocked = Badges.IsUnlocked(Badges.All[i].Id);
                icons[i].color = unlocked ? Color.white : new Color(0.55f, 0.6f, 0.68f, 0.55f);
                locks[i].SetActive(!unlocked);
                titles[i].color = unlocked ? UIKit.Navy : new Color(0.45f, 0.5f, 0.6f, 1f);
            }
            count.text = Badges.UnlockedCount + " / " + Badges.All.Length;
        }

        private void ShowHowTo(BadgeInfo badge)
        {
            string prefix = Badges.IsUnlocked(badge.Id) ? "ربحتها: " : "";
            howTo.text = ArabicText.Fix(prefix + badge.HowTo);
        }
    }
}
