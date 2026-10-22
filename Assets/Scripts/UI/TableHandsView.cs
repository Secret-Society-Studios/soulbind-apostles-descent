using System.Collections.Generic;
using SoulBind.Core;
using SoulBind.Managers;
using SoulBind.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
        [Tooltip("Opponents' hands bigger than this show this many face-down cards (the label still gives the real count). Your own hand always shows every card.")]
        [SerializeField, Range(1, 15)] private int maxCardsShown = 10;

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

        [Header("Flights")]
        [SerializeField] private float playFlightTime = 0.4f;
        [SerializeField] private float drawFlightTime = 0.5f;
        [Tooltip("How high a flying card arcs, in screen pixels.")]
        [SerializeField] private float arcHeight = 60f;

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
        private readonly Vector3[] _corners = new Vector3[4];
        private RectTransform _rect;
        private Camera _camera;
        private CardFlightLayer _flights;
        private Seat _yours;
        private Match _builtFor;
        private int _builtViewer;
        private int _lastDiscardCount;
        private int _lastDrawCount;
        private bool _pendingRebuild;
        private bool _ready;
        private CardView _hovered;
        private CardView _dragged;

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
                preview.gameObject.SetActive(false);
            }
            if (previewReason != null)
                previewReason.gameObject.SetActive(false);
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
            canvas.sortingOrder = 6; // above the piles, so a dragged card passes over them
            canvas.worldCamera = _camera;
            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>(); // your cards can be hovered, clicked and dragged

            // Flying cards are drawn on the screen-space canvas, above everything.
            Canvas screenCanvas = preview != null ? preview.GetComponentInParent<Canvas>() : null;
            if (screenCanvas != null)
                _flights = CardFlightLayer.Create(screenCanvas.rootCanvas, cardPrefab, drawPile.CardBackTemplate, arcHeight);

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

            // The screen now belongs to someone else (hot-seat testing): let cards land, then turn the table.
            if (viewer != _builtViewer)
            {
                if (_flights != null && _flights.ActiveCount > 0)
                    _pendingRebuild = true;
                else
                    BuildSeats(match, viewer);
                return;
            }

            if (_hovered != null)
                ShowPreview(_hovered);
        }

        // ---- Seats ----

        private void BuildSeats(Match match, int viewer)
        {
            _pendingRebuild = false;
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
                seat.Root.sizeDelta = new Vector2(length / scale, depth / scale);
                Vector3 local = (center - board.transform.position) / scale;
                seat.Root.anchoredPosition = new Vector2(local.x, local.y);
                seat.Root.localRotation = Quaternion.Euler(0f, 0f, rotation);

                // Glows behind the tray: gold on the turn's Apostle, blue while it has DEF.
                // The gold one is wide, the blue one tight to the tray, so both show when both apply.
                seat.TurnGlow = CreateGlow("TurnGlow", seat.Root, turnGlowColor, 0.25f);
                seat.DefenseGlow = CreateGlow("DefenseGlow", seat.Root, defenseGlowColor, 0.38f);

                // A dark tray behind the cards, edged in the Apostle's color.
                seat.Tray = CreateRect("Tray", seat.Root);
                Image tray = seat.Tray.gameObject.AddComponent<Image>();
                tray.color = trayColor;
                tray.raycastTarget = false;
                Outline edge = seat.Tray.gameObject.AddComponent<Outline>();
                edge.effectColor = ApostleColors.Get(apostle.Id);
                edge.effectDistance = new Vector2(3f, -3f);
                seat.Edge = edge;

                // The name, on the outer edge, always upright on screen.
                RectTransform labelRect = CreateRect("Label", seat.Root);
                labelRect.sizeDelta = new Vector2(Mathf.Max(260f, seat.Root.sizeDelta.x * 0.6f), LabelHeight);
                labelRect.anchoredPosition = new Vector2(0f, -seat.Root.sizeDelta.y / 2f + LabelHeight / 2f);
                labelRect.localRotation = Quaternion.Euler(0f, 0f, -rotation);
                TextMeshProUGUI label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
                label.alignment = TextAlignmentOptions.Center;
                label.fontSize = labelFontSize;
                label.enableAutoSizing = true;
                label.fontSizeMin = 18f;
                label.fontSizeMax = labelFontSize;
                label.fontStyle = FontStyles.Bold;
                label.color = ApostleColors.Get(apostle.Id);
                label.raycastTarget = false;
                seat.Label = label;

                IReadOnlyList<ArcanaId> hand = match.GetHand(apostle.Id);
                for (int c = 0; c < hand.Count; c++)
                    seat.Shown.Add(hand[c]);
                _seats.Add(seat);
                if (seat.IsYours)
                    _yours = seat;
                Layout(seat, match, true);
            }

            _builtFor = match;
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
            // Wheel of Fortune played as part of an attack swaps a card with the target: shown below.
            bool wheelPlayed = _newDiscards[(int)ArcanaId.WheelOfFortune] > 0;

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

            if (toPile > 0 && discardPile != null)
                discardPile.HoldTop(toPile);
            // Who it swapped with: recorded by the match (or, on a joined player's copy, the attack target).
            int swappedWith = match.LastSwapTargetId != 0 ? match.LastSwapTargetId
                : match.Engagement == EngagementState.Engaged ? match.EngagedTargetId : 0;
            if (wheelPlayed && swappedWith != 0 && match.CurrentApostle != null)
                ShowSwap(match.CurrentApostle.Id, swappedWith);
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

            ApostleState apostle = FindApostle(match, seat.ApostleId);
            int count = Mathf.Max(0, seat.Shown.Count - seat.Arriving);
            string who = seat.IsYours ? $"You (A{seat.ApostleId})" : $"A{seat.ApostleId}";
            seat.Label.text = apostle != null && apostle.IsEliminated ? $"{who}  OUT" : $"{who}  {count} cards";
        }

        // Cards sit toward the board, leaving the outer edge for the name.
        private static float RowY(Seat seat) => LabelHeight / 2f;

        private Vector2 SlotPosition(Seat seat, int slot, int total)
        {
            float offset = slot - (total - 1) / 2f;
            return new Vector2(offset * Spacing(seat, total), RowY(seat));
        }

        // Side by side with a small gap; squeezed together (overlapping) only if the row would not fit.
        private float Spacing(Seat seat, int total)
        {
            if (total <= 1)
                return 0f;
            float cardWidth = CardWidth * seat.CardScale;
            float room = seat.Root.sizeDelta.x - cardWidth - 2f * trayPadding - 16f;
            return Mathf.Clamp(room / (total - 1), 6f, cardWidth + cardGap);
        }

        private CardView CreateFace(Seat seat)
        {
            CardView face = Instantiate(cardPrefab, seat.Root);
            RectTransform rect = face.Rect;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one * seat.CardScale;
            if (face.GetComponent<CanvasGroup>() == null)
                face.gameObject.AddComponent<CanvasGroup>();
            face.HoverStarted += OnHoverStarted;
            face.HoverEnded += OnHoverEnded;
            face.Clicked += OnClicked;
            face.DragStarted += OnDragStarted;
            face.Dragged += OnDragged;
            face.DragEnded += OnDragEnded;
            return face;
        }

        private RectTransform CreateBack(Seat seat)
        {
            RectTransform back = Instantiate(drawPile.CardBackTemplate, seat.Root);
            back.name = "Card";
            back.anchorMin = new Vector2(0.5f, 0.5f);
            back.anchorMax = new Vector2(0.5f, 0.5f);
            back.pivot = new Vector2(0.5f, 0.5f);
            back.sizeDelta = new Vector2(CardWidth, CardHeight);
            back.localScale = Vector3.one;
            back.localRotation = Quaternion.identity;
            return back;
        }

        // ---- Your cards: lifting, hovering, clicking and dragging ----

        private void Update()
        {
            if (_pendingRebuild && (_flights == null || _flights.ActiveCount == 0))
            {
                Match match = matchController.CurrentMatch;
                if (match != null)
                    BuildSeats(match, matchController.ViewerId);
            }

            PulseGlows();

            Seat seat = _yours;
            if (seat == null)
                return;

            int total = seat.SlotCount(maxCardsShown);
            float t = 1f - Mathf.Exp(-moveSpeed * Time.unscaledDeltaTime);
            for (int k = 0; k < seat.Faces.Count && k < total; k++)
            {
                CardView face = seat.Faces[k];
                if (face == _dragged || !face.gameObject.activeSelf)
                    continue;

                bool hovered = face == _hovered;
                bool selected = matchController.HasSelectedCard && matchController.SelectedCard == face.CardId;
                Vector2 target = SlotPosition(seat, k, total);
                if (hovered)
                    target.y += hoverLift;
                else if (selected)
                    target.y += selectedLift;
                float scale = seat.CardScale * (hovered ? hoverScale : 1f);

                RectTransform rect = face.Rect;
                rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, target, t);
                rect.localRotation = Quaternion.identity;
                rect.localScale = Vector3.Lerp(rect.localScale, Vector3.one * scale, t);
            }
        }

        private void OnHoverStarted(CardView card)
        {
            if (_dragged != null)
                return;
            _hovered = card;
            card.transform.SetAsLastSibling(); // in front of its neighbours
            ShowPreview(card);
        }

        private void OnHoverEnded(CardView card)
        {
            if (_hovered == card)
                EndHover();
        }

        private void EndHover()
        {
            if (_hovered != null)
                RestoreOrder();
            _hovered = null;
            if (preview != null)
                preview.gameObject.SetActive(false);
            if (previewReason != null)
                previewReason.gameObject.SetActive(false);
        }

        // Puts your cards back in hand order (after the tray) once a hovered card was brought to the front.
        private void RestoreOrder()
        {
            if (_yours == null)
                return;
            for (int i = 0; i < _yours.Faces.Count; i++)
                _yours.Faces[i].transform.SetAsLastSibling();
        }

        private void ShowPreview(CardView card)
        {
            if (preview != null)
            {
                preview.gameObject.SetActive(true);
                preview.Show(CardCatalog.Get(card.CardId));
                preview.SetSelected(false);
                preview.SetDimmed(false);
            }

            if (previewReason != null)
            {
                string reason = matchController.GetUnplayableReason(card.CardId);
                previewReason.gameObject.SetActive(reason != null);
                if (reason != null)
                    previewReason.text = reason;
            }
        }

        private void OnClicked(CardView card)
        {
            matchController.ClickCard(card.CardId);
        }

        private void OnDragStarted(CardView card, PointerEventData eventData)
        {
            // Cards that can't be played (dimmed) stay in the hand.
            if (_dragged != null || matchController.GetUnplayableReason(card.CardId) != null
                || matchController.IsChoosingComboTarget)
                return;

            _dragged = card;
            EndHover();
            card.transform.SetAsLastSibling();
            card.GetComponent<CanvasGroup>().blocksRaycasts = false;
            FollowPointer(card, eventData);
        }

        private void OnDragged(CardView card, PointerEventData eventData)
        {
            if (card == _dragged)
                FollowPointer(card, eventData);
        }

        private void OnDragEnded(CardView card, PointerEventData eventData)
        {
            if (card != _dragged)
                return;
            EndDrag();

            // Played cards leave the hand when the controller refreshes it; anything else slides back.
            Vector2 point = eventData.position;
            if (discardPile != null && discardPile.ContainsScreenPoint(point))
                matchController.DropCardOnPile(card.CardId);
            else
                matchController.TryDropCardOnBoard(card.CardId, point);
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

        // ---- Flying cards ----

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

        // ---- Wheel of Fortune's swap ----

        // Two face-down cards cross between the two hands, so the swap can be seen.
        private void ShowSwap(int playerId, int targetId)
        {
            Seat player = FindSeat(playerId);
            Seat target = FindSeat(targetId);
            if (_flights == null || player == null || target == null)
                return;
            if (!TrySeatSpot(player, out Vector2 a, out float aPixels, out float aAngle)
                || !TrySeatSpot(target, out Vector2 b, out float bPixels, out float bAngle))
                return;
            float time = drawFlightTime * 1.6f;
            _flights.Fly(default, a, aAngle, _flights.ScaleForPixels(aPixels), b, bAngle, _flights.ScaleForPixels(bPixels),
                true, true, time, null);
            _flights.Fly(default, b, bAngle, _flights.ScaleForPixels(bPixels), a, aAngle, _flights.ScaleForPixels(aPixels),
                true, true, time, null);
            Debug.Log($"TableHandsView: Wheel of Fortune swapped a card between Apostle {playerId} and Apostle {targetId}.");
        }

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

        // ---- Glows ----

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
            float breath = (Mathf.Sin(time * Mathf.PI * 2f / 2.4f) + 1f) / 2f;
            float shimmer = (Mathf.Sin(time * Mathf.PI * 2f / 3.7f) + 1f) / 2f;
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