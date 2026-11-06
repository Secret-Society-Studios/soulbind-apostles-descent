using SoulBind.UI;
using UnityEngine;

namespace SoulBind.Entities
{
    /// <summary>
    /// Plays the avatar's frames on one Apostle's piece. Walking is worked out from how the piece
    /// moves, so it needs no calls; attacks and hits are started by ApostleView.
    /// The sprite always faces the camera, and left, right, up and down always mean as seen on screen,
    /// wherever the camera has turned. During its turn the avatar keeps facing the way it last walked.
    /// </summary>
    public sealed class AvatarAnimator : MonoBehaviour
    {
        private enum Clip { Idle, WalkUp, WalkDown, WalkLeft, WalkRight, Attack, Damaged }

        private const float MinSpeed = 0.05f;   // world units per second that count as walking
        private const float StopDelay = 0.12f;  // seconds still before going back to idle
        private static readonly Color FrozenTint = new Color(0.62f, 0.5f, 0.95f); // over its colour in stopped time
        private const float BreathHeight = 0.03f;  // how much taller it gets at the top of a breath, as a share
        private const float BreathSeconds = 1.8f;  // one breath: the same length as the idle clip

        private SpriteRenderer _renderer;
        private AvatarSkin _skin;
        private Sprite[] _frames;
        private float _fps;
        private Clip _clip;
        private bool _loop;
        private float _time;      // seconds into the current clip
        private float _stillTime; // seconds since the piece last moved
        private Vector3 _lastPosition;
        private float _frameHeight; // of a frame, before scaling; the frames stand on their bottom edge
        private bool _holdsPosture; // its turn: it keeps facing the way it walked
        private bool _hasFacing;
        private Vector3 _facing;    // the world direction it last walked this turn
        private bool _holding;      // standing still on a walk clip's first frame
        private Vector3 _restScale; // the avatar's scale, before any breathing
        private bool _corrupted;    // shows the Corrupted frames
        private bool _frozen;       // caught in The World's stopped time
        private Color _color;       // the renderer's colour outside stopped time
        private bool _looksRight = true; // which way a Corrupted idle faces: the side it last walked towards

        /// <summary>Builds the avatar under parent, standing on its origin. Returns null without frames.</summary>
        public static AvatarAnimator Create(Transform parent, AvatarSkin skin, Color color, int sortingOrder)
        {
            if (skin == null || !skin.HasFrames)
                return null;

            var go = new GameObject("Avatar");
            go.transform.SetParent(parent, false);
            var animator = go.AddComponent<AvatarAnimator>();
            animator.Build(skin, color, sortingOrder);
            return animator;
        }

        public Renderer Renderer => _renderer;

        /// <summary>The top of the frame in world space. The avatar leans back to face the camera.</summary>
        public Vector3 TopPoint => transform.TransformPoint(0f, _frameHeight, 0f);

        // Adds the renderer and starts the idle clip.
        private void Build(AvatarSkin skin, Color color, int sortingOrder)
        {
            _skin = skin;
            _renderer = gameObject.AddComponent<SpriteRenderer>();
            _renderer.sortingOrder = sortingOrder;
            _color = skin.Tinted(color);
            _renderer.color = _color;

            // The frames import with Point filter (hard pixels), set in their .meta files.
            Sprite first = skin.Idle[0];
            // Scale so the frame is Height world units tall.
            _frameHeight = Mathf.Max(0.0001f, first.bounds.size.y);
            _restScale = Vector3.one * (skin.Height / _frameHeight);
            transform.localScale = _restScale;

            _lastPosition = transform.position;
            Play(Clip.Idle, skin.Idle, skin.IdleFps, true);
            FaceCamera();
        }

        // ---------- Called by ApostleView ----------

        /// <summary>Swings towards a world direction, to that side of the screen.</summary>
        public void PlayAttack(Vector3 direction)
        {
            // No Corrupted attack art yet: its walk stands in, so it never turns back into the normal avatar.
            _looksRight = IsToTheRight(direction);
            Sprite[] frames = _looksRight
                ? Look(_skin.AttackRight, _skin.CorruptedWalkRight)
                : Look(_skin.AttackLeft, _skin.CorruptedWalkLeft);
            Play(Clip.Attack, frames, _skin.AttackFps, false);
        }

