using System;
using System.Collections.Generic;

namespace SoulBind.Core
{
    /// <summary>
    /// The board plus the Apostles standing on it. Owns the rules for spawning and
    /// moving. Pure rules data: no Unity types, and no allocations in move checks.
    /// </summary>
    public sealed class Battlefield
    {
        private readonly List<ApostleState> _apostles = new List<ApostleState>(GameConstants.MaxPlayers);

        public HexBoard Board { get; }

        /// <summary>The special tiles still waiting to be used.</summary>
        public SpecialTiles Specials { get; }

        public IReadOnlyList<ApostleState> Apostles => _apostles;

        public Battlefield(HexBoard board, SpecialTiles specialTiles = null)
        {
            Board = board ?? throw new ArgumentNullException(nameof(board));
            Specials = specialTiles ?? new SpecialTiles();
        }

        /// <summary>The living Apostle on this tile, or null. Eliminated Apostles do not count.</summary>
        public ApostleState GetApostleAt(HexCoord tile)
        {
            for (int i = 0; i < _apostles.Count; i++)
            {
                ApostleState apostle = _apostles[i];
                if (!apostle.IsEliminated && apostle.Position == tile)
                    return apostle;
            }
            return null;
        }

        public bool IsOccupied(HexCoord tile) => GetApostleAt(tile) != null;

        /// <summary>Apostles may only spawn on a free edge tile.</summary>
        public bool CanSpawnAt(HexCoord tile) => Board.IsEdge(tile) && !IsOccupied(tile);

        /// <summary>Registers an Apostle at the position it was created with.</summary>
        public void AddApostle(ApostleState apostle)
        {
            if (apostle == null)
                throw new ArgumentNullException(nameof(apostle));
            if (_apostles.Count >= GameConstants.MaxPlayers)
                throw new InvalidOperationException($"A match has at most {GameConstants.MaxPlayers} Apostles.");

            for (int i = 0; i < _apostles.Count; i++)
            {
                if (_apostles[i].Id == apostle.Id)
                    throw new ArgumentException($"An Apostle with id {apostle.Id} already exists.", nameof(apostle));
            }

            if (!CanSpawnAt(apostle.Position))
                throw new ArgumentException(
                    $"{apostle.Position} is not a free edge tile, so an Apostle cannot spawn there.", nameof(apostle));

            _apostles.Add(apostle);
        }

        /// <summary>
        /// For snapshots (online play): registers an Apostle wherever it stands now, mid-match,
        /// without the spawn rules.
        /// </summary>
        internal void AddApostleFromSnapshot(ApostleState apostle)
        {
            if (apostle == null)
                throw new ArgumentNullException(nameof(apostle));
            _apostles.Add(apostle);
        }

        /// <summary>
        /// Moves an Apostle one tile and spends Capacity, or explains why it cannot.
        /// Nothing changes unless the result is Success. A successful move onto a
        /// special tile triggers it.
        /// </summary>
        public MoveResult TryMove(ApostleState apostle, HexCoord destination, TurnCapacity capacity)
        {
            RequireRegistered(apostle);
            if (capacity == null)
                throw new ArgumentNullException(nameof(capacity));

            if (apostle.IsEliminated)
                return MoveResult.ApostleEliminated;
            if (!Board.Contains(destination))
                return MoveResult.OffBoard;
            if (!HexCoord.AreAdjacent(apostle.Position, destination))
                return MoveResult.NotAdjacent;
            if (IsOccupied(destination))
                return MoveResult.TileOccupied;
            if (!capacity.CanSpend(GameConstants.MoveCapacityCost))
                return MoveResult.NotEnoughCapacity;

            capacity.Spend(GameConstants.MoveCapacityCost);
            apostle.MoveTo(destination);
            Specials.TryTrigger(apostle);
            return MoveResult.Success;
        }

        /// <summary>
        /// Fills <paramref name="results"/> with the tiles this Apostle can move to right now.
        /// The caller supplies the list so it can be reused without allocating.
        /// </summary>
        public void GetLegalDestinations(ApostleState apostle, TurnCapacity capacity, List<HexCoord> results)
        {
            RequireRegistered(apostle);
            if (capacity == null)
                throw new ArgumentNullException(nameof(capacity));
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            results.Clear();
            if (apostle.IsEliminated || !capacity.CanSpend(GameConstants.MoveCapacityCost))
                return;

            IReadOnlyList<HexCoord> neighbors = Board.GetNeighborsOnBoard(apostle.Position);
            for (int i = 0; i < neighbors.Count; i++)
            {
                if (!IsOccupied(neighbors[i]))
                    results.Add(neighbors[i]);
            }
        }

        private void RequireRegistered(ApostleState apostle)
        {
            if (apostle == null)
                throw new ArgumentNullException(nameof(apostle));
            if (!_apostles.Contains(apostle))
                throw new ArgumentException("That Apostle has not been added to this battlefield.", nameof(apostle));
        }
        
        /// <summary>
        /// Fills results with the living Apostles on the 6 tiles next to the attacker.
        /// The caller supplies the list so it can be reused without allocating.
        /// </summary>
        public void GetAttackTargets(ApostleState attacker, List<ApostleState> results)
        {
            RequireRegistered(attacker);
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            results.Clear();
            if (attacker.IsEliminated)
                return;

            for (int i = 0; i < _apostles.Count; i++)
            {
                ApostleState other = _apostles[i];
                if (other != attacker && !other.IsEliminated && HexCoord.AreAdjacent(attacker.Position, other.Position))
                    results.Add(other);
            }
        }
    }
}