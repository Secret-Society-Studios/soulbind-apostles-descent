using System;
using System.Collections.Generic;

namespace SoulBind.Core
{
    /// <summary>
    /// Tracks the Awakened Apostle and the Ascension tile. At most one Apostle is Awakened.
    /// Reaching 20 Souls Awakens an Apostle and places the Ascension tile on the farthest
    /// free tile; dropping below 20 or being eliminated removes it.
    /// Pure rules data: no Unity types.
    /// </summary>
    public sealed class AscensionTracker
    {
        // Reused for tied farthest tiles, so placement does not allocate.
        private readonly List<HexCoord> _ties = new List<HexCoord>();

        /// <summary>Id of the Awakened Apostle, or 0 if nobody is Awakened.</summary>
        public int AwakenedId { get; private set; }

        public bool HasAwakened => AwakenedId != 0;

        /// <summary>The Ascension tile. Only meaningful while HasAwakened is true.</summary>
        public HexCoord Tile { get; private set; }

        /// <summary>
        /// Re-checks the Awakened state. Call after anything that can change Souls or eliminate
        /// an Apostle. If nobody is Awakened, the priority Apostle (usually the one acting, may be
        /// null) is checked first, then the others in the order they were added.
        /// </summary>
        public void Update(Battlefield battlefield, ApostleState priority, Random random)
        {
            if (battlefield == null)
                throw new ArgumentNullException(nameof(battlefield));
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            if (HasAwakened)
            {
                ApostleState awakened = FindApostle(battlefield, AwakenedId);
                if (!awakened.IsEliminated && awakened.Souls >= GameConstants.SoulsToAwaken)
                    return;

                AwakenedId = 0;
                Tile = default;
            }

            if (priority != null && TryAwaken(battlefield, priority, random))
                return;

            IReadOnlyList<ApostleState> apostles = battlefield.Apostles;
            for (int i = 0; i < apostles.Count; i++)
            {
                if (apostles[i] != priority && TryAwaken(battlefield, apostles[i], random))
                    return;
            }
        }

        /// <summary>For snapshots (online play): sets who is Awakened and where the tile is.</summary>
        internal void Restore(int awakenedId, HexCoord tile)
        {
            AwakenedId = awakenedId;
            Tile = awakenedId != 0 ? tile : default;
        }

        /// <summary>True when the living Awakened Apostle stands on the Ascension tile.</summary>
        public bool HasAscended(Battlefield battlefield)
        {
            if (battlefield == null)
                throw new ArgumentNullException(nameof(battlefield));
            if (!HasAwakened)
                return false;

            ApostleState awakened = FindApostle(battlefield, AwakenedId);
            return !awakened.IsEliminated && awakened.Position == Tile;
        }

        private bool TryAwaken(Battlefield battlefield, ApostleState apostle, Random random)
        {
            if (apostle.IsEliminated || apostle.Souls < GameConstants.SoulsToAwaken)
                return false;
            if (!TryFindAscensionTile(battlefield, apostle.Position, random, out HexCoord tile))
                return false;

            AwakenedId = apostle.Id;
            Tile = tile;
            return true;
        }

        // The farthest tile from 'from' that has no living Apostle and no unused special tile.
        // Ties are broken with the match's seeded random source.
        private bool TryFindAscensionTile(Battlefield battlefield, HexCoord from, Random random, out HexCoord tile)
        {
            _ties.Clear();
            int best = -1;
            IReadOnlyList<HexCoord> tiles = battlefield.Board.Tiles;

            for (int i = 0; i < tiles.Count; i++)
            {
                HexCoord candidate = tiles[i];
                if (battlefield.IsOccupied(candidate) || battlefield.Specials.Active.ContainsKey(candidate))
                    continue;

                int distance = HexCoord.Distance(from, candidate);
                if (distance > best)
                {
                    best = distance;
                    _ties.Clear();
                    _ties.Add(candidate);
                }
                else if (distance == best)
                {
                    _ties.Add(candidate);
                }
            }

            if (_ties.Count == 0)
            {
                tile = default;
                return false;
            }

            tile = _ties.Count == 1 ? _ties[0] : _ties[random.Next(_ties.Count)];
            return true;
        }

        private static ApostleState FindApostle(Battlefield battlefield, int id)
        {
            IReadOnlyList<ApostleState> apostles = battlefield.Apostles;
            for (int i = 0; i < apostles.Count; i++)
            {
                if (apostles[i].Id == id)
                    return apostles[i];
            }
            throw new InvalidOperationException($"No Apostle with id {id}.");
        }
    }
}