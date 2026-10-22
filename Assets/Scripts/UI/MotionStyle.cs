using UnityEngine;

namespace SoulBind.UI
{
    /// <summary>
    /// The game's motion style: one speed for every animation, the screen-shake switch, and the easing
    /// curves. Heavy, deliberate motion: things ease in and out, never bounce or overshoot.
    /// MatchController sets Speed and ScreenShake from its Inspector.
    /// </summary>
    public static class MotionStyle
    {
        /// <summary>1 = normal. 2 = twice as fast. Every animation's time is divided by it.</summary>
        public static float Speed { get; set; } = 1f;

        public static bool ScreenShake { get; set; } = true;

        /// <summary>Real seconds for an animation designed to take this long at normal speed.</summary>
        public static float Seconds(float designed) => designed / Mathf.Max(0.1f, Speed);

        /// <summary>Starts and ends gently (smootherstep).</summary>
        public static float InOut(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        /// <summary>Starts fast, settles slowly.</summary>
        public static float Out(float t)
        {
            t = 1f - Mathf.Clamp01(t);
            return 1f - t * t * t;
        }

        /// <summary>Starts slowly, ends fast (a fall, a strike).</summary>
        public static float In(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t;
        }

        // Soft round glow, white with alpha fading to the edge: tinted for embers, auras and smoke.
        private static Sprite _glow;
        private static Sprite _vignette;
        private static Sprite _ring;

        /// <summary>A soft glowing ring (brightest at 60% of the radius, clear in the middle): for halos around pieces.</summary>
        public static Sprite Ring
        {
            get
            {
                if (_ring != null)
                    return _ring;
                const int size = 128;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "Ring"
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
                        float a = Mathf.Exp(-((d - 0.6f) / 0.14f) * ((d - 0.6f) / 0.14f));
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                    }
                }
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                _ring = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
                return _ring;
            }
        }

        public static Sprite Glow
        {
            get
            {
                if (_glow == null)
                    _glow = MakeRadial(128, 0f, 1f, 2f, "Glow");
                return _glow;
            }
        }

        /// <summary>Clear in the middle, dark at the edges: for the strike's dark pulse.</summary>
        public static Sprite Vignette
        {
            get
            {
                if (_vignette == null)
                    _vignette = MakeRadial(256, 0.45f, 1.05f, 1.6f, "Vignette", true);
                return _vignette;
            }
        }

        // A radial gradient: alpha 1 at the centre fading to 0 between inner and outer (in radii),
        // or the reverse when inverted.
        private static Sprite MakeRadial(int size, float inner, float outer, float power, string name, bool inverted = false)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = name
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
                    float a = 1f - Mathf.Clamp01((d - inner) / (outer - inner));
                    a = Mathf.Pow(a, power);
                    if (inverted)
                        a = 1f - a;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}