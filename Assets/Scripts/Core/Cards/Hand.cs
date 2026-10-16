using System;
using System.Collections.Generic;

namespace SoulBind.Core
{
    /// <summary>
    /// The cards one Apostle holds. Hidden information: only its owner may see it.
    /// Pure rules data: no Unity types.
    /// </summary>
    public sealed class Hand
    {
        private readonly List<ArcanaId> _cards = new List<ArcanaId>(GameConstants.MaxHandSize + 2);

        public IReadOnlyList<ArcanaId> Cards => _cards;
        public int Count => _cards.Count;

        /// <summary>True when the hand holds more than the maximum (7).</summary>
        public bool IsOverLimit => _cards.Count > GameConstants.MaxHandSize;

        public void Add(ArcanaId card) => _cards.Add(card);

        public bool Contains(ArcanaId card) => _cards.Contains(card);

        /// <summary>Removes one copy of the card. Returns false if the hand does not hold it.</summary>
        public bool Remove(ArcanaId card) => _cards.Remove(card);

        /// <summary>Replaces the card at this position (for Wheel of Fortune) and returns the old one.</summary>
        public ArcanaId ReplaceAt(int index, ArcanaId card)
        {
            if (index < 0 || index >= _cards.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            ArcanaId old = _cards[index];
            _cards[index] = card;
            return old;
        }

        /// <summary>Moves every card in the hand to the deck's discard pile.</summary>
        public void DiscardAllTo(Deck deck)
        {
            for (int i = 0; i < _cards.Count; i++)
                deck.Discard(_cards[i]);
            _cards.Clear();
        }

        /// <summary>For snapshots (online play): replaces the whole hand.</summary>
        internal void Restore(IReadOnlyList<ArcanaId> cards)
        {
            _cards.Clear();
            for (int i = 0; i < cards.Count; i++)
                _cards.Add(cards[i]);
        }
    }
}