using System;
using System.Collections.Generic;
using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests.EditMode
{
    /// <summary>Commands sent over the network: they survive encoding, and only the acting Apostle's are applied.</summary>
    public class MatchCommandTests
    {
        private static Match NewMatch()
        {
            HexBoard board = StandardBoard.Create();
            return Match.Create(3, board, SpawnPlanner.PickSpread(board, 3), new Random(4));
        }

        private static int SomeoneElse(Match match)
        {
            foreach (ApostleState apostle in match.Battlefield.Apostles)
            {
                if (apostle != match.CurrentApostle)
                    return apostle.Id;
            }
            return 0;
        }

        [Test]
        public void EveryKindOfCommand_SurvivesEncoding()
        {
            MatchCommand[] commands =
            {
                MatchCommand.ChooseDirection(TurnDirection.Backward),
                MatchCommand.Move(new HexCoord(-3, 7)),
                MatchCommand.PlayCard(ArcanaId.Devil, 2, 3),
                MatchCommand.Strike(),
                MatchCommand.EndTurn(),
            };
            var buffer = new byte[MatchCommand.EncodedSize + 4];

            foreach (MatchCommand original in commands)
            {
                original.Encode(buffer, 2);
                Assert.IsTrue(MatchCommand.TryDecode(buffer, 2, out MatchCommand copy), original.ToString());
                Assert.AreEqual(original.Kind, copy.Kind);
                Assert.AreEqual(original.Destination, copy.Destination);
                Assert.AreEqual(original.Card, copy.Card);
                Assert.AreEqual(original.TargetId, copy.TargetId);
                Assert.AreEqual(original.ComboTargetId, copy.ComboTargetId);
                Assert.AreEqual(original.Direction, copy.Direction);
            }
        }

        [Test]
        public void GarbageBytes_AreNotACommand()
        {
            var buffer = new byte[MatchCommand.EncodedSize];
            buffer[0] = 200; // no such kind
            Assert.IsFalse(MatchCommand.TryDecode(buffer, 0, out _));
            Assert.IsFalse(MatchCommand.TryDecode(new byte[3], 0, out _), "Too short");

            buffer[0] = (byte)CommandKind.PlayCard;
            buffer[5] = 99; // no such card
            Assert.IsFalse(MatchCommand.TryDecode(buffer, 0, out _));
        }

        [Test]
        public void ACommandFromSomeoneElse_IsIgnored()
        {
            Match match = NewMatch();
            int current = match.CurrentApostle.Id;

            CommandResult result = MatchCommands.Apply(match, SomeoneElse(match), MatchCommand.EndTurn());

            Assert.AreEqual(CommandResult.NotYourTurn, result);
            Assert.AreEqual(current, match.CurrentApostle.Id, "The turn did not change");
        }

        [Test]
        public void TheCurrentApostlesCommand_IsApplied()
        {
            Match match = NewMatch();
            int current = match.CurrentApostle.Id;

            Assert.AreEqual(CommandResult.Applied, MatchCommands.Apply(match, current, MatchCommand.EndTurn()));
            Assert.AreNotEqual(current, match.CurrentApostle.Id);
        }

        [Test]
        public void AMoveTheRulesRefuse_ChangesNothing()
        {
            Match match = NewMatch();
            ApostleState me = match.CurrentApostle;
            HexCoord start = me.Position;
            int capacity = match.CurrentCapacity.Remaining;

            // Two tiles away is never a legal single move.
            var far = new HexCoord(start.Q + 2, start.R);
            CommandResult result = MatchCommands.Apply(match, me.Id, MatchCommand.Move(far));

            Assert.AreEqual(CommandResult.Refused, result);
            Assert.AreEqual(start, me.Position);
            Assert.AreEqual(capacity, match.CurrentCapacity.Remaining);
        }

        [Test]
        public void ALegalMove_IsApplied()
        {
            Match match = NewMatch();
            var legal = new List<HexCoord>();
            match.GetLegalDestinations(legal);
            Assume.That(legal.Count, Is.GreaterThan(0));

            Assert.AreEqual(CommandResult.Applied, MatchCommands.Apply(match, match.CurrentApostle.Id, MatchCommand.Move(legal[0])));
            Assert.AreEqual(legal[0], match.CurrentApostle.Position);
        }

        [Test]
        public void ChoosingADirection_DuringATurn_IsRefused()
        {
            Match match = NewMatch();
            Assert.AreEqual(CommandResult.Refused,
                MatchCommands.Apply(match, match.CurrentApostle.Id, MatchCommand.ChooseDirection(TurnDirection.Forward)));
        }

        [Test]
        public void BetweenPhases_OnlyTheChooserMayPickTheDirection()
        {
            Match match = NewMatch();
            for (int i = 0; i < 100 && !match.IsAwaitingPhaseStart; i++)
                MatchCommands.Apply(match, MatchCommands.GetActorId(match), MatchCommand.EndTurn());
            Assume.That(match.IsAwaitingPhaseStart, "A Phase should end after 3 rounds");

            int chooser = match.GetNextPhaseChooserId();
            int other = chooser == 1 ? 2 : 1;
            Assert.AreEqual(CommandResult.NotYourTurn,
                MatchCommands.Apply(match, other, MatchCommand.ChooseDirection(TurnDirection.Forward)));
            Assert.AreEqual(CommandResult.Refused, MatchCommands.Apply(match, chooser, MatchCommand.EndTurn()));
            Assert.AreEqual(CommandResult.Applied,
                MatchCommands.Apply(match, chooser, MatchCommand.ChooseDirection(TurnDirection.Forward)));
            Assert.IsFalse(match.IsAwaitingPhaseStart);
        }
    }
}