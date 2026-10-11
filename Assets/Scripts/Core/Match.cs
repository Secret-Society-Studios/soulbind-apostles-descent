using System;
using System.Collections.Generic;

namespace SoulBind.Core
{
    /// <summary>
    /// One match: the battlefield, the turn order, and the turn flow that ties them together.
    /// Pure rules data: no Unity types. The UI and network layers call these methods and
    /// read the resulting state. They never change Apostle numbers on their own.
    /// </summary>
    public sealed class Match
    {
        public Battlefield Battlefield { get; }
        public TurnTracker Turns { get; }

        /// <summary>The Apostle whose turn it is. Null while a Phase is waiting to start, or after the match ends.</summary>
        public ApostleState CurrentApostle { get; private set; }

        /// <summary>Capacity left this turn. Null whenever CurrentApostle is null.</summary>
        public TurnCapacity CurrentCapacity { get; private set; }

        public bool IsOver { get; private set; }

        /// <summary>Id of the winner, or 0 if the match ended with no living Apostle.</summary>
        public int WinnerId { get; private set; }

        public bool IsAwaitingPhaseStart => Turns.IsAwaitingPhaseStart;

        private Match(Battlefield battlefield, TurnTracker turns)
        {
            Battlefield = battlefield;
            Turns = turns;
        }

        /// <summary>
        /// Creates a match. Apostle i+1 spawns on spawnTiles[i]. The random source is used
        /// first for the seat order, so the same seed gives the same starting player.
        /// </summary>
        public static Match Create(int playerCount, HexBoard board, IReadOnlyList<HexCoord> spawnTiles, Random random)
        {
            if (board == null)
                throw new ArgumentNullException(nameof(board));
            if (spawnTiles == null)
                throw new ArgumentNullException(nameof(spawnTiles));
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            if (playerCount < GameConstants.MinPlayers || playerCount > GameConstants.MaxPlayers)
                throw new ArgumentOutOfRangeException(nameof(playerCount), "Player count must be 2 to 6.");
            if (spawnTiles.Count != playerCount)
                throw new ArgumentException("There must be exactly one spawn tile per player.", nameof(spawnTiles));

            var battlefield = new Battlefield(board);
            int startingSouls = GameConstants.GetStartingSouls(playerCount);
            for (int i = 0; i < playerCount; i++)
                battlefield.AddApostle(new ApostleState(i + 1, spawnTiles[i], startingSouls));

            var turns = new TurnTracker(TurnTracker.CreateRandomSeatOrder(playerCount, random));
            var match = new Match(battlefield, turns);
            match.StartTurn();
            return match;
        }

        /// <summary>Moves the current Apostle one tile. See MoveResult for the possible outcomes.</summary>
        public MoveResult Move(HexCoord destination)
        {
            RequireActiveTurn();
            return Battlefield.TryMove(CurrentApostle, destination, CurrentCapacity);
        }

        /// <summary>
        /// Ends the current turn. The returned event says what the turn tracker did.
        /// Read CurrentApostle, IsAwaitingPhaseStart and IsOver afterwards for the resulting state,
        /// because Corruption damage can eliminate Apostles and pass the turn on again.
        /// </summary>
        public TurnEvent EndTurn()
        {
            RequireActiveTurn();

            TurnEvent turnEvent = Turns.EndTurn(Battlefield.Apostles);
            if (turnEvent == TurnEvent.PhaseEnded)
            {
                CurrentApostle = null;
                CurrentCapacity = null;
            }
            else
            {
                StartTurn();
            }
            return turnEvent;
        }

        /// <summary>The Apostle who chooses the turn direction when the next Phase begins.</summary>
        public int GetNextPhaseChooserId() => Turns.GetLowestVesselApostleId(Battlefield.Apostles);

        /// <summary>Starts the next Phase with the direction chosen by the lowest-Vessel Apostle.</summary>
        public void BeginNextPhase(TurnDirection direction)
        {
            if (UpdateGameOver())
                throw new InvalidOperationException("The match is over.");
            if (!Turns.IsAwaitingPhaseStart)
                throw new InvalidOperationException("The current phase has not ended.");

            Turns.BeginNextPhase(Battlefield.Apostles, direction);
            StartTurn();
        }

        /// <summary>
        /// Checks whether the match has ended. Call this after anything that can eliminate
        /// an Apostle (such as combat). Returns true if the match is over.
        /// </summary>
        public bool RefreshGameState() => UpdateGameOver();

        // Applies start-of-turn effects to the current Apostle. If Corruption damage
        // eliminates them, their turn passes on to the next living Apostle.
        private void StartTurn()
        {
            while (true)
            {
                if (UpdateGameOver())
                    return;

                ApostleState apostle = FindApostle(Turns.CurrentApostleId);
                apostle.ApplyCorruptionDamage();

                if (!apostle.IsEliminated)
                {
                    CurrentApostle = apostle;
                    CurrentCapacity = Turns.CreateTurnCapacity();
                    return;
                }

                if (UpdateGameOver())
                    return;

                if (Turns.EndTurn(Battlefield.Apostles) == TurnEvent.PhaseEnded)
                {
                    CurrentApostle = null;
                    CurrentCapacity = null;
                    return;
                }
            }
        }

        private void RequireActiveTurn()
        {
            if (UpdateGameOver())
                throw new InvalidOperationException("The match is over.");
            if (Turns.IsAwaitingPhaseStart)
                throw new InvalidOperationException("The phase has ended; call BeginNextPhase first.");
        }

        private bool UpdateGameOver()
        {
            if (IsOver)
                return true;

            IReadOnlyList<ApostleState> apostles = Battlefield.Apostles;
            int living = 0;
            int lastLivingId = 0;
            for (int i = 0; i < apostles.Count; i++)
            {
                if (!apostles[i].IsEliminated)
                {
                    living++;
                    lastLivingId = apostles[i].Id;
                }
            }

            if (living > 1)
                return false;

            IsOver = true;
            WinnerId = living == 1 ? lastLivingId : 0;
            CurrentApostle = null;
            CurrentCapacity = null;
            return true;
        }

        private ApostleState FindApostle(int id)
        {
            IReadOnlyList<ApostleState> apostles = Battlefield.Apostles;
            for (int i = 0; i < apostles.Count; i++)
            {
                if (apostles[i].Id == id)
                    return apostles[i];
            }
            throw new InvalidOperationException($"No Apostle with id {id}.");
        }
    }
}