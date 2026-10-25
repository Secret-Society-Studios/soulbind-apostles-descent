using System;
using System.Collections.Generic;

namespace SoulBind.Core
{
    /// <summary>
    /// The special tiles still waiting to be used. Each works once: when an Apostle steps
    /// onto it, its effect applies and the tile becomes an ordinary tile. At the start of every
    /// Phase, every used tile comes back on a random free interior tile (Replenish).
    /// Pure rules data: no Unity types.
    /// </summary>
    public sealed class SpecialTiles
    {
        private const int TypeCount = 3;

        private readonly Dictionary<HexCoord, SpecialTileType> _active = new Dictionary<HexCoord, SpecialTileType>();
        private readonly int[] _placedPerType = new int[TypeCount];
        private readonly List<HexCoord> _candidates = new List<HexCoord>(64);

        /// <summary>Special tiles that have not been used yet.</summary>
        public IReadOnlyDictionary<HexCoord, SpecialTileType> Active => _active;

        public void Add(HexCoord tile, SpecialTileType type)
        {
            if (!_active.TryAdd(tile, type))
                throw new ArgumentException($"There is already a special tile at {tile}.", nameof(tile));
            _placedPerType[(int)type]++;
        }

        /// <summary>How many tiles of this type the match started with (used or not).</summary>
        public int PlacedCountOf(SpecialTileType type) => _placedPerType[(int)type];

        /// <summary>
        /// Brings back every used special tile (at the start of each Phase), each on a random interior tile
        /// that is not already special and not blocked (an Apostle standing there, the Ascension tile).
        /// Unused tiles stay where they are. Returns how many came back.
        /// </summary>
        public int Replenish(HexBoard board, Random random, Func<HexCoord, bool> isBlocked)
        {
            if (board == null)
                throw new ArgumentNullException(nameof(board));
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            _candidates.Clear();
            for (int i = 0; i < board.Tiles.Count; i++)
            {
                HexCoord tile = board.Tiles[i];
                if (!board.IsEdge(tile) && !_active.ContainsKey(tile) && (isBlocked == null || !isBlocked(tile)))
                    _candidates.Add(tile);
            }

            int added = 0;
            for (int t = 0; t < TypeCount; t++)
            {
                var type = (SpecialTileType)t;
                int missing = _placedPerType[t] - CountOf(type);
                for (int k = 0; k < missing && _candidates.Count > 0; k++)
                {
                    int pick = random.Next(_candidates.Count);
                    _active[_candidates[pick]] = type;
                    _candidates[pick] = _candidates[_candidates.Count - 1];
                    _candidates.RemoveAt(_candidates.Count - 1);
                    added++;
                }
            }
            return added;
        }

        public int CountOf(SpecialTileType type)
        {
            int count = 0;
            foreach (KeyValuePair<HexCoord, SpecialTileType> pair in _active)
            {
                if (pair.Value == type)
                    count++;
            }
            return count;
        }

        /// <summary>For snapshots (online play): replaces every unused special tile.</summary>
        internal void Restore(IReadOnlyList<HexCoord> tiles, IReadOnlyList<SpecialTileType> types)
        {
            _active.Clear();
            for (int i = 0; i < tiles.Count; i++)
                _active[tiles[i]] = types[i];
        }

        /// <summary>
        /// Places countPerType tiles of each type on random interior (non-edge) tiles.
        /// The same seed always gives the same placement.
        /// </summary>
        public static SpecialTiles CreateRandom(HexBoard board, Random random,
            int countPerType = GameConstants.SpecialTilesPerType)
        {
            if (board == null)
                throw new ArgumentNullException(nameof(board));
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            if (countPerType < 1)
                throw new ArgumentOutOfRangeException(nameof(countPerType), "Count per type must be at least 1.");

            var candidates = new List<HexCoord>();
            for (int i = 0; i < board.Tiles.Count; i++)
            {
                if (!board.IsEdge(board.Tiles[i]))
                    candidates.Add(board.Tiles[i]);
            }

            int total = countPerType * TypeCount;
            if (candidates.Count < total)
                throw new ArgumentException(
                    $"The board has only {candidates.Count} interior tiles, but {total} special tiles are needed.",
                    nameof(board));

            var result = new SpecialTiles();
            for (int i = 0; i < total; i++)
            {
                // Partial shuffle: swap a random remaining candidate into position i.
                int j = i + random.Next(candidates.Count - i);
                HexCoord chosen = candidates[j];
                candidates[j] = candidates[i];
                candidates[i] = chosen;

                result.Add(chosen, (SpecialTileType)(i / countPerType));
            }
            return result;
        }

        /// <summary>
        /// Applies the tile under the Apostle, if there is one, and uses it up.
        /// Returns true if a tile was used. A Soul tile does nothing for a Soullocked Apostle and stays.
        /// </summary>
        public bool TryTrigger(ApostleState apostle)
        {
            if (apostle == null)
                throw new ArgumentNullException(nameof(apostle));
            if (!_active.TryGetValue(apostle.Position, out SpecialTileType type))
                return false;

            switch (type)
            {
                case SpecialTileType.Heal:
                    apostle.Heal(GameConstants.HealTileAmount);
                    break;
                case SpecialTileType.Purify:
                    apostle.ReduceCorruption(GameConstants.PurifyTileAmount);
                    break;
                case SpecialTileType.Soul:
                    // Stays on the board for someone who can use it: a Soullocked Apostle gains nothing,
                    // and one already at the Souls needed to Awaken can't hold more.
                    if (apostle.IsSoullocked || apostle.HasMaxSouls)
                        return false;
                    apostle.AddSouls(GameConstants.SoulTileAmount);
                    break;
            }

            _active.Remove(apostle.Position);
            return true;
        }
    }
}