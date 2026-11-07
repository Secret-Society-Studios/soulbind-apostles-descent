using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests
{
    public class StrikeBreakdownTests
    {
        private static int Sum(int[] values)
        {
            int sum = 0;
            foreach (int value in values)
                sum += value;
            return sum;
        }

        [Test]
        public void SplitAttack_PrintedAtk_KeepsEachCardsAtk()
        {
            CollectionAssert.AreEqual(new[] { 4, 3, 5 }, StrikeBreakdown.SplitAttack(12, new[] { 4, 3, 5 }));
        }

        [Test]
        public void SplitAttack_WithABonus_StillAddsUp()
        {
            int[] shares = StrikeBreakdown.SplitAttack(17, new[] { 4, 3, 5 });

            Assert.AreEqual(17, Sum(shares));
        }

        [Test]
        public void SplitAttack_ADividedAttack_StillAddsUp()
        {
            int[] shares = StrikeBreakdown.SplitAttack(5, new[] { 4, 3, 5 });

            Assert.AreEqual(5, Sum(shares));
        }

        [Test]
        public void SplitAttack_ACardWithNoAtkGetsNone()
        {
            int[] shares = StrikeBreakdown.SplitAttack(9, new[] { 0, 3, 6 });

            Assert.AreEqual(0, shares[0]);
            Assert.AreEqual(9, Sum(shares));
        }

        [Test]
        public void SplitAttack_NoCardHasAtk_TheLastCardTakesTheBonus()
        {
            CollectionAssert.AreEqual(new[] { 0, 4 }, StrikeBreakdown.SplitAttack(4, new[] { 0, 0 }));
        }

        [Test]
        public void DamagePerCard_DefIsUsedUpByTheFirstCards()
        {
            // 12 ATK against 6 DEF: 6 damage, none of it from the first card.
            CollectionAssert.AreEqual(new[] { 0, 1, 5 }, StrikeBreakdown.DamagePerCard(new[] { 4, 3, 5 }, 6));
        }

        [Test]
        public void DamagePerCard_NoDef_EveryCardDealsItsShare()
        {
            CollectionAssert.AreEqual(new[] { 4, 3, 5 }, StrikeBreakdown.DamagePerCard(new[] { 4, 3, 5 }, 12));
        }

        [Test]
        public void DamagePerCard_AllBlocked_NoCardDealsAnything()
        {
            CollectionAssert.AreEqual(new[] { 0, 0 }, StrikeBreakdown.DamagePerCard(new[] { 4, 3 }, 0));
        }
    }
}
