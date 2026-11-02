using System.Collections.Generic;
using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests.EditMode
{
    public class TurnTimerTests
    {
        [TestCase(10f, TurnTimer.MinSeconds)]
        [TestCase(38f, 38f)]
        [TestCase(90f, TurnTimer.MaxSeconds)]
        public void Limit_IsKeptBetween30And45(float asked, float expected)
        {
            Assert.AreEqual(expected, new TurnTimer(asked).Limit);
        }

        [Test]
        public void Tick_RunsOutOnce()
        {
            var timer = new TurnTimer(30f);

            Assert.IsFalse(timer.Tick(5, 29f));
            Assert.IsTrue(timer.Tick(5, 2f));
            Assert.IsFalse(timer.Tick(5, 2f));
            Assert.AreEqual(0f, timer.Remaining);
            Assert.IsFalse(timer.IsRunning);
        }

        [Test]
        public void Tick_NewTurnKey_RestartsTheCount()
        {
            var timer = new TurnTimer(30f);
            timer.Tick(5, 20f);

            timer.Tick(6, 0f);

            Assert.AreEqual(30f, timer.Remaining);
            Assert.IsTrue(timer.IsRunning);
        }

        [Test]
        public void Tick_NobodyActing_DoesNotCount()
        {
            var timer = new TurnTimer(30f);

            Assert.IsFalse(timer.Tick(TurnTimer.NoTurn, 100f));
            Assert.IsFalse(timer.IsRunning);
        }

        [Test]
        public void TurnKey_ChangesWhenTheTurnEnds()
        {
            HexBoard board = StandardBoard.Create();
            var spawns = new List<HexCoord> { board.EdgeTiles[0], board.EdgeTiles[board.EdgeTiles.Count / 2] };
            Match match = Match.Create(2, board, spawns, new System.Random(1));
            int first = TurnTimer.GetTurnKey(match);

            match.EndTurn();

            Assert.AreNotEqual(TurnTimer.NoTurn, first);
            Assert.AreNotEqual(first, TurnTimer.GetTurnKey(match));
        }

        [Test]
        public void TurnKey_NullMatch_IsNoTurn()
        {
            Assert.AreEqual(TurnTimer.NoTurn, TurnTimer.GetTurnKey(null));
        }
    }
}
