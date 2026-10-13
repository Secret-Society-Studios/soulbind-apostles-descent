using UnityEngine;

namespace SoulBind.Utility
{
    /// <summary>One color per Apostle, shared by the board discs and the HUD panels.</summary>
    public static class ApostleColors
    {
        private static readonly Color[] Colors =
        {
            new Color(0.85f, 0.25f, 0.25f),
            new Color(0.25f, 0.50f, 0.95f),
            new Color(0.30f, 0.80f, 0.40f),
            new Color(0.95f, 0.80f, 0.20f),
            new Color(0.70f, 0.40f, 0.90f),
            new Color(0.95f, 0.55f, 0.20f)
        };

        /// <summary>The color of the Apostle with this id (ids start at 1).</summary>
        public static Color Get(int apostleId) => Colors[(apostleId - 1 + Colors.Length) % Colors.Length];
    }
}