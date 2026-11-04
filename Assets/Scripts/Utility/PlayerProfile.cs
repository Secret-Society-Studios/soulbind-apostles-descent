using System.Collections.Generic;
using System.Text;
using SoulBind.Core;
using UnityEngine;

namespace SoulBind.Utility
{
    /// <summary>
    /// This player's own stats, saved on the device: matches, wins, streaks and the cards they use
    /// most. MatchController records each finished match (not the tutorial); the main menu's
    /// profile page shows them.
    /// </summary>
    public static class PlayerProfile
    {
        private const string SaveKey = "SoulBind.Profile";
        private const string FormatTag = "p1"; // bump if the saved layout ever changes

        /// <summary>One slot per card, indexed by ArcanaId.</summary>
        public static readonly int CardCount = CardCatalog.All.Count;

        /// <summary>Everything saved for this player.</summary>
        public sealed class Stats
        {
            public int Matches;
            public int Wins;
            public int Ascensions;   // wins by reaching the Ascension tile
            public int Streak;       // wins in a row right now
            public int BestStreak;
            public int CardsPlayed;
            public int Combos;       // element combos triggered
            public readonly int[] CardUses = new int[CardCount]; // index = ArcanaId

            public int Losses => Matches - Wins;

            /// <summary>Wins out of matches, 0 to 1 (0 before the first match).</summary>
            public float WinRate => Matches > 0 ? (float)Wins / Matches : 0f;
        }

        /// <summary>The saved stats (all zero on a new device).</summary>
        public static Stats Load()
        {
            var stats = new Stats();
            string saved = PlayerPrefs.GetString(SaveKey, "");
            string[] parts = saved.Split('|');
            if (parts.Length < 9 || parts[0] != FormatTag)
                return stats;

            stats.Matches = Read(parts[1]);
            stats.Wins = Read(parts[2]);
            stats.Ascensions = Read(parts[3]);
            stats.Streak = Read(parts[4]);
            stats.BestStreak = Read(parts[5]);
            stats.CardsPlayed = Read(parts[6]);
            stats.Combos = Read(parts[7]);
            string[] uses = parts[8].Split(',');
            for (int i = 0; i < uses.Length && i < CardCount; i++)
                stats.CardUses[i] = Read(uses[i]);
            return stats;
        }

        /// <summary>
        /// Adds one finished match. cardUses = how many times each card was played (index = ArcanaId).
        /// </summary>
        public static void RecordMatch(bool won, bool ascended, int[] cardUses, int combos)
        {
            Stats stats = Load();
            stats.Matches++;
            if (won)
            {
                stats.Wins++;
                stats.Streak++;
                if (stats.Streak > stats.BestStreak)
                    stats.BestStreak = stats.Streak;
                if (ascended)
                    stats.Ascensions++;
            }
            else
            {
                stats.Streak = 0;
            }

            stats.Combos += Mathf.Max(0, combos);
            for (int i = 0; i < CardCount && i < cardUses.Length; i++)
            {
                int uses = Mathf.Max(0, cardUses[i]);
                stats.CardUses[i] += uses;
                stats.CardsPlayed += uses;
            }
            Save(stats);
        }

        /// <summary>The most played cards, most first. Cards never played are left out.</summary>
        public static void GetTopCards(Stats stats, int count, List<ArcanaId> results)
        {
            results.Clear();
            for (int n = 0; n < count; n++)
            {
                int best = -1; // -1 = none left
                for (int i = 0; i < CardCount; i++)
                {
                    bool taken = results.Contains((ArcanaId)i);
                    if (!taken && stats.CardUses[i] > 0 && (best < 0 || stats.CardUses[i] > stats.CardUses[best]))
                        best = i;
                }
                if (best < 0)
                    return;
                results.Add((ArcanaId)best);
            }
        }

        /// <summary>Clears every stat (for testing).</summary>
        public static void Reset()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }

        // Writes the stats as one tagged, pipe-separated line in PlayerPrefs.
        private static void Save(Stats stats)
        {
            var text = new StringBuilder(160);
            text.Append(FormatTag).Append('|');
            text.Append(stats.Matches).Append('|');
            text.Append(stats.Wins).Append('|');
            text.Append(stats.Ascensions).Append('|');
            text.Append(stats.Streak).Append('|');
            text.Append(stats.BestStreak).Append('|');
            text.Append(stats.CardsPlayed).Append('|');
            text.Append(stats.Combos).Append('|');
            for (int i = 0; i < CardCount; i++)
            {
                if (i > 0)
                    text.Append(',');
                text.Append(stats.CardUses[i]);
            }
            PlayerPrefs.SetString(SaveKey, text.ToString());
            PlayerPrefs.Save();
        }

        // Reads a saved count; anything broken or negative counts as 0.
        private static int Read(string text) => int.TryParse(text, out int value) && value > 0 ? value : 0;
    }
}
