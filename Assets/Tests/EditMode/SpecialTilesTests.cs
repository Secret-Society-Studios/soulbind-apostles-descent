using System;
using System.Collections.Generic;
using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests.EditMode
{
    public class SpecialTilesTests
    {
        private static readonly HexCoord Spot = new HexCoord(2, 2);

        private static ApostleState ApostleOnSpot() => new ApostleState(1, Spot, 10);

        private static SpecialTiles OneTile(SpecialTileType type)
        {
            var specials = new SpecialTiles();
            specials.Add(Spot, type);
            return specials;
        }

        // ---- Placement ----

        [Test]
        public void CreateRandom_PlacesThreeOfEachTypeOnInteriorTiles()
        {
            HexBoard board = StandardBoard.Create();
            SpecialTiles specials = SpecialTiles.CreateRandom(board, new Random(1));

            Assert.AreEqual(9, specials.Active.Count);
            Assert.AreEqual(3, specials.CountOf(SpecialTileType.Heal));
            Assert.AreEqual(3, specials.CountOf(SpecialTileType.Purify));
            Assert.AreEqual(3, specials.CountOf(SpecialTileType.Soul));
            foreach (KeyValuePair<HexCoord, SpecialTileType> pair in specials.Active)
            {
                Assert.IsTrue(board.Contains(pair.Key));
                Assert.IsFalse(board.IsEdge(pair.Key), $"{pair.Key} is an edge tile");
            }
        }

        [Test]
        public void CreateRandom_SameSeed_GivesSamePlacement()
        {
            HexBoard board = StandardBoard.Create();
            SpecialTiles a = SpecialTiles.CreateRandom(board, new Random(7));
            SpecialTiles b = SpecialTiles.CreateRandom(board, new Random(7));

            Assert.AreEqual(a.Active.Count, b.Active.Count);
            foreach (KeyValuePair<HexCoord, SpecialTileType> pair in a.Active)
            {
                Assert.IsTrue(b.Active.TryGetValue(pair.Key, out SpecialTileType type));
                Assert.AreEqual(pair.Value, type);
            }
        }

        [Test]
        public void CreateRandom_TooFewInteriorTiles_Throws()
        {
            HexBoard small = HexBoard.FromRows(new[] { "##", "##" }, 0);
            Assert.Throws<ArgumentException>(() => SpecialTiles.CreateRandom(small, new Random(1)));
        }

        [Test]
        public void CreateRandom_InvalidArguments_Throw()
        {
            HexBoard board = StandardBoard.Create();
            Assert.Throws<ArgumentNullException>(() => SpecialTiles.CreateRandom(null, new Random(1)));
            Assert.Throws<ArgumentNullException>(() => SpecialTiles.CreateRandom(board, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => SpecialTiles.CreateRandom(board, new Random(1), 0));
        }

        [Test]
        public void Add_DuplicateTile_Throws()
        {
            SpecialTiles specials = OneTile(SpecialTileType.Heal);
            Assert.Throws<ArgumentException>(() => specials.Add(Spot, SpecialTileType.Soul));
        }

        // ---- Effects ----

        [Test]
        public void Heal_HealsAndIsUsedUp()
        {
            SpecialTiles specials = OneTile(SpecialTileType.Heal);
            ApostleState apostle = ApostleOnSpot();
            apostle.TakeDamage(20);

            Assert.IsTrue(specials.TryTrigger(apostle));
            Assert.AreEqual(35, apostle.Vessel);
            Assert.IsFalse(specials.Active.ContainsKey(Spot));

            Assert.IsFalse(specials.TryTrigger(apostle));
            Assert.AreEqual(35, apostle.Vessel);
        }

        [Test]
        public void Heal_AtFullVessel_IsStillUsedUp()
        {
            SpecialTiles specials = OneTile(SpecialTileType.Heal);
            ApostleState apostle = ApostleOnSpot();

            Assert.IsTrue(specials.TryTrigger(apostle));
            Assert.AreEqual(50, apostle.Vessel);
            Assert.IsFalse(specials.Active.ContainsKey(Spot));
        }

        [Test]
        public void Purify_ReducesCorruptionByOne()
        {
            SpecialTiles specials = OneTile(SpecialTileType.Purify);
            ApostleState apostle = ApostleOnSpot();
            apostle.AddCorruption(3);

            Assert.IsTrue(specials.TryTrigger(apostle));
            Assert.AreEqual(2, apostle.Corruption);
            Assert.IsFalse(specials.Active.ContainsKey(Spot));
        }

        [Test]
        public void Purify_AtZeroCorruption_IsStillUsedUp()
        {
            SpecialTiles specials = OneTile(SpecialTileType.Purify);
            ApostleState apostle = ApostleOnSpot();

            Assert.IsTrue(specials.TryTrigger(apostle));
            Assert.AreEqual(0, apostle.Corruption);
            Assert.IsFalse(specials.Active.ContainsKey(Spot));
        }

        [Test]
        public void Soul_GivesThreeSouls()
        {
            SpecialTiles specials = OneTile(SpecialTileType.Soul);
            ApostleState apostle = ApostleOnSpot();

            Assert.IsTrue(specials.TryTrigger(apostle));
            Assert.AreEqual(13, apostle.Souls);
            Assert.IsFalse(specials.Active.ContainsKey(Spot));
        }

        [Test]
        public void Soul_WhileSoullocked_GivesNothingAndStaysActive()
        {
            SpecialTiles specials = OneTile(SpecialTileType.Soul);
            ApostleState apostle = ApostleOnSpot();
            apostle.AddCorruption(5);

            Assert.IsFalse(specials.TryTrigger(apostle));
            Assert.AreEqual(10, apostle.Souls);
            Assert.IsTrue(specials.Active.ContainsKey(Spot));
        }

        [Test]
        public void Soul_AtTheSoulsNeededToAwaken_GivesNothingAndStaysActive()
        {
            SpecialTiles specials = OneTile(SpecialTileType.Soul);
            var apostle = new ApostleState(1, Spot, GameConstants.SoulsToAwaken);

            Assert.IsFalse(specials.TryTrigger(apostle));
            Assert.AreEqual(GameConstants.SoulsToAwaken, apostle.Souls);
            Assert.IsTrue(specials.Active.ContainsKey(Spot));
        }

        [Test]
        public void Soul_NearTheSoulsNeededToAwaken_StopsThere()
        {
            SpecialTiles specials = OneTile(SpecialTileType.Soul);
            var apostle = new ApostleState(1, Spot, GameConstants.SoulsToAwaken - 1);

            Assert.IsTrue(specials.TryTrigger(apostle));
            Assert.AreEqual(GameConstants.SoulsToAwaken, apostle.Souls, "3 Souls, but only 1 fits");
            Assert.IsFalse(specials.Active.ContainsKey(Spot));
        }


        [Test]
        public void OrdinaryTile_DoesNothing()
        {
            var specials = new SpecialTiles();
            ApostleState apostle = ApostleOnSpot();

            Assert.IsFalse(specials.TryTrigger(apostle));
            Assert.AreEqual(10, apostle.Souls);
        }

        [Test]
        public void CountOf_ReflectsRemainingTiles()
        {
            var specials = new SpecialTiles();
            specials.Add(new HexCoord(0, 0), SpecialTileType.Heal);
            specials.Add(new HexCoord(1, 0), SpecialTileType.Heal);
            specials.Add(new HexCoord(2, 0), SpecialTileType.Soul);

            Assert.AreEqual(2, specials.CountOf(SpecialTileType.Heal));
            Assert.AreEqual(0, specials.CountOf(SpecialTileType.Purify));
            Assert.AreEqual(1, specials.CountOf(SpecialTileType.Soul));

            var apostle = new ApostleState(1, new HexCoord(0, 0), 10);
            specials.TryTrigger(apostle);
            Assert.AreEqual(1, specials.CountOf(SpecialTileType.Heal));
        }

        // ---- Integration with movement and matches ----

        [Test]
        public void Move_OntoHealTile_HealsAndUsesItUp()
        {
            HexBoard board = HexBoard.FromRows(new[] { "####", "####", "####" }, 0);
            var specials = new SpecialTiles();
            specials.Add(new HexCoord(1, 0), SpecialTileType.Heal);
            var field = new Battlefield(board, specials);
            var apostle = new ApostleState(1, new HexCoord(0, 0), 10);
            field.AddApostle(apostle);
            apostle.TakeDamage(20);

            MoveResult result = field.TryMove(apostle, new HexCoord(1, 0), new TurnCapacity(8));

            Assert.AreEqual(MoveResult.Success, result);
            Assert.AreEqual(35, apostle.Vessel);
            Assert.IsFalse(field.Specials.Active.ContainsKey(new HexCoord(1, 0)));
        }

        [Test]
        public void RejectedMove_DoesNotTriggerTile()
        {
            HexBoard board = HexBoard.FromRows(new[] { "####", "####", "####" }, 0);
            var specials = new SpecialTiles();
            specials.Add(new HexCoord(1, 0), SpecialTileType.Soul);
            var field = new Battlefield(board, specials);
            var apostle = new ApostleState(1, new HexCoord(0, 0), 10);
            field.AddApostle(apostle);

            MoveResult result = field.TryMove(apostle, new HexCoord(1, 0), new TurnCapacity(1));

            Assert.AreEqual(MoveResult.NotEnoughCapacity, result);
            Assert.AreEqual(10, apostle.Souls);
            Assert.IsTrue(field.Specials.Active.ContainsKey(new HexCoord(1, 0)));
        }

        [Test]
        public void Match_Create_PlacesNineSpecialTilesAwayFromSpawns()
        {
            HexBoard board = StandardBoard.Create();
            List<HexCoord> spawns = SpawnPlanner.PickSpread(board, 4);

            Match match = Match.Create(4, board, spawns, new Random(1));

            Assert.AreEqual(9, match.Battlefield.Specials.Active.Count);
            foreach (ApostleState apostle in match.Battlefield.Apostles)
                Assert.IsFalse(match.Battlefield.Specials.Active.ContainsKey(apostle.Position));
        }

        [Test]
        public void Match_Create_WithSameSeed_GivesSameSpecialTiles()
        {
            HexBoard board = StandardBoard.Create();
            List<HexCoord> spawns = SpawnPlanner.PickSpread(board, 3);

            Match a = Match.Create(3, board, spawns, new Random(5));
            Match b = Match.Create(3, board, spawns, new Random(5));

            foreach (KeyValuePair<HexCoord, SpecialTileType> pair in a.Battlefield.Specials.Active)
            {
                Assert.IsTrue(b.Battlefield.Specials.Active.TryGetValue(pair.Key, out SpecialTileType type));
                Assert.AreEqual(pair.Value, type);
            }
        }
    }
}