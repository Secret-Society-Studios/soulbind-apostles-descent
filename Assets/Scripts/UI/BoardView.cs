using System.Collections.Generic;
using SoulBind.Core;
using SoulBind.Utility;
using UnityEngine;

namespace SoulBind.UI
{
    /// <summary>
    /// Draws the standard board from the HexTile prefab. Display only: every rule lives in SoulBind.Core.
    /// The board is built in Awake, so other scripts can use it from Start.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        [SerializeField] private GameObject hexTilePrefab;
        [SerializeField] private float hexSize = 0.5f;
        [SerializeField, Range(0.8f, 1f)] private float tileScale = 0.95f;
        [SerializeField] private Color tileColor = new Color(0.62f, 0.54f, 0.47f);
        [SerializeField] private Color edgeTileColor = new Color(0.80f, 0.70f, 0.56f);
        [SerializeField] private Color highlightColor = new Color(0.45f, 0.85f, 0.50f);
        [SerializeField] private Color healTileColor = new Color(0.90f, 0.40f, 0.45f);
        [SerializeField] private Color purifyTileColor = new Color(0.50f, 0.75f, 0.95f);
        [SerializeField] private Color soulTileColor = new Color(0.68f, 0.45f, 0.90f);
        [SerializeField] private Color ascensionTileColor = new Color(1.00f, 0.80f, 0.20f);
        [SerializeField] private Color ascensionHighlightColor = new Color(1.00f, 0.95f, 0.60f);
        [SerializeField] private Color attackTargetColor = new Color(0.45f, 0.08f, 0.10f);

        [Header("Tile art (optional: an empty slot keeps the placeholder hex and color)")]
        [Tooltip("A normal tile. Also used for edge tiles if Edge Tile Sprite is empty.")]
        [SerializeField] private Sprite plainTileSprite;
        [SerializeField] private Sprite edgeTileSprite;
        [SerializeField] private Sprite healTileSprite;
        [SerializeField] private Sprite purifyTileSprite;
        [SerializeField] private Sprite soulTileSprite;
        [SerializeField] private bool fitCameraToBoard = true;
        [SerializeField] private float cameraMargin = 0.4f;

        [Header("Table view")]
        [Tooltip("How far the camera tilts away from straight down, in degrees.")]
        [SerializeField, Range(0f, 60f)] private float cameraTilt = 35f;
        [SerializeField, Range(20f, 70f)] private float fieldOfView = 35f;
        [Tooltip("Placeholder table color until table art exists.")]
        [SerializeField] private Color tableColor = new Color(0.32f, 0.21f, 0.13f);
        [Tooltip("Placeholder play-mat color under the board.")]
        [SerializeField] private Color matColor = new Color(0.16f, 0.14f, 0.20f);
        [SerializeField] private float matPadding = 0.6f;
        [Tooltip("Width of the table area kept on screen right of the mat, for the discard pile (and the deck later).")]
        [SerializeField] private float sideAreaWidth = 2f;
        [Tooltip("Gap between the mat and the side area.")]
        [SerializeField] private float sideAreaGap = 0.4f;
        [Tooltip("Depth of the table strip kept on screen above the mat, for the opponents' face-down hands.")]
        [SerializeField] private float topAreaDepth = 1.9f;
        [Tooltip("Gap between the mat and the top strip.")]
        [SerializeField] private float topAreaGap = 0.2f;

        private readonly Dictionary<HexCoord, SpriteRenderer> _tiles = new Dictionary<HexCoord, SpriteRenderer>();
        private readonly Dictionary<HexCoord, SpecialTileType> _specialTypes = new Dictionary<HexCoord, SpecialTileType>();
        private readonly List<HexCoord> _highlighted = new List<HexCoord>(6);
        private readonly List<HexCoord> _attackMarks = new List<HexCoord>(6);
        private readonly List<HexCoord> _changedTiles = new List<HexCoord>(16);
        private Sprite _placeholderSprite;
        private bool _hasAscensionTile;
        private HexCoord _ascensionTile;
        private float _centerX;
        private float _centerY;
        private float _boardWidth;
        private float _boardHeight;
        private float _reservedLeft;
        private float _reservedTop;
        private float _reservedBottom;

