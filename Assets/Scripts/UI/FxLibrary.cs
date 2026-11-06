using System;
using UnityEngine;

namespace SoulBind.UI
{
    /// <summary>
    /// One animation as a list of frames. An empty list means the art is not in yet.
    /// </summary>
    [Serializable]
    public struct FrameClip
    {
        [Tooltip("Frames in play order. Slice the sprite sheet in the Sprite Editor, then drag the frames here.")]
        public Sprite[] Frames;
        [Tooltip("Frames per second.")]
        public float Fps;
        [Tooltip("On: repeats. Off: plays once and holds the last frame.")]
        public bool Loop;

        public bool HasFrames => Frames != null && Frames.Length > 0;
    }

    /// <summary>
    /// The slots for every animation the art team is making. It lives at Assets/Resources/FxLibrary.asset.
    /// Drag the frames into a slot; an empty slot keeps the current code-drawn look, so the animations
    /// can arrive one at a time. Put the files in Assets/Art/Animations (one folder per slot).
    /// </summary>
    [CreateAssetMenu(menuName = "SoulBind/FX Library", fileName = "FxLibrary")]
    public sealed class FxLibrary : ScriptableObject
    {
        // ---------- World ----------

        [Header("Background (Animations/Lava)")]
        [Tooltip("The lava sea under the island, looping.")]
        [SerializeField] private FrameClip lava = new FrameClip { Fps = 8f, Loop = true };

        [Header("Layered island (Animations/Island)")]
        [Tooltip("Island layers from back to front. Each layer can have its own looping animation below.")]
        [SerializeField] private Sprite[] islandLayers = new Sprite[0];
        [Tooltip("Optional animation per layer, same order as Island Layers. Empty = the layer stays still.")]
        [SerializeField] private FrameClip[] islandLayerClips = new FrameClip[0];

        // ---------- Apostles ----------

        [Header("Apostles (Animations/Spawn, Animations/Death)")]
        [Tooltip("The Apostle falling onto its spawn tile.")]
        [SerializeField] private FrameClip spawnFall = new FrameClip { Fps = 12f };
        [Tooltip("How tall a frame of the spawn animation is, in world units (the avatar is about 1.1).")]
        [SerializeField, Min(0.1f)] private float spawnFallHeight = 3.5f;
        [Tooltip("The Apostle dying when its Vessel hits 0.")]
        [SerializeField] private FrameClip death = new FrameClip { Fps = 12f };

        [Header("Aura effects (Animations/Aura)")]
        [SerializeField] private FrameClip corruptionAura = new FrameClip { Fps = 10f, Loop = true };
        [SerializeField] private FrameClip healAura = new FrameClip { Fps = 12f };
        [SerializeField] private FrameClip purifyAura = new FrameClip { Fps = 12f };
        [SerializeField] private FrameClip shieldAura = new FrameClip { Fps = 10f, Loop = true };
        [SerializeField] private FrameClip awakenedAura = new FrameClip { Fps = 10f, Loop = true };
        [Tooltip("A buff card is played.")]
        [SerializeField] private FrameClip buffEffect = new FrameClip { Fps = 12f };
        [Tooltip("An Apostle is debuffed, by another player or by its own card.")]
        [SerializeField] private FrameClip debuffEffect = new FrameClip { Fps = 12f };
        [Tooltip("A Soul tile is picked up.")]
        [SerializeField] private FrameClip soulPick = new FrameClip { Fps = 12f };

        // ---------- Cards ----------

        [Header("Card animation per element (Animations/Cards)")]
        [Tooltip("Order: Radiance, Celeste, Crimson, Nocturne. Plays when a card of that element is played.")]
        [SerializeField] private FrameClip[] cardElements = new FrameClip[ElementCount];

        private const int ElementCount = 4; // Radiance, Celeste, Crimson, Nocturne

        [Header("Legendary card sounds")]
        [SerializeField] private AudioClip sunSound;
        [SerializeField] private AudioClip moonSound;
        [SerializeField] private AudioClip emperorSound;

