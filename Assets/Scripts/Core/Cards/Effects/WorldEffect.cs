namespace SoulBind.Core
{
    /// <summary>
    /// The World: "Stop all opponents' turns; go to the next round."
    /// Playing it ends the turn at once, exactly like End Turn (an attack in progress resolves first, and
    /// the player still draws). Every other Apostle's turn is passed over (no start-of-turn effects, no
    /// draw) and the player goes again in the next round, with a fresh Capacity.
    /// If that crosses into a new Phase, the Phase ends as normal and the direction is chosen as normal,
    /// but the player still goes first in it.
    /// </summary>
    public sealed class WorldEffect : CardEffect
    {
        // Ends the turn now and passes over everyone else's.
        public override void OnPlayed(CardPlayContext context)
        {
            context.SkipRestOfRound();
            context.EndTurnAfterThisCard();
        }
    }
}
