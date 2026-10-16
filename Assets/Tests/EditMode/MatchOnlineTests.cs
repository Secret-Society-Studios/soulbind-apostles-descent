using System;
using System.Collections.Generic;
using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests.EditMode
{
    /// <summary>A player's screen knows from its own copy when the Nocturne combo needs a target.</summary>
    public class MatchOnlineTests
    {
        [Test]
        public void NeedsComboTarget_AgreesWithPlayCard_OnTheHostAndOnACopy()
        {
            // Both Apostles get High Priestess, Hermit (two Nocturne cards) and three Fools.
            var stack = new List<ArcanaId>();
            foreach (ArcanaId card in new[] { ArcanaId.HighPriestess, ArcanaId.Hermit, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool })
            {
                stack.Add(card);
                stack.Add(card);
            }
            for (int i = 0; i < 20; i++)
                stack.Add(ArcanaId.Fool);
            HexBoard board = StandardBoard.Create();
            Match host = Match.Create(2, board, SpawnPlanner.PickSpread(board, 2), new Random(1),
                Deck.CreateStacked(stack, new Random(2)));
            int player = host.CurrentApostle.Id;

            Assert.IsFalse(host.NeedsComboTarget(ArcanaId.Hermit, 0), "The first Nocturne card triggers nothing");
            Assert.AreEqual(CardPlayResult.Success, host.PlayCard(ArcanaId.HighPriestess, 0));

            Match copy = Match.FromSnapshot(board, host.CaptureSnapshot(player));
            Assert.IsTrue(host.NeedsComboTarget(ArcanaId.Hermit, 0));
            Assert.IsTrue(copy.NeedsComboTarget(ArcanaId.Hermit, 0), "The player's copy must agree with the host");
            Assert.AreEqual(CardPlayResult.ComboTargetRequired, host.PlayCard(ArcanaId.Hermit, 0));
        }
    }
}