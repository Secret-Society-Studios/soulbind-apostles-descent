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

        // Reused by GetReachableTiles, so the walk allocates nothing once they have grown.
        private readonly HashSet<HexCoord> _seen = new HashSet<HexCoord>();
        private readonly List<HexCoord> _ring = new List<HexCoord>(32);
        private readonly List<HexCoord> _nextRing = new List<HexCoord>(32);
        private readonly List<HexCoord> _pathScratch = new List<HexCoord>(64);
        private readonly Dictionary<HexCoord, HexCoord> _cameFrom = new Dictionary<HexCoord, HexCoord>(); // tile -> the tile before it

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

        /// <summary>
        /// Fills results with every tile this Apostle could walk to this turn, one step at a time,
        /// with the Capacity it has left (each step costs MoveCapacityCost). Other Apostles block
        /// the way, and so does closedTile when hasClosedTile is true (the Ascension tile, for
        /// anyone but the Awakened). The start tile is not included. Does not allocate.
        /// </summary>
        public void GetReachableTiles(ApostleState apostle, TurnCapacity capacity, bool hasClosedTile,
            HexCoord closedTile, List<HexCoord> results)
        {
            RequireRegistered(apostle);
            if (capacity == null)
                throw new ArgumentNullException(nameof(capacity));
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            results.Clear();
            if (apostle.IsEliminated)
                return;

            int steps = capacity.Remaining / GameConstants.MoveCapacityCost;
            _seen.Clear();
            _cameFrom.Clear();
            _ring.Clear();
            _seen.Add(apostle.Position);
            _ring.Add(apostle.Position);

            // Walk out one ring at a time, so a tile is only kept if a free path reaches it in time.
            for (int step = 0; step < steps && _ring.Count > 0; step++)
            {
                _nextRing.Clear();
                for (int i = 0; i < _ring.Count; i++)
                    AddFreeNeighbors(_ring[i], hasClosedTile, closedTile, results);
                _ring.Clear();
                _ring.AddRange(_nextRing);
            }
        }

        /// <summary>
        /// Fills path with the shortest walk to destination, one tile per step (the first step first,
        /// destination last), within the same reach as GetReachableTiles. False, with path empty,
        /// if destination cannot be reached this turn. Does not allocate.
        /// </summary>
        public bool TryGetPath(ApostleState apostle, TurnCapacity capacity, bool hasClosedTile,
            HexCoord closedTile, HexCoord destination, List<HexCoord> path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            path.Clear();
            GetReachableTiles(apostle, capacity, hasClosedTile, closedTile, _pathScratch);
            if (!_cameFrom.ContainsKey(destination))
                return false;

            // Walk back from the destination to the start, then flip it so the first step comes first.
            HexCoord tile = destination;
            while (tile != apostle.Position)
            {
                path.Add(tile);
                tile = _cameFrom[tile];
            }
            path.Reverse();
            return true;
        }

        // Adds the unseen, walkable neighbours of a tile to the next ring and to the results.
        private void AddFreeNeighbors(HexCoord tile, bool hasClosedTile, HexCoord closedTile, List<HexCoord> results)
        {
            IReadOnlyList<HexCoord> neighbors = Board.GetNeighborsOnBoard(tile);
            for (int n = 0; n < neighbors.Count; n++)
            {
                HexCoord next = neighbors[n];
                if (!_seen.Add(next))
                    continue;
                if (IsOccupied(next) || (hasClosedTile && next == closedTile))
                    continue;
                _nextRing.Add(next);
                _cameFrom[next] = tile;
                results.Add(next);
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