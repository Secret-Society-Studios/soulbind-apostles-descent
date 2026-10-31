using System.Collections.Generic;

namespace SoulBind.Core
{
    // Online play: questions a player's screen can answer from its own copy of the match,
    // before sending a command to the host.
    public sealed partial class Match
    {
        /// <summary>Cards of this element played this turn, counting the ones waiting in the queue.</summary>
        public int GetElementsPlayedThisTurn(Element element)
        {
            int count = _elementsPlayedThisTurn[(int)element];
            for (int i = 0; i < _queuedCards.Count; i++)
            {
                if (CardCatalog.Get(_queuedCards[i]).Element == element)
                    count++;
            }
            return count;
        }
                
        /// <summary>The cards waiting in the queue, in order: an attack's while Engaged, plain plays while Free.</summary>
        public IReadOnlyList<ArcanaId> AttackCards => _queuedCards;

        /// <summary>
        /// The Capacity this Apostle's next turn would start with, as things stand now: the Phase's
        /// Capacity plus or minus running timed effects (The Hermit, The Tower). Shown off-turn.
        /// </summary>
        public int GetCapacityPreview(int apostleId)
        {
            int phase = PhaseRules.GetBaseCapacityForRound(Turns.RoundNumber);
            ApostleState apostle = TryFindApostle(apostleId);
            if (apostle == null)
                return phase;
            int change = apostle.GetTimedTotal(TimedEffectKind.MaxCapacity)
                       - apostle.GetTimedTotal(TimedEffectKind.CapacityPenalty);
            return System.Math.Max(0, phase + change);
        }

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
            return CardCatalog.Get(card).Element == Element.Nocturne
                && GetElementsPlayedThisTurn(Element.Nocturne) == ElementCombo.CardsNeeded - 1;
        }
    }
}