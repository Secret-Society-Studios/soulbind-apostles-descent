using UnityEngine;

namespace SoulBind.Utility
{
    /// <summary>Builds a plain pointy-top hexagon sprite in code. A stand-in until real tile art exists.</summary>
    public static class HexSpriteFactory
    {
        private const float Sqrt3 = 1.7320508f;
        private const int TextureHeight = 256;

        /// <summary>A white hexagon sprite that is 2 * hexSize tall in world units.</summary>
        public static Sprite Create(float hexSize)
        {
            int height = TextureHeight;
            int width = Mathf.RoundToInt(height * Sqrt3 / 2f);
            float radius = height / 2f;
            float halfWidth = Sqrt3 / 2f * radius;

            var pixels = new Color32[width * height];
            var inside = new Color32(255, 255, 255, 255);
            var outside = new Color32(255, 255, 255, 0);

            for (int py = 0; py < height; py++)
            {
                float dy = Mathf.Abs(py + 0.5f - height / 2f);
                for (int px = 0; px < width; px++)
                {
                    float dx = Mathf.Abs(px + 0.5f - width / 2f);
                    bool isInside = dx <= halfWidth && dy <= radius - dx / Sqrt3;
                    pixels[py * width + px] = isInside ? inside : outside;
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply();

            float pixelsPerUnit = height / (2f * hexSize);
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }

        /// <summary>The six corners of a pointy-top hexagon, for a collider outline.</summary>
        public static Vector2[] GetCorners(float radius)
        {
            var corners = new Vector2[6];
            for (int i = 0; i < 6; i++)
            {
                float angle = (60f * i - 30f) * Mathf.Deg2Rad;
                corners[i] = new Vector2(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle));
            }
            return corners;
        }
    }
}