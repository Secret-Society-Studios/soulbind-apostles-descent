using SoulBind.UI;
using UnityEngine;

namespace SoulBind.Entities
{
    /// <summary>
    /// Plays the avatar's frames on one Apostle's piece. Walking is worked out from how the piece
    /// moves, so it needs no calls; attacks and hits are started by ApostleView.
    /// The sprite always faces the camera.
    /// </summary>
    public sealed class AvatarAnimator : MonoBehaviour
    {
        private enum Clip { Idle, WalkUp, WalkDown, WalkLeft, WalkRight, Attack, Damaged }

        private const float MinSpeed = 0.05f;   // world units per second that count as walking
        private const float StopDelay = 0.12f;  // seconds still before going back to idle

        private SpriteRenderer _renderer;
        private AvatarSkin _skin;
        private Sprite[] _frames;
        private float _fps;
        private Clip _clip;
        private bool _loop;
        private float _time;      // seconds into the current clip
        private float _stillTime; // seconds since the piece last moved
        private Vector3 _lastPosition;

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

        // Adds the renderer and starts the idle clip.
        private void Build(AvatarSkin skin, Color color, int sortingOrder)
        {
            _skin = skin;
            _renderer = gameObject.AddComponent<SpriteRenderer>();
            _renderer.sortingOrder = sortingOrder;
            _renderer.color = Color.Lerp(Color.white, color, skin.Tint);

            // The frames import with Point filter (hard pixels), set in their .meta files.
            Sprite first = skin.Idle[0];
            // Scale so the frame is Height world units tall.
            float frameHeight = Mathf.Max(0.0001f, first.bounds.size.y);
            transform.localScale = Vector3.one * (skin.Height / frameHeight);

            _lastPosition = transform.position;
            Play(Clip.Idle, skin.Idle, skin.IdleFps, true);
            FaceCamera();
        }

        // ---------- Called by ApostleView ----------

        /// <summary>Swings towards the target: right if directionX is 0 or more, else left.</summary>
        public void PlayAttack(float directionX)
        {
            Sprite[] frames = directionX >= 0f ? _skin.AttackRight : _skin.AttackLeft;
            Play(Clip.Attack, frames, _skin.AttackFps, false);
        }

        /// <summary>Flinches facing the attacker: right if the hit came from the right.</summary>
        public void PlayDamaged(bool fromRight)
        {
            Sprite[] frames = fromRight ? _skin.DamagedRight : _skin.DamagedLeft;
            Play(Clip.Damaged, frames, _skin.DamagedFps, false);
        }

        // ---------- Every frame ----------

        private void Update()
        {
            if (_frames == null)
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
                    PlayIdle();
            }
            else if (delta > 0f && moved.magnitude / delta >= MinSpeed)
            {
                _stillTime = 0f;
                PlayWalk(moved);
            }
            else if (_clip != Clip.Idle)
            {
                _stillTime += delta;
                if (_stillTime >= StopDelay)
                    PlayIdle();
            }

            ShowFrame();
        }

        private void LateUpdate()
        {
            FaceCamera();
        }

        // The board lies in the XY plane: +X is right on screen, +Y is away from the camera.
        private void PlayWalk(Vector3 moved)
        {
            Clip clip;
            if (Mathf.Abs(moved.x) >= Mathf.Abs(moved.y))
                clip = moved.x >= 0f ? Clip.WalkRight : Clip.WalkLeft;
            else
                clip = moved.y >= 0f ? Clip.WalkUp : Clip.WalkDown;
            if (clip == _clip)
                return;

            Sprite[] frames = clip switch
            {
                Clip.WalkUp => _skin.WalkUp,
                Clip.WalkDown => _skin.WalkDown,
                Clip.WalkLeft => _skin.WalkLeft,
                _ => _skin.WalkRight
            };
            Play(clip, frames, _skin.WalkFps, true);
        }

        // Goes back to the looping idle clip.
        private void PlayIdle() => Play(Clip.Idle, _skin.Idle, _skin.IdleFps, true);

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
            int index = (int)(_time * _fps);
            index = _loop ? index % _frames.Length : Mathf.Min(index, _frames.Length - 1);
            Sprite sprite = _frames[index];
            if (_renderer.sprite != sprite)
                _renderer.sprite = sprite;
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
