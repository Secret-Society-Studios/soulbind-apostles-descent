using System;
using System.Collections.Generic;
using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests.EditMode
{
    /// <summary>DEF absorbs attacks and is used up by the ATK it absorbs.</summary>
    public class DefenseDepletionTests
    {
        private static ApostleState Attacker() => new ApostleState(1, new HexCoord(0, 0), 7);
        private static ApostleState Target() => new ApostleState(2, new HexCoord(1, 0), 7);

        [Test]
        public void Hit_SmallerThanTheShield_UsesUpThatMuchDef()
        {
            ApostleState target = Target();
            target.AddShield(3);

            AttackOutcome outcome = Combat.ResolveHit(Attacker(), target, 2);

            Assert.AreEqual(1, target.Shield);
            Assert.AreEqual(0, outcome.Damage);
            Assert.AreEqual(50, target.Vessel);
        }

        [Test]
        public void Hit_BiggerThanTheShield_EmptiesItAndTheRestIsDamage()
        {
            ApostleState target = Target();
            target.AddShield(3);

            AttackOutcome outcome = Combat.ResolveHit(Attacker(), target, 5);

            Assert.AreEqual(0, target.Shield);
            Assert.AreEqual(2, outcome.Damage);
            Assert.AreEqual(48, target.Vessel);
        }

        [Test]
        public void SecondHit_FacesTheReducedDef()
        {
            ApostleState attacker = Attacker();
            ApostleState target = Target();
            target.AddShield(4);

            Combat.ResolveHit(attacker, target, 6); // 4 absorbed, 2 damage
            AttackOutcome second = Combat.ResolveHit(attacker, target, 6);

            Assert.AreEqual(6, second.Damage);
            Assert.AreEqual(42, target.Vessel);
        }

        [Test]
        public void TimedDef_IsUsedAfterTheShield()
        {
            ApostleState target = Target();
            target.AddShield(2);
            target.AddTimedEffect(TimedEffectKind.Defense, 4, 2);

            AttackOutcome outcome = Combat.ResolveHit(Attacker(), target, 5);

            Assert.AreEqual(0, outcome.Damage);
            Assert.AreEqual(0, target.Shield);
            Assert.AreEqual(1, target.Defense, "2 from the shield, 3 from the timed DEF");
        }

        [Test]
        public void TimedDef_TheSoonestToExpireIsUsedFirst()
        {
            ApostleState target = Target();
            target.AddTimedEffect(TimedEffectKind.Defense, 3, 1); // ends next turn
            target.AddTimedEffect(TimedEffectKind.Defense, 3, 2); // lasts a turn longer

            target.AbsorbAttack(4);
            Assert.AreEqual(2, target.Defense);

            target.BeginTurn();
            Assert.AreEqual(2, target.Defense, "The longer one kept its remaining 2");
        }

        [Test]
        public void AbsorbAttack_Negative_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Target().AbsorbAttack(-1));
        }

        [Test]
        public void Match_Attack_UsesUpTheTargetsShield()
        {
            HexBoard board = StandardBoard.Create();
            Match match = Match.Create(2, board, SpawnPlanner.PickSpread(board, 2), new Random(1));
            IReadOnlyList<ApostleState> apostles = match.Battlefield.Apostles;
            ApostleState target = apostles[0] == match.CurrentApostle ? apostles[1] : apostles[0];
            IReadOnlyList<HexCoord> neighbors = board.GetNeighborsOnBoard(match.CurrentApostle.Position);
            foreach (HexCoord tile in neighbors)
            {
                if (!match.Battlefield.IsOccupied(tile))
                {
                    target.MoveTo(tile);
                    break;
                }
            }
            target.AddShield(3);

            match.Attack(target.Id, 2, out AttackOutcome outcome);

            Assert.AreEqual(1, target.Shield);
            Assert.AreEqual(0, outcome.Damage);
        }
    }
}