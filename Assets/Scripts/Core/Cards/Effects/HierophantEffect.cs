namespace SoulBind.Core
{
    /// <summary>
    /// The Hierophant: "Increase ATK and DEF by 2 for the next round."
    /// Team ruling: applies on the turn it is played. +2 DEF until the start of the player's
    /// next turn, and +2 ATK to an attack made this turn.
    /// </summary>
    public sealed class HierophantEffect : CardEffect
    {
        public const int Bonus = 2;
        public const int DurationTurns = 1;

        public override void OnPlayed(CardPlayContext context)
        {
            context.Player.AddTimedEffect(TimedEffectKind.Attack, Bonus, DurationTurns);
            context.Player.AddTimedEffect(TimedEffectKind.Defense, Bonus, DurationTurns);
        }
    }
}