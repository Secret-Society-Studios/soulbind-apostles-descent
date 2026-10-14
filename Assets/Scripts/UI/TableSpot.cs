using UnityEngine;

namespace SoulBind.UI
{
    /// <summary>Finds where a card lying on the table appears on screen, for cards flying to or from it.</summary>
    public static class TableSpot
    {
        /// <summary>
        /// The screen position of the slot's center, and the on-screen height of the slot (a card's height
        /// there). corners is a reused 4-element array, so nothing is allocated.
        /// </summary>
        public static bool TryGet(RectTransform slot, Camera camera, Vector3[] corners, out Vector2 center,
            out float cardPixelHeight)
        {
            center = default;
            cardPixelHeight = 0f;
            if (slot == null || camera == null)
                return false;

            slot.GetWorldCorners(corners); // bottom-left, top-left, top-right, bottom-right
            Vector3 bottom = camera.WorldToScreenPoint((corners[0] + corners[3]) * 0.5f);
            Vector3 top = camera.WorldToScreenPoint((corners[1] + corners[2]) * 0.5f);
            Vector3 middle = camera.WorldToScreenPoint(slot.position);
            if (bottom.z <= 0f || top.z <= 0f || middle.z <= 0f)
                return false; // behind the camera

            center = middle;
            cardPixelHeight = Vector2.Distance(bottom, top);
            return true;
        }
    }
}