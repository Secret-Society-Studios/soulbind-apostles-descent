using UnityEngine;

namespace SoulBind.UI
{
    /// <summary>
    /// The art slots and settings for the map around the board: sky, fire sea, island, lava glow,
    /// floating rocks and embers. It lives at Assets/Resources/MapSkin.asset. An empty slot uses a
    /// small code-drawn pixel placeholder, so the painted layers can arrive one at a time.
    /// Values can be tuned during Play: changes to the asset stay after Play stops.
    /// </summary>
    [CreateAssetMenu(menuName = "SoulBind/Map Skin", fileName = "MapSkin")]
    public sealed class MapSkin : ScriptableObject
    {
        [Header("Map")]
        [Tooltip("Off: the old plain table, no map.")]
        [SerializeField] private bool showMap = true;
        [Tooltip("Draws every map layer with hard pixels (Point filter). " +
                 "Best with 512x512 art or smaller.")]
        [SerializeField] private bool pixelated = true;

        [Header("Sky (far behind everything)")]
        [Tooltip("Tiled. Leave empty for placeholder stars.")]
        [SerializeField] private Sprite sky;
        [SerializeField] private Color skyColor = new Color(0.05f, 0.03f, 0.09f);
        [SerializeField, Min(1f)] private float skyDepth = 40f;     // world units behind the board
        [SerializeField, Min(1f)] private float skyTileSize = 12f;  // world units per tile

        [Header("Fire sea (tiled, scrolls)")]
        [Tooltip("Must tile seamlessly. Import with Mesh Type: Full Rect.")]
        [SerializeField] private Sprite fireSea;
        [SerializeField, Min(0.5f)] private float seaDepth = 4f;    // world units below the board
        [SerializeField, Min(10f)] private float seaSize = 140f;    // world units across
        [SerializeField, Min(0.5f)] private float seaTileSize = 6f; // world units per tile
        [Tooltip("How fast the sea drifts, in world units per second.")]
        [SerializeField] private Vector2 seaScroll = new Vector2(0.25f, 0.12f);

        [Header("Island (under the board and the hands)")]
        [Tooltip("Replaces the plain table. Stretched to the size below.")]
        [SerializeField] private Sprite island;
        [Tooltip("Island size as a multiple of the board's radius (x = width, y = height). " + "Make it big enough that every tile sits on the island's flat top.")]
        [SerializeField] private Vector2 islandSize = new Vector2(1.35f, 2.15f);
        [Tooltip("Moves the island down by this share of its height. Leave at 0 when the island sprite's " + "pivot is already on the centre of its flat top (MapBoard.png's is).")]
        [SerializeField, Range(0f, 0.5f)] private float islandTopShift = 0f;

        [Header("3D island (built in code from the island art)")]
        [Tooltip("On: the island is a 3D rock, its top cut from the island art and its cliffs hanging below, " + "so it looks right when the camera turns. Off: the flat picture above.")]
        [SerializeField] private bool island3D = true;
        [Tooltip("The flat top's size as a multiple of the board's radius (x = width, y = depth).")]
        [SerializeField] private Vector2 islandTopSize = new Vector2(1.3f, 1.25f);
        [Tooltip("How far the rock hangs below the top, as a multiple of the top's half-width.")]
        [SerializeField, Min(0.1f)] private float islandDepth = 1.1f;
        [Tooltip("How bumpy the rim and the cliffs are: 0 = a smooth oval.")]
        [SerializeField, Range(0f, 0.3f)] private float islandRoughness = 0.06f;
        [Tooltip("The part of the island art used for the flat top (0 to 1 across the picture, from its " + "bottom-left corner).")]
        [SerializeField] private Rect islandTopArea = new Rect(0.08f, 0.42f, 0.79f, 0.5f);
        [Tooltip("The part of the island art used for the cliffs and the edge rocks.")]
        [SerializeField] private Rect islandCliffArea = new Rect(0.15f, 0.05f, 0.7f, 0.31f);
        [Tooltip("How dark the bottom of the cliffs gets: 1 = no darkening.")]
        [SerializeField, Range(0f, 1f)] private float cliffShade = 0.45f;

        [Header("Edge rocks (big rocks standing on the 3D island's rim)")]
        [Tooltip("Your rock art, picked in turn. Each stands on the rim and faces the camera, like the " + "avatars. Empty = 3D placeholder rocks cut from the cliff art.")]
        [SerializeField] private Sprite[] edgeRocks = new Sprite[0];
        [SerializeField, Range(0, 40)] private int edgeRockCount = 14;
        [Tooltip("The smallest and largest rock height, in world units.")]
        [SerializeField] private Vector2 edgeRockHeight = new Vector2(0.7f, 1.6f);
        [Tooltip("How far in from the rim the rocks stand, as a share of the island's radius.")]
        [SerializeField, Range(0f, 0.3f)] private float edgeRockInset = 0.05f;

