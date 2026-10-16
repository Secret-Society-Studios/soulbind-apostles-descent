using System;
using System.Collections.Generic;

namespace SoulBind.Core
{
    /// <summary>
    /// Tracks whose turn it is. Seat order is fixed at the start (random). Each round runs
    /// from the Phase's first Apostle in the chosen direction, skipping eliminated Apostles.
    /// Pure rules data: no Unity types, and no allocations while turns advance.
    /// </summary>
    public sealed class TurnTracker
    {
        private readonly int[] _seats;
        private readonly int[] _sequence = new int[GameConstants.MaxPlayers];
        private int _sequenceCount;
        private int _sequenceIndex;
        private int _startSeat;

        public int RoundNumber { get; private set; } = 1;
        public int Phase => PhaseRules.GetPhase(RoundNumber);
        public TurnDirection Direction { get; private set; } = TurnDirection.Forward;

        /// <summary>True after a Phase's last round, until BeginNextPhase is called.</summary>
        public bool IsAwaitingPhaseStart { get; private set; }

        public int CurrentApostleId
        {
            get
            {
                if (IsAwaitingPhaseStart)
                    throw new InvalidOperationException("The phase has ended; call BeginNextPhase first.");
                return _sequence[_sequenceIndex];
            }
        }

        /// <param name="seatOrder">Apostle ids in seating order. The first id plays first.</param>
        public TurnTracker(IReadOnlyList<int> seatOrder)
        {
            if (seatOrder == null)
                throw new ArgumentNullException(nameof(seatOrder));
            if (seatOrder.Count < GameConstants.MinPlayers || seatOrder.Count > GameConstants.MaxPlayers)
                throw new ArgumentException(
                    $"A match needs {GameConstants.MinPlayers} to {GameConstants.MaxPlayers} Apostles.", nameof(seatOrder));

            _seats = new int[seatOrder.Count];
            for (int i = 0; i < _seats.Length; i++)
            {
                for (int j = 0; j < i; j++)
                {
                    if (_seats[j] == seatOrder[i])
                        throw new ArgumentException("Apostle ids must be distinct.", nameof(seatOrder));
                }
                _seats[i] = seatOrder[i];
            }

            // Everyone is alive at the start, so round 1 simply follows seat order.
            for (int i = 0; i < _seats.Length; i++)
                _sequence[i] = _seats[i];
            _sequenceCount = _seats.Length;
        }

        /// <summary>Returns the ids 1..playerCount in a random order (Fisher-Yates shuffle).</summary>
        public static int[] CreateRandomSeatOrder(int playerCount, Random random)
        {
            if (playerCount < GameConstants.MinPlayers || playerCount > GameConstants.MaxPlayers)
                throw new ArgumentOutOfRangeException(nameof(playerCount), "Player count must be 2 to 6.");
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            var ids = new int[playerCount];
            for (int i = 0; i < playerCount; i++)
                ids[i] = i + 1;

            for (int i = playerCount - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int swap = ids[i];
                ids[i] = ids[j];
                ids[j] = swap;
            }
            return ids;
        }

        /// <summary>A fresh Capacity budget for the current round's Phase.</summary>
        public TurnCapacity CreateTurnCapacity() =>
            new TurnCapacity(PhaseRules.GetBaseCapacityForRound(RoundNumber));

        /// <summary>Ends the current turn and moves on to the next living Apostle, round or Phase end.</summary>
        public TurnEvent EndTurn(IReadOnlyList<ApostleState> apostles)
        {
            if (apostles == null)
                throw new ArgumentNullException(nameof(apostles));
            if (IsAwaitingPhaseStart)
                throw new InvalidOperationException("The phase has ended; call BeginNextPhase first.");

            for (int i = _sequenceIndex + 1; i < _sequenceCount; i++)
            {
                if (IsAlive(apostles, _sequence[i]))
                {
                    _sequenceIndex = i;
                    return TurnEvent.NextTurn;
                }
            }

            // Everyone has had a turn, so the round is over.
            if (RoundNumber % GameConstants.RoundsPerPhase == 0)
            {
                IsAwaitingPhaseStart = true;
                return TurnEvent.PhaseEnded;
            }

            RoundNumber++;
            BuildSequence(apostles);
            return TurnEvent.NewRound;
        }

