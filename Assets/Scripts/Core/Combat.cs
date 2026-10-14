using System;

namespace SoulBind.Core
{
    /// <summary>
    /// Resolves attacks between Apostles. It does not check range, turns, Capacity
    /// or the engage-once rule; the caller (Match) does that.
    /// Pure rules code: no Unity types.
    /// </summary>
    public static class Combat
    {
        /// <summary>Damage = ATK minus DEF, minimum 0 (before any DEF is used up).</summary>
        public static int GetDamage(int totalAttack, int targetDefense) => Math.Max(0, totalAttack - targetDefense);

        /// <summary>
        /// A single-target attack: one hit (see ResolveHit) plus the attacker's +1 Corruption if it
        /// broke DEF. Used by Match.Attack; a Strike on several targets uses ResolveHit for each
        /// and adds the Corruption once.
        /// </summary>
        public static AttackOutcome Resolve(ApostleState attacker, ApostleState target, int totalAttack)
        {
            AttackOutcome hit = ResolveHit(attacker, target, totalAttack);
            if (!hit.BrokeDefense)
                return hit;

            attacker.AddCorruption(GameConstants.AttackCorruption);
            return new AttackOutcome(hit.Damage, hit.SoulsStolen, GameConstants.AttackCorruption);
        }

        /// <summary>
        /// One hit on one target. The target's DEF (shield, then timed DEF) absorbs the ATK first and is
        /// used up by what it absorbs; whatever gets through is the damage. On a hit (damage above 0):
        /// 1. the target takes the damage;
        /// 2. unless the attacker is Soullocked, 1 Soul moves from the target to the attacker.
        /// The attacker's +1 Corruption for attacking is NOT added here (see Resolve and Match.Strike).
        /// On a failed hit (0 damage) nothing happens, except that a target protected by The Moon
        /// gives the attacker its Corruption (reported in CorruptionGained).
        /// </summary>
        public static AttackOutcome ResolveHit(ApostleState attacker, ApostleState target, int attack)
        {
            if (attacker == null)
                throw new ArgumentNullException(nameof(attacker));
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (attacker == target)
                throw new ArgumentException("An Apostle cannot attack itself.", nameof(target));
            if (attack < 1)
                throw new ArgumentOutOfRangeException(nameof(attack), "An attack needs ATK above 0.");
            if (attacker.IsEliminated)
                throw new InvalidOperationException("An eliminated Apostle cannot attack.");
            if (target.IsEliminated)
                throw new InvalidOperationException("An eliminated Apostle cannot be attacked.");

            int damage = target.AbsorbAttack(attack);
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

            return new AttackOutcome(damage, stolen, 0);
        }

        /// <summary>
        /// One target's share of an attack divided between several targets: as even as possible,
        /// with leftover points going one each to the first targets (index 0 is the primary target).
        /// </summary>
        public static int GetShare(int totalAttack, int targetCount, int index)
        {
            if (targetCount < 1)
                throw new ArgumentOutOfRangeException(nameof(targetCount), "There must be at least one target.");
            if (index < 0 || index >= targetCount)
                throw new ArgumentOutOfRangeException(nameof(index));
            if (totalAttack < 0)
                throw new ArgumentOutOfRangeException(nameof(totalAttack), "ATK cannot be negative.");

            int share = totalAttack / targetCount;
            if (index < totalAttack % targetCount)
                share++;
            return share;
        }
    }
}