using SoulBind.Managers;
using UnityEngine;
using UnityEngine.Rendering;

namespace SoulBind.UI
{
    /// <summary>
    /// The World's stopped time: a wave spreads from the Apostle who played it, cycling through colours,
    /// and leaves the map a tinted negative that holds still until their extra turn ends, when a second
    /// wave brings it back. It draws in the scene just above the map, so the tiles, pieces, table cards and
    /// HUD all stay in their own colours. On Low graphics the colour simply fades in, with no wave.
    /// MatchController creates it at runtime and tells it who stopped time.
    /// </summary>
    public sealed class TimeStopEffect : MonoBehaviour
    {
        private const int NegativeOrder = -5;      // over the island (-30) and edge rocks (-28), under the tiles
        private const int ShadowOrder = -4;
        private const float WaveSeconds = 1.3f;
        private const float FreezeShare = 0.5f;    // how far through a wave things freeze or start again
        private const float RingFadeFrom = 0.7f;   // the ring fades out from this share of the wave's time
        private const float QuadDistance = 1f;     // in front of the camera; drawn on top by sorting anyway
        private const float QuadOverscan = 1.2f;   // a little past the screen's edges

        // The colours the wave flashes through before it settles on the skin's colour.
        private static readonly Color FlashMagenta = new Color(1f, 0.15f, 0.75f);
        private static readonly Color FlashGreen = new Color(0.25f, 1f, 0.55f);

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int CenterId = Shader.PropertyToID("_Center");
        private static readonly int RadiusId = Shader.PropertyToID("_Radius");
        private static readonly int RingGlowId = Shader.PropertyToID("_RingGlow");
        private static readonly int ReverseId = Shader.PropertyToID("_Reverse");
        private static readonly int StrengthId = Shader.PropertyToID("_Strength");
        private static readonly int AspectId = Shader.PropertyToID("_Aspect");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");

        /// <summary>True while the map stands still (lava, rocks, embers).</summary>
        public static bool IsFrozen { get; private set; }

        /// <summary>Set while the card is still flying: a new time stop waits for it.</summary>
        public bool Hold { get; set; }

        private FrameFx _frameFx;
        private Mesh _quad;
        private Transform _negative;
        private Transform _shadow;
        private MeshRenderer _negativeRenderer;
        private MeshRenderer _shadowRenderer;
        private Material _negativeMaterial;
        private Material _shadowMaterial;

        private bool _wanted;       // time should be stopped
        private bool _stopped;      // the last wave started was the one that stops time
        private bool _playing;      // a wave is running
        private float _age;
        private Vector3 _center;

        /// <summary>Creates the effect. Its clock animation plays through frameFx.</summary>
        public static TimeStopEffect Create(Transform parent, FrameFx frameFx)
        {
            var go = new GameObject("TimeStopEffect");
            go.transform.SetParent(parent, false);
            TimeStopEffect effect = go.AddComponent<TimeStopEffect>();
            effect._frameFx = frameFx;
            effect.Build();
            return effect;
        }

        /// <summary>Says whether time should be stopped, and the world point the wave spreads from.</summary>
        public void Set(bool stopped, Vector3 center)
        {
            _wanted = stopped;
            if (stopped)
                _center = center;
        }

        private void Build()
        {
            Shader shader = Resources.Load<Shader>("TimeStopWave");
            if (shader == null)
            {
                Debug.LogWarning("TimeStopEffect: Resources/TimeStopWave is missing, so time stops with no colour.");
                return;
            }

            _quad = new Mesh { name = "TimeStopQuad" };
            _quad.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f)
            };
            _quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            _quad.RecalculateBounds();

            _negativeMaterial = new Material(shader);
            _negativeMaterial.SetFloat(SrcBlendId, (float)BlendMode.OneMinusDstColor);
            _negativeMaterial.SetFloat(DstBlendId, (float)BlendMode.OneMinusSrcAlpha);
            _negative = NewLayer("TimeStopNegative", _negativeMaterial, NegativeOrder, out _negativeRenderer);