        /// <summary>
        /// Starts the next Phase. The living Apostle with the lowest Vessel goes first,
        /// and the chosen direction decides how turns travel around the seats.
        /// </summary>
        public void BeginNextPhase(IReadOnlyList<ApostleState> apostles, TurnDirection direction)
        {
            if (apostles == null)
                throw new ArgumentNullException(nameof(apostles));
            if (!IsAwaitingPhaseStart)
                throw new InvalidOperationException("The current phase has not ended.");

            _startSeat = SeatOf(GetLowestVesselApostleId(apostles));
            Direction = direction;
            RoundNumber++;
            IsAwaitingPhaseStart = false;
            BuildSequence(apostles);
        }

        /// <summary>
        /// The living Apostle with the lowest Vessel, who picks the direction for the next Phase.
        /// A tie goes to the tied Apostle who sits earliest in the seating order (provisional rule).
        /// </summary>
        public int GetLowestVesselApostleId(IReadOnlyList<ApostleState> apostles)
        {
            if (apostles == null)
                throw new ArgumentNullException(nameof(apostles));

            int bestId = 0;
            int bestVessel = int.MaxValue;
            bool found = false;

            for (int i = 0; i < _seats.Length; i++)
            {
                ApostleState apostle = Find(apostles, _seats[i]);
                if (!apostle.IsEliminated && apostle.Vessel < bestVessel)
                {
                    bestVessel = apostle.Vessel;
                    bestId = apostle.Id;
                    found = true;
                }
            }

            if (!found)
                throw new InvalidOperationException("No living Apostles remain.");
            return bestId;
        }

        // ---- Snapshots (online play) ----

        internal IReadOnlyList<int> Seats => _seats;
        internal int SequenceCount => _sequenceCount;
        internal int SequenceIndex => _sequenceIndex;
        internal int StartSeat => _startSeat;
        internal int GetSequenceEntry(int index) => _sequence[index];

        /// <summary>Sets where the turn order stands, as the host reported it.</summary>
        internal void Restore(int roundNumber, TurnDirection direction, bool awaitingPhaseStart, int startSeat,
            IReadOnlyList<int> sequence, int sequenceIndex)
        {
            RoundNumber = roundNumber;
            Direction = direction;
            IsAwaitingPhaseStart = awaitingPhaseStart;
            _startSeat = startSeat;
            _sequenceCount = Math.Min(sequence.Count, _sequence.Length);
            for (int i = 0; i < _sequenceCount; i++)
                _sequence[i] = sequence[i];
            _sequenceIndex = sequenceIndex;
        }

        private void BuildSequence(IReadOnlyList<ApostleState> apostles)
        {
            int step = Direction == TurnDirection.Forward ? 1 : _seats.Length - 1;
            int seat = _startSeat;
            _sequenceCount = 0;

            for (int k = 0; k < _seats.Length; k++)
            {
                int id = _seats[seat];
                if (IsAlive(apostles, id))
                    _sequence[_sequenceCount++] = id;
                seat = (seat + step) % _seats.Length;
            }

            if (_sequenceCount == 0)
                throw new InvalidOperationException("No living Apostles remain.");
            _sequenceIndex = 0;
        }

        private int SeatOf(int apostleId)
        {
            for (int i = 0; i < _seats.Length; i++)
            {
                if (_seats[i] == apostleId)
                    return i;
            }
            throw new ArgumentException($"Apostle {apostleId} has no seat.", nameof(apostleId));
        }

        private static bool IsAlive(IReadOnlyList<ApostleState> apostles, int id) =>
            !Find(apostles, id).IsEliminated;

        private static ApostleState Find(IReadOnlyList<ApostleState> apostles, int id)
        {
            for (int i = 0; i < apostles.Count; i++)
            {
                if (apostles[i].Id == id)
                    return apostles[i];
            }
            throw new ArgumentException($"No Apostle with id {id} was provided.", nameof(apostles));
        }
    }
}