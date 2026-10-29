using UnityEngine;
using UnityEngine.UI;

namespace RoadSafety.UI
{
    /// <summary>
    /// Builds the whole HUD in code (Canvas, buttons, counters...), so nobody has to edit
    /// Main.unity at the same time. Layout is for a landscape tablet, reference 1920x1080,
    /// scaled to any screen size (US 4.5). Big touch zones for 5-9 year olds.
    ///
    ///   top-right    : avatar + seatbelt button (+ speech bubble under them)
    ///   top-centre   : stars and hearts
    ///   bottom-left  : left / right arrows
    ///   bottom-centre: horn
    ///   bottom-right : Go, with the red Stop button under it
    ///
    /// Sprites are in Assets/_Project/UI/Resources/HUD, the Arabic font in .../Resources/Fonts.
    /// </summary>
    public static class HUDBuilder
    {
        public const string SpriteFolder = "HUD/";
        public const string FontPath = "Fonts/Almarai-ExtraBold";

        private static readonly Color Navy = new Color32(45, 58, 90, 255);
        private static readonly Color PanelColor = new Color(1f, 1f, 1f, 0.9f);
        private static readonly Color OverlayColor = new Color32(196, 236, 255, 242);

        private static readonly Vector2 BottomLeft = new Vector2(0f, 0f);
        private static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);
        private static readonly Vector2 BottomRight = new Vector2(1f, 0f);
        private static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
        private static readonly Vector2 TopRight = new Vector2(1f, 1f);
        private static readonly Vector2 Middle = new Vector2(0.5f, 0.5f);

