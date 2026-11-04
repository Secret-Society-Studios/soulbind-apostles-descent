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

        // ---------- Playing ----------

        private static AudioSource _music; // loops one track at a time
        private static AudioSource _sfx;   // one-shots, many at once

        /// <summary>Plays a sound once. A null clip (empty slot) does nothing.</summary>
        public static void PlaySfx(AudioClip clip)
        {
            if (clip == null)
                return;
            EnsureSources();
            _sfx.PlayOneShot(clip);
        }

        /// <summary>Loops a music track. The same track keeps playing; null stops the music.</summary>
        public static void PlayMusic(AudioClip clip)
        {
            if (clip == null)
            {
                if (_music != null)
                    _music.Stop();
                return;
            }
            EnsureSources();
            if (_music.clip == clip && _music.isPlaying)
                return;
            _music.clip = clip;
            _music.Play();
        }

        // One hidden object that survives scene loads holds both sources.
        private static void EnsureSources()
        {
            if (_music != null && _sfx != null)
                return;
            var go = new GameObject("GameAudio");
            Object.DontDestroyOnLoad(go);
            _music = go.AddComponent<AudioSource>();
            _music.loop = true;
            _music.playOnAwake = false;
            _sfx = go.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
        }

        // Forgets the sources on every Play, even with domain reload turned off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _music = null;
            _sfx = null;
        }

        // The saved volume applies from the first scene.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplySaved()
        {
            AudioListener.volume = Volume;
        }
    }
}
