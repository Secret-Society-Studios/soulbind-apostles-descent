namespace SoulBind.Core
{
    /// <summary>
    /// The engage-once flow of a turn. Free: move and play any card. Engaged: build up one attack
    /// on one target, no moving. Disengaged: the attack is done, no more cards this turn.
    /// </summary>
    public enum EngagementState : byte
    {
        Free,
        Engaged,
        Disengaged
    }
}