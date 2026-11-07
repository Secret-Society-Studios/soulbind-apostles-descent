using System;
using System.Collections.Generic;

namespace SoulBind.Core
{
    /// <summary>
    /// Divides a finished Strike between the cards that made it, so the screen can show them landing one
    /// by one. The rules still resolve one hit (see Match.Strike); this only decides what each card shows,
    /// and the shares always add up to what really happened. Pure rules code: no Unity types.
    /// </summary>
    public static class StrikeBreakdown
    {
        /// <summary>
        /// Shares out total in proportion to weights (each card's ATK), which also covers bonuses, The
        /// Emperor's doubling and a target's part of a divided attack. Leftover points go to the
        /// largest remainders, earlier cards first. If every weight is 0, the last card takes it all.
        /// </summary>
        public static int[] SplitAttack(int total, IReadOnlyList<int> weights)
        {
            if (weights == null)
                throw new ArgumentNullException(nameof(weights));
            if (total < 0)
                throw new ArgumentOutOfRangeException(nameof(total), "ATK cannot be negative.");

            var shares = new int[weights.Count];
            if (shares.Length == 0)
                return shares;

            int sum = 0;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] < 0)
                    throw new ArgumentOutOfRangeException(nameof(weights), "ATK cannot be negative.");
                sum += weights[i];
            }
            if (sum == 0)
            {
                shares[shares.Length - 1] = total;
                return shares;
            }

            var remainders = new int[shares.Length];
            int left = total;
            for (int i = 0; i < shares.Length; i++)
            {
                int part = total * weights[i];
                shares[i] = part / sum;
                remainders[i] = part % sum;
                left -= shares[i];
            }
            for (; left > 0; left--)
            {
                int best = 0;
                for (int i = 1; i < remainders.Length; i++)
                {
                    if (remainders[i] > remainders[best])
                        best = i;
                }
                remainders[best] = -1; // one point each
                shares[best]++;
            }
            return shares;
        }

        /// <summary>
        /// What each card's share deals, with DEF used up by the first cards. damage is the hit's real
        /// damage, so the result always adds up to it.
        /// </summary>
        public static int[] DamagePerCard(IReadOnlyList<int> shares, int damage)
        {
            if (shares == null)
                throw new ArgumentNullException(nameof(shares));

            int total = 0;
            for (int i = 0; i < shares.Count; i++)
                total += shares[i];

            int absorbed = Math.Max(0, total - damage);
            var result = new int[shares.Count];
            for (int i = 0; i < shares.Count; i++)
            {
                int blocked = Math.Min(absorbed, shares[i]);
                absorbed -= blocked;
                result[i] = shares[i] - blocked;
            }
            return result;
        }
    }
}
