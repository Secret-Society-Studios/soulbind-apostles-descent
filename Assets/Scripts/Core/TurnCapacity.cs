using System;

namespace SoulBind.Core
{
    /// <summary>
    /// The Capacity budget for one Apostle's turn. Unspent Capacity is lost
    /// when the turn ends, so a new TurnCapacity is created for each turn.
    /// </summary>
    public sealed class TurnCapacity
    {
        public int Maximum { get; }
        public int Remaining { get; private set; }

        public TurnCapacity(int maximum)
        {
            if (maximum < 0)
                throw new ArgumentOutOfRangeException(nameof(maximum), "Capacity cannot be negative.");

            Maximum = maximum;
            Remaining = maximum;
        }

        public bool CanSpend(int amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "Cost cannot be negative.");

            return amount <= Remaining;
        }

        /// <summary>Spends Capacity. Throws, and changes nothing, if there isn't enough.</summary>
        public void Spend(int amount)
        {
            if (!CanSpend(amount))
                throw new InvalidOperationException(
                    $"Cannot spend {amount} Capacity; only {Remaining} remaining.");

            Remaining -= amount;
        }

        /// <summary>For snapshots (online play): sets what is left, as the host reported it.</summary>
        internal void RestoreRemaining(int remaining) => Remaining = Math.Max(0, remaining);
    }
}