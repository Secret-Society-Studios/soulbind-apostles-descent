namespace SoulBind.Core
{
    /// <summary>
    /// Whether an attack could be made. Resolved means it happened, even if it did 0 damage;
    /// read the AttackOutcome for what it did. Every other value means nothing changed.
    /// </summary>
    public enum AttackResult : byte
    {
        Resolved,
        TargetNotFound,
        TargetIsSelf,
        TargetEliminated,
        OutOfRange
    }
}