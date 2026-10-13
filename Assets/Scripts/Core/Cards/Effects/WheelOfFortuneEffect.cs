namespace SoulBind.Core
{
    /// <summary>
    /// Wheel of Fortune: "Swap a random card with an opponent."
    /// Only as part of an attack: one random card from the player's hand is swapped with one
    /// random card from the target's hand (the match's seeded random picks both).
    /// Nothing happens if either hand is empty or the card is played without attacking.
    /// </summary>
    public sealed class WheelOfFortuneEffect : CardEffect
    {
        public override void OnPlayed(CardPlayContext context)
        {
            ApostleState target = context.Target;
            if (target != null)
                context.SwapRandomCardWith(target);
        }
    }
}