using SoulBind.Managers;
using UnityEngine;

namespace SoulBind.UI
{
    /// <summary>
    /// The map around the board: a floating island over a scrolling sea of fire, under a starry sky,
    /// with a pulsing lava glow, bobbing rocks and rising embers. Settings and art come from MapSkin;
    /// empty slots get small pixel placeholders drawn in code. Display only, no rules.
    /// The board lies in the XY plane and the camera looks from -Z, so a larger Z is further below.
    /// </summary>
    public sealed class MapBackdrop : MonoBehaviour
    {
        // Sorting orders: all below the old table (-20) and the mat (-10).
        private const int SkyOrder = -60;
        private const int SeaOrder = -50;
        private const int RockOrder = -45;
        private const int EmberOrder = -44;
        private const int IslandOrder = -30;
        private const int GlowOrder = -29;
        private const int EdgeRockOrder = -28;
        private const int RimSegments = 72; // corners around the 3D island's rim

        // Placeholder texture sizes, in pixels. Small on purpose: they read as pixel art.
        private const int SeaPixels = 64;
        private const int SkyPixels = 64;
        private const int IslandWidth = 160;
        private const int IslandHeight = 128;
        private const int RockPixels = 16;

        private MapSkin _skin;
        private BoardView _board;
        private SpriteRenderer _sky;
        private SpriteRenderer _sea;
        private SpriteRenderer _island;
        private SpriteRenderer _glow;
        private SpriteRenderer[] _rocks;
        private Vector3[] _rockHome;
        private float[] _rockPhase;
        private SpriteRenderer[] _embers;
        private Vector3[] _emberVelocity;
        private float[] _emberAge;
        private float[] _emberLife;
        private float _islandRadiusX; // world units
        private float _islandRadiusY;
        private SpriteRenderer[] _edgeRockSprites; // painted edge rocks, turned to the camera each frame
        private Vector3[] _edgeRockBase;           // where each one stands, on the rim
        private float[] _edgeRockLift;             // pivot to bottom edge, so it stands on its base

        /// <summary>Builds the map under the board, or returns null if MapSkin turns the map off.</summary>
        public static MapBackdrop Create(BoardView board)
        {
            MapSkin skin = MapSkin.Current;
            if (board == null || skin == null || !skin.ShowMap)
                return null;

            var go = new GameObject("MapBackdrop");
            go.transform.SetParent(board.transform, false);
            var backdrop = go.AddComponent<MapBackdrop>();
            backdrop.Build(board, skin);
            return backdrop;
        }

        // ---------- Building ----------

        private void Build(BoardView board, MapSkin skin)
        {
            _board = board;
            _skin = skin;

            // The island takes over from the plain table.
            Transform table = board.transform.Find("Table");
            if (table != null)
                table.gameObject.SetActive(false);

            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = skin.SkyColor;
            }

            // The sky sits behind the sea and the match camera never looks past the sea, so drawing it
            // only costs a full screen of pixels. Only High keeps it.
            if (GraphicsQuality.ShowFarLayers)
                _sky = CreateTiled("Sky", Pick(skin.Sky, PlaceholderSky), SkyOrder,
                    skin.SkyTileSize, skin.SeaSize * 3f, skin.SkyDepth);
            _sea = CreateTiled("FireSea", Pick(skin.FireSea, PlaceholderSea), SeaOrder,
                skin.SeaTileSize, skin.SeaSize, skin.SeaDepth);

            // The island is sized to the board itself, so the tiles stand on its flat top (like an
            // auto-battler's arena) instead of a table around them.
            float radius = board.BoardRadius;
            if (skin.Island3D && skin.Island != null && Build3DIsland(skin, radius))
            {
                BuildRocks(skin);
                BuildEmbers(skin);
                return;
            }
            _islandRadiusX = radius * skin.IslandSize.x;
            _islandRadiusY = radius * skin.IslandSize.y;
            _island = CreateFitted("Island", Pick(skin.Island, PlaceholderIsland), IslandOrder,
                _islandRadiusX * 2f, _islandRadiusY * 2f, 0.05f);
            // Painted art has its flat top above the middle; move it down so the top is under the board.
            if (skin.Island != null)
                _island.transform.localPosition += new Vector3(0f, -skin.IslandTopShift * _islandRadiusY * 2f, 0f);
            // The glow is a bit bigger than the island, so it spills over the cliffs. The placeholder
            // glow is an oval made for the placeholder island, so painted island art goes without it.
            if (skin.LavaGlow != null || skin.Island == null)
                _glow = CreateFitted("LavaGlow", Pick(skin.LavaGlow, PlaceholderGlow), GlowOrder,
                    _islandRadiusX * 2.2f, _islandRadiusY * 2.2f, 0.1f);

