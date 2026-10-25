using System;
using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests.EditMode
{
    public class ApostleStateTests
    {
        private static ApostleState Make(int souls = 10) =>
            new ApostleState(1, new HexCoord(0, 0), souls);

        [Test]
        public void NewApostle_HasStartingValues()
        {
            var position = new HexCoord(3, 4);
            var a = new ApostleState(7, position, 13);

            Assert.AreEqual(7, a.Id);
            Assert.AreEqual(position, a.Position);
            Assert.AreEqual(50, a.Vessel);
            Assert.AreEqual(13, a.Souls);
            Assert.AreEqual(0, a.Corruption);
            Assert.IsFalse(a.IsCorrupted);
            Assert.IsFalse(a.IsSoullocked);
            Assert.IsFalse(a.IsEliminated);
        }

        [Test]
        public void TakeDamage_ReducesVessel()
        {
            var a = Make();
            a.TakeDamage(12);
            Assert.AreEqual(38, a.Vessel);
        }

        [Test]
        public void TakeDamage_CannotGoBelowZero_AndEliminates()
        {
            var a = Make();
            a.TakeDamage(60);
            Assert.AreEqual(0, a.Vessel);
            Assert.IsTrue(a.IsEliminated);
        }

        [Test]
        public void Heal_RestoresUpToMaximum()
        {
            var a = Make();
            a.TakeDamage(10);

            Assert.AreEqual(3, a.Heal(3));
            Assert.AreEqual(43, a.Vessel);

            Assert.AreEqual(7, a.Heal(100));
            Assert.AreEqual(50, a.Vessel);
        }

        [Test]
        public void Heal_EliminatedApostle_DoesNothing()
        {
            var a = Make();
            a.TakeDamage(60);

            Assert.AreEqual(0, a.Heal(5));
            Assert.AreEqual(0, a.Vessel);
        }

        [Test]
        public void Corruption_AtThreshold_BecomesCorruptedAndSoullocked()
        {
            var a = Make();

            a.AddCorruption(4);
            Assert.IsFalse(a.IsCorrupted);
            Assert.IsFalse(a.IsSoullocked);

            a.AddCorruption(1);
            Assert.IsTrue(a.IsCorrupted);
            Assert.IsTrue(a.IsSoullocked);
        }

        [Test]
        public void ReduceCorruption_FloorsAtZero_AndCanClearCorrupted()
        {
            var a = Make();
            a.AddCorruption(5);

            a.ReduceCorruption(1);
            Assert.AreEqual(4, a.Corruption);
            Assert.IsFalse(a.IsCorrupted);

            a.ReduceCorruption(10);
            Assert.AreEqual(0, a.Corruption);
        }

        [Test]
        public void AddSouls_GainsSouls()
        {
            var a = Make();
            Assert.AreEqual(3, a.AddSouls(3));
            Assert.AreEqual(13, a.Souls);
        }

        [Test]
        public void AddSouls_StopsAtTheSoulsNeededToAwaken()
        {
            var a = Make();

            Assert.AreEqual(GameConstants.SoulsToAwaken - 10, a.AddSouls(50));
            Assert.AreEqual(GameConstants.SoulsToAwaken, a.Souls);
            Assert.IsTrue(a.HasMaxSouls);
            Assert.AreEqual(0, a.AddSouls(1), "Nothing more fits");
            Assert.AreEqual(GameConstants.SoulsToAwaken, a.Souls);
        }
        
        [Test]
        public void AddSouls_WhileSoullocked_GainsNothing()
        {
            var a = Make();
            a.AddCorruption(5);

            Assert.AreEqual(0, a.AddSouls(3));
            Assert.AreEqual(10, a.Souls);
        }

        [Test]
        public void LoseSouls_ReducesSouls_AndReturnsAmountLost()
        {
            var a = Make();
            Assert.AreEqual(4, a.LoseSouls(4));
            Assert.AreEqual(6, a.Souls);
            Assert.AreEqual(0, a.Corruption);
        }

        [Test]
        public void LoseSouls_CannotGoBelowZero()
        {
            var a = Make(3);
            Assert.AreEqual(3, a.LoseSouls(10));
            Assert.AreEqual(0, a.Souls);
        }

        [Test]
        public void LoseSouls_ReachingZero_GainsFiveCorruption()
        {
            var a = Make(3);
            a.LoseSouls(3);

            Assert.AreEqual(0, a.Souls);
            Assert.AreEqual(5, a.Corruption);
            Assert.IsTrue(a.IsCorrupted);
        }

        [Test]
        public void LoseSouls_WhenAlreadyAtZero_DoesNotGainCorruptionAgain()
        {
            var a = Make(1);
            a.LoseSouls(1);
            a.LoseSouls(1);

            Assert.AreEqual(5, a.Corruption);
        }

        [TestCase(0, 0)]
        [TestCase(4, 0)]
        [TestCase(5, 2)]
        [TestCase(9, 2)]
        [TestCase(10, 4)]
        [TestCase(15, 4)]
        public void CorruptionDamage_FollowsTiers(int corruption, int expectedDamage)
        {
            var a = Make();
            a.AddCorruption(corruption);
            Assert.AreEqual(expectedDamage, a.GetCorruptionDamage());
        }

        [Test]
        public void CorruptionDamage_DoublesAtZeroSouls()
        {
            var a = Make(0);

            a.AddCorruption(5);
            Assert.AreEqual(4, a.GetCorruptionDamage());

            a.AddCorruption(5);
            Assert.AreEqual(8, a.GetCorruptionDamage());
        }

        [Test]
        public void ApplyCorruptionDamage_ReducesVessel_AndReturnsDamage()
        {
            var a = Make();
            a.AddCorruption(5);

            Assert.AreEqual(2, a.ApplyCorruptionDamage());
            Assert.AreEqual(48, a.Vessel);
        }

        [Test]
        public void MoveTo_UpdatesPosition()
        {
            var a = Make();
            a.MoveTo(new HexCoord(2, -1));
            Assert.AreEqual(new HexCoord(2, -1), a.Position);
        }

        [Test]
        public void NegativeAmounts_Throw()
        {
            var a = Make();
            Assert.Throws<ArgumentOutOfRangeException>(() => a.TakeDamage(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => a.Heal(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => a.AddSouls(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => a.LoseSouls(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => a.AddCorruption(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => a.ReduceCorruption(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ApostleState(1, new HexCoord(0, 0), -1));
        }
    }
}