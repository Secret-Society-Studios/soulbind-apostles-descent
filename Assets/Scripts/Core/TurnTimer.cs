namespace SoulBind.Core
{
    /// <summary>
    /// Counts down the time an Apostle has to act: their turn, or choosing the next Phase's direction.
    /// A new turn restarts the count. When it runs out, Tick says so once, and the host ends the turn
    /// (or keeps the direction). Plain numbers, so the host and every player count the same way.
    /// </summary>
    public sealed class TurnTimer
    {
        public const float MinSeconds = 30f;
        public const float MaxSeconds = 45f;
        public const float DefaultSeconds = 45f;
        public const int NoTurn = 0;            // the key while nobody acts (picking tiles, match over)
        private const int KeysPerRound = 8;     // more than MaxPlayers, so every seat gets its own key

        private int _turnKey = NoTurn;
        private bool _expired;

        /// <summary>The seconds each turn gets, between MinSeconds and MaxSeconds.</summary>
        public float Limit { get; }

        /// <summary>Seconds left in this turn (0 once it ran out).</summary>
        public float Remaining { get; private set; }

        /// <summary>True while someone is acting and their time has not run out yet.</summary>
        public bool IsRunning => _turnKey != NoTurn && !_expired;

        public TurnTimer(float seconds = DefaultSeconds)
        {
            Limit = ClampSeconds(seconds);
            Remaining = Limit;
        }

        // A turn timer outside 30 to 45 seconds is pulled back into that range.
        public static float ClampSeconds(float seconds) =>
            seconds < MinSeconds ? MinSeconds : seconds > MaxSeconds ? MaxSeconds : seconds;

        /// <summary>
        /// A number that changes whenever someone new must act: each turn (round and Apostle) and each
        /// direction choice between Phases. NoTurn while nobody acts.
        /// </summary>
        public static int GetTurnKey(Match match)
        {
            if (match == null || match.IsOver)
                return NoTurn;

            int round = match.Turns.RoundNumber;
            if (match.IsAwaitingPhaseStart)
                return -(round * KeysPerRound + match.GetNextPhaseChooserId());
            if (match.CurrentApostle == null)
                return NoTurn;
            return round * KeysPerRound + match.CurrentApostle.Id;
        }

        /// <summary>
        /// Moves the clock on by deltaSeconds. A key that differs from the last one restarts the count.
        /// Returns true once, on the tick the time runs out.
        /// </summary>
        public bool Tick(int turnKey, float deltaSeconds)
        {
            if (turnKey != _turnKey)
            {
                _turnKey = turnKey;
                _expired = false;
                Remaining = Limit;
            }
            if (turnKey == NoTurn || _expired)
                return false;

            if (deltaSeconds > 0f)
                Remaining -= deltaSeconds;
            if (Remaining > 0f)
                return false;

            Remaining = 0f;
            _expired = true;
            return true;
        }
    }
}