        /// <summary>Flinches facing the attacker, given the world direction towards them.</summary>
        public void PlayDamaged(Vector3 towardAttacker)
        {
            Sprite[] frames = IsToTheRight(towardAttacker)
                ? Look(_skin.DamagedRight, _skin.CorruptedDamagedRight)
                : Look(_skin.DamagedLeft, _skin.CorruptedDamagedLeft);
            Play(Clip.Damaged, frames, _skin.DamagedFps, false);
        }

        /// <summary>Holds the current frame, for The World's stopped time.</summary>
        public void SetFrozen(bool frozen)
        {
            _frozen = frozen;
            _renderer.color = frozen ? _color * FrozenTint : _color;
        }

        /// <summary>Swaps between the normal and the Corrupted frames, in step with whatever it is doing.</summary>
        public void SetCorrupted(bool corrupted)
        {
            if (_corrupted == corrupted)
                return;
            _corrupted = corrupted;

            // An attack or a hit finishes first; PlayRest then picks up the new look.
            if (_clip == Clip.Attack || _clip == Clip.Damaged)
                return;
            if (_clip == Clip.Idle)
                PlayIdle();
            else
                PlayWalkClip(_clip);
        }

        /// <summary>On for its turn: it keeps its walking direction. Off: back to the default posture.</summary>
        public void SetHoldsPosture(bool holds)
        {
            if (_holdsPosture == holds)
                return;
            _holdsPosture = holds;
            if (holds)
                return;
            _hasFacing = false;
            if (_holding)
            {
                _holding = false;
                PlayIdle();
            }
        }

        // ---------- Every frame ----------

        private void Update()
        {
            if (_frames == null || _frozen)
                return;
            float delta = Time.deltaTime;
            _time += delta;

            Vector3 position = transform.position;
            Vector3 moved = position - _lastPosition;
            _lastPosition = position;
            bool busy = _clip == Clip.Attack || _clip == Clip.Damaged;

            if (busy)
            {
                // An attack or a hit plays once, then the avatar goes back to idle.
                if (_time * _fps >= _frames.Length)
                    PlayRest();
            }
            else if (delta > 0f && moved.magnitude / delta >= MinSpeed)
            {
                _stillTime = 0f;
                _holding = false;
                if (_holdsPosture)
                {
                    _facing = moved;
                    _hasFacing = true;
                }
                _looksRight = IsToTheRight(moved);
                PlayWalk(moved);
            }
            else if (_holding)
            {
                HoldFacing(); // the camera may have turned
            }
            else if (_clip != Clip.Idle)
            {
                _stillTime += delta;
                if (_stillTime >= StopDelay)
                    PlayRest();
            }

            ShowFrame();
            Breathe();
        }

        private void LateUpdate()
        {
            FaceCamera();
        }

        // There is no standing art for each direction, so an avatar holding its facing shows one walk
        // frame. A slow rise and fall keeps it alive; it stands on its bottom edge, so its feet stay put.
        private void Breathe()
        {
            Vector3 scale = _restScale;
            if (_holding)
                scale.y *= 1f + BreathHeight * (Mathf.Sin(Time.time * Mathf.PI * 2f / BreathSeconds) + 1f) / 2f;
            if (transform.localScale != scale)
                transform.localScale = scale;
        }

        // Walks the way it is moving on screen.
        private void PlayWalk(Vector3 moved)
        {
            Clip clip = WalkClipFor(moved);
            if (clip != _clip)
                PlayWalkClip(clip);
        }

        // Which walk clip a world direction looks like on screen right now.
        private static Clip WalkClipFor(Vector3 direction)
        {
            Camera cam = Camera.main;
            float across = Vector3.Dot(direction, cam != null ? cam.transform.right : Vector3.right);
            float up = Vector3.Dot(direction, cam != null ? cam.transform.up : Vector3.up);
            if (Mathf.Abs(across) >= Mathf.Abs(up))
                return across >= 0f ? Clip.WalkRight : Clip.WalkLeft;
            return up >= 0f ? Clip.WalkUp : Clip.WalkDown;
        }

