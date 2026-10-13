namespace SoulBind.Core
{
    /// <summary>
    /// The Tower: "Decrease each opponent's Capacity by 2 for 1 round."
    /// [Assumed] Every living opponent's next turn starts with 2 less Capacity.
    /// </summary>
    public sealed class TowerEffect : CardEffect
    {
        public const int CapacityLoss = 2;
        public const int DurationTurns = 1;
        public const int DelayTurns = 1;

        public override void OnPlayed(CardPlayContext context)
        {
            for (int i = 0; i < context.Apostles.Count; i++)
            {
                ApostleState other = context.Apostles[i];
                if (other != context.Player && !other.IsEliminated)
                    other.AddTimedEffect(TimedEffectKind.CapacityPenalty, CapacityLoss, DurationTurns, DelayTurns);
            }
        }
    }
}