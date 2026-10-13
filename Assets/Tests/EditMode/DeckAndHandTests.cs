using System;
using System.Collections.Generic;
using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests.EditMode
{
    public class DeckAndHandTests
    {
        private static List<ArcanaId> DrawAll(Deck deck)
        {
            var drawn = new List<ArcanaId>();
            while (deck.DrawCount > 0 && deck.TryDraw(out ArcanaId card))
                drawn.Add(card);
            return drawn;
        }

        // ---- Deck ----

        [Test]
        public void CreateFull_HasSixtyThreeCardsAndEmptyDiscard()
        {
            Deck deck = Deck.CreateFull(new Random(1));

            Assert.AreEqual(63, deck.DrawCount);
            Assert.AreEqual(0, deck.DiscardCount);
        }

        [Test]
        public void CreateFull_HasEachCardsRulebookCount()
        {
            var counts = new int[CardCatalog.All.Count];
            foreach (ArcanaId card in DrawAll(Deck.CreateFull(new Random(1))))
                counts[(int)card]++;

            foreach (CardDefinition definition in CardCatalog.All)
                Assert.AreEqual(definition.Count, counts[(int)definition.Id], definition.Name);
        }

        [Test]
        public void CreateFull_SameSeed_GivesSameOrder()
        {
            CollectionAssert.AreEqual(DrawAll(Deck.CreateFull(new Random(4))),
                                      DrawAll(Deck.CreateFull(new Random(4))));
        }

        [Test]
        public void CreateFull_DifferentSeeds_GiveDifferentOrders()
        {
            CollectionAssert.AreNotEqual(DrawAll(Deck.CreateFull(new Random(1))),
                                         DrawAll(Deck.CreateFull(new Random(2))));
        }

        [Test]
        public void TryDraw_TakesOneCardFromTheDrawPile()
        {
            Deck deck = Deck.CreateFull(new Random(1));

            Assert.IsTrue(deck.TryDraw(out _));
            Assert.AreEqual(62, deck.DrawCount);
        }

        [Test]
        public void TryDraw_EmptyDrawPile_ReshufflesTheDiscardPile()
        {
            var deck = new Deck(new[] { ArcanaId.Fool }, new Random(1));
            deck.TryDraw(out ArcanaId first);
            deck.Discard(first);

            Assert.IsTrue(deck.TryDraw(out ArcanaId again));
            Assert.AreEqual(ArcanaId.Fool, again);
            Assert.AreEqual(0, deck.DiscardCount);
        }

        [Test]
        public void TryDraw_BothPilesEmpty_ReturnsFalse()
        {
            var deck = new Deck(new[] { ArcanaId.Fool }, new Random(1));
            deck.TryDraw(out _);

            Assert.IsFalse(deck.TryDraw(out _));
        }

        [Test]
        public void Discard_PutsCardOnTopOfTheDiscardPile()
        {
            var deck = new Deck(new ArcanaId[0], new Random(1));
            deck.Discard(ArcanaId.Sun);
            deck.Discard(ArcanaId.Moon);

            Assert.AreEqual(2, deck.DiscardCount);
            Assert.AreEqual(ArcanaId.Moon, deck.DiscardPile[deck.DiscardCount - 1]);
        }

        [Test]
        public void Deck_InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new Deck(null, new Random(1)));
            Assert.Throws<ArgumentNullException>(() => new Deck(new[] { ArcanaId.Fool }, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Deck(new[] { (ArcanaId)22 }, new Random(1)));
            Assert.Throws<ArgumentNullException>(() => Deck.CreateFull(null));
        }

        // ---- Hand ----

        [Test]
        public void Hand_AddContainsRemove()
        {
            var hand = new Hand();
            hand.Add(ArcanaId.Star);
            hand.Add(ArcanaId.Star);

            Assert.AreEqual(2, hand.Count);
            Assert.IsTrue(hand.Contains(ArcanaId.Star));
            Assert.IsTrue(hand.Remove(ArcanaId.Star));
            Assert.AreEqual(1, hand.Count, "Remove takes only one copy");
        }

        [Test]
        public void Hand_RemoveMissingCard_ReturnsFalse()
        {
            var hand = new Hand();
            hand.Add(ArcanaId.Fool);

            Assert.IsFalse(hand.Remove(ArcanaId.World));
            Assert.AreEqual(1, hand.Count);
        }

        [Test]
        public void Hand_IsOverLimit_OnlyAboveSeven()
        {
            var hand = new Hand();
            for (int i = 0; i < GameConstants.MaxHandSize; i++)
                hand.Add(ArcanaId.Fool);
            Assert.IsFalse(hand.IsOverLimit, "7 cards is allowed");

            hand.Add(ArcanaId.Fool);
            Assert.IsTrue(hand.IsOverLimit, "8 cards is over");
        }

        [Test]
        public void Hand_DiscardAllTo_MovesEveryCardToTheDiscardPile()
        {
            var deck = new Deck(new ArcanaId[0], new Random(1));
            var hand = new Hand();
            hand.Add(ArcanaId.Fool);
            hand.Add(ArcanaId.Death);

            hand.DiscardAllTo(deck);

            Assert.AreEqual(0, hand.Count);
            Assert.AreEqual(2, deck.DiscardCount);
        }

        // ---- Integration with Match ----

        private static Match NewMatch(int players, int seed = 1)
        {
            HexBoard board = StandardBoard.Create();
            return Match.Create(players, board, SpawnPlanner.PickSpread(board, players), new Random(seed));
        }

        [Test]
        public void Match_Create_DealsFiveCardsToEachApostle()
        {
            Match match = NewMatch(4);

            foreach (ApostleState apostle in match.Battlefield.Apostles)
                Assert.AreEqual(GameConstants.StartingHandSize, match.GetHand(apostle.Id).Count);
            Assert.AreEqual(63 - 4 * GameConstants.StartingHandSize, match.Deck.DrawCount);
        }

        [Test]
        public void Match_Create_SameSeed_GivesSameHands()
        {
            Match a = NewMatch(3, 9);
            Match b = NewMatch(3, 9);

            foreach (ApostleState apostle in a.Battlefield.Apostles)
                CollectionAssert.AreEqual(a.GetHand(apostle.Id), b.GetHand(apostle.Id));
        }

        [Test]
        public void Match_EndTurn_DrawsOneCard()
        {
            Match match = NewMatch(4);
            ApostleState owner = match.CurrentApostle;
            int deckBefore = match.Deck.DrawCount;

            match.EndTurn();

            Assert.AreEqual(GameConstants.StartingHandSize + 1, match.GetHand(owner.Id).Count);
            Assert.AreEqual(deckBefore - 1, match.Deck.DrawCount);
        }

        [Test]
        public void Match_EndTurn_AboveSevenCards_AddsCorruption()
        {
            Match match = NewMatch(4);
            ApostleState owner = match.CurrentApostle;

            // With 4 Apostles, the owner ends turns 1, 5 and 9: hand 6, then 7, then 8.
            for (int i = 0; i < 5; i++)
                match.EndTurn();
            Assert.AreEqual(7, match.GetHand(owner.Id).Count);
            Assert.AreEqual(0, owner.Corruption, "Ending at 7 is allowed");

            for (int i = 0; i < 4; i++)
                match.EndTurn();
            Assert.AreEqual(8, match.GetHand(owner.Id).Count);
            Assert.AreEqual(GameConstants.HandOverLimitCorruption, owner.Corruption);
        }

        [Test]
        public void Match_EliminatedApostle_HandGoesToDiscardPile()
        {
            Match match = NewMatch(4);
            ApostleState victim = match.Battlefield.Apostles[0] == match.CurrentApostle
                ? match.Battlefield.Apostles[1]
                : match.Battlefield.Apostles[0];

            victim.TakeDamage(GameConstants.StartingVessel);
            match.RefreshGameState();

            Assert.AreEqual(0, match.GetHand(victim.Id).Count);
            Assert.AreEqual(GameConstants.StartingHandSize, match.Deck.DiscardCount);
        }

        [Test]
        public void Match_GetHand_UnknownId_Throws()
        {
            Match match = NewMatch(4);
            Assert.Throws<ArgumentException>(() => match.GetHand(99));
        }
    }
}