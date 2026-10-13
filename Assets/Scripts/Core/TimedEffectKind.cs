namespace SoulBind.Core
{
    /// <summary>
    /// Kinds of effect that last for a number of the owner's turns. New kinds are added
    /// at the end as more cards are built, so saved and network values never shift.
    /// </summary>
    public enum TimedEffectKind : byte
    {
        /// <summary>Extra DEF on top of the shield (The Star).</summary>
        Defense,

        /// <summary>An attacker that fails to break this Apostle's DEF gains this much Corruption (The Moon).</summary>
        FailedAttackCorruption
    }
}