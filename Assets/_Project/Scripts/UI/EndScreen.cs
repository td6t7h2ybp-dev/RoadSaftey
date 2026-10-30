using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RoadSafety.Core;

namespace RoadSafety.UI
{
    /// <summary>
    /// End-of-level summary (US 3.3): stars won, the good actions, what to watch next time
    /// (said gently, never as a punishment), the new badges, and two buttons:
    /// "إعادة" (play again) and "المستوى التالي" (next level).
    /// </summary>
    public class EndScreen : MonoBehaviour
    {
        private const int MaxLines = 6;

        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform content;
        [SerializeField] private Text starCount;
        [SerializeField] private Text message;
        [SerializeField] private RectTransform list;
        [SerializeField] private RectTransform badgeRow;
        [SerializeField] private TouchButton replayButton;
        [SerializeField] private TouchButton nextButton;

        private readonly List<BadgeInfo> newBadges = new List<BadgeInfo>();
        private ProgressionManager progression;

        public bool IsOpen { get; private set; }

        public static EndScreen Build(Transform parent)
        {
            CanvasGroup modal = UIKit.MakeModal("EndScreen", parent, UIKit.LimeWhite);
            var screen = modal.gameObject.AddComponent<EndScreen>();
            screen.group = modal;
            screen.content = UIKit.MakeStretched("Content", modal.transform);
            Transform c = screen.content;

            Text title = UIKit.MakeText("Title", c, 92, new Vector2(0f, 420f), new Vector2(1400f, 120f));
            title.text = ArabicText.Fix(Lines.Finish);

            // Right (RTL first): what happened during the drive.
            screen.list = UIKit.MakePanel("List", c, UIKit.Middle, new Vector2(260f, 90f), new Vector2(1060f, 560f));

            // Left: stars and new badges.
            RectTransform star = UIKit.MakeRect("Star", c, UIKit.Middle, new Vector2(-560f, 210f), new Vector2(230f, 230f));
            UIKit.AddImage(star.gameObject, UIKit.LoadSprite("icon_star"), false);
            screen.starCount = UIKit.MakeText("Stars", c, 96, new Vector2(-560f, 30f), new Vector2(300f, 120f));
            screen.badgeRow = UIKit.MakeRect("NewBadges", c, UIKit.Middle, new Vector2(-560f, -130f), new Vector2(440f, 130f));

            screen.message = UIKit.MakeText("Message", c, 56, new Vector2(0f, -275f), new Vector2(1700f, 90f));

            // RTL: "next level" (main action) on the right, "play again" on the left.
            screen.nextButton = UIKit.MakeButton("Btn_Next", c, UIKit.LoadSprite("btn_next"), UIKit.Middle, new Vector2(280f, -385f), 170f);
            screen.replayButton = UIKit.MakeButton("Btn_Replay", c, UIKit.LoadSprite("btn_replay"), UIKit.Middle, new Vector2(-280f, -385f), 170f);
            Text nextLabel = UIKit.MakeText("NextLabel", c, 44, new Vector2(530f, -385f), new Vector2(320f, 70f));
            nextLabel.text = ArabicText.Fix("المستوى التالي");
            Text replayLabel = UIKit.MakeText("ReplayLabel", c, 44, new Vector2(-480f, -385f), new Vector2(200f, 70f));
            replayLabel.text = ArabicText.Fix("إعادة");

            screen.replayButton.Clicked += screen.Replay;
            screen.nextButton.Clicked += screen.Next;
            return screen;
        }

        /// <summary>Remembers the badges unlocked during this level, to show them here.</summary>
        public void RememberBadge(BadgeInfo badge)
        {
            if (!newBadges.Contains(badge))
                newBadges.Add(badge);
            if (IsOpen)
                FillBadges();
        }

        public void Open(ProgressionManager source)
        {
            progression = source;
            IsOpen = true;
            starCount.text = "× " + source.Stars;
            FillList(source);
            FillBadges();
            message.text = ArabicText.Fix(Encouragement(source));
            UIKit.SetVisible(group, true);
            Time.timeScale = 0f;
            StartCoroutine(UIKit.Animate(0.45f, k =>
            {
                group.alpha = k;
                content.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, UIKit.EaseOutBack(k));
            }));
        }

