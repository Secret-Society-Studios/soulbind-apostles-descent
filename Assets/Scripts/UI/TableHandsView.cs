using System;
using System.Collections.Generic;
using SoulBind.Core;
using SoulBind.Managers;
using SoulBind.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SoulBind.UI
{
    /// <summary>
    /// Every Apostle's hand lies on the table along one side of the hexagonal mat, like players sitting
    /// around it. Yours is the bottom side, face-up; everyone else's is face-down, so only the number of
    /// cards shows. Each hand is a straight row in a tray, turned so the cards' tops face the board.
    ///
    /// Your cards: hover to lift one and see it large on screen (with the reason if it can't be played),
    /// click to play it, or drag it to the discard pile (play) or onto a red-ringed enemy (attack).
    /// Played cards fly to the discard pile (opponents' cards turn face-up as they land); drawn cards fly
    /// in face-down from the draw pile (yours turn face-up as they arrive). Online, other hands arrive as
    /// blank cards (MatchSnapshot.HiddenCard), so their played cards are found from the discard pile.
    ///
    /// Display and input only: every action goes through MatchController. Card views are pooled.
    /// </summary>
    public sealed class TableHandsView : MonoBehaviour
    {
        private const float CardWidth = 180f;
        private const float CardHeight = 252f;
        private const float LabelHeight = 56f;
        private const int ArcanaSlots = 32;

        [SerializeField] private MatchController matchController;
        [SerializeField] private CardView cardPrefab;
        [Tooltip("Gives the card back, and drawn cards fly from it.")]
        [SerializeField] private DrawPileView drawPile;
        [SerializeField] private DiscardPileView discardPile;
        [Tooltip("A large CardView on the screen, shown while hovering one of your cards.")]
        [SerializeField] private CardView preview;
        [Tooltip("Shown under the preview when the hovered card can't be played.")]
        [SerializeField] private TMP_Text previewReason;
        [Tooltip("On phones and tablets the card preview is this size compared with PC (1 = same size).")]
        [SerializeField, Range(0.3f, 1f)] private float mobilePreviewScale = 0.6f;
        [Tooltip("Tick to see the phone preview size while testing in the Editor.")]
        [SerializeField] private bool previewMobileInEditor;
        [Tooltip("How far above the middle of the screen the card preview sits.")]
        [SerializeField] private float previewRaise = 40f;

        [Header("Hands on the screen")]
        [Tooltip("On: your hand lies along the bottom of the screen with the Capacity bar, and the others' " +
                 "card counts are on their name plates. Off: every hand lies on the table around the board.")]
        [SerializeField] private bool handsOnScreen = true;
        [Tooltip("Your cards' size on a computer screen (1 = 252 pixels tall on a 1080-pixel-high screen).")]
        [SerializeField, Range(0.3f, 1.2f)] private float pcCardScale = 0.8f;
        [Tooltip("Your cards' size on a phone, where the screen is only a few inches tall. Tablets fall between.")]
        [SerializeField, Range(0.3f, 1.2f)] private float phoneCardScale = 0.75f;
        [Tooltip("Tick to see the phone hand size while testing in the Editor.")]
        [SerializeField] private bool handPhoneInEditor;
        [Tooltip("How wide your hand may get at the PC size, in pixels of a 1080-pixel-high screen. " +
                 "It shrinks with the cards, and never runs under the draw pile or your profile.")]
        [SerializeField] private float pcHandWidth = 1300f;
        [Tooltip("Room kept free on the left (draw pile) and right (your profile), same pixels.")]
        [SerializeField] private float screenLeftClear = 280f, screenRightClear = 360f;
        [Tooltip("Your hand's middle, left (-) or right (+) of the screen's middle, clear of your profile.")]
        [SerializeField] private float screenHandOffset = -120f;
        [SerializeField] private float screenBottomMargin = 12f;

        [Header("Cards on the table")]
        [Tooltip("Height of an opponent's card on the table, in world units (a hex tile is about 1).")]
        [SerializeField] private float cardWorldHeight = 1.4f;
        [Tooltip("Your own cards are this much bigger, so they are easier to read.")]
        [SerializeField] private float yourCardScale = 1.4f;
        [Tooltip("Size of the names under the hands (\"A3  5 cards\").")]
        [SerializeField] private float labelFontSize = 44f;
        [Tooltip("Gap between neighbouring cards, in canvas pixels. Cards overlap only when a hand is too long for its side.")]
        [SerializeField] private float cardGap = 10f;
        [SerializeField] private Color trayColor = new Color(0.04f, 0.04f, 0.07f, 0.8f);
        [SerializeField] private float trayPadding = 12f;
        [Tooltip("Opponents' name tags sit on the board side of " + "their cards, where the screen edge can't cut " + "them off. Untick for the old outer-edge spot.")]
        [SerializeField] private bool nameTagOnBoardSide = true;
        [Tooltip("The old name and card-count tag on each hand. Off: the name plates above the cards say who it is.")]
        [SerializeField] private bool showNameTags;
        [Tooltip("Opponents' hands bigger than this show this many face-down cards (the label still gives the real count). Your own hand always shows every card.")]
        [SerializeField, Range(1, 15)] private int maxCardsShown = 10;

        [Header("Standing cards")]
        [Tooltip("Opponents' cards stand up from the table by this many degrees, like a hand held up (0 = flat).")]
        [SerializeField, Range(0f, 80f)] private float standAngle = 60f;
        [Tooltip("Your own cards tilt up by this much. 0 keeps them flat: easiest to read, hover and drag.")]
        [SerializeField, Range(0f, 60f)] private float yourStandAngle = 0f;

        [Header("Glows around the hands")]
        [Tooltip("The hand of the Apostle whose turn it is glows in this colour (pulsing slowly).")]
        [SerializeField] private Color turnGlowColor = new Color(1f, 0.80f, 0.42f, 1f);
        [Tooltip("A hand whose Apostle has DEF above 0 glows in this colour.")]
        [SerializeField] private Color defenseGlowColor = new Color(0.45f, 0.70f, 1f, 1f);

        [Header("Your hand")]
        [Tooltip("How far a hovered card slides toward the board, in canvas pixels.")]
        [SerializeField] private float hoverLift = 70f;
        [SerializeField] private float hoverScale = 1.1f;
        [SerializeField] private float selectedLift = 45f;
        [SerializeField] private float moveSpeed = 14f;

        [Header("Your hand: fan (like holding cards)")]
        [Tooltip("Fans your hand in an arc, overlapping and tilted, like a hand of cards held up.")]
        [SerializeField] private bool fanYourHand = true;
        [Tooltip("How far the outer cards tilt, in degrees.")]
        [SerializeField, Range(0f, 30f)] private float fanAngle = 10f;
        [Tooltip("How much lower the outer cards sit than the middle ones, in canvas pixels.")]
        [SerializeField, Min(0f)] private float fanDrop = 34f;
        [Tooltip("How far apart neighbouring cards sit, as a share of a card's width (less = more overlap).")]
        [SerializeField, Range(0.3f, 1f)] private float fanOverlap = 0.72f;
        [Tooltip("Each card floats gently up and down, in canvas pixels. 0 = still. Off on Low graphics.")]
        [SerializeField, Min(0f)] private float idleBob = 4f;

        [Header("Flights")]
        [SerializeField] private float playFlightTime = 0.4f;
        [SerializeField] private float drawFlightTime = 0.5f;
        [Tooltip("How high a flying card arcs, in screen pixels.")]
        [SerializeField] private float arcHeight = 60f;

        [Header("Wheel of Fortune")]
        [Tooltip("How long the two swapped cards stay marked, in seconds.")]
        [SerializeField] private float swapMarkSeconds = 6f;
        [Tooltip("The glow and the SWAPPED tag around the two swapped cards.")]
        [SerializeField] private Color swapMarkColor = new Color(0.40f, 0.75f, 0.95f, 1f);

        private sealed class Seat
        {
            public int ApostleId;
            public bool IsYours;
            public float CardScale;
            public RectTransform Root;
            public RectTransform Tray;
            public Image TurnGlow;
            public Image DefenseGlow;
            public Outline Edge;
            public bool IsTurn;
            public bool HasDefense;
            public TMP_Text Label;
            public readonly List<CardView> Faces = new List<CardView>(12);
            public readonly List<RectTransform> Backs = new List<RectTransform>(12);
            public readonly List<ArcanaId> Shown = new List<ArcanaId>(12);
            public int Arriving;

            // Wheel of Fortune: the glow and tag around the card that was just swapped in this hand.
            public Image SwapMark;
            public TMP_Text SwapTag;
            public int SwapIndex = -1; // its place in the hand
            public int SwapHandSize; // the hand's size when it was swapped: any change ends the mark
            public float SwapTimeLeft;

            // Your own hand always shows every card; opponents' face-down hands are capped at max
            // (their label still gives the real count).
            public int SlotCount(int max) => IsYours ? Shown.Count : Mathf.Min(Shown.Count, max);
        }

        // Which sides are used for each player count, counter-clockwise from the bottom (yours).
        private static readonly int[][] SidesByPlayerCount =
        {
            new[] { 0 },
            new[] { 0 },
            new[] { 0, 3 },
            new[] { 0, 2, 4 },
            new[] { 0, 2, 3, 4 },
            new[] { 0, 1, 2, 4, 5 },
            new[] { 0, 1, 2, 3, 4, 5 },
        };

        private readonly List<Seat> _seats = new List<Seat>(GameConstants.MaxPlayers);
        private readonly int[] _cardCounts = new int[ArcanaSlots];
        private readonly int[] _newDiscards = new int[ArcanaSlots];
        private readonly Queue<ArcanaId> _playOrder = new Queue<ArcanaId>(8); // queued cards still to be shown
        private bool _playingCard;
        private readonly Vector3[] _corners = new Vector3[4];
        private RectTransform _rect;
        private Camera _camera;
        private CardFlightLayer _flights;
        private Seat _yours;
        private Match _builtFor;
        private int _builtViewer;
        private int _lastDiscardCount;
        private int _lastDrawCount;
        private int _lastSwapNumber;
        private bool _pendingRebuild;
        private bool _ready;
        private CardView _hovered;
        private CardView _dragged;
        // The card whose preview stays up after a tap, until the preview itself is tapped.
        private bool _hasPinned;
        private ArcanaId _pinned;
        private CardView _pinnedFace; // the copy that was tapped: another copy of the card stays down
        private CanvasGroup _previewGroup;
        private Camera _previewCamera;
        // Your Capacity, like an XP bar just above your hand: one pip per point, lit while unspent.
        private const int MaxCapacityPips = 16;
        private const float CapacityBarHeight = 22f;
        private const float CapacityGap = 16f; // between the tray and the bar
        private static readonly Color CapacityLit = new Color(0.45f, 0.85f, 0.35f, 1f);
        private static readonly Color CapacitySpent = new Color(0.10f, 0.08f, 0.13f, 0.9f);
        private readonly List<UnityEngine.UI.Image> _capacityPips = new List<UnityEngine.UI.Image>(MaxCapacityPips);
        private RectTransform _capacityBar;
        private TMP_Text _capacityText;
        // Under a pinned preview: a tap anywhere else closes it, and never reaches the board.
        private GameObject _previewBlocker;
        private int _pinnedFrame = -1; // the frame a card was pinned, so that same tap doesn't close it

        private void Awake()
        {
            if (preview != null)
            {
                // The preview must never catch the mouse, or hovering would flicker.
                CanvasGroup group = preview.GetComponent<CanvasGroup>();
                if (group == null)
                    group = preview.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;
                _previewGroup = group;
                CenterPreview();
                _previewBlocker = CreatePreviewBlocker();
                preview.gameObject.SetActive(false);
            }
            if (previewReason != null)
                previewReason.gameObject.SetActive(false);
            ShrinkPreviewOnPhones();
        }

        // The preview sits in the middle of the screen, a little high,
        // so the hand along the bottom stays visible.
        private void CenterPreview()
        {
            var middle = new Vector2(0.5f, 0.5f);
            RectTransform rect = preview.Rect;
            rect.anchorMin = rect.anchorMax = middle;
            rect.pivot = middle;
            rect.anchoredPosition = new Vector2(0f, previewRaise);
            rect.SetAsLastSibling();
            if (previewReason == null)
                return;

            // The "can't play" line sits just under the card.
            RectTransform reason = previewReason.rectTransform;
            reason.anchorMin = reason.anchorMax = middle;
            reason.pivot = new Vector2(0.5f, 1f);
            float half = rect.rect.height * 0.5f * rect.localScale.y;
            reason.anchoredPosition = new Vector2(0f, previewRaise - half - 10f);
            reason.SetAsLastSibling();
        }

        // A clear full-screen layer just under the preview.
        // It is only switched on while a card is pinned.
        private GameObject CreatePreviewBlocker()
        {
            var go = new GameObject("PreviewBlocker", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(preview.Rect.parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetSiblingIndex(preview.Rect.GetSiblingIndex()); // just under the preview
            var image = (Image)go.GetComponent(typeof(Image));
            image.color = Color.clear; // invisible, but still catches taps
            // Your own cards stay draggable through it.
            go.AddComponent<BlockerFilter>().Owner = this;
            go.SetActive(false);
            return go;
        }
        // True if this screen point is over one of your hand cards.
        private bool IsOverYourCard(Vector2 screenPoint)
        {
            if (_yours == null)
                return false;
            for (int i = 0; i < _yours.Faces.Count; i++)
            {
                CardView face = _yours.Faces[i];
                if (!face.gameObject.activeInHierarchy)
                    continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(face.Rect, screenPoint, _camera))
                    return true;
            }
            return false;
        }

        // Makes the preview blocker see-through where your hand cards are,
        // so a pinned preview never stops you dragging a card.
        private sealed class BlockerFilter : MonoBehaviour, ICanvasRaycastFilter
        {
            public TableHandsView Owner;

            // Lets clicks through to your cards when the pointer is over one.
            public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera) =>
                Owner == null || !Owner.IsOverYourCard(screenPoint);
        }

        // On a phone the big PC preview covers too much of the board, so it is drawn smaller there.
        // It shrinks toward its right edge, and the "can't play" line moves up to stay just under it.
        private void ShrinkPreviewOnPhones()
        {
            bool phone = Application.isMobilePlatform || (Application.isEditor && previewMobileInEditor);
            if (!phone || preview == null || mobilePreviewScale >= 0.999f)
                return;

            RectTransform rect = preview.Rect;
            rect.localScale *= mobilePreviewScale;
            if (previewReason == null)
                return;

            RectTransform reason = previewReason.rectTransform;
            float bottom = rect.anchoredPosition.y - rect.rect.height * rect.pivot.y * rect.localScale.y;
            reason.anchoredPosition = new Vector2(reason.anchoredPosition.x, bottom - 10f);
            reason.sizeDelta = new Vector2(rect.rect.width * rect.localScale.x, reason.sizeDelta.y);
        }

        private void OnEnable()
        {
            if (matchController != null)
                matchController.StateChanged += Refresh;
        }

        private void OnDisable()
        {
            if (matchController != null)
                matchController.StateChanged -= Refresh;
        }

        private void Start()
        {
            if (matchController == null || cardPrefab == null || drawPile == null || drawPile.CardBackTemplate == null)
            {
                Debug.LogError("TableHandsView: assign the Match Controller, Card Prefab and Draw Pile in the Inspector.", this);
                return;
            }

            _camera = Camera.main;
            _rect = (RectTransform)transform;
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null)
                canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            // Above the piles, so a dragged card passes over them. On the screen, above the pieces too.
            canvas.sortingOrder = handsOnScreen ? 60 : 6;
            canvas.worldCamera = _camera;
            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>(); // your cards can be hovered, clicked and dragged

            // Flying cards are drawn on the screen-space canvas, above everything.
            Canvas screenCanvas = preview != null ? preview.GetComponentInParent<Canvas>() : null;
            if (screenCanvas != null && screenCanvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                _previewCamera = screenCanvas.rootCanvas.worldCamera;
            if (screenCanvas != null)
                _flights = CardFlightLayer.Create(screenCanvas.rootCanvas, cardPrefab, drawPile.CardBackTemplate, arcHeight);

            // On a phone a finger wobbles a little while tapping: only a real move starts a drag.
            if (EventSystem.current != null && Screen.dpi > 0f)
                EventSystem.current.pixelDragThreshold = Mathf.Max(EventSystem.current.pixelDragThreshold,
                    Mathf.RoundToInt(Screen.dpi * 0.08f));

            if (handsOnScreen)
                ScreenPlane.Fill(_rect, _camera);
            else
                PlaceOnTable();
            _ready = true;
            Refresh();
        }

        // Lies flat on the board's plane, centered on the board, big enough for every side.
        private void PlaceOnTable()
        {
            BoardView board = matchController.BoardView;
            float scale = cardWorldHeight / CardHeight;
            _rect.localScale = new Vector3(scale, scale, scale);
            _rect.rotation = Quaternion.identity; // facing -Z, like the board
            if (board == null)
                return;
            float size = 2f * board.SeatRingRadius / scale;
            _rect.sizeDelta = new Vector2(size, size);
            _rect.position = board.transform.position + new Vector3(0f, 0f, -0.02f);
        }

        // Rebuilds the seats for a new match, otherwise moves cards to match the hands.
        private void Refresh()
        {
            if (!_ready)
                return;
            Match match = matchController.CurrentMatch;
            if (match == null)
                return;

            int viewer = matchController.ViewerId;
            if (match != _builtFor)
            {
                BuildSeats(match, viewer);
                return;
            }

            ApplyChanges(match);
            UpdateGlows(match);
            if (_yours != null)
                ShowCapacity(_yours, match);

            // The screen now belongs to someone else (hot-seat testing): let cards land, then turn the table.
            if (viewer != _builtViewer)
            {
                if (_flights != null && _flights.ActiveCount > 0)
                    _pendingRebuild = true;
                else
                    BuildSeats(match, viewer);
                return;
            }

            // A pinned card that left the hand (played, discarded) lets go of the preview.
            if (_hasPinned && (_yours == null || !_yours.Shown.Contains(_pinned)))
                Unpin();

            if (_hovered != null)
                ShowPreview(_hovered.CardId);
            else if (_hasPinned)
                ShowPreview(_pinned);
        }

        // ---------- Seats ----------

        // Builds one seat per Apostle, with the viewer's at the bottom.
        private void BuildSeats(Match match, int viewer)
        {
            _pendingRebuild = false;
            Unpin();
            EndHover();
            EndDrag();
            for (int i = 0; i < _seats.Count; i++)
                Destroy(_seats[i].Root.gameObject);
            _seats.Clear();
            _yours = null;

            // Seat order: you at the bottom, then the others in Id order, going around counter-clockwise.
            IReadOnlyList<ApostleState> apostles = match.Battlefield.Apostles;
            int count = apostles.Count;
            int[] sides = SidesByPlayerCount[Mathf.Clamp(count, 0, SidesByPlayerCount.Length - 1)];
            int start = 0;
            for (int i = 0; i < count; i++)
            {
                if (apostles[i].Id == viewer)
                    start = i;
            }

            BoardView board = matchController.BoardView;
            float scale = _rect.localScale.x;
            for (int n = 0; n < count && n < sides.Length; n++)
            {
                ApostleState apostle = apostles[(start + n) % count];
                board.GetSeat(sides[n], out Vector3 center, out float rotation, out float length, out float depth);

                var seat = new Seat
                {
                    ApostleId = apostle.Id,
                    IsYours = apostle.Id == viewer,
                    CardScale = apostle.Id == viewer ? yourCardScale : 1f,
                };
                seat.Root = CreateRect($"Apostle {apostle.Id} Hand", _rect);
                if (handsOnScreen)
                {
                    PlaceOnScreen(seat, n, count);
                }
                else
                {
                    seat.Root.sizeDelta = new Vector2(length / scale, depth / scale);
                    Vector3 local = (center - board.transform.position) / scale;
                    seat.Root.anchoredPosition = new Vector2(local.x, local.y);
                    seat.Root.localRotation = Quaternion.Euler(0f, 0f, rotation);
                }

                // Glows behind the tray: gold on the turn's Apostle, blue while it has DEF.
                // The gold one is wide, the blue one tight to the tray, so both show when both apply.
                seat.TurnGlow = CreateGlow("TurnGlow", seat.Root, turnGlowColor, 0.25f);
                seat.DefenseGlow = CreateGlow("DefenseGlow", seat.Root, defenseGlowColor, 0.38f);

                // A dark tray behind the cards, edged in the Apostle's color.
                seat.Tray = CreateRect("Tray", seat.Root);
                Image tray = seat.Tray.gameObject.AddComponent<Image>();
                Color seatColor = ApostleColors.Get(apostle.Id);
                tray.color = Color.Lerp(trayColor, seatColor, 0.22f);
                tray.raycastTarget = false;
                Outline edge = seat.Tray.gameObject.AddComponent<Outline>();
                edge.effectColor = ApostleColors.Get(apostle.Id);
                edge.effectDistance = new Vector2(6f, -6f);
                seat.Edge = edge;
                if (seat.IsYours && handsOnScreen)
                    HideTray(seat);

                if (showNameTags && !handsOnScreen)
                {
                    // The name tag: name and card count on a dark plate
                    // edged in the Apostle's colour, always upright.
                    // Opponents' tags sit on the board side of their
                    // cards, where the screen edge can't cut them off.
                    RectTransform tagRect = CreateRect("NameTag", seat.Root);
                    tagRect.sizeDelta = new Vector2(
                        Mathf.Max(300f, seat.Root.sizeDelta.x * 0.5f),
                        LabelHeight);
                    bool onBoardSide = nameTagOnBoardSide && !seat.IsYours;
                    float boardSide = RowY(seat)
                        + CardHeight * seat.CardScale / 2f
                        + trayPadding + 8f + LabelHeight / 2f;
                    float outerEdge = -seat.Root.sizeDelta.y / 2f
                        + LabelHeight / 2f;
                    tagRect.anchoredPosition = new Vector2(0f,
                        onBoardSide ? boardSide : outerEdge);
                    tagRect.localRotation =
                        Quaternion.Euler(0f, 0f, -rotation);

                    Image plate = tagRect.gameObject.AddComponent<Image>();
                    plate.color = new Color(0.05f, 0.04f, 0.08f, 0.85f);
                    plate.raycastTarget = false;
                    Outline plateEdge =
                        tagRect.gameObject.AddComponent<Outline>();
                    plateEdge.effectColor = seatColor;
                    plateEdge.effectDistance = new Vector2(3f, -3f);

                    RectTransform labelRect = CreateRect("Label", tagRect);
                    labelRect.anchorMin = Vector2.zero;
                    labelRect.anchorMax = Vector2.one;
                    labelRect.offsetMin = new Vector2(14f, 2f);
                    labelRect.offsetMax = new Vector2(-14f, -2f);
                    TextMeshProUGUI label =
                        labelRect.gameObject.AddComponent<TextMeshProUGUI>();
                    label.alignment = TextAlignmentOptions.Center;
                    label.fontSize = labelFontSize;
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 18f;
                    label.fontSizeMax = labelFontSize;
                    label.fontStyle = FontStyles.Bold;
                    label.color = seatColor;
                    label.raycastTarget = false;
                    seat.Label = label;
                }
                CreateSwapMark(seat);

                IReadOnlyList<ArcanaId> hand = match.GetHand(apostle.Id);
                for (int c = 0; c < hand.Count; c++)
                    seat.Shown.Add(hand[c]);
                _seats.Add(seat);
                if (seat.IsYours)
                {
                    _yours = seat;
                    CreateCapacityBar(seat);
                }
                Layout(seat, match, true);
            }

            _builtFor = match;
            _lastSwapNumber = match.LastSwap.Number; // a rebuild never replays an old swap
            _builtViewer = viewer;
            UpdateGlows(match);
            _lastDiscardCount = match.Deck.DiscardCount;
            _lastDrawCount = match.Deck.DrawCount;
        }

        // Works out which cards left each hand and which arrived, and launches the flights.
        private void ApplyChanges(Match match)
        {
            Deck deck = match.Deck;

            // Cards that reached the discard pile in this action: its newest entries.
            System.Array.Clear(_newDiscards, 0, ArcanaSlots);
            int discardGrowth = deck.DiscardCount - _lastDiscardCount;
            IReadOnlyList<ArcanaId> discards = deck.DiscardPile;
            for (int i = Mathf.Max(0, discards.Count - discardGrowth); i < discards.Count; i++)
                _newDiscards[(int)discards[i]]++;

            // Cards drawn in this action. If the discard pile was shuffled in, every new card was drawn.
            int drawsLeft = deck.DrawCount < _lastDrawCount ? _lastDrawCount - deck.DrawCount
                : deck.DrawCount > _lastDrawCount ? int.MaxValue : 0;

            int toPile = 0;
            for (int s = 0; s < _seats.Count; s++)
            {
                Seat seat = _seats[s];
                IReadOnlyList<ArcanaId> hand = match.GetHand(seat.ApostleId);

                // Which cards left this hand? Those that went to the discard pile fly there.
                System.Array.Clear(_cardCounts, 0, ArcanaSlots);
                for (int i = 0; i < hand.Count; i++)
                    _cardCounts[(int)hand[i]]++;
                int kept = 0;
                int unexplained = 0;
                for (int i = 0; i < seat.Shown.Count; i++)
                {
                    int id = (int)seat.Shown[i];
                    if (_cardCounts[id] > 0)
                    {
                        _cardCounts[id]--;
                        kept++;
                    }
                    else if (_newDiscards[id] > 0)
                    {
                        _newDiscards[id]--;
                        if (FlyToDiscardPile(seat, i, seat.Shown[i]))
                            toPile++;
                    }
                    else if (!seat.IsYours && matchController.IsOnlineClient)
                    {
                        unexplained++;
                    }
                }

                // Online, an opponent's hand arrives as blank cards, so a played card can't be matched by
                // name. A face-down card that left the hand takes one of the new discards it could be.
                for (int n = 0; n < unexplained; n++)
                {
                    int claimed = TakeAnyNewDiscard();
                    if (claimed < 0)
                        break;
                    if (FlyToDiscardPile(seat, -1, (ArcanaId)claimed))
                        toPile++;
                }

                // Cards that arrived: the drawn ones fly in and appear when they land.
                int added = Mathf.Max(0, hand.Count - kept);
                int flying = 0;
                while (flying < added && drawsLeft > 0)
                {
                    drawsLeft--;
                    flying++;
                }

                seat.Shown.Clear();
                for (int i = 0; i < hand.Count; i++)
                    seat.Shown.Add(hand[i]);
                seat.Arriving += flying;
                Layout(seat, match, false);

                int slots = seat.SlotCount(maxCardsShown);
                for (int j = 0; j < flying; j++)
                    FlyToSeat(seat, match, Mathf.Clamp(slots - flying + j, 0, Mathf.Max(0, slots - 1)), hand.Count - flying + j);
            }

            // What no hand claimed came out of the play queue: those play one at a time, in order.
            int queued = _playOrder.Count;
            if (_flights != null && discardPile != null)
            {
                for (int i = Mathf.Max(0, discards.Count - discardGrowth); i < discards.Count; i++)
                {
                    if (_newDiscards[(int)discards[i]] <= 0)
                        continue;
                    _newDiscards[(int)discards[i]]--;
                    _playOrder.Enqueue(discards[i]);
                }
            }
            toPile += _playOrder.Count - queued;

            if (toPile > 0 && discardPile != null)
                discardPile.HoldTop(toPile);
            if (!_playingCard)
                PlayNextCard();
            // A new wheel of fortune swap (its Number went up): show which two cards changed hands.
            CardSwap swap = match.LastSwap;
            if (swap.Number != _lastSwapNumber)
            {
                _lastSwapNumber = swap.Number;
                if (swap.Number > 0)
                    ShowSwap(swap);
            }
            _lastDiscardCount = deck.DiscardCount;
            _lastDrawCount = deck.DrawCount;
        }

        // Shows the cards that have landed (room is kept for the ones still flying in), and the label.
        private void Layout(Seat seat, Match match, bool snap)
        {
            int total = seat.SlotCount(maxCardsShown);
            int visible = Mathf.Clamp(seat.Shown.Count - seat.Arriving, 0, total);

            if (seat.IsYours)
            {
                while (seat.Faces.Count < total)
                    seat.Faces.Add(CreateFace(seat));
                bool selectionShown = false;
                for (int k = 0; k < seat.Faces.Count; k++)
                {
                    CardView face = seat.Faces[k];
                    bool active = k < visible;
                    if (!active && face == _hovered)
                        EndHover();
                    if (!active && face == _dragged)
                        EndDrag();
                    face.gameObject.SetActive(active);
                    if (!active)
                        continue;

                    ArcanaId card = seat.Shown[k];
                    face.Show(CardCatalog.Get(card));
                    face.SetDimmed(matchController.GetUnplayableReason(card) != null);
                    bool selected = !selectionShown && matchController.HasSelectedCard && matchController.SelectedCard == card;
                    face.SetSelected(selected);
                    selectionShown |= selected;
                    if (snap)
                    {
                        face.Rect.anchoredPosition = SlotPosition(seat, k, total);
                        face.Rect.localScale = Vector3.one * seat.CardScale;
                    }
                }
            }
            else
            {
                while (seat.Backs.Count < total)
                    seat.Backs.Add(CreateBack(seat));
                for (int k = 0; k < seat.Backs.Count; k++)
                {
                    RectTransform back = seat.Backs[k];
                    bool active = k < visible;
                    back.gameObject.SetActive(active);
                    if (active)
                        back.anchoredPosition = SlotPosition(seat, k, total);
                }
            }

            // The tray fits the row (at least one card wide, so an empty hand still shows its place).
            int slots = Mathf.Max(1, total);
            float rowWidth = (slots - 1) * Spacing(seat, slots) + CardWidth * seat.CardScale;
            seat.Tray.sizeDelta = new Vector2(rowWidth + 2f * trayPadding, CardHeight * seat.CardScale + 2f * trayPadding);
            seat.Tray.anchoredPosition = new Vector2(0f, RowY(seat));
            FitGlow(seat.TurnGlow, seat.Tray, 64f);
            FitGlow(seat.DefenseGlow, seat.Tray, 42f);
            PlaceSwapMark(seat, total);
            if (seat.IsYours)
                ShowCapacity(seat, match);

            if (seat.Label == null)
                return;
            ApostleState apostle = FindApostle(match, seat.ApostleId);
            int count = Mathf.Max(0, seat.Shown.Count - seat.Arriving);
            string who = seat.IsYours ? "You" : PlayerNames.Get(seat.ApostleId);
            string state = apostle != null && apostle.IsEliminated ? "OUT" : count == 1 ? "1 card" : $"{count} cards";
            // The name in the Apostle's colour, the count dimmer.
            seat.Label.text = $"{who}  <color=#FFFFFFB0>{state}</color>";
        }

        // Cards sit toward the board, leaving the outer edge for the name.
        private static float RowY(Seat seat) => LabelHeight / 2f;

        // ---------- Hands on the screen ----------

        // Yours along the bottom, beside your profile. The others' hands are kept (cards still fly from
        // them) but hidden just above the top edge: their card counts show on their name plates.
        private void PlaceOnScreen(Seat seat, int n, int count)
        {
            float half = _rect.sizeDelta.y / 2f;
            if (seat.IsYours)
            {
                float scale = YourScreenScale();
                seat.CardScale = scale;
                float depth = CardHeight * scale + 2f * trayPadding + LabelHeight;
                seat.Root.sizeDelta = new Vector2(YourHandWidth(scale), depth);
                float y = -half + screenBottomMargin + depth / 2f - LabelHeight / 2f;
                seat.Root.anchoredPosition = new Vector2(screenHandOffset, y);
                return;
            }

            seat.CardScale = 0.35f;
            float width = _rect.sizeDelta.x;
            float x = -width / 2f + width * n / Mathf.Max(1, count);
            seat.Root.sizeDelta = new Vector2(400f, CardHeight * 0.35f);
            seat.Root.anchoredPosition = new Vector2(x, half + CardHeight);
            CanvasGroup hide = seat.Root.gameObject.AddComponent<CanvasGroup>();
            hide.alpha = 0f;
            hide.blocksRaycasts = false;
        }

        // Phones get smaller cards, computers bigger ones, by how tall the screen really is in inches:
        // about 3 inches or less is a phone, 7 or more a laptop or monitor, tablets in between.
        private float YourScreenScale()
        {
            if (Application.isEditor && handPhoneInEditor)
                return phoneCardScale;
            float inches = Screen.dpi > 0f ? Screen.height / Screen.dpi : 0f;
            if (inches <= 0f)
                return Application.isMobilePlatform ? phoneCardScale : pcCardScale;
            float t = Mathf.InverseLerp(3f, 7f, inches);
            return Mathf.Lerp(phoneCardScale, pcCardScale, t);
        }

        // As wide as the PC width allows at this card size, but clear of the draw pile and your profile.
        private float YourHandWidth(float scale)
        {
            float wanted = pcHandWidth * scale / Mathf.Max(0.01f, pcCardScale);
            float half = _rect.sizeDelta.x / 2f;
            float right = 2f * (half - screenRightClear - screenHandOffset);
            float left = 2f * (half - screenLeftClear + screenHandOffset);
            return Mathf.Max(CardWidth * scale, Mathf.Min(wanted, Mathf.Min(left, right)));
        }

        // Cards standing up from the table only make sense on the table.
        private float YourStand => handsOnScreen ? 0f : yourStandAngle;
        private float OthersStand => handsOnScreen ? 0f : standAngle;

        // ---------- Where a hand is on screen (for the name plates) ----------

        private static readonly Vector3[] Corners = new Vector3[4];

        /// <summary>
        /// The screen point just above an Apostle's hand: the middle of the hand across,
        /// and the highest point of its tray and cards (and your Capacity bar) up.
        /// False if that Apostle has no hand on the table.
        /// </summary>
        public bool TryGetHandTop(int apostleId, Camera cam, out Vector2 screen)
        {
            screen = Vector2.zero;
            Seat seat = null;
            for (int i = 0; i < _seats.Count; i++)
            {
                if (_seats[i].ApostleId == apostleId)
                    seat = _seats[i];
            }
            if (seat == null || cam == null || seat.Tray == null || handsOnScreen)
                return false;

            float top = float.MinValue;
            seat.Tray.GetWorldCorners(Corners);
            Vector3 middle = (Corners[0] + Corners[2]) * 0.5f;
            screen.x = cam.WorldToScreenPoint(middle).x;
            top = Mathf.Max(top, HighestOnScreen(seat.Tray, cam));
            // Standing cards rise above their tray.
            for (int k = 0; k < seat.Backs.Count; k++)
            {
                if (seat.Backs[k].gameObject.activeSelf)
                    top = Mathf.Max(top, HighestOnScreen(seat.Backs[k], cam));
            }
            for (int k = 0; k < seat.Faces.Count; k++)
            {
                if (seat.Faces[k].gameObject.activeSelf)
                    top = Mathf.Max(top, HighestOnScreen(seat.Faces[k].Rect, cam));
            }
            if (seat.IsYours && _capacityBar != null && _capacityBar.gameObject.activeInHierarchy)
                top = Mathf.Max(top, HighestOnScreen(_capacityBar, cam));
            screen.y = top;
            return true;
        }

        // The highest point of a rect on screen, in pixels.
        private static float HighestOnScreen(RectTransform rect, Camera cam)
        {
            rect.GetWorldCorners(Corners);
            float top = float.MinValue;
            for (int i = 0; i < 4; i++)
                top = Mathf.Max(top, cam.WorldToScreenPoint(Corners[i]).y);
            return top;
        }

        // Where a card rests in a seat: spread along the row, fanned for your hand.
        private Vector2 SlotPosition(Seat seat, int slot, int total)
        {
            float offset = slot - (total - 1) / 2f;
            var position = new Vector2(offset * Spacing(seat, total), RowY(seat));
            // Fanned: the cards follow an arc, the outer ones lower.
            if (IsFanned(seat))
            {
                float share = FanShare(slot, total);
                position.y -= fanDrop * share * share;
            }
            return position;
        }

        // True for your own hand when fanning is on.
        private bool IsFanned(Seat seat) => fanYourHand && seat == _yours;

        // -1 for the leftmost card, 0 in the middle, 1 for the rightmost. A small hand fans less.
        private static float FanShare(int slot, int total)
        {
            if (total <= 1)
                return 0f;
            float half = (total - 1) / 2f;
            float spread = Mathf.Min(1f, (total - 1) / 6f);
            return (slot - half) / half * spread;
        }

        // The tilt of a fanned card, in degrees: the left cards lean left, the right cards right.
        private float FanAngle(Seat seat, int slot, int total) =>
            IsFanned(seat) ? -FanShare(slot, total) * fanAngle : 0f;

        // Side by side with a small gap; squeezed together (overlapping) only if the row would not fit.
        private float Spacing(Seat seat, int total)
        {
            if (total <= 1)
                return 0f;
            float cardWidth = CardWidth * seat.CardScale;
            float room = seat.Root.sizeDelta.x - cardWidth - 2f * trayPadding - 16f;
            float widest = IsFanned(seat) ? cardWidth * fanOverlap : cardWidth + cardGap;
            return Mathf.Clamp(room / (total - 1), 6f, widest);
        }

        // Creates a face-up card for a seat.
        private CardView CreateFace(Seat seat)
        {
            CardView face = Instantiate(cardPrefab, seat.Root);
            RectTransform rect = face.Rect;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one * seat.CardScale;
            Stand(rect, YourStand, CardHeight * seat.CardScale);
            if (face.GetComponent<CanvasGroup>() == null)
                face.gameObject.AddComponent<CanvasGroup>();
            // Hover is worked out in UpdateHover from where the cards rest, not from the card's own
            // enter/exit: a lifted card would slide out from under the pointer and flicker.
            face.Clicked += OnClicked;
            face.DragStarted += OnDragStarted;
            face.Dragged += OnDragged;
            face.DragEnded += OnDragEnded;
            return face;
        }

        // Stands a card up from its outer edge: tipped about the row's axis (its top, which faces the
        // board, rises toward the camera) and lifted so the bottom edge stays on the table.
        // anchoredPosition only moves x and y, so the lift survives the layout.
        private static void Stand(RectTransform card, float angle, float height)
        {
            card.localRotation = Quaternion.Euler(-angle, 0f, 0f);
            Vector3 position = card.anchoredPosition3D;
            position.z = -0.5f * height * Mathf.Sin(angle * Mathf.Deg2Rad);
            card.anchoredPosition3D = position;
        }

        // Creates a face-down card for a seat.
        private RectTransform CreateBack(Seat seat)
        {
            RectTransform back = Instantiate(drawPile.CardBackTemplate, seat.Root);
            back.name = "Card";
            back.anchorMin = new Vector2(0.5f, 0.5f);
            back.anchorMax = new Vector2(0.5f, 0.5f);
            back.pivot = new Vector2(0.5f, 0.5f);
            back.sizeDelta = new Vector2(CardWidth, CardHeight);
            back.localScale = Vector3.one;
            Stand(back, OthersStand, CardHeight);

            // Edged in the owner's colour, so each card shows whose hand it belongs to.
            if (back.GetComponent<Image>() != null)
            {
                Outline ring = back.gameObject.AddComponent<Outline>();
                ring.effectColor = ApostleColors.Get(seat.ApostleId);
                ring.effectDistance = new Vector2(5f, -5f);
            }
            return back;
        }

        // ---------- Capacity bar ----------

        // The bar, its pips and its number, made once per seat build.
        // ShowCapacity sizes and fills them.
        private void CreateCapacityBar(Seat seat)
        {
            _capacityPips.Clear();
            _capacityBar = CreateRect("CapacityBar", seat.Root);
            for (int i = 0; i < MaxCapacityPips; i++)
            {
                RectTransform pip = CreateRect("Pip" + (i + 1), _capacityBar);
                var image = (Image)pip.gameObject.AddComponent(typeof(Image));
                image.raycastTarget = false;
                _capacityPips.Add(image);
            }

            // An optional frame around the pips, from UiSkin.
            UiSkin skin = UiSkin.Current;
            if (skin != null && skin.CapacityFrame != null)
            {
                RectTransform frame = CreateRect("Frame", _capacityBar);
                frame.anchorMin = Vector2.zero;
                frame.anchorMax = Vector2.one;
                frame.offsetMin = new Vector2(-8f, -8f);
                frame.offsetMax = new Vector2(8f, 8f);
                var frameImage = (Image)frame.gameObject.AddComponent(typeof(Image));
                frameImage.sprite = skin.CapacityFrame;
                frameImage.type = Image.Type.Sliced;
                frameImage.raycastTarget = false;
            }

            RectTransform textRect = CreateRect("Text", _capacityBar);
            var text = (TextMeshProUGUI)textRect.gameObject.AddComponent(typeof(TextMeshProUGUI));
            text.fontSize = 30f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Bottom;
            text.color = CapacityLit;
            text.raycastTarget = false;
            _capacityText = text;
        }

        // Fills the bar. On your turn it shows what is left; on other turns, dimmed,
        // the Capacity your next turn starts with, so the bar never disappears.
        private void ShowCapacity(Seat seat, Match match)
        {
            if (_capacityBar == null)
                return;
            bool show = !match.IsOver;
            _capacityBar.gameObject.SetActive(show);
            if (!show)
                return;

            bool yourTurn = match.CurrentApostle != null && match.CurrentCapacity != null
                            && match.CurrentApostle.Id == seat.ApostleId;
            int maximum = yourTurn ? match.CurrentCapacity.Maximum : match.GetCapacityPreview(seat.ApostleId);
            int remaining = yourTurn ? match.CurrentCapacity.Remaining : maximum;
            Color lit = yourTurn ? CapacityLit : Color.Lerp(CapacityLit, CapacitySpent, 0.45f);

            // Real pip art (UiSkin) shows its own colours; off-turn it is only dimmed.
            UiSkin skin = UiSkin.Current;
            Sprite litSprite = skin != null ? skin.CapacityPipLit : null;
            Sprite spentSprite = skin != null ? skin.CapacityPipSpent : null;
            Color dim = yourTurn ? Color.white : new Color(0.6f, 0.6f, 0.6f, 1f);

            // Just above the tray, as wide as it.
            float width = seat.Tray.sizeDelta.x;
            float top = seat.Tray.anchoredPosition.y + seat.Tray.sizeDelta.y / 2f;
            _capacityBar.sizeDelta = new Vector2(width, CapacityBarHeight);
            _capacityBar.anchoredPosition = new Vector2(0f, top + CapacityGap + CapacityBarHeight / 2f);

            int pips = Mathf.Clamp(maximum, 1, MaxCapacityPips);
            float gap = 6f;
            float pipWidth = (width - gap * (pips - 1)) / pips;
            for (int i = 0; i < _capacityPips.Count; i++)
            {
                Image pip = _capacityPips[i];
                bool used = i < pips;
                pip.gameObject.SetActive(used);
                if (!used)
                    continue;
                RectTransform rect = pip.rectTransform;
                rect.sizeDelta = new Vector2(pipWidth, CapacityBarHeight);
                float x = -width / 2f + pipWidth / 2f + i * (pipWidth + gap);
                rect.anchoredPosition = new Vector2(x, 0f);
                bool on = i < remaining;
                Sprite art = on ? litSprite : spentSprite;
                pip.sprite = art;
                if (art != null)
                    pip.color = dim;
                else
                    pip.color = on ? lit : CapacitySpent;
            }

            // The number sits above the middle of the bar, like Minecraft's level.
            RectTransform textRect = _capacityText.rectTransform;
            textRect.sizeDelta = new Vector2(width, 40f);
            textRect.anchoredPosition = new Vector2(0f, CapacityBarHeight / 2f + 24f);
            _capacityText.text = $"CAPACITY {remaining}/{maximum}";
            _capacityText.color = lit;
        }

        // ---------- Your cards: lifting, hovering, clicking and dragging ----------

        private void Update()
        {
            TickSwapMarks(Time.unscaledDeltaTime);

            if (_pendingRebuild && (_flights == null || _flights.ActiveCount == 0))
            {
                Match match = matchController.CurrentMatch;
                if (match != null)
                    BuildSeats(match, matchController.ViewerId);
            }

            PulseGlows();
            CheckPreviewTap();

            Seat seat = _yours;
            if (seat == null)
                return;

            int total = seat.SlotCount(maxCardsShown);
            UpdateHover(seat, total);
            float t = 1f - Mathf.Exp(-moveSpeed * Time.unscaledDeltaTime);
            // The gentle float is skipped on Low graphics: a still hand is not redrawn every frame.
            bool bob = IsFanned(seat) && idleBob > 0f && _dragged == null && GraphicsQuality.AnimateUi;
            float time = Time.unscaledTime;
            bool selectionShown = false; // with two copies in hand, only the first one rises
            for (int k = 0; k < seat.Faces.Count && k < total; k++)
            {
                CardView face = seat.Faces[k];
                if (face == _dragged || !face.gameObject.activeSelf)
                    continue;

                bool hovered = face == _hovered || (_hasPinned && face == _pinnedFace && face.CardId == _pinned);
                bool selected = !selectionShown && matchController.HasSelectedCard
                                && matchController.SelectedCard == face.CardId;
                selectionShown |= selected;
                Vector2 target = SlotPosition(seat, k, total);
                float angle = FanAngle(seat, k, total);
                if (hovered)
                {
                    // A hovered card rises, straightens and grows, like picking it up.
                    target.y += hoverLift;
                    angle = 0f;
                }
                else
                {
                    if (selected)
                        target.y += selectedLift;
                    if (bob)
                    {
                        // Each card floats at its own pace, so the hand looks alive.
                        target.y += Mathf.Sin(time * 1.7f + k * 0.9f) * idleBob;
                        angle += Mathf.Sin(time * 1.3f + k * 1.1f) * 0.8f;
                    }
                }
                float scale = seat.CardScale * (hovered ? hoverScale : 1f);

                MoveCard(face.Rect, target, scale, angle, t);
            }
        }

        // Eases a card to its place. A card already there is left alone: touching it would redraw
        // the whole hand every frame, which costs a lot on a phone.
        private void MoveCard(RectTransform rect, Vector2 target, float scale, float angle, float t)
        {
            Vector2 position = rect.anchoredPosition;
            if ((position - target).sqrMagnitude > 0.01f)
                rect.anchoredPosition = Vector2.Lerp(position, target, t);
            else if (position != target)
                rect.anchoredPosition = target;

            Quaternion stand = Quaternion.Euler(-YourStand, 0f, angle);
            if (rect.localRotation != stand)
                rect.localRotation = Quaternion.Slerp(rect.localRotation, stand, t);

            float current = rect.localScale.x;
            if (Mathf.Abs(current - scale) > 0.001f)
                rect.localScale = Vector3.one * Mathf.Lerp(current, scale, t);
            else if (current != scale)
                rect.localScale = Vector3.one * scale;
        }

        // Lifts the card under the pointer and shows its preview.
        private void OnHoverStarted(CardView card)
        {
            if (_dragged != null)
                return;
            _hovered = card;
            card.transform.SetAsLastSibling(); // in front of its neighbours
            ShowPreview(card.CardId);
        }

        // ---------- Hover ----------

        private readonly List<RaycastResult> _hoverHits = new List<RaycastResult>(8);
        private PointerEventData _hoverEvent;
        private EventSystem _hoverEventSystem; // the one _hoverEvent was made for
        private Vector2 _lastHoverPoint = new Vector2(-1f, -1f); // -1 = no point yet
        private bool _touchDown;
        private bool _touchMoved;  // this press went past a tap's wobble
        private Vector2 _touchStart;


        // Picks the card under the pointer by where the cards rest. The hovered card keeps its whole
        // lifted, grown area, so lifting it never moves it out from under the pointer.
        private void UpdateHover(Seat seat, int total)
        {
            if (_dragged != null)
                return;
            Pointer pointer = Pointer.current;
            if (pointer == null)
                return;
            if (pointer is Touchscreen || Application.isMobilePlatform)
            {
                UpdateTouch(seat, total, pointer);
                return;
            }

            Vector2 point = pointer.position.ReadValue();
            if (point == _lastHoverPoint)
                return;
            _lastHoverPoint = point;

            CardView found = FindRestingCard(seat, total, point);
            if (found != null && IsCoveredByOtherUi(seat, point))
                found = null;
            if (found == _hovered)
                return;
            if (_hovered != null)
                EndHover();
            if (found != null)
                OnHoverStarted(found);
        }

        // A finger has no hover: the card it lands on stays the lifted one until it lets go, however the
        // finger wobbles or the card rises out from under it. Letting go without moving is a tap. It is
        // read here because the lifted card may have left the finger, and then uGUI sends no click.
        private void UpdateTouch(Seat seat, int total, Pointer pointer)
        {
            bool down = pointer.press.isPressed;
            Vector2 point = pointer.position.ReadValue();
            if (down == _touchDown)
            {
                if (down && (point - _touchStart).magnitude > MatchCamera.TapSlop())
                    _touchMoved = true;
                return;
            }

            _touchDown = down;
            if (down)
            {
                _touchStart = point;
                _touchMoved = false;
                CardView found = FindRestingCard(seat, total, point);
                if (found != null && IsCoveredByOtherUi(seat, point))
                    found = null;
                if (_hovered != null && found != _hovered)
                    EndHover();
                if (found != null && found != _hovered)
                    OnHoverStarted(found);
                return;
            }

            CardView held = _hovered;
            if (held == null)
                return;
            if (!_touchMoved)
                OnClicked(held);
            EndHover();
        }

        // The card whose resting slot is under the point, so a lifted card does not flicker.
        private CardView FindRestingCard(Seat seat, int total, Vector2 point)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(seat.Root, point, _camera, out Vector2 local))
                return null;
            float halfWidth = CardWidth * seat.CardScale / 2f;
            float halfHeight = CardHeight * seat.CardScale / 2f;
            int count = Mathf.Min(total, seat.Faces.Count);

            int held = _hovered != null ? seat.Faces.IndexOf(_hovered) : -1;
            if (held >= 0 && held < count && _hovered.gameObject.activeSelf)
            {
                Vector2 slot = SlotPosition(seat, held, total);
                bool across = Mathf.Abs(local.x - slot.x) <= halfWidth * hoverScale;
                bool up = local.y >= slot.y - halfHeight && local.y <= slot.y + hoverLift + halfHeight * hoverScale;
                if (across && up)
                    return _hovered;
            }

            // Later cards lie on top, so the first match from the right is the one you see.
            for (int k = count - 1; k >= 0; k--)
            {
                CardView face = seat.Faces[k];
                if (!face.gameObject.activeSelf || face == _dragged)
                    continue;
                Vector2 slot = SlotPosition(seat, k, total);
                if (Mathf.Abs(local.x - slot.x) <= halfWidth && Mathf.Abs(local.y - slot.y) <= halfHeight)
                    return face;
            }
            return null;
        }

        // True when a popup, the card preview or another panel is on top of the hand at this point.
        private bool IsCoveredByOtherUi(Seat seat, Vector2 point)
        {
            EventSystem events = EventSystem.current;
            if (events == null)
                return false;
            if (_hoverEvent == null || _hoverEventSystem != events)
            {
                _hoverEvent = new PointerEventData(events);
                _hoverEventSystem = events;
            }
            _hoverEvent.position = point;
            _hoverHits.Clear();
            events.RaycastAll(_hoverEvent, _hoverHits);
            if (_hoverHits.Count == 0)
                return false;
            GameObject top = _hoverHits[0].gameObject;
            return top != null && !top.transform.IsChildOf(seat.Root);
        }

        // Drops the lifted card back into its slot.
        private void EndHover()
        {
            if (_hovered != null)
                RestoreOrder();
            _hovered = null;
            // A pinned card keeps its preview up; otherwise the preview goes.
            if (_hasPinned)
            {
                ShowPreview(_pinned);
                return;
            }
            HidePreview();
        }

        // Hides the big preview and the reason line.
        private void HidePreview()
        {
            if (preview != null)
                preview.gameObject.SetActive(false);
            if (previewReason != null)
                previewReason.gameObject.SetActive(false);
            if (_previewGroup != null)
                _previewGroup.blocksRaycasts = false;
            if (_previewBlocker != null)
                _previewBlocker.SetActive(false);
        }

        // Releases a pinned preview.
        private void Unpin()
        {
            if (!_hasPinned)
                return;
            _hasPinned = false;
            if (_hovered == null)
                HidePreview();
        }

        // While a card is pinned, a tap outside the preview puts it away.
        // It closes when the tap lets go: the blocker caught the press,
        // so the board and the hand never saw it.
        private void CheckPreviewTap()
        {
            if (!_hasPinned || preview == null || !preview.gameObject.activeInHierarchy)
                return;
            if (Time.frameCount == _pinnedFrame)
                return; // the tap that pinned it
            if (_touchDown && _hovered != null)
                return; // a finger lifting off a card: UpdateTouch pins that card instead
            Pointer pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasReleasedThisFrame)
                return;
            Vector2 point = pointer.position.ReadValue();
            if (RectTransformUtility.RectangleContainsScreenPoint(preview.Rect, point, _previewCamera))
                return; // a tap on the preview itself keeps it open
            _hovered = null;
            Unpin();
            HidePreview();
        }

        // Puts your cards back in hand order (after the tray) once a hovered card was brought to the front.
        private void RestoreOrder()
        {
            if (_yours == null)
                return;
            for (int i = 0; i < _yours.Faces.Count; i++)
                _yours.Faces[i].transform.SetAsLastSibling();
        }

        // Shows the big preview of a card.
        private void ShowPreview(ArcanaId card)
        {
            if (preview != null)
            {
                preview.gameObject.SetActive(true);
                preview.Show(CardCatalog.Get(card));
                preview.SetSelected(false);
                preview.SetDimmed(false);
            }

            if (previewReason != null)
            {
                string reason = matchController.GetUnplayableReason(card);
                previewReason.gameObject.SetActive(reason != null);
                if (reason != null)
                    previewReason.text = reason;
            }
        }

        // A tap only shows the card: its preview stays up until you tap outside it.
        // Cards are played by dragging them out of the hand.
        private void OnClicked(CardView card)
        {
            _hasPinned = true;
            _pinned = card.CardId;
            _pinnedFace = card;
            ShowPreview(_pinned);
            // While pinned, the preview catches taps, so a tap on it never reaches the board underneath.
            if (_previewGroup != null)
                _previewGroup.blocksRaycasts = true;
            _pinnedFrame = Time.frameCount;
            if (_previewBlocker != null)
                _previewBlocker.SetActive(true);
        }

        // Picks a playable card up to drag it to the queue.
        private void OnDragStarted(CardView card, PointerEventData eventData)
        {
            // Cards that can't be played (dimmed) stay in the hand.
            if (_dragged != null || matchController.GetUnplayableReason(card.CardId) != null
                || matchController.IsChoosingComboTarget)
                return;

            _dragged = card;
            _hasPinned = false; // the board should be clear while dragging
            EndHover();
            card.transform.SetAsLastSibling();
            card.GetComponent<CanvasGroup>().blocksRaycasts = false;
            FollowPointer(card, eventData);
        }

        // Moves the dragged card with the pointer.
        private void OnDragged(CardView card, PointerEventData eventData)
        {
            if (card == _dragged)
                FollowPointer(card, eventData);
        }

        // Drops the dragged card: over the hand it slides back, anywhere else it is played.
        private void OnDragEnded(CardView card, PointerEventData eventData)
        {
            if (card != _dragged)
                return;
            EndDrag();

            // Dropped back on the hand: nothing happens, the card slides back.
            Vector2 point = eventData.position;
            if (IsOverYourHand(point, eventData.pressEventCamera))
                return;

            // Dropped anywhere else: on a red-ringed Apostle it attacks that Apostle; otherwise the card
            // is played (an ATK card with targets in reach waits, lifted, for a target to be tapped).
            if (!matchController.TryDropCardOnBoard(card.CardId, point))
                matchController.DropCardOnPile(card.CardId);
        }

        // True when the point is over your hand area.
        private bool IsOverYourHand(Vector2 screenPoint, Camera eventCamera)
        {
            if (_yours == null || _yours.Tray == null)
                return false;
            Camera cam = eventCamera != null ? eventCamera : _camera;
            return RectTransformUtility.RectangleContainsScreenPoint(_yours.Tray, screenPoint, cam);
        }

        // The dragged card slides across the table under the pointer.
        private void FollowPointer(CardView card, PointerEventData eventData)
        {
            if (_yours != null && RectTransformUtility.ScreenPointToWorldPointInRectangle(_yours.Root, eventData.position,
                    eventData.pressEventCamera != null ? eventData.pressEventCamera : _camera, out Vector3 world))
                card.Rect.position = world;

            if (discardPile != null)
                discardPile.SetDragging(true, discardPile.ContainsScreenPoint(eventData.position));
        }

        // Ends the drag so the card catches taps again.
        private void EndDrag()
        {
            if (_dragged == null)
                return;
            CanvasGroup group = _dragged.GetComponent<CanvasGroup>();
            if (group != null)
                group.blocksRaycasts = true;
            _dragged = null;
            if (discardPile != null)
                discardPile.SetDragging(false, false);
        }

        // ---------- Flying cards ----------

        /// <summary>True while cards from the play queue are still being shown.</summary>
        public bool IsPlayingCards => _playingCard;

        /// <summary>About how long until the last queued card has landed.</summary>
        public float PlaySecondsLeft =>
            (_playOrder.Count + (_playingCard ? 1 : 0)) * MotionStyle.Seconds(playFlightTime * 1.9f);

        // Shows the next queued card: it rises large in the middle of the screen, then drops on the pile.
        private void PlayNextCard()
        {
            while (_playOrder.Count > 0)
            {
                ArcanaId card = _playOrder.Dequeue();
                if (discardPile.TryGetScreenSpot(out Vector2 to, out float toPixels))
                {
                    _playingCard = true;
                    var from = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                    _flights.Fly(card, from, 0f, _flights.ScaleForPixels(Screen.height * 0.1f), to, 0f,
                        _flights.ScaleForPixels(toPixels), false, false, playFlightTime, OnCardPlayed, true,
                        _flights.ScaleForPixels(Screen.height * 0.45f));
                    return;
                }
                discardPile.ReleaseOne();
            }
            _playingCard = false;
        }

        // A queued card reached the pile: on to the next one.
        private void OnCardPlayed()
        {
            discardPile.ReleaseOne();
            PlayNextCard();
        }

        // A new discard no hand has claimed yet, or -1.
        private int TakeAnyNewDiscard()
        {
            for (int id = 0; id < ArcanaSlots; id++)
            {
                if (_newDiscards[id] > 0)
                {
                    _newDiscards[id]--;
                    return id;
                }
            }
            return -1;
        }

        // From where the card was in the hand to the discard pile. Opponents' cards turn face-up on the way.
        // handIndex is only needed for your own hand (an opponent's card leaves from the end of the row).
        private bool FlyToDiscardPile(Seat seat, int handIndex, ArcanaId card)
        {
            if (_flights == null || discardPile == null
                || !discardPile.TryGetScreenSpot(out Vector2 to, out float toPixels))
                return false;

            RectTransform from = null;
            if (seat.IsYours)
            {
                if (handIndex >= 0 && handIndex < seat.Faces.Count && seat.Faces[handIndex].gameObject.activeSelf)
                    from = seat.Faces[handIndex].Rect;
            }
            else
            {
                for (int k = seat.Backs.Count - 1; k >= 0 && from == null; k--)
                {
                    if (seat.Backs[k].gameObject.activeSelf)
                        from = seat.Backs[k];
                }
            }
            if (from == null || !TableSpot.TryGet(from, _camera, _corners, out Vector2 start, out float startPixels,
                    out float startAngle))
                return false;

            _flights.Fly(card, start, startAngle, _flights.ScaleForPixels(startPixels), to, 0f,
                _flights.ScaleForPixels(toPixels), !seat.IsYours, false, playFlightTime, discardPile.ReleaseOne, true);
            return true;
        }

        // From the draw pile to its place in the hand: face-down all the way to an opponent,
        // turning face-up on its way to you.
        private void FlyToSeat(Seat seat, Match match, int slot, int handIndex)
        {
            int total = seat.SlotCount(maxCardsShown);
            if (_flights == null || total == 0 || drawPile == null
                || !drawPile.TryGetScreenSpot(out Vector2 from, out float fromPixels))
            {
                Landed(seat, match);
                return;
            }

            // Where it will lie: a card of the row stands in for it while measuring.
            RectTransform stand = seat.IsYours ? seat.Faces[slot].Rect : seat.Backs[slot];
            bool wasActive = stand.gameObject.activeSelf;
            Vector2 oldPosition = stand.anchoredPosition;
            Vector3 oldScale = stand.localScale;
            stand.anchoredPosition = SlotPosition(seat, slot, total);
            stand.localScale = Vector3.one * seat.CardScale;
            bool found = TableSpot.TryGet(stand, _camera, _corners, out Vector2 to, out float toPixels, out float toAngle);
            stand.anchoredPosition = oldPosition;
            stand.localScale = oldScale;
            stand.gameObject.SetActive(wasActive);
            if (!found)
            {
                Landed(seat, match);
                return;
            }

            ArcanaId card = seat.IsYours && handIndex >= 0 && handIndex < seat.Shown.Count ? seat.Shown[handIndex] : default;
            _flights.Fly(card, from, 0f, _flights.ScaleForPixels(fromPixels), to, toAngle,
                _flights.ScaleForPixels(toPixels), true, !seat.IsYours, drawFlightTime, () => Landed(seat, match));
        }

        // A card finished flying into a seat: lay the seat out again.
        private void Landed(Seat seat, Match match)
        {
            if (seat.Root == null)
                return; // the table was rebuilt meanwhile
            if (seat.Arriving > 0)
                seat.Arriving--;
            Layout(seat, match, false);
            if (seat.IsYours)
            {
                // A card that just appeared starts in its place rather than sliding in from elsewhere.
                int total = seat.SlotCount(maxCardsShown);
                int last = Mathf.Min(seat.Shown.Count - seat.Arriving, total) - 1;
                if (last >= 0 && last < seat.Faces.Count)
                {
                    seat.Faces[last].Rect.anchoredPosition = SlotPosition(seat, last, total);
                    seat.Faces[last].Rect.localScale = Vector3.one * seat.CardScale;
                }
            }
        }

        // ---------- Wheel of Fortune's swap ----------

        // Wheel of Fortune: the glow and tag around the card that was just swapped in this hand.
        private void ShowSwap(CardSwap swap)
        {
            Seat player = FindSeat(swap.PlayerId);
            Seat target = FindSeat(swap.TargetId);
            if (player == null || target == null)
                return;

            MarkSwap(player, swap.PlayerIndex);
            MarkSwap(target, swap.TargetIndex);

            int viewer = matchController.ViewerId;
            bool known = swap.CardsKnown && (viewer == swap.PlayerId || viewer == swap.TargetId);
            if (_flights != null && TrySeatSpot(player, out Vector2 a, out float aPixels, out float aAngle) && TrySeatSpot(target, out Vector2 b, out float bPixels, out float bAngle))
            {
                float time = drawFlightTime * 1.6f;
                // The card the player gave flies to the target; the one it got flies back.
                _flights.Fly(known ? swap.Given : default, a, aAngle, _flights.ScaleForPixels(aPixels), b, bAngle, _flights.ScaleForPixels(bPixels), !known, !known, time, null);
                _flights.Fly(known ? swap.Received : default, b, bAngle, _flights.ScaleForPixels(bPixels), a, aAngle, _flights.ScaleForPixels(aPixels), !known, !known, time, null);
            }
            Debug.Log($"TableHandsView: Wheel of Fortune swapped card {swap.PlayerIndex + 1} of Apostle {swap.PlayerId} " + $"with card {swap.TargetIndex + 1} of Apostle {swap.TargetId}.");
        }

        // The glow behind the card, a little bigger than it, with a SWAPPED tag above it. Made once per hand.
        private void CreateSwapMark(Seat seat)
        {
            seat.SwapMark = CreateGlow("SwapMark", seat.Root, swapMarkColor, 0.3f);

            RectTransform tagRect = CreateRect("SwapTag", seat.SwapMark.rectTransform);
            tagRect.anchorMin = tagRect.anchorMax = new Vector2(0.5f, 1f);
            tagRect.pivot = new Vector2(0.5f, 0f);
            tagRect.anchoredPosition = new Vector2(0f, -10f);
            tagRect.sizeDelta = new Vector2(CardWidth * 1.4f, 44f);
            TextMeshProUGUI tag = tagRect.gameObject.AddComponent<TextMeshProUGUI>();
            tag.text = "SWAPPED";
            tag.alignment = TextAlignmentOptions.Center;
            tag.fontSize = 34f;
            tag.fontStyle = FontStyles.Bold;
            tag.color = swapMarkColor;
            tag.raycastTarget = false;
            seat.SwapTag = tag;
        }

        // Marks a card swapped by the Wheel of Fortune.
        private void MarkSwap(Seat seat, int index)
        {
            if (seat.SwapMark == null)
                return;
            seat.SwapIndex = index;
            seat.SwapHandSize = seat.Shown.Count;
            seat.SwapTimeLeft = swapMarkSeconds;
            PlaceSwapMark(seat, seat.SlotCount(maxCardsShown));
        }

        // Keeps the mark on its card while the row moves. It goes away once the hand changes (a card played
        // or drawn moves the others), or if that card isn't drawn (an opponent's long hand shows only some).
        private void PlaceSwapMark(Seat seat, int total)
        {
            if (seat.SwapMark == null)
                return;
            bool show = seat.SwapTimeLeft > 0f && seat.SwapIndex >= 0 && seat.SwapIndex < total && seat.Shown.Count == seat.SwapHandSize;
            if (!show)
            {
                seat.SwapTimeLeft = 0f;
                seat.SwapMark.gameObject.SetActive(false);
                return;
            }
            RectTransform rect = seat.SwapMark.rectTransform;
            rect.sizeDelta = new Vector2(CardWidth, CardHeight) * seat.CardScale + new Vector2(70f, 70f);
            rect.anchoredPosition = SlotPosition(seat, seat.SwapIndex, total);
            seat.SwapMark.gameObject.SetActive(true);
        }

        // Pulses the marks and fades them out at the end.
        private void TickSwapMarks(float delta)
        {
            float pulse = (Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / 0.8f) + 1f) / 2f;
            for (int i = 0; i < _seats.Count; i++)
            {
                Seat seat = _seats[i];
                if (seat.SwapMark == null || seat.SwapTimeLeft <= 0f)
                    continue;
                seat.SwapTimeLeft -= delta;
                if (seat.SwapTimeLeft <= 0f)
                {
                    seat.SwapMark.gameObject.SetActive(false);
                    continue;
                }
                float alpha = Mathf.Lerp(0.55f, 1f, pulse) * Mathf.Clamp01(seat.SwapTimeLeft / 0.6f);
                Color color = swapMarkColor;
                color.a *= alpha;
                seat.SwapMark.color = color;
                if (seat.SwapTag != null)
                    seat.SwapTag.alpha = Mathf.Clamp01(seat.SwapTimeLeft / 0.6f);
            }
        }
        // The seat of an Apostle, or null.
        private Seat FindSeat(int apostleId)
        {
            for (int i = 0; i < _seats.Count; i++)
            {
                if (_seats[i].ApostleId == apostleId)
                    return _seats[i];
            }
            return null;
        }

        // The screen spot of a card in this hand (the last one shown), or the middle of its tray.
        private bool TrySeatSpot(Seat seat, out Vector2 spot, out float pixels, out float angle)
        {
            RectTransform rect = null;
            if (seat.IsYours)
            {
                for (int k = seat.Faces.Count - 1; k >= 0 && rect == null; k--)
                {
                    if (seat.Faces[k].gameObject.activeSelf)
                        rect = seat.Faces[k].Rect;
                }
            }
            else
            {
                for (int k = seat.Backs.Count - 1; k >= 0 && rect == null; k--)
                {
                    if (seat.Backs[k].gameObject.activeSelf)
                        rect = seat.Backs[k];
                }
            }
            if (rect == null)
            {
                spot = default;
                pixels = 0f;
                angle = 0f;
                return false;
            }
            return TableSpot.TryGet(rect, _camera, _corners, out spot, out pixels, out angle);
        }

        // ---------- Glows ----------

        // Which hands glow: the turn's Apostle (gold) and every Apostle with DEF above 0 (blue).
        private void UpdateGlows(Match match)
        {
            for (int i = 0; i < _seats.Count; i++)
            {
                Seat seat = _seats[i];
                ApostleState apostle = FindApostle(match, seat.ApostleId);
                bool alive = apostle != null && !apostle.IsEliminated && !match.IsOver;
                seat.IsTurn = alive && match.CurrentApostle != null && match.CurrentApostle.Id == seat.ApostleId;
                seat.HasDefense = alive && apostle.Defense > 0;
                if (seat.TurnGlow != null)
                    seat.TurnGlow.gameObject.SetActive(seat.IsTurn);
                if (seat.DefenseGlow != null)
                    seat.DefenseGlow.gameObject.SetActive(seat.HasDefense);
                // While it has DEF, the tray's edge also turns the defense colour, and thicker.
                if (seat.Edge != null)
                {
                    seat.Edge.effectColor = seat.HasDefense ? defenseGlowColor : ApostleColors.Get(seat.ApostleId);
                    seat.Edge.effectDistance = seat.HasDefense ? new Vector2(6f, -6f) : new Vector2(3f, -3f);
                }
            }
        }

        // The turn's glow breathes slowly; the DEF glow shimmers faintly. A few seats, no allocation.
        private void PulseGlows()
        {
            float speed = Mathf.Max(0.1f, MotionStyle.Speed);
            float time = Time.unscaledTime * speed;
            // On Low graphics the glows hold still; an unchanged colour does not redraw the hand.
            bool animate = GraphicsQuality.AnimateUi;
            float breath = animate ? (Mathf.Sin(time * Mathf.PI * 2f / 2.4f) + 1f) / 2f : 1f;
            float shimmer = animate ? (Mathf.Sin(time * Mathf.PI * 2f / 3.7f) + 1f) / 2f : 1f;
            for (int i = 0; i < _seats.Count; i++)
            {
                Seat seat = _seats[i];
                if (seat.IsTurn && seat.TurnGlow != null)
                {
                    Color color = turnGlowColor;
                    color.a *= Mathf.Lerp(0.45f, 0.95f, breath);
                    seat.TurnGlow.color = color;
                }
                if (seat.HasDefense && seat.DefenseGlow != null)
                {
                    Color color = defenseGlowColor;
                    color.a *= Mathf.Lerp(0.85f, 1f, shimmer);
                    seat.DefenseGlow.color = color;
                }
            }
        }

        // On the screen your cards float on their own, Balatro style: the tray keeps its size for the
        // layout (the Capacity bar sits on it) but draws nothing, and the glows around it go.
        private static void HideTray(Seat seat)
        {
            seat.Tray.GetComponent<Image>().enabled = false;
            Destroy(seat.Edge);
            seat.Edge = null;
            Destroy(seat.TurnGlow.gameObject);
            Destroy(seat.DefenseGlow.gameObject);
            seat.TurnGlow = null;
            seat.DefenseGlow = null;
        }

        // width: smaller makes a wider band (it scales the glow sprite's edge).
        private static Image CreateGlow(string name, RectTransform parent, Color color, float width)
        {
            RectTransform rect = CreateRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = GothicTheme.EdgeGlowSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = width;
            image.color = color;
            image.raycastTarget = false;
            rect.gameObject.SetActive(false);
            return image;
        }

        // The glow covers the tray plus this much on every side.
        private static void FitGlow(Image glow, RectTransform tray, float outset)
        {
            if (glow == null)
                return;
            RectTransform rect = glow.rectTransform;
            rect.sizeDelta = tray.sizeDelta + new Vector2(2f * outset, 2f * outset);
            rect.anchoredPosition = tray.anchoredPosition;
        }

        // Looks up an Apostle by id, or null when it is missing.
        private static ApostleState FindApostle(Match match, int id)
        {
            IReadOnlyList<ApostleState> apostles = match.Battlefield.Apostles;
            for (int i = 0; i < apostles.Count; i++)
            {
                if (apostles[i].Id == id)
                    return apostles[i];
            }
            return null;
        }

        // Creates a child RectTransform on the parent's layer.
        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }
    }
}
