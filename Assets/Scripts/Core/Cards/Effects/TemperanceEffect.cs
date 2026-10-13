namespace SoulBind.Core
{
    /// <summary>
    /// Temperance: "Skip your current round; +5 ATK and DEF in the next round."
    /// Playing it ends the turn at once, exactly like End Turn (an attack in progress resolves
    /// first, and the player still draws). The player's next turn gets +5 DEF and +5 ATK on attacks.
    /// </summary>
    public sealed class TemperanceEffect : CardEffect
    {
        public const int Bonus = 5;
        public const int DurationTurns = 1;
        public const int DelayTurns = 1;

        public override void OnPlayed(CardPlayContext context)
        {
            context.Player.AddTimedEffect(TimedEffectKind.Attack, Bonus, DurationTurns, DelayTurns);
            context.Player.AddTimedEffect(TimedEffectKind.Defense, Bonus, DurationTurns, DelayTurns);
            context.EndTurnAfterThisCard();
        }
    }
}