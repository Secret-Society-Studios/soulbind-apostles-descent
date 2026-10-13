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
        WrongTarget
    }
}