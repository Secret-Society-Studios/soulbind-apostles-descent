using System.Collections.Generic;
using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests.EditMode
{
    public class MoveReachTests
    {
        // A 5x5 block of tiles, big enough to walk around in.
        private static Battlefield MakeField() =>
            new Battlefield(HexBoard.FromRows(new[] { "#####", "#####", "#####", "#####", "#####" }, 0));

        private static ApostleState At(int id, int q, int r) =>
            new ApostleState(id, new HexCoord(q, r), 10);

        private static List<HexCoord> Reach(Battlefield field, ApostleState apostle, int capacity)
        {
            var results = new List<HexCoord>();
            field.GetReachableTiles(apostle, new TurnCapacity(capacity), false, default, results);
            return results;
        }

        [Test]
        public void Reach_NotEnoughCapacity_IsEmpty()
        {
            var field = MakeField();
            var a = At(1, 0, 0);
            field.AddApostle(a);

            Assert.AreEqual(0, Reach(field, a, GameConstants.MoveCapacityCost - 1).Count);
        }

        [Test]
        public void Reach_OneStep_MatchesLegalDestinations()
        {
            var field = MakeField();
            var a = At(1, 0, 0);
            field.AddApostle(a);
            var capacity = new TurnCapacity(GameConstants.MoveCapacityCost);
            var legal = new List<HexCoord>();
            field.GetLegalDestinations(a, capacity, legal);

            List<HexCoord> reach = Reach(field, a, GameConstants.MoveCapacityCost);

            CollectionAssert.AreEquivalent(legal, reach);
        }

        [Test]
        public void Reach_TwoSteps_KeepsEveryTileWithinTwo()
        {
            var field = MakeField();
            var a = At(1, 0, 0);
            field.AddApostle(a);

            List<HexCoord> reach = Reach(field, a, 2 * GameConstants.MoveCapacityCost);

            Assert.IsNotEmpty(reach);
            foreach (HexCoord tile in reach)
            {
                int distance = HexCoord.Distance(a.Position, tile);
                Assert.That(distance, Is.InRange(1, 2), $"{tile} is {distance} steps away.");
            }
            CollectionAssert.DoesNotContain(reach, a.Position);
            CollectionAssert.AllItemsAreUnique(reach);
        }

        [Test]
        public void Reach_LeavesOutTilesWithAnApostle()
        {
            var field = MakeField();
            var a = At(1, 0, 0);
            var b = At(2, 1, 0);
            field.AddApostle(a);
            field.AddApostle(b);

            List<HexCoord> reach = Reach(field, a, 3 * GameConstants.MoveCapacityCost);

            CollectionAssert.DoesNotContain(reach, b.Position);
        }

        [Test]
        public void Reach_ClosedTile_IsNotEntered()
        {
            var field = MakeField();
            var a = At(1, 0, 0);
            field.AddApostle(a);
            var closed = new HexCoord(1, 0);
            var results = new List<HexCoord>();

            field.GetReachableTiles(a, new TurnCapacity(GameConstants.MoveCapacityCost), true, closed, results);

            CollectionAssert.DoesNotContain(results, closed);
        }

        [Test]
        public void Match_Reach_IncludesEveryNextStep()
        {
            HexBoard board = StandardBoard.Create();
            var spawns = new List<HexCoord> { board.EdgeTiles[0], board.EdgeTiles[board.EdgeTiles.Count / 2] };
            Match match = Match.Create(2, board, spawns, new System.Random(1));
            var legal = new List<HexCoord>();
            var reach = new List<HexCoord>();

            match.GetLegalDestinations(legal);
            match.GetReachableTiles(reach);

            Assert.That(reach.Count, Is.GreaterThanOrEqualTo(legal.Count));
            foreach (HexCoord tile in legal)
                CollectionAssert.Contains(reach, tile);
        }
    }
}
