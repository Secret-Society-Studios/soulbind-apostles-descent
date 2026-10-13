using System;
using System.Collections.Generic;
using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests.EditMode
{
    public class SpawnPlannerTests
    {
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void PickSpread_ReturnsDistinctEdgeTiles(int count)
        {
            HexBoard board = StandardBoard.Create();
            List<HexCoord> picks = SpawnPlanner.PickSpread(board, count);

            Assert.AreEqual(count, picks.Count);
            Assert.AreEqual(count, new HashSet<HexCoord>(picks).Count);
            foreach (HexCoord tile in picks)
                Assert.IsTrue(board.IsEdge(tile));
        }

        [Test]
        public void PickSpread_FirstTileIsTheFirstEdgeTile()
        {
            HexBoard board = StandardBoard.Create();
            Assert.AreEqual(board.EdgeTiles[0], SpawnPlanner.PickSpread(board, 3)[0]);
        }

        [Test]
        public void PickSpread_IsDeterministic()
        {
            HexBoard board = StandardBoard.Create();
            Assert.AreEqual(SpawnPlanner.PickSpread(board, 5), SpawnPlanner.PickSpread(board, 5));
        }

        [TestCase(2, 15)]
        [TestCase(3, 12)]
        [TestCase(4, 9)]
        [TestCase(5, 6)]
        [TestCase(6, 6)]
        public void PickSpread_KeepsApostlesApart(int count, int expectedMinimumDistance)
        {
            List<HexCoord> picks = SpawnPlanner.PickSpread(StandardBoard.Create(), count);

            int closest = int.MaxValue;
            for (int i = 0; i < picks.Count; i++)
            {
                for (int j = i + 1; j < picks.Count; j++)
                    closest = Math.Min(closest, HexCoord.Distance(picks[i], picks[j]));
            }
            Assert.GreaterOrEqual(closest, expectedMinimumDistance);
        }

        [Test]
        public void PickSpread_InvalidArguments_Throw()
        {
            HexBoard board = StandardBoard.Create();

            Assert.Throws<ArgumentNullException>(() => SpawnPlanner.PickSpread(null, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => SpawnPlanner.PickSpread(board, 0));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => SpawnPlanner.PickSpread(board, board.EdgeTiles.Count + 1));
        }

        [Test]
        public void PickSpread_ResultCanCreateAMatch()
        {
            HexBoard board = StandardBoard.Create();
            List<HexCoord> picks = SpawnPlanner.PickSpread(board, 6);

            Match match = Match.Create(6, board, picks, new Random(1));

            Assert.AreEqual(6, match.Battlefield.Apostles.Count);
        }
    }
}