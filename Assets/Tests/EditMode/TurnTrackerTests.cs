using System;
using System.Collections.Generic;
using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests.EditMode
{
    public class TurnTrackerTests
    {
        private static List<ApostleState> Apostles(params int[] ids)
        {
            var list = new List<ApostleState>();
            foreach (int id in ids)
                list.Add(new ApostleState(id, new HexCoord(id, 0), 10));
            return list;
        }

        private static ApostleState ById(List<ApostleState> apostles, int id) =>
            apostles.Find(a => a.Id == id);

        // Ends turns until the Phase is over.
        private static void RunToPhaseEnd(TurnTracker tracker, List<ApostleState> apostles)
        {
            for (int guard = 0; guard < 100; guard++)
            {
                if (tracker.EndTurn(apostles) == TurnEvent.PhaseEnded)
                    return;
            }
            Assert.Fail("The phase never ended.");
        }

        [Test]
        public void NewTracker_StartsAtRound1Phase1WithFirstSeat()
        {
            var tracker = new TurnTracker(new[] { 3, 1, 2 });

            Assert.AreEqual(3, tracker.CurrentApostleId);
            Assert.AreEqual(1, tracker.RoundNumber);
            Assert.AreEqual(1, tracker.Phase);
            Assert.AreEqual(TurnDirection.Forward, tracker.Direction);
            Assert.IsFalse(tracker.IsAwaitingPhaseStart);
        }

        [Test]
        public void EndTurn_WalksSeatsInOrder_ThenStartsNewRound()
        {
            var apostles = Apostles(1, 2, 3);
            var tracker = new TurnTracker(new[] { 3, 1, 2 });

            Assert.AreEqual(TurnEvent.NextTurn, tracker.EndTurn(apostles));
            Assert.AreEqual(1, tracker.CurrentApostleId);
            Assert.AreEqual(TurnEvent.NextTurn, tracker.EndTurn(apostles));
            Assert.AreEqual(2, tracker.CurrentApostleId);
            Assert.AreEqual(TurnEvent.NewRound, tracker.EndTurn(apostles));
            Assert.AreEqual(3, tracker.CurrentApostleId);
            Assert.AreEqual(2, tracker.RoundNumber);
        }

        [Test]
        public void ThirdRound_EndsThePhase()
        {
            var apostles = Apostles(1, 2, 3);
            var tracker = new TurnTracker(new[] { 1, 2, 3 });

            for (int i = 0; i < 8; i++)
                Assert.AreNotEqual(TurnEvent.PhaseEnded, tracker.EndTurn(apostles));

            Assert.AreEqual(TurnEvent.PhaseEnded, tracker.EndTurn(apostles));
            Assert.IsTrue(tracker.IsAwaitingPhaseStart);
            Assert.AreEqual(3, tracker.RoundNumber);
            Assert.AreEqual(1, tracker.Phase);
        }

        [Test]
        public void CurrentApostleId_WhilePhaseEnded_Throws()
        {
            var apostles = Apostles(1, 2);
            var tracker = new TurnTracker(new[] { 1, 2 });
            RunToPhaseEnd(tracker, apostles);

            Assert.Throws<InvalidOperationException>(() => { int id = tracker.CurrentApostleId; });
        }

        [Test]
        public void EndTurn_WhilePhaseEnded_Throws()
        {
            var apostles = Apostles(1, 2);
            var tracker = new TurnTracker(new[] { 1, 2 });
            RunToPhaseEnd(tracker, apostles);

            Assert.Throws<InvalidOperationException>(() => tracker.EndTurn(apostles));
        }

        [Test]
        public void BeginNextPhase_LowestVesselGoesFirst()
        {
            var apostles = Apostles(1, 2, 3);
            var tracker = new TurnTracker(new[] { 1, 2, 3 });
            RunToPhaseEnd(tracker, apostles);
            ById(apostles, 2).TakeDamage(20);

            tracker.BeginNextPhase(apostles, TurnDirection.Forward);

            Assert.AreEqual(2, tracker.CurrentApostleId);
            Assert.AreEqual(4, tracker.RoundNumber);
            Assert.AreEqual(2, tracker.Phase);
            Assert.IsFalse(tracker.IsAwaitingPhaseStart);
        }

        [Test]
        public void BeginNextPhase_Forward_FollowsSeatOrder()
        {
            var apostles = Apostles(1, 2, 3);
            var tracker = new TurnTracker(new[] { 1, 2, 3 });
            RunToPhaseEnd(tracker, apostles);
            ById(apostles, 2).TakeDamage(10);

            tracker.BeginNextPhase(apostles, TurnDirection.Forward);

            Assert.AreEqual(2, tracker.CurrentApostleId);
            tracker.EndTurn(apostles);
            Assert.AreEqual(3, tracker.CurrentApostleId);
            tracker.EndTurn(apostles);
            Assert.AreEqual(1, tracker.CurrentApostleId);
        }

        [Test]
        public void BeginNextPhase_Backward_ReversesSeatOrder()
        {
            var apostles = Apostles(1, 2, 3);
            var tracker = new TurnTracker(new[] { 1, 2, 3 });
            RunToPhaseEnd(tracker, apostles);
            ById(apostles, 2).TakeDamage(10);

            tracker.BeginNextPhase(apostles, TurnDirection.Backward);

            Assert.AreEqual(2, tracker.CurrentApostleId);
            tracker.EndTurn(apostles);
            Assert.AreEqual(1, tracker.CurrentApostleId);
            tracker.EndTurn(apostles);
            Assert.AreEqual(3, tracker.CurrentApostleId);
        }

        [Test]
        public void BeginNextPhase_VesselTie_GoesToEarliestSeat()
        {
            var apostles = Apostles(1, 2, 3);
            var tracker = new TurnTracker(new[] { 3, 1, 2 });
            RunToPhaseEnd(tracker, apostles);
            ById(apostles, 1).TakeDamage(10);
            ById(apostles, 2).TakeDamage(10);

            tracker.BeginNextPhase(apostles, TurnDirection.Forward);

            Assert.AreEqual(1, tracker.CurrentApostleId);
        }

        [Test]
        public void BeginNextPhase_BeforePhaseEnds_Throws()
        {
            var apostles = Apostles(1, 2);
            var tracker = new TurnTracker(new[] { 1, 2 });

            Assert.Throws<InvalidOperationException>(
                () => tracker.BeginNextPhase(apostles, TurnDirection.Forward));
        }

        [Test]
        public void EliminatedApostle_IsSkippedMidRound()
        {
            var apostles = Apostles(1, 2, 3);
            var tracker = new TurnTracker(new[] { 1, 2, 3 });
            ById(apostles, 2).TakeDamage(50);

            Assert.AreEqual(TurnEvent.NextTurn, tracker.EndTurn(apostles));
            Assert.AreEqual(3, tracker.CurrentApostleId);
        }

        [Test]
        public void EliminatedApostle_IsExcludedFromLaterRounds()
        {
            var apostles = Apostles(1, 2, 3);
            var tracker = new TurnTracker(new[] { 1, 2, 3 });
            ById(apostles, 2).TakeDamage(50);

            tracker.EndTurn(apostles);                                    // 1 -> 3
            Assert.AreEqual(TurnEvent.NewRound, tracker.EndTurn(apostles)); // round 2
            Assert.AreEqual(1, tracker.CurrentApostleId);
            tracker.EndTurn(apostles);
            Assert.AreEqual(3, tracker.CurrentApostleId);
        }

        [Test]
        public void NextRound_StartsWithNextLivingApostle_IfFirstWasEliminated()
        {
            var apostles = Apostles(1, 2, 3);
            var tracker = new TurnTracker(new[] { 1, 2, 3 });
            ById(apostles, 1).TakeDamage(50);

            tracker.EndTurn(apostles);                                    // 1 is dead, so -> 2
            Assert.AreEqual(2, tracker.CurrentApostleId);
            tracker.EndTurn(apostles);                                    // -> 3
            Assert.AreEqual(TurnEvent.NewRound, tracker.EndTurn(apostles));
            Assert.AreEqual(2, tracker.CurrentApostleId);
        }

        [Test]
        public void CreateTurnCapacity_FollowsThePhase()
        {
            var apostles = Apostles(1, 2);
            var tracker = new TurnTracker(new[] { 1, 2 });
            Assert.AreEqual(8, tracker.CreateTurnCapacity().Maximum);

            RunToPhaseEnd(tracker, apostles);
            tracker.BeginNextPhase(apostles, TurnDirection.Forward);

            Assert.AreEqual(12, tracker.CreateTurnCapacity().Maximum);
        }

        [Test]
        public void Constructor_InvalidSeatOrder_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new TurnTracker(null));
            Assert.Throws<ArgumentException>(() => new TurnTracker(new[] { 1 }));
            Assert.Throws<ArgumentException>(() => new TurnTracker(new[] { 1, 2, 3, 4, 5, 6, 7 }));
            Assert.Throws<ArgumentException>(() => new TurnTracker(new[] { 1, 1 }));
        }

        [Test]
        public void CreateRandomSeatOrder_ReturnsEachIdOnce()
        {
            int[] order = TurnTracker.CreateRandomSeatOrder(6, new Random(7));

            Assert.AreEqual(6, order.Length);
            var sorted = (int[])order.Clone();
            Array.Sort(sorted);
            Assert.AreEqual(new[] { 1, 2, 3, 4, 5, 6 }, sorted);
        }

        [Test]
        public void CreateRandomSeatOrder_SameSeed_GivesSameOrder()
        {
            int[] a = TurnTracker.CreateRandomSeatOrder(5, new Random(42));
            int[] b = TurnTracker.CreateRandomSeatOrder(5, new Random(42));
            Assert.AreEqual(a, b);
        }

        [Test]
        public void CreateRandomSeatOrder_InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TurnTracker.CreateRandomSeatOrder(1, new Random(1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => TurnTracker.CreateRandomSeatOrder(7, new Random(1)));
            Assert.Throws<ArgumentNullException>(() => TurnTracker.CreateRandomSeatOrder(4, null));
        }

        [Test]
        public void GetLowestVesselApostleId_IgnoresEliminatedApostles()
        {
            var apostles = Apostles(1, 2, 3);
            var tracker = new TurnTracker(new[] { 1, 2, 3 });
            ById(apostles, 2).TakeDamage(50);
            ById(apostles, 3).TakeDamage(10);

            Assert.AreEqual(3, tracker.GetLowestVesselApostleId(apostles));
        }
    }
}