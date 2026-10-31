using UnityEngine;
using UnityEngine.UI;

namespace RoadSafety.UI
{
    /// <summary>
    /// The HUD in a flat "sticker" style, like a mobile kids' game: solid colour buttons with a
    /// thick ink outline, a solid lip underneath (no gradients, no glow, no glassy bevels),
    /// chunky white icons. Counters are dark ink pills with white numbers so they read on any
    /// background. Go is a green pedal with arrows, Stop a red octagon, the shape every child
    /// already knows from the street. Buttons, places and behaviour do not change.
    /// Every sprite is drawn in code (no new image files).
    /// </summary>
    public static class HudRestyle
    {
        private const int Size = 256;
        public static readonly Color Ink = new Color32(30, 27, 46, 255);       // outlines and text
        private static readonly Color Paper = new Color32(255, 252, 245, 255);  // icons, bubble
        private static readonly Color Red = new Color32(236, 86, 76, 255);      // Stop: red, softened
        private static readonly Color Green = new Color32(126, 204, 72, 255);   // vert pomme
        private static readonly Color Yellow = new Color32(255, 206, 60, 255);  // jaune soleil
        private static readonly Color Blue = new Color32(92, 184, 240, 255);    // bleu ciel
        private static readonly Color Orange = new Color32(255, 128, 56, 255);

        private const float InkWidth = 0.06f;  // ink line width (in -1..1 units)
        private const float Lip = 0.11f;      // how far the solid lip shows under a button

        // the sprites are drawn once and reused (Play mode, the Editor preview, every restart)
        private static readonly System.Collections.Generic.Dictionary<string, Sprite> cache = new System.Collections.Generic.Dictionary<string, Sprite>();

        private static Sprite Cached(string key, System.Func<Sprite> make)
        {
            if (cache.TryGetValue(key, out Sprite s) && s != null && s.texture != null)
                return s;
            s = make();
            if (s.texture != null) s.texture.hideFlags = HideFlags.DontSave;
            s.hideFlags = HideFlags.DontSave;
            cache[key] = s;
            return s;
        }

        public static void Apply(HUDController.Refs ui)
        {
            Set(ui.left, Cached("left", () => Round((x, y) => Arrow(-x, y), Blue)));
            Set(ui.right, Cached("right", () => Round((x, y) => Arrow(x, y), Blue)));
            Set(ui.horn, Cached("horn", () => Round(Horn, Yellow)));
            Set(ui.settingsButton, Cached("gear", () => Round(Gear, Blue)));
            Set(ui.badgesButton, Cached("star", () => Round(StarGlyph, Green)));
            Set(ui.go, Cached("go", GoPedal));
            Set(ui.stop, Cached("stop", StopSign));

            Sprite pill = Cached("pill", Pill);
            foreach (Image img in new[] { Panel(ui.starCounter), Panel(ui.hearts.Length > 0 ? ui.hearts[0].transform.parent as RectTransform : null), Panel(ui.levelText != null ? ui.levelText.transform.parent as RectTransform : null) })
                if (img != null)
                {
                    img.sprite = pill;
                    img.type = Image.Type.Sliced;
                    img.pixelsPerUnitMultiplier = 2.4f;
                    img.color = Color.white;
                }
            foreach (Text t in new[] { ui.starCount, ui.levelText })
                if (t != null)
                {
                    t.color = Paper;
                    var o = t.GetComponent<Outline>();
                    if (o != null) o.enabled = false;
                    var s = t.GetComponent<Shadow>();
                    if (s != null) s.enabled = false;
                }

            // the friend's speech bubble: white card, ink outline, ink text
            if (ui.bubble != null)
            {
                Image card = Panel(ui.bubble);
                if (card != null)
                {
                    card.sprite = Cached("card", Card);
                    card.type = Image.Type.Sliced;
                    card.pixelsPerUnitMultiplier = 2.4f;
                    card.color = Color.white;
                }
                if (ui.bubbleText != null) ui.bubbleText.color = Ink;
            }
        }

