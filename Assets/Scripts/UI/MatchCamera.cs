using SoulBind.Entities;
using SoulBind.Managers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace SoulBind.UI
{
    /// <summary>
    /// The match camera, like an auto-battler's: tilted down at the board, close in, following your avatar.
    /// Swipe on the top half of the screen to pan, on the bottom half to turn around the board.
    /// The mouse wheel zooms. Panning snaps back once your avatar moves, so it stays in view.
    /// Values can be tuned on the Main Camera during Play.
    /// </summary>
    [DefaultExecutionOrder(100)] // after MatchController moved the pieces this frame
    public sealed class MatchCamera : MonoBehaviour
    {
        [Header("View")]
        [Tooltip("Degrees away from looking straight down at the board.")]
        [SerializeField, Range(10f, 75f)] private float tilt = 48f;
        [SerializeField, Range(20f, 70f)] private float fieldOfView = 40f;
        [Tooltip("How far the camera sits from the point it looks at, in world units.")]
        [SerializeField, Min(2f)] private float distance = 11f;
        [SerializeField] private Vector2 distanceRange = new Vector2(6f, 18f);
        [Tooltip("How fast the camera catches up with your avatar (higher = snappier).")]
        [SerializeField, Min(0.5f)] private float followSharpness = 6f;

        [Header("Swipes")]
        [Tooltip("Degrees turned per screen pixel swiped on the bottom half.")]
        [SerializeField, Min(0f)] private float turnSpeed = 0.25f;
        [Tooltip("How far you may pan from your avatar, in world units.")]
        [SerializeField, Min(0f)] private float maxPan = 6f;
        [SerializeField, Min(0f)] private float zoomPerScroll = 0.01f;
        [Tooltip("Panning sideways goes the other way: swipe right and the camera moves right.")]
        [SerializeField] private bool invertPanX = true;
        [Tooltip("Turning goes the other way: swipe right and the view turns the other way round.")]
        [SerializeField] private bool invertTurnX;

        private enum Drag { None, Pending, Pan, Turn }

        private Camera _camera;
        private MatchController _controller;
        private Transform _boardCenter;
        private Vector3 _focus;       // the point on the board the camera looks at
        private Vector3 _pan;         // offset from the avatar, on the board's plane
        private float _yaw;           // degrees around the board's normal
        private Vector3 _lastTarget;
        private Drag _drag;
        private bool _dragOnTop;      // the swipe started on the top half
        private Vector2 _pressStart;
        private Vector2 _lastPointer;

        /// <summary>True while a finger or the mouse is panning or turning (so a release is not a tap).</summary>
        public static bool IsDragging { get; private set; }

        /// <summary>Puts the camera on the Main Camera and starts following. Returns null with no camera.</summary>
        public static MatchCamera Create(MatchController controller)
        {
            Camera cam = Camera.main;
            if (cam == null || controller == null)
                return null;
            MatchCamera rig = cam.GetComponent<MatchCamera>();
            if (rig == null)
                rig = cam.gameObject.AddComponent<MatchCamera>();
            rig._camera = cam;
            rig._controller = controller;
            rig._boardCenter = controller.BoardView != null ? controller.BoardView.transform : null;
            rig._focus = rig.GetTarget();
            rig._lastTarget = rig._focus;
            rig.Apply();
            return rig;
        }

        private void OnDisable()
        {
            IsDragging = false;
        }

        private void Update()
        {
            if (_camera == null || _controller == null)
                return;
            ReadSwipes();
            Follow(Time.deltaTime);
            Apply();
        }

        // ---------- Following ----------

        // Your avatar, or the middle of the board while it is not on the board yet.
        private Vector3 GetTarget()
        {
            ApostleView piece = _controller.GetPieceView(_controller.ViewerId);
            if (piece != null && piece.gameObject.activeInHierarchy)
                return piece.transform.position;
            return _boardCenter != null ? _boardCenter.position : Vector3.zero;
        }

        private void Follow(float delta)
        {
            Vector3 target = GetTarget();
            // Your avatar moved: the pan lets go, so the camera brings it back to the middle.
            if ((target - _lastTarget).sqrMagnitude > 0.0001f && _drag != Drag.Pan)
                _pan = Vector3.Lerp(_pan, Vector3.zero, 1f - Mathf.Exp(-followSharpness * delta));
            _lastTarget = target;

            Vector3 goal = target + _pan;
            _focus = Vector3.Lerp(_focus, goal, 1f - Mathf.Exp(-followSharpness * delta));
        }

        // The board lies in the XY plane facing -Z. Yaw turns around Z; tilt looks down from the player's side.
        private void Apply()
        {
            Quaternion rotation = Quaternion.AngleAxis(_yaw, Vector3.forward) * Quaternion.Euler(-tilt, 0f, 0f);
            _camera.orthographic = false;
            _camera.fieldOfView = fieldOfView;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 1000f;
            _camera.transform.rotation = rotation;
            _camera.transform.position = _focus - rotation * Vector3.forward * distance;
        }

        // ---------- Swipes ----------

        private void ReadSwipes()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll != 0f && !IsOverUi())
                    distance = Mathf.Clamp(distance - scroll * zoomPerScroll, distanceRange.x, distanceRange.y);
            }

            Pointer pointer = Pointer.current;
            if (pointer == null)
                return;
            Vector2 point = pointer.position.ReadValue();

            if (pointer.press.wasPressedThisFrame)
            {
                // Swipes never start on cards, buttons or panels.
                _drag = IsOverUi() ? Drag.None : Drag.Pending;
                _dragOnTop = point.y > Screen.height * 0.5f;
                _pressStart = point;
                _lastPointer = point;
            }
            else if (pointer.press.wasReleasedThisFrame || !pointer.press.isPressed)
            {
                _drag = Drag.None;
                IsDragging = false;
                return;
            }

            if (_drag == Drag.None)
                return;
            if (_drag == Drag.Pending)
            {
                // A small wobble is still a tap.
                if ((point - _pressStart).magnitude < DragThreshold())
                    return;
                _drag = _dragOnTop ? Drag.Pan : Drag.Turn;
                IsDragging = true;
                _lastPointer = point;
            }

            Vector2 moved = point - _lastPointer;
            _lastPointer = point;
            if (_drag == Drag.Turn)
            {
                _yaw += (invertTurnX ? -moved.x : moved.x) * turnSpeed;
                return;
            }
            if (invertPanX)
                moved.x = -moved.x;
            PanBy(moved);
        }

        // The board slides with the finger: the point under it stays under it, roughly.
        private void PanBy(Vector2 moved)
        {
            float worldPerPixel = 2f * distance * Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad) / Screen.height;
            Quaternion turn = Quaternion.AngleAxis(_yaw, Vector3.forward);
            Vector3 right = turn * Vector3.right;
            Vector3 away = turn * Vector3.up; // up the screen, along the board
            _pan -= (right * moved.x + away * moved.y) * worldPerPixel;
            _pan = Vector3.ClampMagnitude(_pan, maxPan);
            _focus = _lastTarget + _pan; // panning follows the finger at once
        }

        /// <summary>How far, in screen pixels, a finger may wobble and still count as a tap.</summary>
        public static float TapSlop() => DragThreshold();

        private static float DragThreshold()
        {
            float dpi = Screen.dpi > 0f ? Screen.dpi : 96f;
            return Mathf.Max(10f, dpi * 0.08f);
        }

        private static bool IsOverUi() =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