        public HexBoard Board { get; private set; }

        private void Awake()
        {
            Build();
        }

        /// <summary>The world position of a tile's center, with the board centered on this object.</summary>
        public Vector3 GetWorldPosition(HexCoord tile)
        {
            tile.ToPlane(hexSize, out float x, out float y);
            return transform.position + new Vector3(x - _centerX, y - _centerY, 0f);
        }

        /// <summary>Finds the board tile under a world point. Returns false if the point is off the board.</summary>
        public bool TryGetTileAt(Vector3 worldPoint, out HexCoord tile)
        {
            tile = default;
            if (Board == null)
                return false;

            Vector3 local = worldPoint - transform.position;
            tile = HexCoord.FromPlane(local.x + _centerX, local.y + _centerY, hexSize);
            return Board.Contains(tile);
        }

        /// <summary>
        /// Colors exactly these tiles as special tiles. Call it again after any change, and
        /// call SetHighlights after it so highlights stay on top.
        /// </summary>
        public void ShowSpecialTiles(IReadOnlyDictionary<HexCoord, SpecialTileType> active)
        {
            // Turn the old special tiles back into ordinary ones first.
            _changedTiles.Clear();
            foreach (HexCoord tile in _specialTypes.Keys)
                _changedTiles.Add(tile);
            _specialTypes.Clear();
            for (int i = 0; i < _changedTiles.Count; i++)
                ApplyBase(_changedTiles[i]);

            foreach (KeyValuePair<HexCoord, SpecialTileType> pair in active)
            {
                if (_tiles.TryGetValue(pair.Key, out SpriteRenderer spriteRenderer))
                {
                    _specialTypes[pair.Key] = pair.Value;
                    ApplyBase(pair.Key);
                }
            }
        }

        /// <summary>
        /// Shows the Ascension tile in gold, or hides it when visible is false.
        /// Call SetHighlights after it so highlights stay on top.
        /// </summary>
        public void ShowAscensionTile(bool visible, HexCoord tile)
        {
            if (_hasAscensionTile)
            {
                _hasAscensionTile = false;
                ApplyBase(_ascensionTile);
            }

            if (visible && _tiles.TryGetValue(tile, out SpriteRenderer spriteRenderer))
            {
                _hasAscensionTile = true;
                _ascensionTile = tile;
                spriteRenderer.color = ascensionTileColor;
            }
        }

        /// <summary>Highlights exactly these tiles, clearing any earlier highlights.</summary>
        public void SetHighlights(IReadOnlyList<HexCoord> tiles)
        {
            ClearHighlights();
            for (int i = 0; i < tiles.Count; i++)
            {
                if (_tiles.TryGetValue(tiles[i], out SpriteRenderer spriteRenderer))
                {
                    spriteRenderer.color = IsAscensionTile(tiles[i]) ? ascensionHighlightColor : highlightColor;
                    _highlighted.Add(tiles[i]);
                }
            }
        }

        public void ClearHighlights()
        {
            for (int i = 0; i < _highlighted.Count; i++)
                ApplyBase(_highlighted[i]);
            _highlighted.Clear();
        }

        /// <summary>
        /// Marks the tiles under attackable Apostles, clearing any earlier marks.
        /// Call it last, after ShowSpecialTiles, ShowAscensionTile and SetHighlights.
        /// </summary>
        public void SetAttackMarks(IReadOnlyList<HexCoord> tiles)
        {
            ClearAttackMarks();
            for (int i = 0; i < tiles.Count; i++)
            {
                if (_tiles.TryGetValue(tiles[i], out SpriteRenderer spriteRenderer))
                {
                    spriteRenderer.color = attackTargetColor;
                    _attackMarks.Add(tiles[i]);
                }
            }
        }

