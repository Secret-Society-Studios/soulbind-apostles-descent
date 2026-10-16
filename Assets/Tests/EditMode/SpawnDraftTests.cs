using System;
using System.Collections.Generic;
using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests.EditMode
{
    /// <summary>Each Apostle picks a starting edge tile, in turn order, apart from the others.</summary>
    public class SpawnDraftTests
    {
        private static HexBoard Board() => StandardBoard.Create();

        [Test]
        public void Picks_FollowTheGivenOrder()
        {
            var draft = new SpawnDraft(Board(), new[] { 3, 1, 2 });
            Assert.AreEqual(3, draft.CurrentPickerId);

            draft.TryPick(draft.ChooseRandomTile(new Random(1)));
            Assert.AreEqual(1, draft.CurrentPickerId);
            draft.TryPick(draft.ChooseRandomTile(new Random(2)));
            Assert.AreEqual(2, draft.CurrentPickerId);
            draft.TryPick(draft.ChooseRandomTile(new Random(3)));

            Assert.IsTrue(draft.IsComplete);
            Assert.AreEqual(0, draft.CurrentPickerId);
        }

        [Test]
        public void OnlyFreeEdgeTiles_CanBePicked()
        {
            HexBoard board = Board();
            var draft = new SpawnDraft(board, new[] { 1, 2 });
            HexCoord edge = board.EdgeTiles[0];

            HexCoord interior = default;
            foreach (HexCoord tile in board.Tiles)
            {
                if (!board.IsEdge(tile))
                {
                    interior = tile;
                    break;
                }
            }

            Assert.IsFalse(draft.TryPick(interior), "Not an edge tile");
            Assert.IsTrue(draft.TryPick(edge));
            Assert.IsFalse(draft.TryPick(edge), "Already taken");
        }

        [Test]
        public void TilesNextToAnEarlierPick_AreNotAllowed()
        {
            HexBoard board = Board();
            var draft = new SpawnDraft(board, new[] { 1, 2 });
            HexCoord first = board.EdgeTiles[0];
            draft.TryPick(first);

            var legal = new List<HexCoord>();
            draft.GetLegalTiles(legal);
            Assert.Greater(legal.Count, 0);
            foreach (HexCoord tile in legal)
            {
                Assert.IsTrue(board.IsEdge(tile));
                Assert.IsFalse(HexCoord.AreAdjacent(first, tile), $"{tile} is next to the first pick");
                Assert.AreNotEqual(first, tile);
            }

            foreach (HexCoord neighbor in board.GetNeighborsOnBoard(first))
            {
                if (board.IsEdge(neighbor))
                    Assert.IsFalse(draft.CanPick(neighbor), $"{neighbor} touches the first pick");
            }
        }

        [Test]
        public void SpawnTiles_AreInApostleIdOrder()
        {
            HexBoard board = Board();
            var draft = new SpawnDraft(board, new[] { 2, 1 });
            HexCoord forTwo = board.EdgeTiles[0];
            draft.TryPick(forTwo);
            HexCoord forOne = draft.ChooseRandomTile(new Random(5));
            draft.TryPick(forOne);

            List<HexCoord> spawns = draft.GetSpawnTiles();
            Assert.AreEqual(forOne, spawns[0]);
            Assert.AreEqual(forTwo, spawns[1]);
        }

        [Test]
        public void GetSpawnTiles_BeforeEveryoneHasPicked_Throws()
        {
            var draft = new SpawnDraft(Board(), new[] { 1, 2 });
            Assert.Throws<InvalidOperationException>(() => draft.GetSpawnTiles());
        }

        [Test]
        public void BadPickOrder_Throws()
        {
            Assert.Throws<ArgumentException>(() => new SpawnDraft(Board(), new[] { 1, 1 }));
            Assert.Throws<ArgumentException>(() => new SpawnDraft(Board(), new[] { 1, 3 }));
            Assert.Throws<ArgumentException>(() => new SpawnDraft(Board(), new[] { 1 }));
        }

        [TestCase(6, 1)]
        [TestCase(6, 2)]
        [TestCase(4, 3)]
        public void RandomPicks_FillEverySeat_AndNobodyStartsInCombat(int players, int seed)
        {
            HexBoard board = Board();
            int[] order = TurnTracker.CreateRandomSeatOrder(players, new Random(seed));
            var draft = new SpawnDraft(board, order);
            var random = new Random(seed);
            while (!draft.IsComplete)
                Assert.IsTrue(draft.TryPick(draft.ChooseRandomTile(random)));

            List<HexCoord> spawns = draft.GetSpawnTiles();
            for (int i = 0; i < spawns.Count; i++)
            {
                for (int j = i + 1; j < spawns.Count; j++)
                    Assert.IsFalse(HexCoord.AreAdjacent(spawns[i], spawns[j]), "Two Apostles start next to each other");
            }
        }

        [Test]
        public void Match_CreatedFromADraft_UsesThePickOrderAsTurnOrder()
        {
            HexBoard board = Board();
            int[] order = { 3, 1, 2 };
            var draft = new SpawnDraft(board, order);
            var random = new Random(7);
            while (!draft.IsComplete)
                draft.TryPick(draft.ChooseRandomTile(random));
            List<HexCoord> spawns = draft.GetSpawnTiles();

            Match match = Match.Create(3, board, spawns, order, new Random(8));

            Assert.AreEqual(3, match.CurrentApostle.Id, "The first to pick plays first");
            foreach (ApostleState apostle in match.Battlefield.Apostles)
                Assert.AreEqual(spawns[apostle.Id - 1], apostle.Position);
        }
    }
}