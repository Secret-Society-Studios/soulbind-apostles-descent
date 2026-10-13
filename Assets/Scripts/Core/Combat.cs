using System;

namespace SoulBind.Core
{
    /// <summary>
    /// Resolves one attack between two Apostles. It does not check range, turns, Capacity
    /// or the engage-once rule; the caller (Match) does that.
    /// Pure rules code: no Unity types.
    /// </summary>
    public static class Combat
    {
        /// <summary>Damage = total ATK minus the target's DEF, minimum 0.</summary>
        public static int GetDamage(int totalAttack, int targetDefense) => Math.Max(0, totalAttack - targetDefense);

        /// <summary>
        /// Applies one attack against the target's DEF (shield plus timed DEF). On a hit (damage above 0):
        /// 1. the target takes the damage;
        /// 2. unless the attacker is Soullocked, 1 Soul moves from the target to the attacker;
        /// 3. the attacker gains +1 Corruption.
        /// Soullock is checked before step 3, so an attacker at 4 Corruption still steals.
        /// On a failed attack (0 damage) nothing happens, except that a target protected by
        /// The Moon gives the attacker its Corruption. DEF is never used up.
        /// </summary>
        public static AttackOutcome Resolve(ApostleState attacker, ApostleState target, int totalAttack)
        {
            if (attacker == null)
                throw new ArgumentNullException(nameof(attacker));
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (attacker == target)
                throw new ArgumentException("An Apostle cannot attack itself.", nameof(target));
            if (totalAttack < 1)
                throw new ArgumentOutOfRangeException(nameof(totalAttack), "An attack needs ATK above 0.");
            if (attacker.IsEliminated)
                throw new InvalidOperationException("An eliminated Apostle cannot attack.");
            if (target.IsEliminated)
                throw new InvalidOperationException("An eliminated Apostle cannot be attacked.");

            int damage = GetDamage(totalAttack, target.Defense);
            if (damage == 0)
            {
                int punishment = target.GetTimedTotal(TimedEffectKind.FailedAttackCorruption);
                if (punishment > 0)
                    attacker.AddCorruption(punishment);
                return new AttackOutcome(0, 0, punishment);
            }

            target.TakeDamage(damage);

            int stolen = 0;
            if (!attacker.IsSoullocked)
            {
                stolen = target.LoseSouls(GameConstants.SoulsStolenPerAttack);
                attacker.AddSouls(stolen);
            }

            attacker.AddCorruption(GameConstants.AttackCorruption);
            return new AttackOutcome(damage, stolen, GameConstants.AttackCorruption);
        }
    }
}