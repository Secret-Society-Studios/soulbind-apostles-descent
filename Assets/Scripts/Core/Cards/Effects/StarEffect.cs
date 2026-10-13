namespace SoulBind.Core
{
    /// <summary>
    /// The Star: "Heal 2 Vessel and increase DEF by 4 for 2 rounds."
    /// The +4 DEF is on top of the shield and lasts until the start of the player's turn
    /// two rounds later. The card's own 6 DEF goes on the shield as normal.
    /// </summary>
    public sealed class StarEffect : CardEffect
    {
        public const int HealAmount = 2;
        public const int DefenseBonus = 4;
        public const int DurationRounds = 2;

        public override void OnPlayed(CardPlayContext context)
        {
            context.Player.Heal(HealAmount);
            context.Player.AddTimedEffect(TimedEffectKind.Defense, DefenseBonus, DurationRounds);
        }
    }
}