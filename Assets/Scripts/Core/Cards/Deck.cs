using System;
using System.Collections.Generic;

namespace SoulBind.Core
{
    /// <summary>
    /// The shared draw pile and discard pile. Shuffling uses the match's seeded random source,
    /// so the same seed always gives the same card order. Pure rules data: no Unity types.
    /// </summary>
    public sealed class Deck
    {
        private readonly List<ArcanaId> _drawPile;
        private readonly List<ArcanaId> _discardPile;
        private readonly Random _random;

        /// <summary>Creates a deck from these cards and shuffles it.</summary>
        public Deck(IReadOnlyList<ArcanaId> cards, Random random) : this(cards, random, true)
        {
        }

        private Deck(IReadOnlyList<ArcanaId> cards, Random random, bool shuffle)
        {
            if (cards == null)
                throw new ArgumentNullException(nameof(cards));
            _random = random ?? throw new ArgumentNullException(nameof(random));

            _drawPile = new List<ArcanaId>(cards.Count);
            _discardPile = new List<ArcanaId>(cards.Count);
            for (int i = 0; i < cards.Count; i++)
            {
                RequireKnown(cards[i]);
                _drawPile.Add(cards[i]);
            }

            if (shuffle)
                Shuffle(_drawPile);
        }

        /// <summary>A shuffled deck with every card's rulebook count (63 cards).</summary>
        public static Deck CreateFull(Random random)
        {
            var cards = new List<ArcanaId>(CardCatalog.DeckSize);
            IReadOnlyList<CardDefinition> all = CardCatalog.All;
            for (int i = 0; i < all.Count; i++)
            {
                for (int copy = 0; copy < all[i].Count; copy++)
                    cards.Add(all[i].Id);
            }
            return new Deck(cards, random);
        }

        /// <summary>
        /// An unshuffled deck: topFirst[0] is drawn first. For tests and scripted scenarios
        /// (such as a tutorial). The random source is still used when the discard pile is reshuffled.
        /// </summary>
        public static Deck CreateStacked(IReadOnlyList<ArcanaId> topFirst, Random random)
        {
            if (topFirst == null)
                throw new ArgumentNullException(nameof(topFirst));

            // Cards are drawn from the end of the list, so store them in reverse.
            var reversed = new List<ArcanaId>(topFirst.Count);
            for (int i = topFirst.Count - 1; i >= 0; i--)
                reversed.Add(topFirst[i]);
            return new Deck(reversed, random, false);
        }

        public int DrawCount => _drawPile.Count;
        public int DiscardCount => _discardPile.Count;

        /// <summary>The discard pile is public information. The last card is the top.</summary>
        public IReadOnlyList<ArcanaId> DiscardPile => _discardPile;

        /// <summary>
        /// Draws the top card. If the draw pile is empty, the discard pile is shuffled back in first.
        /// Returns false only when both piles are empty.
        /// </summary>
        public bool TryDraw(out ArcanaId card)
        {
            if (_drawPile.Count == 0)
                ReshuffleDiscardPile();

            if (_drawPile.Count == 0)
            {
                card = default;
                return false;
            }

            int top = _drawPile.Count - 1;
            card = _drawPile[top];
            _drawPile.RemoveAt(top);
            return true;
        }

        public void Discard(ArcanaId card)
        {
            RequireKnown(card);
            _discardPile.Add(card);
        }

        /// <summary>The draw pile, bottom first. Only for snapshots: it is hidden from players.</summary>
        internal IReadOnlyList<ArcanaId> DrawPileForSnapshot => _drawPile;

        /// <summary>For snapshots (online play): replaces both piles.</summary>
        internal void Restore(IReadOnlyList<ArcanaId> drawPile, IReadOnlyList<ArcanaId> discardPile)
        {
            _drawPile.Clear();
            for (int i = 0; i < drawPile.Count; i++)
                _drawPile.Add(drawPile[i]);
            _discardPile.Clear();
            for (int i = 0; i < discardPile.Count; i++)
                _discardPile.Add(discardPile[i]);
        }

        private void ReshuffleDiscardPile()
        {
            _drawPile.AddRange(_discardPile);
            _discardPile.Clear();
            Shuffle(_drawPile);
        }

        // Fisher-Yates shuffle: every order is equally likely.
        private void Shuffle(List<ArcanaId> cards)
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                ArcanaId temp = cards[i];
                cards[i] = cards[j];
                cards[j] = temp;
            }
        }

        private static void RequireKnown(ArcanaId card)
        {
            if ((int)card >= CardCatalog.All.Count)
                throw new ArgumentOutOfRangeException(nameof(card), $"Unknown card {card}.");
        }
    }
}