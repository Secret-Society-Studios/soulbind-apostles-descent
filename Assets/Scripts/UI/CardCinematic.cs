using SoulBind.Core;
using SoulBind.Managers;
using UnityEngine;
using UnityEngine.Rendering;

namespace SoulBind.UI
{
    /// <summary>
    /// The short moment a legendary card gets when it resolves: The Sun's rays of light, The Moon's
    /// darkness and pale glow, The Emperor's gold wave and shake. It draws in the scene just above the
    /// map, so the tiles, pieces, table cards and HUD stay untouched. On Low graphics there is no moving
    /// wave. The World has its own, longer effect (TimeStopEffect).
    /// MatchController creates it at runtime and calls Play.
    /// </summary>
    public sealed class CardCinematic : MonoBehaviour
    {
        private const int DarkOrder = -3;  // over TimeStopEffect (-5, -4), under the tiles
        private const int LightOrder = -2;
        private const float QuadDistance = 1f;
        private const float QuadOverscan = 1.2f;
        private const float RiseShare = 0.15f;  // of the moment, to reach full strength
        private const float FallFrom = 0.6f;    // of the moment, when it starts to fade
        private const float SunSeconds = 1.5f;
        private const float MoonSeconds = 1.9f;
        private const float EmperorSeconds = 1.1f;
        private const float MoonDarkness = 0.7f;
        private const float MoonGlowRadius = 0.55f; // screen heights
        private const float MoonGlowSoftness = 0.5f;
        private const float EmperorShake = 0.3f;

        private static readonly Color SunLight = new Color(1f, 0.82f, 0.4f);
        private static readonly Color MoonLight = new Color(0.45f, 0.55f, 0.8f);
        private static readonly Color EmperorGold = new Color(0.8f, 0.55f, 0.12f);

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int CenterId = Shader.PropertyToID("_Center");
        private static readonly int RadiusId = Shader.PropertyToID("_Radius");
        private static readonly int RingGlowId = Shader.PropertyToID("_RingGlow");
        private static readonly int StrengthId = Shader.PropertyToID("_Strength");
        private static readonly int AspectId = Shader.PropertyToID("_Aspect");
        private static readonly int SoftId = Shader.PropertyToID("_Soft");
        private static readonly int RaysId = Shader.PropertyToID("_Rays");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");

        /// <summary>Set while the card is still flying: its moment waits for it to land.</summary>
        public bool Hold { get; set; }

        private TableEffects _effects;
        private Mesh _quad;
        private Transform _dark;
        private Transform _light;
        private MeshRenderer _darkRenderer;
        private MeshRenderer _lightRenderer;
        private Material _darkMaterial;
        private Material _lightMaterial;

        private bool _waiting;  // asked for, waiting for the card to land
        private bool _playing;
        private ArcanaId _card;
        private Vector3 _center;
        private float _age;
        private float _seconds;

        /// <summary>Creates the effect. effects gives The Emperor its screen shake.</summary>
        public static CardCinematic Create(Transform parent, TableEffects effects)
        {
            var go = new GameObject("CardCinematic");
            go.transform.SetParent(parent, false);
            CardCinematic cinematic = go.AddComponent<CardCinematic>();
            cinematic._effects = effects;
            cinematic.Build();
            return cinematic;
        }

        /// <summary>True for the cards with a moment of their own here.</summary>
        public static bool HasMoment(ArcanaId card) =>
            card == ArcanaId.Sun || card == ArcanaId.Moon || card == ArcanaId.Emperor;

        /// <summary>Plays this card's moment, spreading from a world point, once the card has landed.</summary>
        public void Play(ArcanaId card, Vector3 center)
        {
            if (!HasMoment(card))
                return;
            _card = card;
            _center = center;
            _waiting = true;
        }

        private void Build()
        {
            Shader shader = Resources.Load<Shader>("TimeStopWave");
            if (shader == null)
            {
                Debug.LogWarning("CardCinematic: Resources/TimeStopWave is missing, so legendary cards have no moment.");
                return;
            }

            _quad = new Mesh { name = "CardCinematicQuad" };
            _quad.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f)
            };
            _quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            _quad.RecalculateBounds();

            // Dark takes colour away from the map; light adds it.
            _darkMaterial = new Material(shader);
            _darkMaterial.SetFloat(SrcBlendId, (float)BlendMode.Zero);
            _darkMaterial.SetFloat(DstBlendId, (float)BlendMode.OneMinusSrcAlpha);
            _dark = NewLayer("CinematicDark", _darkMaterial, DarkOrder, out _darkRenderer);

