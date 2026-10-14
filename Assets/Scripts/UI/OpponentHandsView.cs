using System.Collections.Generic;
using SoulBind.Core;
using SoulBind.Managers;
using SoulBind.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoulBind.UI
{
    /// <summary>
    /// Every opponent's hand, lying face-down on the far side of the table above the board, in Id order
    /// (the same order as their portraits): a straight row of card backs in a dark tray, one tray each. Only the number of cards can be seen, never which cards.
    /// When an opponent plays a card it flies face-down to the discard pile and turns face-up as it lands;
    /// cards they draw fly face-down from the draw pile into their hand.
    /// Display only. The canvas and the seats are built when the match starts, so the only setup is
    /// SoulBind > Add Opponent Hands.
    /// </summary>
    public sealed class OpponentHandsView : MonoBehaviour
    {
        private const float CardWidth = 180f;
        private const float CardHeight = 252f;
        private const float LabelHeight = 46f;
        private const int ArcanaSlots = 32;

        [SerializeField] private MatchController matchController;
        [Tooltip("Its flight layer is shared, so all flying cards are drawn the same way.")]
        [SerializeField] private HandView handView;
        [Tooltip("Gives the card back, and drawn cards fly from it.")]
        [SerializeField] private DrawPileView drawPile;
        [SerializeField] private DiscardPileView discardPile;

        [Header("On the table")]
        [Tooltip("Height of one card on the table, in world units (a hex tile is about 1).")]
        [SerializeField] private float cardWorldHeight = 1.3f;
        [Tooltip("Gap between neighbouring cards in a hand, in canvas pixels. Cards overlap only when a hand is too wide for its place.")]
        [SerializeField] private float cardGap = 10f;
        [Tooltip("The dark tray each hand sits in.")]
        [SerializeField] private Color trayColor = new Color(0.04f, 0.04f, 0.07f, 0.8f);
        [SerializeField] private float trayPadding = 12f;
        [Tooltip("Hands bigger than this show this many cards (the label still gives the real count).")]
        [SerializeField, Range(1, 15)] private int maxCardsShown = 10;

        [Header("Flights")]
        [SerializeField] private float playFlightTime = 0.45f;
        [SerializeField] private float drawFlightTime = 0.5f;

        private sealed class Seat
        {
            public int ApostleId;
            public RectTransform Root;
            public TMP_Text Label;
            public RectTransform Tray;
            public readonly List<RectTransform> Cards = new List<RectTransform>(12);
            public readonly List<ArcanaId> Shown = new List<ArcanaId>(12);
            public int Arriving;
        }

        private readonly List<Seat> _seats = new List<Seat>(GameConstants.MaxPlayers);
        private readonly int[] _cardCounts = new int[ArcanaSlots];
        private readonly int[] _newDiscards = new int[ArcanaSlots];
        private readonly Vector3[] _corners = new Vector3[4];
        private RectTransform _rect;
        private Camera _camera;
        private Match _builtFor;
        private int _builtViewer;
        private int _lastDiscardCount;
        private int _lastDrawCount;
        private bool _ready;

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
            if (matchController == null || drawPile == null || drawPile.CardBackTemplate == null)
            {
                Debug.LogError("OpponentHandsView: assign the Match Controller and the Draw Pile in the Inspector.", this);
                return;
            }

            _camera = Camera.main;
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null)
                canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 5; // above the table and mat sprites
            if (canvas.worldCamera == null)
                canvas.worldCamera = _camera;

            _rect = (RectTransform)transform;
            PlaceOnTable();
            _ready = true;
            Refresh();
        }

        // Lies flat on the board's plane in the strip the camera keeps on screen above the mat.
        private void PlaceOnTable()
        {
            BoardView board = matchController.BoardView;
            float scale = cardWorldHeight / CardHeight;
            _rect.localScale = new Vector3(scale, scale, scale);
            _rect.rotation = Quaternion.identity; // facing -Z, like the board

            if (board == null)
                return;
            _rect.sizeDelta = new Vector2(board.TopAreaWidth / scale, board.TopAreaDepth / scale);
            _rect.position = board.TopAreaCenter + new Vector3(0f, 0f, -0.01f);
        }

        private void Refresh()
        {
            if (!_ready)
                return;
            Match match = matchController.CurrentMatch;
            if (match == null)
                return;

            int viewer = matchController.ViewerId;
            if (match != _builtFor || viewer != _builtViewer)
            {
                BuildSeats(match, viewer);
                return;
            }

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
                        if (FlyToDiscardPile(seat, seat.Shown[i]))
                            toPile++;
                    }
                }

                // Cards that arrived: the drawn ones fly in face-down and appear when they land.
                int added = hand.Count - kept;
                int flying = 0;
                for (int i = 0; i < added && drawsLeft > 0; i++)
                {
                    drawsLeft--;
                    flying++;
                }

                seat.Shown.Clear();
                for (int i = 0; i < hand.Count; i++)
                    seat.Shown.Add(hand[i]);
                seat.Arriving += flying;
                Layout(seat, match);

                for (int i = 0; i < flying; i++)
                    FlyToSeat(seat, match);
            }

            if (toPile > 0 && discardPile != null)
                discardPile.HoldTop(toPile);
            _lastDiscardCount = deck.DiscardCount;
            _lastDrawCount = deck.DrawCount;
        }

        // One seat per opponent of this screen's player, spread evenly along the strip, in Id order.
        private void BuildSeats(Match match, int viewer)
        {
            for (int i = 0; i < _seats.Count; i++)
                Destroy(_seats[i].Root.gameObject);
            _seats.Clear();

            IReadOnlyList<ApostleState> apostles = match.Battlefield.Apostles;
            int count = 0;
            for (int i = 0; i < apostles.Count; i++)
            {
                if (apostles[i].Id != viewer)
                    count++;
            }

            float width = _rect.rect.width;
            float seatWidth = count > 0 ? width / count : width;
            int index = 0;
            for (int i = 0; i < apostles.Count; i++)
            {
                int id = apostles[i].Id;
                if (id == viewer)
                    continue;

                var seat = new Seat { ApostleId = id };
                seat.Root = CreateRect($"Apostle {id} Hand", _rect);
                seat.Root.sizeDelta = new Vector2(seatWidth, CardHeight + LabelHeight);
                seat.Root.anchoredPosition = new Vector2(-width / 2f + seatWidth * (index + 0.5f), 0f);

                // A dark tray behind the cards, edged in the Apostle's color.
                seat.Tray = CreateRect("Tray", seat.Root);
                Image tray = seat.Tray.gameObject.AddComponent<Image>();
                tray.color = trayColor;
                tray.raycastTarget = false;
                Outline edge = seat.Tray.gameObject.AddComponent<Outline>();
                edge.effectColor = ApostleColors.Get(id);
                edge.effectDistance = new Vector2(3f, -3f);

                RectTransform labelRect = CreateRect("Label", seat.Root);
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(1f, 0f);
                labelRect.pivot = new Vector2(0.5f, 0f);
                labelRect.sizeDelta = new Vector2(0f, LabelHeight);
                labelRect.anchoredPosition = Vector2.zero;
                TextMeshProUGUI label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
                label.alignment = TextAlignmentOptions.Center;
                label.fontSize = 30f;
                label.fontStyle = FontStyles.Bold;
                label.color = ApostleColors.Get(id);
                label.raycastTarget = false;
                seat.Label = label;

                IReadOnlyList<ArcanaId> hand = match.GetHand(id);
                for (int c = 0; c < hand.Count; c++)
                    seat.Shown.Add(hand[c]);
                _seats.Add(seat);
                Layout(seat, match);
                index++;
            }

            _builtFor = match;
            _builtViewer = viewer;
            _lastDiscardCount = match.Deck.DiscardCount;
            _lastDrawCount = match.Deck.DrawCount;
        }

        // Fans the face-down cards that have landed. Room is kept for the ones still flying in.
        private void Layout(Seat seat, Match match)
        {
            int total = Mathf.Min(seat.Shown.Count, maxCardsShown);
            int visible = Mathf.Clamp(seat.Shown.Count - seat.Arriving, 0, total);
            while (seat.Cards.Count < total)
                seat.Cards.Add(CreateCard(seat));

            for (int k = 0; k < seat.Cards.Count; k++)
            {
                RectTransform card = seat.Cards[k];
                bool active = k < visible;
                card.gameObject.SetActive(active);
                if (!active)
                    continue;
                PlaceInSlot(seat, card, k, total);
            }

            // The tray fits the row (at least one card wide, so an empty hand still shows its place).
            int slots = Mathf.Max(1, total);
            float rowWidth = (slots - 1) * Spacing(seat, slots) + CardWidth;
            seat.Tray.sizeDelta = new Vector2(rowWidth + 2f * trayPadding, CardHeight + 2f * trayPadding);
            seat.Tray.anchoredPosition = new Vector2(0f, LabelHeight / 2f);

            ApostleState apostle = FindApostle(match, seat.ApostleId);
            int shownCount = seat.Shown.Count - seat.Arriving;
            seat.Label.text = apostle != null && apostle.IsEliminated
                ? $"A{seat.ApostleId}  OUT"
                : $"A{seat.ApostleId}  {Mathf.Max(0, shownCount)} cards";
        }

        // A straight row of upright cards, centered in the seat, like the opponent's hand in a digital card game.
        private void PlaceInSlot(Seat seat, RectTransform card, int slot, int total)
        {
            float offset = slot - (total - 1) / 2f;
            card.anchoredPosition = new Vector2(offset * Spacing(seat, total), LabelHeight / 2f);
            card.localRotation = Quaternion.identity;
        }

        // Side by side with a small gap; squeezed together (overlapping) only if the row would not fit.
        private float Spacing(Seat seat, int total)
        {
            if (total <= 1)
                return 0f;
            float room = seat.Root.rect.width - CardWidth - 2f * trayPadding - 16f;
            return Mathf.Clamp(room / (total - 1), 6f, CardWidth + cardGap);
        }

        private RectTransform CreateCard(Seat seat)
        {
            RectTransform card = Instantiate(drawPile.CardBackTemplate, seat.Root);
            card.name = "Card";
            card.anchorMin = new Vector2(0.5f, 0.5f);
            card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(CardWidth, CardHeight);
            card.localScale = Vector3.one;
            card.SetAsLastSibling();
            return card;
        }

        // ---- Flying cards ----

        // From the top card of the hand to the discard pile, turning face-up as it lands.
        private bool FlyToDiscardPile(Seat seat, ArcanaId card)
        {
            CardFlightLayer flights = handView != null ? handView.Flights : null;
            if (flights == null || discardPile == null
                || !discardPile.TryGetScreenSpot(out Vector2 to, out float toPixels)
                || !TryGetTopCardSpot(seat, out Vector2 from, out float fromPixels))
                return false;

            flights.Fly(card, from, flights.ScaleForPixels(fromPixels), to, flights.ScaleForPixels(toPixels),
                true, false, playFlightTime, discardPile.ReleaseOne);
            return true;
        }

        // From the draw pile to the next free place in the hand, face-down all the way.
        private void FlyToSeat(Seat seat, Match match)
        {
            CardFlightLayer flights = handView != null ? handView.Flights : null;
            int total = Mathf.Min(seat.Shown.Count, maxCardsShown);
            if (flights == null || total == 0 || drawPile == null
                || !drawPile.TryGetScreenSpot(out Vector2 from, out float fromPixels))
            {
                Landed(seat, match);
                return;
            }

            // Where the card will lie: the last place in the fan (the template stands in for it).
            RectTransform slot = seat.Cards[total - 1];
            bool wasActive = slot.gameObject.activeSelf;
            PlaceInSlot(seat, slot, total - 1, total);
            bool found = TableSpot.TryGet(slot, _camera, _corners, out Vector2 to, out float toPixels);
            slot.gameObject.SetActive(wasActive);
            if (!found)
            {
                Landed(seat, match);
                return;
            }

            // The card's face never shows: any card id will do for the hidden front.
            flights.Fly(default, from, flights.ScaleForPixels(fromPixels), to, flights.ScaleForPixels(toPixels),
                true, true, drawFlightTime, () => Landed(seat, match));
        }

        private void Landed(Seat seat, Match match)
        {
            if (seat.Arriving > 0)
                seat.Arriving--;
            if (seat.Root != null)
                Layout(seat, match);
        }

        private bool TryGetTopCardSpot(Seat seat, out Vector2 center, out float pixelHeight)
        {
            for (int k = seat.Cards.Count - 1; k >= 0; k--)
            {
                if (seat.Cards[k].gameObject.activeSelf)
                    return TableSpot.TryGet(seat.Cards[k], _camera, _corners, out center, out pixelHeight);
            }
            center = default;
            pixelHeight = 0f;
            return false;
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