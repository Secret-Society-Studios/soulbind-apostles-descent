namespace SoulBind.Core
{
    /// <summary>
    /// The World: "Stop all opponents' turns; go to the next round."
    /// The player's own turn carries on as normal. When it ends, everyone still waiting in the
    /// current round is passed over and the next round starts (or the Phase ends, if this was its
    /// last round).
    /// </summary>
    public sealed class WorldEffect : CardEffect
    {
        public override void OnPlayed(CardPlayContext context)
        {
            context.SkipRestOfRound();
        }
    }
}