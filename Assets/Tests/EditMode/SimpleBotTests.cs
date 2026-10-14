using System;
using System.Collections.Generic;
using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests.EditMode
{
    /// <summary>The test bot only ever does what the rules allow, and plays sensibly.</summary>
    public class SimpleBotTests
    {
        // Every Apostle's hand: Magician A2 D1 C1, Death A7 D4 C5, Emperor A6 D3 C7, Devil A8 D3 C6, Fool.
        private static Match NewMatch(int players = 3)
        {
            ArcanaId[] hand = { ArcanaId.Death, ArcanaId.Magician, ArcanaId.Emperor, ArcanaId.Devil, ArcanaId.Fool };
            var stack = new List<ArcanaId>();
            foreach (ArcanaId card in hand)
            {
                for (int p = 0; p < players; p++)
                    stack.Add(card);
            }
            for (int i = 0; i < 20; i++)
                stack.Add(ArcanaId.Fool);

            HexBoard board = StandardBoard.Create();
            return Match.Create(players, board, SpawnPlanner.PickSpread(board, players), new Random(1),
                Deck.CreateStacked(stack, new Random(2)));
        }

        private static ApostleState FirstOpponent(Match match)
        {
            foreach (ApostleState apostle in match.Battlefield.Apostles)
            {
                if (apostle != match.CurrentApostle)
                    return apostle;
            }
            return null;
        }

        private static void MoveNextToCurrent(Match match, ApostleState mover)
        {
            IReadOnlyList<HexCoord> neighbors = match.Battlefield.Board.GetNeighborsOnBoard(match.CurrentApostle.Position);
            for (int i = 0; i < neighbors.Count; i++)
            {
                if (!match.Battlefield.IsOccupied(neighbors[i]))
                {
                    mover.MoveTo(neighbors[i]);
                    return;
                }
            }
            throw new InvalidOperationException("No free tile next to the current Apostle.");
        }

        [Test]
        public void Bot_AttacksAnAdjacentOpponent_WithItsStrongestCard()
        {
            Match match = NewMatch();
            ApostleState target = FirstOpponent(match);
            MoveNextToCurrent(match, target);

            BotAction action = new SimpleBot().Decide(match);

            Assert.AreEqual(BotActionKind.PlayCard, action.Kind);
            Assert.AreEqual(ArcanaId.Devil, action.Card, "Devil has the most ATK (8) of the affordable cards");
            Assert.AreEqual(target.Id, action.TargetId);
        }

        [Test]
        public void Bot_WhileEngaged_AddsWhatItCanAfford_ThenStrikes()
        {
            Match match = NewMatch();
            MoveNextToCurrent(match, FirstOpponent(match));
            var bot = new SimpleBot();

            Assert.IsTrue(SimpleBot.Apply(match, bot.Decide(match)));   // Devil: 8 Capacity - 6 = 2 left
            BotAction second = bot.Decide(match);
            Assert.AreEqual(BotActionKind.PlayCard, second.Kind);
            Assert.AreEqual(ArcanaId.Magician, second.Card, "Magician (C1) is the only ATK card still affordable");
            Assert.IsTrue(SimpleBot.Apply(match, second));

            Assert.AreEqual(BotActionKind.Strike, bot.Decide(match).Kind);
        }

        [Test]
        public void Bot_WithNobodyInReach_StepsTowardTheNearestOpponent()
        {
            Match match = NewMatch();
            ApostleState me = match.CurrentApostle;
            int before = int.MaxValue;
            foreach (ApostleState other in match.Battlefield.Apostles)
            {
                if (other != me)
                    before = Math.Min(before, HexCoord.Distance(me.Position, other.Position));
            }
            Assume.That(before, Is.GreaterThan(1), "The spawns should start apart");

            BotAction action = new SimpleBot().Decide(match);

            Assert.AreEqual(BotActionKind.Move, action.Kind);
            int after = int.MaxValue;
            foreach (ApostleState other in match.Battlefield.Apostles)
            {
                if (other != me)
                    after = Math.Min(after, HexCoord.Distance(action.Destination, other.Position));
            }
            Assert.Less(after, before);
        }

        [TestCase(2, 11)]
        [TestCase(4, 22)]
        [TestCase(6, 33)]
        public void BotsOnly_PlayLongMatches_WithEveryActionAllowed(int players, int seed)
        {
            HexBoard board = StandardBoard.Create();
            Match match = Match.Create(players, board, SpawnPlanner.PickSpread(board, players), new Random(seed));
            var bot = new SimpleBot();
            const int MaxActions = 20000;
            const int MaxActionsPerTurn = 60;
            int actionsThisTurn = 0;
            int lastApostleId = -1;

            for (int i = 0; i < MaxActions && !match.IsOver; i++)
            {
                int current = match.CurrentApostle != null ? match.CurrentApostle.Id : 0;
                actionsThisTurn = current == lastApostleId ? actionsThisTurn + 1 : 0;
                lastApostleId = current;
                Assert.Less(actionsThisTurn, MaxActionsPerTurn, $"Apostle {current}'s turn never ends");

                BotAction action = bot.Decide(match);
                Assert.AreNotEqual(BotActionKind.None, action.Kind, "A running match always has something to do");
                Assert.IsTrue(SimpleBot.Apply(match, action), $"Apostle {current}: the rules refused '{action}'");
            }
        }
    }
}