namespace SoulBind.Core
{
    /// <summary>
    /// Strength: "Reduce Corruption by 2; doubled if used with another Corruption-reducing card."
    /// If another reducer was already played this turn, it reduces 4 straight away. Otherwise it
    /// reduces 2, and Match adds the other 2 as soon as a reducer is played later in the same turn.
    /// </summary>
    public sealed class StrengthEffect : CardEffect
    {
        public const int BaseReduction = 2;

        public override bool ReducesCorruption => true;

        public override void OnPlayed(CardPlayContext context)
        {
            if (context.CorruptionReducersPlayedThisTurn > 0)
            {
                context.Player.ReduceCorruption(BaseReduction * 2);
            }
            else
            {
                context.Player.ReduceCorruption(BaseReduction);
                context.MarkStrengthAwaitingPartner();
            }
        }
    }
}