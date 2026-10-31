using System;
using System.Collections.Generic;
using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests.EditMode
{
    /// <summary>Stage C5b: The Emperor, The Chariot and The Lovers.</summary>
    public class CardEffectBatchBTests
    {
        // Every Apostle gets the same 5-card hand. The draw pile after dealing is all Fools.
        private static Match MatchWithHand(params ArcanaId[] hand)
        {
            Assert.AreEqual(GameConstants.StartingHandSize, hand.Length, "Give exactly 5 cards");

            var stack = new List<ArcanaId>();
            foreach (ArcanaId card in hand)
            {
                stack.Add(card);
                stack.Add(card);
            }
            for (int i = 0; i < 12; i++)
                stack.Add(ArcanaId.Fool);

            HexBoard board = StandardBoard.Create();
            return Match.Create(2, board, SpawnPlanner.PickSpread(board, 2), new Random(1),
                Deck.CreateStacked(stack, new Random(2)));
        }

        // Moves the opponent next to the current Apostle and returns it.
        private static ApostleState OpponentNextToCurrent(Match match)
        {
            IReadOnlyList<ApostleState> apostles = match.Battlefield.Apostles;
            ApostleState opponent = apostles[0] == match.CurrentApostle ? apostles[1] : apostles[0];

            IReadOnlyList<HexCoord> neighbors = match.Battlefield.Board.GetNeighborsOnBoard(match.CurrentApostle.Position);
            for (int i = 0; i < neighbors.Count; i++)
            {
                if (!match.Battlefield.IsOccupied(neighbors[i]))
                {
                    opponent.MoveTo(neighbors[i]);
                    return opponent;
                }
            }
            throw new InvalidOperationException("No free tile next to the current Apostle.");
        }

        private static void Play(Match match, ArcanaId card, int targetId = 0)
        {
            Assert.AreEqual(CardPlayResult.Success, match.PlayCard(card, targetId), $"Playing {card}");
            // Cards played outside an attack wait in the queue: confirm them, like the PLAY button.
            if (match.Engagement == EngagementState.Free && match.AttackCards.Count > 0)
                match.PlayQueued();
        }

        // ---- The Chariot ----

        [TestCase(1, 3)]
        [TestCase(2, 2)]
        [TestCase(3, 2)]
        [TestCase(4, 1)]
        [TestCase(7, 1)]
        public void Chariot_BonusDependsOnPhase(int phase, int expected)
        {
            Assert.AreEqual(expected, ChariotEffect.GetBonus(phase));
        }

        [Test]
        public void Chariot_InPhaseOne_AddsThreeAtk()
        {
            Match match = MatchWithHand(ArcanaId.Chariot, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState target = OpponentNextToCurrent(match);

            Play(match, ArcanaId.Chariot, target.Id);

            Assert.AreEqual(8, match.PendingAttack); // 5 + 3
        }

        [Test]
        public void Chariot_InPhaseTwo_AddsTwoAtk()
        {
            Match match = MatchWithHand(ArcanaId.Chariot, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            for (int i = 0; i < 2 * GameConstants.RoundsPerPhase; i++)
                match.EndTurn();
            Assert.IsTrue(match.IsAwaitingPhaseStart);
            match.BeginNextPhase(TurnDirection.Forward);
            Assert.AreEqual(2, match.Turns.Phase);

            ApostleState target = OpponentNextToCurrent(match);
            Play(match, ArcanaId.Chariot, target.Id);

            Assert.AreEqual(7, match.PendingAttack); // 5 + 2
        }

        [Test]
        public void Chariot_WithoutAnAttack_GivesNoBonusLater()
        {
            Match match = MatchWithHand(ArcanaId.Chariot, ArcanaId.Magician, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState target = OpponentNextToCurrent(match);
            Play(match, ArcanaId.Chariot); // for DEF only

            Play(match, ArcanaId.Magician, target.Id);

            Assert.AreEqual(3, match.PendingAttack); // Magician 2 + 1, nothing from the Chariot
        }

        // ---- The Lovers ----

        [Test]
        public void Lovers_Single_HasNoBonus()
        {
            Match match = MatchWithHand(ArcanaId.Lovers, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState target = OpponentNextToCurrent(match);

            Play(match, ArcanaId.Lovers, target.Id);

            Assert.AreEqual(4, match.PendingAttack);
        }

        [Test]
        public void Lovers_PairInOneAttack_AddsTwoAtk()
        {
            Match match = MatchWithHand(ArcanaId.Lovers, ArcanaId.Lovers, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState target = OpponentNextToCurrent(match);

            Play(match, ArcanaId.Lovers, target.Id);
            Play(match, ArcanaId.Lovers);

            Assert.AreEqual(10, match.PendingAttack); // 4 + 4 + 2
        }

        [Test]
        public void Lovers_FirstPlayedWithoutAttack_DoesNotPair()
        {
            Match match = MatchWithHand(ArcanaId.Lovers, ArcanaId.Lovers, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState target = OpponentNextToCurrent(match);
            Play(match, ArcanaId.Lovers); // for DEF only

            Play(match, ArcanaId.Lovers, target.Id);

            Assert.AreEqual(4, match.PendingAttack);
        }

        // ---- The Emperor ----

        [Test]
        public void Emperor_TargetMoreCorrupted_DoublesTheWholeAttack()
        {
            Match match = MatchWithHand(ArcanaId.Magician, ArcanaId.Emperor, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState target = OpponentNextToCurrent(match);
            target.AddCorruption(3);

            Play(match, ArcanaId.Magician, target.Id); // 3
            Play(match, ArcanaId.Emperor);             // 3 + 6 = 9
            match.Strike(out AttackOutcome outcome);

            Assert.AreEqual(18, outcome.Damage);
            Assert.AreEqual(32, target.Vessel);
        }

        [Test]
        public void Emperor_TargetNotMoreCorrupted_NoDoubling()
        {
            Match match = MatchWithHand(ArcanaId.Emperor, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState target = OpponentNextToCurrent(match);

            Play(match, ArcanaId.Emperor, target.Id);
            match.Strike(out AttackOutcome outcome);

            Assert.AreEqual(6, outcome.Damage, "Equal Corruption is not higher");
        }

        [Test]
        public void Emperor_ComparesCorruptionWhenTheAttackResolves()
        {
            Match match = MatchWithHand(ArcanaId.Emperor, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState target = OpponentNextToCurrent(match);
            Play(match, ArcanaId.Emperor, target.Id);

            target.AddCorruption(2); // e.g. from another card played during the attack
            match.Strike(out AttackOutcome outcome);

            Assert.AreEqual(12, outcome.Damage);
        }

        [Test]
        public void Emperor_WithoutAnAttack_DoesNothingLater()
        {
            Match match = MatchWithHand(ArcanaId.Emperor, ArcanaId.Magician, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState target = OpponentNextToCurrent(match);
            target.AddCorruption(5);
            Play(match, ArcanaId.Emperor); // for DEF only

            Play(match, ArcanaId.Magician, target.Id);
            match.Strike(out AttackOutcome outcome);

            Assert.AreEqual(3, outcome.Damage);
        }
    }
}