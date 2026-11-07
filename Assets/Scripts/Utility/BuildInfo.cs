using UnityEngine;

namespace SoulBind.Utility
{
    /// <summary>
    /// Which build this is, for online play and patches. Two players can only share a match when their
    /// ProtocolVersion is the same: the host turns away anyone else with a clear message, instead of
    /// letting a mismatched game fall out of step halfway through.
    /// </summary>
    public static class BuildInfo
    {
        /// <summary>
        /// Raise this by one whenever anything players send each other changes: commands, snapshots,
        /// card rules or numbers, chat ids. A patch that only changes art or menus can keep it.
        /// </summary>
        public const int ProtocolVersion = 5;

        /// <summary>The version typed in Player Settings, as shown on the menus ("1.0.3").</summary>
        public static string GameVersion => Application.version;

        /// <summary>What the host tells a player whose game does not match.</summary>
        public static string MismatchMessage(int theirs) =>
            theirs < ProtocolVersion
                ? "Your game is out of date. Update it to play with this host."
                : "The host's game is out of date. Ask them to update.";
    }
}
