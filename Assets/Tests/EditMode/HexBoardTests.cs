using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests.EditMode
{
    public class HexBoardTests
    {
        // ---- Small custom boards ----

        [Test]
        public void FromRows_ParsesExpectedTiles()
        {
            var board = HexBoard.FromRows(new[] { "##", ".#" }, 0);

            Assert.AreEqual(3, board.TileCount);
            Assert.IsTrue(board.Contains(new HexCoord(0, 0)));
            Assert.IsTrue(board.Contains(new HexCoord(1, 0)));
            Assert.IsTrue(board.Contains(new HexCoord(1, 1)));
            Assert.IsFalse(board.Contains(new HexCoord(0, 1)));
        }

        [Test]
        public void FromRows_InvalidCharacter_Throws()
        {
            Assert.Throws<FormatException>(() => HexBoard.FromRows(new[] { "#x#" }, 0));
        }

        [Test]
        public void Constructor_EmptyTileList_Throws()
        {
            Assert.Throws<ArgumentException>(() => new HexBoard(new List<HexCoord>()));
        }

        [Test]
        public void Constructor_DuplicateTile_Throws()
        {
            var tiles = new[] { new HexCoord(0, 0), new HexCoord(0, 0) };
            Assert.Throws<ArgumentException>(() => new HexBoard(tiles));
        }

        [Test]
        public void SingleTileBoard_IsEdgeAndHasNoNeighbors()
        {
            var board = HexBoard.FromRows(new[] { "#" }, 0);
            var tile = new HexCoord(0, 0);

            Assert.IsTrue(board.IsEdge(tile));
            Assert.AreEqual(1, board.EdgeTiles.Count);
            Assert.AreEqual(0, board.GetNeighborsOnBoard(tile).Count);
        }

        [Test]
        public void GetNeighborsOnBoard_OffBoardTile_Throws()
        {
            var board = HexBoard.FromRows(new[] { "##" }, 0);
            Assert.Throws<ArgumentException>(() => board.GetNeighborsOnBoard(new HexCoord(5, 5)));
        }

        [Test]
        public void IsConnected_FalseForSeparatedTiles()
        {
            var board = HexBoard.FromRows(new[] { "#.#" }, 0);
            Assert.IsFalse(board.IsConnected());
        }

        // ---- The official board ----

        [Test]
        public void StandardBoard_HasExpectedTileCount()
        {
            Assert.AreEqual(170, StandardBoard.Create().TileCount);
        }

        [Test]
        public void StandardBoard_RowCountsMatchDesign()
        {
            HexBoard board = StandardBoard.Create();
            int[] expected = { 8, 12, 13, 14, 14, 13, 13, 12, 13, 13, 13, 12, 12, 8 };

            for (int r = 0; r < expected.Length; r++)
            {
                int actual = board.Tiles.Count(t => t.R == r);
                Assert.AreEqual(expected[r], actual, $"Row {r} has the wrong number of tiles");
            }
        }

        [Test]
        public void StandardBoard_IsConnected()
        {
            Assert.IsTrue(StandardBoard.Create().IsConnected());
        }

        [Test]
        public void StandardBoard_NotchesAreNotTiles()
        {
            HexBoard board = StandardBoard.Create();

            // Top notch (row 0) and bottom notch (row 13)
            Assert.IsFalse(board.Contains(new HexCoord(8, 0)));
            Assert.IsFalse(board.Contains(new HexCoord(9, 0)));
            Assert.IsFalse(board.Contains(new HexCoord(0, 13)));
            Assert.IsFalse(board.Contains(new HexCoord(1, 13)));

            // Tiles on either side of a notch do exist
            Assert.IsTrue(board.Contains(new HexCoord(7, 0)));
            Assert.IsTrue(board.Contains(new HexCoord(10, 0)));
        }

        [Test]
        public void StandardBoard_Has49EdgeTiles()
        {
            Assert.AreEqual(49, StandardBoard.Create().EdgeTiles.Count);
        }

        [Test]
        public void StandardBoard_TilesBesideNotchesAreEdge()
        {
            HexBoard board = StandardBoard.Create();
            var beside = new[]
            {
                new HexCoord(7, 1), new HexCoord(8, 1), new HexCoord(9, 1),   // under the top notch
                new HexCoord(0, 12), new HexCoord(1, 12), new HexCoord(2, 12) // above the bottom notch
            };

            foreach (HexCoord tile in beside)
                Assert.IsTrue(board.IsEdge(tile), $"{tile} should be an edge tile");
        }

        [Test]
        public void StandardBoard_InteriorTileHasSixNeighbors()
        {
            HexBoard board = StandardBoard.Create();
            var tile = new HexCoord(4, 6);

            Assert.IsFalse(board.IsEdge(tile));
            Assert.AreEqual(6, board.GetNeighborsOnBoard(tile).Count);
        }

        [Test]
        public void StandardBoard_EdgeTilesAreUniqueAndOnBoard()
        {
            HexBoard board = StandardBoard.Create();

            Assert.AreEqual(board.EdgeTiles.Count, new HashSet<HexCoord>(board.EdgeTiles).Count);
            foreach (HexCoord tile in board.EdgeTiles)
            {
                Assert.IsTrue(board.Contains(tile));
                Assert.IsTrue(board.IsEdge(tile));
            }
        }

        [Test]
        public void StandardBoard_TileOrderIsDeterministic()
        {
            HexBoard a = StandardBoard.Create();
            HexBoard b = StandardBoard.Create();

            Assert.AreEqual(a.TileCount, b.TileCount);
            for (int i = 0; i < a.TileCount; i++)
                Assert.AreEqual(a.Tiles[i], b.Tiles[i]);
        }
    }
}