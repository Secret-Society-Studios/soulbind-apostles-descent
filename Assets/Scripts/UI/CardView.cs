using System;
using SoulBind.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SoulBind.UI
{
    /// <summary>
    /// One card drawn from its rules data, following the team's card layout: element medallion
    /// top-left, art window, ATK/DEF/CAP badges down the right edge, name plate and effect text.
    /// Everything shown comes from CardCatalog, so a card can never show a stat the rules don't use.
    /// With a CardArtLibrary, the card's flat front (character art and border) fills the card and the
    /// numbers, element, name and effect are layered on top; cards without art keep the placeholder look.
    /// </summary>
    public sealed class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Image highlight;
        [SerializeField] private Image artWindow;
        [SerializeField] private Image medallion;
        [SerializeField] private TMP_Text medallionText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text effectText;
        [SerializeField] private TMP_Text attackText;
        [SerializeField] private TMP_Text defenseText;
        [SerializeField] private TMP_Text capText;
        [SerializeField] private Image dimOverlay;

        [Header("Art (from the team's designs)")]
        [Tooltip("Where the fronts, element medallions and stat icons come from. Empty: placeholders only.")]
        [SerializeField] private CardArtLibrary artLibrary;
        [Tooltip("Fills the whole card with the flat front.")]
        [SerializeField] private Image fullArt;
        [Tooltip("The placeholder frame. Made invisible over art, but it still catches hover and clicks.")]
        [SerializeField] private Image frame;
        [Tooltip("Placeholder parts hidden when the card has art (inner panel, art window).")]
        [SerializeField] private Graphic[] placeholderParts;
        [Tooltip("Plates behind the text (name plate, effect box, badges), hidden over art if the library says so.")]
        [SerializeField] private Graphic[] plates;
        [SerializeField] private Image attackIcon;
        [SerializeField] private Image defenseIcon;
        [SerializeField] private Image capIcon;
        [Tooltip("The small ATK / DEF / CAP words, hidden when the stat icons are used.")]
        [SerializeField] private Graphic[] statLabels;

        private Color _frameColor;
        private Color _nameColor;
        private Color _effectColor;
        private bool _colorsSaved;

        public ArcanaId CardId { get; private set; }

        public RectTransform Rect => (RectTransform)transform;

        public event Action<CardView> HoverStarted;
        public event Action<CardView> HoverEnded;
        public event Action<CardView> Clicked;
        public event Action<CardView, PointerEventData> DragStarted;
        public event Action<CardView, PointerEventData> Dragged;
        public event Action<CardView, PointerEventData> DragEnded;

        public void Show(CardDefinition card)
        {
            CardId = card.Id;
            Color elementColor = GetElementColor(card.Element);
            SaveColors();

            // The flat front, if the team's art for this card is in the library.
            Sprite front = artLibrary != null ? artLibrary.GetFront(card.Id) : null;
            bool hasArt = front != null;
            if (fullArt != null)
            {
                fullArt.enabled = hasArt;
                fullArt.sprite = front;
            }

            // A complete front already shows everything: draw nothing over it.
            bool complete = hasArt && artLibrary.FrontIsComplete;
            SetOverlaysVisible(!complete);
            if (complete)
            {
                if (frame != null)
                {
                    Color clear = _frameColor;
                    clear.a = 0f; // invisible, but still catches the pointer
                    frame.color = clear;
                }
                return;
            }
            if (frame != null)
            {
                Color color = _frameColor;
                if (hasArt)
                    color.a = 0f; // invisible, but still catches the pointer
                frame.color = color;
            }
            SetEnabled(placeholderParts, !hasArt);
            SetEnabled(plates, !(hasArt && artLibrary.HidePlatesOverArt));
            if (nameText != null)
            {
                nameText.enabled = !hasArt || artLibrary.ShowNameOverArt;
                nameText.color = hasArt ? artLibrary.TextColorOverArt : _nameColor;
            }
            if (effectText != null)
                effectText.color = hasArt ? artLibrary.TextColorOverArt : _effectColor;

            // The element: the team's medallion, or a colored placeholder with the element's initial.
            Sprite medallionArt = artLibrary != null ? artLibrary.GetElement(card.Element) : null;
            if (medallion != null)
            {
                medallion.sprite = medallionArt;
                medallion.color = medallionArt != null ? Color.white : elementColor;
                medallion.preserveAspect = medallionArt != null;
            }
            if (medallionText != null)
            {
                medallionText.enabled = medallionArt == null;
                medallionText.text = GetElementInitial(card.Element);
            }

            // Stat icons behind the numbers, when the team's icons are in the library.
            bool icons = ShowIcon(attackIcon, artLibrary != null ? artLibrary.AttackIcon : null)
                         | ShowIcon(defenseIcon, artLibrary != null ? artLibrary.DefenseIcon : null)
                         | ShowIcon(capIcon, artLibrary != null ? artLibrary.CapIcon : null);
            SetEnabled(statLabels, !icons);
            if (artWindow != null)
                artWindow.color = Color.Lerp(elementColor, Color.black, 0.45f);
            if (nameText != null)
                nameText.text = card.Name;
            if (effectText != null)
                effectText.text = card.Effect;
            if (attackText != null)
                attackText.text = card.Attack.ToString();
            if (defenseText != null)
                defenseText.text = card.Defense.ToString();
            if (capText != null)
                capText.text = card.Cap.ToString();
        }

        private void SaveColors()
        {
            if (_colorsSaved)
                return;
            _colorsSaved = true;
            _frameColor = frame != null ? frame.color : Color.white;
            _nameColor = nameText != null ? nameText.color : Color.black;
            _effectColor = effectText != null ? effectText.color : Color.black;
        }

        // Everything drawn over the art (or making up the placeholder card), on or off together.
        private void SetOverlaysVisible(bool visible)
        {
            SetEnabled(placeholderParts, visible);
            SetEnabled(plates, visible);
            SetEnabled(statLabels, visible);
            SetGraphic(artWindow, visible);
            SetGraphic(medallion, visible);
            SetGraphic(medallionText, visible);
            SetGraphic(nameText, visible);
            SetGraphic(effectText, visible);
            SetGraphic(attackText, visible);
            SetGraphic(defenseText, visible);
            SetGraphic(capText, visible);
            if (!visible)
            {
                SetGraphic(attackIcon, false);
                SetGraphic(defenseIcon, false);
                SetGraphic(capIcon, false);
            }
        }

        private static void SetGraphic(Graphic graphic, bool enabled)
        {
            if (graphic != null)
                graphic.enabled = enabled;
        }

        private static bool ShowIcon(Image icon, Sprite sprite)
        {
            if (icon == null)
                return false;
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            icon.preserveAspect = true;
            return sprite != null;
        }

        private static void SetEnabled(Graphic[] graphics, bool enabled)
        {
            if (graphics == null)
                return;
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] != null)
                    graphics[i].enabled = enabled;
            }
        }

        public void SetSelected(bool selected)
        {
            if (highlight != null)
                highlight.enabled = selected;
        }

        public void SetDimmed(bool dimmed)
        {
            if (dimOverlay != null)
                dimOverlay.enabled = dimmed;
        }

        public void OnPointerEnter(PointerEventData eventData) => HoverStarted?.Invoke(this);

        public void OnPointerExit(PointerEventData eventData) => HoverEnded?.Invoke(this);

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                Clicked?.Invoke(this);
        }

        // Dragging: uGUI does not send a click when the pointer was dragged, so the two never mix.
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                DragStarted?.Invoke(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                Dragged?.Invoke(this, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                DragEnded?.Invoke(this, eventData);
        }

        // Placeholder element colors until the medallion art is imported.
        public static Color GetElementColor(SoulBind.Core.Element element)
        {
            switch (element)
            {
                case SoulBind.Core.Element.Radiance: return new Color(0.95f, 0.78f, 0.25f);
                case SoulBind.Core.Element.Celeste: return new Color(0.40f, 0.75f, 0.95f);
                case SoulBind.Core.Element.Crimson: return new Color(0.85f, 0.22f, 0.25f);
                default: return new Color(0.55f, 0.30f, 0.75f); // Nocturne
            }
        }

        private static string GetElementInitial(SoulBind.Core.Element element)
        {
            switch (element)
            {
                case SoulBind.Core.Element.Radiance: return "R";
                case SoulBind.Core.Element.Celeste: return "Ce";
                case SoulBind.Core.Element.Crimson: return "Cr";
                default: return "N";
            }
        }
    }
}