namespace SoulBind.Core
{
    /// <summary>What one resolved attack did. Small and immutable, so it is cheap to pass around.</summary>
    public readonly struct AttackOutcome
    {
        /// <summary>ATK minus the target's Shield, minimum 0 (before Vessel is clamped at 0).</summary>
        public readonly int Damage;

        /// <summary>Souls moved from the target to the attacker (0 or 1).</summary>
        public readonly int SoulsStolen;

        /// <summary>Corruption the attacker gained from this attack.</summary>
        public readonly int CorruptionGained;

        public AttackOutcome(int damage, int soulsStolen, int corruptionGained)
        {
            Damage = damage;
            SoulsStolen = soulsStolen;
            CorruptionGained = corruptionGained;
        }

        /// <summary>False when the attack "failed to break DEF" (0 damage). Cards like The Moon react to this.</summary>
        public bool BrokeDefense => Damage > 0;
    }
}