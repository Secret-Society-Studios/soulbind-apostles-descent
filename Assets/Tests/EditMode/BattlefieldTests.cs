using System;
using System.Collections.Generic;
using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests.EditMode
{
    public class BattlefieldTests
    {
        // A small 4x3 board. Tile (1,1) and (2,1) are interior; the rest are edge tiles.
        private static Battlefield MakeField() =>
            new Battlefield(HexBoard.FromRows(new[] { "####", "####", "####" }, 0));

        private static ApostleState At(int id, int q, int r) =>
            new ApostleState(id, new HexCoord(q, r), 10);

        // ---- Spawning ----

        [Test]
        public void AddApostle_OnEdgeTile_Succeeds()
        {
            var field = MakeField();
            var a = At(1, 0, 0);

            field.AddApostle(a);

            Assert.AreEqual(1, field.Apostles.Count);
            Assert.AreSame(a, field.GetApostleAt(new HexCoord(0, 0)));
        }

        [Test]
        public void AddApostle_OnInteriorTile_Throws()
        {
            var field = MakeField();
            Assert.Throws<ArgumentException>(() => field.AddApostle(At(1, 1, 1)));
        }

        [Test]
        public void AddApostle_OnOccupiedTile_Throws()
        {
            var field = MakeField();
            field.AddApostle(At(1, 0, 0));
            Assert.Throws<ArgumentException>(() => field.AddApostle(At(2, 0, 0)));
        }

        [Test]
        public void AddApostle_OffBoardTile_Throws()
        {
            var field = MakeField();
            Assert.Throws<ArgumentException>(() => field.AddApostle(At(1, 9, 9)));
        }

        [Test]
        public void AddApostle_DuplicateId_Throws()
        {
            var field = MakeField();
            field.AddApostle(At(1, 0, 0));
            Assert.Throws<ArgumentException>(() => field.AddApostle(At(1, 3, 0)));
        }

        [Test]
        public void AddApostle_MoreThanMaxPlayers_Throws()
        {
            var field = new Battlefield(StandardBoard.Create());
            for (int i = 0; i < 6; i++)
                field.AddApostle(new ApostleState(i + 1, field.Board.EdgeTiles[i], 5));

            var seventh = new ApostleState(7, field.Board.EdgeTiles[6], 5);
            Assert.Throws<InvalidOperationException>(() => field.AddApostle(seventh));
        }

        [Test]
        public void CanSpawnAt_TrueOnlyForFreeEdgeTiles()
        {
            var field = MakeField();
            var edge = new HexCoord(3, 0);
            var interior = new HexCoord(1, 1);

            Assert.IsTrue(field.CanSpawnAt(edge));
            Assert.IsFalse(field.CanSpawnAt(interior));

            field.AddApostle(At(1, 3, 0));
            Assert.IsFalse(field.CanSpawnAt(edge));
        }

        [Test]
        public void EliminatedApostle_FreesItsTile()
        {
            var field = MakeField();
            var a = At(1, 0, 0);
            field.AddApostle(a);

            a.TakeDamage(50);

            Assert.IsNull(field.GetApostleAt(new HexCoord(0, 0)));
            Assert.IsFalse(field.IsOccupied(new HexCoord(0, 0)));
            Assert.IsTrue(field.CanSpawnAt(new HexCoord(0, 0)));
        }

        // ---- Movement ----

        [Test]
        public void TryMove_ToAdjacentFreeTile_Succeeds()
        {
            var field = MakeField();
            var a = At(1, 0, 0);
            field.AddApostle(a);
            var capacity = new TurnCapacity(8);

            MoveResult result = field.TryMove(a, new HexCoord(1, 0), capacity);

            Assert.AreEqual(MoveResult.Success, result);
            Assert.AreEqual(new HexCoord(1, 0), a.Position);
            Assert.AreEqual(6, capacity.Remaining);
        }

        [Test]
        public void TryMove_NotAdjacent_FailsAndChangesNothing()
        {
            var field = MakeField();
            var a = At(1, 0, 0);
            field.AddApostle(a);
            var capacity = new TurnCapacity(8);

            MoveResult result = field.TryMove(a, new HexCoord(2, 0), capacity);

            Assert.AreEqual(MoveResult.NotAdjacent, result);
            Assert.AreEqual(new HexCoord(0, 0), a.Position);
            Assert.AreEqual(8, capacity.Remaining);
        }

        [Test]
        public void TryMove_ToSameTile_IsNotAdjacent()
        {
            var field = MakeField();
            var a = At(1, 0, 0);
            field.AddApostle(a);

            Assert.AreEqual(MoveResult.NotAdjacent, field.TryMove(a, new HexCoord(0, 0), new TurnCapacity(8)));
        }

        [Test]
        public void TryMove_OffBoard_Fails()
        {
            var field = MakeField();
            var a = At(1, 0, 0);
            field.AddApostle(a);

            Assert.AreEqual(MoveResult.OffBoard, field.TryMove(a, new HexCoord(-1, 0), new TurnCapacity(8)));
        }

        [Test]
        public void TryMove_OccupiedTile_FailsAndChangesNothing()
        {
            var field = MakeField();
            var a = At(1, 0, 0);
            var b = At(2, 1, 0);
            field.AddApostle(a);
            field.AddApostle(b);
            var capacity = new TurnCapacity(8);

            MoveResult result = field.TryMove(a, new HexCoord(1, 0), capacity);

            Assert.AreEqual(MoveResult.TileOccupied, result);
            Assert.AreEqual(new HexCoord(0, 0), a.Position);
            Assert.AreEqual(8, capacity.Remaining);
        }

        [Test]
        public void TryMove_NotEnoughCapacity_Fails()
        {
            var field = MakeField();
            var a = At(1, 0, 0);
            field.AddApostle(a);

            MoveResult result = field.TryMove(a, new HexCoord(1, 0), new TurnCapacity(1));

            Assert.AreEqual(MoveResult.NotEnoughCapacity, result);
            Assert.AreEqual(new HexCoord(0, 0), a.Position);
        }

        [Test]
        public void TryMove_EliminatedApostle_Fails()
        {
            var field = MakeField();
            var a = At(1, 0, 0);
            field.AddApostle(a);
            a.TakeDamage(50);

            Assert.AreEqual(MoveResult.ApostleEliminated, field.TryMove(a, new HexCoord(1, 0), new TurnCapacity(8)));
        }

        [Test]
        public void TryMove_CanRepeatUntilCapacityRunsOut()
        {
            var field = MakeField();
            var a = At(1, 0, 0);
            field.AddApostle(a);
            var capacity = new TurnCapacity(4);

            Assert.AreEqual(MoveResult.Success, field.TryMove(a, new HexCoord(1, 0), capacity));
            Assert.AreEqual(MoveResult.Success, field.TryMove(a, new HexCoord(1, 1), capacity));
            Assert.AreEqual(MoveResult.NotEnoughCapacity, field.TryMove(a, new HexCoord(1, 2), capacity));
            Assert.AreEqual(new HexCoord(1, 1), a.Position);
            Assert.AreEqual(0, capacity.Remaining);
        }

        [Test]
        public void TryMove_UnregisteredApostle_Throws()
        {
            var field = MakeField();
            var stranger = At(9, 0, 0);

            Assert.Throws<ArgumentException>(() => field.TryMove(stranger, new HexCoord(1, 0), new TurnCapacity(8)));
        }

        // ---- Legal destinations ----

        [Test]
        public void GetLegalDestinations_ReturnsOnlyFreeAdjacentTiles()
        {
            var field = MakeField();
            var a = At(1, 0, 0);
            field.AddApostle(a);
            field.AddApostle(At(2, 1, 0)); // blocks one of the two neighbors
            var results = new List<HexCoord>();

            field.GetLegalDestinations(a, new TurnCapacity(8), results);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(new HexCoord(0, 1), results[0]);
        }

        [Test]
        public void GetLegalDestinations_WithoutCapacity_ReturnsEmptyAndClearsList()
        {
            var field = MakeField();
            var a = At(1, 0, 0);
            field.AddApostle(a);
            var results = new List<HexCoord> { new HexCoord(9, 9) };

            field.GetLegalDestinations(a, new TurnCapacity(1), results);

            Assert.AreEqual(0, results.Count);
        }
    }
}
