using System.Collections.Generic;

namespace SoulBind.Core
{
    /// <summary>
    /// The window a card effect gets onto the match while its card is being played.
    /// Match creates one and reuses it, so playing a card allocates nothing.
    /// </summary>
    public sealed class CardPlayContext
    {
        private readonly Match _match;

        internal CardPlayContext(Match match)
        {
            _match = match;
        }

        /// <summary>The Apostle who played the card.</summary>
        public ApostleState Player => _match.CurrentApostle;

        /// <summary>Every Apostle in the match, including eliminated ones (check IsEliminated).</summary>
        public IReadOnlyList<ApostleState> Apostles => _match.Battlefield.Apostles;

        /// <summary>True while the player is Engaged, so the card is part of the current attack.</summary>
        public bool IsAttacking => _match.Engagement == EngagementState.Engaged;

        /// <summary>The Apostle being attacked, or null when the card is not part of an attack.</summary>
        public ApostleState Target => _match.EngagedTarget;

        /// <summary>The opponent the player picked for this card (Wheel of Fortune), or null.</summary>
        public ApostleState PickedTarget => _match.PickedTarget;

        /// <summary>The current Phase (1, 2, 3 ...).</summary>
        public int Phase => _match.Turns.Phase;

        /// <summary>
        /// How many copies of a card were already part of the current attack before this one.
        /// 0 when not attacking.
        /// </summary>
        public int CountPlayedInAttack(ArcanaId card) => _match.CountInCurrentAttack(card);

        /// <summary>
        /// The Emperor only: when the attack resolves, its whole ATK doubles if the target has
        /// more Corruption than the player. Only valid while IsAttacking.
        /// </summary>
        public void ArmEmperor() => _match.ArmEmperor();

        /// <summary>Corruption-reducing cards already played this turn, not counting this one.</summary>
        public int CorruptionReducersPlayedThisTurn => _match.CorruptionReducersThisTurn;

        /// <summary>Adds ATK to the current attack. Only valid while IsAttacking.</summary>
        public void AddToAttack(int amount) => _match.AddToPendingAttack(amount);

        /// <summary>
        /// When the current attack resolves and breaks DEF, a target whose Vessel was below
        /// this value is eliminated. Only valid while IsAttacking.
        /// </summary>
        public void ArmExecute(int vesselBelow) => _match.ArmExecute(vesselBelow);

        /// <summary>Temperance only: ends the player's turn as soon as the card has been played.</summary>
        public void EndTurnAfterThisCard() => _match.RequestEndTurn();

        /// <summary>The World only: when the player's turn ends, the rest of the current round is skipped.</summary>
        public void SkipRestOfRound() => _match.SkipRestOfRound();

        /// <summary>
        /// Swaps one random card from the player's hand with one random card from the other Apostle's
        /// hand. Returns false (and swaps nothing) if either hand is empty.
        /// </summary>
        public bool SwapRandomCardWith(ApostleState other) => _match.SwapRandomCardWith(other);

        /// <summary>Raises this turn's maximum Capacity (and what is left of it) by this amount.</summary>
        public void AddCapacityThisTurn(int amount) => _match.IncreaseCurrentCapacity(amount);

        /// <summary>Draws one card into the player's hand. Returns false if both piles are empty.</summary>
        public bool DrawCard() => _match.DrawCardForCurrent();

        /// <summary>Strength only: its doubling waits for another Corruption-reducing card this turn.</summary>
        public void MarkStrengthAwaitingPartner() => _match.MarkStrengthAwaitingPartner();
    }
}