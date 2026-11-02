using System;
using SoulBind.Core;
using UnityEngine;

namespace SoulBind.Managers
{
    // Quick chat: preset lines and emotes (QuickChat). Offline and on the host a message is shown at
    // once; an online player sends it to the host, which checks it and passes it on to everyone.
    public sealed partial class MatchController
    {
        // ---------- Quick chat ----------

        /// <summary>Someone said something: the seat that said it, and the chat id.</summary>
        public event Action<int, int> ChatShown;

        /// <summary>Online player: a chat id to send to the host (NetworkMatch sends it).</summary>
        public event Action<int> ChatSubmitted;

        private readonly ChatLimiter _chatLimiter = new ChatLimiter();

        /// <summary>This screen's player says chat id (a line or an emote).</summary>
        public void SayChat(int chatId)
        {
            if (CurrentMatch == null || !QuickChat.IsValid(chatId))
                return;
            int seat = ViewerId;
            if (IsOnlineClient)
            {
                // Checked here too, so the button doesn't send what the host would drop anyway.
                if (_chatLimiter.TryUse(seat, Time.unscaledTime))
                    ChatSubmitted?.Invoke(chatId);
                return;
            }
            PostChat(seat, chatId);
        }

        /// <summary>Host: an online player's message. The seat comes from their connection.</summary>
        public bool ReceiveRemoteChat(int seat, int chatId) => PostChat(seat, chatId);

        /// <summary>Online player: the host passed a message on, already checked.</summary>
        public void ShowRemoteChat(int seat, int chatId)
        {
            if (QuickChat.IsValid(chatId))
                ChatShown?.Invoke(seat, chatId);
        }

        // Checks the id and the spam limit, then shows it (the host's NetworkMatch also sends it on).
        private bool PostChat(int seat, int chatId)
        {
            if (!QuickChat.IsValid(chatId) || !_chatLimiter.TryUse(seat, Time.unscaledTime))
                return false;
            ChatShown?.Invoke(seat, chatId);
            return true;
        }
    }
}
