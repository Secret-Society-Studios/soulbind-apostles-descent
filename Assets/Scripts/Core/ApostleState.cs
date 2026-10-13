using System;
using System.Collections.Generic;

namespace SoulBind.Core
{
    /// <summary>
    /// The numbers that describe one Apostle, and the rules that keep them valid.
    /// Pure rules data: no Unity types. Other systems (movement, combat, cards, turns)
    /// decide WHEN something happens; this class decides WHAT it does to the numbers.
    /// </summary>
    public sealed class ApostleState
    {
        private readonly List<TimedEffect> _timedEffects = new List<TimedEffect>(4);

        public int Id { get; }
        public HexCoord Position { get; private set; }
        public int Vessel { get; private set; }
        public int Souls { get; private set; }
        public int Corruption { get; private set; }

        /// <summary>
        /// DEF shield from played cards. Each attack's damage is reduced by it, and hits do not use it up.
        /// Shields stack, and the whole shield clears at the start of this Apostle's next turn.
        /// </summary>
        public int Shield { get; private set; }

        /// <summary>How many of its own turns this Apostle has started. Timed effects count these.</summary>
        public int TurnsStarted { get; private set; }

        /// <summary>Effects that last for a number of this Apostle's turns.</summary>
        public IReadOnlyList<TimedEffect> TimedEffects => _timedEffects;

        /// <summary>The DEF an attack has to beat: the shield plus any timed DEF bonus.</summary>
        public int Defense => Shield + GetTimedTotal(TimedEffectKind.Defense);

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

        /// <summary>Removes the whole shield.</summary>
        public void ClearShield() => Shield = 0;

        /// <summary>Lowers the shield, never below 0. Timed DEF is not touched.</summary>
        public void ReduceShield(int amount)
        {
            RequireNonNegative(amount, nameof(amount));
            Shield = Math.Max(0, Shield - amount);
        }

        /// <summary>
        /// Adds an effect that lasts for ownTurns of this Apostle's turns.
        /// With delayTurns 0 it is active right away (for the rest of the current turn, then the next
        /// ownTurns - 1 turns). With delayTurns 1 it starts at this Apostle's next turn instead.
        /// It ends at the start of the turn after its last one.
        /// </summary>
        public void AddTimedEffect(TimedEffectKind kind, int amount, int ownTurns, int delayTurns = 0)
        {
            RequireNonNegative(amount, nameof(amount));
            if (ownTurns < 1)
                throw new ArgumentOutOfRangeException(nameof(ownTurns), "An effect lasts at least 1 turn.");
            if (delayTurns < 0)
                throw new ArgumentOutOfRangeException(nameof(delayTurns), "A delay cannot be negative.");

            int starts = TurnsStarted + delayTurns;
            _timedEffects.Add(new TimedEffect(kind, amount, starts, starts + ownTurns));
        }

        /// <summary>The total amount of every timed effect of this kind that is active right now.</summary>
        public int GetTimedTotal(TimedEffectKind kind)
        {
            int total = 0;
            for (int i = 0; i < _timedEffects.Count; i++)
            {
                TimedEffect effect = _timedEffects[i];
                if (effect.Kind == kind && effect.IsActiveOn(TurnsStarted))
                    total += effect.Amount;
            }
            return total;
        }

        /// <summary>
        /// Start-of-turn bookkeeping: counts the turn, clears the shield and ends expired timed effects.
        /// Corruption damage is applied separately by the turn flow.
        /// </summary>
        public void BeginTurn()
        {
            TurnsStarted++;
            Shield = 0;
            for (int i = _timedEffects.Count - 1; i >= 0; i--)
            {
                if (_timedEffects[i].ExpiresOnTurn <= TurnsStarted)
                    _timedEffects.RemoveAt(i);
            }
        }

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