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
        public int Had;         // (match, Apostle) pairs that held it in hand at some point
        public int HadWins;     // ...of which that Apostle won the match
        public int NotHad;      // (match, Apostle) pairs that never held it
        public int NotHadWins;  // ...of which that Apostle won the match

        /// <summary>How often an Apostle who played this card won (compare with 1 / players).</summary>
        public float WinRate => Holders > 0 ? (float)HolderWins / Holders : 0f;

        /// <summary>How often an Apostle who never held this card won.</summary>
        public float WinRateWithout => NotHad > 0 ? (float)NotHadWins / NotHad : 0f;

        /// <summary>
        /// Played minus never held, in percentage points. The clearest strength signal: well above 0
        /// means the card wins games, well below 0 means having it hurts.
        /// </summary>
        public float Lift => Holders > 0 && NotHad > 0 ? (WinRate - WinRateWithout) * 100f : 0f;

        /// <summary>How often it was held but never played (too costly or too situational).</summary>
        public float DeadRate => Had > 0 ? (float)(Had - Holders) / Had : 0f;
    }

    /// <summary>How one element's combo did across the simulated matches.</summary>
    public sealed class ComboStat
    {
        public Element Element;
        public int Fires;       // times the combo triggered
        public int Firers;      // (match, Apostle) pairs that triggered it at least once
        public int FirerWins;   // ...of which that Apostle won the match

        public float WinRate => Firers > 0 ? (float)FirerWins / Firers : 0f;
    }

    /// <summary>What a batch of bot-vs-bot matches showed.</summary>
    public sealed class BalanceReport
    {
        public string BotName = "SimpleBot";
        public int Players;
        public int Matches;
        public int NoWinner;     // everyone died together
        public int Stalled;      // hit the action limit without ending
        public int Refused;      // bot actions the rules refused (the turn was ended instead)
        public long TotalRounds;
        public readonly int[] EndReasons = new int[Enum.GetValues(typeof(MatchEndReason)).Length];
        public readonly int[] WinsBySeat = new int[GameConstants.MaxPlayers]; // by turn order, 0 = played first
        public readonly List<CardStat> Cards = new List<CardStat>();
        public readonly List<ComboStat> Combos = new List<ComboStat>();

        /// <summary>A readable summary: results, wins by turn order, then cards from best to worst.</summary>
        public string ToText()
        {
            var text = new StringBuilder();
            int finished = Math.Max(1, Matches - Stalled);
            text.AppendLine($"{Matches} matches, {Players} Apostles, all played by {BotName}.");
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
            text.AppendLine("Lift = win rate when played minus win rate when never held (points).");
            text.AppendLine("Dead = held but never played. Small samples (under 30) are noise.");
            text.AppendLine();
            text.AppendLine("Card                 Plays  Played by  Win rate  Without  Lift    Dead");
            foreach (CardStat stat in Sorted())
            {
                string name = CardCatalog.Get(stat.Card).Name;
                text.AppendLine($"{name,-20} {stat.Plays,5}  {stat.Holders,9}  " +
                                $"{Percent(stat.HolderWins, stat.Holders),8}  " +
                                $"{Percent(stat.NotHadWins, stat.NotHad),7}  " +
                                $"{stat.Lift,5:+0.0;-0.0;0.0}  {stat.DeadRate * 100f,5:0}%");
            }
            text.AppendLine();
            text.AppendLine("Combo       Fires  Fired by  Win rate");
            foreach (ComboStat combo in Combos)
            {
                text.AppendLine($"{combo.Element,-10} {combo.Fires,6}  {combo.Firers,8}  " +
                                $"{Percent(combo.FirerWins, combo.Firers),8}");
            }
            return text.ToString();
        }

        /// <summary>The card table as CSV, for a spreadsheet.</summary>
        public string ToCsv()
        {
            var csv = new StringBuilder();
            csv.AppendLine("Card,Element,ATK,DEF,CAP,Plays,PlayedBy,Won,WinRate,WinRateWithout,Lift,DeadRate");
            foreach (CardStat stat in Sorted())
            {
                CardDefinition card = CardCatalog.Get(stat.Card);
                csv.AppendLine($"{card.Name},{card.Element},{card.Attack},{card.Defense},{card.Cap}," +
                               $"{stat.Plays},{stat.Holders},{stat.HolderWins},{stat.WinRate:0.000}," +
                               $"{stat.WinRateWithout:0.000},{stat.Lift:0.0},{stat.DeadRate:0.000}");
            }
            return csv.ToString();
        }

        // A copy of the card stats, biggest win lift first.
        private List<CardStat> Sorted()
        {
            var sorted = new List<CardStat>(Cards);
            sorted.Sort((a, b) => b.Lift.CompareTo(a.Lift));
            return sorted;
        }

        // Formats part / whole as a percentage, or "-" when there is nothing to divide by.
        private static string Percent(int part, int whole) =>
            whole > 0 ? $"{100.0 * part / whole:0.0}%" : "-";
    }

    /// <summary>
    /// Plays many bot-vs-bot matches and counts what happened: who won, how, how long it took, whether
    /// playing first helps, how each card and combo did, and which cards sit in hands unplayed.
    /// A balance tool for testing, not used in the game. Pure rules code: no Unity types.
    /// </summary>
    public static class BalanceSimulator
    {
        /// <summary>A match that takes more bot actions than this is counted as stalled.</summary>
        public const int MaxActions = 20000;

        /// <summary>A batch played by SimpleBot.</summary>
        public static BalanceReport Run(int players, int matches, int seed) =>
            Run(players, matches, seed, new SimpleBot());

        /// <summary>A batch where this bot plays every seat.</summary>
        public static BalanceReport Run(int players, int matches, int seed, IBot bot)
        {
            if (players < GameConstants.MinPlayers || players > GameConstants.MaxPlayers)
                throw new ArgumentOutOfRangeException(nameof(players));
            if (matches < 1)
                throw new ArgumentOutOfRangeException(nameof(matches));
            if (bot == null)
                throw new ArgumentNullException(nameof(bot));

            int cardCount = CardCatalog.All.Count;
            var report = new BalanceReport { Players = players, BotName = bot.Name };
            for (int c = 0; c < cardCount; c++)
                report.Cards.Add(new CardStat { Card = CardCatalog.All[c].Id });
            for (int e = 0; e < ElementCombo.ElementCount; e++)
                report.Combos.Add(new ComboStat { Element = (Element)e });

            var tally = new Tally(players, cardCount);
            var seeds = new Random(seed);
            for (int m = 0; m < matches; m++)
            {
                tally.Clear();
                PlayOne(bot, players, seeds.Next(), report, tally);
            }
            return report;
        }

        // What each Apostle did in one match. Index = Apostle id (0 unused).
        private sealed class Tally
        {
            public readonly bool[,] Played; // [Apostle id, card]
            public readonly bool[,] Had;    // [Apostle id, card]
            public readonly bool[,] Fired;  // [Apostle id, element]

            public Tally(int players, int cardCount)
            {
                Played = new bool[players + 1, cardCount];
                Had = new bool[players + 1, cardCount];
                Fired = new bool[players + 1, ElementCombo.ElementCount];
            }

            // Resets the per-match counters so the tally can be reused without allocating.
            public void Clear()
            {
                Array.Clear(Played, 0, Played.Length);
                Array.Clear(Had, 0, Had.Length);
                Array.Clear(Fired, 0, Fired.Length);
            }
        }

        // Plays one full bot match from the seed and adds what happened to the report.
        private static void PlayOne(IBot bot, int players, int seed, BalanceReport report, Tally tally)
        {
            HexBoard board = StandardBoard.Create();
            var random = new Random(seed);
            Match match = Match.Create(players, board, SpawnPlanner.PickSpread(board, players), random);
            report.Matches++;

            for (int i = 0; i < MaxActions && !match.IsOver; i++)
            {
                int actor = match.CurrentApostle != null ? match.CurrentApostle.Id : 0;
                if (actor > 0)
                    NoteHand(match, actor, tally);

                BotAction action = bot.Decide(match);
                bool isPlay = action.Kind == BotActionKind.PlayCard && actor > 0;
                Element element = isPlay ? CardCatalog.Get(action.Card).Element : default;
                // One short of the combo before this card means this card triggers it.
                bool completesCombo = isPlay
                    && match.GetElementsPlayedThisTurn(element) == ElementCombo.CardsNeeded - 1;

                if (!SimpleBot.Apply(match, action))
                {
                    // The rules refused it: end the turn, as the game does, so the bot never gets stuck.
                    report.Refused++;
                    if (match.CurrentApostle == null)
                        break;
                    match.EndTurn();
                    continue;
                }
                if (!isPlay)
                    continue;
                report.Cards[(int)action.Card].Plays++;
                tally.Played[actor, (int)action.Card] = true;
                tally.Had[actor, (int)action.Card] = true;
                if (completesCombo)
                {
                    report.Combos[(int)element].Fires++;
                    tally.Fired[actor, (int)element] = true;
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
                bool won = match.WinnerId == id;
                for (int c = 0; c < report.Cards.Count; c++)
                    Count(report.Cards[c], tally.Played[id, c], tally.Had[id, c], won);
                for (int e = 0; e < report.Combos.Count; e++)
                {
                    if (!tally.Fired[id, e])
                        continue;
                    report.Combos[e].Firers++;
                    if (won)
                        report.Combos[e].FirerWins++;
                }
            }
        }

        // Adds one Apostle's result for one card to its stats.
        private static void Count(CardStat stat, bool played, bool had, bool won)
        {
            int win = won ? 1 : 0;
            if (played)
            {
                stat.Holders++;
                stat.HolderWins += win;
            }
            if (had)
            {
                stat.Had++;
                stat.HadWins += win;
            }
            else
            {
                stat.NotHad++;
                stat.NotHadWins += win;
            }
        }

        // Marks every card the acting Apostle holds right now.
        private static void NoteHand(Match match, int actor, Tally tally)
        {
            IReadOnlyList<ArcanaId> hand = match.GetHand(actor);
            for (int i = 0; i < hand.Count; i++)
                tally.Had[actor, (int)hand[i]] = true;
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
