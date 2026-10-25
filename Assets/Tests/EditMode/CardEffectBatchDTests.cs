using System;
using System.Collections.Generic;
using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests.EditMode
{
    /// <summary>Stage C5d: Temperance, Wheel of Fortune and The World.</summary>
    public class CardEffectBatchDTests
    {
        // hands[i] is dealt to the Apostle at index i (Apostle i + 1). The draw pile after dealing is all Fools.
        private static Match MatchWithHands(params ArcanaId[][] hands)
        {
            var stack = new List<ArcanaId>();
            for (int card = 0; card < GameConstants.StartingHandSize; card++)
            {
                for (int p = 0; p < hands.Length; p++)
                    stack.Add(hands[p][card]);
            }
            for (int i = 0; i < 20; i++)
                stack.Add(ArcanaId.Fool);

            HexBoard board = StandardBoard.Create();
            return Match.Create(hands.Length, board, SpawnPlanner.PickSpread(board, hands.Length), new Random(1),
                Deck.CreateStacked(stack, new Random(2)));
        }

        // Every Apostle gets the same hand.
        private static Match MatchWithHand(int players, params ArcanaId[] hand)
        {
            var hands = new ArcanaId[players][];
            for (int p = 0; p < players; p++)
                hands[p] = hand;
            return MatchWithHands(hands);
        }

        // 2 players only: moves the opponent next to the current Apostle and returns it.
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
        }

        private static int CountOf(IReadOnlyList<ArcanaId> hand, ArcanaId card)
        {
            int count = 0;
            for (int i = 0; i < hand.Count; i++)
            {
                if (hand[i] == card)
                    count++;
            }
            return count;
        }

        // ---- Temperance ----

        [Test]
        public void Temperance_EndsTheTurnImmediately()
        {
            Match match = MatchWithHand(2, ArcanaId.Temperance, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState player = match.CurrentApostle;

            Play(match, ArcanaId.Temperance);

            Assert.AreNotSame(player, match.CurrentApostle);
            Assert.AreEqual(5, match.GetHand(player.Id).Count, "5 - 1 played + 1 drawn at the end of the turn");
        }

        [Test]
        public void Temperance_NextTurnPlusFiveAtkAndDef()
        {
            Match match = MatchWithHand(2, ArcanaId.Temperance, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState player = match.CurrentApostle;

            Play(match, ArcanaId.Temperance);
            Assert.AreEqual(5, player.Defense, "Only its own 5 DEF shield during the opponent's turn");
            Assert.AreEqual(0, player.GetTimedTotal(TimedEffectKind.Attack));

            match.EndTurn();
            Assert.AreSame(player, match.CurrentApostle);
            Assert.AreEqual(5, player.Defense, "Shield cleared, +5 timed DEF active");
            Assert.AreEqual(5, player.GetTimedTotal(TimedEffectKind.Attack));

            match.EndTurn();
            match.EndTurn();
            Assert.AreEqual(0, player.Defense);
            Assert.AreEqual(0, player.GetTimedTotal(TimedEffectKind.Attack));
        }

        [Test]
        public void Temperance_WhileEngaged_TheAttackResolvesFirst()
        {
            Match match = MatchWithHand(2, ArcanaId.Magician, ArcanaId.Temperance, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState player = match.CurrentApostle;
            ApostleState target = OpponentNextToCurrent(match);
            Play(match, ArcanaId.Magician, target.Id); // 3 ATK

            Play(match, ArcanaId.Temperance);

            Assert.AreEqual(47, target.Vessel);
            Assert.AreNotSame(player, match.CurrentApostle);
        }

        // ---- Wheel of Fortune ----

        private static readonly ArcanaId[] SunHand =
            { ArcanaId.WheelOfFortune, ArcanaId.Sun, ArcanaId.Sun, ArcanaId.Sun, ArcanaId.Sun };
        private static readonly ArcanaId[] MoonHand =
            { ArcanaId.WheelOfFortune, ArcanaId.Moon, ArcanaId.Moon, ArcanaId.Moon, ArcanaId.Moon };

        [Test]
        public void WheelOfFortune_WhenAttacking_SwapsOneRandomCardEachWay()
        {
            Match match = MatchWithHands(SunHand, MoonHand);
            ApostleState player = match.CurrentApostle;
            ApostleState target = OpponentNextToCurrent(match);
            bool playerHasSuns = player == match.Battlefield.Apostles[0];
            ArcanaId playerFiller = playerHasSuns ? ArcanaId.Sun : ArcanaId.Moon;

            Play(match, ArcanaId.WheelOfFortune, target.Id);

            IReadOnlyList<ArcanaId> playerHand = match.GetHand(player.Id);
            IReadOnlyList<ArcanaId> targetHand = match.GetHand(target.Id);
            Assert.AreEqual(4, playerHand.Count);
            Assert.AreEqual(5, targetHand.Count);
            Assert.AreEqual(3, CountOf(playerHand, playerFiller), "The player gave one of its cards away");
            Assert.AreEqual(1, CountOf(targetHand, playerFiller), "The target received it");
        }

        
        [Test]
        public void WheelOfFortune_RecordsWhichCardsChangedHands()
        {
            Match match = MatchWithHands(SunHand, MoonHand);
            ApostleState player = match.CurrentApostle;
            ApostleState target = OpponentNextToCurrent(match);
            int before = match.LastSwap.Number;

            Play(match, ArcanaId.WheelOfFortune, target.Id);

            CardSwap swap = match.LastSwap;
            Assert.AreEqual(before + 1, swap.Number);
            Assert.AreEqual(player.Id, swap.PlayerId);
            Assert.AreEqual(target.Id, swap.TargetId);
            Assert.IsTrue(swap.CardsKnown);
            Assert.AreEqual(swap.Received, match.GetHand(player.Id)[swap.PlayerIndex], "The player's new card sits where the old one was");
            Assert.AreEqual(swap.Given, match.GetHand(target.Id)[swap.TargetIndex], "The target's new card sits where the old one was");
            Assert.AreNotEqual(swap.Given, swap.Received, "A Sun went one way and a Moon the other");
        }
        // ---- The World ----

        [Test]
        public void World_SkipsTheRestOfTheRound()
        {
            Match match = MatchWithHand(3, ArcanaId.World, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState player = match.CurrentApostle;
            Play(match, ArcanaId.World);

            match.EndTurn();

            Assert.AreEqual(2, match.Turns.RoundNumber);
            Assert.AreSame(player, match.CurrentApostle, "The player was first in the round, so goes first again");
            foreach (ApostleState other in match.Battlefield.Apostles)
            {
                if (other != player)
                    Assert.AreEqual(5, match.GetHand(other.Id).Count, "Skipped Apostles took no turn, so drew nothing");
            }
        }

        [Test]
        public void World_OnlyAffectsTheTurnItWasPlayed()
        {
            Match match = MatchWithHand(3, ArcanaId.World, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState player = match.CurrentApostle;
            Play(match, ArcanaId.World);
            match.EndTurn(); // round 2, the player again

            match.EndTurn();

            Assert.AreEqual(2, match.Turns.RoundNumber);
            Assert.AreNotSame(player, match.CurrentApostle, "The next Apostle plays normally");
        }

        [Test]
        public void World_InThePhasesLastRound_EndsThePhase()
        {
            Match match = MatchWithHand(2, ArcanaId.World, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState player = match.CurrentApostle;
            for (int i = 0; i < 4; i++)
                match.EndTurn();
            Assert.AreEqual(3, match.Turns.RoundNumber);
            Assert.AreSame(player, match.CurrentApostle);

            Play(match, ArcanaId.World);
            TurnEvent turnEvent = match.EndTurn();

            Assert.AreEqual(TurnEvent.PhaseEnded, turnEvent);
            Assert.IsTrue(match.IsAwaitingPhaseStart);
        }
    }
}