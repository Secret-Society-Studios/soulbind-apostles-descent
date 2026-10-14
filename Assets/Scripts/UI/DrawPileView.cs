using System.Collections.Generic;
using SoulBind.Core;
using SoulBind.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoulBind.UI
{
    /// <summary>
    /// The shared draw pile, lying face-down on the table below the discard pile. The stack gets
    /// thinner as cards are drawn, and the count under it says exactly how many are left.
    /// When it runs out, the rules shuffle the discard pile back in and the stack grows again.
    /// Display only: drawing is done by the rules at the end of each turn.
    /// </summary>
    public sealed class DrawPileView : MonoBehaviour
    {
        [SerializeField] private MatchController matchController;
        [Tooltip("One face-down card, kept inactive. The stack is built from copies of it.")]
        [SerializeField] private RectTransform cardBackTemplate;
        [Tooltip("The copies are created under this, bottom card first.")]
        [SerializeField] private RectTransform stack;
        [Tooltip("The card-sized slot printed on the table.")]
        [SerializeField] private RectTransform slot;
        [SerializeField] private TMP_Text countText;
        [Tooltip("Shown while the draw pile is empty.")]
        [SerializeField] private TMP_Text emptyText;

        [Header("Art (optional: empty keeps the placeholder back)")]
        [SerializeField] private Sprite cardBackSprite;

        [Header("On the table")]
        [Tooltip("Height of one card on the table, in world units (a hex tile is about 1).")]
        [SerializeField] private float cardWorldHeight = 2.4f;
        [Tooltip("How far below the middle of the side area the pile sits, in card heights.")]
        [SerializeField] private float heightOffset = 0.65f;

        [Header("Stack")]
        [Tooltip("One card edge is drawn for every this many cards, so a full deck looks thick.")]
        [SerializeField, Range(1, 20)] private int cardsPerLayer = 6;
        [SerializeField, Range(1, 20)] private int maxLayers = 11;
        [Tooltip("How far each card edge shows below the one above it, in canvas pixels.")]
        [SerializeField] private float layerStep = 3f;
        [SerializeField] private float drawLift = 1.12f;
        [SerializeField] private float settleSpeed = 12f;

        private readonly List<RectTransform> _layers = new List<RectTransform>(20);
        private readonly Vector3[] _corners = new Vector3[4];
        private Camera _camera;
        private int _lastCount = -1;

        // The card back art goes on the template first thing, so every copy made later (the stack,
        // flying cards, the opponents' hands) shows it.
        private void Awake()
        {
            if (cardBackTemplate != null)
                ApplyBackArt();
        }

        private void Start()
        {
            if (matchController == null || cardBackTemplate == null || stack == null || slot == null)
            {
                Debug.LogError("DrawPileView: assign Match Controller, Card Back Template, Stack and Slot in the Inspector.", this);
                return;
            }

            _camera = Camera.main;
            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null && canvas.worldCamera == null)
                canvas.worldCamera = _camera;

            cardBackTemplate.gameObject.SetActive(false);
            PlaceOnTable();
            for (int i = 0; i < maxLayers; i++)
                _layers.Add(CreateLayer(i));
            Refresh();
        }

        /// <summary>The face-down card, with the team's art if assigned. Flying drawn cards copy it.</summary>
        public RectTransform CardBackTemplate => cardBackTemplate;

        /// <summary>Where the top card is on screen, and how tall a card looks there (in screen pixels).</summary>
        public bool TryGetScreenSpot(out Vector2 center, out float cardPixelHeight)
        {
            return TableSpot.TryGet(slot, _camera, _corners, out center, out cardPixelHeight);
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

        // The team's card back, when assigned, replaces the placeholder drawing.
        private void ApplyBackArt()
        {
            if (cardBackSprite == null)
                return;

            Image back = cardBackTemplate.GetComponent<Image>();
            if (back != null)
            {
                back.sprite = cardBackSprite;
                back.color = Color.white;
                back.preserveAspect = true;
            }
            for (int i = 0; i < cardBackTemplate.childCount; i++)
                cardBackTemplate.GetChild(i).gameObject.SetActive(false);
        }

        // Lies flat on the board's plane, below the discard pile.
        private void PlaceOnTable()
        {
            BoardView board = matchController.BoardView;
            float slotHeight = slot.rect.height > 0f ? slot.rect.height : 252f;
            float scale = cardWorldHeight / slotHeight;
            transform.localScale = new Vector3(scale, scale, scale);
            transform.rotation = Quaternion.identity; // facing -Z, like the board

            Vector3 center = board != null ? board.SideAreaCenter : transform.position;
            transform.position = center + new Vector3(0f, -cardWorldHeight * heightOffset, -0.01f);
        }

        private RectTransform CreateLayer(int index)
        {
            RectTransform layer = Instantiate(cardBackTemplate, stack);
            layer.name = "Card " + index;
            layer.anchorMin = new Vector2(0.5f, 0.5f);
            layer.anchorMax = new Vector2(0.5f, 0.5f);
            layer.pivot = new Vector2(0.5f, 0.5f);
            layer.sizeDelta = slot.rect.size;
            layer.gameObject.SetActive(false);
            return layer;
        }

        private void Update()
        {
            float t = 1f - Mathf.Exp(-settleSpeed * Time.unscaledDeltaTime);
            for (int i = 0; i < _layers.Count; i++)
            {
                RectTransform layer = _layers[i];
                if (layer.gameObject.activeSelf && layer.localScale.x != 1f)
                    layer.localScale = Mathf.Abs(layer.localScale.x - 1f) < 0.002f
                        ? Vector3.one
                        : Vector3.Lerp(layer.localScale, Vector3.one, t);
            }
        }

        private void Refresh()
        {
            if (_layers.Count == 0)
                return;

            Match match = matchController.CurrentMatch;
            int count = match != null ? match.Deck.DrawCount : 0;

            // Rounded up, so even one card left still shows one card.
            int shown = Mathf.Min((count + cardsPerLayer - 1) / cardsPerLayer, _layers.Count);
            for (int i = 0; i < _layers.Count; i++)
            {
                RectTransform layer = _layers[i];
                bool active = i < shown;
                layer.gameObject.SetActive(active);
                if (active)
                {
                    // Lower cards peek out below the top one, which sits on the slot.
                    layer.anchoredPosition = new Vector2(0f, -(shown - 1 - i) * layerStep);
                }
            }

            // A card was drawn: lift the top card for a moment.
            if (_lastCount >= 0 && count < _lastCount && shown > 0)
                _layers[shown - 1].localScale = Vector3.one * drawLift;
            _lastCount = count;

            if (emptyText != null)
                emptyText.gameObject.SetActive(count == 0);
            if (countText != null)
                countText.text = count == 0 ? string.Empty : count.ToString();
        }
    }
}