using System;
using System.Collections.Generic;
using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests.EditMode
{
    /// <summary>Stage C5c: The Hierophant, The Hermit, The Hanged Man and The Tower.</summary>
    public class CardEffectBatchCTests
    {
        // Every Apostle gets the same 5-card hand. The draw pile after dealing is all Fools.
        private static Match MatchWithHand(int players, params ArcanaId[] hand)
        {
            Assert.AreEqual(GameConstants.StartingHandSize, hand.Length, "Give exactly 5 cards");

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

        // In a 2-player match, ends turns until it is this Apostle's turn again.
        private static void SkipToNextTurnOf(Match match, ApostleState apostle)
        {
            match.EndTurn();
            match.EndTurn();
            Assert.AreSame(apostle, match.CurrentApostle);
        }

        // ---- Delayed timed effects ----

        [Test]
        public void DelayedTimedEffect_StartsOnTheNextTurn()
        {
            var apostle = new ApostleState(1, new HexCoord(0, 0), 7);
            apostle.AddTimedEffect(TimedEffectKind.Attack, 2, 1, 1);
            Assert.AreEqual(0, apostle.GetTimedTotal(TimedEffectKind.Attack), "Not yet");

            apostle.BeginTurn();
            Assert.AreEqual(2, apostle.GetTimedTotal(TimedEffectKind.Attack), "Active on the next turn");

            apostle.BeginTurn();
            Assert.AreEqual(0, apostle.GetTimedTotal(TimedEffectKind.Attack), "Ended after it");
        }

        [Test]
        public void AddTimedEffect_NegativeDelay_Throws()
        {
            var apostle = new ApostleState(1, new HexCoord(0, 0), 7);
            Assert.Throws<ArgumentOutOfRangeException>(() => apostle.AddTimedEffect(TimedEffectKind.Attack, 2, 1, -1));
        }

        // ---- The Hierophant ----

        [Test]
        public void Hierophant_PlusTwoAtkAndDefOnTheTurnItIsPlayed()
        {
            Match match = MatchWithHand(2, ArcanaId.Hierophant, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState player = match.CurrentApostle;

            Play(match, ArcanaId.Hierophant);
            Assert.AreEqual(2, player.Defense);
            Assert.AreEqual(2, player.GetTimedTotal(TimedEffectKind.Attack));

            match.EndTurn();
            Assert.AreEqual(2, player.Defense, "The DEF lasts through the opponents' turns");

            match.EndTurn();
            Assert.AreSame(player, match.CurrentApostle);
            Assert.AreEqual(0, player.Defense, "Ended at the start of the player's next turn");
            Assert.AreEqual(0, player.GetTimedTotal(TimedEffectKind.Attack));
        }

        [Test]
        public void Hierophant_AtkBonusJoinsThisTurnsAttack()
        {
            Match match = MatchWithHand(2, ArcanaId.Hierophant, ArcanaId.Magician, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState target = OpponentNextToCurrent(match);
            Play(match, ArcanaId.Hierophant);

            Play(match, ArcanaId.Magician, target.Id);
            Assert.AreEqual(3, match.PendingAttack, "Timed ATK is added when the attack resolves");
            match.Strike(out AttackOutcome outcome);

            Assert.AreEqual(5, outcome.Damage); // 3 + 2
        }

        // ---- The Hermit ----

        [Test]
        public void Hermit_ThisTurnAndNextTurnHaveTwoMoreCapacity()
        {
            Match match = MatchWithHand(2, ArcanaId.Hermit, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState player = match.CurrentApostle;

            Play(match, ArcanaId.Hermit); // costs 2, then +2 maximum
            Assert.AreEqual(GameConstants.Phase1Capacity + 2, match.CurrentCapacity.Maximum);
            Assert.AreEqual(GameConstants.Phase1Capacity, match.CurrentCapacity.Remaining);

            match.EndTurn();
            Assert.AreEqual(GameConstants.Phase1Capacity, match.CurrentCapacity.Maximum, "The opponent is unaffected");

            match.EndTurn();
            Assert.AreSame(player, match.CurrentApostle);
            Assert.AreEqual(GameConstants.Phase1Capacity + 2, match.CurrentCapacity.Maximum, "Next turn too");

            SkipToNextTurnOf(match, player);
            Assert.AreEqual(GameConstants.Phase1Capacity, match.CurrentCapacity.Maximum, "Then it ends");
        }

        // ---- The Tower ----

        [Test]
        public void Tower_EachOpponentsNextTurnHasTwoLessCapacity()
        {
            Match match = MatchWithHand(3, ArcanaId.Tower, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState player = match.CurrentApostle;
            Play(match, ArcanaId.Tower);

            match.EndTurn();
            Assert.AreEqual(GameConstants.Phase1Capacity - 2, match.CurrentCapacity.Maximum, "First opponent");
            match.EndTurn();
            Assert.AreEqual(GameConstants.Phase1Capacity - 2, match.CurrentCapacity.Maximum, "Second opponent");
            match.EndTurn();
            Assert.AreSame(player, match.CurrentApostle);
            Assert.AreEqual(GameConstants.Phase1Capacity, match.CurrentCapacity.Maximum, "The player is unaffected");

            match.EndTurn();
            Assert.AreEqual(GameConstants.Phase1Capacity, match.CurrentCapacity.Maximum, "Only for 1 round");
        }

        // ---- The Hanged Man ----

        [Test]
        public void HangedMan_LowestVessel_PlusFiveAtkThisTurnAndNext()
        {
            Match match = MatchWithHand(2, ArcanaId.HangedMan, ArcanaId.Magician, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState player = match.CurrentApostle;
            player.TakeDamage(10);
            ApostleState target = OpponentNextToCurrent(match);

            Play(match, ArcanaId.HangedMan);
            Play(match, ArcanaId.Magician, target.Id);
            match.Strike(out AttackOutcome outcome);
            Assert.AreEqual(8, outcome.Damage); // 3 + 5

            SkipToNextTurnOf(match, player);
            Assert.AreEqual(5, player.GetTimedTotal(TimedEffectKind.Attack), "Still active next turn");

            SkipToNextTurnOf(match, player);
            Assert.AreEqual(0, player.GetTimedTotal(TimedEffectKind.Attack), "Ended after 2 rounds");
        }

        [Test]
        public void HangedMan_NotLowestVessel_NoBonus()
        {
            Match match = MatchWithHand(2, ArcanaId.HangedMan, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState player = match.CurrentApostle;
            OpponentNextToCurrent(match).TakeDamage(10);

            Play(match, ArcanaId.HangedMan);

            Assert.AreEqual(0, player.GetTimedTotal(TimedEffectKind.Attack));
        }

        [Test]
        public void HangedMan_TiedForLowest_GetsTheBonus()
        {
            Match match = MatchWithHand(2, ArcanaId.HangedMan, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState player = match.CurrentApostle;

            Play(match, ArcanaId.HangedMan);

            Assert.AreEqual(HangedManEffect.AttackBonus, player.GetTimedTotal(TimedEffectKind.Attack));
        }
    }
}