        /// <summary>Removes the attack marks. Call it first, before the other board updates.</summary>
        public void ClearAttackMarks()
        {
            for (int i = 0; i < _attackMarks.Count; i++)
                ApplyBase(_attackMarks[i]);
            _attackMarks.Clear();
        }

        /// <summary>The share of the screen height kept clear at the top (for the HUD strip).</summary>
        public float ReservedTopFraction => _reservedTop;

        /// <summary>Keeps this share of the screen width clear on the left (0 to 0.8) and re-fits the camera.</summary>
        public void SetReservedLeft(float fraction)
        {
            _reservedLeft = Mathf.Clamp(fraction, 0f, 0.8f);
            FitCamera();
        }

        /// <summary>
        /// The middle of the table area right of the mat, on the board's plane. The camera always keeps
        /// this area on screen. The discard pile sits here.
        /// </summary>
        /// <summary>
        /// The middle of the table strip above the mat (the far side of the table), on the board's plane.
        /// The camera always keeps it on screen. The opponents' face-down hands sit here.
        /// </summary>
        public Vector3 TopAreaCenter =>
            transform.position + new Vector3(0f, _boardHeight / 2f + matPadding + topAreaGap + topAreaDepth / 2f, 0f);

        /// <summary>Width of the top strip (as wide as the mat), in world units.</summary>
        public float TopAreaWidth => _boardWidth + 2f * matPadding;

        /// <summary>Depth of the top strip, in world units.</summary>
        public float TopAreaDepth => topAreaDepth;

        public Vector3 SideAreaCenter =>
            transform.position + new Vector3(_boardWidth / 2f + matPadding + sideAreaGap + sideAreaWidth / 2f, 0f, 0f);

        /// <summary>The share of the screen height kept clear at the bottom (for the player's strip and hand).</summary>
        public float ReservedBottomFraction => _reservedBottom;

        /// <summary>
        /// Keeps these shares of the screen height clear at the top and bottom (0 to 0.4 each)
        /// and re-fits the camera.
        /// </summary>
        public void SetReservedTopAndBottom(float top, float bottom)
        {
            _reservedTop = Mathf.Clamp(top, 0f, 0.4f);
            _reservedBottom = Mathf.Clamp(bottom, 0f, 0.4f);
            FitCamera();
        }

        /// <summary>
        /// Finds the board tile under a screen position (for example the mouse), by casting a ray
        /// from the camera onto the board's plane. Works with the tilted perspective camera.
        /// </summary>
        public bool TryGetTileAtScreen(Vector2 screenPoint, out HexCoord tile)
        {
            tile = default;
            Camera cam = Camera.main;
            if (cam == null || Board == null)
                return false;

            Ray ray = cam.ScreenPointToRay(screenPoint);
            var boardPlane = new Plane(Vector3.back, transform.position);
            if (!boardPlane.Raycast(ray, out float distance))
                return false;

            return TryGetTileAt(ray.GetPoint(distance), out tile);
        }

