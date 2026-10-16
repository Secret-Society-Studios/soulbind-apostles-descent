using System;
using System.Collections.Generic;
using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests.EditMode
{
    /// <summary>
    /// Snapshots carry a match from the host to each player: every public number arrives intact,
    /// and other players' hands and the draw pile arrive blank.
    /// </summary>
    public class MatchSnapshotTests
    {
        private static Match NewMatch(int players, int seed, out HexBoard board)
        {
            board = StandardBoard.Create();
            return Match.Create(players, board, SpawnPlanner.PickSpread(board, players), new Random(seed));
        }

        // Lets bots play for a while, so the match has damage, Corruption, effects and discards.
        private static void PlaySome(Match match, int actions)
        {
            var bot = new SimpleBot();
            for (int i = 0; i < actions && !match.IsOver; i++)
            {
                BotAction action = bot.Decide(match);
                if (!SimpleBot.Apply(match, action) && match.CurrentApostle != null)
                    match.EndTurn();
            }
        }

        private static MatchSnapshot ThroughBytes(MatchSnapshot snapshot)
        {
            MatchSnapshot copy = MatchSnapshot.FromBytes(snapshot.ToBytes());
            Assert.IsNotNull(copy, "The bytes could not be read back");
            return copy;
        }

        // Everything a player can see must be the same. Hands are compared only for the viewer (0 = all).
        private static void AssertSameView(Match host, Match copy, int viewerId)
        {
            Assert.AreEqual(host.Battlefield.Apostles.Count, copy.Battlefield.Apostles.Count);
            for (int i = 0; i < host.Battlefield.Apostles.Count; i++)
            {
                ApostleState a = host.Battlefield.Apostles[i];
                ApostleState b = copy.Battlefield.Apostles[i];
                string who = $"Apostle {a.Id}";
                Assert.AreEqual(a.Id, b.Id, who);
                Assert.AreEqual(a.Position, b.Position, who);
                Assert.AreEqual(a.Vessel, b.Vessel, who);
                Assert.AreEqual(a.Souls, b.Souls, who);
                Assert.AreEqual(a.Corruption, b.Corruption, who);
                Assert.AreEqual(a.Shield, b.Shield, who);
                Assert.AreEqual(a.Defense, b.Defense, who);
                Assert.AreEqual(a.TurnsStarted, b.TurnsStarted, who);
                Assert.AreEqual(a.TimedEffects.Count, b.TimedEffects.Count, who);

                IReadOnlyList<ArcanaId> hostHand = host.GetHand(a.Id);
                IReadOnlyList<ArcanaId> copyHand = copy.GetHand(a.Id);
                Assert.AreEqual(hostHand.Count, copyHand.Count, who + " hand size");
                if (viewerId == 0 || viewerId == a.Id)
                    CollectionAssert.AreEqual(hostHand, copyHand, who + " hand");
            }

            Assert.AreEqual(host.CurrentApostle?.Id, copy.CurrentApostle?.Id, "Current Apostle");
            Assert.AreEqual(host.CurrentCapacity?.Maximum, copy.CurrentCapacity?.Maximum, "Capacity");
            Assert.AreEqual(host.CurrentCapacity?.Remaining, copy.CurrentCapacity?.Remaining, "Capacity left");
            Assert.AreEqual(host.Engagement, copy.Engagement);
            Assert.AreEqual(host.EngagedTargetId, copy.EngagedTargetId);
            Assert.AreEqual(host.PendingAttack, copy.PendingAttack);
            Assert.AreEqual(host.Turns.RoundNumber, copy.Turns.RoundNumber);
            Assert.AreEqual(host.Turns.Phase, copy.Turns.Phase);
            Assert.AreEqual(host.IsAwaitingPhaseStart, copy.IsAwaitingPhaseStart);
            Assert.AreEqual(host.Deck.DrawCount, copy.Deck.DrawCount);
            CollectionAssert.AreEqual(host.Deck.DiscardPile, copy.Deck.DiscardPile);
            Assert.AreEqual(host.Battlefield.Specials.Active.Count, copy.Battlefield.Specials.Active.Count);
            Assert.AreEqual(host.Ascension.AwakenedId, copy.Ascension.AwakenedId);
            Assert.AreEqual(host.IsOver, copy.IsOver);
            Assert.AreEqual(host.WinnerId, copy.WinnerId);
            for (int e = 0; e < ElementCombo.ElementCount; e++)
                Assert.AreEqual(host.IsComboTriggered((SoulBind.Core.Element)e), copy.IsComboTriggered((SoulBind.Core.Element)e));
        }

        [TestCase(2, 1, 0)]
        [TestCase(4, 2, 40)]
        [TestCase(6, 3, 150)]
        public void ACopyBuiltFromASnapshot_ShowsTheSameMatch(int players, int seed, int actions)
        {
            Match host = NewMatch(players, seed, out HexBoard board);
            PlaySome(host, actions);

            Match copy = Match.FromSnapshot(board, ThroughBytes(host.CaptureSnapshot(0)));

            AssertSameView(host, copy, 0);
        }

        [Test]
        public void AViewersSnapshot_HidesOtherHandsAndTheDrawPile()
        {
            Match host = NewMatch(4, 5, out HexBoard board);
            PlaySome(host, 20);
            int viewer = host.Battlefield.Apostles[1].Id;

            MatchSnapshot snapshot = ThroughBytes(host.CaptureSnapshot(viewer));
            Match copy = Match.FromSnapshot(board, snapshot);

            AssertSameView(host, copy, viewer);
            foreach (ApostleState apostle in copy.Battlefield.Apostles)
            {
                if (apostle.Id == viewer)
                    continue;
                foreach (ArcanaId card in copy.GetHand(apostle.Id))
                    Assert.AreEqual(MatchSnapshot.HiddenCard, card, $"Apostle {apostle.Id}'s cards must stay hidden");
            }
            foreach (ArcanaId card in snapshot.DrawPile)
                Assert.AreEqual(MatchSnapshot.HiddenCard, card, "The draw pile must stay hidden");
        }

        [Test]
        public void LaterSnapshots_UpdateTheSameCopy()
        {
            Match host = NewMatch(3, 7, out HexBoard board);
            int viewer = host.CurrentApostle.Id;
            Match copy = Match.FromSnapshot(board, ThroughBytes(host.CaptureSnapshot(viewer, 1)));
            Match sameObject = copy;

            for (int step = 2; step < 30; step++)
            {
                PlaySome(host, 5);
                copy.ApplySnapshot(ThroughBytes(host.CaptureSnapshot(viewer, step)));
                AssertSameView(host, copy, viewer);
            }
            Assert.AreSame(sameObject, copy);
        }

        [Test]
        public void ASnapshotTakenMidAttack_StrikesTheSameOnTheCopy()
        {
            Match host = NewMatch(3, 9, out HexBoard board);
            var bot = new SimpleBot();
            for (int i = 0; i < 2000 && !host.IsOver && host.Engagement != EngagementState.Engaged; i++)
            {
                if (!SimpleBot.Apply(host, bot.Decide(host)) && host.CurrentApostle != null)
                    host.EndTurn();
            }
            Assume.That(host.Engagement, Is.EqualTo(EngagementState.Engaged), "The bots should start an attack");

            Match copy = Match.FromSnapshot(board, ThroughBytes(host.CaptureSnapshot(0)));
            host.Strike(out _);
            copy.Strike(out _);

            AssertSameView(host, copy, 0);
        }

        [Test]
        public void DamagedBytes_AreRejected()
        {
            Match host = NewMatch(2, 11, out _);
            byte[] bytes = host.CaptureSnapshot(1).ToBytes();

            Assert.IsNull(MatchSnapshot.FromBytes(null));
            Assert.IsNull(MatchSnapshot.FromBytes(new byte[0]));
            byte[] cut = new byte[bytes.Length / 2];
            Array.Copy(bytes, cut, cut.Length);
            Assert.IsNull(MatchSnapshot.FromBytes(cut), "A cut-off snapshot");
            bytes[0] = 99;
            Assert.IsNull(MatchSnapshot.FromBytes(bytes), "Another format version");
        }

        [Test]
        public void ASnapshot_IsSmall()
        {
            Match host = NewMatch(6, 13, out _);
            PlaySome(host, 100);
            Assert.Less(host.CaptureSnapshot(1).ToBytes().Length, 1024, "Sent after every action, so it must stay small");
        }
    }
}