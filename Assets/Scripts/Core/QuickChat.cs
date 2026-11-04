using System;

namespace SoulBind.Core
{
    /// <summary>
    /// The fixed chat lines and emote slots players can send in a match. Only a chat id (one small
    /// number) travels over the network, so nobody can type anything, and nothing needs filtering.
    /// Ids 0 to Lines.Length - 1 are lines; the EmoteCount ids after them are emotes.
    /// Pure rules code: no Unity types.
    /// </summary>
    public static class QuickChat
    {
        /// <summary>The preset lines, in picker order. Add or change lines here.</summary>
        public static readonly string[] Lines =
        {
            "GG", "GG EZ", "Nice try", "Well played", "Good luck",
            "Thanks", "Oops", "Sorry", "Thinking...", "Wow!"
        };

        /// <summary>Emote slots. Their art goes in UiSkin; an empty slot shows a numbered placeholder.</summary>
        public const int EmoteCount = 8;

        /// <summary>How long one player waits between messages, so nobody can spam.</summary>
        public const float CooldownSeconds = 2f;

        /// <summary>How many ids there are: every line, then every emote.</summary>
        public static int Count => Lines.Length + EmoteCount;

        // True for any line or emote id the chat knows.
        public static bool IsValid(int chatId) => chatId >= 0 && chatId < Count;

        // True when the id is an emote rather than a text line.
        public static bool IsEmote(int chatId) => chatId >= Lines.Length && chatId < Count;

        /// <summary>Which emote slot an emote id is (0 to EmoteCount - 1).</summary>
        public static int GetEmoteIndex(int chatId) => chatId - Lines.Length;

        /// <summary>The chat id of emote slot index.</summary>
        public static int GetEmoteId(int index) => Lines.Length + index;

        /// <summary>The text of a line id ("" for an emote or a bad id).</summary>
        public static string GetLine(int chatId) =>
            chatId >= 0 && chatId < Lines.Length ? Lines[chatId] : string.Empty;
    }

    /// <summary>
    /// Keeps each seat to one message per QuickChat.CooldownSeconds. The host checks every message
    /// with it, so a modified client can't spam the others either. The time is passed in (seconds).
    /// </summary>
    public sealed class ChatLimiter
    {
        private readonly float[] _nextAllowed = new float[GameConstants.MaxPlayers + 1]; // index = Apostle id

        /// <summary>True (and starts the wait) if this seat may send now.</summary>
        public bool TryUse(int seat, float now)
        {
            if (seat <= 0 || seat >= _nextAllowed.Length || now < _nextAllowed[seat])
                return false;
            _nextAllowed[seat] = now + QuickChat.CooldownSeconds;
            return true;
        }

        // Lets everyone chat again straight away, e.g. for a new match.
        public void Clear() => Array.Clear(_nextAllowed, 0, _nextAllowed.Length);
    }
}
