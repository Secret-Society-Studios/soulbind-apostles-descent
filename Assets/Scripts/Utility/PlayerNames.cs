using System;
using System.Security.Claims;
using System.Text;
using SoulBind.Core;
using UnityEngine;

namespace SoulBind.Utility
{
    /// <summary>
    /// The names shown for the Apostles in this match, and the
    /// name this player typed on the main menu (saved on the
    /// device). An Apostle without a name (a bot, or nobody
    /// typed one) shows as "Apostle 3".
    /// </summary>
    public static class PlayerNames
    {
        public const int MaxLength = 12;
        private const string SaveKey = "SoulBind.PlayerName";

        // Index = Apostle id (1 to 6). Index 0 is unused.
        private static readonly string[] Names =
            new string[GameConstants.MaxPlayers + 1];

        // Starts empty on every play, even with domain reload off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Clear();

        /// <summary>The name typed on this device ("" = none).</summary>
        public static string LocalName
        {
            get => Clean(PlayerPrefs.GetString(SaveKey, ""));
            set
            {
                PlayerPrefs.SetString(SaveKey, Clean(value));
                PlayerPrefs.Save();
            }
        }

        /// <summary>The name to show for this Apostle.</summary>
        public static string Get(int apostleId)
        {
            bool named = apostleId > 0 && apostleId < Names.Length && !string.IsNullOrEmpty(Names[apostleId]);
            return named ? Names[apostleId] : $"Apostle {apostleId}";
        }

        public static void Set(int apostleId, string name)
        {
            if (apostleId > 0 && apostleId < Names.Length)
                Names[apostleId] = Clean(name);
        }

        public static void Clear() => Array.Clear(Names, 0, Names.Length);

        /// <summary>All names in one string, to send online.</summary>
        public static string Pack() => string.Join("\n", Names);

        /// <summary>Reads what Pack made (on the players' side).</summary>
        public static void Unpack(string packed)
        {
            Clear();
            if (string.IsNullOrEmpty(packed))
                return;
            string[] parts = packed.Split('\n');
            for (int i = 1; i < parts.Length && i < Names.Length; i++)
                Set(i, parts[i]);
        }

        /// <summary>
        /// Allowed characters only (no rich-text tags like
        /// &lt;color&gt;), single spaces, at most MaxLength long.
        /// </summary>
        public static string Clean(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return string.Empty;
            var text = new StringBuilder(MaxLength);
            bool space = false;
            foreach (char c in raw.Trim())
            {
                if (c == ' ')
                {
                    space = text.Length > 0;
                    continue;
                }
                if (!IsAllowed(c))
                    continue;
                if (space && text.Length < MaxLength - 1)
                    text.Append(' ');
                space = false;
                text.Append(c);
                if (text.Length >= MaxLength)
                    break;
            }
            return text.ToString();
        }

        public static bool IsAllowed(char c) => char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_' || c == '.' || c == '\'';
    }
    
}