using System;
using System.Collections.Generic;
using SoulBind.Core;
using UnityEngine;

namespace SoulBind.UI
{
    /// <summary>
    /// The team's card art, in one asset: one image per card. By default each image is the complete card
    /// (border, name, element, stats and effect), shown exactly as drawn. Cards without an image keep the
    /// placeholder look. The medallion and stat icon slots are only used when Front Is Complete is off.
    /// Create it with SoulBind > Add Card Art Support; fill it by dragging sprites in, or with
    /// SoulBind > Fill Card Art From Folder.
    /// </summary>
    [CreateAssetMenu(fileName = "CardArtLibrary", menuName = "SoulBind/Card Art Library")]
    public sealed class CardArtLibrary : ScriptableObject
    {
        [Serializable]
        public struct CardArt
        {
            public ArcanaId Card;
            [Tooltip("The flat front: character art and border in one image.")]
            public Sprite Front;
        }

        [Tooltip("One entry per card.")]
        [SerializeField] private List<CardArt> cards = new List<CardArt>();

        [Header("Element medallions")]
        [SerializeField] private Sprite radiance;
        [SerializeField] private Sprite celeste;
        [SerializeField] private Sprite crimson;
        [SerializeField] private Sprite nocturne;

        [Header("Stat icons (shown behind the numbers)")]
        [SerializeField] private Sprite attackIcon;
        [SerializeField] private Sprite defenseIcon;
        [SerializeField] private Sprite capIcon;

        [Header("Layers over the art")]
        [Tooltip("On: each front is the whole card (border, name, element, stats and effect all in the image), so nothing is drawn over it. Off: the numbers and text are drawn over the art.")]
        [SerializeField] private bool frontIsComplete = true;
        [Tooltip("Hide the placeholder plates (name plate, effect box, badges) when a card has art, so only the text sits on the art.")]
        [SerializeField] private bool hidePlatesOverArt = true;
        [Tooltip("Show the card's name as text over the art. Turn off if the name is already part of the art.")]
        [SerializeField] private bool showNameOverArt = true;
        [SerializeField] private Color textColorOverArt = new Color(0.12f, 0.10f, 0.08f);

        private Sprite[] _byCard;

        public IReadOnlyList<CardArt> Cards => cards;
        public Sprite AttackIcon => attackIcon;
        public Sprite DefenseIcon => defenseIcon;
        public Sprite CapIcon => capIcon;
        public bool FrontIsComplete => frontIsComplete;
        public bool HidePlatesOverArt => hidePlatesOverArt;
        public bool ShowNameOverArt => showNameOverArt;
        public Color TextColorOverArt => textColorOverArt;

        /// <summary>The card's flat front, or null if none was assigned.</summary>
        public Sprite GetFront(ArcanaId card)
        {
            if (_byCard == null)
                BuildLookup();
            int index = (int)card;
            return index >= 0 && index < _byCard.Length ? _byCard[index] : null;
        }

        /// <summary>The element's medallion, or null if none was assigned.</summary>
        public Sprite GetElement(SoulBind.Core.Element element)
        {
            switch (element)
            {
                case SoulBind.Core.Element.Radiance: return radiance;
                case SoulBind.Core.Element.Celeste: return celeste;
                case SoulBind.Core.Element.Crimson: return crimson;
                default: return nocturne;
            }
        }

        /// <summary>Editor helper: puts one entry per card in the list, keeping any art already assigned.</summary>
        public void EnsureEntryPerCard()
        {
            var existing = new Dictionary<ArcanaId, Sprite>();
            foreach (CardArt entry in cards)
            {
                if (!existing.ContainsKey(entry.Card) || existing[entry.Card] == null)
                    existing[entry.Card] = entry.Front;
            }

            cards.Clear();
            foreach (CardDefinition card in CardCatalog.All)
            {
                existing.TryGetValue(card.Id, out Sprite front);
                cards.Add(new CardArt { Card = card.Id, Front = front });
            }
            _byCard = null;
        }

        /// <summary>Editor helper: assigns a card's front.</summary>
        public void SetFront(ArcanaId card, Sprite front)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i].Card == card)
                {
                    cards[i] = new CardArt { Card = card, Front = front };
                    _byCard = null;
                    return;
                }
            }
            cards.Add(new CardArt { Card = card, Front = front });
            _byCard = null;
        }

        private void OnValidate() => _byCard = null; // rebuilt after edits in the Inspector

        private void BuildLookup()
        {
            int size = 0;
            foreach (CardArt entry in cards)
                size = Mathf.Max(size, (int)entry.Card + 1);
            _byCard = new Sprite[Mathf.Max(size, 32)];
            foreach (CardArt entry in cards)
            {
                if (entry.Front != null)
                    _byCard[(int)entry.Card] = entry.Front;
            }
        }
    }
}