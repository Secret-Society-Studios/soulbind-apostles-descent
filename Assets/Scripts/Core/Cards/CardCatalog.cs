using System;
using System.Collections.Generic;

namespace SoulBind.Core
{
    /// <summary>
    /// Every card in the game, indexed by ArcanaId. Values come from the rulebook card list (Section 10).
    /// Built once and never changed, so lookups are free and allocation-free.
    /// </summary>
    public static class CardCatalog
    {
        private static readonly CardDefinition[] Cards =
        {
            new CardDefinition(ArcanaId.Fool, "The Fool", Element.Celeste, 0, 0, 0, 6,
                "Reduce Corruption by 1, but reduce DEF by 2 (DEF cannot go below 0)."),
            new CardDefinition(ArcanaId.Magician, "The Magician", Element.Radiance, 2, 1, 1, 6,
                "Current attack has +1 ATK."),
            new CardDefinition(ArcanaId.HighPriestess, "The High Priestess", Element.Nocturne, 2, 2, 2, 4,
                "Draw an additional card."),
            new CardDefinition(ArcanaId.Empress, "The Empress", Element.Radiance, 0, 3, 3, 3,
                "Reduce Corruption by 2 and heal 1 Vessel."),
            new CardDefinition(ArcanaId.Emperor, "The Emperor", Element.Crimson, 6, 3, 7, 1,
                "The higher the opponent's Corruption than yours, multiply your ATK by 2."),
            new CardDefinition(ArcanaId.Hierophant, "The Hierophant", Element.Celeste, 0, 0, 2, 3,
                "Increase ATK and DEF by 2 for the next round."),
            new CardDefinition(ArcanaId.Lovers, "The Lovers", Element.Radiance, 4, 4, 4, 3,
                "Combo with another Lovers card to increase ATK by 2."),
            new CardDefinition(ArcanaId.Chariot, "The Chariot", Element.Crimson, 5, 2, 3, 3,
                "Phase 1: +3 ATK. Phases 2-3: +2 ATK. Phase 4+: +1 ATK."),
            new CardDefinition(ArcanaId.Strength, "Strength", Element.Crimson, 0, 3, 3, 4,
                "Reduce Corruption by 2; doubled if used with another Corruption-reducing card."),
            new CardDefinition(ArcanaId.Hermit, "The Hermit", Element.Nocturne, 2, 2, 2, 3,
                "Increase maximum Capacity by 2 for the next 2 rounds."),
            new CardDefinition(ArcanaId.WheelOfFortune, "Wheel of Fortune", Element.Celeste, 3, 2, 3, 3,
                "Swap a random card with an opponent."),
            new CardDefinition(ArcanaId.Justice, "Justice", Element.Celeste, 0, 2, 4, 3,
                "Increase all opponents' Corruption by 2."),
            new CardDefinition(ArcanaId.HangedMan, "The Hanged Man", Element.Nocturne, 0, 5, 3, 4,
                "If you have the lowest Vessel, +5 ATK for 2 rounds."),
            new CardDefinition(ArcanaId.Death, "Death", Element.Nocturne, 7, 4, 5, 2,
                "Execute an opponent with less than 5 Vessel."),
            new CardDefinition(ArcanaId.Temperance, "Temperance", Element.Celeste, 0, 5, 4, 2,
                "Skip your current round; +5 ATK and DEF in the next round."),
            new CardDefinition(ArcanaId.Devil, "The Devil", Element.Nocturne, 8, 3, 6, 2,
                "Opponent gains +3 Corruption; you gain +1."),
            new CardDefinition(ArcanaId.Tower, "The Tower", Element.Crimson, 3, 3, 6, 3,
                "Decrease each opponent's Capacity by 2 for 1 round."),
            new CardDefinition(ArcanaId.Star, "The Star", Element.Radiance, 0, 6, 4, 2,
                "Heal 2 Vessel and increase DEF by 4 for 2 rounds."),
            new CardDefinition(ArcanaId.Moon, "The Moon", Element.Nocturne, 4, 10, 6, 1,
                "If an opponent fails to break your DEF, they gain +2 Corruption."),
            new CardDefinition(ArcanaId.Sun, "The Sun", Element.Radiance, 10, 4, 6, 1,
                "Reduce Corruption by 1 and heal 2 Vessel."),
            new CardDefinition(ArcanaId.Judgement, "Judgement", Element.Radiance, 2, 4, 3, 3,
                "Increase DEF by 2 for this round and heal 1 Vessel."),
            new CardDefinition(ArcanaId.World, "The World", Element.Celeste, 6, 7, 8, 1,
                "Stop all opponents' turns; go to the next round.")
        };

        private static readonly IReadOnlyList<CardDefinition> ReadOnlyCards = Array.AsReadOnly(Cards);

        /// <summary>All 22 cards, in ArcanaId order.</summary>
        public static IReadOnlyList<CardDefinition> All => ReadOnlyCards;

        /// <summary>Total cards in a full deck (the sum of every card's Count).</summary>
        public static int DeckSize { get; } = SumCounts();

        public static CardDefinition Get(ArcanaId id)
        {
            int index = (int)id;
            if (index < 0 || index >= Cards.Length)
                throw new ArgumentOutOfRangeException(nameof(id), $"Unknown card {id}.");
            return Cards[index];
        }

        private static int SumCounts()
        {
            int total = 0;
            for (int i = 0; i < Cards.Length; i++)
                total += Cards[i].Count;
            return total;
        }
    }
}