        // True when a world direction points to the right of the screen.
        private static bool IsToTheRight(Vector3 direction)
        {
            Camera cam = Camera.main;
            return Vector3.Dot(direction, cam != null ? cam.transform.right : Vector3.right) >= 0f;
        }

        // Stops walking: it keeps facing that way during its turn, otherwise it goes back to idle.
        private void PlayRest()
        {
            // Corrupted frames have an idle for each side, so nothing needs holding there.
            if (!_hasFacing || HasCorruptedIdleFor(WalkClipFor(_facing)))
            {
                PlayIdle();
                return;
            }
            _holding = true;
            HoldFacing();
        }

        // Stands on the walk clip that matches its facing from where the camera is now.
        private void HoldFacing()
        {
            Clip clip = WalkClipFor(_facing);
            if (clip != _clip)
                PlayWalkClip(clip);
        }

        // Starts one of the four walk clips.
        private void PlayWalkClip(Clip clip)
        {
            Sprite[] frames = clip switch
            {
                Clip.WalkUp => Look(_skin.WalkUp, _skin.CorruptedWalkUp),
                Clip.WalkDown => Look(_skin.WalkDown, _skin.CorruptedWalkDown),
                Clip.WalkLeft => Look(_skin.WalkLeft, _skin.CorruptedWalkLeft),
                _ => Look(_skin.WalkRight, _skin.CorruptedWalkRight)
            };
            Play(clip, frames, _skin.WalkFps, true);
        }

        // Goes back to the looping idle clip.
        private void PlayIdle()
        {
            Sprite[] corrupted = _looksRight ? _skin.CorruptedIdleRight : _skin.CorruptedIdleLeft;
            Play(Clip.Idle, Look(_skin.Idle, corrupted), _skin.IdleFps, true);
        }

        // The Corrupted frames while Corrupted, unless that clip has none.
        private Sprite[] Look(Sprite[] normal, Sprite[] corrupted) =>
            _corrupted && corrupted != null && corrupted.Length > 0 ? corrupted : normal;

        // True when a Corrupted avatar facing this way has its own idle to play.
        private bool HasCorruptedIdleFor(Clip walk)
        {
            if (!_corrupted)
                return false;
            if (walk == Clip.WalkLeft)
                return _skin.CorruptedIdleLeft.Length > 0;
            return walk == Clip.WalkRight && _skin.CorruptedIdleRight.Length > 0;
        }

        // An empty clip (art not in yet) falls back to idle, so nothing ever disappears.
        private void Play(Clip clip, Sprite[] frames, float fps, bool loop)
        {
            if (frames == null || frames.Length == 0)
            {
                clip = Clip.Idle;
                frames = _skin.Idle;
                fps = _skin.IdleFps;
                loop = true;
            }
            _clip = clip;
            _frames = frames;
            _fps = fps;
            _loop = loop;
            _time = 0f;
            ShowFrame();
        }

        // Shows the frame for the current clip time, only touching the renderer when it changes.
        private void ShowFrame()
        {
            int index = _holding ? 0 : (int)(_time * _fps);
            index = _loop ? index % _frames.Length : Mathf.Min(index, _frames.Length - 1);
            Sprite sprite = _frames[index];
            if (_renderer.sprite != sprite)
                _renderer.sprite = sprite;

            // The Corrupted up and down art leans left, so it is mirrored for going right. On a hex
            // board most steps to the right are diagonal, and those use the up and down clips.
            bool flip = _corrupted && _looksRight && (_clip == Clip.WalkUp || _clip == Clip.WalkDown);
            if (_renderer.flipX != flip)
                _renderer.flipX = flip;
        }

        // A flat sprite turned to the camera reads as standing up on the tilted board.
        private void FaceCamera()
        {
            Camera cam = Camera.main;
            if (cam != null)
                transform.rotation = cam.transform.rotation;
        }
    }
}
