namespace SoulBind.Core
{
    /// <summary>
    /// The World: "Every other Apostle loses their turn."
    /// The player's own turn carries on as normal. When it ends, every other Apostle's turn is passed
    /// over (no start-of-turn effects, no draw) until it is the player's turn again, in the next round.
    /// If the Phase ends on the way (The World played in a Phase's last round), the Phase ends as normal.
    /// </summary>
    public sealed class WorldEffect : CardEffect
    {
        public override void OnPlayed(CardPlayContext context)
        {
            context.SkipRestOfRound();
        }
    }
}