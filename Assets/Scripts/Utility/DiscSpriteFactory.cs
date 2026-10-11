using UnityEngine;

namespace SoulBind.Utility
{
    /// <summary>Builds a plain white disc sprite in code. A stand-in until real Apostle art exists.</summary>
    public static class DiscSpriteFactory
    {
        private const int TextureSize = 128;

        /// <summary>A white disc sprite that is worldDiameter units across.</summary>
        public static Sprite Create(float worldDiameter)
        {
            int size = TextureSize;
            float radius = size / 2f;
            var pixels = new Color32[size * size];
            var inside = new Color32(255, 255, 255, 255);
            var outside = new Color32(255, 255, 255, 0);

            for (int y = 0; y < size; y++)
            {
                float dy = y + 0.5f - radius;
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - radius;
                    pixels[y * size + x] = dx * dx + dy * dy <= radius * radius ? inside : outside;
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size / worldDiameter);
        }
    }
}