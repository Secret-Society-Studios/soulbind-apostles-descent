using System.Collections.Generic;
using UnityEngine;

namespace SoulBind.UI
{
    /// <summary>
    /// Plays FxLibrary animations on the board: each stands on a world point, faces the camera like the
    /// avatars, plays once and goes away. A sprite's pivot is the spot that sits on that point.
    /// MatchController creates it at runtime. Renderers are pooled, so playing one allocates nothing.
    /// </summary>
    public sealed class FrameFx : MonoBehaviour
    {
        /// <summary>Over the pieces.</summary>
        public const int OverPieces = 60;

        /// <summary>Over the tiles, under the pieces (the discs are 10, the avatars 11).</summary>
        public const int UnderPieces = 9;

        private sealed class Running
        {
            public SpriteRenderer Renderer;
            public FrameClip Clip;
            public Transform Follow; // it stays on this while it plays; null = it stays where it started
            public float Age;
            public bool Active;
        }

        private readonly List<Running> _running = new List<Running>(6);

        /// <summary>Creates the player under parent.</summary>
        public static FrameFx Create(Transform parent)
        {
            var go = new GameObject("FrameFx");
            go.transform.SetParent(parent, false);
            return go.AddComponent<FrameFx>();
        }

        /// <summary>
        /// Plays a clip once on a world point, its frames this tall in world units. Given follow, it stays
        /// on that object while it plays. An empty clip does nothing.
        /// </summary>
        public void Play(FrameClip clip, Vector3 position, float height, int sortingOrder = OverPieces,
            Transform follow = null)
        {
            Play(clip, position, height, Color.white, sortingOrder, follow);
        }

        /// <summary>The same, tinted: for grey art that takes an Apostle's colour.</summary>
        public void Play(FrameClip clip, Vector3 position, float height, Color tint,
            int sortingOrder = OverPieces, Transform follow = null)
        {
            if (!clip.HasFrames || clip.Fps <= 0f)
                return;

            Running effect = FindFree();
            effect.Clip = clip;
            effect.Follow = follow;
            effect.Age = 0f;
            effect.Active = true;

            SpriteRenderer spriteRenderer = effect.Renderer;
            spriteRenderer.sprite = clip.Frames[0];
            spriteRenderer.color = tint;
            spriteRenderer.sortingOrder = sortingOrder;
            spriteRenderer.transform.position = position;
            spriteRenderer.transform.localScale =
                Vector3.one * (height / Mathf.Max(0.0001f, clip.Frames[0].bounds.size.y));
            spriteRenderer.gameObject.SetActive(true);
            FaceCamera(spriteRenderer.transform);
        }

        // A renderer that is not in use, or a new one.
        private Running FindFree()
        {
            for (int i = 0; i < _running.Count; i++)
            {
                if (!_running[i].Active)
                    return _running[i];
            }

            var go = new GameObject("Frame Effect");
            go.transform.SetParent(transform, false);
            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            var effect = new Running { Renderer = spriteRenderer };
            _running.Add(effect);
            return effect;
        }

        private void LateUpdate()
        {
            for (int i = 0; i < _running.Count; i++)
            {
                Running effect = _running[i];
                if (!effect.Active)
                    continue;

                effect.Age += Time.deltaTime;
                int frame = (int)(effect.Age * effect.Clip.Fps);
                if (frame >= effect.Clip.Frames.Length)
                {
                    effect.Active = false;
                    effect.Renderer.gameObject.SetActive(false);
                    continue;
                }
                effect.Renderer.sprite = effect.Clip.Frames[frame];
                if (effect.Follow != null)
                    effect.Renderer.transform.position = effect.Follow.position;
                FaceCamera(effect.Renderer.transform);
            }
        }

        private static void FaceCamera(Transform target)
        {
            Camera cam = Camera.main;
            if (cam != null)
                target.rotation = cam.transform.rotation;
        }
    }
}
