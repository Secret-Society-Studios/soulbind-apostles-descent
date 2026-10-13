using System;
using System.Collections.Generic;
using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests.EditMode
{
    public class HexCoordTests
    {
        [Test]
        public void GetNeighbors_ReturnsSixDistinctTilesAtDistanceOne()
        {
            var origin = new HexCoord(0, 0);
            HexCoord[] neighbors = origin.GetNeighbors();

            Assert.AreEqual(6, neighbors.Length);
            Assert.AreEqual(6, new HashSet<HexCoord>(neighbors).Count);
            foreach (HexCoord n in neighbors)
                Assert.AreEqual(1, HexCoord.Distance(origin, n));
        }

        [Test]
        public void Neighbor_OppositeDirectionReturnsToStart()
        {
            var start = new HexCoord(3, -2);
            for (int d = 0; d < HexCoord.DirectionCount; d++)
            {
                HexCoord back = start.Neighbor(d).Neighbor((d + 3) % 6);
                Assert.AreEqual(start, back, $"Direction {d} did not return to start");
            }
        }

        [Test]
        public void Neighbor_InvalidDirection_Throws()
        {
            var origin = new HexCoord(0, 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => origin.Neighbor(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => origin.Neighbor(6));
        }

        [Test]
        public void Distance_SameTile_IsZero()
        {
            var tile = new HexCoord(4, -7);
            Assert.AreEqual(0, HexCoord.Distance(tile, tile));
        }

        [TestCase(0, 0, 3, -3, 3)]
        [TestCase(0, 0, 2, 1, 3)]
        [TestCase(0, 0, -2, 3, 3)]
        [TestCase(2, -1, -1, 1, 3)]
        public void Distance_KnownValues(int q1, int r1, int q2, int r2, int expected)
        {
            Assert.AreEqual(expected, HexCoord.Distance(new HexCoord(q1, r1), new HexCoord(q2, r2)));
        }

        [Test]
        public void Distance_IsSymmetric()
        {
            var a = new HexCoord(2, -3);
            var b = new HexCoord(-1, 4);
            Assert.AreEqual(HexCoord.Distance(a, b), HexCoord.Distance(b, a));
        }

        [Test]
        public void AreAdjacent_TrueOnlyForNeighbors()
        {
            var origin = new HexCoord(0, 0);
            foreach (HexCoord n in origin.GetNeighbors())
                Assert.IsTrue(HexCoord.AreAdjacent(origin, n));

            Assert.IsFalse(HexCoord.AreAdjacent(origin, new HexCoord(2, 0)));
            Assert.IsFalse(HexCoord.AreAdjacent(origin, origin));
        }

        [Test]
        public void Equality_AndHashing_WorkForSetsAndComparisons()
        {
            var a = new HexCoord(1, 2);
            var b = new HexCoord(1, 2);
            var c = new HexCoord(2, 1);

            Assert.IsTrue(a == b);
            Assert.IsTrue(a != c);
            Assert.IsTrue(a.Equals((object)b));
            Assert.IsTrue(new HashSet<HexCoord> { a }.Contains(b));
        }

        [Test]
        public void ToPlane_OriginIsAtZero()
        {
            new HexCoord(0, 0).ToPlane(0.5f, out float x, out float y);
            Assert.AreEqual(0f, x, 0.0001f);
            Assert.AreEqual(0f, y, 0.0001f);
        }

        [Test]
        public void ToPlane_AdjacentTilesAreSqrt3TimesSizeApart()
        {
            const float size = 0.5f;
            var origin = new HexCoord(2, -1);
            origin.ToPlane(size, out float ox, out float oy);

            foreach (HexCoord neighbor in origin.GetNeighbors())
            {
                neighbor.ToPlane(size, out float nx, out float ny);
                float distance = (float)Math.Sqrt((nx - ox) * (nx - ox) + (ny - oy) * (ny - oy));
                Assert.AreEqual(size * 1.7320508f, distance, 0.0001f);
            }
        }

        [Test]
        public void ToPlane_HigherRowsAreLowerOnScreen()
        {
            new HexCoord(0, 0).ToPlane(0.5f, out _, out float top);
            new HexCoord(0, 1).ToPlane(0.5f, out _, out float below);
            Assert.Less(below, top);
        }

        [Test]
        public void FromPlane_OriginIsTileZeroZero()
        {
            Assert.AreEqual(new HexCoord(0, 0), HexCoord.FromPlane(0f, 0f, 0.5f));
        }

        [Test]
        public void FromPlane_RoundTripsEveryTileCenter()
        {
            const float size = 0.5f;
            foreach (HexCoord tile in StandardBoard.Create().Tiles)
            {
                tile.ToPlane(size, out float x, out float y);
                Assert.AreEqual(tile, HexCoord.FromPlane(x, y, size), $"Tile {tile} did not round-trip");
            }
        }

        [Test]
        public void FromPlane_PointsNearACenterMapToThatTile()
        {
            const float size = 0.5f;
            var tile = new HexCoord(3, 4);
            tile.ToPlane(size, out float cx, out float cy);

            for (int i = 0; i < 6; i++)
            {
                double angle = i * Math.PI / 3.0;
                float x = cx + (float)Math.Cos(angle) * size * 0.45f;
                float y = cy + (float)Math.Sin(angle) * size * 0.45f;
                Assert.AreEqual(tile, HexCoord.FromPlane(x, y, size), $"Offset {i} left the tile");
            }
        }
    }
}