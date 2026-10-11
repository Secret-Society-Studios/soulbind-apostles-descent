namespace SoulBind.Core
{
    /// <summary>What happened when a turn ended.</summary>
    public enum TurnEvent : byte
    {
        NextTurn,   // another Apostle's turn in the same round
        NewRound,   // everyone has played; the next round begins
        PhaseEnded  // the Phase is over; BeginNextPhase must be called
    }
}