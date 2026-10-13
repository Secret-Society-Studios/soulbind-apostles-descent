namespace SoulBind.Core
{
    /// <summary>The outcome of trying to play a card. Nothing changes unless it is Success.</summary>
    public enum CardPlayResult : byte
    {
        Success,
        NotInHand,
        NotEnoughCapacity,
        Disengaged,
        CardHasNoAttack,
        InvalidTarget,
        TargetOutOfRange,
        WrongTarget,

        /// <summary>The card triggers the Nocturne combo while not attacking, and no opponent was picked.</summary>
        ComboTargetRequired,

        /// <summary>The picked Nocturne combo target is not a living opponent.</summary>
        InvalidComboTarget
    }
}