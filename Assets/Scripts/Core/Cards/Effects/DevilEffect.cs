namespace SoulBind.Core
{
    /// <summary>
    /// The Devil: "Opponent gains +3 Corruption; you gain +1."
    /// Only as part of an attack: the target gains 3 and the player gains 1 when the card is played.
    /// Played without attacking, it does nothing beyond its DEF.
    /// </summary>
    public sealed class DevilEffect : CardEffect
    {
        public const int TargetCorruption = 3;
        public const int PlayerCorruption = 1;

        public override void OnPlayed(CardPlayContext context)
        {
            ApostleState target = context.Target;
            if (target == null)
                return;

            target.AddCorruption(TargetCorruption);
            context.Player.AddCorruption(PlayerCorruption);
        }
    }
}