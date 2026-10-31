using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoulBind.UI
{
    /// <summary>
    /// The game's dark gothic look, applied at runtime over the HUD that SoulBind > Build Match HUD made,
    /// so nothing in the scene is rebuilt and hand-made tweaks stay. Every frame, gradient and glow is
    /// drawn in code here: dark panels with thin gold double borders, gradient bars, dark buttons with gold
    /// edges that brighten on hover, a pool of light over the table with darkness around it, and an
    /// optional serif title font (Cinzel, in Resources/Fonts) for names, titles and buttons.
    /// </summary>
    public static class GothicTheme
    {
        public static readonly Color Gold = new Color(0.86f, 0.70f, 0.38f);
        public static readonly Color GoldBright = new Color(1f, 0.86f, 0.50f);
        public static readonly Color GoldDim = new Color(0.55f, 0.44f, 0.24f, 0.85f);
        public static readonly Color PanelColor = new Color(0.09f, 0.07f, 0.12f, 0.94f);
        public static readonly Color ButtonColor = new Color(0.17f, 0.12f, 0.22f, 1f);
        public static readonly Color StrikeButtonColor = new Color(0.42f, 0.07f, 0.10f, 1f);
        public static readonly Color TextColor = new Color(0.92f, 0.88f, 0.80f);
        public static readonly Color TableEdge = new Color(0.025f, 0.018f, 0.035f);
        public static readonly Color TableLight = new Color(0.16f, 0.12f, 0.17f);

        private static Sprite _panel;
        private static Sprite _frame;
        private static Sprite _glow;
        private static Sprite _bar;
        private static Sprite _table;
        private static TMP_FontAsset _titleFont;
        private static bool _fontLooked;

        // Looked up again on every Play, even with domain reload turned off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _fontLooked = false;
            _titleFont = null;
        }

        /// <summary>The serif title font, if it was added at Assets/Resources/Fonts/Cinzel SDF; otherwise null.</summary>
        public static TMP_FontAsset TitleFont
        {
            get
            {
                if (!_fontLooked)
                {
                    _fontLooked = true;
                    _titleFont = Resources.Load<TMP_FontAsset>("Fonts/Cinzel SDF");
                    if (_titleFont == null)
                        _titleFont = Resources.Load<TMP_FontAsset>("Cinzel SDF");
                }
                return _titleFont;
            }
        }

        // ---- Whole screens ----

        /// <summary>
        /// Restyles the HUD built by SoulBind > Build Match HUD: the turn info box, the buttons, the hint
        /// line and the titles. Cards, piles and the portraits (styled one by one as they are made) are skipped.
        /// </summary>
        public static void ApplyToHud(Transform hudRoot)
        {
            if (hudRoot == null)
                return;

            foreach (Button button in hudRoot.GetComponentsInChildren<Button>(true))
            {
                if (!IsSkipped(button.transform, hudRoot))
                    StyleButton(button);
            }

            foreach (Image image in hudRoot.GetComponentsInChildren<Image>(true))
            {
                if (IsSkipped(image.transform, hudRoot) || image.GetComponent<Button>() != null || image.sprite != null)
                    continue;
                // Plain dark boxes big enough to be panels (the turn info box) get the panel look.
                Color c = image.color;
                RectTransform rect = image.rectTransform;
                if (c.a > 0.5f && c.r + c.g + c.b < 0.75f && rect.rect.width > 120f && rect.rect.height > 50f)
                    StylePanel(image, true);
            }

            foreach (TMP_Text text in hudRoot.GetComponentsInChildren<TMP_Text>(true))
            {
                if (IsSkipped(text.transform, hudRoot))
                    continue;
                switch (text.name)
                {
                    case "TurnText":
                        Title(text, GoldBright);
                        break;
                    case "RoundText":
                    case "CapacityText":
                        Title(text, Gold);
                        break;
                    case "HintText":
                        text.color = new Color(TextColor.r, TextColor.g, TextColor.b, 0.85f);
                        text.fontStyle |= FontStyles.Italic;
                        AddShadow(text.gameObject);
                        break;
                    case "PreviewReason":
                        text.color = new Color(0.95f, 0.35f, 0.32f);
                        Title(text, text.color);
                        break;
                }
            }
        }

        /// <summary>The serif font (if present) for every title in the banners, the guide and the end screen.</summary>
        public static void ApplyToOverlay(Transform overlay)
        {
            if (overlay == null || TitleFont == null)
                return;
            foreach (TMP_Text text in overlay.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.name != "Summary") // the end screen's table keeps its even-width numbers
                    text.font = TitleFont;
            }
        }

        /// <summary>The table becomes dark, with a soft pool of light over the mat; the camera's empty background goes black.</summary>
        public static void ApplyToTable(BoardView board)
        {

            if (board != null)
            {
                Transform table = board.transform.Find("Table");
                SpriteRenderer renderer = table != null ? table.GetComponent<SpriteRenderer>() : null;
                if (renderer != null)
                {
                    renderer.sprite = TableSprite;
                    renderer.color = Color.white;
                }
            }
            Camera cam = Camera.main;
            if (cam != null)
                cam.backgroundColor = TableEdge;
        }

        // ---- Pieces ----

        /// <summary>One Apostle's portrait box: panel, gold frame, framed portrait and gradient bars.</summary>
        public static void StylePortrait(ApostlePanelView panel)
        {
            if (panel == null)
                return;
            Transform root = panel.transform;
            Image background = root.GetComponent<Image>();
            if (background != null)
            {
                // ApostlePanelView keeps choosing the background colour (normal, turn, out); the sprite adds depth.
                background.sprite = PanelSprite;
                background.type = Image.Type.Sliced;
            }
            AddFrame(root, "ThemeFrame", GoldDim, 0f);
            AddGlow(root);

            Transform portrait = root.Find("Portrait");
            if (portrait != null)
                AddFrame(portrait, "ThemeFrame", Gold, 0f);

            StyleBar(root.Find("VesselBar"));
            StyleBar(root.Find("CorruptionBar"));
            StyleBar(root.Find("SoulsBar"));

            Transform name = root.Find("NameText");
            if (name != null)
                Title(name.GetComponent<TMP_Text>(), GoldBright);
            Transform status = root.Find("StatusText");
            if (status != null)
                Title(status.GetComponent<TMP_Text>(), Gold);
            Transform stats = root.Find("StatsText");
            if (stats != null && stats.GetComponent<TMP_Text>() != null)
                stats.GetComponent<TMP_Text>().color = TextColor;

            // Real bar art, where the UiSkin asset has any, goes over the drawn look.
            UiSkin skin = UiSkin.Current;
            if (skin != null)
                skin.ApplyToPortrait(root);
        }

        /// <summary>The Apostle whose turn it is: a bright gold frame and a soft glow around the box.</summary>
        public static void SetPortraitHighlight(ApostlePanelView panel, bool isCurrent)
        {
            if (panel == null)
                return;
            Transform frame = panel.transform.Find("ThemeFrame");
            if (frame != null)
                frame.GetComponent<Image>().color = isCurrent ? GoldBright : GoldDim;
            Transform glow = panel.transform.Find("ThemeGlow");
            if (glow != null)
                glow.gameObject.SetActive(isCurrent);
        }

        public static void StyleButton(Button button)
        {
            Image image = button.targetGraphic as Image;
            if (image == null)
                return;
            bool strike = image.color.r > image.color.g + 0.25f; // the red Strike button stays red
            image.sprite = PanelSprite;
            image.type = Image.Type.Sliced;
            image.color = strike ? StrikeButtonColor : ButtonColor;

            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.pressedColor = new Color(0.65f, 0.6f, 0.6f, 1f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
            colors.colorMultiplier = 1.5f; // lets hovering brighten the dark button
            colors.fadeDuration = 0.15f;
            button.colors = colors;

            AddFrame(button.transform, "ThemeFrame", Gold, 0f);
            foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
                Title(label, GoldBright);
        }

        public static void StylePanel(Image image, bool framed)
        {
            image.sprite = PanelSprite;
            image.type = Image.Type.Sliced;
            image.color = PanelColor;
            if (framed)
                AddFrame(image.transform, "ThemeFrame", GoldDim, 0f);
        }

        /// <summary>
        /// Gives a runtime-built bar (back Image, "Fill" child) the same look as the portrait bars:
        /// dark back, sheen on the fill, faint gold frame.
        /// </summary>
        public static void StyleRuntimeBar(Transform bar) => StyleBar(bar);

        /// <summary>A thin gold double frame over this rect, named "ThemeFrame". Returns its Image.</summary>
        public static Image AddThemeFrame(Transform parent, Color color)
        {
            AddFrame(parent, "ThemeFrame", color, 0f);
            return (Image)parent.Find("ThemeFrame").GetComponent(typeof(Image));
        }

        private static void StyleBar(Transform bar)
        {
            if (bar == null)
                return;
            Image back = bar.GetComponent<Image>();
            if (back != null)
            {
                back.sprite = PanelSprite;
                back.type = Image.Type.Sliced;
                back.color = new Color(0.02f, 0.01f, 0.03f, 0.9f);
            }
            Transform fill = bar.Find("Fill");
            Image fillImage = fill != null ? fill.GetComponent<Image>() : null;
            if (fillImage != null)
            {
                // The fill keeps the colour ApostlePanelView gives it; the sprite adds a sheen on top.
                fillImage.sprite = BarSprite;
                fillImage.type = Image.Type.Sliced;
            }
            AddFrame(bar, "ThemeFrame", new Color(Gold.r, Gold.g, Gold.b, 0.5f), 0f);
            Transform label = bar.Find("Label");
            if (label != null)
                AddShadow(label.gameObject);
        }

        // ---- Small helpers ----

        private static void Title(TMP_Text text, Color color)
        {
            if (text == null)
                return;
            if (TitleFont != null)
                text.font = TitleFont;
            text.color = color;
            AddShadow(text.gameObject);
        }

        // A thin dark drop shadow, so text stands off the background.
        private static void AddShadow(GameObject textObject)
        {
            if (textObject.GetComponent<Shadow>() != null)
                return;
            Shadow shadow = textObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
        }

        // A gold double border over the edges of a box. Transparent in the middle, never catches clicks.
        private static void AddFrame(Transform parent, string name, Color color, float outset)
        {
            if (parent.Find(name) != null)
                return;
            RectTransform rect = NewChild(parent, name);
            rect.offsetMin = new Vector2(-outset, -outset);
            rect.offsetMax = new Vector2(outset, outset);
            rect.SetAsLastSibling();
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = FrameSprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
        }

        // A soft gold glow around the box, shown only on the turn's Apostle.
        private static void AddGlow(Transform parent)
        {
            if (parent.Find("ThemeGlow") != null)
                return;
            RectTransform rect = NewChild(parent, "ThemeGlow");
            rect.offsetMin = new Vector2(-14f, -14f);
            rect.offsetMax = new Vector2(14f, 14f);
            rect.SetAsFirstSibling();
            LayoutElement ignore = rect.gameObject.AddComponent<LayoutElement>();
            ignore.ignoreLayout = true;
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = GlowSprite;
            image.type = Image.Type.Sliced;
            image.color = new Color(GoldBright.r, GoldBright.g, GoldBright.b, 0.55f);
            image.raycastTarget = false;
            rect.gameObject.SetActive(false);
        }

        private static RectTransform NewChild(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            LayoutElement layout = go.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            return rect;
        }

        // Cards, piles and portraits look after themselves.
        private static bool IsSkipped(Transform t, Transform root)
        {
            for (; t != null && t != root; t = t.parent)
            {
                if (t.GetComponent<CardView>() != null || t.GetComponent<ApostlePanelView>() != null
                    || t.GetComponent<DrawPileView>() != null || t.GetComponent<DiscardPileView>() != null
                    || t.GetComponent<MatchOverlay>() != null || t.name.StartsWith("Theme"))
                    return true;
            }
            return false;
        }

        // ---- Sprites, drawn once ----

        // White, with a gentle top-to-bottom shading and rounded corners: tinted by the image's colour.
        private static Sprite PanelSprite => _panel != null ? _panel : (_panel = MakeSliced(64, 16, (x, y, s) =>
        {
            float alpha = RoundedAlpha(x, y, s, 6f);
            float shade = Mathf.Lerp(0.78f, 1f, y / (s - 1f)); // lighter at the top
            float rim = EdgeDistance(x, y, s) < 2f ? 1.15f : 1f;
            float v = Mathf.Clamp01(shade * rim);
            return new Color(v, v, v, alpha);
        }));

        // A thin outer line and a fainter inner line, with small square studs in the corners.
        private static Sprite FrameSprite => _frame != null ? _frame : (_frame = MakeSliced(64, 16, (x, y, s) =>
        {
            float d = EdgeDistance(x, y, s);
            float a = 0f;
            if (d >= 0.5f && d < 2f)
                a = 1f;
            else if (d >= 4.5f && d < 5.5f)
                a = 0.45f;
            bool corner = (x < 6 || x > s - 7) && (y < 6 || y > s - 7);
            if (corner && d < 6f)
                a = Mathf.Max(a, 0.9f);
            a *= RoundedAlpha(x, y, s, 3f);
            return new Color(1f, 1f, 1f, a);
        }));

        /// <summary>A soft glow just outside a box's edges (9-sliced), for highlights such as the hands on the table.</summary>
        public static Sprite EdgeGlowSprite => GlowSprite;

        // Soft light just outside the edges, fading outward.
        private static Sprite GlowSprite => _glow != null ? _glow : (_glow = MakeSliced(64, 24, (x, y, s) =>
        {
            float d = EdgeDistance(x, y, s); // 0 at the outer edge of the glow
            float a = Mathf.Clamp01(d / 14f);
            a = a * a * (1f - Mathf.Clamp01((d - 14f) / 4f));
            return new Color(1f, 1f, 1f, a);
        }));

        // A bar fill with a brighter band near the top, like light catching polished metal.
        private static Sprite BarSprite => _bar != null ? _bar : (_bar = MakeSliced(32, 6, (x, y, s) =>
        {
            float t = y / (s - 1f);
            float v = Mathf.Lerp(0.7f, 1f, t);
            if (t > 0.6f && t < 0.8f)
                v = 1f;
            return new Color(v, v, v, 1f);
        }));

        // Dark with a soft pool of light in the middle. The table object is about six boards wide,
        // so the light covers roughly the mat and fades into darkness well before the edges.
        private static Sprite TableSprite
        {
            get
            {
                if (_table != null)
                    return _table;
                const int size = 256;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "GothicTable"
                };
                var pixels = new Color32[size * size];
                float half = size / 2f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f - half) / half;
                        float dy = (y + 0.5f - half) / half;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float light = Mathf.Exp(-(d / 0.3f) * (d / 0.3f));
                        pixels[y * size + x] = Color.Lerp(TableEdge, TableLight, light);
                    }
                }
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                _table = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
                return _table;
            }
        }

        private delegate Color Pixel(int x, int y, int size);

        private static Sprite MakeSliced(int size, int border, Pixel pixel)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = pixel(x, y, size);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        // Pixels from the nearest edge of the square.
        private static float EdgeDistance(int x, int y, int size) =>
            Mathf.Min(Mathf.Min(x, y), Mathf.Min(size - 1 - x, size - 1 - y));

        // 1 inside, fading to 0 outside corners rounded with this radius.
        private static float RoundedAlpha(int x, int y, int size, float radius)
        {
            float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
            float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
            float dx = x + 0.5f - cx;
            float dy = y + 0.5f - cy;
            return Mathf.Clamp01(radius + 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
        }
    }
}