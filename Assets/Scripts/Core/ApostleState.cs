using System;

namespace SoulBind.Core
{
    /// <summary>
    /// The numbers that describe one Apostle, and the rules that keep them valid.
    /// Pure rules data: no Unity types. Other systems (movement, combat, cards, turns)
    /// decide WHEN something happens; this class decides WHAT it does to the numbers.
    /// </summary>
    public sealed class ApostleState
    {
        public int Id { get; }
        public HexCoord Position { get; private set; }
        public int Vessel { get; private set; }
        public int Souls { get; private set; }
        public int Corruption { get; private set; }

        
        /// <summary>
        /// DEF shield. Each attack's damage is reduced by it, and hits do not use it up.
        /// Shields stack, and the whole shield clears at the start of this Apostle's next turn.
        /// </summary>
        public int Shield { get; private set; }

        public bool IsEliminated => Vessel <= 0;
        public bool IsCorrupted => Corruption >= GameConstants.CorruptionThreshold;

        /// <summary>Corrupted Apostles cannot gain Souls until Corruption drops below the threshold.</summary>
        public bool IsSoullocked => IsCorrupted;

        public ApostleState(int id, HexCoord startPosition, int startingSouls)
        {
            if (startingSouls < 0)
                throw new ArgumentOutOfRangeException(nameof(startingSouls), "Souls cannot be negative.");

            Id = id;
            Position = startPosition;
            Vessel = GameConstants.StartingVessel;
            Souls = startingSouls;
            Corruption = 0;
        }

        /// <summary>Sets the position. Whether the move is legal is checked by the movement rules, not here.</summary>
        public void MoveTo(HexCoord destination) => Position = destination;

        public void TakeDamage(int amount)
        {
            RequireNonNegative(amount, nameof(amount));
            Vessel = Math.Max(0, Vessel - amount);
        }

        /// <summary>Heals up to the maximum Vessel. Returns how much was actually healed.</summary>
        public int Heal(int amount)
        {
            RequireNonNegative(amount, nameof(amount));
            if (IsEliminated)
                return 0;

            int healed = Math.Min(amount, GameConstants.MaxVessel - Vessel);
            Vessel += healed;
            return healed;
        }

        /// <summary>Gains Souls unless Soullocked. Returns how many Souls were actually gained.</summary>
        public int AddSouls(int amount)
        {
            RequireNonNegative(amount, nameof(amount));
            if (IsSoullocked)
                return 0;

            Souls += amount;
            return amount;
        }

        /// <summary>
        /// Loses Souls (never below 0). Returns how many were actually lost.
        /// Reaching 0 from a positive amount immediately adds Corruption.
        /// </summary>
        public int LoseSouls(int amount)
        {
            RequireNonNegative(amount, nameof(amount));

            int lost = Math.Min(amount, Souls);
            bool reachedZero = Souls > 0 && Souls - lost == 0;
            Souls -= lost;

            if (reachedZero)
                AddCorruption(GameConstants.ZeroSoulCorruption);

            return lost;
        }

        public void AddCorruption(int amount)
        {
            RequireNonNegative(amount, nameof(amount));
            Corruption += amount;
        }

        public void ReduceCorruption(int amount)
        {
            RequireNonNegative(amount, nameof(amount));
            Corruption = Math.Max(0, Corruption - amount);
        }

        
        /// <summary>Adds DEF to the shield. Shields stack.</summary>
        public void AddShield(int amount)
        {
            RequireNonNegative(amount, nameof(amount));
            Shield += amount;
        }

        /// <summary>Removes the whole shield. Called at the start of this Apostle's turn.</summary>
        public void ClearShield() => Shield = 0;

        /// <summary>Vessel damage this Apostle takes from Corruption at the start of its turn.</summary>
        public int GetCorruptionDamage()
        {
            int damage;
            if (Corruption >= GameConstants.SevereCorruptionThreshold)
                damage = GameConstants.SevereCorruptionDamage;
            else if (Corruption >= GameConstants.CorruptionThreshold)
                damage = GameConstants.CorruptedDamage;
            else
                damage = 0;

            if (Souls == 0)
                damage *= GameConstants.ZeroSoulDamageMultiplier;

            return damage;
        }

        /// <summary>Applies Corruption damage to Vessel and returns the damage dealt.</summary>
        public int ApplyCorruptionDamage()
        {
            int damage = GetCorruptionDamage();
            TakeDamage(damage);
            return damage;
        }

        private static void RequireNonNegative(int value, string paramName)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(paramName, "Amount cannot be negative.");
        }
    }
}