namespace SoulBind.Core
{
    /// <summary>What a Strike did to one of its targets. Small and immutable.</summary>
    public readonly struct StrikeHit
    {
        public readonly int TargetId;

        /// <summary>This target's share of the attack's ATK (after The Emperor's doubling, if any).</summary>
        public readonly int Attack;

        /// <summary>The share minus the target's DEF, minimum 0.</summary>
        public readonly int Damage;

        public readonly int SoulsStolen;
        public readonly bool Eliminated;

        public StrikeHit(int targetId, int attack, int damage, int soulsStolen, bool eliminated)
        {
            TargetId = targetId;
            Attack = attack;
            Damage = damage;
            SoulsStolen = soulsStolen;
            Eliminated = eliminated;
        }

        public bool BrokeDefense => Damage > 0;
    }
}