            _shadowMaterial = new Material(shader);
            _shadowMaterial.SetFloat(SrcBlendId, (float)BlendMode.One);
            _shadowMaterial.SetFloat(DstBlendId, (float)BlendMode.One);
            _shadow = NewLayer("TimeStopShadows", _shadowMaterial, ShadowOrder, out _shadowRenderer);
        }

        private Transform NewLayer(string name, Material material, int order, out MeshRenderer meshRenderer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = _quad;
            meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.sortingOrder = order;
            meshRenderer.enabled = false;
            return go.transform;
        }

        private void LateUpdate()
        {
            if (!_playing && _wanted != _stopped && !(_wanted && Hold))
                Begin(_wanted);

            if (_negativeMaterial != null && (_playing || _stopped))
                FollowCamera();
            if (!_playing)
                return;

            _age += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(_age / WaveSeconds);
            if (progress >= FreezeShare)
                IsFrozen = _stopped;
            Show(progress);
            if (progress >= 1f)
                Finish();
        }

        // Starts the wave that stops time, or the one that starts it again.
        private void Begin(bool stopping)
        {
            _stopped = stopping;
            _playing = true;
            _age = 0f;
            if (_negativeMaterial != null)
            {
                _negativeRenderer.enabled = true;
                _shadowRenderer.enabled = true;
                _negativeMaterial.SetFloat(ReverseId, stopping ? 0f : 1f);
                _shadowMaterial.SetFloat(ReverseId, stopping ? 0f : 1f);
            }

            FxLibrary fx = FxLibrary.Current;
            if (fx == null)
                return;
            GameAudio.PlaySfx(stopping ? fx.TimeStopSound : fx.TimeResumeSound);
            if (stopping && _frameFx != null)
                _frameFx.Play(fx.WorldClock, _center, fx.WorldClockHeight, FrameFx.UnderPieces);
        }

        private void Finish()
        {
            _playing = false;
            if (_negativeMaterial == null || _stopped)
                return;
            _negativeRenderer.enabled = false;
            _shadowRenderer.enabled = false;
        }

        // Keeps the two layers across the whole view.
        private void FollowCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;
            float height = 2f * QuadDistance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * QuadOverscan;
            Vector3 size = new Vector3(height * cam.aspect, height, 1f);
            Vector3 spot = cam.transform.position + cam.transform.forward * QuadDistance;
            _negative.SetPositionAndRotation(spot, cam.transform.rotation);
            _negative.localScale = size;
            _shadow.SetPositionAndRotation(spot, cam.transform.rotation);
            _shadow.localScale = size;
        }

        // One moment of a wave. With no animation (Low graphics) the colour only fades.
        private void Show(float progress)
        {
            if (_negativeMaterial == null)
                return;

            FxLibrary fx = FxLibrary.Current;
            Color settled = fx != null ? fx.TimeStopColor : Color.white;
            Color shadows = fx != null ? fx.TimeStopShadows : Color.black;

            bool wave = GraphicsQuality.AnimateUi;
            Camera cam = Camera.main;
            Vector3 point = cam != null ? cam.WorldToViewportPoint(_center) : new Vector3(0.5f, 0.5f, 0f);
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            float reach = Mathf.Sqrt(aspect * aspect + 1f) * 1.2f;
            float eased = 1f - (1f - progress) * (1f - progress);
            float ring = wave ? 1f - Mathf.InverseLerp(RingFadeFrom, 1f, progress) : 0f;

            // With no wave the whole view is covered and only the strength changes.
            float radius = wave ? eased * reach : reach * 2f;
            float strength = wave ? 1f : (_stopped ? progress : 1f - progress);
            if (!wave)
            {
                // Covered everywhere: reversing would cover nothing.
                _negativeMaterial.SetFloat(ReverseId, 0f);
                _shadowMaterial.SetFloat(ReverseId, 0f);
            }

            Color color = _stopped ? FlashColor(progress, settled) : settled;
            ApplyTo(_negativeMaterial, color, point, radius, ring, strength, aspect);
            ApplyTo(_shadowMaterial, shadows, point, radius, 0f, strength, aspect);
        }

        // The colour at this point of the stopping wave: magenta, green, then settling.
        private static Color FlashColor(float progress, Color settled)
        {
            if (progress < 0.35f)
                return Color.Lerp(FlashMagenta, FlashGreen, progress / 0.35f);
            return Color.Lerp(FlashGreen, settled, Mathf.InverseLerp(0.35f, 0.85f, progress));
        }

        private static void ApplyTo(Material material, Color color, Vector3 point, float radius, float ring,
            float strength, float aspect)
        {
            material.SetColor(ColorId, color);
            material.SetVector(CenterId, new Vector4(point.x, point.y, 0f, 0f));
            material.SetFloat(RadiusId, radius);
            material.SetFloat(RingGlowId, ring);
            material.SetFloat(StrengthId, strength);
            material.SetFloat(AspectId, aspect);
        }

        private void OnDestroy()
        {
            IsFrozen = false;
            if (_negativeMaterial != null)
                Destroy(_negativeMaterial);
            if (_shadowMaterial != null)
                Destroy(_shadowMaterial);
            if (_quad != null)
                Destroy(_quad);
        }
    }
}
