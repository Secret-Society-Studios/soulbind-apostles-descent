namespace SoulBind.Core
{
    /// <summary>
    /// The Moon: "If an opponent fails to break your DEF, they gain +2 Corruption."
    /// Lasts as long as the card's shield: until the start of the player's next turn.
    /// Combat applies it when an attack does 0 damage.
    /// </summary>
    public sealed class MoonEffect : CardEffect
    {
        public const int CorruptionForFailedAttack = 2;
        public const int DurationTurns = 1;

        // For a few turns, an attack on the player that fails corrupts the attacker.
        public override void OnPlayed(CardPlayContext context)
        {
            context.Player.AddTimedEffect(TimedEffectKind.FailedAttackCorruption,
                CorruptionForFailedAttack, DurationTurns);
        }
    }
}
