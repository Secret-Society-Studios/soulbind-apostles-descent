using System;
using System.Collections.Generic;
using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests.EditMode
{
    public class CombatTests
    {
        private static ApostleState Attacker(int souls = 7) => new ApostleState(1, new HexCoord(0, 0), souls);
        private static ApostleState Target(int souls = 7) => new ApostleState(2, new HexCoord(1, 0), souls);

        // ---- Shield ----

        [Test]
        public void NewApostle_HasNoShield()
        {
            Assert.AreEqual(0, Attacker().Shield);
        }

        [Test]
        public void AddShield_Stacks()
        {
            ApostleState apostle = Attacker();
            apostle.AddShield(3);
            apostle.AddShield(4);
            Assert.AreEqual(7, apostle.Shield);
        }

        [Test]
        public void AddShield_Negative_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Attacker().AddShield(-1));
        }

        [Test]
        public void ClearShield_RemovesAllShield()
        {
            ApostleState apostle = Attacker();
            apostle.AddShield(5);
            apostle.ClearShield();
            Assert.AreEqual(0, apostle.Shield);
        }

        // ---- Damage formula ----

        [TestCase(10, 0, 10)]
        [TestCase(10, 4, 6)]
        [TestCase(4, 4, 0)]
        [TestCase(3, 10, 0)]
        public void GetDamage_IsAttackMinusShield_NeverNegative(int attack, int shield, int expected)
        {
            Assert.AreEqual(expected, Combat.GetDamage(attack, shield));
        }

        // ---- Resolving an attack ----

        [Test]
        public void Resolve_Hit_DealsDamageStealsSoulAndAddsCorruption()
        {
            ApostleState attacker = Attacker();
            ApostleState target = Target();
            target.AddShield(2);

            AttackOutcome outcome = Combat.Resolve(attacker, target, 6);

            Assert.AreEqual(4, outcome.Damage);
            Assert.AreEqual(1, outcome.SoulsStolen);
            Assert.AreEqual(1, outcome.CorruptionGained);
            Assert.IsTrue(outcome.BrokeDefense);
            Assert.AreEqual(46, target.Vessel);
            Assert.AreEqual(6, target.Souls);
            Assert.AreEqual(8, attacker.Souls);
            Assert.AreEqual(1, attacker.Corruption);
        }

        [Test]
        public void Resolve_ShieldIsNotUsedUpByHits()
        {
            ApostleState attacker = Attacker();
            ApostleState target = Target();
            target.AddShield(4);

            Combat.Resolve(attacker, target, 6);
            Combat.Resolve(attacker, target, 6);

            Assert.AreEqual(4, target.Shield);
            Assert.AreEqual(46, target.Vessel);
        }

        [Test]
        public void Resolve_FailsToBreakDefense_ChangesNothing()
        {
            ApostleState attacker = Attacker();
            ApostleState target = Target();
            target.AddShield(10);

            AttackOutcome outcome = Combat.Resolve(attacker, target, 4);

            Assert.AreEqual(0, outcome.Damage);
            Assert.IsFalse(outcome.BrokeDefense);
            Assert.AreEqual(0, outcome.SoulsStolen);
            Assert.AreEqual(0, outcome.CorruptionGained);
            Assert.AreEqual(50, target.Vessel);
            Assert.AreEqual(7, target.Souls);
            Assert.AreEqual(7, attacker.Souls);
            Assert.AreEqual(0, attacker.Corruption);
        }

        [Test]
        public void Resolve_SoullockedAttacker_DamagesAndCorruptsButStealsNothing()
        {
            ApostleState attacker = Attacker();
            ApostleState target = Target();
            attacker.AddCorruption(5);

            AttackOutcome outcome = Combat.Resolve(attacker, target, 5);

            Assert.AreEqual(5, outcome.Damage);
            Assert.AreEqual(0, outcome.SoulsStolen);
            Assert.AreEqual(45, target.Vessel);
            Assert.AreEqual(7, target.Souls, "The target keeps its Soul");
            Assert.AreEqual(7, attacker.Souls);
            Assert.AreEqual(6, attacker.Corruption);
        }

        [Test]
        public void Resolve_AttackerAtFourCorruption_StealsThenBecomesCorrupted()
        {
            ApostleState attacker = Attacker();
            ApostleState target = Target();
            attacker.AddCorruption(4);

            Combat.Resolve(attacker, target, 5);

            Assert.AreEqual(8, attacker.Souls);
            Assert.AreEqual(5, attacker.Corruption);
            Assert.IsTrue(attacker.IsSoullocked);
        }

        [Test]
        public void Resolve_TargetWithNoSouls_StillGivesCorruption()
        {
            ApostleState attacker = Attacker();
            ApostleState target = Target(0);

            AttackOutcome outcome = Combat.Resolve(attacker, target, 5);

            Assert.AreEqual(0, outcome.SoulsStolen);
            Assert.AreEqual(45, target.Vessel);
            Assert.AreEqual(0, target.Corruption, "Already at 0 Souls: no new +5");
            Assert.AreEqual(7, attacker.Souls);
            Assert.AreEqual(1, attacker.Corruption);
        }

        [Test]
        public void Resolve_TargetLosesLastSoul_GainsZeroSoulCorruption()
        {
            ApostleState attacker = Attacker();
            ApostleState target = Target(1);

            Combat.Resolve(attacker, target, 5);

            Assert.AreEqual(0, target.Souls);
            Assert.AreEqual(GameConstants.ZeroSoulCorruption, target.Corruption);
            Assert.AreEqual(8, attacker.Souls);
        }

        [Test]
        public void Resolve_EliminatingHit_StillStealsAndCorrupts()
        {
            ApostleState attacker = Attacker();
            ApostleState target = Target();
            target.TakeDamage(45);

            AttackOutcome outcome = Combat.Resolve(attacker, target, 10);

            Assert.IsTrue(target.IsEliminated);
            Assert.AreEqual(10, outcome.Damage);
            Assert.AreEqual(1, outcome.SoulsStolen);
            Assert.AreEqual(8, attacker.Souls);
            Assert.AreEqual(1, attacker.Corruption);
        }

        [Test]
        public void Resolve_InvalidArguments_Throw()
        {
            ApostleState attacker = Attacker();
            ApostleState target = Target();
            var dead = new ApostleState(3, new HexCoord(2, 0), 7);
            dead.TakeDamage(GameConstants.StartingVessel);

            Assert.Throws<ArgumentNullException>(() => Combat.Resolve(null, target, 5));
            Assert.Throws<ArgumentNullException>(() => Combat.Resolve(attacker, null, 5));
            Assert.Throws<ArgumentException>(() => Combat.Resolve(attacker, attacker, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => Combat.Resolve(attacker, target, 0));
            Assert.Throws<InvalidOperationException>(() => Combat.Resolve(attacker, dead, 5));
            Assert.Throws<InvalidOperationException>(() => Combat.Resolve(dead, target, 5));
        }

        // ---- Attack range on the battlefield ----

        private static HexBoard Grid() => HexBoard.FromRows(new[] { "####", "####", "####" }, 0);

        [Test]
        public void GetAttackTargets_OnlyAdjacentLivingOpponents()
        {
            var field = new Battlefield(Grid());
            var a = new ApostleState(1, new HexCoord(0, 0), 7);
            var b = new ApostleState(2, new HexCoord(2, 0), 7);
            var c = new ApostleState(3, new HexCoord(0, 2), 7);
            var d = new ApostleState(4, new HexCoord(3, 2), 7);
            field.AddApostle(a);
            field.AddApostle(b);
            field.AddApostle(c);
            field.AddApostle(d);

            a.MoveTo(new HexCoord(1, 1));          // b and c are now adjacent, d is 3 tiles away
            c.TakeDamage(GameConstants.StartingVessel);

            var results = new List<ApostleState>();
            field.GetAttackTargets(a, results);

            Assert.AreEqual(1, results.Count);
            Assert.AreSame(b, results[0]);
        }

        [Test]
        public void GetAttackTargets_NullArguments_Throw()
        {
            var field = new Battlefield(Grid());
            var a = new ApostleState(1, new HexCoord(0, 0), 7);
            field.AddApostle(a);

            Assert.Throws<ArgumentNullException>(() => field.GetAttackTargets(a, null));
            Assert.Throws<ArgumentNullException>(() => field.GetAttackTargets(null, new List<ApostleState>()));
        }

        // ---- Integration with Match ----

        private static Match NewMatch(int players)
        {
            HexBoard board = StandardBoard.Create();
            return Match.Create(players, board, SpawnPlanner.PickSpread(board, players), new Random(1));
        }

        private static ApostleState Opponent(Match match)
        {
            IReadOnlyList<ApostleState> apostles = match.Battlefield.Apostles;
            for (int i = 0; i < apostles.Count; i++)
            {
                if (apostles[i] != match.CurrentApostle)
                    return apostles[i];
            }
            throw new InvalidOperationException("No opponent.");
        }

        // Teleports mover onto a free tile next to anchor (test setup only).
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

        [Test]
        public void Match_Attack_AdjacentOpponent_Resolves()
        {
            Match match = NewMatch(4);
            ApostleState current = match.CurrentApostle;
            ApostleState target = Opponent(match);
            PlaceNextTo(match, target, current);

            AttackResult result = match.Attack(target.Id, 5, out AttackOutcome outcome);

            Assert.AreEqual(AttackResult.Resolved, result);
            Assert.AreEqual(5, outcome.Damage);
            Assert.AreEqual(45, target.Vessel);
            Assert.AreEqual(8, current.Souls);
            Assert.AreEqual(1, current.Corruption);
        }

        [Test]
        public void Match_Attack_OutOfRange_ChangesNothing()
        {
            Match match = NewMatch(4);
            ApostleState target = Opponent(match);

            AttackResult result = match.Attack(target.Id, 5, out _);

            Assert.AreEqual(AttackResult.OutOfRange, result);
            Assert.AreEqual(50, target.Vessel);
            Assert.AreEqual(0, match.CurrentApostle.Corruption);
        }

        [Test]
        public void Match_Attack_Self_IsRejected()
        {
            Match match = NewMatch(4);
            Assert.AreEqual(AttackResult.TargetIsSelf, match.Attack(match.CurrentApostle.Id, 5, out _));
        }

        [Test]
        public void Match_Attack_UnknownId_IsRejected()
        {
            Match match = NewMatch(4);
            Assert.AreEqual(AttackResult.TargetNotFound, match.Attack(99, 5, out _));
        }

        [Test]
        public void Match_Attack_EliminatedTarget_IsRejected()
        {
            Match match = NewMatch(4);
            ApostleState target = Opponent(match);
            target.TakeDamage(GameConstants.StartingVessel);

            Assert.AreEqual(AttackResult.TargetEliminated, match.Attack(target.Id, 5, out _));
        }

        [Test]
        public void Match_Attack_ZeroAttack_Throws()
        {
            Match match = NewMatch(4);
            ApostleState target = Opponent(match);
            PlaceNextTo(match, target, match.CurrentApostle);

            Assert.Throws<ArgumentOutOfRangeException>(() => match.Attack(target.Id, 0, out _));
        }

        [Test]
        public void Match_Attack_StealingTwentiethSoul_Awakens()
        {
            Match match = NewMatch(4);
            ApostleState current = match.CurrentApostle;
            ApostleState target = Opponent(match);
            current.AddSouls(GameConstants.SoulsToAwaken - 1 - current.Souls);
            PlaceNextTo(match, target, current);

            match.Attack(target.Id, 5, out _);

            Assert.AreEqual(GameConstants.SoulsToAwaken, current.Souls);
            Assert.AreEqual(current.Id, match.Ascension.AwakenedId);
        }

        [Test]
        public void Match_Attack_EliminatingLastOpponent_WinsMatch()
        {
            Match match = NewMatch(2);
            ApostleState current = match.CurrentApostle;
            ApostleState target = Opponent(match);
            target.TakeDamage(GameConstants.StartingVessel - 3);
            PlaceNextTo(match, target, current);

            AttackResult result = match.Attack(target.Id, 10, out _);

            Assert.AreEqual(AttackResult.Resolved, result);
            Assert.IsTrue(match.IsOver);
            Assert.AreEqual(current.Id, match.WinnerId);
            Assert.AreEqual(MatchEndReason.LastApostleStanding, match.EndReason);
            Assert.IsNull(match.CurrentApostle);
        }

        [Test]
        public void Match_Shield_LastsUntilStartOfOwnersNextTurn()
        {
            Match match = NewMatch(4);
            ApostleState owner = match.CurrentApostle;
            owner.AddShield(4);

            match.EndTurn();
            Assert.AreNotSame(owner, match.CurrentApostle);
            Assert.AreEqual(4, owner.Shield, "Other Apostles' turns must not clear it");

            for (int i = 0; i < 10 && match.CurrentApostle != owner; i++)
                match.EndTurn();

            Assert.AreSame(owner, match.CurrentApostle);
            Assert.AreEqual(0, owner.Shield);
        }

        [Test]
        public void Match_GetAttackTargets_ListsAdjacentOpponent()
        {
            Match match = NewMatch(4);
            ApostleState target = Opponent(match);
            PlaceNextTo(match, target, match.CurrentApostle);

            var results = new List<ApostleState>();
            match.GetAttackTargets(results);

            Assert.AreEqual(1, results.Count);
            Assert.AreSame(target, results[0]);
        }
    }
}