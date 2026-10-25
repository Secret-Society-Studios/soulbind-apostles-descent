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
        [SerializeField] private float cameraMargin = 0.2f;

        [Header("Table view")]
        [Tooltip("How far the camera tilts away from straight down, in degrees.")]
        [SerializeField, Range(0f, 60f)] private float cameraTilt = 35f;
        [SerializeField, Range(20f, 70f)] private float fieldOfView = 35f;
        [Tooltip("Placeholder table color until table art exists.")]
        [SerializeField] private Color tableColor = new Color(0.32f, 0.21f, 0.13f);
        [Tooltip("Placeholder play-mat color under the board.")]
        [SerializeField] private Color matColor = new Color(0.16f, 0.14f, 0.20f);
        [SerializeField] private float matPadding = 0.6f;
        [Tooltip("Width of the lighter rim around the hexagonal mat, in world units.")]
        [SerializeField] private float matRimWidth = 0.15f;
        [Tooltip("How steep the mat's four slanted sides are, in degrees from flat. 60 = a regular hexagon; " + "lower makes a wider mat with longer top and bottom sides.")]
        [SerializeField, Range(30f, 60f)] private float matSideAngle = 45f;
        [Tooltip("Extra room above and below the board, in world units (added to both the top and the bottom). " + "The slanted sides move out with it, so the top and bottom sides keep their length.")]
        [SerializeField, Min(0f)] private float matExtraHeight = 1f;
        
        [Header("Tapping tiles")]
        [Tooltip("A tap this close to a tile you can pick (a highlighted move, an Apostle you can attack) " + "picks it, even if the finger landed on a neighbouring tile. In centimetres on the screen. " + "Tiles far from the camera look small, so this makes them much easier to hit on a phone.")]
        [SerializeField, Range(0f, 2f)] private float tapRadiusCm = 0.8f;

        [Header("Piles (on the mat, in its left and right corners)")]
        [Tooltip("Height of a pile's card, in world units. The mat grows if the piles need more room.")]
        [SerializeField] private float pileCardHeight = 2.4f;
        [Tooltip("Gap between the board's tiles and the piles, in world units.")]
        [SerializeField] private float pileGap = 0.3f;

        [Header("Seats (a hand along each side of the mat)")]
        [Tooltip("Gap between the mat's edge and the hands lying along it, in world units.")]
        [SerializeField] private float seatGap = 0.15f;
        [Tooltip("How deep each hand's place is, from the mat's edge outward, in world units (cards plus name).")]
        [SerializeField] private float seatDepth = 2.3f;

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
        private float _matRadius;     // center to the left and right corners
        private float _matHalfHeight; // center to the flat top and bottom sides
        private float _matHalfTop;    // half the length of the top and bottom sides
        private float _pileOffsetX;
        private const float PileCardAspect = 180f / 252f; // width / height
        private const float PileCountRoom = 0.35f;         // the card count under a pile, in world units

        private const float Sqrt3 = 1.7320508f;
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

        /// <summary>
        /// Like TryGetTileAtScreen, but forgiving for fingers: if the tap is near one of the tiles that can
        /// be picked right now, that tile wins, even when the exact point fell on a neighbouring tile.
        /// Among several, the one whose center looks closest on the screen wins. A tap far from all of
        /// them falls back to the exact tile under the point.
        /// </summary>
        public bool TryPickTile(Vector2 screenPoint, IReadOnlyList<HexCoord> pickable, out HexCoord tile)
        {
            bool hit = TryGetTileAtScreen(screenPoint, out tile);
            if (pickable == null || pickable.Count == 0)
                return hit;
            
            // A tap right on a pickable tile always picks it.
            if (hit)
            {
                for (int i = 0; i < pickable.Count; i++)
                {
                    if (pickable[i].Equals(tile))
                        return true;
                }
            }

            Camera cam = Camera.main;
            if (cam == null)
                return hit;

            float dpi = Screen.dpi > 0f ? Screen.dpi : 96f; // some devices report 0
            float radius = tapRadiusCm / 2.54f * dpi;
            float best = radius * radius;
            bool found = false;
            for (int i = 0; i < pickable.Count; i++)
            {
                Vector3 onScreen = cam.WorldToScreenPoint(GetWorldPosition(pickable[i]));
                if (onScreen.z <= 0f)
                    continue; // behind the camera
                float distance = ((Vector2)onScreen - screenPoint).sqrMagnitude;
                if (distance <= best)
                {
                    best = distance;
                    tile = pickable[i];
                    found = true;
                }
            }
            return found || hit;
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

        /// <summary>Height of a pile's card on the mat, in world units.</summary>
        public float PileCardHeight => pileCardHeight;

        /// <summary>Where the draw pile lies: on the mat, in its left corner, beside the board.</summary>
        public Vector3 DrawPileSpot => transform.position + new Vector3(-_pileOffsetX, 0f, 0f);

        /// <summary>Where the discard pile lies: on the mat, in its right corner, beside the board.</summary>
        public Vector3 DiscardPileSpot => transform.position + new Vector3(_pileOffsetX, 0f, 0f);

        /// <summary>The mat's half-width (center to its left and right corners), in world units.</summary>
        public float MatRadius => _matRadius;

        /// <summary>The mat's half-height (center to its flat top and bottom sides), in world units.</summary>
        public float MatHalfHeight => _matHalfHeight;

        /// <summary>The radius of a circle that encloses the mat and the hands lying around it.</summary>
        public float SeatRingRadius => Mathf.Max(_matRadius, _matHalfHeight) + seatGap + seatDepth;

        /// <summary>
        /// One of the mat's six sides, counter-clockwise from the bottom (0 bottom, 1 bottom-right,
        /// 2 top-right, 3 top, 4 top-left, 5 bottom-left): the middle of the place where a hand lies along it,
        /// how far its cards are turned so their tops face the board (degrees), the side's length and the
        /// place's depth, all in world units on the board's plane. The mat is a hexagon stretched sideways:
        /// the top and bottom sides are longer than the four slanted ones.
        /// </summary>
        public void GetSeat(int side, out Vector3 center, out float rotation, out float length, out float depth)
        {
            GetSide(side, out Vector2 middle, out Vector2 normal, out length, out float normalDegrees);
            Vector2 place = middle + normal * (seatGap + seatDepth / 2f);
            center = transform.position + new Vector3(place.x, place.y, 0f);
            rotation = normalDegrees + 90f;
            depth = seatDepth;
        }

        // One side of the mat, relative to its center: its middle point, the direction pointing out of it
        // (and that direction's angle, in degrees), and its length.
        private void GetSide(int side, out Vector2 middle, out Vector2 normal, out float length, out float normalDegrees)
        {
            side = ((side % 6) + 6) % 6;
            float slant = matSideAngle;
            float slantLength = _matHalfHeight / Mathf.Sin(slant * Mathf.Deg2Rad);
            float slantX = (_matHalfTop + _matRadius) / 2f;
            float halfY = _matHalfHeight / 2f;
            switch (side)
            {
                case 0: middle = new Vector2(0f, -_matHalfHeight); normalDegrees = 270f; length = 2f * _matHalfTop; break;
                case 1: middle = new Vector2(slantX, -halfY); normalDegrees = 270f + slant; length = slantLength; break;
                case 2: middle = new Vector2(slantX, halfY); normalDegrees = 90f - slant; length = slantLength; break;
                case 3: middle = new Vector2(0f, _matHalfHeight); normalDegrees = 90f; length = 2f * _matHalfTop; break;
                case 4: middle = new Vector2(-slantX, halfY); normalDegrees = 90f + slant; length = slantLength; break;
                default: middle = new Vector2(-slantX, -halfY); normalDegrees = 270f - slant; length = slantLength; break;
            }
            float radians = normalDegrees * Mathf.Deg2Rad;
            normal = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

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
                GetViewportBounds(cam, out Vector2 min, out Vector2 max);
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
                GetViewportBounds(cam, out Vector2 boardMin, out Vector2 boardMax);
                GetViewportBounds(cam, out Vector2 allMin, out Vector2 allMax);

                // A positive offset moves everything left on screen.
                float offsetX = (boardMin.x + boardMax.x) / 2f - boardTargetX;
                offsetX = Mathf.Max(offsetX, allMax.x - freeMaxX); // piles stay on screen
                offsetX = Mathf.Min(offsetX, allMin.x - freeMinX); // board stays right of the left panel
                float offsetY = (allMin.y + allMax.y) / 2f - freeCenter.y;

                cam.transform.position += cam.transform.right * (offsetX * frustumWidth)
                                        + cam.transform.up * (offsetY * frustumHeight);
            }
        }

        // The screen-space box (viewport units) around the mat and the six places where hands lie,
        // plus margin. Each place is measured by its own corners, so the empty table at the hexagon's
        // outer corners is not counted and everything can be shown bigger.
        private void GetViewportBounds(Camera cam, out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);

            float outer = seatGap + seatDepth + cameraMargin;
            for (int side = 0; side < 6; side++)
            {
                GetSide(side, out Vector2 middle, out Vector2 normal2, out float length, out _);
                var normal = new Vector3(normal2.x, normal2.y, 0f);
                var along = new Vector3(-normal.y, normal.x, 0f);
                var start = new Vector3(middle.x, middle.y, 0f);
                float halfLength = length / 2f + cameraMargin;
                for (int corner = 0; corner < 4; corner++)
                {
                    float distance = (corner & 1) == 0 ? 0f : outer;
                    float offset = (corner & 2) == 0 ? -halfLength : halfLength;
                    if (!AddToBounds(cam, start + normal * distance + along * offset, ref min, ref max))
                        return;
                }
            }
        }

        // Grows the box by one point (relative to the board's center). Returns false, with a box far too
        // big to fit, if the point is behind the camera.
        private bool AddToBounds(Camera cam, Vector3 offset, ref Vector2 min, ref Vector2 max)
        {
            Vector3 viewport = cam.WorldToViewportPoint(transform.position + offset);
            if (viewport.z <= 0f)
            {
                min = new Vector2(-1000f, -1000f);
                max = new Vector2(1000f, 1000f);
                return false;
            }
            min = Vector2.Min(min, viewport);
            max = Vector2.Max(max, viewport);
            return true;
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

            // A hexagon stretched sideways: flat top and bottom sides, corners pointing left and right,
            // and four slanted sides at matSideAngle. The flat bottom side faces the player, whose hand
            // lies along it. First the smallest such hexagon around every tile, matPadding away from them.
            float slant = matSideAngle * Mathf.Deg2Rad;
            float invTan = 1f / Mathf.Tan(slant);
            float reachY = 0f;
            float reachSlant = 0f; // the largest x + y / tan(slant) of any tile corner
            foreach (HexCoord tile in Board.Tiles)
            {
                tile.ToPlane(hexSize, out float cx, out float cy);
                cx -= _centerX;
                cy -= _centerY;
                for (int corner = 0; corner < 6; corner++)
                {
                    // The tiles are pointy-top: their corners are at 30, 90, 150... degrees.
                    float angle = (30f + 60f * corner) * Mathf.Deg2Rad;
                    float x = Mathf.Abs(cx + hexSize * Mathf.Cos(angle));
                    float y = Mathf.Abs(cy + hexSize * Mathf.Sin(angle));
                    reachY = Mathf.Max(reachY, y);
                    reachSlant = Mathf.Max(reachSlant, x + y * invTan);
                }
            }
            _matHalfHeight = reachY + matPadding;
            _matRadius = reachSlant + matPadding / Mathf.Sin(slant);

            // The piles lie in the mat's left and right corners, beside the board, at mid-height.
            // Find how far the tiles reach sideways in that band, then grow the mat if a pile would not fit.
            float pileHalfWidth = pileCardHeight * PileCardAspect / 2f + 0.15f; // a little room for tilted cards
            float pileHalfHeight = pileCardHeight / 2f + PileCountRoom;
            float tilesReach = 0f;
            foreach (HexCoord tile in Board.Tiles)
            {
                tile.ToPlane(hexSize, out float cx, out float cy);
                cx -= _centerX;
                cy -= _centerY;
                for (int corner = 0; corner < 6; corner++)
                {
                    float angle = (30f + 60f * corner) * Mathf.Deg2Rad;
                    float y = Mathf.Abs(cy + hexSize * Mathf.Sin(angle));
                    if (y <= pileHalfHeight + pileGap)
                        tilesReach = Mathf.Max(tilesReach, Mathf.Abs(cx + hexSize * Mathf.Cos(angle)));
                }
            }
            _pileOffsetX = tilesReach + pileGap + pileHalfWidth;
            float pileNeeds = _pileOffsetX + pileHalfWidth + pileHalfHeight * invTan + (matRimWidth + 0.1f) / Mathf.Sin(slant);
            _matRadius = Mathf.Max(_matRadius, pileNeeds);
            _matHalfHeight = Mathf.Max(_matHalfHeight, pileHalfHeight + matRimWidth + 0.1f);
            // Extra height above and below the board; the slanted sides move out to match.
            _matHalfHeight += matExtraHeight;
            _matRadius += matExtraHeight * invTan;

            // The top and bottom sides must keep some length, or the hexagon turns into a diamond.
            _matRadius = Mathf.Max(_matRadius, _matHalfHeight * invTan + 0.5f);
            _matHalfTop = _matRadius - _matHalfHeight * invTan;

            Sprite hexagon = CreateHexagonSprite(_matRadius, _matHalfHeight, matSideAngle, matColor,
                Color.Lerp(matColor, Color.white, 0.25f), matRimWidth);
            CreateFlat("Mat", hexagon, Color.white, -10, Vector3.one);
        }

        // Draws the stretched hexagon (with a soft edge and a lighter rim) into a texture, sized in world
        // units: halfWidth to the left and right corners, halfHeight to the flat top and bottom, slanted
        // sides at sideAngle degrees. Made once when the board is built.
        private static Sprite CreateHexagonSprite(float halfWidth, float halfHeight, float sideAngle, Color fill,
            Color rim, float rimWidth)
        {
            const int LongSide = 1024;
            float pixelsPerUnit = LongSide / (2f * Mathf.Max(halfWidth, halfHeight));
            int width = Mathf.CeilToInt(2f * halfWidth * pixelsPerUnit);
            int height = Mathf.CeilToInt(2f * halfHeight * pixelsPerUnit);
            float sin = Mathf.Sin(sideAngle * Mathf.Deg2Rad);
            float invTan = 1f / Mathf.Tan(sideAngle * Mathf.Deg2Rad);

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[width * height];
            Color32 clear = new Color32(0, 0, 0, 0);
            float rimPixels = rimWidth * pixelsPerUnit;

            for (int py = 0; py < height; py++)
            {
                for (int px = 0; px < width; px++)
                {
                    // Position relative to the center, in world units.
                    float x = Mathf.Abs((px + 0.5f - width / 2f) / pixelsPerUnit);
                    float y = Mathf.Abs((py + 0.5f - height / 2f) / pixelsPerUnit);

                    // Distance outside the hexagon (negative inside), in pixels: past the flat top or
                    // bottom, or past a slanted side.
                    float outside = Mathf.Max(y - halfHeight, (x + y * invTan - halfWidth) * sin) * pixelsPerUnit;
                    float alpha = Mathf.Clamp01(0.5f - outside);
                    if (alpha <= 0f)
                    {
                        pixels[py * width + px] = clear;
                        continue;
                    }

                    Color color = -outside < rimPixels ? rim : fill;
                    color.a *= alpha;
                    pixels[py * width + px] = color;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true); // no longer readable: frees the CPU copy
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
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