        private static string Encouragement(ProgressionManager source)
        {
            if (source.Mistakes.Count == 0)
                return "رائع! أنت سائق مثالي!";
            if (source.Stars >= 2)
                return "أحسنت! يمكنك أن تكون أفضل في المرّة القادمة";
            return "لا بأس! نتعلّم معًا، حاول مرّة أخرى";
        }

        private void FillList(ProgressionManager source)
        {
            for (int i = list.childCount - 1; i >= 0; i--)
                Destroy(list.GetChild(i).gameObject);

            var lines = new List<KeyValuePair<string, bool>>();
            if (source.GoodActions.Count > 0)
            {
                lines.Add(new KeyValuePair<string, bool>("ما فعلته جيّدًا:", true));
                foreach (string line in Grouped(source.GoodActions))
                    lines.Add(new KeyValuePair<string, bool>(line, true));
            }
            if (source.Mistakes.Count > 0)
            {
                lines.Add(new KeyValuePair<string, bool>("انتبه في المرّة القادمة:", false));
                foreach (string line in Grouped(source.Mistakes))
                    lines.Add(new KeyValuePair<string, bool>(line, false));
            }
            if (lines.Count == 0)
                lines.Add(new KeyValuePair<string, bool>("قُد بهدوء واجمع النجوم!", true));

            Sprite check = UIKit.LoadSprite("icon_check");
            Sprite watch = UIKit.LoadSprite("icon_watch");
            int shown = Mathf.Min(lines.Count, MaxLines + 2);
            float y = 215f;
            for (int i = 0; i < shown; i++)
            {
                bool heading = lines[i].Key.EndsWith(":");
                if (heading && i > 0)
                    y -= 12f;
                if (!heading)
                {
                    RectTransform icon = UIKit.MakeRect("Icon", list, UIKit.Middle, new Vector2(445f, y), new Vector2(58f, 58f));
                    UIKit.AddImage(icon.gameObject, lines[i].Value ? check : watch, false);
                }
                Text text = UIKit.MakeText("Line", list, heading ? 46 : 42, new Vector2(heading ? 20f : -20f, y), new Vector2(heading ? 920f : 860f, 62f), TextAnchor.MiddleRight);
                text.text = ArabicText.Fix(lines[i].Key);
                text.color = heading ? (lines[i].Value ? UIKit.SoftGreen : UIKit.SoftOrange) : UIKit.Navy;
                y -= 64f;
            }
        }

        /// <summary>"اصطدمت بشيء" twice → "اصطدمت بشيء (2)".</summary>
        private static IEnumerable<string> Grouped(IReadOnlyList<string> items)
        {
            var order = new List<string>();
            var counts = new Dictionary<string, int>();
            foreach (string item in items)
            {
                if (!counts.ContainsKey(item))
                {
                    counts[item] = 0;
                    order.Add(item);
                }
                counts[item]++;
            }
            foreach (string item in order)
                yield return counts[item] > 1 ? item + " (" + counts[item] + ")" : item;
        }

        private void FillBadges()
        {
            for (int i = badgeRow.childCount - 1; i >= 0; i--)
                Destroy(badgeRow.GetChild(i).gameObject);
            int n = Mathf.Min(newBadges.Count, 4);
            for (int i = 0; i < n; i++)
            {
                float x = (n - 1) * 55f - i * 110f;
                RectTransform icon = UIKit.MakeRect("Badge", badgeRow, UIKit.Middle, new Vector2(x, 0f), new Vector2(110f, 110f));
                UIKit.AddImage(icon.gameObject, UIKit.LoadSprite(newBadges[i].Sprite), false);
            }
        }

        private void Replay()
        {
            Leave();
        }

        private void Next()
        {
            LevelSession.NextLevel();
            Leave();
        }

        private void Leave()
        {
            Time.timeScale = 1f;
            if (progression != null)
                progression.RestartLevel();
        }
    }
}
