using UnityEngine;

namespace SoulBind.Utility
{
    /// <summary>The player's chat choices, saved on the device.</summary>
    public static class ChatSettings
    {
        private const string MuteKey = "SoulBind.ChatMuted";

        /// <summary>On: other players' messages are hidden (your own still show).</summary>
        public static bool MuteOthers
        {
            get => PlayerPrefs.GetInt(MuteKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(MuteKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }
    }
}