            _lightMaterial = new Material(shader);
            _lightMaterial.SetFloat(SrcBlendId, (float)BlendMode.One);
            _lightMaterial.SetFloat(DstBlendId, (float)BlendMode.One);
            _light = NewLayer("CinematicLight", _lightMaterial, LightOrder, out _lightRenderer);
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
            if (_waiting && !Hold)
            {
                _waiting = false;
                Begin();
            }
            if (!_playing)
                return;

            FollowCamera();
            _age += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(_age / _seconds);
            Show(progress);
            if (progress >= 1f)
                Finish();
        }

        private void Begin()
        {
            FxLibrary fx = FxLibrary.Current;
            switch (_card)
            {
                case ArcanaId.Sun:
                    _seconds = SunSeconds;
                    GameAudio.PlaySfx(fx != null ? fx.SunSound : null);
                    break;
                case ArcanaId.Moon:
                    _seconds = MoonSeconds;
                    GameAudio.PlaySfx(fx != null ? fx.MoonSound : null);
                    break;
                default:
                    _seconds = EmperorSeconds;
                    GameAudio.PlaySfx(fx != null ? fx.EmperorSound : null);
                    if (_effects != null)
                        _effects.Shake(EmperorShake, EmperorSeconds * 0.6f);
                    break;
            }

            if (_darkMaterial == null)
                return;
            _age = 0f;
            _playing = true;
            _darkRenderer.enabled = _card == ArcanaId.Moon;
            _lightRenderer.enabled = true;
        }

        private void Finish()
        {
            _playing = false;
            _darkRenderer.enabled = false;
            _lightRenderer.enabled = false;
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
            _dark.SetPositionAndRotation(spot, cam.transform.rotation);
            _dark.localScale = size;
            _light.SetPositionAndRotation(spot, cam.transform.rotation);
            _light.localScale = size;
        }

        // One moment of the effect: it rises fast, holds, then fades.
        private void Show(float progress)
        {
            Camera cam = Camera.main;
            Vector3 point = cam != null ? cam.WorldToViewportPoint(_center) : new Vector3(0.5f, 0.5f, 0f);
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            float reach = Mathf.Sqrt(aspect * aspect + 1f) * 1.2f;
            float eased = 1f - (1f - progress) * (1f - progress);
            float strength = Mathf.Clamp01(progress / RiseShare) * (1f - Mathf.InverseLerp(FallFrom, 1f, progress));
            bool wave = GraphicsQuality.AnimateUi;
            float grow = wave ? eased * reach : reach * 2f;
            float ringFade = wave ? 1f - Mathf.InverseLerp(0.3f, 0.8f, progress) : 0f;

            switch (_card)
            {
                case ArcanaId.Sun:
                    ApplyTo(_lightMaterial, SunLight, point, grow, ringFade, strength, aspect, 0.25f, 0.75f);
                    break;
                case ArcanaId.Moon:
                    ApplyTo(_darkMaterial, Color.black, point, reach * 2f, 0f, strength * MoonDarkness, aspect, 0.01f, 0f);
                    float glow = wave ? MoonGlowRadius * eased : MoonGlowRadius;
                    ApplyTo(_lightMaterial, MoonLight, point, glow + MoonGlowSoftness, 0f, strength, aspect, MoonGlowSoftness, 0f);
                    break;
                default:
                    ApplyTo(_lightMaterial, EmperorGold, point, grow, ringFade, strength * 0.7f, aspect, 0.1f, 0f);
                    break;
            }
        }

        private static void ApplyTo(Material material, Color color, Vector3 point, float radius, float ring,
            float strength, float aspect, float soft, float rays)
        {
            material.SetColor(ColorId, color);
            material.SetVector(CenterId, new Vector4(point.x, point.y, 0f, 0f));
            material.SetFloat(RadiusId, radius);
            material.SetFloat(RingGlowId, ring);
            material.SetFloat(StrengthId, strength);
            material.SetFloat(AspectId, aspect);
            material.SetFloat(SoftId, soft);
            material.SetFloat(RaysId, rays);
        }

        private void OnDestroy()
        {
            if (_darkMaterial != null)
                Destroy(_darkMaterial);
            if (_lightMaterial != null)
                Destroy(_lightMaterial);
            if (_quad != null)
                Destroy(_quad);
        }
    }
}
