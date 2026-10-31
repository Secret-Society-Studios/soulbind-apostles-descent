using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests.EditMode
{
    public class BalanceSimulatorTests
    {
        // A few quick matches should all be played, and cards should get played.
        [Test]
        public void Run_PlaysEveryMatch()
        {
            BalanceReport report = BalanceSimulator.Run(2, 3, 1);

            Assert.AreEqual(3, report.Matches);
            Assert.AreEqual(CardCatalog.All.Count, report.Cards.Count);
            int plays = 0;
            foreach (CardStat stat in report.Cards)
                plays += stat.Plays;
            Assert.Greater(plays, 0);
            Assert.IsNotEmpty(report.ToText());
            Assert.IsNotEmpty(report.ToCsv());
        }

        // The same seed must give the same results.
        [Test]
        public void Run_SameSeed_SameResults()
        {
            BalanceReport a = BalanceSimulator.Run(4, 2, 7);
            BalanceReport b = BalanceSimulator.Run(4, 2, 7);

            Assert.AreEqual(a.ToCsv(), b.ToCsv());
            Assert.AreEqual(a.TotalRounds, b.TotalRounds);
        }
    }
}
