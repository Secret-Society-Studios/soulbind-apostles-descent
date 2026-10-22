namespace SoulBind.Core
{
    /// <summary>
    /// Wheel of Fortune: "Swap a random card with any other Apostle."
    /// One random card from the player's hand is swapped with one random card from the other
    /// Apostle's hand (the match's seeded random picks both). The other Apostle can be anywhere on
    /// the board: the one the player picked, or the attack target when the card is part of an attack
    /// and nobody was picked. Nothing happens if either hand is empty.
    /// </summary>
    public sealed class WheelOfFortuneEffect : CardEffect
    {
        public override void OnPlayed(CardPlayContext context)
        {
            ApostleState other = context.PickedTarget ?? context.Target;
            if (other != null)
                context.SwapRandomCardWith(other);
        }
    }
}