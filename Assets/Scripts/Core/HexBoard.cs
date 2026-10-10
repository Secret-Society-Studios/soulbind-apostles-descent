using System;
using System.Collections.Generic;

namespace SoulBind.Core
{
    /// <summary>
    /// A board made of an arbitrary set of hex tiles. Pure rules data: no Unity types.
    /// Tile order is the order given, so every machine can build an identical board.
    /// Neighbor lists are built once at creation, so lookups never allocate.
    /// </summary>
    public sealed class HexBoard
    {
        private readonly HashSet<HexCoord> _tileSet = new HashSet<HexCoord>();
        private readonly HashSet<HexCoord> _edgeSet = new HashSet<HexCoord>();
        private readonly List<HexCoord> _tiles = new List<HexCoord>();
        private readonly List<HexCoord> _edgeTiles = new List<HexCoord>();
        private readonly Dictionary<HexCoord, IReadOnlyList<HexCoord>> _neighbors =
            new Dictionary<HexCoord, IReadOnlyList<HexCoord>>();

        public int TileCount => _tiles.Count;
        public IReadOnlyList<HexCoord> Tiles => _tiles;

        /// <summary>Tiles with at least one missing neighbor. Apostles spawn on these.</summary>
        public IReadOnlyList<HexCoord> EdgeTiles => _edgeTiles;

        public HexBoard(IEnumerable<HexCoord> tiles)
        {
            if (tiles == null)
                throw new ArgumentNullException(nameof(tiles));

            foreach (HexCoord tile in tiles)
            {
                if (!_tileSet.Add(tile))
                    throw new ArgumentException($"Duplicate tile {tile}.", nameof(tiles));
                _tiles.Add(tile);
            }

            if (_tiles.Count == 0)
                throw new ArgumentException("A board needs at least one tile.", nameof(tiles));

            var found = new List<HexCoord>(HexCoord.DirectionCount);
            foreach (HexCoord tile in _tiles)
            {
                found.Clear();
                for (int d = 0; d < HexCoord.DirectionCount; d++)
                {
                    HexCoord neighbor = tile.Neighbor(d);
                    if (_tileSet.Contains(neighbor))
                        found.Add(neighbor);
                }

                _neighbors.Add(tile, Array.AsReadOnly(found.ToArray()));

                if (found.Count < HexCoord.DirectionCount)
                {
                    _edgeSet.Add(tile);
                    _edgeTiles.Add(tile);
                }
            }
        }

        /// <summary>
        /// Builds a board from text rows. Row index is R. Character index plus
        /// firstColumnQ is Q. '#' is a tile and '.' is empty space.
        /// </summary>
        public static HexBoard FromRows(IReadOnlyList<string> rows, int firstColumnQ)
        {
            if (rows == null)
                throw new ArgumentNullException(nameof(rows));

            var tiles = new List<HexCoord>();
            for (int r = 0; r < rows.Count; r++)
            {
                string row = rows[r];
                if (row == null)
                    throw new ArgumentException($"Row {r} is null.", nameof(rows));

                for (int i = 0; i < row.Length; i++)
                {
                    char c = row[i];
                    if (c == '#')
                        tiles.Add(new HexCoord(firstColumnQ + i, r));
                    else if (c != '.')
                        throw new FormatException(
                            $"Invalid character '{c}' in row {r}, column {i}. Use '#' for a tile and '.' for empty.");
                }
            }
            return new HexBoard(tiles);
        }

        public bool Contains(HexCoord tile) => _tileSet.Contains(tile);

        public bool IsEdge(HexCoord tile) => _edgeSet.Contains(tile);

        /// <summary>Neighbors of a tile that are on the board (0 to 6 tiles). Does not allocate.</summary>
        public IReadOnlyList<HexCoord> GetNeighborsOnBoard(HexCoord tile)
        {
            if (!_neighbors.TryGetValue(tile, out IReadOnlyList<HexCoord> neighbors))
                throw new ArgumentException($"Tile {tile} is not on the board.", nameof(tile));

            return neighbors;
        }

        /// <summary>True if every tile can be reached from every other by stepping between neighbors.</summary>
        public bool IsConnected()
        {
            var seen = new HashSet<HexCoord> { _tiles[0] };
            var queue = new Queue<HexCoord>();
            queue.Enqueue(_tiles[0]);

            while (queue.Count > 0)
            {
                HexCoord current = queue.Dequeue();
                foreach (HexCoord next in _neighbors[current])
                {
                    if (seen.Add(next))
                        queue.Enqueue(next);
                }
            }
            return seen.Count == _tiles.Count;
        }
    }
}