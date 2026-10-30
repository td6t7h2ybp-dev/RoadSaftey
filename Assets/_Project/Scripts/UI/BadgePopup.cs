using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RoadSafety.Core;

namespace RoadSafety.UI
{
    /// <summary>"شارة جديدة!" popup when a badge is unlocked (US 3.2). Several badges are shown one after the other.</summary>
    public class BadgePopup : MonoBehaviour
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Image icon;
        [SerializeField] private Text title;

        private readonly Queue<KeyValuePair<BadgeInfo, Action>> queue = new Queue<KeyValuePair<BadgeInfo, Action>>();
        private bool showing;

        public static BadgePopup Build(Transform parent)
        {
            RectTransform root = UIKit.MakeRect("BadgePopup", parent, UIKit.TopCenter, new Vector2(0f, -300f), new Vector2(700f, 240f));
            var popup = root.gameObject.AddComponent<BadgePopup>();
            popup.group = root.gameObject.AddComponent<CanvasGroup>();
            popup.group.alpha = 0f;
            popup.group.blocksRaycasts = false;
            popup.group.interactable = false;
            popup.panel = UIKit.MakePanel("Panel", root, UIKit.Middle, Vector2.zero, new Vector2(700f, 240f));
            RectTransform iconRect = UIKit.MakeRect("Icon", popup.panel, UIKit.Middle, new Vector2(230f, 0f), new Vector2(190f, 190f));
            popup.icon = UIKit.AddImage(iconRect.gameObject, null, false);
            Text header = UIKit.MakeText("Header", popup.panel, 44, new Vector2(-90f, 45f), new Vector2(460f, 70f));
            header.text = ArabicText.Fix("شارة جديدة!");
            header.color = UIKit.SoftOrange;
            popup.title = UIKit.MakeText("Title", popup.panel, 58, new Vector2(-90f, -35f), new Vector2(460f, 80f));
            return popup;
        }

        public void Show(BadgeInfo badge, Action onShow)
        {
            queue.Enqueue(new KeyValuePair<BadgeInfo, Action>(badge, onShow));
            if (!showing)
                StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            showing = true;
            while (queue.Count > 0)
            {
                KeyValuePair<BadgeInfo, Action> next = queue.Dequeue();
                icon.sprite = UIKit.LoadSprite(next.Key.Sprite);
                title.text = ArabicText.Fix(next.Key.Title);
                next.Value?.Invoke();
                yield return UIKit.Animate(0.4f, k =>
                {
                    group.alpha = Mathf.Clamp01(k * 2f);
                    panel.localScale = Vector3.one * UIKit.EaseOutBack(k);
                });
                yield return UIKit.Animate(0.5f, k => icon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(k * Mathf.PI * 4f) * 12f * (1f - k)));
                yield return new WaitForSecondsRealtime(2.2f);
                yield return UIKit.Animate(0.3f, k => group.alpha = 1f - k);
            }
            showing = false;
        }
    }
}
