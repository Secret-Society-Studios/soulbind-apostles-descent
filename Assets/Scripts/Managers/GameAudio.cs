using UnityEngine;

namespace SoulBind.Managers
{
    /// <summary>
    /// The player's master volume, saved between launches. It turns the whole game up or down
    /// (AudioListener.volume), so every sound and music track added later follows it with no extra code.
    /// </summary>
    public static class GameAudio
    {
        private const string PrefKey = "masterVolume"; // 0 to 1

        /// <summary>0 = silent, 1 = full. Saved at once.</summary>
        public static float Volume
        {
            get => PlayerPrefs.GetFloat(PrefKey, 1f);
            set
            {
                float volume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(PrefKey, volume);
                AudioListener.volume = volume;
            }
        }

        /// <summary>Writes the volume to disk (call when the player lets go of the slider).</summary>
        public static void Save() => PlayerPrefs.Save();

        // The saved volume applies from the first scene.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplySaved()
        {
            AudioListener.volume = Volume;
        }
    }
}