        [Header("The World's time stop (Animations/World)")]
        [Tooltip("The clock, played once on the Apostle as the wave starts.")]
        [SerializeField] private FrameClip worldClock = new FrameClip { Fps = 12f };
        [Tooltip("How tall a frame of the clock is, in world units (the avatar is about 1.1).")]
        [SerializeField, Min(0.1f)] private float worldClockHeight = 3f;
        [Tooltip("The tint of the map's negative while time is stopped.")]
        [SerializeField] private Color timeStopColor = new Color(0.95f, 0.85f, 0.3f);
        [Tooltip("Added over the negative, to colour its dark parts.")]
        [SerializeField] private Color timeStopShadows = new Color(0.2f, 0.04f, 0.32f);
        [Tooltip("Plays as time stops.")]
        [SerializeField] private AudioClip timeStopSound;
        [Tooltip("Plays as time starts again.")]
        [SerializeField] private AudioClip timeResumeSound;

        // ---------- Menus and UI ----------

        [Header("Main menu background (Animations/MainMenuBackground)")]
        [SerializeField] private FrameClip mainMenuBackground = new FrameClip { Fps = 8f, Loop = true };

        [Header("UI animations (Animations/UI)")]
        [SerializeField] private FrameClip buttonPress = new FrameClip { Fps = 24f };
        [SerializeField] private FrameClip popupOpen = new FrameClip { Fps = 24f };
        [SerializeField] private FrameClip turnBanner = new FrameClip { Fps = 12f };

        public FrameClip Lava => lava;
        public FrameClip SpawnFall => spawnFall;
        public float SpawnFallHeight => spawnFallHeight;
        public FrameClip Death => death;
        public FrameClip CorruptionAura => corruptionAura;
        public FrameClip HealAura => healAura;
        public FrameClip PurifyAura => purifyAura;
        public FrameClip ShieldAura => shieldAura;
        public FrameClip AwakenedAura => awakenedAura;
        public FrameClip BuffEffect => buffEffect;
        public FrameClip DebuffEffect => debuffEffect;
        public FrameClip SoulPick => soulPick;
        public FrameClip WorldClock => worldClock;
        public float WorldClockHeight => worldClockHeight;
        public Color TimeStopColor => timeStopColor;
        public Color TimeStopShadows => timeStopShadows;
        public AudioClip TimeStopSound => timeStopSound;
        public AudioClip TimeResumeSound => timeResumeSound;
        public AudioClip SunSound => sunSound;
        public AudioClip MoonSound => moonSound;
        public AudioClip EmperorSound => emperorSound;
        public FrameClip MainMenuBackground => mainMenuBackground;
        public FrameClip ButtonPress => buttonPress;
        public FrameClip PopupOpen => popupOpen;
        public FrameClip TurnBanner => turnBanner;
        public int IslandLayerCount => islandLayers != null ? islandLayers.Length : 0;

        // One island layer, back to front, or null.
        public Sprite GetIslandLayer(int index) =>
            islandLayers != null && index >= 0 && index < islandLayers.Length ? islandLayers[index] : null;

        // The animation for an island layer; empty when the layer stays still.
        public FrameClip GetIslandLayerClip(int index) =>
            islandLayerClips != null && index >= 0 && index < islandLayerClips.Length
                ? islandLayerClips[index]
                : default;

        // The play animation for a card element; empty until the art is in.
        public FrameClip GetCardElement(SoulBind.Core.Element element) =>
            cardElements != null && (int)element < cardElements.Length ? cardElements[(int)element] : default;

        // ---------- Finding it ----------

        private static FxLibrary _current;
        private static bool _looked;

        // Looked up again on every Play, even with domain reload turned off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _looked = false;
            _current = null;
        }

        /// <summary>The library at Resources/FxLibrary, or null (everything keeps its current look).</summary>
        public static FxLibrary Current
        {
            get
            {
                if (!_looked)
                {
                    _looked = true;
                    _current = Resources.Load<FxLibrary>("FxLibrary");
                }
                return _current;
            }
        }
    }
}
