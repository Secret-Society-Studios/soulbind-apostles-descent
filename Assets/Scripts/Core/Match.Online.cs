namespace SoulBind.Core
{
    // Online play: questions a player's screen can answer from its own copy of the match,
    // before sending a command to the host.
    public sealed partial class Match
    {
        /// <summary>
        /// True if playing this card now (with this attack target, 0 = not attacking) triggers the
        /// Nocturne combo without anyone to hit, so the player must pick the combo's target first.
        /// PlayCard returns ComboTargetRequired in exactly these cases (when the card is otherwise playable).
        /// </summary>
        public bool NeedsComboTarget(ArcanaId card, int targetId)
        {
            if (IsOver || CurrentApostle == null || targetId != 0 || Engagement != EngagementState.Free)
                return false;
            return CardCatalog.Get(card).Element == Element.Nocturne
                   && _elementsPlayedThisTurn[(int)Element.Nocturne] == ElementCombo.CardsNeeded - 1;
        }
    }
}