            BuildRocks(skin);
            BuildEmbers(skin);
        }

        // Fewer effects on weaker devices (GraphicsQuality), but never none if the skin asks for some.
        private static int ScaledCount(int count) =>
            count <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(count * GraphicsQuality.EffectsScale));

        // ---------- 3D island ----------

        // The island as a 3D rock with big rocks on its rim. Returns false (use the flat picture)
        // if the island material is missing.
        private bool Build3DIsland(MapSkin skin, float boardRadius)
        {
            Material shared = Resources.Load<Material>("IslandMaterial");
            if (shared == null)
            {
                Debug.LogWarning("MapBackdrop: Resources/IslandMaterial is missing, using the flat island.");
                return false;
            }
            Sprite art = Pick(skin.Island, PlaceholderIsland);
            var material = new Material(shared) { mainTexture = art.texture };
            Rect top = IslandMesh.ToTexture(art, skin.IslandTopArea);
            Rect cliff = IslandMesh.ToTexture(art, skin.IslandCliffArea);

            _islandRadiusX = boardRadius * skin.IslandTopSize.x;
            _islandRadiusY = boardRadius * skin.IslandTopSize.y;
            float[] rim = IslandMesh.MakeRim(RimSegments, skin.IslandRoughness, 5);
            Mesh mesh = IslandMesh.BuildIsland(_islandRadiusX, _islandRadiusY, _islandRadiusX * skin.IslandDepth,
                rim, skin.IslandRoughness, top, cliff, skin.CliffShade);
            // Just under the tiles, so they never fight it.
            CreateMesh("Island3D", mesh, material, IslandOrder, new Vector3(0f, 0f, 0.02f));

            BuildEdgeRocks(skin, rim, material, cliff);
            return true;
        }

        // Big rocks spaced around the rim: your art standing up like the avatars, or 3D placeholder rocks.
        private void BuildEdgeRocks(MapSkin skin, float[] rim, Material material, Rect cliff)
        {
            int count = skin.EdgeRockCount;
            bool painted = skin.HasEdgeRockArt;
            _edgeRockSprites = painted ? new SpriteRenderer[count] : null;
            _edgeRockBase = painted ? new Vector3[count] : null;
            _edgeRockLift = painted ? new float[count] : null;
            var random = new System.Random(13); // same layout every match

            for (int i = 0; i < count; i++)
            {
                float turn = (i + (float)random.NextDouble() * 0.7f) / count;
                int segment = Mathf.Clamp((int)(turn * RimSegments), 0, RimSegments - 1);
                float angle = turn * Mathf.PI * 2f;
                float reach = rim[segment] * (1f - skin.EdgeRockInset);
                var spot = new Vector3(Mathf.Cos(angle) * _islandRadiusX * reach,
                    Mathf.Sin(angle) * _islandRadiusY * reach, 0f);
                float height = Mathf.Lerp(skin.EdgeRockHeight.x, skin.EdgeRockHeight.y, (float)random.NextDouble());

                if (painted)
                {
                    Sprite sprite = Pick(skin.GetEdgeRock(i), PlaceholderRock);
                    SpriteRenderer rock = CreateRenderer("EdgeRock", sprite, EdgeRockOrder);
                    float scale = height / Mathf.Max(0.0001f, sprite.bounds.size.y);
                    rock.transform.localScale = Vector3.one * scale;
                    rock.flipX = random.Next(2) == 0;
                    _edgeRockSprites[i] = rock;
                    _edgeRockBase[i] = spot;
                    _edgeRockLift[i] = -sprite.bounds.min.y * scale;
                }
                else
                {
                    Mesh mesh = IslandMesh.BuildRock(100 + i, cliff, skin.CliffShade);
                    Transform rock = CreateMesh("EdgeRock", mesh, material, EdgeRockOrder, spot);
                    float width = height * (0.7f + (float)random.NextDouble() * 0.5f);
                    rock.localScale = new Vector3(width, width, height);
                    rock.localRotation = Quaternion.AngleAxis((float)random.NextDouble() * 360f, Vector3.forward);
                }
            }
        }

        private Transform CreateMesh(string name, Mesh mesh, Material material, int order, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = position;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = order;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go.transform;
        }

        // Painted edge rocks stand on the rim facing the camera, like the avatars. No allocations.
        private void LateUpdate()
        {
            if (_edgeRockSprites == null)
                return;
            Camera cam = Camera.main;
            if (cam == null)
                return;
            Quaternion facing = cam.transform.rotation;
            Vector3 up = facing * Vector3.up;
            for (int i = 0; i < _edgeRockSprites.Length; i++)
            {
                Transform rock = _edgeRockSprites[i].transform;
                rock.rotation = facing;
                rock.position = transform.TransformPoint(_edgeRockBase[i]) + up * _edgeRockLift[i];
            }
        }

        private Sprite Pick(Sprite art, System.Func<Sprite> placeholder)
        {
            Sprite sprite = art != null ? art : placeholder();
            if (_skin.Pixelated && sprite != null && sprite.texture != null)
                sprite.texture.filterMode = FilterMode.Point;
            return sprite;
        }

        // A sprite repeated over a square `size` wide, each copy `tile` world units across.
        private SpriteRenderer CreateTiled(string name, Sprite sprite, int order,
            float tile, float size, float depth)
        {
            SpriteRenderer renderer = CreateRenderer(name, sprite, order);
            Vector2 spriteSize = sprite.bounds.size;
            float scale = tile / Mathf.Max(0.0001f, spriteSize.x);
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
            renderer.transform.localPosition = new Vector3(0f, 0f, depth);
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.size = new Vector2(size / scale, size / scale);
            return renderer;
        }

        // A sprite stretched to exactly width x height world units.
        private SpriteRenderer CreateFitted(string name, Sprite sprite, int order,
            float width, float height, float depth)
        {
            SpriteRenderer renderer = CreateRenderer(name, sprite, order);
            Vector2 spriteSize = sprite.bounds.size;
            renderer.transform.localScale = new Vector3(
                width / Mathf.Max(0.0001f, spriteSize.x),
                height / Mathf.Max(0.0001f, spriteSize.y), 1f);
            renderer.transform.localPosition = new Vector3(0f, 0f, depth);
            return renderer;
        }

        private SpriteRenderer CreateRenderer(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        // Rocks float in a ring just outside the island, between it and the sea.
        private void BuildRocks(MapSkin skin)
        {
            int count = ScaledCount(skin.RockCount);
            _rocks = new SpriteRenderer[count];
            _rockHome = new Vector3[count];
            _rockPhase = new float[count];
            var random = new System.Random(7); // same layout every match
            for (int i = 0; i < count; i++)
            {
                Sprite sprite = Pick(skin.GetRock(i), PlaceholderRock);
                float angle = (i + (float)random.NextDouble() * 0.6f) / count * Mathf.PI * 2f;
                float reach = 1.12f + (float)random.NextDouble() * 0.35f;
                float size = skin.RockSize * (0.6f + (float)random.NextDouble() * 0.8f);
                float depth = skin.SeaDepth * (0.3f + (float)random.NextDouble() * 0.4f);
                var home = new Vector3(Mathf.Cos(angle) * _islandRadiusX * reach,
                    Mathf.Sin(angle) * _islandRadiusY * reach, depth);

                SpriteRenderer rock = CreateFitted("Rock", sprite, RockOrder, size, size, depth);
                rock.transform.localPosition = home;
                rock.flipX = random.Next(2) == 0;
                _rocks[i] = rock;
                _rockHome[i] = home;
                _rockPhase[i] = (float)random.NextDouble() * Mathf.PI * 2f;
            }
        }

        private void BuildEmbers(MapSkin skin)
        {
            int count = ScaledCount(skin.EmberCount);
            _embers = new SpriteRenderer[count];
            _emberVelocity = new Vector3[count];
            _emberAge = new float[count];
            _emberLife = new float[count];
            Sprite sprite = Pick(skin.Ember, PlaceholderEmber);
            for (int i = 0; i < count; i++)
            {
                SpriteRenderer ember = CreateFitted("Ember", sprite, EmberOrder,
                    skin.EmberSize, skin.EmberSize, skin.SeaDepth);
                _embers[i] = ember;
                Respawn(i);
                // Spread the first wave out, so they don't all rise together.
                _emberAge[i] = Random.value * _emberLife[i];
            }
        }

        // ---------- Moving ----------

        private void Update()
        {
            if (_skin == null)
                return;
            float time = Time.time;
            float delta = Time.deltaTime;

            // Sea: slide by less than one tile, then jump back; tiling hides the jump.
            float tile = _skin.SeaTileSize;
            Vector2 drift = _skin.SeaScroll * time;
            _sea.transform.localPosition = new Vector3(
                Mathf.Repeat(drift.x, tile), Mathf.Repeat(drift.y, tile), _skin.SeaDepth);

            float pulse = 0.5f + 0.5f * Mathf.Sin(time * _skin.GlowPulseSpeed * Mathf.PI * 2f);
            Color glow = _skin.GlowColor;
            glow.a = Mathf.Lerp(_skin.GlowAlpha.x, _skin.GlowAlpha.y, pulse);
            if (_glow != null)
                _glow.color = glow;

            for (int i = 0; i < _rocks.Length; i++)
            {
                float bob = Mathf.Sin(time * _skin.RockBobSpeed + _rockPhase[i]) * _skin.RockBob;
                _rocks[i].transform.localPosition = _rockHome[i] + new Vector3(0f, bob, -bob);
            }

            for (int i = 0; i < _embers.Length; i++)
            {
                _emberAge[i] += delta;
                if (_emberAge[i] >= _emberLife[i])
                    Respawn(i);
                Transform ember = _embers[i].transform;
                ember.localPosition += _emberVelocity[i] * delta;
                float life = _emberAge[i] / _emberLife[i];
                Color color = _skin.EmberColor;
                color.a = Mathf.Sin(life * Mathf.PI); // fade in, then out
                _embers[i].color = color;
            }
        }

        // An ember starts on the sea somewhere around the island and rises towards the camera.
        private void Respawn(int i)
        {
            float angle = Random.value * Mathf.PI * 2f;
            float reach = 1.05f + Random.value * 0.8f;
            _embers[i].transform.localPosition = new Vector3(
                Mathf.Cos(angle) * _islandRadiusX * reach,
                Mathf.Sin(angle) * _islandRadiusY * reach,
                _skin.SeaDepth);
            float speed = _skin.EmberSpeed * (0.6f + Random.value * 0.8f);
            _emberVelocity[i] = new Vector3((Random.value - 0.5f) * 0.4f, speed * 0.35f, -speed);
            _emberLife[i] = _skin.SeaDepth / speed;
            _emberAge[i] = 0f;
        }

        // ---------- Placeholders (tiny pixel textures, made once) ----------

        private static Sprite _placeholderSky;
        private static Sprite _placeholderSea;
        private static Sprite _placeholderIsland;
        private static Sprite _placeholderGlow;
        private static Sprite _placeholderRock;
        private static Sprite _placeholderEmber;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _placeholderSky = null;
            _placeholderSea = null;
            _placeholderIsland = null;
            _placeholderGlow = null;
            _placeholderRock = null;
            _placeholderEmber = null;
        }

        // Dark sky with a few stars; tiles seamlessly.
        private static Sprite PlaceholderSky()
        {
            if (_placeholderSky != null)
                return _placeholderSky;
            int n = SkyPixels;
            var pixels = new Color32[n * n];
            var dark = new Color32(13, 8, 24, 255);
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = dark;
            var random = new System.Random(3);
            for (int s = 0; s < 18; s++)
            {
                byte light = (byte)(150 + random.Next(105));
                pixels[random.Next(n) + random.Next(n) * n] = new Color32(light, light, 255, 255);
            }
            _placeholderSky = MakeSprite("MapSky", n, n, pixels);
            return _placeholderSky;
        }

        // Banded fire made from waves that repeat exactly across the tile, so it tiles seamlessly.
        private static Sprite PlaceholderSea()
        {
            if (_placeholderSea != null)
                return _placeholderSea;
            int n = SeaPixels;
            var pixels = new Color32[n * n];
            Color32[] palette =
            {
                new Color32(70, 8, 6, 255),
                new Color32(140, 24, 8, 255),
                new Color32(214, 70, 14, 255),
                new Color32(255, 150, 40, 255),
                new Color32(255, 222, 120, 255)
            };
            float tau = Mathf.PI * 2f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (float)x / n;
                    float v = (float)y / n;
                    float wave = Mathf.Sin(tau * (2f * u + v))
                               + 0.6f * Mathf.Sin(tau * (3f * v - u))
                               + 0.4f * Mathf.Sin(tau * (5f * u + 4f * v));
                    float t = Mathf.InverseLerp(-2f, 2f, wave);
                    int index = Mathf.Clamp(Mathf.FloorToInt(t * t * palette.Length * 1.3f), 0, palette.Length - 1);
                    pixels[x + y * n] = palette[index];
                }
            }
            _placeholderSea = MakeSprite("MapFireSea", n, n, pixels);
            return _placeholderSea;
        }

        // A stone oval with a ragged edge, a dark cliff band round the rim and a few lava cracks.
        private static Sprite PlaceholderIsland()
        {
            if (_placeholderIsland != null)
                return _placeholderIsland;
            int w = IslandWidth;
            int h = IslandHeight;
            var pixels = new Color32[w * h];
            var random = new System.Random(5);
            var stone = new Color32(74, 64, 72, 255);
            var stoneLight = new Color32(96, 84, 92, 255);
            var cliff = new Color32(38, 30, 38, 255);
            var crack = new Color32(255, 110, 30, 255);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x + 0.5f) / w * 2f - 1f;
                    float dy = (y + 0.5f) / h * 2f - 1f;
                    float angle = Mathf.Atan2(dy, dx);
                    // Whole-number wave counts, so the edge closes up all the way round.
                    float edge = 0.9f + 0.05f * Mathf.Sin(angle * 7f) + 0.03f * Mathf.Sin(angle * 13f + 1f);
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / edge;
                    if (r > 1f)
                        continue; // transparent
                    Color32 colour = r > 0.9f ? cliff : ((x / 4 + y / 4) % 3 == 0 ? stoneLight : stone);
                    if (r <= 0.9f && random.Next(9) == 0)
                        colour = stoneLight;
                    pixels[x + y * w] = colour;
                }
            }
            // Cracks: short random walks of glowing pixels.
            for (int c = 0; c < 10; c++)
            {
                int x = w / 2 + random.Next(-w / 3, w / 3);
                int y = h / 2 + random.Next(-h / 3, h / 3);
                for (int step = 0; step < 14; step++)
                {
                    int i = x + y * w;
                    if (x <= 0 || y <= 0 || x >= w - 1 || y >= h - 1 || pixels[i].a == 0)
                        break;
                    pixels[i] = crack;
                    x += random.Next(-1, 2);
                    y += random.Next(-1, 2);
                }
            }
            _placeholderIsland = MakeSprite("MapIsland", w, h, pixels);
            return _placeholderIsland;
        }

        // A soft ring that is brightest just outside the island's edge. White, so MapSkin tints it.
        private static Sprite PlaceholderGlow()
        {
            if (_placeholderGlow != null)
                return _placeholderGlow;
            int w = IslandWidth / 2;
            int h = IslandHeight / 2;
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x + 0.5f) / w * 2f - 1f;
                    float dy = (y + 0.5f) / h * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    // The island fills r < 1/1.1 of this sprite; glow from there outwards.
                    float glow = 1f - Mathf.Abs(r - 0.86f) / 0.14f;
                    int steps = Mathf.FloorToInt(Mathf.Clamp01(glow) * 4f); // 0-4 hard steps, pixel style
                    var a = (byte)(Mathf.Min(steps, 3) * 85);
                    pixels[x + y * w] = new Color32(255, 255, 255, a);
                }
            }
            _placeholderGlow = MakeSprite("MapLavaGlow", w, h, pixels);
            return _placeholderGlow;
        }

        private static Sprite PlaceholderRock()
        {
            if (_placeholderRock != null)
                return _placeholderRock;
            int n = RockPixels;
            var pixels = new Color32[n * n];
            var top = new Color32(90, 78, 86, 255);
            var side = new Color32(42, 34, 42, 255);
            var hot = new Color32(230, 90, 25, 255);
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f;
                    float dy = (y + 0.5f) / n * 2f - 1f;
                    if (dx * dx + dy * dy * 1.6f > 0.85f)
                        continue;
                    pixels[x + y * n] = y < 3 ? hot : (y < n / 2 ? side : top);
                }
            }
            _placeholderRock = MakeSprite("MapRock", n, n, pixels);
            return _placeholderRock;
        }

        // A 2x2 white square; MapSkin tints it.
        private static Sprite PlaceholderEmber()
        {
            if (_placeholderEmber != null)
                return _placeholderEmber;
            var pixels = new Color32[4];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 255, 255, 255);
            _placeholderEmber = MakeSprite("MapEmber", 2, 2, pixels);
            return _placeholderEmber;
        }

        // One pixel per 1/16 world unit; Full Rect so it can be tiled.
        private static Sprite MakeSprite(string name, int width, int height, Color32[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                name = name
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f),
                16f, 0, SpriteMeshType.FullRect);
        }
    }
}
