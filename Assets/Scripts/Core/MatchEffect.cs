namespace SoulBind.Core
{
    /// <summary>What a screen effect shows. New kinds go at the end, so network values never shift.</summary>
    public enum MatchEffectKind : byte
    {
        /// <summary>A card strengthened the Apostle who played it: healing or a lasting bonus.</summary>
        Buff,

        /// <summary>An opponent's card put a lasting penalty on this Apostle (The Tower).</summary>
        Debuff,

        /// <summary>An opponent's card gave this Apostle Corruption.</summary>
        Corrupt,

        /// <summary>This Apostle picked up a Heal tile.</summary>
        HealTile,

        /// <summary>This Apostle picked up a Soul tile.</summary>
        SoulTile,

        /// <summary>This Apostle picked up a Purify tile.</summary>
        PurifyTile
    }

    /// <summary>One thing that happened to one Apostle, for the screen to show. Rules never read these.</summary>
    public readonly struct MatchEffect
    {
        public readonly MatchEffectKind Kind;
        public readonly int ApostleId;

        public MatchEffect(MatchEffectKind kind, int apostleId)
        {
            Kind = kind;
            ApostleId = apostleId;
        }
    }
}
