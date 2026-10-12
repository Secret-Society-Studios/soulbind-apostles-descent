using System;
using System.Collections.Generic;
using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests.EditMode
{
    public class AscensionTests
    {
        private static HexBoard Grid() => HexBoard.FromRows(new[] { "####", "####", "####" }, 0);
        private static HexBoard Row() => HexBoard.FromRows(new[] { "#####" }, 0);

        private static Battlefield Field(HexBoard board, SpecialTiles specials, params ApostleState[] apostles)
        {
            var field = new Battlefield(board, specials);
            foreach (ApostleState apostle in apostles)
                field.AddApostle(apostle);
            return field;
        }

        // ---- Awakening and tile placement ----

        [Test]
        public void Update_BelowTwentySouls_NobodyAwakens()
        {
            var a = new ApostleState(1, new HexCoord(0, 0), 19);
            Battlefield field = Field(Grid(), null, a);
            var tracker = new AscensionTracker();

            tracker.Update(field, null, new Random(1));

            Assert.IsFalse(tracker.HasAwakened);
            Assert.AreEqual(0, tracker.AwakenedId);
        }

        [Test]
        public void Update_AtTwentySouls_AwakensAndPlacesTileOnFarthestTile()
        {
            var a = new ApostleState(1, new HexCoord(0, 0), 20);
            Battlefield field = Field(Grid(), null, a);
            var tracker = new AscensionTracker();

            tracker.Update(field, null, new Random(1));

            Assert.AreEqual(1, tracker.AwakenedId);
            Assert.AreEqual(new HexCoord(3, 2), tracker.Tile);
        }

        [Test]
        public void Update_TiedFarthestTiles_PicksOneOfThem()
        {
            var a = new ApostleState(1, new HexCoord(2, 0), 20);
            Battlefield field = Field(Row(), null, a);
            var tracker = new AscensionTracker();

            tracker.Update(field, null, new Random(1));

            Assert.IsTrue(tracker.Tile == new HexCoord(0, 0) || tracker.Tile == new HexCoord(4, 0),
                $"Unexpected tile {tracker.Tile}");
        }

        [Test]
        public void Update_TiedFarthestTiles_SameSeedGivesSameTile()
        {
            var a = new ApostleState(1, new HexCoord(2, 0), 20);
            Battlefield field = Field(Row(), null, a);
            var first = new AscensionTracker();
            var second = new AscensionTracker();

            first.Update(field, null, new Random(11));
            second.Update(field, null, new Random(11));

            Assert.AreEqual(first.Tile, second.Tile);
        }

        [Test]
        public void Update_SkipsTilesWithAnApostle()
        {
            var a = new ApostleState(1, new HexCoord(0, 0), 20);
            var b = new ApostleState(2, new HexCoord(4, 0), 0);
            Battlefield field = Field(Row(), null, a, b);
            var tracker = new AscensionTracker();

            tracker.Update(field, null, new Random(1));

            Assert.AreEqual(new HexCoord(3, 0), tracker.Tile);
        }

        [Test]
        public void Update_SkipsUnusedSpecialTiles()
        {
            var specials = new SpecialTiles();
            specials.Add(new HexCoord(4, 0), SpecialTileType.Soul);
            var a = new ApostleState(1, new HexCoord(0, 0), 20);
            Battlefield field = Field(Row(), specials, a);
            var tracker = new AscensionTracker();

            tracker.Update(field, null, new Random(1));

            Assert.AreEqual(new HexCoord(3, 0), tracker.Tile);
        }

        [Test]
        public void Update_NoFreeTile_DoesNotAwaken()
        {
            HexBoard tiny = HexBoard.FromRows(new[] { "##" }, 0);
            var a = new ApostleState(1, new HexCoord(0, 0), 20);
            var b = new ApostleState(2, new HexCoord(1, 0), 0);
            Battlefield field = Field(tiny, null, a, b);
            var tracker = new AscensionTracker();

            tracker.Update(field, null, new Random(1));

            Assert.IsFalse(tracker.HasAwakened);
        }

        [Test]
        public void Update_OnlyOneApostleAwakensAtATime()
        {
            var a = new ApostleState(1, new HexCoord(0, 0), 20);
            var b = new ApostleState(2, new HexCoord(3, 2), 20);
            Battlefield field = Field(Grid(), null, a, b);
            var tracker = new AscensionTracker();

            tracker.Update(field, null, new Random(1));
            tracker.Update(field, b, new Random(1));

            Assert.AreEqual(1, tracker.AwakenedId);
        }

        [Test]
        public void Update_PriorityApostleAwakensFirst()
        {
            var a = new ApostleState(1, new HexCoord(0, 0), 20);
            var b = new ApostleState(2, new HexCoord(3, 2), 20);
            Battlefield field = Field(Grid(), null, a, b);
            var tracker = new AscensionTracker();

            tracker.Update(field, b, new Random(1));

            Assert.AreEqual(2, tracker.AwakenedId);
        }

        // ---- Losing the Awakened state ----

        [Test]
        public void Update_DropsBelowTwenty_LosesAwakening()
        {
            var a = new ApostleState(1, new HexCoord(0, 0), 20);
            Battlefield field = Field(Grid(), null, a);
            var tracker = new AscensionTracker();
            tracker.Update(field, null, new Random(1));

            a.LoseSouls(1);
            tracker.Update(field, null, new Random(1));

            Assert.IsFalse(tracker.HasAwakened);
            Assert.AreEqual(0, tracker.AwakenedId);
        }

        [Test]
        public void Update_AwakenedApostleEliminated_LosesAwakening()
        {
            var a = new ApostleState(1, new HexCoord(0, 0), 20);
            Battlefield field = Field(Grid(), null, a);
            var tracker = new AscensionTracker();
            tracker.Update(field, null, new Random(1));

            a.TakeDamage(GameConstants.StartingVessel);
            tracker.Update(field, null, new Random(1));

            Assert.IsFalse(tracker.HasAwakened);
        }

        [Test]
        public void Update_AfterAwakeningIsLost_AnotherApostleCanAwaken()
        {
            var a = new ApostleState(1, new HexCoord(0, 0), 20);
            var b = new ApostleState(2, new HexCoord(3, 2), 20);
            Battlefield field = Field(Grid(), null, a, b);
            var tracker = new AscensionTracker();
            tracker.Update(field, a, new Random(1));

            a.LoseSouls(1);
            tracker.Update(field, null, new Random(1));

            Assert.AreEqual(2, tracker.AwakenedId);
        }

        [Test]
        public void Update_TileStaysFixedWhileAwakened()
        {
            var a = new ApostleState(1, new HexCoord(0, 0), 20);
            Battlefield field = Field(Grid(), null, a);
            var tracker = new AscensionTracker();
            tracker.Update(field, null, new Random(1));

            a.MoveTo(new HexCoord(3, 1));
            tracker.Update(field, null, new Random(1));

            Assert.AreEqual(new HexCoord(3, 2), tracker.Tile);
        }

        [Test]
        public void HasAscended_OnlyWhenTheAwakenedApostleStandsOnTheTile()
        {
            var a = new ApostleState(1, new HexCoord(0, 0), 20);
            var b = new ApostleState(2, new HexCoord(3, 1), 0);
            Battlefield field = Field(Grid(), null, a, b);
            var tracker = new AscensionTracker();
            tracker.Update(field, null, new Random(1));
            Assert.IsFalse(tracker.HasAscended(field));

            b.MoveTo(new HexCoord(3, 2));
            Assert.IsFalse(tracker.HasAscended(field), "Another Apostle on the tile must not count");

            b.MoveTo(new HexCoord(2, 2));
            a.MoveTo(new HexCoord(3, 2));
            Assert.IsTrue(tracker.HasAscended(field));
        }

        [Test]
        public void Update_NullArguments_Throw()
        {
            var a = new ApostleState(1, new HexCoord(0, 0), 0);
            Battlefield field = Field(Grid(), null, a);
            var tracker = new AscensionTracker();

            Assert.Throws<ArgumentNullException>(() => tracker.Update(null, null, new Random(1)));
            Assert.Throws<ArgumentNullException>(() => tracker.Update(field, null, null));
            Assert.Throws<ArgumentNullException>(() => tracker.HasAscended(null));
        }

        // ---- Integration with Match ----

        private static Match FourPlayerMatch()
        {
            HexBoard board = StandardBoard.Create();
            return Match.Create(4, board, SpawnPlanner.PickSpread(board, 4), new Random(1));
        }

        private static HexCoord FreeNeighbor(Battlefield field, HexCoord tile)
        {
            IReadOnlyList<HexCoord> neighbors = field.Board.GetNeighborsOnBoard(tile);
            for (int i = 0; i < neighbors.Count; i++)
            {
                if (!field.IsOccupied(neighbors[i]))
                    return neighbors[i];
            }
            throw new InvalidOperationException($"No free neighbor next to {tile}.");
        }

        [Test]
        public void Match_NewMatch_HasNoAwakenedApostleAndNoEndReason()
        {
            Match match = FourPlayerMatch();

            Assert.IsFalse(match.Ascension.HasAwakened);
            Assert.AreEqual(MatchEndReason.None, match.EndReason);
        }

        [Test]
        public void Match_ReachingTwentySouls_AwakensOnAFreeNonSpecialTile()
        {
            Match match = FourPlayerMatch();
            ApostleState current = match.CurrentApostle;
            current.AddSouls(GameConstants.SoulsToAwaken - current.Souls);

            Assert.IsFalse(match.RefreshGameState());

            HexCoord tile = match.Ascension.Tile;
            Assert.AreEqual(current.Id, match.Ascension.AwakenedId);
            Assert.IsTrue(match.Battlefield.Board.Contains(tile));
            Assert.IsFalse(match.Battlefield.IsOccupied(tile));
            Assert.IsFalse(match.Battlefield.Specials.Active.ContainsKey(tile));
        }

        [Test]
        public void Match_SteppingOnSoulTile_CanAwaken()
        {
            Match match = FourPlayerMatch();
            ApostleState current = match.CurrentApostle;

            HexCoord soulTile = default;
            foreach (KeyValuePair<HexCoord, SpecialTileType> pair in match.Battlefield.Specials.Active)
            {
                if (pair.Value == SpecialTileType.Soul)
                {
                    soulTile = pair.Key;
                    break;
                }
            }

            current.MoveTo(FreeNeighbor(match.Battlefield, soulTile));
            current.AddSouls(GameConstants.SoulsToAwaken - GameConstants.SoulTileAmount - current.Souls);

            Assert.AreEqual(MoveResult.Success, match.Move(soulTile));
            Assert.AreEqual(GameConstants.SoulsToAwaken, current.Souls);
            Assert.AreEqual(current.Id, match.Ascension.AwakenedId);
        }

        [Test]
        public void Match_AwakenedApostleReachesAscensionTile_Wins()
        {
            Match match = FourPlayerMatch();
            ApostleState current = match.CurrentApostle;
            current.AddSouls(GameConstants.SoulsToAwaken - current.Souls);
            match.RefreshGameState();

            HexCoord tile = match.Ascension.Tile;
            current.MoveTo(FreeNeighbor(match.Battlefield, tile));

            Assert.AreEqual(MoveResult.Success, match.Move(tile));
            Assert.IsTrue(match.IsOver);
            Assert.AreEqual(current.Id, match.WinnerId);
            Assert.AreEqual(MatchEndReason.Ascension, match.EndReason);
            Assert.IsNull(match.CurrentApostle);
        }

        [Test]
        public void Match_LastApostleStanding_SetsEndReason()
        {
            HexBoard board = StandardBoard.Create();
            Match match = Match.Create(3, board, SpawnPlanner.PickSpread(board, 3), new Random(1));
            IReadOnlyList<ApostleState> apostles = match.Battlefield.Apostles;

            apostles[0].TakeDamage(GameConstants.StartingVessel);
            apostles[1].TakeDamage(GameConstants.StartingVessel);

            Assert.IsTrue(match.RefreshGameState());
            Assert.AreEqual(apostles[2].Id, match.WinnerId);
            Assert.AreEqual(MatchEndReason.LastApostleStanding, match.EndReason);
        }
    }
}