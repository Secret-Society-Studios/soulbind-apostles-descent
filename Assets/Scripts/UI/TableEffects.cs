using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SoulBind.UI
{
    /// <summary>
    /// Effects on the table, all drawn with one soft glow sprite: smoke left behind by moving pieces,
    /// the strike's crimson slash and impact flash, the slow aura under the Apostle whose turn it is,
    /// a dark pulse at the screen's edges, and a subtle camera shake.
    /// MatchController creates it at runtime. Everything is pooled, so nothing is allocated per frame.
    /// </summary>
    public sealed class TableEffects : MonoBehaviour
    {
        private const int SmokeOrder = 8;   // over the board, under the pieces' shadows (10)
        private const int AuraOrder = 9;
        private const int StrikeOrder = 60; // over the pieces

        private sealed class Effect
        {
            public SpriteRenderer Renderer;
            public Vector3 Position;
            public Vector3 Direction;
            public Color Color;
            public float Age;
            public float Duration;
            public float StartSize;
            public float EndSize;
            public bool IsSlash;
            public bool Active;
        }

        private readonly List<Effect> _effects = new List<Effect>(32);
        private SpriteRenderer _aura;
        private readonly SpriteRenderer[] _defense = new SpriteRenderer[6];
        private readonly SpriteRenderer[] _defenseRing = new SpriteRenderer[6];
        private const int RingOrder = 11; // over a flat disc's edge (10), under standing pieces
        private readonly Color[] _defenseColor = new Color[6];
        private readonly float[] _defenseSize = new float[6];
        private Color _auraColor;
        private float _auraTime;
        private float _auraSize = 1f;

        private float _shakeTime;
        private float _shakeDuration;
        private float _shakeStrength;
        private Vector3 _shakeOffset;
        private Vector3 _shakenPosition;
        private bool _shaken;

        private Image _vignette;
        private float _pulseTime = -1f;
        private float _pulseDuration;
        private float _pulseStrength;

        public static TableEffects Create(Transform parent)
        {
            var go = new GameObject("TableEffects");
            go.transform.SetParent(parent, false);
            TableEffects effects = go.AddComponent<TableEffects>();
            effects.Build();
            return effects;
        }

        private void Build()
        {
            _aura = CreateRenderer("Aura", AuraOrder);
            _aura.gameObject.SetActive(false);

            // The dark pulse: a full-screen vignette under every HUD canvas.
            var canvasObject = new GameObject("DarkPulse", typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -5;
            var imageObject = new GameObject("Vignette", typeof(RectTransform));
            imageObject.transform.SetParent(canvasObject.transform, false);
            var rect = (RectTransform)imageObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _vignette = imageObject.AddComponent<Image>();
            _vignette.sprite = MotionStyle.Vignette;
            _vignette.raycastTarget = false;
            _vignette.color = new Color(0f, 0f, 0f, 0f);
            _vignette.enabled = false;
        }

        // ---- What other scripts call ----

        /// <summary>A puff of dark smoke that spreads and fades, left behind a moving piece.</summary>
        public void Smoke(Vector3 position, float size)
        {
            Effect effect = Take(SmokeOrder);
            Begin(effect, position, Vector3.zero, new Color(0.05f, 0.04f, 0.07f, 0.45f),
                MotionStyle.Seconds(0.9f), size * 0.6f, size * 1.4f, false);
        }

        /// <summary>The strike landing on a target: a crimson slash across it and a flash of light.</summary>
        public void Strike(Vector3 target, Vector3 direction, float size)
        {
            Vector3 across = new Vector3(-direction.y, direction.x, 0f);
            if (across.sqrMagnitude < 0.0001f)
                across = Vector3.right;
            across.Normalize();
            // The slash runs diagonally across the line of attack.
            Vector3 slashDirection = (across + direction.normalized * 0.6f).normalized;

            Effect slash = Take(StrikeOrder + 1);
            Begin(slash, target, slashDirection, new Color(0.85f, 0.08f, 0.12f, 1f), MotionStyle.Seconds(0.45f),
                0f, size * 2.2f, true);

            Effect flash = Take(StrikeOrder);
            Begin(flash, target, Vector3.zero, new Color(0.75f, 0.10f, 0.12f, 0.75f), MotionStyle.Seconds(0.55f),
                size * 0.4f, size * 1.8f, false);
        }

        /// <summary>A soft glow, e.g. in an element's colour where something lands.</summary>
        public void Glow(Vector3 position, Color color, float size)
        {
            Effect effect = Take(StrikeOrder);
            Begin(effect, position, Vector3.zero, color, MotionStyle.Seconds(0.6f), size * 0.5f, size * 1.6f, false);
        }

        /// <summary>The slow pulsing glow under the Apostle whose turn it is. Call every frame while it moves.</summary>
        public void SetAura(bool visible, Vector3 position, Color color, float size)
        {
            _aura.gameObject.SetActive(visible);
            if (!visible)
                return;
            _aura.transform.position = position + new Vector3(0f, 0f, -0.01f);
            _auraColor = color;
            _auraSize = size;
        }

        /// <summary>
        /// A steady blue glow under a piece while its Apostle has DEF (index = the piece's slot, 0 to 5).
        /// Call every frame with the piece's position, so it follows the piece as it moves.
        /// </summary>
        public void SetDefenseGlow(int index, bool visible, Vector3 position, Color color, float size)
        {
            if (index < 0 || index >= _defense.Length)
                return;
            if (_defense[index] == null)
            {
                if (!visible)
                    return;
                _defense[index] = CreateRenderer("DefenseGlow", AuraOrder);
                _defenseRing[index] = CreateRenderer("DefenseRing", RingOrder);
                _defenseRing[index].sprite = MotionStyle.Ring;
            }
            SpriteRenderer glow = _defense[index];
            SpriteRenderer ring = _defenseRing[index];
            if (glow.gameObject.activeSelf != visible)
            {
                glow.gameObject.SetActive(visible);
                ring.gameObject.SetActive(visible);
            }
            if (!visible)
                return;
            glow.transform.position = position + new Vector3(0f, 0f, -0.01f);
            ring.transform.position = position + new Vector3(0f, 0f, -0.015f);
            _defenseColor[index] = color;
            _defenseSize[index] = size;
        }

        /// <summary>The edges of the screen darken briefly.</summary>
        public void DarkPulse(float strength)
        {
            _pulseTime = 0f;
            _pulseDuration = MotionStyle.Seconds(0.7f);
            _pulseStrength = Mathf.Clamp01(strength);
            _vignette.enabled = true;
        }

        /// <summary>A short, subtle camera shake (if screen shake is on). strength is in world units.</summary>
        public void Shake(float strength, float duration)
        {
            if (!MotionStyle.ScreenShake)
                return;
            _shakeStrength = strength;
            _shakeDuration = MotionStyle.Seconds(duration);
            _shakeTime = 0f;
        }

        // ---- Running the effects ----

        private void Update()
        {
            float delta = Time.deltaTime;
            for (int i = 0; i < _effects.Count; i++)
            {
                Effect effect = _effects[i];
                if (!effect.Active)
                    continue;
                effect.Age += delta;
                float t = effect.Age / effect.Duration;
                if (t >= 1f)
                {
                    effect.Active = false;
                    effect.Renderer.gameObject.SetActive(false);
                    continue;
                }
                Place(effect, t);
            }

            _auraTime += delta * Mathf.Max(0.1f, MotionStyle.Speed);
            if (_aura.gameObject.activeSelf)
            {
                float wave = (Mathf.Sin(_auraTime * Mathf.PI * 2f / 2.4f) + 1f) / 2f; // one breath every 2.4 s
                Color color = _auraColor;
                color.a = Mathf.Lerp(0.35f, 0.7f, wave);
                _aura.color = color;
                _aura.transform.localScale = Vector3.one * (_auraSize * Mathf.Lerp(0.95f, 1.1f, wave));
            }

            // The DEF glows shimmer faintly, slower than the turn aura.
            float shimmer = (Mathf.Sin(_auraTime * Mathf.PI * 2f / 3.7f) + 1f) / 2f;
            for (int i = 0; i < _defense.Length; i++)
            {
                SpriteRenderer glow = _defense[i];
                if (glow == null || !glow.gameObject.activeSelf)
                    continue;
                // A wide soft glow, plus a bright ring hugging the piece's base, so it reads at a glance.
                Color color = _defenseColor[i];
                color.a = 1f;
                glow.color = color;
                glow.transform.localScale = Vector3.one * (_defenseSize[i] * 1.35f * Mathf.Lerp(0.97f, 1.06f, shimmer));
                SpriteRenderer ring = _defenseRing[i];
                Color ringColor = Color.Lerp(color, Color.white, 0.35f);
                ringColor.a = Mathf.Lerp(0.85f, 1f, shimmer);
                ring.color = ringColor;
                ring.transform.localScale = Vector3.one * (_defenseSize[i] * 0.95f);
            }

            if (_pulseTime >= 0f)
            {
                _pulseTime += delta;
                float t = _pulseTime / _pulseDuration;
                if (t >= 1f)
                {
                    _pulseTime = -1f;
                    _vignette.enabled = false;
                }
                else
                {
                    // Darkens quickly, lifts slowly.
                    float alpha = t < 0.2f ? MotionStyle.Out(t / 0.2f) : 1f - MotionStyle.InOut((t - 0.2f) / 0.8f);
                    _vignette.color = new Color(0f, 0f, 0f, alpha * _pulseStrength);
                }
            }
        }

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;
            Transform camTransform = cam.transform;

            // Take back last frame's offset, unless something else moved the camera since (then its new
            // position is the real one).
            if (_shaken && camTransform.position == _shakenPosition)
                camTransform.position -= _shakeOffset;
            _shaken = false;
            _shakeOffset = Vector3.zero;

            if (_shakeTime >= _shakeDuration)
                return;
            _shakeTime += Time.deltaTime;
            float fade = 1f - Mathf.Clamp01(_shakeTime / _shakeDuration);
            float strength = _shakeStrength * fade * fade;
            // Smooth noise rather than random jumps: a heavy shudder, not a rattle.
            float time = _shakeTime * 28f;
            _shakeOffset = camTransform.right * ((Mathf.PerlinNoise(time, 0.3f) - 0.5f) * 2f * strength)
                           + camTransform.up * ((Mathf.PerlinNoise(0.7f, time) - 0.5f) * 2f * strength);
            camTransform.position += _shakeOffset;
            _shakenPosition = camTransform.position;
            _shaken = true;
        }

        private void Place(Effect effect, float t)
        {
            Transform effectTransform = effect.Renderer.transform;
            Color color = effect.Color;
            if (effect.IsSlash)
            {
                // Cuts across fast, then thins and fades.
                float cut = MotionStyle.Out(Mathf.Clamp01(t / 0.3f));
                float thin = 1f - MotionStyle.InOut(t);
                effectTransform.localScale = new Vector3(effect.EndSize * cut, effect.EndSize * 0.07f * thin + 0.01f, 1f);
                color.a *= 1f - MotionStyle.In(t);
            }
            else
            {
                float size = Mathf.Lerp(effect.StartSize, effect.EndSize, MotionStyle.Out(t));
                effectTransform.localScale = new Vector3(size, size, 1f);
                color.a *= 1f - MotionStyle.InOut(t);
            }
            effect.Renderer.color = color;
        }

        private void Begin(Effect effect, Vector3 position, Vector3 direction, Color color, float duration,
            float startSize, float endSize, bool isSlash)
        {
            effect.Position = position;
            effect.Direction = direction;
            effect.Color = color;
            effect.Age = 0f;
            effect.Duration = Mathf.Max(0.05f, duration);
            effect.StartSize = startSize;
            effect.EndSize = endSize;
            effect.IsSlash = isSlash;
            effect.Active = true;

            Transform effectTransform = effect.Renderer.transform;
            effectTransform.position = position + new Vector3(0f, 0f, effect.Renderer.sortingOrder >= StrikeOrder ? -0.6f : -0.02f);
            float angle = direction.sqrMagnitude > 0f ? Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg : 0f;
            effectTransform.rotation = Quaternion.Euler(0f, 0f, angle);
            effect.Renderer.gameObject.SetActive(true);
            Place(effect, 0f);
        }

        private Effect Take(int sortingOrder)
        {
            Effect effect = null;
            for (int i = 0; i < _effects.Count; i++)
            {
                if (!_effects[i].Active)
                {
                    effect = _effects[i];
                    break;
                }
            }
            if (effect == null)
            {
                effect = new Effect { Renderer = CreateRenderer("Effect", sortingOrder) };
                _effects.Add(effect);
            }
            effect.Renderer.sortingOrder = sortingOrder;
            return effect;
        }

        private SpriteRenderer CreateRenderer(string name, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            SpriteRenderer spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = MotionStyle.Glow;
            spriteRenderer.sortingOrder = sortingOrder;
            go.SetActive(false);
            return spriteRenderer;
        }
    }
}