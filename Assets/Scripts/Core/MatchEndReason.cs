namespace SoulBind.Core
{
    /// <summary>Why a match ended. None while the match is still running.</summary>
    public enum MatchEndReason : byte
    {
        None,
        LastApostleStanding,
        NoSurvivors,
        Ascension
    }
}