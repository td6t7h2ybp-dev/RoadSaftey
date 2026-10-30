using UnityEngine;
using UnityEngine.UI;
using static RoadSafety.UI.UIKit;

namespace RoadSafety.UI
{
    /// <summary>
    /// Builds the whole HUD in code (Canvas, buttons, counters, menus), so nobody has to edit
    /// Main.unity at the same time. Layout is for a landscape tablet, reference 1920x1080,
    /// scaled to any screen size (US 4.5). Big touch zones for 5-9 year olds.
    ///
    ///   top-left     : settings (US 4.4), badges (US 3.2), level number
    ///   top-centre   : stars and hearts
    ///   top-right    : avatar + seatbelt button (+ speech bubble and danger sign under them)
    ///   bottom-left  : left / right arrows
    ///   bottom-centre: horn
    ///   bottom-right : Go, with the red Stop button under it
    ///
    /// Sprites are in Assets/_Project/UI/Resources/HUD, the Arabic font in .../Resources/Fonts.
    /// Style: flat colours with a dark-blue outline, Sidi Bou Said blue, door studs, an arched
    /// blue door around the avatar (Tunisian inspiration, charte: bleu ciel / vert pomme / jaune soleil).
    /// </summary>
    public static class HUDBuilder
    {
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
            audio.ignoreListenerPause = true;
            var voice = root.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
            voice.ignoreListenerPause = true;

            HUDController hud = root.AddComponent<HUDController>();
            HUDController.Refs ui = hud.UI;
            ui.audioSource = audio;
            ui.voiceSource = voice;
            ui.font = UIKit.Font;
            Transform parent = root.transform;

            // ---- bottom-left: steering arrows (US 1.2) ----
            ui.left = MakeButton("Btn_Left", parent, LoadSprite("btn_left"), BottomLeft, new Vector2(175f, 175f), 270f);
            ui.right = MakeButton("Btn_Right", parent, LoadSprite("btn_right"), BottomLeft, new Vector2(465f, 175f), 270f);

            // ---- bottom-right: Go, Stop under it (US 1.1) ----
            ui.go = MakeButton("Btn_Go", parent, LoadSprite("btn_go"), BottomRight, new Vector2(-190f, 395f), 290f);
            ui.stop = MakeButton("Btn_Stop", parent, LoadSprite("btn_stop"), BottomRight, new Vector2(-190f, 130f), 215f);

            // ---- bottom-centre: horn (US 1.4) ----
            ui.horn = MakeButton("Btn_Horn", parent, LoadSprite("btn_horn"), BottomCenter, new Vector2(0f, 130f), 205f);

            // ---- top-left: settings, badges, level ----
            ui.settingsButton = MakeButton("Btn_Settings", parent, LoadSprite("btn_settings"), TopLeft, new Vector2(95f, -95f), 140f);
            ui.badgesButton = MakeButton("Btn_Badges", parent, LoadSprite("btn_badges"), TopLeft, new Vector2(250f, -95f), 140f);
            RectTransform level = MakePanel("Level", parent, TopLeft, new Vector2(490f, -95f), new Vector2(300f, 100f));
            ui.levelText = MakeText("Text", level, 44, Vector2.zero, new Vector2(280f, 90f));

            // ---- top-right: avatar + seatbelt (US 1.3) ----
            ui.avatarNoBelt = LoadSprite("avatar_nobelt");
            ui.avatarBelt = LoadSprite("avatar_belt");
            ui.avatar = MakeRect("Avatar", parent, TopRight, new Vector2(-150f, -150f), new Vector2(230f, 230f));
            ui.avatarImage = AddImage(ui.avatar.gameObject, ui.avatarNoBelt, false);

            ui.beltOff = LoadSprite("btn_belt_off");
            ui.beltOn = LoadSprite("btn_belt_on");
            ui.belt = MakeButton("Btn_Seatbelt", parent, ui.beltOff, TopRight, new Vector2(-385f, -150f), 205f);
            ui.beltIcon = ui.belt.Visual.GetComponent<Image>();

            // danger sign (behaviour tree "Avoid": something in front of the car)
            ui.danger = MakeRect("Danger", parent, TopRight, new Vector2(-560f, -150f), new Vector2(130f, 130f));
            AddImage(ui.danger.gameObject, LoadSprite("icon_warning"), false);
            ui.danger.gameObject.SetActive(false);

            // speech bubble under the avatar
            ui.bubble = MakeRect("SpeechBubble", parent, TopRight, new Vector2(-370f, -360f), new Vector2(660f, 160f));
            ui.bubbleGroup = ui.bubble.gameObject.AddComponent<CanvasGroup>();
            ui.bubbleGroup.blocksRaycasts = false;
            ui.bubbleGroup.interactable = false;
            RectTransform tail = MakeRect("Tail", ui.bubble, Middle, new Vector2(215f, 77f), new Vector2(70f, 70f));
            AddImage(tail.gameObject, LoadSprite("ui_bubble_tail"), false);
            MakePanel("Panel", ui.bubble, Middle, Vector2.zero, new Vector2(660f, 160f));
            ui.bubbleText = MakeText("Text", ui.bubble, 46, Vector2.zero, new Vector2(620f, 140f));

            // ---- top-centre: stars (US 3.1) and hearts (US 3.4) ----
            ui.starCounter = MakePanel("Stars", parent, TopCenter, new Vector2(-175f, -80f), new Vector2(250f, 116f));
            ui.starSprite = LoadSprite("icon_star");
            RectTransform starIconRect = MakeRect("StarIcon", ui.starCounter, Middle, new Vector2(-58f, 2f), new Vector2(100f, 100f));
            ui.starIcon = AddImage(starIconRect.gameObject, ui.starSprite, false);
            ui.starCount = MakeText("Count", ui.starCounter, 66, new Vector2(55f, 0f), new Vector2(120f, 100f));
            ui.starCount.text = "0";

            RectTransform hearts = MakePanel("Hearts", parent, TopCenter, new Vector2(150f, -80f), new Vector2(350f, 116f));
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
            overlayImage.color = LimeWhite;
            ui.restartOverlay = overlay.gameObject.AddComponent<CanvasGroup>();
            ui.restartOverlay.alpha = 0f;
            ui.restartOverlay.blocksRaycasts = false;
            ui.restartHeart = MakeRect("Heart", overlay, Middle, new Vector2(0f, 110f), new Vector2(220f, 220f));
            AddImage(ui.restartHeart.gameObject, ui.heartFull, false);
            ui.restartText = MakeText("Message", overlay, 84, new Vector2(0f, -90f), new Vector2(1400f, 160f));

            // ---- menus (built hidden, above everything else) ----
            ui.settingsMenu = SettingsMenu.Build(parent);
            ui.badgeMenu = BadgeMenu.Build(parent);
            ui.endScreen = EndScreen.Build(parent);
            ui.badgePopup = BadgePopup.Build(parent); // last: drawn on top of the end screen

            // what the HUD shows before Play (it can be saved in the scene: RoadSafety > UI)
            ui.levelText.text = ArabicText.Fix("المستوى 1");
            SetVisible(ui.bubbleGroup, false);
            return hud;
        }
    }
}