        private static Image Panel(RectTransform rect)
        {
            if (rect == null) return null;
            foreach (Image img in rect.GetComponentsInChildren<Image>(true))
                if (img.transform == rect || img.name == "Panel" || img.name == "Background")
                    return img;
            return rect.GetComponent<Image>();
        }

        private static void Set(TouchButton button, Sprite sprite)
        {
            if (button == null || button.Visual == null) return;
            var img = button.Visual.GetComponent<Image>();
            if (img == null) return;
            img.sprite = sprite;
            img.color = Color.white;
            img.preserveAspect = true;
        }

        // ------------------------------------------------------------------ drawing

        private delegate float Shape(float x, float y); // signed distance, x/y in -1..1, < 0 inside

        private static Sprite Draw(Color[] pixels, float border = 0f)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.SetPixels(pixels);
            tex.Apply(true);
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        private static float Cover(float d, float px) => Mathf.Clamp01(0.5f - d / px);

        private static Color Darker(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);

        private static Color Lighter(Color c, float k) => new Color(c.r + (1f - c.r) * k, c.g + (1f - c.g) * k, c.b + (1f - c.b) * k, 1f);

        /// <summary>
        /// A 3D button: a domed face lit from the top left (bright bevel and a glossy crescent on
        /// the lit edge, darker bevel on the far edge), standing on a thick side wall, with a soft
        /// shadow on the screen behind it. The white glyph is embossed: it casts a small shadow
        /// on the face. A dark ink outline keeps it readable on any background.
        /// </summary>
        private static Sprite Sticker(Shape plate, Shape glyph, Color face, float border = 0f)
        {
            var pixels = new Color[Size * Size];
            float px = 2.2f / Size;
            const float bevel = 0.2f, e = 0.01f;
            Vector2 light = new Vector2(-0.4f, 0.92f).normalized;
            Color wall = Darker(face, 0.55f), wallDark = Darker(face, 0.42f);
            Color glyphLow = new Color(0.86f, 0.85f, 0.88f, 1f);
            for (int j = 0; j < Size; j++)
                for (int i = 0; i < Size; i++)
                {
                    float x = (i + 0.5f) / Size * 2f - 1f, y = (j + 0.5f) / Size * 2f - 1f;
                    float yt = y - Lip * 0.5f;
                    float top = plate(x, yt);                // the face, raised
                    float bottom = plate(x, y + Lip * 0.5f); // the side wall under it
                    float whole = Mathf.Min(top, bottom);

                    // soft shadow on the screen
                    float sh = plate(x + 0.03f, y + Lip * 0.5f + 0.07f);
                    float shadowA = Mathf.Clamp01(0.5f - sh / 0.18f) * 0.35f;

                    Color c = Ink;
                    float wallIn = Cover(bottom + InkWidth * 0.5f, px);
                    c = Color.Lerp(c, wall, wallIn);
                    c = Color.Lerp(c, wallDark, wallIn * Mathf.Clamp01(0.5f + x * 0.4f) * 0.5f);
                    c = Color.Lerp(c, Ink, Cover(top, px));

                    float faceIn = Cover(top + InkWidth * 0.5f, px);
                    if (faceIn > 0f)
                    {
                        Vector2 n = new Vector2(plate(x + e, yt) - plate(x - e, yt), plate(x, yt + e) - plate(x, yt - e));
                        n = n.sqrMagnitude > 1e-12f ? n.normalized : Vector2.zero;
                        float rim = Vector2.Dot(n, light);
                        float inner = -top - InkWidth * 0.5f;
                        float edge = Mathf.Pow(1f - Mathf.Clamp01(inner / bevel), 1.6f);
                        Color body = Color.Lerp(Darker(face, 0.86f), Lighter(face, 0.12f), Mathf.Clamp01(0.5f + yt * 0.7f));
                        body = Color.Lerp(body, Color.white, edge * Mathf.Clamp01(rim) * 0.45f);
                        body = Color.Lerp(body, Color.black, edge * Mathf.Clamp01(-rim) * 0.35f);
                        float gloss = Mathf.Clamp01(1f - Mathf.Abs(inner - bevel * 0.9f) / (bevel * 0.5f)) * Mathf.Clamp01((rim - 0.2f) / 0.6f);
                        body = Color.Lerp(body, Color.white, gloss * 0.5f);
                        c = Color.Lerp(c, body, faceIn);

                        if (glyph != null)
                        {
                            float gx = x / 0.5f;
                            float gs = glyph(gx, (yt + 0.035f) / 0.5f) * 0.5f;           // the glyph's shadow on the face
                            c = Color.Lerp(c, Darker(face, 0.5f), Cover(gs - InkWidth * 0.3f, px) * faceIn * 0.85f);
                            float g = glyph(gx, yt / 0.5f) * 0.5f;
                            c = Color.Lerp(c, Ink, Cover(g - InkWidth * 0.35f, px) * faceIn);
                            Color gw = Color.Lerp(glyphLow, Color.white, Mathf.Clamp01(0.5f + yt * 1.2f));
                            c = Color.Lerp(c, gw, Cover(g, px) * faceIn);
                        }
                    }
                    float solid = Cover(whole - InkWidth, px);
                    if (solid < 1f) c = Color.Lerp(Ink, c, solid);
                    c.a = Mathf.Max(solid, shadowA);
                    pixels[j * Size + i] = c;
                }
            return Draw(pixels, border);
        }

