using System;
using System.Collections.Generic;

namespace SoulBind.Core
{
    /// <summary>
    /// Picks spawn tiles that are as far apart as possible. A default for testing and AI players;
    /// real players will choose their own tile. Deterministic: the same board gives the same result.
    /// </summary>
    public static class SpawnPlanner
    {
        public static List<HexCoord> PickSpread(HexBoard board, int count)
        {
            if (board == null)
                throw new ArgumentNullException(nameof(board));

            IReadOnlyList<HexCoord> edge = board.EdgeTiles;
            if (count < 1 || count > edge.Count)
                throw new ArgumentOutOfRangeException(nameof(count), $"Count must be 1 to {edge.Count}.");

            var chosen = new List<HexCoord>(count) { edge[0] };

            // Repeatedly take the edge tile whose nearest chosen tile is farthest away.
            while (chosen.Count < count)
            {
                int bestIndex = -1;
                int bestDistance = -1;

                for (int i = 0; i < edge.Count; i++)
                {
                    int nearest = int.MaxValue;
                    for (int j = 0; j < chosen.Count; j++)
                    {
                        int distance = HexCoord.Distance(edge[i], chosen[j]);
                        if (distance < nearest)
                            nearest = distance;
                    }

                    if (nearest > bestDistance)
                    {
                        bestDistance = nearest;
                        bestIndex = i;
                    }
                }
                chosen.Add(edge[bestIndex]);
            }
            return chosen;
        }
    }
}