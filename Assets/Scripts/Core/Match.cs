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
        // Kept for decisions made during play (such as Ascension tile tie-breaks).
        private readonly Random _random;

        public Battlefield Battlefield { get; }
        public TurnTracker Turns { get; }

        /// <summary>Who is Awakened and where the Ascension tile is.</summary>
        public AscensionTracker Ascension { get; }

        /// <summary>The Apostle whose turn it is. Null while a Phase is waiting to start, or after the match ends.</summary>
        public ApostleState CurrentApostle { get; private set; }

        /// <summary>Capacity left this turn. Null whenever CurrentApostle is null.</summary>
        public TurnCapacity CurrentCapacity { get; private set; }

        public bool IsOver { get; private set; }

        /// <summary>Id of the winner, or 0 if the match ended with no living Apostle.</summary>
        public int WinnerId { get; private set; }

        /// <summary>Why the match ended. None while it is still running.</summary>
        public MatchEndReason EndReason { get; private set; }

        public bool IsAwaitingPhaseStart => Turns.IsAwaitingPhaseStart;

        private Match(Battlefield battlefield, TurnTracker turns, Random random)
        {
            Battlefield = battlefield;
            Turns = turns;
            _random = random;
            Ascension = new AscensionTracker();
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

            // The seat order must be the first use of the random source,
            // so the same seed always gives the same starting player.
            int[] seatOrder = TurnTracker.CreateRandomSeatOrder(playerCount, random);
            SpecialTiles specialTiles = SpecialTiles.CreateRandom(board, random);

            var battlefield = new Battlefield(board, specialTiles);
            int startingSouls = GameConstants.GetStartingSouls(playerCount);
            for (int i = 0; i < playerCount; i++)
                battlefield.AddApostle(new ApostleState(i + 1, spawnTiles[i], startingSouls));

            var turns = new TurnTracker(seatOrder);
            var match = new Match(battlefield, turns, random);
            match.StartTurn();
            return match;
        }

        /// <summary>
        /// Moves the current Apostle one tile. See MoveResult for the possible outcomes.
        /// Only the Awakened Apostle may step onto the Ascension tile.
        /// A successful move can Awaken an Apostle or win the match: check IsOver afterwards.
        /// </summary>
        public MoveResult Move(HexCoord destination)
        {
            RequireActiveTurn();

            if (IsAscensionTileClosedTo(CurrentApostle, destination)
                && HexCoord.AreAdjacent(CurrentApostle.Position, destination))
                return MoveResult.AscensionTileReserved;

            MoveResult result = Battlefield.TryMove(CurrentApostle, destination, CurrentCapacity);
            if (result == MoveResult.Success)
                UpdateGameOver();
            return result;
        }

        /// <summary>
        /// Fills results with the tiles the current Apostle can move to right now, leaving out
        /// the Ascension tile for anyone but the Awakened Apostle. Empty when nobody's turn is active.
        /// The caller supplies the list so it can be reused without allocating.
        /// </summary>
        public void GetLegalDestinations(List<HexCoord> results)
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));
            if (CurrentApostle == null)
            {
                results.Clear();
                return;
            }

            Battlefield.GetLegalDestinations(CurrentApostle, CurrentCapacity, results);
            for (int i = results.Count - 1; i >= 0; i--)
            {
                if (IsAscensionTileClosedTo(CurrentApostle, results[i]))
                    results.RemoveAt(i);
            }
        }

        
        /// <summary>
        /// The current Apostle attacks another Apostle with this total ATK.
        /// Only adjacent, living Apostles can be targeted. Capacity and the engage-once rule
        /// are handled by card play, not here. An attack can Awaken someone, eliminate someone
        /// or end the match: check IsOver afterwards.
        /// </summary>
        public AttackResult Attack(int targetId, int totalAttack, out AttackOutcome outcome)
        {
            RequireActiveTurn();
            if (totalAttack < 1)
                throw new ArgumentOutOfRangeException(nameof(totalAttack), "An attack needs ATK above 0.");

            outcome = default;
            ApostleState target = TryFindApostle(targetId);
            if (target == null)
                return AttackResult.TargetNotFound;
            if (target == CurrentApostle)
                return AttackResult.TargetIsSelf;
            if (target.IsEliminated)
                return AttackResult.TargetEliminated;
            if (!HexCoord.AreAdjacent(CurrentApostle.Position, target.Position))
                return AttackResult.OutOfRange;

            outcome = Combat.Resolve(CurrentApostle, target, totalAttack);
            UpdateGameOver();
            return AttackResult.Resolved;
        }

        /// <summary>
        /// Fills results with the Apostles the current Apostle can attack right now.
        /// Empty when nobody's turn is active. The caller supplies the list to avoid allocating.
        /// </summary>
        public void GetAttackTargets(List<ApostleState> results)
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));
            if (CurrentApostle == null)
            {
                results.Clear();
                return;
            }
            Battlefield.GetAttackTargets(CurrentApostle, results);
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
        /// Re-checks Awakening, Ascension and eliminations. Call this after anything that can
        /// change Souls or eliminate an Apostle (such as combat). Returns true if the match is over.
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
                apostle.ClearShield();
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

        // Updates Awakening, then checks both win conditions. Returns true if the match is over.
        private bool UpdateGameOver()
        {
            if (IsOver)
                return true;

            Ascension.Update(Battlefield, CurrentApostle, _random);

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

            if (living <= 1)
            {
                if (living == 1)
                    EndMatch(lastLivingId, MatchEndReason.LastApostleStanding);
                else
                    EndMatch(0, MatchEndReason.NoSurvivors);
                return true;
            }

            if (Ascension.HasAscended(Battlefield))
            {
                EndMatch(Ascension.AwakenedId, MatchEndReason.Ascension);
                return true;
            }

            return false;
        }

        // True if this tile is the Ascension tile and the Apostle is not the Awakened one.
        private bool IsAscensionTileClosedTo(ApostleState apostle, HexCoord tile) =>
            Ascension.HasAwakened && Ascension.Tile == tile && apostle.Id != Ascension.AwakenedId;

        private void EndMatch(int winnerId, MatchEndReason reason)
        {
            IsOver = true;
            WinnerId = winnerId;
            EndReason = reason;
            CurrentApostle = null;
            CurrentCapacity = null;
        }
        
        // Like FindApostle, but returns null instead of throwing (for ids that come from player input).
        private ApostleState TryFindApostle(int id)
        {
            IReadOnlyList<ApostleState> apostles = Battlefield.Apostles;
            for (int i = 0; i < apostles.Count; i++)
            {
                if (apostles[i].Id == id)
                    return apostles[i];
            }
            return null;
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