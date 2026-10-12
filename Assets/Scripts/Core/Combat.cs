using System;

namespace SoulBind.Core
{
    /// <summary>
    /// Resolves one attack between two Apostles. It does not check range, turns, Capacity
    /// or the engage-once rule; the caller (Match, and later card play) does that.
    /// Pure rules code: no Unity types.
    /// </summary>
    public static class Combat
    {
        /// <summary>Damage = total ATK minus the target's Shield, minimum 0.</summary>
        public static int GetDamage(int totalAttack, int targetShield) => Math.Max(0, totalAttack - targetShield);

        /// <summary>
        /// Applies one attack. On a hit (damage above 0), in this order:
        /// 1. the target takes the damage;
        /// 2. unless the attacker is Soullocked, 1 Soul moves from the target to the attacker;
        /// 3. the attacker gains +1 Corruption.
        /// Soullock is checked before step 3, so an attacker at 4 Corruption still steals.
        /// A 0-damage attack changes nothing. The Shield is never used up.
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

            int damage = GetDamage(totalAttack, target.Shield);
            if (damage == 0)
                return new AttackOutcome(0, 0, 0);

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