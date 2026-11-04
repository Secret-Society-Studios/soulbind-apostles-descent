using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SoulBind.Managers
{
    /// <summary>
    /// Low, Medium or High graphics, picked for the device on first launch (cheap phones get Low,
    /// laptops with built-in graphics get Medium) and changeable from the match Menu. It sets the frame
    /// rate, the render resolution, HDR and post-processing, and tells the map how many effects to make.
    /// The project's settings files are never changed: it works on a copy of the URP asset.
    /// </summary>
    public static class GraphicsQuality
    {
        public enum Tier { Low, Medium, High }

        private const string PrefKey = "graphicsQuality"; // -1 = auto, else (int)Tier
        private const int Auto = -1;

        private static UniversalRenderPipelineAsset _original; // the project's asset, never edited
        private static UniversalRenderPipelineAsset _copy;     // the one actually used
        private static RenderPipelineAsset _qualityPipeline;   // the quality level's own slot, put back after Play
        private static int _originalVSync;
        private static int _originalFrameRate;
        private static bool _started;

        /// <summary>The tier in use now.</summary>
        public static Tier Current { get; private set; } = Tier.High;

        /// <summary>True when the player left it on Auto (the device decides).</summary>
        public static bool IsAuto => PlayerPrefs.GetInt(PrefKey, Auto) == Auto;

        /// <summary>How many map effects to make (embers, floating rocks), as a share of MapSkin's counts.</summary>
        public static float EffectsScale => Current == Tier.Low ? 0.35f : Current == Tier.Medium ? 0.7f : 1f;

        /// <summary>False on Low: glows hold still instead of pulsing, so the UI is not redrawn every frame.</summary>
        public static bool AnimateUi => Current != Tier.Low;

        /// <summary>The far sky layer: hidden behind the fire sea with the match camera, so only High draws it.</summary>
        public static bool ShowFarLayers => Current == Tier.High;

        /// <summary>A button label such as "Graphics: Auto (Medium)".</summary>
        public static string Label => IsAuto ? "Graphics: Auto (" + Current + ")" : "Graphics: " + Current;

        // ---------- Starting ----------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Start()
        {
            if (_started)
                return;
            _started = true;
            _qualityPipeline = QualitySettings.renderPipeline;
            _original = _qualityPipeline as UniversalRenderPipelineAsset;
            if (_original == null)
                _original = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            _originalVSync = QualitySettings.vSyncCount;
            _originalFrameRate = Application.targetFrameRate;
            SceneManager.sceneLoaded += OnSceneLoaded;
            // In the editor, put everything back when Play stops.
            Application.quitting += Restore;
            Apply();
        }

        // Domain reload may be off in the editor: start fresh on every Play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _started = false;
            _copy = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Application.quitting -= Restore;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyCamera();

        // ---------- Changing ----------

        /// <summary>Auto, Low, Medium, High, then Auto again. Saved for next time.</summary>
        public static void Cycle()
        {
            int saved = PlayerPrefs.GetInt(PrefKey, Auto);
            int next = saved >= (int)Tier.High ? Auto : saved + 1;
            PlayerPrefs.SetInt(PrefKey, next);
            PlayerPrefs.Save();
            Apply();
        }

        /// <summary>Works out the tier and applies it. Map effect counts change from the next match.</summary>
        public static void Apply()
        {
            int saved = PlayerPrefs.GetInt(PrefKey, Auto);
            Current = saved == Auto ? Detect() : (Tier)Mathf.Clamp(saved, 0, 2);
            bool phone = Application.isMobilePlatform;

            // Phones: a fixed frame rate saves battery and heat. A turn-based game reads fine at 30.
            // Computers: follow the screen's refresh (vSync), or every other refresh on Low.
            if (phone)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = Current == Tier.Low ? 30 : 60;
            }
            else
            {
                QualitySettings.vSyncCount = Current == Tier.Low ? 2 : 1;
                Application.targetFrameRate = -1;
            }

            ApplyPipeline(phone);
            ApplyCamera();
            Debug.Log("[GQ] Graphics " + Label + ", " + SystemInfo.systemMemorySize + " MB RAM, "
                + SystemInfo.graphicsMemorySize + " MB video, " + SystemInfo.processorCount + " cores.");
        }

        // Cheap phones: 3 GB of memory or less, or 4 cores or fewer. Laptops with built-in graphics
        // (little video memory of their own) or 8 GB of memory or less: Medium.
        private static Tier Detect()
        {
            int ram = SystemInfo.systemMemorySize;
            if (Application.isMobilePlatform)
            {
                if (ram <= 3072 || SystemInfo.processorCount <= 4)
                    return Tier.Low;
                return ram <= 6144 ? Tier.Medium : Tier.High;
            }
            if (SystemInfo.graphicsMemorySize < 2048 || ram <= 8192)
                return Tier.Medium;
            return Tier.High;
        }

        // Fewer pixels to draw is the biggest saving on a phone. The HUD text stays sharp: it is drawn
        // at full resolution on top.
        private static void ApplyPipeline(bool phone)
        {
            if (_original == null)
                return;
            if (_copy == null)
            {
                _copy = Object.Instantiate(_original);
                _copy.name = _original.name + " (runtime)";
            }

            float scale;
            if (Current == Tier.Low)
                scale = phone ? 0.7f : 0.85f;
            else if (Current == Tier.Medium)
                scale = phone ? 0.85f : 1f;
            else
                scale = 1f;
            _copy.renderScale = scale;
            _copy.supportsHDR = Current != Tier.Low && _original.supportsHDR;
            _copy.msaaSampleCount = 1; // pixel art needs no anti-aliasing
            _copy.shadowDistance = Current == Tier.Low ? 0f : _original.shadowDistance;
            QualitySettings.renderPipeline = _copy;
        }

        // Bloom and colour grading (the Global Volume) are skipped on Low.
        private static void ApplyCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;
            cam.allowMSAA = false;
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            if (data != null)
                data.renderPostProcessing = Current != Tier.Low;
        }

        // Puts the project's own settings back, so Play mode never leaves them changed.
        private static void Restore()
        {
            QualitySettings.renderPipeline = _qualityPipeline;
            QualitySettings.vSyncCount = _originalVSync;
            Application.targetFrameRate = _originalFrameRate;
        }
    }
}