        public static HUDController Build()
        {
            var root = new GameObject("HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;

            HUDController hud = root.AddComponent<HUDController>();
            HUDController.Refs ui = hud.UI;
            ui.audioSource = audio;
            ui.font = LoadFont();
            Transform parent = root.transform;

            // ---- bottom-left: steering arrows (US 1.2) ----
            ui.left = MakeButton("Btn_Left", parent, LoadSprite("btn_left"), BottomLeft, new Vector2(175f, 175f), 270f);
            ui.right = MakeButton("Btn_Right", parent, LoadSprite("btn_right"), BottomLeft, new Vector2(465f, 175f), 270f);

            // ---- bottom-right: Go, Stop under it (US 1.1) ----
            ui.go = MakeButton("Btn_Go", parent, LoadSprite("btn_go"), BottomRight, new Vector2(-190f, 395f), 290f);
            ui.stop = MakeButton("Btn_Stop", parent, LoadSprite("btn_stop"), BottomRight, new Vector2(-190f, 130f), 215f);

            // ---- bottom-centre: horn (US 1.4) ----
            ui.horn = MakeButton("Btn_Horn", parent, LoadSprite("btn_horn"), BottomCenter, new Vector2(0f, 130f), 205f);

            // ---- top-right: avatar + seatbelt (US 1.3) ----
            ui.avatarNoBelt = LoadSprite("avatar_nobelt");
            ui.avatarBelt = LoadSprite("avatar_belt");
            ui.avatar = MakeRect("Avatar", parent, TopRight, new Vector2(-150f, -150f), new Vector2(230f, 230f));
            ui.avatarImage = AddImage(ui.avatar.gameObject, ui.avatarNoBelt, false);

            ui.beltOff = LoadSprite("btn_belt_off");
            ui.beltOn = LoadSprite("btn_belt_on");
            ui.belt = MakeButton("Btn_Seatbelt", parent, ui.beltOff, TopRight, new Vector2(-385f, -150f), 205f);
            ui.beltIcon = ui.belt.Visual.GetComponent<Image>();

            // speech bubble under the avatar
            ui.bubble = MakeRect("SpeechBubble", parent, TopRight, new Vector2(-370f, -345f), new Vector2(660f, 130f));
            ui.bubbleGroup = ui.bubble.gameObject.AddComponent<CanvasGroup>();
            ui.bubbleGroup.blocksRaycasts = false;
            ui.bubbleGroup.interactable = false;
            RectTransform tail = MakeRect("Tail", ui.bubble, Middle, new Vector2(215f, 62f), new Vector2(70f, 70f));
            AddImage(tail.gameObject, LoadSprite("ui_bubble_tail"), false);
            RectTransform panel = MakeRect("Panel", ui.bubble, Middle, Vector2.zero, new Vector2(660f, 130f));
            AddSlicedImage(panel.gameObject, LoadSprite("ui_panel"), Color.white);
            ui.bubbleText = MakeText("Text", ui.bubble, ui.font, 46, Vector2.zero, new Vector2(620f, 110f));

            // ---- top-centre: stars (US 3.1) and hearts (US 3.4) ----
            ui.starCounter = MakeRect("Stars", parent, TopCenter, new Vector2(-175f, -80f), new Vector2(250f, 116f));
            AddSlicedImage(ui.starCounter.gameObject, LoadSprite("ui_panel"), PanelColor);
            ui.starSprite = LoadSprite("icon_star");
            RectTransform starIconRect = MakeRect("StarIcon", ui.starCounter, Middle, new Vector2(-58f, 2f), new Vector2(100f, 100f));
            ui.starIcon = AddImage(starIconRect.gameObject, ui.starSprite, false);
            ui.starCount = MakeText("Count", ui.starCounter, ui.font, 66, new Vector2(55f, 0f), new Vector2(120f, 100f));
            ui.starCount.text = "0";

            RectTransform hearts = MakeRect("Hearts", parent, TopCenter, new Vector2(150f, -80f), new Vector2(350f, 116f));
            AddSlicedImage(hearts.gameObject, LoadSprite("ui_panel"), PanelColor);
            ui.heartFull = LoadSprite("icon_heart");
            ui.heartEmpty = LoadSprite("icon_heart_empty");
            ui.hearts = new Image[3];
            for (int i = 0; i < ui.hearts.Length; i++)
            {
                // RTL: the first heart is on the right, hearts are lost from the left.
                RectTransform heart = MakeRect("Heart_" + (i + 1), hearts, Middle, new Vector2(105f - i * 105f, 0f), new Vector2(92f, 92f));
                ui.hearts[i] = AddImage(heart.gameObject, ui.heartFull, false);
            }

            // ---- layer for the flying stars ----
            ui.flyLayer = MakeStretched("FlyLayer", parent);

            // ---- restart screen (no hearts left), soft and encouraging ----
            RectTransform overlay = MakeStretched("RestartScreen", parent);
            var overlayImage = overlay.gameObject.AddComponent<Image>();
            overlayImage.color = OverlayColor;
            ui.restartOverlay = overlay.gameObject.AddComponent<CanvasGroup>();
            ui.restartOverlay.alpha = 0f;
            ui.restartOverlay.blocksRaycasts = false;
            ui.restartHeart = MakeRect("Heart", overlay, Middle, new Vector2(0f, 110f), new Vector2(220f, 220f));
            AddImage(ui.restartHeart.gameObject, ui.heartFull, false);
            ui.restartText = MakeText("Message", overlay, ui.font, 84, new Vector2(0f, -90f), new Vector2(1400f, 160f));

            return hud;
        }

        // ---------- helpers ----------

        private static Sprite LoadSprite(string name)
        {
            var sprite = Resources.Load<Sprite>(SpriteFolder + name);
            if (sprite == null)
                Debug.LogWarning($"[HUDBuilder] Sprite Resources/{SpriteFolder}{name} not found.");
            return sprite;
        }

        private static Font LoadFont()
        {
            var font = Resources.Load<Font>(FontPath);
            if (font != null)
                return font;
            Debug.LogWarning($"[HUDBuilder] Arabic font Resources/{FontPath} not found; using Unity's default font.");
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static RectTransform MakeRect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
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

        private static RectTransform MakeStretched(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Image AddImage(GameObject go, Sprite sprite, bool raycast)
        {
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = raycast;
            return image;
        }

        private static void AddSlicedImage(GameObject go, Sprite sprite, Color color)
        {
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
        }

        /// <summary>Square touch zone (invisible, catches the finger) with the icon as a child that shrinks when pressed.</summary>
        private static TouchButton MakeButton(string name, Transform parent, Sprite sprite, Vector2 anchor, Vector2 position, float size)
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

        private static Text MakeText(string name, Transform parent, Font font, int size, Vector2 position, Vector2 rectSize)
        {
            RectTransform rect = MakeRect(name, parent, Middle, position, rectSize);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = Navy;
            text.alignment = TextAnchor.MiddleCenter;
            // Arabic lines are already broken and reordered by ArabicText.Fix: don't let Unity re-wrap them.
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }
    }
}
