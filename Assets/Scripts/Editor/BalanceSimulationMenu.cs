using System;
using System.IO;
using SoulBind.Core;
using UnityEditor;
using UnityEngine;

namespace SoulBind.EditorTools
{
    /// <summary>
    /// SoulBind > Run Balance Simulation: plays bot-vs-bot matches
    /// for 2, 4 and 6 Apostles and writes one report per size
    /// into BalanceReports/ (next to Assets, not imported).
    /// The Smart Bots version uses SmartBot, which plays closer to
    /// a real player but is much slower, so it plays fewer matches.
    /// </summary>
    public static class BalanceSimulationMenu
    {
        // Matches per player count. More is slower but steadier.
        private const int MatchesPerSize = 200;
        private const int SmartMatchesPerSize = 60;

        // Same seed, same results, so two runs can be compared.
        private const int Seed = 12345;

        private static readonly int[] Sizes = { 2, 4, 6 };

        [MenuItem("SoulBind/Run Balance Simulation")]
        private static void RunSimple() => Run(false);

        [MenuItem("SoulBind/Run Balance Simulation (Smart Bots)")]
        private static void RunSmart() => Run(true);

        private static void Run(bool smart)
        {
            int matches = smart ? SmartMatchesPerSize : MatchesPerSize;
            string tag = smart ? "smart" : "simple";
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "BalanceReports");
            Directory.CreateDirectory(folder);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmm");

            try
            {
                for (int i = 0; i < Sizes.Length; i++)
                {
                    int players = Sizes[i];
                    bool cancel = EditorUtility.DisplayCancelableProgressBar(
                        "Balance simulation",
                        $"{matches} matches with {players} Apostles...",
                        (float)i / Sizes.Length);
                    if (cancel)
                        break;

                    IBot bot = smart ? new SmartBot() : (IBot)new SimpleBot();
                    BalanceReport report = BalanceSimulator.Run(players, matches, Seed, bot);
                    string name = $"balance-{tag}-{players}p-{stamp}";
                    string text = report.ToText();
                    File.WriteAllText(Path.Combine(folder, name + ".txt"), text);
                    File.WriteAllText(Path.Combine(folder, name + ".csv"), report.ToCsv());
                    Debug.Log($"[Balance] {players} Apostles\n{text}");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log($"[Balance] Reports saved in {folder}");
            EditorUtility.RevealInFinder(folder);
        }
    }
}
