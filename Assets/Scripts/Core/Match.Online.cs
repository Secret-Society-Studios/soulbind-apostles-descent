using System.Collections.Generic;

namespace SoulBind.Core
{
    // Online play: questions a player's screen can answer from its own copy of the match,
    // before sending a command to the host.
    public sealed partial class Match
    {
        /// <summary>How many cards of this element the current Apostle has played this turn.</summary>
        public int GetElementsPlayedThisTurn(Element element) => _elementsPlayedThisTurn[(int)element];
                
        /// <summary>The cards played into the attack in progress, in order (empty when not Engaged).</summary>
        public IReadOnlyList<ArcanaId> AttackCards => _attackCards;

        /// <summary>
        /// True if playing this card now (with this attack target, 0 = not attacking) needs the player
        /// to pick an opponent first: Wheel of Fortune played outside an attack (who to swap with), or
        /// the Nocturne combo triggered with nobody to hit. PlayCard returns ComboTargetRequired in
        /// exactly these cases (when the card is otherwise playable).
        /// </summary>
        public bool NeedsComboTarget(ArcanaId card, int targetId)
        {
            if (IsOver || CurrentApostle == null || targetId != 0 || Engagement != EngagementState.Free)
                return false;
            if (card == ArcanaId.WheelOfFortune)
                return true;
            return CardCatalog.Get(card).Element == Element.Nocturne && _elementsPlayedThisTurn[(int)Element.Nocturne] == ElementCombo.CardsNeeded - 1;
        }
    }
}