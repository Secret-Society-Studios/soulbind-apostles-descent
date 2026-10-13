using System;

namespace SoulBind.Core
{
    /// <summary>
    /// One Major Arcana card as written in the rulebook. Immutable rules data: no Unity types.
    /// Stats are final; do not change them without the team's approval.
    /// </summary>
    public sealed class CardDefinition
    {
        /// <summary>4 or more copies is Common, 2 to 3 is Epic, 1 is Legendary.</summary>
        public const int CommonMinCopies = 4;
        public const int LegendaryCopies = 1;

        public ArcanaId Id { get; }
        public string Name { get; }
        public Element Element { get; }
        public int Attack { get; }
        public int Defense { get; }

        /// <summary>Capacity it costs to play this card (CAP).</summary>
        public int Cap { get; }

        /// <summary>Copies of this card in the deck.</summary>
        public int Count { get; }

        /// <summary>The effect text from the rulebook.</summary>
        public string Effect { get; }

        public CardRarity Rarity { get; }

        public CardDefinition(ArcanaId id, string name, Element element,
            int attack, int defense, int cap, int count, string effect)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("A card needs a name.", nameof(name));
            if (attack < 0)
                throw new ArgumentOutOfRangeException(nameof(attack), "ATK cannot be negative.");
            if (defense < 0)
                throw new ArgumentOutOfRangeException(nameof(defense), "DEF cannot be negative.");
            if (cap < 0)
                throw new ArgumentOutOfRangeException(nameof(cap), "CAP cannot be negative.");

            Id = id;
            Name = name;
            Element = element;
            Attack = attack;
            Defense = defense;
            Cap = cap;
            Count = count;
            Effect = effect ?? string.Empty;
            Rarity = GetRarity(count); // also rejects a count below 1
        }

        public static CardRarity GetRarity(int count)
        {
            if (count < 1)
                throw new ArgumentOutOfRangeException(nameof(count), "A card has at least 1 copy.");
            if (count >= CommonMinCopies)
                return CardRarity.Common;
            if (count > LegendaryCopies)
                return CardRarity.Epic;
            return CardRarity.Legendary;
        }
    }
}