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
    }
}