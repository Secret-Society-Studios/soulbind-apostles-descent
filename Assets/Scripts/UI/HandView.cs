using System.Collections.Generic;
using SoulBind.Core;
using SoulBind.Managers;
using TMPro;
using UnityEngine;

namespace SoulBind.UI
{
    /// <summary>
    /// The current Apostle's hand, fanned along the bottom of the screen and partly below its edge.
    /// Hovering a card lifts it and shows a large preview; clicking it asks MatchController to play it.
    /// Cards that can't be played are dimmed, and the preview says why.
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

        private readonly List<CardView> _cards = new List<CardView>(12);
        private int _shownCount;
        private CardView _hovered;
        private RectTransform _rect;

        private void Awake()
        {
            _rect = (RectTransform)transform;
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
                Debug.LogError("HandView: assign the Match Controller and Card Prefab in the Inspector.", this);
            Refresh();
        }

        private void Refresh()
        {
            if (matchController == null || cardPrefab == null)
                return;

            Match match = matchController.CurrentMatch;
            ApostleState current = match != null ? match.CurrentApostle : null;
            IReadOnlyList<ArcanaId> hand = current != null ? match.GetHand(current.Id) : null;
            int count = hand != null ? hand.Count : 0;

            while (_cards.Count < count)
                _cards.Add(CreateCard());

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

            if (_hovered != null && !_hovered.gameObject.activeSelf)
                EndHover();
            else if (_hovered != null)
                ShowPreview(_hovered);
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
            return card;
        }

        private void Update()
        {
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
    }
}