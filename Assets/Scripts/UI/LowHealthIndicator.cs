using SoulBind.Core;
using SoulBind.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace SoulBind.UI
{
    /// <summary>
    /// A red glow around the edge of the screen that pulses while your Vessel is low, and beats faster
    /// the closer you are to dying. On Low graphics it holds still. It sits under the HUD and never
    /// catches taps. MatchHud creates it at runtime, so it needs no setup. Display only.
    /// </summary>
    public sealed class LowHealthIndicator : MonoBehaviour
    {
        private const float LowShare = 0.3f;     // of the full Vessel: 15 of 50
        private const float SlowBeat = 2.5f;     // radians a second, at the top of the low range
        private const float FastBeat = 6f;       // at 1 Vessel
        private const float FaintAlpha = 0.35f;
        private const float StrongAlpha = 0.8f;
        private const int TextureSize = 64;
        private const float GlowWidth = 0.3f;    // of the distance from the edge to the middle

        private MatchController _controller;
        private RawImage _glow;
        private Texture2D _texture;
        private float _beat;  // radians a second now
        private float _phase;

        /// <summary>Creates the glow behind everything else on the canvas.</summary>
        public static LowHealthIndicator Create(Canvas canvas, MatchController controller)
        {
            var go = new GameObject("LowHealthIndicator", typeof(RectTransform));
            go.layer = canvas.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.SetAsFirstSibling();

            LowHealthIndicator indicator = go.AddComponent<LowHealthIndicator>();
            indicator._controller = controller;
            indicator.Build();
            controller.StateChanged += indicator.Refresh;
            indicator.Refresh();
            return indicator;
        }

        private void OnDestroy()
        {
            if (_controller != null)
                _controller.StateChanged -= Refresh;
            if (_texture != null)
                Destroy(_texture);
        }

        // The glow is one small texture, stretched over the screen.
        private void Build()
        {
            _texture = CreateTexture();
            _glow = gameObject.AddComponent<RawImage>();
            _glow.texture = _texture;
            _glow.color = new Color(0.8f, 0.05f, 0.05f, 0f);
            _glow.raycastTarget = false;
            _glow.enabled = false;
        }

        // Clear in the middle, red-ready white at the edges.
        private static Texture2D CreateTexture()
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[TextureSize * TextureSize];
            float half = (TextureSize - 1) / 2f;
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float edge = Mathf.Max(Mathf.Abs(x - half), Mathf.Abs(y - half)) / half;
                    float glow = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f - GlowWidth, 1f, edge));
                    pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)(glow * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        // Turns the glow on or off for whoever this screen is showing.
        private void Refresh()
        {
            int vessel = ViewerVessel();
            int low = Mathf.CeilToInt(GameConstants.MaxVessel * LowShare);
            bool show = vessel > 0 && vessel <= low;
            if (_glow.enabled != show)
                _glow.enabled = show;
            if (!show)
                return;
            _beat = Mathf.Lerp(FastBeat, SlowBeat, Mathf.InverseLerp(1f, low, vessel));
            if (!GraphicsQuality.AnimateUi)
                SetAlpha(StrongAlpha);
        }

        // The viewer's Vessel, or 0 with no running match or once they are out.
        private int ViewerVessel()
        {
            Match match = _controller != null ? _controller.CurrentMatch : null;
            if (match == null || match.IsOver)
                return 0;
            var apostles = match.Battlefield.Apostles;
            int viewer = _controller.ViewerId;
            for (int i = 0; i < apostles.Count; i++)
            {
                if (apostles[i].Id == viewer)
                    return apostles[i].IsEliminated ? 0 : SoulBind.Managers.StrikeDisplay.ShownVessel(apostles[i]);
            }
            return 0;
        }

        private void Update()
        {
            if (!_glow.enabled || !GraphicsQuality.AnimateUi)
                return;
            _phase = Mathf.Repeat(_phase + _beat * Time.unscaledDeltaTime, Mathf.PI * 2f);
            SetAlpha(Mathf.Lerp(FaintAlpha, StrongAlpha, (Mathf.Sin(_phase) + 1f) / 2f));
        }

        private void SetAlpha(float alpha)
        {
            Color color = _glow.color;
            color.a = alpha;
            _glow.color = color;
        }
    }
}
