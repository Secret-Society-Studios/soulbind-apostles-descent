using System;
using System.Collections.Generic;
using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests.EditMode
{
    public class CardPlayTests
    {
        // Both Apostles get this hand, so the tests do not depend on who plays first.
        // Magician A2 D1 C1 (+1 ATK when attacking), Death A7 D4 C5, Star A0 D6 C4, Fool A0 D0 C0, Moon A4 D10 C6.
        private static readonly ArcanaId[] StartingHand =
        {
            ArcanaId.Magician, ArcanaId.Death, ArcanaId.Star, ArcanaId.Fool, ArcanaId.Moon
        };

        private static Match NewMatch()
        {
            // Dealing goes round the Apostles one card at a time, so each card appears twice in a row.
            var stack = new List<ArcanaId>();
            foreach (ArcanaId card in StartingHand)
            {
                stack.Add(card);
                stack.Add(card);
            }
            for (int i = 0; i < 6; i++)
                stack.Add(ArcanaId.Fool); // cards to draw later

            HexBoard board = StandardBoard.Create();
            return Match.Create(2, board, SpawnPlanner.PickSpread(board, 2), new Random(1),
                Deck.CreateStacked(stack, new Random(2)));
        }

        private static ApostleState Opponent(Match match)
        {
            IReadOnlyList<ApostleState> apostles = match.Battlefield.Apostles;
            return apostles[0] == match.CurrentApostle ? apostles[1] : apostles[0];
        }

        private static HexCoord FreeNeighbor(Match match, HexCoord tile)
        {
            IReadOnlyList<HexCoord> neighbors = match.Battlefield.Board.GetNeighborsOnBoard(tile);
            for (int i = 0; i < neighbors.Count; i++)
            {
                if (!match.Battlefield.IsOccupied(neighbors[i]))
                    return neighbors[i];
            }
            throw new InvalidOperationException($"No free tile next to {tile}.");
        }

        // A match where the current Apostle has engaged its opponent with The Magician (ATK 2 + 1 = 3).
        private static Match EngagedMatch(out ApostleState target)
        {
            Match match = NewMatch();
            target = Opponent(match);
            target.MoveTo(FreeNeighbor(match, match.CurrentApostle.Position));
            Assert.AreEqual(CardPlayResult.Success, match.PlayCard(ArcanaId.Magician, target.Id));
            return match;
        }

        // ---- Setup ----

        [Test]
        public void Deck_CreateStacked_DrawsInGivenOrder()
        {
            Deck deck = Deck.CreateStacked(new[] { ArcanaId.Sun, ArcanaId.Moon, ArcanaId.Star }, new Random(1));

            deck.TryDraw(out ArcanaId first);
            deck.TryDraw(out ArcanaId second);
            deck.TryDraw(out ArcanaId third);

            Assert.AreEqual(ArcanaId.Sun, first);
            Assert.AreEqual(ArcanaId.Moon, second);
            Assert.AreEqual(ArcanaId.Star, third);
        }

        [Test]
        public void Match_StackedDeck_DealsTheChosenHand()
        {
            Match match = NewMatch();
            CollectionAssert.AreEquivalent(StartingHand, match.GetHand(match.CurrentApostle.Id));
            Assert.AreEqual(EngagementState.Free, match.Engagement);
        }

        // ---- Playing cards while Free ----

        [Test]
        public void PlayCard_NotInHand_IsRejected()
        {
            Match match = NewMatch();
            Assert.AreEqual(CardPlayResult.NotInHand, match.PlayCard(ArcanaId.Sun, 0));
        }

        [Test]
        public void PlayCard_WithoutTarget_PaysCapAddsDefAndDiscards()
        {
            Match match = NewMatch();
            ApostleState current = match.CurrentApostle;

            Assert.AreEqual(CardPlayResult.Success, match.PlayCard(ArcanaId.Star, 0));

            Assert.AreEqual(6, current.Shield);
            Assert.AreEqual(GameConstants.Phase1Capacity - 4, match.CurrentCapacity.Remaining);
            Assert.AreEqual(4, match.GetHand(current.Id).Count);
            Assert.AreEqual(1, match.Deck.DiscardCount);
            Assert.AreEqual(EngagementState.Free, match.Engagement);
        }

        [Test]
        public void PlayCard_AttackCardWithoutTarget_LosesItsAtkAndStaysFree()
        {
            Match match = NewMatch();

            match.PlayCard(ArcanaId.Magician, 0);

            Assert.AreEqual(EngagementState.Free, match.Engagement);
            Assert.AreEqual(0, match.PendingAttack);
            Assert.AreEqual(1, match.CurrentApostle.Shield, "Its DEF still applies");
        }

        [Test]
        public void PlayCard_NotEnoughCapacity_ChangesNothing()
        {
            Match match = NewMatch();
            match.PlayCard(ArcanaId.Death, 0); // 8 - 5 = 3 left

            Assert.AreEqual(CardPlayResult.NotEnoughCapacity, match.PlayCard(ArcanaId.Star, 0));
            Assert.AreEqual(4, match.GetHand(match.CurrentApostle.Id).Count);
            Assert.AreEqual(4, match.CurrentApostle.Shield);
            Assert.AreEqual(3, match.CurrentCapacity.Remaining);
        }

        [Test]
        public void PlayCard_ZeroCapCard_CostsNothing()
        {
            Match match = NewMatch();

            Assert.AreEqual(CardPlayResult.Success, match.PlayCard(ArcanaId.Fool, 0));
            Assert.AreEqual(GameConstants.Phase1Capacity, match.CurrentCapacity.Remaining);
        }

        // ---- Engaging ----

        [Test]
        public void PlayCard_WithAdjacentTarget_Engages()
        {
            Match match = EngagedMatch(out ApostleState target);

            Assert.AreEqual(EngagementState.Engaged, match.Engagement);
            Assert.AreEqual(target.Id, match.EngagedTargetId);
            Assert.AreEqual(3, match.PendingAttack);
            Assert.AreEqual(GameConstants.Phase1Capacity - 1, match.CurrentCapacity.Remaining);
            Assert.AreEqual(50, target.Vessel, "Nothing is resolved until Strike");
        }

        [Test]
        public void PlayCard_WhileEngaged_AddsItsAtkToTheAttack()
        {
            Match match = EngagedMatch(out _);

            Assert.AreEqual(CardPlayResult.Success, match.PlayCard(ArcanaId.Death, 0));
            Assert.AreEqual(10, match.PendingAttack);
            Assert.AreEqual(GameConstants.Phase1Capacity - 6, match.CurrentCapacity.Remaining);
        }

        [Test]
        public void PlayCard_ZeroAtkCardWithTarget_IsRejected()
        {
            Match match = NewMatch();
            ApostleState target = Opponent(match);
            target.MoveTo(FreeNeighbor(match, match.CurrentApostle.Position));

            Assert.AreEqual(CardPlayResult.CardHasNoAttack, match.PlayCard(ArcanaId.Star, target.Id));
            Assert.AreEqual(5, match.GetHand(match.CurrentApostle.Id).Count);
            Assert.AreEqual(GameConstants.Phase1Capacity, match.CurrentCapacity.Remaining);
        }

        [Test]
        public void PlayCard_TargetNotAdjacent_IsRejected()
        {
            Match match = NewMatch();

            Assert.AreEqual(CardPlayResult.TargetOutOfRange, match.PlayCard(ArcanaId.Magician, Opponent(match).Id));
            Assert.AreEqual(5, match.GetHand(match.CurrentApostle.Id).Count);
        }

        [Test]
        public void PlayCard_SelfOrUnknownTarget_IsRejected()
        {
            Match match = NewMatch();

            Assert.AreEqual(CardPlayResult.InvalidTarget, match.PlayCard(ArcanaId.Magician, match.CurrentApostle.Id));
            Assert.AreEqual(CardPlayResult.InvalidTarget, match.PlayCard(ArcanaId.Magician, 99));
        }

        [Test]
        public void PlayCard_WhileEngaged_DifferentTarget_IsRejected()
        {
            Match match = EngagedMatch(out _);

            Assert.AreEqual(CardPlayResult.WrongTarget, match.PlayCard(ArcanaId.Death, 99));
            Assert.AreEqual(3, match.PendingAttack);
        }

        // ---- Strike and disengaging ----

        [Test]
        public void Strike_ResolvesTheTotalAtkAgainstTheShield()
        {
            Match match = EngagedMatch(out ApostleState target);
            target.AddShield(3);
            match.PlayCard(ArcanaId.Death, 0); // 3 + 7 = 10 ATK

            Assert.IsTrue(match.Strike(out AttackOutcome outcome));

            Assert.AreEqual(7, outcome.Damage);
            Assert.AreEqual(43, target.Vessel);
            Assert.AreEqual(EngagementState.Disengaged, match.Engagement);
            Assert.AreEqual(0, match.PendingAttack);
            Assert.AreEqual(0, match.EngagedTargetId);
        }

        [Test]
        public void Strike_WhenNotEngaged_ReturnsFalse()
        {
            Match match = NewMatch();

            Assert.IsFalse(match.Strike(out _));
            Assert.AreEqual(EngagementState.Free, match.Engagement);
        }

        [Test]
        public void PlayCard_AfterStrike_IsRejected()
        {
            Match match = EngagedMatch(out _);
            match.Strike(out _);

            Assert.AreEqual(CardPlayResult.Disengaged, match.PlayCard(ArcanaId.Fool, 0));
        }

        [Test]
        public void Move_WhileEngaged_IsBlocked()
        {
            Match match = EngagedMatch(out _);
            HexCoord free = FreeNeighbor(match, match.CurrentApostle.Position);

            var destinations = new List<HexCoord>();
            match.GetLegalDestinations(destinations);

            Assert.AreEqual(0, destinations.Count);
            Assert.AreEqual(MoveResult.EngagedInCombat, match.Move(free));
        }

        [Test]
        public void Move_AfterStrike_IsAllowed()
        {
            Match match = EngagedMatch(out _);
            match.Strike(out _);

            var destinations = new List<HexCoord>();
            match.GetLegalDestinations(destinations);

            Assert.Greater(destinations.Count, 0);
            Assert.AreEqual(MoveResult.Success, match.Move(destinations[0]));
        }

        [Test]
        public void GetAttackTargets_AfterStrike_IsEmpty()
        {
            Match match = EngagedMatch(out _);
            match.Strike(out _);

            var targets = new List<ApostleState>();
            match.GetAttackTargets(targets);

            Assert.AreEqual(0, targets.Count);
        }

        [Test]
        public void EndTurn_WhileEngaged_StrikesFirst()
        {
            Match match = EngagedMatch(out ApostleState target);

            match.EndTurn();

            Assert.AreEqual(47, target.Vessel);
        }

        [Test]
        public void NextTurn_StartsFree()
        {
            Match match = EngagedMatch(out _);

            match.EndTurn();

            Assert.AreEqual(EngagementState.Free, match.Engagement);
            Assert.AreEqual(0, match.PendingAttack);
            Assert.AreEqual(0, match.EngagedTargetId);
        }

        [Test]
        public void Strike_KillingBlow_WinsTheMatch()
        {
            Match match = EngagedMatch(out ApostleState target);
            ApostleState attacker = match.CurrentApostle;
            target.TakeDamage(GameConstants.StartingVessel - 5);
            match.PlayCard(ArcanaId.Death, 0); // 10 ATK

            match.Strike(out _);

            Assert.IsTrue(match.IsOver);
            Assert.AreEqual(attacker.Id, match.WinnerId);
            Assert.AreEqual(MatchEndReason.LastApostleStanding, match.EndReason);
        }
    }
}