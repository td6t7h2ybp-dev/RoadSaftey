using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace RoadSafety.UI
{
    /// <summary>
    /// Helpers to build uGUI in code (HUD, menus, end screen) with the game's style:
    /// sprites from Resources/HUD, the rounded Arabic font, dark-blue text.
    /// Positions are in the 1920x1080 reference layout of the CanvasScaler.
    /// </summary>
    public static class UIKit
    {
        public const string SpriteFolder = "HUD/";
        public const string FontPath = "Fonts/BalooBhaijaanRS-Bold";

        public static readonly Color Navy = new Color32(22, 50, 92, 255);
        public static readonly Color SoftGreen = new Color32(70, 150, 50, 255);
        public static readonly Color SoftOrange = new Color32(214, 110, 40, 255);
        public static readonly Color Dim = new Color(0.05f, 0.12f, 0.25f, 0.55f);
        public static readonly Color LimeWhite = new Color32(250, 247, 238, 245);

        public static readonly Vector2 BottomLeft = new Vector2(0f, 0f);
        public static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);
        public static readonly Vector2 BottomRight = new Vector2(1f, 0f);
        public static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        public static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
        public static readonly Vector2 TopRight = new Vector2(1f, 1f);
        public static readonly Vector2 Middle = new Vector2(0.5f, 0.5f);

        private static Font font;

        public static Font Font
        {
            get
            {
                if (font != null)
                    return font;
                font = Resources.Load<Font>(FontPath);
                if (font == null)
                {
                    Debug.LogWarning($"[UIKit] Arabic font Resources/{FontPath} not found; using Unity's default font.");
                    font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return font;
            }
        }

        public static Sprite LoadSprite(string name)
        {
            var sprite = Resources.Load<Sprite>(SpriteFolder + name);
            if (sprite == null)
                Debug.LogWarning($"[UIKit] Sprite Resources/{SpriteFolder}{name} not found.");
            return sprite;
        }

        public static RectTransform MakeRect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static RectTransform MakeStretched(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        public static Image AddImage(GameObject go, Sprite sprite, bool raycast)
        {
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = raycast;
            return image;
        }

        public static Image AddSlicedImage(GameObject go, Sprite sprite, Color color)
        {
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Panel with the blue ceramic border.</summary>
        public static RectTransform MakePanel(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            RectTransform rect = MakeRect(name, parent, anchor, position, size);
            AddSlicedImage(rect.gameObject, LoadSprite("ui_panel"), Color.white);
            return rect;
        }

        /// <summary>Square touch zone (invisible, catches the finger) with the icon as a child that shrinks when pressed.</summary>
        public static TouchButton MakeButton(string name, Transform parent, Sprite sprite, Vector2 anchor, Vector2 position, float size)
        {
            RectTransform rect = MakeRect(name, parent, anchor, position, new Vector2(size, size));
            var zone = rect.gameObject.AddComponent<Image>();
            zone.color = new Color(1f, 1f, 1f, 0f); // invisible but still receives touches
            zone.raycastTarget = true;
            // A fully transparent mesh is culled by default, and a culled graphic gets no touches.
            zone.canvasRenderer.cullTransparentMesh = false;

            RectTransform icon = MakeRect("Icon", rect, Middle, Vector2.zero, new Vector2(size, size));
            AddImage(icon.gameObject, sprite, false);

            var button = rect.gameObject.AddComponent<TouchButton>();
            button.Visual = icon;
            return button;
        }

        public static Text MakeText(string name, Transform parent, int size, Vector2 position, Vector2 rectSize, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            RectTransform rect = MakeRect(name, parent, Middle, position, rectSize);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Font;
            text.fontSize = size;
            text.color = Navy;
            text.alignment = alignment;
            // Arabic lines are already broken and reordered by ArabicText.Fix: don't let Unity re-wrap them.
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>Full-screen layer that dims the game and catches touches (for menus).</summary>
        public static CanvasGroup MakeModal(string name, Transform parent, Color color)
        {
            RectTransform rect = MakeStretched(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            var group = rect.gameObject.AddComponent<CanvasGroup>();
            SetVisible(group, false);
            return group;
        }

        public static void SetVisible(CanvasGroup group, bool visible)
        {
            if (group == null)
                return;
            group.alpha = visible ? 1f : 0f;
            group.blocksRaycasts = visible;
            group.interactable = visible;
        }

        /// <summary>Runs step(0..1) over the duration, in unscaled time (works while the game is paused).</summary>
        public static IEnumerator Animate(float duration, Action<float> step)
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

        public static float EaseOutBack(float k)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float x = k - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }
    }
}
