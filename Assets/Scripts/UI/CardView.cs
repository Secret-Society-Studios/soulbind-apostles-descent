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
    /// The art window and frame are placeholders until the flat card art is imported.
    /// </summary>
    public sealed class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
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

        public ArcanaId CardId { get; private set; }

        public RectTransform Rect => (RectTransform)transform;

        public event Action<CardView> HoverStarted;
        public event Action<CardView> HoverEnded;
        public event Action<CardView> Clicked;

        public void Show(CardDefinition card)
        {
            CardId = card.Id;
            Color elementColor = GetElementColor(card.Element);

            if (medallion != null)
                medallion.color = elementColor;
            if (medallionText != null)
                medallionText.text = GetElementInitial(card.Element);
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