using System;
using System.Collections.Generic;

namespace SoulBind.Core
{
    /// <summary>
    /// Before the first turn, each Apostle picks their starting tile, one at a time in turn order.
    /// A pick must be a free edge tile that is not next to an Apostle who has already picked, so nobody
    /// starts in combat. If no such tile is left, any free edge tile is allowed.
    /// Bots pick a random allowed tile. Pure rules data: no Unity types.
    /// </summary>
    public sealed class SpawnDraft
    {
        private readonly HexBoard _board;
        private readonly int[] _pickOrder;
        private readonly HexCoord[] _picks;   // by Apostle id - 1
        private readonly bool[] _picked;      // by Apostle id - 1
        private readonly List<HexCoord> _scratch = new List<HexCoord>(64);
        private int _turn;

        /// <param name="pickOrder">Apostle ids (1..N) in the order they pick: the match's turn order.</param>
        public SpawnDraft(HexBoard board, IReadOnlyList<int> pickOrder)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            if (pickOrder == null)
                throw new ArgumentNullException(nameof(pickOrder));
            if (pickOrder.Count < GameConstants.MinPlayers || pickOrder.Count > GameConstants.MaxPlayers)
                throw new ArgumentException($"A match needs {GameConstants.MinPlayers} to {GameConstants.MaxPlayers} Apostles.",
                    nameof(pickOrder));

            int count = pickOrder.Count;
            _pickOrder = new int[count];
            var seen = new bool[count];
            for (int i = 0; i < count; i++)
            {
                int id = pickOrder[i];
                if (id < 1 || id > count || seen[id - 1])
                    throw new ArgumentException("The pick order must hold each id from 1 to the player count once.",
                        nameof(pickOrder));
                seen[id - 1] = true;
                _pickOrder[i] = id;
            }
            _picks = new HexCoord[count];
            _picked = new bool[count];
        }

        public int PlayerCount => _pickOrder.Length;

        public bool IsComplete => _turn >= _pickOrder.Length;

        /// <summary>The Apostle who picks now, or 0 when everyone has picked.</summary>
        public int CurrentPickerId => IsComplete ? 0 : _pickOrder[_turn];

        /// <summary>True once this Apostle has picked; tile is their pick.</summary>
        public bool TryGetPick(int apostleId, out HexCoord tile)
        {
            tile = default;
            if (apostleId < 1 || apostleId > _picks.Length || !_picked[apostleId - 1])
                return false;
            tile = _picks[apostleId - 1];
            return true;
        }

        /// <summary>True if the current picker may start on this tile.</summary>
        public bool CanPick(HexCoord tile)
        {
            if (IsComplete || !_board.Contains(tile) || !_board.IsEdge(tile) || IsTaken(tile))
                return false;
            return !IsNextToPick(tile) || !AnyTileApartFromPicks();
        }

        /// <summary>Fills results with every tile the current picker may choose (empty when complete).</summary>
        public void GetLegalTiles(List<HexCoord> results)
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));
            results.Clear();
            if (IsComplete)
                return;

            bool keepApart = AnyTileApartFromPicks();
            IReadOnlyList<HexCoord> edge = _board.EdgeTiles;
            for (int i = 0; i < edge.Count; i++)
            {
                HexCoord tile = edge[i];
                if (!IsTaken(tile) && (!keepApart || !IsNextToPick(tile)))
                    results.Add(tile);
            }
        }

        /// <summary>The current picker takes this tile. Returns false (and changes nothing) if not allowed.</summary>
        public bool TryPick(HexCoord tile)
        {
            if (!CanPick(tile))
                return false;
            int id = _pickOrder[_turn];
            _picks[id - 1] = tile;
            _picked[id - 1] = true;
            _turn++;
            return true;
        }

        /// <summary>A random allowed tile for the current picker (how bots pick).</summary>
        public HexCoord ChooseRandomTile(Random random)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            GetLegalTiles(_scratch);
            if (_scratch.Count == 0)
                throw new InvalidOperationException(IsComplete ? "Everyone has already picked." : "No edge tile is free.");
            return _scratch[random.Next(_scratch.Count)];
        }

        /// <summary>Everyone's tile in Apostle id order (index 0 is Apostle 1), as Match.Create expects.</summary>
        public List<HexCoord> GetSpawnTiles()
        {
            if (!IsComplete)
                throw new InvalidOperationException("Not everyone has picked a tile yet.");
            return new List<HexCoord>(_picks);
        }

        private bool IsTaken(HexCoord tile)
        {
            for (int i = 0; i < _picks.Length; i++)
            {
                if (_picked[i] && _picks[i] == tile)
                    return true;
            }
            return false;
        }

        private bool IsNextToPick(HexCoord tile)
        {
            for (int i = 0; i < _picks.Length; i++)
            {
                if (_picked[i] && HexCoord.AreAdjacent(_picks[i], tile))
                    return true;
            }
            return false;
        }

        // Is there still a free edge tile that is not next to anyone? If not, the rule is relaxed.
        private bool AnyTileApartFromPicks()
        {
            IReadOnlyList<HexCoord> edge = _board.EdgeTiles;
            for (int i = 0; i < edge.Count; i++)
            {
                if (!IsTaken(edge[i]) && !IsNextToPick(edge[i]))
                    return true;
            }
            return false;
        }
    }
}