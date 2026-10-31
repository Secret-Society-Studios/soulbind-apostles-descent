using System;
using System.Collections.Generic;
using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests.EditMode
{
    /// <summary>Stage C5a: Fool, High Priestess, Empress, Strength, Justice, Devil, Sun and Judgement.</summary>
    public class CardEffectBatchATests
    {
        // Every Apostle gets the same 5-card hand, so the tests do not depend on who plays first.
        // The draw pile after dealing is all Magicians.
        private static Match MatchWithHand(int players, params ArcanaId[] hand)
        {
            Assert.AreEqual(GameConstants.StartingHandSize, hand.Length, "Give exactly 5 cards");

            var stack = new List<ArcanaId>();
            foreach (ArcanaId card in hand)
            {
                for (int p = 0; p < players; p++)
                    stack.Add(card);
            }
            for (int i = 0; i < 10; i++)
                stack.Add(ArcanaId.Magician);

            HexBoard board = StandardBoard.Create();
            return Match.Create(players, board, SpawnPlanner.PickSpread(board, players), new Random(1),
                Deck.CreateStacked(stack, new Random(2)));
        }

        private static ApostleState Opponent(Match match)
        {
            IReadOnlyList<ApostleState> apostles = match.Battlefield.Apostles;
            return apostles[0] == match.CurrentApostle ? apostles[1] : apostles[0];
        }

        private static void PlaceNextTo(Match match, ApostleState mover, ApostleState anchor)
        {
            IReadOnlyList<HexCoord> neighbors = match.Battlefield.Board.GetNeighborsOnBoard(anchor.Position);
            for (int i = 0; i < neighbors.Count; i++)
            {
                if (!match.Battlefield.IsOccupied(neighbors[i]))
                {
                    mover.MoveTo(neighbors[i]);
                    return;
                }
            }
            throw new InvalidOperationException($"No free tile next to {anchor.Position}.");
        }

        private static void Play(Match match, ArcanaId card, int targetId = 0)
        {
            Assert.AreEqual(CardPlayResult.Success, match.PlayCard(card, targetId), $"Playing {card}");
        }

        // ---- The Fool ----

        [Test]
        public void Fool_ReducesOneCorruptionAndTwoShield_TimedDefUntouched()
        {
            Match match = MatchWithHand(2, ArcanaId.Star, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState player = match.CurrentApostle;
            player.AddCorruption(3);
            Play(match, ArcanaId.Star); // shield 6, timed DEF 4

            Play(match, ArcanaId.Fool);

            Assert.AreEqual(2, player.Corruption);
            Assert.AreEqual(4, player.Shield);
            Assert.AreEqual(8, player.Defense);
        }

        [Test]
        public void Fool_ShieldAndCorruptionNeverGoBelowZero()
        {
            Match match = MatchWithHand(2, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);

            Play(match, ArcanaId.Fool);

            Assert.AreEqual(0, match.CurrentApostle.Shield);
            Assert.AreEqual(0, match.CurrentApostle.Corruption);
        }

        // ---- The High Priestess ----

        [Test]
        public void HighPriestess_DrawsACardImmediately()
        {
            Match match = MatchWithHand(2, ArcanaId.HighPriestess, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            int drawPileBefore = match.Deck.DrawCount;

            Play(match, ArcanaId.HighPriestess);

            IReadOnlyList<ArcanaId> hand = match.GetHand(match.CurrentApostle.Id);
            Assert.AreEqual(5, hand.Count, "5 - 1 played + 1 drawn");
            CollectionAssert.Contains(hand, ArcanaId.Magician);
            Assert.AreEqual(drawPileBefore - 1, match.Deck.DrawCount);
        }

        // ---- The Empress and The Sun ----

        [Test]
        public void Empress_ReducesTwoCorruptionAndHealsOne()
        {
            Match match = MatchWithHand(2, ArcanaId.Empress, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState player = match.CurrentApostle;
            player.AddCorruption(3);
            player.TakeDamage(10);

            Play(match, ArcanaId.Empress);

            Assert.AreEqual(1, player.Corruption);
            Assert.AreEqual(41, player.Vessel);
        }

        [Test]
        public void Sun_ReducesOneCorruptionAndHealsTwo()
        {
            Match match = MatchWithHand(2, ArcanaId.Sun, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState player = match.CurrentApostle;
            player.AddCorruption(3);
            player.TakeDamage(10);

            Play(match, ArcanaId.Sun);

            Assert.AreEqual(2, player.Corruption);
            Assert.AreEqual(42, player.Vessel);
        }

        // ---- Judgement ----

        [Test]
        public void Judgement_AddsTwoShieldOnTopOfItsDefAndHealsOne()
        {
            Match match = MatchWithHand(2, ArcanaId.Judgement, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState player = match.CurrentApostle;
            player.TakeDamage(10);

            Play(match, ArcanaId.Judgement);

            Assert.AreEqual(6, player.Shield); // 4 DEF + 2
            Assert.AreEqual(41, player.Vessel);
        }

        // ---- Justice ----

        [Test]
        public void Justice_GivesEveryLivingOpponentTwoCorruption()
        {
            Match match = MatchWithHand(3, ArcanaId.Justice, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState player = match.CurrentApostle;
            ApostleState living = null;
            ApostleState eliminated = null;
            foreach (ApostleState apostle in match.Battlefield.Apostles)
            {
                if (apostle == player)
                    continue;
                if (living == null)
                    living = apostle;
                else
                    eliminated = apostle;
            }
            eliminated.TakeDamage(GameConstants.StartingVessel);

            Play(match, ArcanaId.Justice);

            Assert.AreEqual(JusticeEffect.CorruptionAdded, living.Corruption, "Not adjacent, still affected");
            Assert.AreEqual(0, eliminated.Corruption);
            Assert.AreEqual(0, player.Corruption);
        }

        // ---- The Devil ----

        [Test]
        public void Devil_WhenAttacking_TargetGainsThreeAndPlayerGainsOne()
        {
            Match match = MatchWithHand(2, ArcanaId.Devil, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState player = match.CurrentApostle;
            ApostleState target = Opponent(match);
            PlaceNextTo(match, target, player);

            Play(match, ArcanaId.Devil, target.Id);
            Assert.AreEqual(0, target.Corruption, "Waits in the queue until Strike");

            match.Strike(out _);

            Assert.AreEqual(3, target.Corruption);
            Assert.AreEqual(1 + GameConstants.AttackCorruption, player.Corruption, "The Devil's 1, plus 1 for breaking DEF");
        }

        [Test]
        public void Devil_WithoutAnAttack_DoesNothing()
        {
            Match match = MatchWithHand(2, ArcanaId.Devil, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState player = match.CurrentApostle;

            Play(match, ArcanaId.Devil);

            Assert.AreEqual(0, player.Corruption);
            Assert.AreEqual(0, Opponent(match).Corruption);
        }

        // ---- Strength ----

        [Test]
        public void Strength_Alone_ReducesTwo()
        {
            Match match = MatchWithHand(2, ArcanaId.Strength, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState player = match.CurrentApostle;
            player.AddCorruption(6);

            Play(match, ArcanaId.Strength);

            Assert.AreEqual(4, player.Corruption);
        }

        [Test]
        public void Strength_AfterAnotherReducer_ReducesFour()
        {
            Match match = MatchWithHand(2, ArcanaId.Empress, ArcanaId.Strength, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState player = match.CurrentApostle;
            player.AddCorruption(8);

            Play(match, ArcanaId.Empress);  // 8 - 2 = 6
            Play(match, ArcanaId.Strength); // 6 - 4 = 2

            Assert.AreEqual(2, player.Corruption);
        }

        [Test]
        public void Strength_BeforeAnotherReducer_GetsItsBonusWhenPaired()
        {
            Match match = MatchWithHand(2, ArcanaId.Strength, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState player = match.CurrentApostle;
            player.AddCorruption(8);

            Play(match, ArcanaId.Strength); // 8 - 2 = 6
            Assert.AreEqual(6, player.Corruption);

            Play(match, ArcanaId.Fool);     // Strength's other 2, then the Fool's 1: 6 - 3 = 3
            Assert.AreEqual(3, player.Corruption);
        }

        [Test]
        public void TwoStrengths_AreBothDoubled()
        {
            Match match = MatchWithHand(2, ArcanaId.Strength, ArcanaId.Strength, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState player = match.CurrentApostle;
            player.AddCorruption(10);

            Play(match, ArcanaId.Strength);
            Play(match, ArcanaId.Strength);

            Assert.AreEqual(2, player.Corruption); // 4 + 4
        }

        [Test]
        public void Strength_DoesNotPairAcrossTurns()
        {
            Match match = MatchWithHand(2, ArcanaId.Strength, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState player = match.CurrentApostle;
            player.AddCorruption(6);
            Play(match, ArcanaId.Strength); // 6 - 2 = 4

            match.EndTurn();
            match.EndTurn();
            Assert.AreSame(player, match.CurrentApostle);
            Play(match, ArcanaId.Fool);     // only the Fool's 1

            Assert.AreEqual(3, player.Corruption);
        }
    }
}