using System;
using System.Collections.Generic;
using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests.EditMode
{
    /// <summary>Used special tiles come back, on random free tiles, at the start of every Phase.</summary>
    public class SpecialTileReplenishTests
    {
        private static HexCoord AnyTile(SpecialTiles tiles)
        {
            foreach (KeyValuePair<HexCoord, SpecialTileType> pair in tiles.Active)
                return pair.Key;
            throw new InvalidOperationException("No special tile.");
        }

        [Test]
        public void Replenish_BringsBackUsedTiles_OnFreeInteriorTiles()
        {
            HexBoard board = StandardBoard.Create();
            SpecialTiles tiles = SpecialTiles.CreateRandom(board, new Random(3));
            int total = tiles.Active.Count;
            HexCoord used = AnyTile(tiles);
            SpecialTileType type = tiles.Active[used];
            var apostle = new ApostleState(1, used, 5);
            apostle.TakeDamage(10); // so a Heal tile has something to heal

            Assert.IsTrue(tiles.TryTrigger(apostle));
            Assert.AreEqual(total - 1, tiles.Active.Count);

            // The Apostle still stands on the used tile, so it can't come back there.
            int added = tiles.Replenish(board, new Random(5), tile => tile == apostle.Position);

            Assert.AreEqual(1, added);
            Assert.AreEqual(total, tiles.Active.Count);
            Assert.AreEqual(tiles.PlacedCountOf(type), tiles.CountOf(type), "The same kind comes back");
            Assert.IsFalse(tiles.Active.ContainsKey(used), "Not under the Apostle");
            foreach (KeyValuePair<HexCoord, SpecialTileType> pair in tiles.Active)
                Assert.IsFalse(board.IsEdge(pair.Key), "Never on an edge tile");
        }

        [Test]
        public void ANewPhase_StartsWithEverySpecialTile()
        {
            HexBoard board = StandardBoard.Create();
            Match match = Match.Create(2, board, SpawnPlanner.PickSpread(board, 2), new Random(4));
            SpecialTiles tiles = match.Battlefield.Specials;
            int total = tiles.Active.Count;
            var visitor = new ApostleState(9, AnyTile(tiles), 5);
            visitor.TakeDamage(10);
            Assert.IsTrue(tiles.TryTrigger(visitor));

            for (int i = 0; i < 20 && !match.IsAwaitingPhaseStart; i++)
                match.EndTurn();
            Assume.That(match.IsAwaitingPhaseStart, "The first Phase should have ended");
            Assert.AreEqual(total - 1, tiles.Active.Count, "Still used until the new Phase begins");

            match.BeginNextPhase(TurnDirection.Forward);

            Assert.AreEqual(total, tiles.Active.Count);
        }
    }
}