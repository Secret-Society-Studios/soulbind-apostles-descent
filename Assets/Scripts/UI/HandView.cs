using System.Collections.Generic;
using SoulBind.Core;
using SoulBind.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SoulBind.UI
{
    /// <summary>
    /// The current Apostle's hand, fanned along the bottom of the screen and partly below its edge.
    /// Hovering a card lifts it and shows a large preview; clicking it asks MatchController to play it.
    /// A card can also be dragged: dropped on the discard pile it is played like a click, dropped on a
    /// red-ringed enemy it attacks that enemy, and dropped anywhere else it slides back to the hand.
    /// Cards that can't be played are dimmed, and the preview says why.
    /// Played cards fly to the discard pile and drawn cards fly in from the draw pile. When a turn ends,
    /// the hand stays on screen until the end-of-turn draw has landed, then the next hand slides in.
    /// Card views are pooled, so changing the hand does not create garbage every turn.
    /// </summary>
    public sealed class HandView : MonoBehaviour
    {
        [SerializeField] private MatchController matchController;
        [SerializeField] private CardView cardPrefab;
        [Tooltip("A large CardView shown while hovering a card.")]
        [SerializeField] private CardView preview;
        [Tooltip("Shown under the preview when the hovered card can't be played.")]
        [SerializeField] private TMP_Text previewReason;
        [Tooltip("Where dragged cards are dropped to play them.")]
        [SerializeField] private DiscardPileView discardPile;
        [Tooltip("Drawn cards fly in from here.")]
        [SerializeField] private DrawPileView drawPile;

        [Header("Layout")]
        [SerializeField] private float cardScale = 0.9f;
        [SerializeField] private float hoverScale = 1.05f;
        [Tooltip("Height of a resting card's center above the bottom of the hand area (cards dip below the screen edge).")]
        [SerializeField] private float restY = 75f;
        [SerializeField] private float selectedY = 135f;
        [SerializeField] private float hoverY = 160f;
        [SerializeField] private float maxSpacing = 150f;
        [SerializeField] private float fanAngle = 3f;
        [SerializeField] private float fanDrop = 5f;
        [SerializeField] private float moveSpeed = 14f;
        [Tooltip("Size of a card while it is dragged, so the board stays visible under it.")]
        [SerializeField] private float dragScale = 0.7f;

        [Header("Flying cards")]
        [SerializeField] private float playFlightTime = 0.35f;
        [SerializeField] private float drawFlightTime = 0.5f;
        [Tooltip("How high a flying card arcs, in canvas pixels.")]
        [SerializeField] private float arcHeight = 60f;

        private readonly List<CardView> _cards = new List<CardView>(12);
        private int _shownCount;
        private CardView _hovered;
        private RectTransform _rect;
        private CardView _dragged;
        private CanvasGroup _group;
        private CardFlightLayer _flights;

        // What the hand shows right now, to see what changed after each action.
        private readonly List<ArcanaId> _shownHand = new List<ArcanaId>(12);
        private Match _shownMatch;
        private int _shownPlayerId;
        private int _lastDiscardCount;
        private int _lastDrawCount;
        private bool _pendingSwitch;

        // Reused while working out the changes, so nothing is allocated.
        private const int ArcanaSlots = 32;
        private readonly int[] _cardCounts = new int[ArcanaSlots];
        private readonly int[] _newDiscards = new int[ArcanaSlots];
        private readonly List<bool> _oldKept = new List<bool>(12);
        private readonly List<bool> _oldUsed = new List<bool>(12);
        private readonly List<CardView> _nextCards = new List<CardView>(12);
        private readonly List<CardView> _freeCards = new List<CardView>(12);
        private readonly List<CardView> _arriving = new List<CardView>(4);

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _group = GetComponent<CanvasGroup>();
            if (_group == null)
                _group = gameObject.AddComponent<CanvasGroup>();

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
            if (matchController == null || cardPrefab == null)
            {
                Debug.LogError("HandView: assign the Match Controller and Card Prefab in the Inspector.", this);
                return;
            }

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
                _flights = CardFlightLayer.Create(canvas.rootCanvas, cardPrefab,
                    drawPile != null ? drawPile.CardBackTemplate : null, arcHeight);
            Refresh();
        }

        private void Refresh()
        {
            if (matchController == null || cardPrefab == null)
                return;

            Match match = matchController.CurrentMatch;
            int playerToShow = match != null && match.CurrentApostle != null ? match.CurrentApostle.Id : 0;

            // A new match, or the first look: just show the hand.
            if (match == null || match != _shownMatch)
            {
                ShowNewHand(match, playerToShow, false);
                return;
            }

            // Animate whatever happened to the hand on screen (even if the turn has just passed).
            ApplyChanges(match);

            if (playerToShow != _shownPlayerId)
            {
                // Let the end-of-turn draw land in this hand before showing the next one.
                if (_flights != null && _flights.ActiveCount > 0)
                    _pendingSwitch = true;
                else
                    ShowNewHand(match, playerToShow, true);
            }

            _group.blocksRaycasts = !_pendingSwitch;
            if (_hovered != null)
                ShowPreview(_hovered);
        }

        // Shows a hand from scratch, with no flights. slideIn drops the cards below the screen first.
        private void ShowNewHand(Match match, int playerId, bool slideIn)
        {
            _pendingSwitch = false;
            _group.blocksRaycasts = true;
            EndHover();
            EndDrag();
            RevealArriving();

            IReadOnlyList<ArcanaId> hand = match != null && playerId != 0 ? match.GetHand(playerId) : null;
            int count = hand != null ? hand.Count : 0;
            while (_cards.Count < count)
                _cards.Add(CreateCard());

            for (int i = 0; i < count; i++)
            {
                if (slideIn)
                    _cards[i].Rect.anchoredPosition = new Vector2(_cards[i].Rect.anchoredPosition.x, -250f);
            }
            Bind(hand, count);
            TakeSnapshot(match, playerId, hand);
        }

        // Works out which cards left the hand and which arrived, keeps each remaining card on its own
        // view (so nothing jumps), and launches the flights.
        private void ApplyChanges(Match match)
        {
            IReadOnlyList<ArcanaId> hand = _shownPlayerId != 0 ? match.GetHand(_shownPlayerId) : null;
            int count = hand != null ? hand.Count : 0;
            int oldCount = _shownHand.Count;
            Deck deck = match.Deck;

            // Cards that went to the discard pile in this action: its newest entries.
            System.Array.Clear(_newDiscards, 0, ArcanaSlots);
            int discardGrowth = deck.DiscardCount - _lastDiscardCount;
            IReadOnlyList<ArcanaId> discards = deck.DiscardPile;
            for (int i = Mathf.Max(0, discards.Count - discardGrowth); i < discards.Count; i++)
                _newDiscards[(int)discards[i]]++;

            // Cards drawn in this action. If the discard pile was shuffled in, every new card was drawn.
            int drawsLeft = deck.DrawCount < _lastDrawCount ? _lastDrawCount - deck.DrawCount
                : deck.DrawCount > _lastDrawCount ? int.MaxValue : 0;

            // Which old cards are still in the hand?
            System.Array.Clear(_cardCounts, 0, ArcanaSlots);
            for (int j = 0; j < count; j++)
                _cardCounts[(int)hand[j]]++;
            _oldKept.Clear();
            _oldUsed.Clear();
            for (int i = 0; i < oldCount; i++)
            {
                int id = (int)_shownHand[i];
                bool kept = _cardCounts[id] > 0;
                if (kept)
                    _cardCounts[id]--;
                _oldKept.Add(kept);
                _oldUsed.Add(false);
            }

            // Cards that left: fly the played and discarded ones to the pile; their views are free again.
            _freeCards.Clear();
            int toPile = 0;
            for (int i = 0; i < oldCount; i++)
            {
                if (_oldKept[i])
                    continue;
                CardView view = _cards[i];
                if (view == _hovered)
                    EndHover();
                if (view == _dragged)
                    EndDrag();

                int id = (int)_shownHand[i];
                if (_newDiscards[id] > 0)
                {
                    _newDiscards[id]--;
                    if (FlyToDiscardPile(view))
                        toPile++;
                }
                _freeCards.Add(view);
            }
            for (int i = oldCount; i < _cards.Count; i++)
                _freeCards.Add(_cards[i]);
            if (toPile > 0 && discardPile != null)
                discardPile.HoldTop(toPile);

            // The new order: kept cards keep their views, new cards take free ones.
            _nextCards.Clear();
            int freeIndex = 0;
            for (int j = 0; j < count; j++)
            {
                CardView view = null;
                for (int i = 0; i < oldCount; i++)
                {
                    if (_oldKept[i] && !_oldUsed[i] && _shownHand[i] == hand[j])
                    {
                        _oldUsed[i] = true;
                        view = _cards[i];
                        break;
                    }
                }

                if (view == null)
                {
                    view = freeIndex < _freeCards.Count ? _freeCards[freeIndex++] : CreateCard();
                    view.Rect.anchoredPosition = new Vector2(0f, -250f);
                    if (drawsLeft > 0)
                    {
                        drawsLeft--;
                        FlyFromDrawPile(view, hand[j]);
                    }
                }
                _nextCards.Add(view);
            }
            for (int k = freeIndex; k < _freeCards.Count; k++)
                _nextCards.Add(_freeCards[k]);

            _cards.Clear();
            _cards.AddRange(_nextCards);
            RestoreOrder();
            Bind(hand, count);
            TakeSnapshot(match, _shownPlayerId, hand);
        }

        // Shows these cards on the first views and hides the rest.
        private void Bind(IReadOnlyList<ArcanaId> hand, int count)
        {
            bool selectionShown = false;
            for (int i = 0; i < _cards.Count; i++)
            {
                CardView card = _cards[i];
                bool shown = i < count;
                card.gameObject.SetActive(shown);
                if (!shown)
                    continue;

                card.Show(CardCatalog.Get(hand[i]));
                card.SetDimmed(matchController.GetUnplayableReason(hand[i]) != null);

                // Highlight one copy of the selected card (the hand may hold several).
                bool selected = !selectionShown && matchController.HasSelectedCard && matchController.SelectedCard == hand[i];
                card.SetSelected(selected);
                selectionShown |= selected;
            }
            _shownCount = count;
        }

        private void TakeSnapshot(Match match, int playerId, IReadOnlyList<ArcanaId> hand)
        {
            _shownMatch = match;
            _shownPlayerId = playerId;
            _shownHand.Clear();
            if (hand != null)
            {
                for (int i = 0; i < hand.Count; i++)
                    _shownHand.Add(hand[i]);
            }
            _lastDiscardCount = match != null ? match.Deck.DiscardCount : 0;
            _lastDrawCount = match != null ? match.Deck.DrawCount : 0;
        }

        // ---- Flying cards ----

        private bool FlyToDiscardPile(CardView view)
        {
            if (_flights == null || discardPile == null
                || !discardPile.TryGetScreenSpot(out Vector2 spot, out float pixelHeight))
                return false;

            RectTransform rect = view.Rect;
            _flights.FlyTo(view.CardId, rect.position, rect.rotation, rect.localScale.x,
                spot, ScaleForPixels(rect, pixelHeight), playFlightTime, discardPile.ReleaseOne);
            return true;
        }

        private void FlyFromDrawPile(CardView view, ArcanaId card)
        {
            if (_flights == null || drawPile == null
                || !drawPile.TryGetScreenSpot(out Vector2 spot, out float pixelHeight))
                return;

            // Invisible in the hand until its flight lands there.
            CanvasGroup group = view.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            _arriving.Add(view);
            _flights.FlyToFollow(card, spot, ScaleForPixels(view.Rect, pixelHeight), view.Rect, drawFlightTime,
                () => Reveal(view));
        }

        // The scale that makes a card this many screen pixels tall on the flight layer.
        private float ScaleForPixels(RectTransform card, float pixelHeight)
        {
            float height = card.rect.height * _flights.transform.lossyScale.y;
            return height > 0f ? pixelHeight / height : 1f;
        }

        private void Reveal(CardView view)
        {
            if (!_arriving.Remove(view))
                return;
            CanvasGroup group = view.GetComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = true;
        }

        private void RevealArriving()
        {
            for (int i = _arriving.Count - 1; i >= 0; i--)
                Reveal(_arriving[i]);
        }

        private void EndDrag()
        {
            if (_dragged == null)
                return;
            _dragged.GetComponent<CanvasGroup>().blocksRaycasts = true;
            _dragged = null;
            if (discardPile != null)
                discardPile.SetDragging(false, false);
        }

        private CardView CreateCard()
        {
            CardView card = Instantiate(cardPrefab, transform);
            RectTransform rect = card.Rect;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -200f); // starts below the screen and slides in
            card.HoverStarted += OnHoverStarted;
            card.HoverEnded += OnHoverEnded;
            card.Clicked += OnClicked;
            card.DragStarted += OnDragStarted;
            card.Dragged += OnDragged;
            card.DragEnded += OnDragEnded;
            // Lets the pointer "see through" the dragged card to the pile and the board.
            if (card.GetComponent<CanvasGroup>() == null)
                card.gameObject.AddComponent<CanvasGroup>();
            return card;
        }

        private void Update()
        {
            // The end-of-turn draw has landed: now show the next Apostle's hand.
            if (_pendingSwitch && (_flights == null || _flights.ActiveCount == 0))
            {
                _pendingSwitch = false;
                Refresh();
            }

            int count = _shownCount;
            if (count == 0)
                return;

            float middle = (count - 1) / 2f;
            float available = Mathf.Max(0f, _rect.rect.width - 200f);
            float spacing = count > 1 ? Mathf.Min(maxSpacing, available / (count - 1)) : 0f;
            float t = 1f - Mathf.Exp(-moveSpeed * Time.unscaledDeltaTime);

            for (int i = 0; i < count; i++)
            {
                CardView card = _cards[i];
                if (card == _dragged)
                    continue; // follows the pointer instead
                float offset = i - middle;
                bool hovered = card == _hovered;
                bool selected = matchController.HasSelectedCard && matchController.SelectedCard == card.CardId;

                float y = hovered ? hoverY : selected ? selectedY : restY - fanDrop * offset * offset;
                float angle = hovered || selected ? 0f : -offset * fanAngle;
                float scale = hovered ? hoverScale : cardScale;

                RectTransform rect = card.Rect;
                rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, new Vector2(offset * spacing, y), t);
                rect.localRotation = Quaternion.Slerp(rect.localRotation, Quaternion.Euler(0f, 0f, angle), t);
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

        // Puts the cards back in hand order after a hovered card was brought to the front.
        private void RestoreOrder()
        {
            for (int i = 0; i < _cards.Count; i++)
                _cards[i].transform.SetSiblingIndex(i);
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

        // ---- Dragging ----

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
            card.Rect.localRotation = Quaternion.identity;
            card.Rect.localScale = Vector3.one * dragScale;
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

            _dragged = null;
            card.GetComponent<CanvasGroup>().blocksRaycasts = true;
            RestoreOrder();
            if (discardPile != null)
                discardPile.SetDragging(false, false);

            // Played cards leave the hand when the controller refreshes it; anything else slides back.
            Vector2 point = eventData.position;
            if (discardPile != null && discardPile.ContainsScreenPoint(point))
                matchController.DropCardOnPile(card.CardId);
            else
                matchController.TryDropCardOnBoard(card.CardId, point);
        }

        private void FollowPointer(CardView card, PointerEventData eventData)
        {
            RectTransform rect = card.Rect;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(_rect, eventData.position,
                    eventData.pressEventCamera, out Vector3 world))
                rect.position = world;

            if (discardPile != null)
                discardPile.SetDragging(true, discardPile.ContainsScreenPoint(eventData.position));
        }
    }
}