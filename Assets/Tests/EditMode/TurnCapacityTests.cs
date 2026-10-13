using System;
using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests.EditMode
{
    public class TurnCapacityTests
    {
        [Test]
        public void NewCapacity_StartsFull()
        {
            var capacity = new TurnCapacity(8);
            Assert.AreEqual(8, capacity.Maximum);
            Assert.AreEqual(8, capacity.Remaining);
        }

        [Test]
        public void Spend_ReducesRemaining()
        {
            var capacity = new TurnCapacity(8);
            capacity.Spend(2);
            Assert.AreEqual(6, capacity.Remaining);
            Assert.AreEqual(8, capacity.Maximum);
        }

        [Test]
        public void Spend_ZeroCost_IsAllowed()
        {
            var capacity = new TurnCapacity(8);
            capacity.Spend(0);
            Assert.AreEqual(8, capacity.Remaining);
        }

        [Test]
        public void Spend_ExactRemaining_LeavesZero()
        {
            var capacity = new TurnCapacity(4);
            capacity.Spend(4);

            Assert.AreEqual(0, capacity.Remaining);
            Assert.IsFalse(capacity.CanSpend(1));
            Assert.IsTrue(capacity.CanSpend(0));
        }

        [Test]
        public void CanSpend_FalseWhenInsufficient()
        {
            var capacity = new TurnCapacity(3);
            Assert.IsTrue(capacity.CanSpend(3));
            Assert.IsFalse(capacity.CanSpend(4));
        }

        [Test]
        public void Spend_MoreThanRemaining_ThrowsAndChangesNothing()
        {
            var capacity = new TurnCapacity(8);
            capacity.Spend(6);

            Assert.Throws<InvalidOperationException>(() => capacity.Spend(3));
            Assert.AreEqual(2, capacity.Remaining);
        }

        [Test]
        public void NegativeValues_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TurnCapacity(-1));

            var capacity = new TurnCapacity(8);
            Assert.Throws<ArgumentOutOfRangeException>(() => capacity.Spend(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => capacity.CanSpend(-1));
        }
    }
}