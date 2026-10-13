using System;

namespace SoulBind.Core
{
    /// <summary>
    /// Phase and Capacity rules. A round is one turn for every Apostle,
    /// and a Phase is three rounds. Rounds and Phases both start at 1.
    /// "Base" Capacity excludes card effects such as The Hermit or The Tower.
    /// </summary>
    public static class PhaseRules
    {
        public static int GetPhase(int roundNumber)
        {
            if (roundNumber < 1)
                throw new ArgumentOutOfRangeException(nameof(roundNumber), "Rounds start at 1.");

            return (roundNumber - 1) / GameConstants.RoundsPerPhase + 1;
        }

        public static int GetBaseCapacity(int phase)
        {
            if (phase < 1)
                throw new ArgumentOutOfRangeException(nameof(phase), "Phases start at 1.");

            if (phase == 1)
                return GameConstants.Phase1Capacity;
            if (phase <= 3)
                return GameConstants.Phase2To3Capacity;
            return GameConstants.Phase4PlusCapacity;
        }

        public static int GetBaseCapacityForRound(int roundNumber) =>
            GetBaseCapacity(GetPhase(roundNumber));
    }
}