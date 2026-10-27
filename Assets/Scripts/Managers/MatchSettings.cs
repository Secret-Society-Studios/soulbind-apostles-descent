using UnityEngine;

namespace SoulBind.Managers
{
    /// <summary>
    /// The choices made in the main menu, carried into the match scene. MatchController uses them
    /// instead of its Inspector values when they are set (when the game was started from the menu).
    /// Starting the match scene directly in the Editor keeps using the Inspector values.
    /// </summary>
    public static class MatchSettings
    {
        public static bool IsSet { get; private set; }
        public static int PlayerCount { get; private set; } = 4;
        public static bool BotsPlayOthers { get; private set; } = true;
        public static int LocalApostleId { get; private set; } = 1;
        public static bool ChooseSpawns { get; private set; } = true;

        /// <summary>True when the match is played online (the host's NetworkMatch assigns the seats).</summary>
        public static bool IsOnline { get; private set; }

        public static void Set(int playerCount, bool botsPlayOthers, bool chooseSpawns)
        {
            PlayerCount = Mathf.Clamp(playerCount, 2, 6);
            BotsPlayOthers = botsPlayOthers;
            LocalApostleId = 1;
            ChooseSpawns = chooseSpawns;
            IsOnline = false;
            IsSet = true;
        }

        /// <summary>
        /// An online match. On the host, playerCount is the number of seats (bots fill the ones nobody
        /// joined for). On a player who joined, it is ignored: the host sends the seat and the count.
        /// </summary>
        public static void SetOnline(int playerCount)
        {
            PlayerCount = Mathf.Clamp(playerCount, 2, 6);
            BotsPlayOthers = true;
            LocalApostleId = 1;
            ChooseSpawns = true; // picking starting tiles online comes later
            IsOnline = true;
            IsSet = true;
        }

        // Cleared whenever Play Mode starts, even with domain reload turned off, so pressing Play on the
        // match scene never picks up choices left over from an earlier run.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            IsSet = false;
            IsOnline = false;
        }
    }
}