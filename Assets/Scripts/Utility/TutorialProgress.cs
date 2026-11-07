using UnityEngine;

namespace SoulBind.Utility
{
    /// <summary>
    /// Whether this player has finished or skipped the tutorial, saved on the device. Until then,
    /// Play vs Bots offers the tutorial first.
    /// </summary>
    public static class TutorialProgress
    {
        private const string DoneKey = "tutorialDone";

        /// <summary>True once the tutorial was won or skipped. Leaving it halfway doesn't count.</summary>
        public static bool IsDone => PlayerPrefs.GetInt(DoneKey, 0) == 1;

        /// <summary>Saves that Play vs Bots no longer needs to offer the tutorial.</summary>
        public static void MarkDone()
        {
            PlayerPrefs.SetInt(DoneKey, 1);
            PlayerPrefs.Save();
        }
    }
}
