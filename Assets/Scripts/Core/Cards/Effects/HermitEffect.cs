namespace SoulBind.Core
{
    /// <summary>
    /// The Hermit: "Increase maximum Capacity by 2 for the next 2 rounds."
    /// Team ruling: this turn and the player's next turn. This turn's Capacity goes up right away;
    /// the timed effect adds the +2 when the next turn starts.
    /// </summary>
    public sealed class HermitEffect : CardEffect
    {
        public const int CapacityBonus = 2;
        public const int DurationTurns = 2;

        public override void OnPlayed(CardPlayContext context)
        {
            context.AddCapacityThisTurn(CapacityBonus);
            context.Player.AddTimedEffect(TimedEffectKind.MaxCapacity, CapacityBonus, DurationTurns);
        }
    }
}