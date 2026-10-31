using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SoulBind.UI
{
    /// <summary>
    /// The look from the SoulBind UI Screens design canvas, in one place: the colours, the two
    /// fonts (Cinzel for titles and big buttons, Inter for everything else), rounded panels with a
    /// thin border, and the four button styles. Menus build themselves from these helpers.
    /// Positions are in design pixels on a 1280 x 720 frame (see CreateFrame), measured from the
    /// frame's top-left corner, so numbers can be copied straight from the design.
    /// </summary>
    public static class UiKit
    {
        // ---------- Colours (from the design) ----------

        public static readonly Color Background = Hex(0x0F0B16);
        public static readonly Color Panel = Hex(0x1A1325);
        public static readonly Color Raised = Hex(0x2A1F38);   // buttons and steppers
        public static readonly Color Inset = Hex(0x120D1A);    // segmented controls, info rows
        public static readonly Color Field = Hex(0x0C0912);    // text boxes
        public static readonly Color GoldLine = Hex(0x8A6D3B); // panel borders
        public static readonly Color BronzeLine = Hex(0x5A4628);
        public static readonly Color Line = Hex(0x3D3048);     // quiet borders and dividers
        public static readonly Color Gold = Hex(0xDBB261);
        public static readonly Color GoldBright = Hex(0xFFDB80);
        public static readonly Color Title = Hex(0xF2C766);
        public static readonly Color Link = Hex(0xF2D38A);
        public static readonly Color Text = Hex(0xF5EBD8);
        public static readonly Color Soft = Hex(0xD9C9A8);
        public static readonly Color Muted = Hex(0xB8AA92);
        public static readonly Color PlayFill = Hex(0xB8862E);
        public static readonly Color PlayText = Hex(0x1A1008);
        public static readonly Color ReadyFill = Hex(0x1F3A2A);
        public static readonly Color ReadyText = Hex(0x8FE0A8);
        public static readonly Color OpenFill = Hex(0x241B30);

        public enum ButtonStyle
        {
            Primary,   // gold, Cinzel capitals: the main action on a screen
            Secondary, // dark purple, gold border, Inter
            Ghost,     // no fill, quiet border, Inter
            Outline    // dark purple, bright gold border, Cinzel capitals
        }

        public static Color Hex(uint rgb) => new Color(
            ((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);

        // ---------- Fonts ----------

        private static TMP_FontAsset _cinzel;
        private static TMP_FontAsset _cinzelBlack;
        private static TMP_FontAsset _inter;
        private static TMP_FontAsset _interSemiBold;
        private static TMP_FontAsset _interBold;
        private static bool _fontsLoaded;
        private static readonly Dictionary<int, Sprite> Fills = new Dictionary<int, Sprite>();
        private static readonly Dictionary<int, Sprite> Rings = new Dictionary<int, Sprite>();

        // Looked up again on every Play, even with domain reload turned off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _fontsLoaded = false;
            Fills.Clear();
            Rings.Clear();
        }

        /// <summary>Cinzel Bold: titles, names and the big buttons. Null if the font is missing.</summary>
        public static TMP_FontAsset Cinzel { get { LoadFonts(); return _cinzel; } }
        public static TMP_FontAsset CinzelBlack { get { LoadFonts(); return _cinzelBlack; } }
        /// <summary>Inter: body text. Null if the font is missing (TextMesh Pro's default is used).</summary>
        public static TMP_FontAsset Inter { get { LoadFonts(); return _inter; } }
        public static TMP_FontAsset InterSemiBold { get { LoadFonts(); return _interSemiBold; } }
        public static TMP_FontAsset InterBold { get { LoadFonts(); return _interBold; } }

        // The .ttf files in Resources/Fonts become TextMesh Pro fonts at runtime, so no font asset
        // has to be made by hand. A ready-made "Cinzel SDF" asset, if there is one, still wins.
        private static void LoadFonts()
        {
            if (_fontsLoaded)
                return;
            _fontsLoaded = true;
            _cinzel = Resources.Load<TMP_FontAsset>("Fonts/Cinzel SDF") ?? Make("Fonts/Cinzel-Bold");
            _cinzelBlack = Make("Fonts/Cinzel-Black") ?? _cinzel;
            _inter = Make("Fonts/Inter-Regular");
            _interSemiBold = Make("Fonts/Inter-SemiBold") ?? _inter;
            _interBold = Make("Fonts/Inter-Bold") ?? _interSemiBold;
        }

        private static TMP_FontAsset Make(string path)
        {
            Font font = Resources.Load<Font>(path);
            if (font == null)
                return null;
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font);
            if (asset != null)
                asset.name = font.name;
            return asset;
        }

        // ---------- The design frame ----------

        /// <summary>
        /// A 1280 x 720 box in the middle of parent, scaled to fit whatever the screen is, so a
        /// screen can be laid out with the design's own numbers. Clicks still land correctly.
        /// </summary>
        public static RectTransform CreateFrame(RectTransform parent, string name)
        {
            RectTransform frame = CreateRect(name, parent);
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = new Vector2(1280f, 720f);
            frame.gameObject.AddComponent<FitToParent>();
            return frame;
        }

        // Keeps the design frame as big as possible without spilling off the screen.
        private sealed class FitToParent : MonoBehaviour
        {
            private Vector2 _lastSize;

            private void LateUpdate()
            {
                var parent = transform.parent as RectTransform;
                if (parent == null)
                    return;
                Vector2 size = parent.rect.size;
                if (size == _lastSize)
                    return;
                _lastSize = size;
                float scale = Mathf.Min(size.x / 1280f, size.y / 720f);
                transform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        // ---------- Building blocks ----------

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>Places rect at x, y (its top-left, in design pixels from the parent's top-left).</summary>
        public static RectTransform At(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        public static RectTransform Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            return rect;
        }

        /// <summary>A rounded panel: a fill and a thin border on top. Either colour can be clear.</summary>
        public static RectTransform Box(Transform parent, string name, Color fill, Color border,
            float radius, float borderWidth = 1f)
        {
            RectTransform rect = CreateRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = RoundedFill(radius);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 2f;
            image.color = fill;
            image.raycastTarget = fill.a > 0f;
            if (border.a > 0f && borderWidth > 0f)
                AddBorder(rect, border, radius, borderWidth);
            return rect;
        }

        /// <summary>A thin rounded border over rect (a child named Border), returned so it can be recoloured.</summary>
        public static Image AddBorder(RectTransform rect, Color color, float radius, float width)
        {
            RectTransform ring = Stretch(CreateRect("Border", rect));
            Image image = ring.gameObject.AddComponent<Image>();
            image.sprite = RoundedRing(radius, width);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 2f;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>A circle (a box whose corners meet), for avatars and round buttons.</summary>
        public static RectTransform Circle(Transform parent, string name, float size, Color fill, Color border,
            float borderWidth = 1f)
        {
            RectTransform rect = Box(parent, name, fill, border, size / 2f, borderWidth);
            rect.sizeDelta = new Vector2(size, size);
            return rect;
        }

        public static TMP_Text Label(Transform parent, string name, string text, float size, TMP_FontAsset font,
            Color color, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            RectTransform rect = CreateRect(name, parent);
            TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null)
                tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;
            return tmp;
        }

        /// <summary>A button in one of the design's styles. Returns its label; the Button is on its parent.</summary>
        public static TMP_Text Button(Transform parent, string name, string text, ButtonStyle style,
            UnityAction onClick, float fontSize = 0f, float radius = 10f)
        {
            Color fill, border, ink;
            TMP_FontAsset font;
            float borderWidth = 1f;
            switch (style)
            {
                case ButtonStyle.Primary:
                    fill = PlayFill; border = GoldBright; ink = PlayText; font = Cinzel; borderWidth = 2f;
                    break;
                case ButtonStyle.Outline:
                    fill = Raised; border = Gold; ink = Link; font = Cinzel; borderWidth = 2f;
                    break;
                case ButtonStyle.Ghost:
                    fill = new Color(0f, 0f, 0f, 0f); border = Line; ink = Soft; font = Inter;
                    break;
                default:
                    fill = Raised; border = GoldLine; ink = Link; font = InterSemiBold;
                    break;
            }

            RectTransform rect = Box(parent, name, fill, border, radius, borderWidth);
            Image image = rect.GetComponent<Image>();
            image.raycastTarget = true; // a ghost button is clear but still clickable
            UnityEngine.UI.Button button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            button.colors = colors;
            if (onClick != null)
                button.onClick.AddListener(onClick);

            bool capitals = style == ButtonStyle.Primary || style == ButtonStyle.Outline;
            float size = fontSize > 0f ? fontSize : (capitals ? 21f : 16f);
            TMP_Text label = Label(rect, "Label", text, size, font, ink);
            Stretch(label.rectTransform, 4f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            if (capitals)
            {
                label.fontStyle = FontStyles.UpperCase;
                label.characterSpacing = 8f;
            }
            return label;
        }

        /// <summary>A thin horizontal line.</summary>
        public static RectTransform Divider(Transform parent, float x, float y, float width)
        {
            RectTransform rect = At(CreateRect("Divider", parent), x, y, width, 1f);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = Line;
            image.raycastTarget = false;
            return rect;
        }

        // ---------- Rounded sprites (drawn once per size, white, 9-sliced) ----------

        // Drawn at twice the design size (Image.pixelsPerUnitMultiplier = 2) so edges stay smooth.
        private const float Detail = 2f;

        private static Sprite RoundedFill(float radius)
        {
            int key = Mathf.RoundToInt(radius * Detail);
            if (Fills.TryGetValue(key, out Sprite sprite) && sprite != null)
                return sprite;
            sprite = DrawRounded(key, 0);
            Fills[key] = sprite;
            return sprite;
        }

        private static Sprite RoundedRing(float radius, float width)
        {
            int r = Mathf.RoundToInt(radius * Detail);
            int w = Mathf.Max(1, Mathf.RoundToInt(width * Detail));
            int key = r * 1000 + w;
            if (Rings.TryGetValue(key, out Sprite sprite) && sprite != null)
                return sprite;
            sprite = DrawRounded(r, w);
            Rings[key] = sprite;
            return sprite;
        }

        // A white rounded square: filled when ring is 0, otherwise just an edge ring pixels wide.
        // The corners are the radius; the middle row and column stretch.
        private static Sprite DrawRounded(int radius, int ring)
        {
            int corner = Mathf.Max(radius, ring + 1);
            int size = corner * 2 + 2;
            var pixels = new Color32[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Distance outside a rounded box (negative inside).
                    float qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                    float qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                    float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude
                                    + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    float alpha = Mathf.Clamp01(0.5f - outside);
                    if (ring > 0)
                        alpha *= Mathf.Clamp01(0.5f + outside + ring);
                    pixels[x + y * size] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = ring > 0 ? "UiRing" : "UiRounded"
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var border = new Vector4(corner, corner, corner, corner);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, border);
        }
    }
}
