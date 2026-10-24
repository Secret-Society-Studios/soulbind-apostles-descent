using System;
using System.Collections.Generic;
using SoulBind.Core;
using SoulBind.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SoulBind.UI
{
    /// <summary>
    /// A screen listing every card in the discard pile, newest first. The discard pile is public
    /// information, so any player may open it: click or tap the pile on the table. Esc, the X button or
    /// a click outside the panel closes it. It stays live while open, so a card played by someone else
    /// shows up, and a reshuffle (draw pile empty) empties it.
    /// DiscardPileView creates it at runtime on the HUD's screen canvas, like MatchOverlay, so it needs
    /// no setup in the scene. Display only.
    /// </summary>
    public sealed class DiscardViewer : MonoBehaviour
    {
        private const float CellHeight = 230f;
        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.72f);

        /// <summary>True while any discard viewer is open (MatchController leaves Esc to it then).</summary>
        public static bool IsAnyOpen { get; private set; }

        private MatchController _controller;
        private CardView _cardPrefab;
        private Func<int> _heldCount; // cards still flying to the pile are not shown yet
        private RectTransform _content;
        private ScrollRect _scroll;
        private TMP_Text _title;
        private TMP_Text _emptyText;
        private readonly List<CardView> _cards = new List<CardView>(CardCatalog.DeckSize);

        public bool IsOpen => gameObject.activeSelf;

        // Reset on every Play, even with domain reload turned off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => IsAnyOpen = false;

        public static DiscardViewer Create(Canvas canvas, MatchController controller, CardView cardPrefab,
            Func<int> heldCount)
        {
            var go = new GameObject("DiscardViewer", typeof(RectTransform));
            go.layer = canvas.gameObject.layer;
            var root = (RectTransform)go.transform;
            root.SetParent(canvas.transform, false);
            Stretch(root);

            DiscardViewer viewer = go.AddComponent<DiscardViewer>();
            viewer._controller = controller;
            viewer._cardPrefab = cardPrefab;
            viewer._heldCount = heldCount;
            viewer.Build(root);
            go.SetActive(false);
            return viewer;
        }

        public void Open()
        {
            transform.SetAsLastSibling(); // over the banners, the combo tracker and the buttons
            gameObject.SetActive(true);   // OnEnable refreshes
            if (_scroll != null)
                _scroll.verticalNormalizedPosition = 1f; // start at the newest card
        }

        public void Close() => gameObject.SetActive(false);

        private void OnEnable()
        {
            IsAnyOpen = true;
            if (_controller != null)
                _controller.StateChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            IsAnyOpen = false;
            if (_controller != null)
                _controller.StateChanged -= Refresh;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                Close();
        }

        /// <summary>Shows the pile as it is now. DiscardPileView also calls it as each flying card lands.</summary>
        public void Refresh()
        {
            if (_content == null || !IsOpen)
                return;

            Match match = _controller != null ? _controller.CurrentMatch : null;
            IReadOnlyList<ArcanaId> pile = match != null ? match.Deck.DiscardPile : null;
            int total = pile != null ? pile.Count : 0;
            int held = _heldCount != null ? Mathf.Clamp(_heldCount(), 0, total) : 0;
            int count = total - held;

            _title.text = count == 1 ? "Discard Pile: 1 card" : $"Discard Pile: {count} cards";
            _emptyText.gameObject.SetActive(count == 0);

            while (_cards.Count < count)
                _cards.Add(CreateCard());

            for (int i = 0; i < _cards.Count; i++)
            {
                bool active = i < count;
                _cards[i].transform.parent.gameObject.SetActive(active);
                if (active)
                    _cards[i].Show(CardCatalog.Get(pile[count - 1 - i])); // the top of the pile comes first
            }
        }

        private void Build(RectTransform root)
        {
            // Darkens the table. Clicking it closes the viewer.
            RectTransform dim = CreateRect("Dim", root);
            Stretch(dim);
            dim.gameObject.AddComponent<Image>().color = DimColor;
            Button dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(Close);

            // A sibling of Dim, not its child, so clicks on the panel never close it.
            RectTransform panel = CreateRect("Panel", root);
            panel.anchorMin = new Vector2(0.08f, 0.08f);
            panel.anchorMax = new Vector2(0.92f, 0.92f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            GothicTheme.StylePanel(panel.gameObject.AddComponent<Image>(), true);

            _title = CreateText("Title", panel, 36f, GothicTheme.GoldBright, true);
            RectTransform titleRect = _title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -10f);
            titleRect.sizeDelta = new Vector2(-140f, 64f);

            RectTransform close = CreateRect("Close", panel);
            close.anchorMin = close.anchorMax = close.pivot = new Vector2(1f, 1f);
            close.anchoredPosition = new Vector2(-16f, -16f);
            close.sizeDelta = new Vector2(52f, 52f);
            close.gameObject.AddComponent<Image>().color = GothicTheme.ButtonColor;
            Button closeButton = close.gameObject.AddComponent<Button>();
            closeButton.onClick.AddListener(Close);
            Stretch(CreateText("Label", close, 28f, GothicTheme.GoldBright, true).rectTransform);
            close.GetComponentInChildren<TMP_Text>().text = "X";
            GothicTheme.StyleButton(closeButton);

            // Scroll area: Viewport (clips) > Content (the grid of cards).
            RectTransform viewport = CreateRect("Viewport", panel);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(28f, 28f);
            viewport.offsetMax = new Vector2(-28f, -84f);
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f); // catches drags and the wheel
            viewport.gameObject.AddComponent<RectMask2D>();

            _content = CreateRect("Content", viewport);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.sizeDelta = Vector2.zero;

            Rect prefabRect = _cardPrefab.Rect.rect;
            float aspect = prefabRect.height > 0f ? prefabRect.width / prefabRect.height : 0.7f;
            GridLayoutGroup grid = _content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(CellHeight * aspect, CellHeight);
            grid.spacing = new Vector2(18f, 18f);
            grid.padding = new RectOffset(8, 8, 8, 8);
            grid.childAlignment = TextAnchor.UpperCenter;
            ContentSizeFitter fitter = _content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scroll = panel.gameObject.AddComponent<ScrollRect>();
            _scroll.viewport = viewport;
            _scroll.content = _content;
            _scroll.horizontal = false;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 40f;

            _emptyText = CreateText("Empty", viewport, 26f,
                new Color(GothicTheme.TextColor.r, GothicTheme.TextColor.g, GothicTheme.TextColor.b, 0.6f), false);
            Stretch(_emptyText.rectTransform);
            _emptyText.fontStyle = FontStyles.Italic;
            _emptyText.text = "No cards in the discard pile.";
        }

        // Each card sits in a grid cell at its own authored size, scaled down to fit.
        // Scaling (instead of letting the grid resize it) keeps the card's inner layout intact.
        private CardView CreateCard()
        {
            RectTransform cell = CreateRect("Cell", _content);
            // The cards only show; they don't catch the pointer, so dragging over them scrolls (phones).
            CanvasGroup group = cell.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            CardView card = Instantiate(_cardPrefab, cell);
            RectTransform rect = card.Rect;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.localRotation = Quaternion.identity;
            float scale = CellHeight / Mathf.Max(1f, rect.rect.height);
            rect.localScale = new Vector3(scale, scale, 1f);
            card.SetSelected(false);
            card.SetDimmed(false);
            card.gameObject.SetActive(true);
            return card;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static TMP_Text CreateText(string name, Transform parent, float size, Color color, bool title)
        {
            TextMeshProUGUI text = CreateRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            if (title && GothicTheme.TitleFont != null)
                text.font = GothicTheme.TitleFont;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}