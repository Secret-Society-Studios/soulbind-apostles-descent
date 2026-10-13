using System;
using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests.EditMode
{
    public class PhaseRulesTests
    {
        [TestCase(1, 1)]
        [TestCase(3, 1)]
        [TestCase(4, 2)]
        [TestCase(6, 2)]
        [TestCase(7, 3)]
        [TestCase(9, 3)]
        [TestCase(10, 4)]
        public void GetPhase_FollowsThreeRoundsPerPhase(int round, int expectedPhase)
        {
            Assert.AreEqual(expectedPhase, PhaseRules.GetPhase(round));
        }

        [Test]
        public void GetPhase_InvalidRound_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PhaseRules.GetPhase(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => PhaseRules.GetPhase(-1));
        }

        [TestCase(1, 8)]
        [TestCase(2, 12)]
        [TestCase(3, 12)]
        [TestCase(4, 15)]
        [TestCase(10, 15)]
        public void GetBaseCapacity_MatchesRulebook(int phase, int expectedCapacity)
        {
            Assert.AreEqual(expectedCapacity, PhaseRules.GetBaseCapacity(phase));
        }

        [Test]
        public void GetBaseCapacity_InvalidPhase_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PhaseRules.GetBaseCapacity(0));
        }

        [TestCase(1, 8)]
        [TestCase(3, 8)]
        [TestCase(4, 12)]
        [TestCase(7, 12)]
        [TestCase(10, 15)]
        public void GetBaseCapacityForRound_CombinesPhaseAndCapacity(int round, int expectedCapacity)
        {
            Assert.AreEqual(expectedCapacity, PhaseRules.GetBaseCapacityForRound(round));
        }
    }
}
