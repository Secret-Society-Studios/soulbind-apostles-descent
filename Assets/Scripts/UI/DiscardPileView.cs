using System.Collections.Generic;
using SoulBind.Core;
using SoulBind.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SoulBind.UI
{
    /// <summary>
    /// The discard pile, lying on the mat in its right corner. Every played card lands on top of the
    /// stack, a little turned and shifted like a real pile, so the last few cards peek out underneath.
    /// It sits on a World Space canvas, so it tilts with the table under the perspective camera.
    /// It glows while a card is dragged, so the player knows where to drop it.
    /// Display only: HandView asks it whether a drop landed on it, and MatchController plays the card.
    /// </summary>
    public sealed class DiscardPileView : MonoBehaviour
    {
        [SerializeField] private MatchController matchController;
        [SerializeField] private CardView cardPrefab;
        [Tooltip("The cards are created under this, oldest first, so the newest is drawn on top.")]
        [SerializeField] private RectTransform stack;
        [Tooltip("The card-sized slot. Drops are checked against it.")]
        [SerializeField] private RectTransform slot;
        [SerializeField] private TMP_Text countText;
        [Tooltip("Shown while the pile is empty.")]
        [SerializeField] private TMP_Text emptyText;
        [Tooltip("Shown while a card is being dragged.")]
        [SerializeField] private Image glow;
        [Tooltip("The screen-space HUD canvas the discard viewer opens on. Empty: the MatchHud's canvas.")]
        [SerializeField] private Canvas screenCanvas;


        [Header("Stack")]
        [Tooltip("Only the top cards are drawn; the ones under them can't be seen anyway.")]
        [SerializeField, Range(1, 20)] private int maxShown = 8;
        [SerializeField] private float maxTurn = 10f;
        [SerializeField] private float maxShift = 10f;
        [Tooltip("How big a newly played card starts before it settles onto the pile.")]
        [SerializeField] private float landingScale = 1.35f;
        [SerializeField] private float settleSpeed = 12f;
        [SerializeField] private float pulseSpeed = 4f;

        private readonly List<CardView> _views = new List<CardView>(20);
        private readonly Vector3[] _corners = new Vector3[4];
        private int _held;
        private Camera _camera;
        private int _lastCount;
        private bool _dragging;
        private bool _overPile;
        private DiscardViewer _viewer;
        private bool _pressedOnPile;

        private void Awake()
        {
            if (glow != null)
                glow.enabled = false;
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
            if (matchController == null || cardPrefab == null || stack == null || slot == null)
            {
                Debug.LogError("DiscardPileView: assign Match Controller, Card Prefab, Stack and Slot in the Inspector.", this);
                return;
            }

            _camera = Camera.main;
            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null && canvas.worldCamera == null)
                canvas.worldCamera = _camera;

            PlaceOnTable();
            for (int i = 0; i < maxShown; i++)
                _views.Add(CreateView());
            CreateViewer();
            Refresh();
        }

        // The full list of discarded cards, on the HUD's screen canvas.
        private void CreateViewer()
        {
            if (screenCanvas == null)
            {
                MatchHud hud = FindAnyObjectByType<MatchHud>();
                Canvas hudCanvas = hud != null ? hud.GetComponentInParent<Canvas>() : null;
                if (hudCanvas != null)
                    screenCanvas = hudCanvas.rootCanvas;
            }
            if (screenCanvas == null)
            {
                Debug.LogWarning("DiscardPileView: no screen canvas found, so the discard pile can't be opened.", this);
                return;
            }
            _viewer = DiscardViewer.Create(screenCanvas, matchController, cardPrefab, () => _held);
        }

        /// <summary>True if this screen point is over the pile (used when a dragged card is dropped).</summary>
        public bool ContainsScreenPoint(Vector2 screenPoint)
        {
            // A ray from the camera onto the pile's tilted rectangle.
            return slot != null && _camera != null
                && RectTransformUtility.RectangleContainsScreenPoint(slot, screenPoint, _camera);
        }

        /// <summary>
        /// Hides the top cards for now: they are still flying here. Call ReleaseOne as each one lands.
        /// </summary>
        public void HoldTop(int cards)
        {
            _held += Mathf.Max(0, cards);
            Refresh();
            if (_viewer != null)
                _viewer.Refresh();
        }

        /// <summary>A flying card has landed: show it on top of the pile.</summary>
        public void ReleaseOne()
        {
            if (_held > 0)
                _held--;
            Refresh();
            if (_viewer != null)
                _viewer.Refresh();
        }

        /// <summary>
        /// Where the pile's top card is on screen, and how tall a card looks there (in screen pixels),
        /// so a flying card can land exactly on it.
        /// </summary>
        public bool TryGetScreenSpot(out Vector2 center, out float cardPixelHeight)
        {
            return TableSpot.TryGet(slot, _camera, _corners, out center, out cardPixelHeight);
        }

        /// <summary>Called by HandView while a card is dragged. The glow is brighter over the pile.</summary>
        public void SetDragging(bool dragging, bool overPile)
        {
            _dragging = dragging;
            _overPile = dragging && overPile;
            if (glow != null)
                glow.enabled = dragging;
        }

        // Lies flat on the mat, in its right corner beside the board.
        private void PlaceOnTable()
        {
            BoardView board = matchController.BoardView;
            float cardHeight = board != null ? board.PileCardHeight : 2.4f;
            float slotHeight = slot.rect.height > 0f ? slot.rect.height : 252f;
            float scale = cardHeight / slotHeight;
            transform.localScale = new Vector3(scale, scale, scale);
            transform.rotation = Quaternion.identity; // facing -Z, like the board

            // A hair toward the camera so it never fights with the mat.
            Vector3 spot = board != null ? board.DiscardPileSpot : transform.position;
            transform.position = spot + new Vector3(0f, 0f, -0.01f);
        }

        private CardView CreateView()
        {
            CardView view = Instantiate(cardPrefab, stack);
            RectTransform rect = view.Rect;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = slot.rect.size;
            view.SetSelected(false);
            view.SetDimmed(false);
            view.gameObject.SetActive(false);
            return view;
        }

        private void Update()
        {
            CheckOpenViewer();
            float t = 1f - Mathf.Exp(-settleSpeed * Time.unscaledDeltaTime);
            for (int i = 0; i < _views.Count; i++)
            {
                Transform view = _views[i].transform;
                if (view.gameObject.activeSelf && view.localScale.x != 1f)
                    view.localScale = Mathf.Abs(view.localScale.x - 1f) < 0.002f
                        ? Vector3.one
                        : Vector3.Lerp(view.localScale, Vector3.one, t);
            }

            if (_dragging && glow != null)
            {
                Color color = glow.color;
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed);
                color.a = _overPile ? 1f : Mathf.Lerp(0.35f, 0.75f, pulse);
                glow.color = color;
            }
        }

        private void Refresh()
        {
            if (_views.Count == 0)
                return;

            Match match = matchController.CurrentMatch;
            IReadOnlyList<ArcanaId> pile = match != null ? match.Deck.DiscardPile : null;
            int total = pile != null ? pile.Count : 0;
            if (_held > total)
                _held = total; // the pile was shuffled back into the draw pile meanwhile
            int count = total - _held; // cards still in the air are not shown yet
            int shown = Mathf.Min(count, _views.Count);
            int first = count - shown; // pile index of the lowest card drawn

            for (int i = 0; i < _views.Count; i++)
            {
                CardView view = _views[i];
                bool active = i < shown;
                view.gameObject.SetActive(active);
                if (!active)
                    continue;

                int pileIndex = first + i;
                view.Show(CardCatalog.Get(pile[pileIndex]));

                // Each card keeps the same turn and shift for as long as it is in the pile.
                RectTransform rect = view.Rect;
                rect.anchoredPosition = new Vector2(Jitter(pileIndex, 8) * maxShift, Jitter(pileIndex, 16) * maxShift);
                rect.localRotation = Quaternion.Euler(0f, 0f, Jitter(pileIndex, 24) * maxTurn);
            }

            // A card was just played: drop it onto the pile.
            if (count > _lastCount && shown > 0)
                _views[shown - 1].transform.localScale = Vector3.one * landingScale;
            _lastCount = count;

            if (emptyText != null)
                emptyText.gameObject.SetActive(count == 0);
            if (countText != null)
                countText.text = count == 0 ? string.Empty : count.ToString();
        }


        // A click or tap that starts and ends on the pile opens the viewer. Opening on release (not press)
        // means the same press can't land on the viewer's background and close it straight away, and a card
        // dragged from the hand and dropped here is never mistaken for a click (that press began on the hand).
                // A click or tap that starts and ends on the pile opens the viewer.
        private void CheckOpenViewer()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null)
                return;
            Vector2 screenPoint = pointer.position.ReadValue();

            

            if (_viewer == null || _viewer.IsOpen)
            {
                _pressedOnPile = false;
                return;
            }

            if (pointer.press.wasPressedThisFrame)
            {
                bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                _pressedOnPile = !_dragging && !overUi && ContainsScreenPoint(screenPoint);
            }
            else if (pointer.press.wasReleasedThisFrame)
            {
                if (_pressedOnPile && !_dragging && ContainsScreenPoint(screenPoint))
                    _viewer.Open();
                _pressedOnPile = false;
            }
        }
        // A steady value from -1 to 1 for this pile position, so a card doesn't move when others land.
        private static float Jitter(int index, int shift)
        {
            uint hash = (uint)(index + 1) * 2654435761u;
            return ((hash >> shift) & 0xFF) / 127.5f - 1f;
        }
    }
}