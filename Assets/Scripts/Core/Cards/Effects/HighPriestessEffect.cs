namespace SoulBind.Core
{
    /// <summary>
    /// The High Priestess: "Draw an additional card."
    /// The card is drawn right away, on top of the normal draw at the end of the turn.
    /// </summary>
    public sealed class HighPriestessEffect : CardEffect
    {
        public override void OnPlayed(CardPlayContext context)
        {
            context.DrawCard();
        }
    }
}