        // Points the tilted perspective camera at the board so the whole mat fits inside the part
        // of the screen the panels leave free, centered in it.
        private void FitCamera()
        {
            if (!fitCameraToBoard || Board == null)
                return;

            Camera cam = Camera.main;
            if (cam == null)
                return;

            cam.orthographic = false;
            cam.fieldOfView = fieldOfView;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000f;
            // The board lies in the XY plane facing -Z. Tilting up looks at it from the player's side.
            cam.transform.rotation = Quaternion.Euler(-cameraTilt, 0f, 0f);

            // The free part of the screen, in viewport units (0 to 1).
            float freeMinX = _reservedLeft;
            // The right edge is free: the buttons and portrait on the right only cover the bottom corner,
            // and the turn info the top corner, both outside the band the board and piles use.
            float freeMaxX = 1f;
            float freeMinY = _reservedBottom;
            float freeMaxY = 1f - _reservedTop;
            var freeCenter = new Vector2((freeMinX + freeMaxX) / 2f, (freeMinY + freeMaxY) / 2f);
            float freeWidth = freeMaxX - freeMinX;
            float freeHeight = freeMaxY - freeMinY;

            Vector3 boardCenter = transform.position;
            Vector3 forward = cam.transform.forward;

            // 1. Move back until the whole mat fits in the free area.
            float near = 1f;
            float far = 500f;
            for (int i = 0; i < 30; i++)
            {
                float middle = (near + far) / 2f;
                cam.transform.position = boardCenter - forward * middle;
                GetViewportBounds(cam, true, out Vector2 min, out Vector2 max);
                if (max.x - min.x <= freeWidth && max.y - min.y <= freeHeight)
                    far = middle;
                else
                    near = middle;
            }
            cam.transform.position = boardCenter - forward * far;

            // 2. Slide sideways and up/down. The board itself is centered in the screen width the left
            //    panel leaves, then nudged left only as far as needed to keep the piles on screen.
            float frustumHeight = 2f * far * Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
            float frustumWidth = frustumHeight * cam.aspect;
            float boardTargetX = (freeMinX + 1f) / 2f;
            for (int i = 0; i < 6; i++)
            {
                GetViewportBounds(cam, false, out Vector2 boardMin, out Vector2 boardMax);
                GetViewportBounds(cam, true, out Vector2 allMin, out Vector2 allMax);

                // A positive offset moves everything left on screen.
                float offsetX = (boardMin.x + boardMax.x) / 2f - boardTargetX;
                offsetX = Mathf.Max(offsetX, allMax.x - freeMaxX); // piles stay on screen
                offsetX = Mathf.Min(offsetX, allMin.x - freeMinX); // board stays right of the left panel
                float offsetY = (allMin.y + allMax.y) / 2f - freeCenter.y;

                cam.transform.position += cam.transform.right * (offsetX * frustumWidth)
                                        + cam.transform.up * (offsetY * frustumHeight);
            }
        }

        // The screen-space box (viewport units) around the four corners of the mat plus margin,
        // the strip above it for the opponents' hands, and with includeSide, the side area on its right
        // for the piles too.
        private void GetViewportBounds(Camera cam, bool includeSide, out Vector2 min, out Vector2 max)
        {
            float left = -(_boardWidth / 2f + matPadding + cameraMargin);
            float right = _boardWidth / 2f + matPadding + cameraMargin
                          + (includeSide ? sideAreaGap + sideAreaWidth : 0f);
            float halfHeight = _boardHeight / 2f + matPadding + cameraMargin;
            float top = halfHeight + topAreaGap + topAreaDepth; // the opponents' hands above the mat
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);

