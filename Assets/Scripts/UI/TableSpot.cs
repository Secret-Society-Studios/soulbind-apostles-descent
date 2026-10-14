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

        /// <summary>
        /// Also gives how far the card looks turned on screen (degrees, 0 = upright), for cards lying along
        /// the sides of the hexagonal mat.
        /// </summary>
        public static bool TryGet(RectTransform slot, Camera camera, Vector3[] corners, out Vector2 center,
            out float cardPixelHeight, out float screenAngle)
        {
            screenAngle = 0f;
            if (!TryGet(slot, camera, corners, out center, out cardPixelHeight))
                return false;

            Vector3 bottom = camera.WorldToScreenPoint((corners[0] + corners[3]) * 0.5f);
            Vector3 top = camera.WorldToScreenPoint((corners[1] + corners[2]) * 0.5f);
            Vector2 up = (Vector2)(top - bottom);
            screenAngle = Mathf.Atan2(up.y, up.x) * Mathf.Rad2Deg - 90f;
            return true;
        }
    }
}