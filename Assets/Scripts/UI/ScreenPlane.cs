using UnityEngine;

namespace SoulBind.UI
{
    /// <summary>
    /// Keeps a World Space canvas glued to the camera, just in front of it and facing it, so it sits on the
    /// screen like the HUD while the camera moves and turns. Its code still works in world space (rays,
    /// corners, flights), so nothing else has to change. Positions use a frame 1080 pixels high,
    /// measured from the middle of the screen.
    /// </summary>
    public sealed class ScreenPlane : MonoBehaviour
    {
        private const float FrameHeight = 1080f;
        private const float Distance = 1f; // world units in front of the camera

        private Camera _camera;
        private RectTransform _rect;
        private bool _fill;        // resize to the whole screen (the hands), or keep its own size (a pile)
        private Vector2 _offset;   // frame pixels from the middle of the screen
        private float _size = 1f;  // extra scale on top of one canvas pixel = one frame pixel
        private float _lastFov;
        private float _lastAspect;

        /// <summary>Fills the screen: one canvas pixel is one frame pixel, (0, 0) is the middle.</summary>
        public static ScreenPlane Fill(RectTransform canvasRoot, Camera cam) =>
            Attach(canvasRoot, cam, true, Vector2.zero, 1f);

        /// <summary>Pins it at offset (frame pixels from the middle), drawn size times bigger.</summary>
        public static ScreenPlane Pin(RectTransform canvasRoot, Camera cam, Vector2 offset, float size) =>
            Attach(canvasRoot, cam, false, offset, size);

        private static ScreenPlane Attach(RectTransform canvasRoot, Camera cam, bool fill, Vector2 offset, float size)
        {
            if (canvasRoot == null || cam == null)
                return null;
            ScreenPlane plane = canvasRoot.GetComponent<ScreenPlane>();
            if (plane == null)
                plane = canvasRoot.gameObject.AddComponent<ScreenPlane>();
            plane._camera = cam;
            plane._rect = canvasRoot;
            plane._fill = fill;
            plane._offset = offset;
            plane._size = size;
            canvasRoot.SetParent(cam.transform, false);
            plane._lastFov = -1f;
            plane.Fit();
            return plane;
        }

        /// <summary>The screen's width in frame pixels (the height is always 1080).</summary>
        public float FrameWidth => FrameHeight * (_camera != null ? _camera.aspect : 16f / 9f);

        private void LateUpdate()
        {
            if (_camera == null)
                return;
            if (_camera.fieldOfView != _lastFov || _camera.aspect != _lastAspect)
                Fit();
        }

        // Sizes it so FrameHeight canvas pixels cover the screen's height at Distance.
        private void Fit()
        {
            _lastFov = _camera.fieldOfView;
            _lastAspect = _camera.aspect;
            float unit = 2f * Distance * Mathf.Tan(_lastFov * 0.5f * Mathf.Deg2Rad) / FrameHeight;
            _rect.localRotation = Quaternion.identity;
            _rect.localPosition = new Vector3(_offset.x * unit, _offset.y * unit, Distance);
            _rect.localScale = Vector3.one * (unit * _size);
            if (_fill)
                _rect.sizeDelta = new Vector2(FrameHeight * _lastAspect, FrameHeight);
        }
    }
}
