using UnityEngine;

namespace SoulBind.UI
{
    /// <summary>
    /// Moves the match HUD in from the screen's edges once, at the start: past a camera notch or a
    /// rounded corner (the safe area), plus a margin on phones. Anything pinned to an edge or a corner
    /// moves in; anything stretched across the screen is looked inside instead. On a PC with no notch
    /// and no margin nothing moves.
    /// </summary>
    public static class SafeHud
    {
        private const int MaxDepth = 3; // stretched layers looked inside (canvas > HUD > overlay > button)

        /// <summary>Insets every edge-pinned piece under root. margin is in canvas pixels.</summary>
        public static void Apply(RectTransform root, Vector2 margin)
        {
            if (root == null || Screen.width <= 0 || Screen.height <= 0)
                return;
            Rect safe = Screen.safeArea;
            Vector2 size = root.rect.size;
            float perPixelX = size.x / Screen.width;
            float perPixelY = size.y / Screen.height;

            // Each side in canvas pixels: x = left, y = bottom, z = right, w = top.
            var inset = new Vector4(
                safe.xMin * perPixelX + margin.x,
                safe.yMin * perPixelY + margin.y,
                (Screen.width - safe.xMax) * perPixelX + margin.x,
                (Screen.height - safe.yMax) * perPixelY + margin.y);
            if (inset.sqrMagnitude < 0.01f)
                return;
            Walk(root, inset, 0);
        }

        // Pushes every edge-anchored child in from the screen edge, a few levels deep.
        private static void Walk(RectTransform parent, Vector4 inset, int depth)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                if (!(parent.GetChild(i) is RectTransform child))
                    continue;
                bool pointX = Mathf.Approximately(child.anchorMin.x, child.anchorMax.x);
                bool pointY = Mathf.Approximately(child.anchorMin.y, child.anchorMax.y);
                if (!pointX && !pointY)
                {
                    // Stretched over the screen (a layer, a dim backdrop): its own pieces may be pinned.
                    if (depth < MaxDepth)
                        Walk(child, inset, depth + 1);
                    continue;
                }
                child.anchoredPosition += Shift(child, pointX, pointY, inset);
            }
        }

        // Left or right, top or bottom, by where the piece is anchored. The middle stays put.
        private static Vector2 Shift(RectTransform child, bool pointX, bool pointY, Vector4 inset)
        {
            Vector2 shift = Vector2.zero;
            if (pointX && child.anchorMin.x <= 0.01f)
                shift.x = inset.x;
            else if (pointX && child.anchorMin.x >= 0.99f)
                shift.x = -inset.z;
            if (pointY && child.anchorMin.y <= 0.01f)
                shift.y = inset.y;
            else if (pointY && child.anchorMin.y >= 0.99f)
                shift.y = -inset.w;
            return shift;
        }
    }
}