        private static Sprite Round(Shape glyph, Color face) =>
            Sticker((x, y) => Mathf.Sqrt(x * x + y * y) - 0.74f, glyph, face);

        private static Sprite GoPedal() =>
            Sticker((x, y) => RoundRect(x / Mathf.Lerp(1f, 0.86f, (y + 1f) * 0.5f), y, 0.5f, 0.76f, 0.24f), Chevrons, Green);

        private static Sprite StopSign() =>
            Sticker(Octagon, (x, y) => Box(x, y, 0f, 0f, 0.82f, 0.22f), Red);

        private static Sprite Pill()
        {
            Color face = new Color(Ink.r, Ink.g, Ink.b, 1f);
            var pixels = new Color[Size * Size];
            float px = 2.2f / Size;
            for (int j = 0; j < Size; j++)
                for (int i = 0; i < Size; i++)
                {
                    float x = (i + 0.5f) / Size * 2f - 1f, y = (j + 0.5f) / Size * 2f - 1f;
                    float d = RoundRect(x, y, 0.86f, 0.86f, 0.5f);
                    Color c = face;
                    c.a = Cover(d, px) * 0.82f;
                    pixels[j * Size + i] = c;
                }
            return Draw(pixels, Size * 0.4f);
        }

        private static Sprite Card()
        {
            var pixels = new Color[Size * Size];
            float px = 2.2f / Size;
            for (int j = 0; j < Size; j++)
                for (int i = 0; i < Size; i++)
                {
                    float x = (i + 0.5f) / Size * 2f - 1f, y = (j + 0.5f) / Size * 2f - 1f;
                    float d = RoundRect(x, y + 0.04f, 0.88f, 0.84f, 0.36f);
                    float lipD = RoundRect(x, y - 0.04f, 0.88f, 0.84f, 0.36f);
                    Color c = Color.Lerp(Ink, Paper, Cover(d + 0.06f, px));
                    c.a = Cover(Mathf.Min(d, lipD), px);
                    pixels[j * Size + i] = c;
                }
            return Draw(pixels, Size * 0.38f);
        }

        /// <summary>The bubble's pointer: a paper triangle with ink sides, tip at the bottom.
        /// Its top runs off the texture, so it joins the card without a line.</summary>
        public static Sprite BubbleTail()
        {
            var pixels = new Color[Size * Size];
            float px = 2.2f / Size;
            for (int j = 0; j < Size; j++)
                for (int i = 0; i < Size; i++)
                {
                    float x = (i + 0.5f) / Size * 2f - 1f, y = (j + 0.5f) / Size * 2f - 1f;
                    float d = Tri(x, y, new Vector2(-0.55f, 1.4f), new Vector2(0.55f, 1.4f), new Vector2(0f, -0.9f));
                    Color c = Color.Lerp(Ink, Paper, Cover(d + 0.14f, px));
                    c.a = Cover(d, px);
                    pixels[j * Size + i] = c;
                }
            return Draw(pixels);
        }

