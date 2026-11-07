using SoulBind.Core;
using UnityEngine;

namespace SoulBind.Managers
{
    /// <summary>
    /// While a Strike plays out card by card, what the screen shows. The rules resolved the whole Strike
    /// at once, so anything that would give the result away asks here first: the Vessel bars read
    /// ShownVessel, and eliminations, banners and the end screen wait while Active is true.
    /// </summary>
    public static class StrikeDisplay
    {
        private const int IdSlots = GameConstants.MaxPlayers + 1; // ids start at 1

        private static readonly int[] Shown = new int[IdSlots];

        /// <summary>True while a Strike is playing out on screen.</summary>
        public static bool Active { get; private set; }

        // Clean on every Play, even with domain reload turned off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Active = false;

        /// <summary>Starts holding the result back from this Apostle's Vessel before the Strike.</summary>
        public static void Hold(int apostleId, int vessel)
        {
            Shown[apostleId] = vessel;
            Active = true;
        }

        /// <summary>One card's damage lands.</summary>
        public static void Deal(int apostleId, int damage) => Shown[apostleId] -= damage;

        /// <summary>The screen has caught up with the rules.</summary>
        public static void End() => Active = false;

        /// <summary>The Vessel to show: the real one, or what the cards so far have left.</summary>
        public static int ShownVessel(ApostleState apostle) =>
            Active ? Mathf.Max(apostle.Vessel, Shown[apostle.Id]) : apostle.Vessel;
    }
}