            for (int corner = 0; corner < 4; corner++)
            {
                float x = (corner & 1) == 0 ? left : right;
                float y = (corner & 2) == 0 ? -halfHeight : top;
                Vector3 viewport = cam.WorldToViewportPoint(transform.position + new Vector3(x, y, 0f));
                if (viewport.z <= 0f)
                {
                    // A corner behind the camera: treat the mat as far too big to fit.
                    min = new Vector2(-1000f, -1000f);
                    max = new Vector2(1000f, 1000f);
                    return;
                }
                min = Vector2.Min(min, viewport);
                max = Vector2.Max(max, viewport);
            }
        }

        // A plain table under everything and a play mat under the board (placeholders for art).
        private void CreateTableAndMat()
        {
            var texture = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            Sprite square = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);

            CreateFlat("Table", square, tableColor, -20,
                new Vector3(_boardWidth * 6f, _boardHeight * 6f, 1f));
            CreateFlat("Mat", square, matColor, -10,
                new Vector3(_boardWidth + 2f * matPadding, _boardHeight + 2f * matPadding, 1f));
        }

        private void CreateFlat(string objectName, Sprite sprite, Color color, int sortingOrder, Vector3 scale)
        {
            var flat = new GameObject(objectName);
            flat.transform.SetParent(transform, false);
            flat.transform.localScale = scale;
            var spriteRenderer = flat.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = sortingOrder;
        }

        private bool IsAscensionTile(HexCoord tile) => _hasAscensionTile && _ascensionTile == tile;

        // Puts a tile back to how it looks when nothing is highlighted or marked: its art (or the
        // placeholder hex), sized to the board, with its color. Highlights then tint over the art.
        private void ApplyBase(HexCoord tile)
        {
            SpriteRenderer spriteRenderer = _tiles[tile];
            Sprite art = ArtFor(tile);
            Sprite sprite = art != null ? art : _placeholderSprite;

            if (spriteRenderer.sprite != sprite)
            {
                spriteRenderer.sprite = sprite;
                spriteRenderer.transform.localScale = Vector3.one * ScaleFor(sprite);
            }

            if (IsAscensionTile(tile))
                spriteRenderer.color = ascensionTileColor;
            else if (art != null)
                spriteRenderer.color = Color.white; // show the art as drawn
            else if (_specialTypes.TryGetValue(tile, out SpecialTileType type))
                spriteRenderer.color = SpecialColor(type);
            else
                spriteRenderer.color = Board.IsEdge(tile) ? edgeTileColor : tileColor;
        }

        // The team's art for this tile, or null to use the placeholder.
        private Sprite ArtFor(HexCoord tile)
        {
            if (_specialTypes.TryGetValue(tile, out SpecialTileType type))
            {
                switch (type)
                {
                    case SpecialTileType.Heal: return healTileSprite;
                    case SpecialTileType.Purify: return purifyTileSprite;
                    default: return soulTileSprite;
                }
            }
            if (Board.IsEdge(tile) && edgeTileSprite != null)
                return edgeTileSprite;
            return plainTileSprite;
        }

        // Scales any sprite so it is as tall as a hex tile, whatever its size or Pixels Per Unit.
        private float ScaleFor(Sprite sprite)
        {
            float height = sprite.bounds.size.y;
            if (height <= 0f)
                return tileScale;
            return tileScale * (2f * hexSize) / height;
        }

        private Color SpecialColor(SpecialTileType type)
        {
            switch (type)
            {
                case SpecialTileType.Heal: return healTileColor;
                case SpecialTileType.Purify: return purifyTileColor;
                default: return soulTileColor;
            }
        }

        private void Build()
        {
            if (hexTilePrefab == null)
            {
                Debug.LogError("BoardView: assign the HexTile prefab in the Inspector.", this);
                return;
            }

            HexBoard board = StandardBoard.Create();
            _placeholderSprite = HexSpriteFactory.Create(hexSize);
            Vector2[] outline = HexSpriteFactory.GetCorners(hexSize);

            // Find the board's bounds so it can be centered on this object.
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (HexCoord tile in board.Tiles)
            {
                tile.ToPlane(hexSize, out float x, out float y);
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
            _centerX = (minX + maxX) / 2f;
            _centerY = (minY + maxY) / 2f;
            _boardWidth = (maxX - minX) + Mathf.Sqrt(3f) * hexSize;
            _boardHeight = (maxY - minY) + 2f * hexSize;

            foreach (HexCoord tile in board.Tiles)
            {
                GameObject instance = Instantiate(hexTilePrefab, GetWorldPosition(tile), Quaternion.identity, transform);
                instance.name = $"Hex ({tile.Q},{tile.R})";
                instance.transform.localScale = Vector3.one * tileScale;

                var spriteRenderer = instance.GetComponent<SpriteRenderer>();
                if (spriteRenderer == null)
                {
                    Debug.LogError("BoardView: the HexTile prefab needs a SpriteRenderer.", hexTilePrefab);
                    return;
                }
                spriteRenderer.sprite = _placeholderSprite;
                _tiles.Add(tile, spriteRenderer);

                var collider = instance.GetComponent<PolygonCollider2D>();
                if (collider != null)
                {
                    collider.pathCount = 1;
                    collider.SetPath(0, outline);
                }
            }

            Board = board;

            // Give every tile its art (or placeholder) and color.
            foreach (HexCoord tile in board.Tiles)
                ApplyBase(tile);
            Debug.Log($"BoardView: built {_tiles.Count} tiles ({board.EdgeTiles.Count} edge).");

            CreateTableAndMat();

            FitCamera();
        }
    }
}