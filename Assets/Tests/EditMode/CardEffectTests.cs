using System;
using System.Collections.Generic;
using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests.EditMode
{
    public class CardEffectTests
    {
        // Both Apostles get this hand, so the tests do not depend on who plays first.
        // Magician A2 D1 C1, Death A7 D4 C5, Star A0 D6 C4, Moon A4 D10 C6, Fool A0 D0 C0.
        private static readonly ArcanaId[] StartingHand =
        {
            ArcanaId.Magician, ArcanaId.Death, ArcanaId.Star, ArcanaId.Moon, ArcanaId.Fool
        };

        private static Match NewMatch()
        {
            var stack = new List<ArcanaId>();
            foreach (ArcanaId card in StartingHand)
            {
                stack.Add(card);
                stack.Add(card);
            }
            for (int i = 0; i < 8; i++)
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

        // A new match with the opponent standing next to the current Apostle.
        private static Match MatchSideBySide(out ApostleState player, out ApostleState opponent)
        {
            Match match = NewMatch();
            player = match.CurrentApostle;
            opponent = Opponent(match);

            IReadOnlyList<HexCoord> neighbors = match.Battlefield.Board.GetNeighborsOnBoard(player.Position);
            for (int i = 0; i < neighbors.Count; i++)
            {
                if (!match.Battlefield.IsOccupied(neighbors[i]))
                {
                    opponent.MoveTo(neighbors[i]);
                    return match;
                }
            }
            throw new InvalidOperationException("No free tile next to the current Apostle.");
        }

        // ---- Timed effects ----

        [Test]
        public void TimedEffect_LastsForTheGivenNumberOfOwnTurns()
        {
            var apostle = new ApostleState(1, new HexCoord(0, 0), 7);
            apostle.AddTimedEffect(TimedEffectKind.Defense, 4, 2);
            Assert.AreEqual(4, apostle.Defense);

            apostle.BeginTurn();
            Assert.AreEqual(4, apostle.Defense, "Still active on the next turn");

            apostle.BeginTurn();
            Assert.AreEqual(0, apostle.Defense, "Ended at the start of the turn after that");
            Assert.AreEqual(0, apostle.TimedEffects.Count);
        }

        [Test]
        public void BeginTurn_ClearsShieldButKeepsActiveTimedDefense()
        {
            var apostle = new ApostleState(1, new HexCoord(0, 0), 7);
            apostle.AddShield(6);
            apostle.AddTimedEffect(TimedEffectKind.Defense, 4, 2);
            Assert.AreEqual(10, apostle.Defense);

            apostle.BeginTurn();

            Assert.AreEqual(0, apostle.Shield);
            Assert.AreEqual(4, apostle.Defense);
            Assert.AreEqual(1, apostle.TurnsStarted);
        }

        [Test]
        public void AddTimedEffect_InvalidArguments_Throw()
        {
            var apostle = new ApostleState(1, new HexCoord(0, 0), 7);

            Assert.Throws<ArgumentOutOfRangeException>(() => apostle.AddTimedEffect(TimedEffectKind.Defense, -1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => apostle.AddTimedEffect(TimedEffectKind.Defense, 4, 0));
        }

        [Test]
        public void Combat_CountsTimedDefense()
        {
            var attacker = new ApostleState(1, new HexCoord(0, 0), 7);
            var target = new ApostleState(2, new HexCoord(1, 0), 7);
            target.AddTimedEffect(TimedEffectKind.Defense, 4, 1);

            AttackOutcome outcome = Combat.Resolve(attacker, target, 6);

            Assert.AreEqual(2, outcome.Damage);
        }

        [Test]
        public void CardEffects_ProvenCardsHaveEffects()
        {
            Assert.IsInstanceOf<MagicianEffect>(CardEffects.Get(ArcanaId.Magician));
            Assert.IsInstanceOf<StarEffect>(CardEffects.Get(ArcanaId.Star));
            Assert.IsInstanceOf<MoonEffect>(CardEffects.Get(ArcanaId.Moon));
            Assert.IsInstanceOf<DeathEffect>(CardEffects.Get(ArcanaId.Death));
            Assert.Throws<ArgumentOutOfRangeException>(() => CardEffects.Get((ArcanaId)22));
        }

        // ---- The Magician ----

        [Test]
        public void Magician_AsTheEngagingCard_AddsOneAtk()
        {
            Match match = MatchSideBySide(out _, out ApostleState opponent);

            match.PlayCard(ArcanaId.Magician, opponent.Id);

            Assert.AreEqual(3, match.PendingAttack); // 2 + 1
        }

        [Test]
        public void Magician_WhileEngaged_AddsOneAtk()
        {
            Match match = MatchSideBySide(out _, out ApostleState opponent);
            match.PlayCard(ArcanaId.Death, opponent.Id);

            match.PlayCard(ArcanaId.Magician, 0);

            Assert.AreEqual(10, match.PendingAttack); // 7 + 2 + 1
        }

        [Test]
        public void Magician_WithoutAnAttack_GivesNoBonusLater()
        {
            Match match = MatchSideBySide(out _, out ApostleState opponent);
            match.PlayCard(ArcanaId.Magician, 0); // played for DEF only

            match.PlayCard(ArcanaId.Death, opponent.Id);

            Assert.AreEqual(7, match.PendingAttack);
        }

        // ---- The Star ----

        [Test]
        public void Star_HealsTwoVessel()
        {
            Match match = NewMatch();
            ApostleState player = match.CurrentApostle;
            player.TakeDamage(10);

            match.PlayCard(ArcanaId.Star, 0);
            match.PlayQueued();

            Assert.AreEqual(42, player.Vessel);
        }

        [Test]
        public void Star_DefenseBonusLastsTwoRounds()
        {
            Match match = NewMatch();
            ApostleState player = match.CurrentApostle;

            match.PlayCard(ArcanaId.Star, 0);
            match.PlayQueued();
            Assert.AreEqual(10, player.Defense, "6 shield + 4 timed");

            match.EndTurn(); // opponent's turn
            Assert.AreEqual(10, player.Defense, "Nothing clears on other turns");

            match.EndTurn(); // player's 2nd turn: the shield clears, the bonus stays
            Assert.AreSame(player, match.CurrentApostle);
            Assert.AreEqual(4, player.Defense);

            match.EndTurn();
            match.EndTurn(); // player's 3rd turn: the bonus has ended
            Assert.AreSame(player, match.CurrentApostle);
            Assert.AreEqual(0, player.Defense);
        }

        // ---- The Moon ----

        [Test]
        public void Moon_FailedAttackGivesTheAttackerTwoCorruption()
        {
            Match match = MatchSideBySide(out ApostleState player, out ApostleState opponent);
            match.PlayCard(ArcanaId.Moon, 0); // 10 shield, Moon active
            match.EndTurn();

            Assert.AreSame(opponent, match.CurrentApostle);
            match.PlayCard(ArcanaId.Magician, player.Id); // 3 ATK against 10 DEF
            match.Strike(out AttackOutcome outcome);

            Assert.IsFalse(outcome.BrokeDefense);
            Assert.AreEqual(MoonEffect.CorruptionForFailedAttack, opponent.Corruption);
            Assert.AreEqual(MoonEffect.CorruptionForFailedAttack, outcome.CorruptionGained);
            Assert.AreEqual(50, player.Vessel);
        }

        [Test]
        public void Moon_BrokenDefense_GivesOnlyTheNormalCorruption()
        {
            var attacker = new ApostleState(1, new HexCoord(0, 0), 7);
            var target = new ApostleState(2, new HexCoord(1, 0), 7);
            target.AddShield(10);
            target.AddTimedEffect(TimedEffectKind.FailedAttackCorruption, MoonEffect.CorruptionForFailedAttack, 1);

            Combat.Resolve(attacker, target, 12);

            Assert.AreEqual(GameConstants.AttackCorruption, attacker.Corruption);
        }

        [Test]
        public void Moon_EndsAtTheStartOfTheOwnersNextTurn()
        {
            Match match = NewMatch();
            ApostleState player = match.CurrentApostle;
            match.PlayCard(ArcanaId.Moon, 0);

            match.EndTurn();
            Assert.AreEqual(2, player.GetTimedTotal(TimedEffectKind.FailedAttackCorruption));

            match.EndTurn();
            Assert.AreEqual(0, player.GetTimedTotal(TimedEffectKind.FailedAttackCorruption));
        }

        // ---- Death ----

        [Test]
        public void Death_ExecutesALowVesselTargetWhenDefenseBreaks()
        {
            Match match = MatchSideBySide(out _, out ApostleState opponent);
            opponent.TakeDamage(46); // 4 Vessel
            opponent.AddShield(5);

            match.PlayCard(ArcanaId.Death, opponent.Id); // 7 ATK - 5 DEF = 2 damage
            match.Strike(out _);

            Assert.IsTrue(opponent.IsEliminated);
        }

        [Test]
        public void Death_DefenseHolds_NoExecution()
        {
            Match match = MatchSideBySide(out _, out ApostleState opponent);
            opponent.TakeDamage(46);
            opponent.AddShield(10);

            match.PlayCard(ArcanaId.Death, opponent.Id);
            match.Strike(out _);

            Assert.AreEqual(4, opponent.Vessel);
        }

        [Test]
        public void Death_TargetAtFiveVessel_IsNotExecuted()
        {
            Match match = MatchSideBySide(out _, out ApostleState opponent);
            opponent.TakeDamage(45); // 5 Vessel: not "less than 5"
            opponent.AddShield(5);

            match.PlayCard(ArcanaId.Death, opponent.Id);
            match.Strike(out _);

            Assert.AreEqual(3, opponent.Vessel);
        }

        [Test]
        public void Death_HealthyTarget_TakesNormalDamage()
        {
            Match match = MatchSideBySide(out _, out ApostleState opponent);

            match.PlayCard(ArcanaId.Death, opponent.Id);
            match.Strike(out _);

            Assert.AreEqual(43, opponent.Vessel);
        }

        [Test]
        public void Death_PlayedWithoutAnAttack_ArmsNothing()
        {
            Match match = MatchSideBySide(out _, out ApostleState opponent);
            opponent.TakeDamage(46);
            opponent.AddShield(2);
            match.PlayCard(ArcanaId.Death, 0); // for DEF only

            match.PlayCard(ArcanaId.Magician, opponent.Id); // 3 ATK - 2 DEF = 1 damage
            match.Strike(out _);

            Assert.AreEqual(3, opponent.Vessel);
        }
    }
}