using System;
using System.Collections.Generic;
using System.Text;

namespace SoulBind.Core
{
    /// <summary>How one card did across the simulated matches.</summary>
    public sealed class CardStat
    {
        public ArcanaId Card;
        public int Plays;       // times any Apostle played it
        public int Holders;     // (match, Apostle) pairs that played it at least once
        public int HolderWins;  // ...of which that Apostle won the match

        /// <summary>How often an Apostle who played this card won (compare with 1 / players).</summary>
        public float WinRate => Holders > 0 ? (float)HolderWins / Holders : 0f;
    }

    /// <summary>What a batch of bot-vs-bot matches showed.</summary>
    public sealed class BalanceReport
    {
        public int Players;
        public int Matches;
        public int NoWinner;     // everyone died together
        public int Stalled;      // hit the action limit without ending
        public int Refused;      // bot actions the rules refused (the turn was ended instead)
        public long TotalRounds;
        public readonly int[] EndReasons = new int[Enum.GetValues(typeof(MatchEndReason)).Length];
        public readonly int[] WinsBySeat = new int[GameConstants.MaxPlayers]; // by turn order, 0 = played first
        public readonly List<CardStat> Cards = new List<CardStat>();

        /// <summary>A readable summary: results, wins by turn order, then cards from best to worst.</summary>
        public string ToText()
        {
            var text = new StringBuilder();
            int finished = Math.Max(1, Matches - Stalled);
            text.AppendLine($"{Matches} matches, {Players} Apostles, all played by SimpleBot.");
            text.AppendLine($"Average length: {(double)TotalRounds / finished:0.0} rounds. Stalled: {Stalled}. No winner: {NoWinner}. Refused bot actions: {Refused}.");
            text.AppendLine($"Last standing: {EndReasons[(int)MatchEndReason.LastApostleStanding]}   " +
                            $"Ascension: {EndReasons[(int)MatchEndReason.Ascension]}   " +
                            $"No survivors: {EndReasons[(int)MatchEndReason.NoSurvivors]}");
            text.Append("Wins by turn order:");
            for (int i = 0; i < Players; i++)
                text.Append($"  #{i + 1} {Percent(WinsBySeat[i], finished)}");
            text.AppendLine();
            text.AppendLine($"Fair share for one Apostle: {Percent(1, Players)}.");
            text.AppendLine();
            text.AppendLine("Card                 Plays  Played by  Won   Win rate");
            foreach (CardStat stat in Sorted())
            {
                string name = CardCatalog.Get(stat.Card).Name;
                text.AppendLine($"{name,-20} {stat.Plays,5}  {stat.Holders,9}  {stat.HolderWins,4}   {Percent(stat.HolderWins, stat.Holders)}");
            }
            return text.ToString();
        }

        /// <summary>The card table as CSV, for a spreadsheet.</summary>
        public string ToCsv()
        {
            var csv = new StringBuilder();
            csv.AppendLine("Card,Element,ATK,DEF,CAP,Plays,PlayedBy,Won,WinRate");
            foreach (CardStat stat in Sorted())
            {
                CardDefinition card = CardCatalog.Get(stat.Card);
                csv.AppendLine($"{card.Name},{card.Element},{card.Attack},{card.Defense},{card.Cap}," +
                               $"{stat.Plays},{stat.Holders},{stat.HolderWins},{stat.WinRate:0.000}");
            }
            return csv.ToString();
        }

        private List<CardStat> Sorted()
        {
            var sorted = new List<CardStat>(Cards);
            sorted.Sort((a, b) => b.WinRate.CompareTo(a.WinRate));
            return sorted;
        }

        private static string Percent(int part, int whole) =>
            whole > 0 ? $"{100.0 * part / whole:0.0}%" : "-";
    }

    /// <summary>
    /// Plays many bot-vs-bot matches with SimpleBot and counts what happened: who won, how, how long it
    /// took, whether playing first helps, and how often an Apostle who played each card went on to win.
    /// A balance tool for testing, not used in the game. Pure rules code: no Unity types.
    /// </summary>
    public static class BalanceSimulator
    {
        /// <summary>A match that takes more bot actions than this is counted as stalled.</summary>
        public const int MaxActions = 20000;

        public static BalanceReport Run(int players, int matches, int seed)
        {
            if (players < GameConstants.MinPlayers || players > GameConstants.MaxPlayers)
                throw new ArgumentOutOfRangeException(nameof(players));
            if (matches < 1)
                throw new ArgumentOutOfRangeException(nameof(matches));

            int cardCount = CardCatalog.All.Count;
            var report = new BalanceReport { Players = players };
            for (int c = 0; c < cardCount; c++)
                report.Cards.Add(new CardStat { Card = CardCatalog.All[c].Id });

            var bot = new SimpleBot();
            var played = new bool[players + 1, cardCount]; // [Apostle id, card]
            var seeds = new Random(seed);
            for (int m = 0; m < matches; m++)
            {
                Array.Clear(played, 0, played.Length);
                PlayOne(bot, players, seeds.Next(), report, played);
            }
            return report;
        }

        private static void PlayOne(SimpleBot bot, int players, int seed, BalanceReport report, bool[,] played)
        {
            HexBoard board = StandardBoard.Create();
            var random = new Random(seed);
            Match match = Match.Create(players, board, SpawnPlanner.PickSpread(board, players), random);
            report.Matches++;

            for (int i = 0; i < MaxActions && !match.IsOver; i++)
            {
                BotAction action = bot.Decide(match);
                int actor = match.CurrentApostle != null ? match.CurrentApostle.Id : 0;
                if (!SimpleBot.Apply(match, action))
                {
                    // The rules refused it: end the turn, as the game does, so the bot never gets stuck.
                    report.Refused++;
                    if (match.CurrentApostle == null)
                        break;
                    match.EndTurn();
                    continue;
                }
                if (action.Kind == BotActionKind.PlayCard && actor > 0)
                {
                    report.Cards[(int)action.Card].Plays++;
                    played[actor, (int)action.Card] = true;
                }
            }

            if (!match.IsOver)
            {
                report.Stalled++;
                return;
            }

            report.TotalRounds += match.Turns.RoundNumber;
            report.EndReasons[(int)match.EndReason]++;
            if (match.WinnerId == 0)
                report.NoWinner++;
            else
                report.WinsBySeat[SeatOf(match, match.WinnerId)]++;

            for (int id = 1; id <= players; id++)
            {
                for (int c = 0; c < report.Cards.Count; c++)
                {
                    if (!played[id, c])
                        continue;
                    report.Cards[c].Holders++;
                    if (match.WinnerId == id)
                        report.Cards[c].HolderWins++;
                }
            }
        }

        // Where this Apostle sits in the turn order (0 = played first).
        private static int SeatOf(Match match, int apostleId)
        {
            IReadOnlyList<int> seats = match.Turns.Seats;
            for (int i = 0; i < seats.Count; i++)
            {
                if (seats[i] == apostleId)
                    return i;
            }
            return 0;
        }
    }
}
