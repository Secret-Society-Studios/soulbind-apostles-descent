using System;
using System.Collections.Generic;
using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests.EditMode
{
    public class MatchTests
    {
        private static List<HexCoord> Spawns(HexBoard board, int count)
        {
            var spawns = new List<HexCoord>();
            for (int i = 0; i < count; i++)
                spawns.Add(board.EdgeTiles[i]);
            return spawns;
        }

        private static Match MakeMatch(int players, int seed = 1)
        {
            HexBoard board = StandardBoard.Create();
            return Match.Create(players, board, Spawns(board, players), new Random(seed));
        }

        // Uses the same seed as MakeMatch, so this is the seat order the match will use.
        private static int[] SeatOrder(int players, int seed = 1) =>
            TurnTracker.CreateRandomSeatOrder(players, new Random(seed));

        private static ApostleState Get(Match match, int id)
        {
            foreach (ApostleState apostle in match.Battlefield.Apostles)
            {
                if (apostle.Id == id)
                    return apostle;
            }
            throw new InvalidOperationException($"No Apostle with id {id}.");
        }

        // ---- Creation ----

        [TestCase(2, 13)]
        [TestCase(3, 9)]
        [TestCase(4, 7)]
        [TestCase(5, 6)]
        [TestCase(6, 5)]
        public void Create_SetsStartingValues(int players, int expectedSouls)
        {
            var match = MakeMatch(players);

            Assert.AreEqual(players, match.Battlefield.Apostles.Count);
            for (int id = 1; id <= players; id++)
            {
                ApostleState apostle = Get(match, id);
                Assert.AreEqual(expectedSouls, apostle.Souls);
                Assert.AreEqual(50, apostle.Vessel);
                Assert.AreEqual(0, apostle.Corruption);
            }
            Assert.IsFalse(match.IsOver);
        }

        [Test]
        public void Create_PlacesApostlesOnTheGivenSpawnTiles()
        {
            HexBoard board = StandardBoard.Create();
            List<HexCoord> spawns = Spawns(board, 3);
            var match = Match.Create(3, board, spawns, new Random(1));

            for (int i = 0; i < 3; i++)
                Assert.AreEqual(spawns[i], Get(match, i + 1).Position);
        }

        [Test]
        public void Create_FirstApostleFollowsSeatOrder()
        {
            var match = MakeMatch(4, 5);

            Assert.AreEqual(SeatOrder(4, 5)[0], match.CurrentApostle.Id);
            Assert.AreEqual(1, match.Turns.RoundNumber);
            Assert.AreEqual(8, match.CurrentCapacity.Maximum);
        }

        [Test]
        public void Create_InvalidArguments_Throw()
        {
            HexBoard board = StandardBoard.Create();
            List<HexCoord> two = Spawns(board, 2);

            Assert.Throws<ArgumentNullException>(() => Match.Create(2, null, two, new Random(1)));
            Assert.Throws<ArgumentNullException>(() => Match.Create(2, board, null, new Random(1)));
            Assert.Throws<ArgumentNullException>(() => Match.Create(2, board, two, null));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Match.Create(1, board, Spawns(board, 1), new Random(1)));
            Assert.Throws<ArgumentException>(() => Match.Create(3, board, two, new Random(1)));

            var interior = new List<HexCoord> { new HexCoord(4, 6), board.EdgeTiles[0] };
            Assert.Throws<ArgumentException>(() => Match.Create(2, board, interior, new Random(1)));

            var duplicate = new List<HexCoord> { board.EdgeTiles[0], board.EdgeTiles[0] };
            Assert.Throws<ArgumentException>(() => Match.Create(2, board, duplicate, new Random(1)));
        }

        // ---- Turn flow ----

        [Test]
        public void Move_SpendsCapacityAndMovesTheCurrentApostle()
        {
            var match = MakeMatch(2);
            ApostleState mover = match.CurrentApostle;
            var options = new List<HexCoord>();
            match.Battlefield.GetLegalDestinations(mover, match.CurrentCapacity, options);
            Assert.IsNotEmpty(options);

            MoveResult result = match.Move(options[0]);

            Assert.AreEqual(MoveResult.Success, result);
            Assert.AreEqual(options[0], mover.Position);
            Assert.AreEqual(6, match.CurrentCapacity.Remaining);
        }

        [Test]
        public void EndTurn_PassesTheTurnToTheNextSeat_WithFreshCapacity()
        {
            var match = MakeMatch(3, 2);
            int[] order = SeatOrder(3, 2);
            Assert.AreEqual(order[0], match.CurrentApostle.Id);
            match.CurrentCapacity.Spend(4);

            Assert.AreEqual(TurnEvent.NextTurn, match.EndTurn());

            Assert.AreEqual(order[1], match.CurrentApostle.Id);
            Assert.AreEqual(8, match.CurrentCapacity.Remaining);
        }

        [Test]
        public void EndTurn_AfterThreeRounds_AwaitsThePhaseStart()
        {
            var match = MakeMatch(2);

            for (int i = 0; i < 5; i++)
                Assert.AreNotEqual(TurnEvent.PhaseEnded, match.EndTurn());
            Assert.AreEqual(TurnEvent.PhaseEnded, match.EndTurn());

            Assert.IsTrue(match.IsAwaitingPhaseStart);
            Assert.IsNull(match.CurrentApostle);
            Assert.IsNull(match.CurrentCapacity);
        }

        [Test]
        public void BeginNextPhase_LowestVesselGoesFirst_WithPhaseCapacity()
        {
            var match = MakeMatch(2);
            for (int i = 0; i < 6; i++)
                match.EndTurn();
            Get(match, 2).TakeDamage(10);

            match.BeginNextPhase(TurnDirection.Forward);

            Assert.AreEqual(2, match.CurrentApostle.Id);
            Assert.AreEqual(12, match.CurrentCapacity.Maximum);
            Assert.AreEqual(4, match.Turns.RoundNumber);
            Assert.IsFalse(match.IsAwaitingPhaseStart);
        }

        [Test]
        public void Actions_WhileAwaitingPhaseStart_Throw()
        {
            var match = MakeMatch(2);
            for (int i = 0; i < 6; i++)
                match.EndTurn();

            Assert.Throws<InvalidOperationException>(() => match.EndTurn());
            Assert.Throws<InvalidOperationException>(() => match.Move(new HexCoord(0, 0)));
        }

        [Test]
        public void BeginNextPhase_WhenThePhaseHasNotEnded_Throws()
        {
            var match = MakeMatch(2);
            Assert.Throws<InvalidOperationException>(() => match.BeginNextPhase(TurnDirection.Forward));
        }

        [Test]
        public void GetNextPhaseChooserId_IsTheLowestVesselApostle()
        {
            var match = MakeMatch(3);
            Get(match, 3).TakeDamage(5);

            Assert.AreEqual(3, match.GetNextPhaseChooserId());
        }

        // ---- Start-of-turn effects ----

        [Test]
        public void StartOfTurn_AppliesCorruptionDamage()
        {
            var match = MakeMatch(3);
            ApostleState current = match.CurrentApostle;
            foreach (ApostleState apostle in match.Battlefield.Apostles)
            {
                if (apostle != current)
                    apostle.AddCorruption(5);
            }

            match.EndTurn();

            Assert.AreEqual(48, match.CurrentApostle.Vessel);
        }

        [Test]
        public void StartOfTurn_CorruptionDeath_SkipsToTheNextLivingApostle()
        {
            var match = MakeMatch(3, 3);
            int[] order = SeatOrder(3, 3);
            ApostleState second = Get(match, order[1]);
            second.TakeDamage(49);
            second.AddCorruption(5);

            match.EndTurn();

            Assert.IsTrue(second.IsEliminated);
            Assert.AreEqual(order[2], match.CurrentApostle.Id);
            Assert.IsFalse(match.IsOver);
        }

        [Test]
        public void RivalsEliminatedByCorruption_EndTheMatch()
        {
            var match = MakeMatch(3);
            ApostleState first = match.CurrentApostle;
            foreach (ApostleState apostle in match.Battlefield.Apostles)
            {
                if (apostle == first)
                    continue;
                apostle.TakeDamage(49);
                apostle.AddCorruption(5);
            }

            match.EndTurn();

            Assert.IsTrue(match.IsOver);
            Assert.AreEqual(first.Id, match.WinnerId);
            Assert.IsNull(match.CurrentApostle);
        }

        // ---- Ending the match ----

        [Test]
        public void RefreshGameState_EndsTheMatch_WhenOnlyOneApostleLives()
        {
            var match = MakeMatch(2);
            ApostleState survivor = match.CurrentApostle;
            Get(match, survivor.Id == 1 ? 2 : 1).TakeDamage(50);

            Assert.IsTrue(match.RefreshGameState());

            Assert.IsTrue(match.IsOver);
            Assert.AreEqual(survivor.Id, match.WinnerId);
            Assert.IsNull(match.CurrentApostle);
        }

        [Test]
        public void Actions_WhenTheMatchIsOver_Throw()
        {
            var match = MakeMatch(2);
            Get(match, match.CurrentApostle.Id == 1 ? 2 : 1).TakeDamage(50);
            match.RefreshGameState();

            Assert.Throws<InvalidOperationException>(() => match.EndTurn());
            Assert.Throws<InvalidOperationException>(() => match.Move(new HexCoord(0, 0)));
            Assert.Throws<InvalidOperationException>(() => match.BeginNextPhase(TurnDirection.Forward));
        }

        [Test]
        public void RefreshGameState_WhenNobodyLives_HasNoWinner()
        {
            var match = MakeMatch(2);
            Get(match, 1).TakeDamage(50);
            Get(match, 2).TakeDamage(50);

            Assert.IsTrue(match.RefreshGameState());

            Assert.IsTrue(match.IsOver);
            Assert.AreEqual(0, match.WinnerId);
        }
    }
}