        private static float Octagon(float x, float y)
        {
            float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
            return Mathf.Max(Mathf.Max(ax, ay), (ax + ay) * 0.7071f) - 0.76f;
        }

        private static float Chevrons(float x, float y)
        {
            float d = 99f;
            foreach (float cy in new[] { -0.42f, 0.32f })
            {
                Vector2 p = new Vector2(x, y - cy);
                float a = Seg(p, new Vector2(-0.58f, -0.28f), new Vector2(0f, 0.26f));
                float b = Seg(p, new Vector2(0f, 0.26f), new Vector2(0.58f, -0.28f));
                d = Mathf.Min(d, Mathf.Min(a, b) - 0.17f);
            }
            return d;
        }

        private static float Seg(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude;
        }

        private static float RoundRect(float x, float y, float hw, float hh, float r)
        {
            float qx = Mathf.Abs(x) - hw + r, qy = Mathf.Abs(y) - hh + r;
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        private static float Box(float x, float y, float cx, float cy, float hw, float hh) => RoundRect(x - cx, y - cy, hw, hh, Mathf.Min(hw, hh) * 0.5f);

        private static float Tri(float x, float y, Vector2 a, Vector2 b, Vector2 c)
        {
            // signed distance to a triangle
            Vector2 p = new Vector2(x, y);
            Vector2 e0 = b - a, e1 = c - b, e2 = a - c, v0 = p - a, v1 = p - b, v2 = p - c;
            Vector2 pq0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / Vector2.Dot(e0, e0));
            Vector2 pq1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / Vector2.Dot(e1, e1));
            Vector2 pq2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / Vector2.Dot(e2, e2));
            float s = Mathf.Sign(e0.x * e2.y - e0.y * e2.x);
            Vector2 d = Vector2.Min(Vector2.Min(new Vector2(pq0.sqrMagnitude, s * (v0.x * e0.y - v0.y * e0.x)),
                                                new Vector2(pq1.sqrMagnitude, s * (v1.x * e1.y - v1.y * e1.x))),
                                    new Vector2(pq2.sqrMagnitude, s * (v2.x * e2.y - v2.y * e2.x)));
            return -Mathf.Sqrt(d.x) * Mathf.Sign(d.y);
        }

        private static float Arrow(float x, float y)
        {
            float head = Tri(x, y, new Vector2(0.95f, 0f), new Vector2(0.05f, 0.62f), new Vector2(0.05f, -0.62f));
            float shaft = Box(x, y, -0.4f, 0f, 0.5f, 0.2f);
            return Mathf.Min(head, shaft);
        }

        private static float Horn(float x, float y)
        {
            float bell = Tri(x, y, new Vector2(0.9f, 0.55f), new Vector2(0.9f, -0.55f), new Vector2(0.05f, 0f));
            float tube = Box(x, y, -0.4f, 0f, 0.48f, 0.09f);
            float mouth = Box(x, y, -0.88f, 0f, 0.06f, 0.2f);
            float hole = -(Box(x, y, 0.62f, 0f, 0.14f, 0.32f)); // hollow bell
            return Mathf.Min(Mathf.Max(bell, hole), Mathf.Min(tube, mouth));
        }

        private static float Gear(float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y), a = Mathf.Atan2(y, x);
            float teeth = 0.72f + 0.16f * Mathf.Clamp(Mathf.Cos(a * 8f) * 3f, -1f, 1f);
            float outer = r - teeth;
            float hole = 0.3f - r;
            return Mathf.Max(outer, hole);
        }

        private static float StarGlyph(float x, float y)
        {
            float a = Mathf.Atan2(x, y), r = Mathf.Sqrt(x * x + y * y);
            float k = Mathf.Repeat(a, Mathf.PI * 2f / 5f) - Mathf.PI / 5f;
            float edge = Mathf.Lerp(0.42f, 0.95f, Mathf.Pow(Mathf.Abs(Mathf.Cos(k * 2.5f)), 3f));
            return r - edge * 0.9f;
        }
    }
}
