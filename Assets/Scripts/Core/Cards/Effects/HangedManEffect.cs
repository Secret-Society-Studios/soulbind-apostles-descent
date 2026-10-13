namespace SoulBind.Core
{
    /// <summary>
    /// The Hanged Man: "If you have the lowest Vessel, +5 ATK for 2 rounds."
    /// [Assumed] Checked when played; a tie for lowest counts. The +5 ATK is added to attacks
    /// this turn and the player's next turn (the same "2 rounds" as The Star).
    /// </summary>
    public sealed class HangedManEffect : CardEffect
    {
        public const int AttackBonus = 5;
        public const int DurationTurns = 2;

        public override void OnPlayed(CardPlayContext context)
        {
            if (HasLowestVessel(context))
                context.Player.AddTimedEffect(TimedEffectKind.Attack, AttackBonus, DurationTurns);
        }

        private static bool HasLowestVessel(CardPlayContext context)
        {
            int vessel = context.Player.Vessel;
            for (int i = 0; i < context.Apostles.Count; i++)
            {
                ApostleState other = context.Apostles[i];
                if (other != context.Player && !other.IsEliminated && other.Vessel < vessel)
                    return false;
            }
            return true;
        }
    }
}