using System;
using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests.EditMode
{
    public class GameConstantsTests
    {
        [Test] public void StartingVessel_Is50() => Assert.AreEqual(50, GameConstants.StartingVessel);
        [Test] public void SoulsToAwaken_Is20() => Assert.AreEqual(20, GameConstants.SoulsToAwaken);
        [Test] public void HandSizeLimits_Are5And7() =>
            Assert.AreEqual((5, 7), (GameConstants.StartingHandSize, GameConstants.MaxHandSize));
        [Test] public void MoveCost_Is2() => Assert.AreEqual(2, GameConstants.MoveCapacityCost);
        [Test] public void CorruptionThreshold_Is5() => Assert.AreEqual(5, GameConstants.CorruptionThreshold);

        [TestCase(2, 13)]
        [TestCase(3, 9)]
        [TestCase(4, 7)]
        [TestCase(5, 6)]
        [TestCase(6, 5)]
        public void GetStartingSouls_MatchesRulebook(int players, int expected)
        {
            Assert.AreEqual(expected, GameConstants.GetStartingSouls(players));
        }

        [Test]
        public void GetStartingSouls_InvalidPlayerCount_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => GameConstants.GetStartingSouls(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => GameConstants.GetStartingSouls(7));
        }
    }
}