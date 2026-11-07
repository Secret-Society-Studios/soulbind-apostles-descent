using UnityEngine;
using UnityEngine.UI;

namespace SoulBind.UI
{
    /// <summary>
    /// The main menu's picture, softly blurred and dimmed so the menu reads on top of it, and drifting
    /// slowly from side to side. The blur is made once: the picture is halved a few times into small
    /// render textures (each halving averages 2x2 pixels), then shown stretched with bilinear filtering.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class MenuBackdrop : MonoBehaviour
    {
        private const int BlurSteps = 3;        // each step halves the size: more steps, softer picture
        private const float Dim = 0.8f;       // keeps even muted supporting text readable over the brightest art
        private const float Overscan = 1.08f;   // a little larger than the screen, so the drift never shows an edge
        private const float DriftSeconds = 40f; // one full sway, left and back
        private const float DriftShare = 0.03f; // of the picture's width

        private RenderTexture _blurred;
        private RectTransform _picture;

        /// <summary>Puts the blurred, dimmed picture under everything in parent.</summary>
        public static void Create(RectTransform parent, Sprite art, Color dim)
        {
            RectTransform root = UiKit.Stretch(UiKit.CreateRect("Backdrop", parent));
            root.SetAsFirstSibling();
            root.gameObject.AddComponent<RectMask2D>();

            Image fill = root.gameObject.AddComponent<Image>();
            fill.color = dim; // shows alone if the picture can't be made
            fill.raycastTarget = false;

            var backdrop = root.gameObject.AddComponent<MenuBackdrop>();
            backdrop.Build(art, dim);
        }

        private void Build(Sprite art, Color dim)
        {
            _blurred = Blur(art);
            if (_blurred == null)
                return;

            // Covers the screen at any aspect, cropping the sides or top rather than leaving bars.
            _picture = UiKit.CreateRect("Picture", transform);
            _picture.anchorMin = _picture.anchorMax = _picture.pivot = new Vector2(0.5f, 0.5f);
            var fit = _picture.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = art.rect.width / art.rect.height;
            _picture.localScale = Vector3.one * Overscan;

            RawImage image = _picture.gameObject.AddComponent<RawImage>();
            image.texture = _blurred;
            image.raycastTarget = false;

            RectTransform shade = UiKit.Stretch(UiKit.CreateRect("Dim", transform));
            Image cover = shade.gameObject.AddComponent<Image>();
            cover.color = new Color(dim.r, dim.g, dim.b, Dim);
            cover.raycastTarget = false;
        }

        private static RenderTexture Blur(Sprite art)
        {
            if (art == null || art.texture == null)
                return null;

            Rect area = art.textureRect;
            Texture source = art.texture;
            int width = Mathf.Max(1, (int)area.width);
            int height = Mathf.Max(1, (int)area.height);

            // A 1:1 copy first (only the sprite's own area), so the source's filter mode doesn't matter.
            RenderTexture current = Target(width, height);
            var scale = new Vector2(area.width / source.width, area.height / source.height);
            var offset = new Vector2(area.x / source.width, area.y / source.height);
            Graphics.Blit(source, current, scale, offset);

            for (int i = 0; i < BlurSteps; i++)
            {
                width = Mathf.Max(1, width / 2);
                height = Mathf.Max(1, height / 2);
                RenderTexture smaller = Target(width, height);
                Graphics.Blit(current, smaller);
                current.Release();
                Destroy(current);
                current = smaller;
            }

            // One step back up smooths the blocky look the smallest size would have when stretched.
            RenderTexture result = Target(width * 2, height * 2);
            Graphics.Blit(current, result);
            current.Release();
            Destroy(current);
            return result;
        }

        private static RenderTexture Target(int width, int height)
        {
            var texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
            };
            texture.Create();
            return texture;
        }

        private void Update()
        {
            if (_picture == null)
                return;
            float sway = Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / DriftSeconds);
            _picture.anchoredPosition = new Vector2(sway * DriftShare * _picture.rect.width, 0f);
        }

        private void OnDestroy()
        {
            if (_blurred == null)
                return;
            _blurred.Release();
            Destroy(_blurred);
        }
    }
}
