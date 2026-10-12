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
        [SerializeField] private bool fitCameraToBoard = true;
        [SerializeField] private float cameraMargin = 1f;

        private readonly Dictionary<HexCoord, SpriteRenderer> _tiles = new Dictionary<HexCoord, SpriteRenderer>();
        private readonly Dictionary<HexCoord, SpecialTileType> _specialTypes = new Dictionary<HexCoord, SpecialTileType>();
        private readonly List<HexCoord> _highlighted = new List<HexCoord>(6);
        private float _centerX;
        private float _centerY;

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
            foreach (HexCoord tile in _specialTypes.Keys)
                _tiles[tile].color = PlainColor(tile);
            _specialTypes.Clear();

            foreach (KeyValuePair<HexCoord, SpecialTileType> pair in active)
            {
                if (_tiles.TryGetValue(pair.Key, out SpriteRenderer spriteRenderer))
                {
                    _specialTypes[pair.Key] = pair.Value;
                    spriteRenderer.color = SpecialColor(pair.Value);
                }
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
                    spriteRenderer.color = highlightColor;
                    _highlighted.Add(tiles[i]);
                }
            }
        }

        public void ClearHighlights()
        {
            for (int i = 0; i < _highlighted.Count; i++)
                _tiles[_highlighted[i]].color = BaseColor(_highlighted[i]);
            _highlighted.Clear();
        }

        private Color PlainColor(HexCoord tile) => Board.IsEdge(tile) ? edgeTileColor : tileColor;

        private Color BaseColor(HexCoord tile) =>
            _specialTypes.TryGetValue(tile, out SpecialTileType type) ? SpecialColor(type) : PlainColor(tile);

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
            Sprite sprite = HexSpriteFactory.Create(hexSize);
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
                spriteRenderer.sprite = sprite;
                spriteRenderer.color = board.IsEdge(tile) ? edgeTileColor : tileColor;
                _tiles.Add(tile, spriteRenderer);

                var collider = instance.GetComponent<PolygonCollider2D>();
                if (collider != null)
                {
                    collider.pathCount = 1;
                    collider.SetPath(0, outline);
                }
            }

            Board = board;
            Debug.Log($"BoardView: built {_tiles.Count} tiles ({board.EdgeTiles.Count} edge).");

            if (fitCameraToBoard)
            {
                float width = (maxX - minX) + Mathf.Sqrt(3f) * hexSize;
                float height = (maxY - minY) + 2f * hexSize;
                FitCamera(width, height);
            }
        }

        private void FitCamera(float boardWidth, float boardHeight)
        {
            Camera cam = Camera.main;
            if (cam == null || !cam.orthographic)
                return;

            float halfHeight = boardHeight / 2f + cameraMargin;
            float halfWidth = boardWidth / 2f + cameraMargin;
            cam.orthographicSize = Mathf.Max(halfHeight, halfWidth / cam.aspect);

            Vector3 cameraPosition = cam.transform.position;
            cam.transform.position = new Vector3(transform.position.x, transform.position.y, cameraPosition.z);
        }
    }
}