        [Header("Lava glow (over the island's edge, pulses)")]
        [SerializeField] private Sprite lavaGlow;
        [SerializeField] private Color glowColor = new Color(1f, 0.45f, 0.1f);
        [SerializeField, Min(0f)] private float glowPulseSpeed = 1.6f;   // pulses per second (radians x 1/2pi)
        [SerializeField] private Vector2 glowAlpha = new Vector2(0.45f, 0.9f); // min, max

        [Header("Floating rocks (bob up and down)")]
        [Tooltip("Picked in turn for each rock. Empty = placeholder rocks.")]
        [SerializeField] private Sprite[] rocks = new Sprite[0];
        [SerializeField, Range(0, 24)] private int rockCount = 9;
        [SerializeField, Min(0.1f)] private float rockSize = 0.9f;  // world units across
        [SerializeField, Min(0f)] private float rockBob = 0.18f;    // world units up and down
        [SerializeField, Min(0f)] private float rockBobSpeed = 0.9f;

        [Header("Embers (rise from the sea)")]
        [SerializeField] private Sprite ember;
        [SerializeField, Range(0, 80)] private int emberCount = 36;
        [SerializeField, Min(0.01f)] private float emberSize = 0.14f;
        [SerializeField, Min(0.1f)] private float emberSpeed = 1.1f; // world units per second
        [SerializeField] private Color emberColor = new Color(1f, 0.6f, 0.2f);

        public bool ShowMap => showMap;
        public bool Pixelated => pixelated;
        public Sprite Sky => sky;
        public Color SkyColor => skyColor;
        public float SkyDepth => skyDepth;
        public float SkyTileSize => skyTileSize;
        public Sprite FireSea => fireSea;
        public float SeaDepth => seaDepth;
        public float SeaSize => seaSize;
        public float SeaTileSize => seaTileSize;
        public Vector2 SeaScroll => seaScroll;
        public Sprite Island => island;
        public Vector2 IslandSize => islandSize;
        public float IslandTopShift => islandTopShift;
        public bool Island3D => island3D;
        public Vector2 IslandTopSize => islandTopSize;
        public float IslandDepth => islandDepth;
        public float IslandRoughness => islandRoughness;
        public Rect IslandTopArea => islandTopArea;
        public Rect IslandCliffArea => islandCliffArea;
        public float CliffShade => cliffShade;
        public bool HasEdgeRockArt => edgeRocks != null && edgeRocks.Length > 0;
        public int EdgeRockCount => edgeRockCount;
        public Vector2 EdgeRockHeight => edgeRockHeight;
        public float EdgeRockInset => edgeRockInset;
        public Sprite LavaGlow => lavaGlow;
        public Color GlowColor => glowColor;
        public float GlowPulseSpeed => glowPulseSpeed;
        public Vector2 GlowAlpha => glowAlpha;
        public int RockCount => rockCount;
        public float RockSize => rockSize;
        public float RockBob => rockBob;
        public float RockBobSpeed => rockBobSpeed;
        public Sprite Ember => ember;
        public int EmberCount => emberCount;
        public float EmberSize => emberSize;
        public float EmberSpeed => emberSpeed;
        public Color EmberColor => emberColor;

        /// <summary>The art for edge rock number i, or null for a 3D placeholder rock.</summary>
        public Sprite GetEdgeRock(int i) => HasEdgeRockArt ? edgeRocks[i % edgeRocks.Length] : null;

        /// <summary>The art for rock number i, or null for the placeholder.</summary>
        public Sprite GetRock(int i) =>
            rocks != null && rocks.Length > 0 ? rocks[i % rocks.Length] : null;

        private static MapSkin _current;
        private static bool _looked;

        // Looked up again on every Play, even with domain reload turned off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _looked = false;
            _current = null;
        }

        /// <summary>The skin at Resources/MapSkin, or default settings with placeholders if there is none.</summary>
        public static MapSkin Current
        {
            get
            {
                if (_looked)
                    return _current;
                _looked = true;
                _current = Resources.Load<MapSkin>("MapSkin");
                if (_current == null)
                    _current = CreateInstance<MapSkin>();
                return _current;
            }
        }
    }
}
