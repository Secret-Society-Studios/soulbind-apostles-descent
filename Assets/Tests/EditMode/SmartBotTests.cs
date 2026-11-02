using System;
using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests.EditMode
{
    /// <summary>
    /// SmartBot tries plays on a copy of the match: the real match must not change while it thinks,
    /// and every action it picks must be one the rules allow.
    /// </summary>
    public class SmartBotTests
    {
        private static Match NewMatch(int players, int seed)
        {
            HexBoard board = StandardBoard.Create();
            return Match.Create(players, board, SpawnPlanner.PickSpread(board, players), new Random(seed));
        }

        // Thinking must leave the real match exactly as it was.
        [Test]
        public void Decide_DoesNotChangeTheMatch()
        {
            Match match = NewMatch(4, 3);
            var bot = new SmartBot();
            for (int i = 0; i < 40 && !match.IsOver; i++)
            {
                byte[] before = match.CaptureSnapshot(0).ToBytes();
                BotAction action = bot.Decide(match);
                byte[] after = match.CaptureSnapshot(0).ToBytes();
                CollectionAssert.AreEqual(before, after, $"Deciding '{action}' changed the match");
                if (!SimpleBot.Apply(match, action) && match.CurrentApostle != null)
                    match.EndTurn();
            }
        }

        // Every action it picks on a free turn is one the rules accept.
        [Test]
        public void Decide_PicksLegalActions()
        {
            Match match = NewMatch(2, 11);
            var bot = new SmartBot();
            int refused = 0;
            for (int i = 0; i < 200 && !match.IsOver; i++)
            {
                BotAction action = bot.Decide(match);
                if (SimpleBot.Apply(match, action))
                    continue;
                refused++;
                if (match.CurrentApostle != null)
                    match.EndTurn();
            }
            Assert.AreEqual(0, refused);
        }

        // A short batch with SmartBot plays every match and fills in cards and combos.
        [Test]
        public void BalanceRun_WithSmartBot_PlaysEveryMatch()
        {
            BalanceReport report = BalanceSimulator.Run(2, 2, 5, new SmartBot());

            Assert.AreEqual(2, report.Matches);
            Assert.AreEqual("SmartBot", report.BotName);
            Assert.AreEqual(ElementCombo.ElementCount, report.Combos.Count);
            int plays = 0;
            foreach (CardStat stat in report.Cards)
                plays += stat.Plays;
            Assert.Greater(plays, 0);
        }

        // Every Apostle of a finished match either held a card at some point or never did.
        [Test]
        public void BalanceRun_HadAndNotHad_CoverEveryApostle()
        {
            BalanceReport report = BalanceSimulator.Run(3, 4, 9);
            int finished = report.Matches - report.Stalled;
            foreach (CardStat stat in report.Cards)
            {
                Assert.AreEqual(finished * report.Players, stat.Had + stat.NotHad, stat.Card.ToString());
                Assert.LessOrEqual(stat.Holders, stat.Had, stat.Card.ToString());
            }
        }
    }
}
