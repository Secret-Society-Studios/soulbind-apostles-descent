using UnityEngine;

namespace SoulBind.Managers
{
    /// <summary>
    /// The player's Music, Sounds and Narrator volumes, saved between launches. Each one turns its own
    /// AudioSource up or down, so every clip played through PlayMusic or PlaySfx follows it.
    /// </summary>
    public static class GameAudio
    {
        private const string MusicKey = "musicVolume"; // 0 to 1
        private const string SoundKey = "soundVolume"; // 0 to 1
        private const string NarratorKey = "narratorVolume"; // 0 to 1
        private const string OldMasterKey = "masterVolume"; // from before the split: the starting value of both

        /// <summary>The music's volume: 0 = silent, 1 = full.</summary>
        public static float MusicVolume
        {
            get => Saved(MusicKey);
            set
            {
                float volume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(MusicKey, volume);
                if (_music != null)
                    _music.volume = volume;
            }
        }

        /// <summary>The sound effects' volume: 0 = silent, 1 = full.</summary>
        public static float SoundVolume
        {
            get => Saved(SoundKey);
            set
            {
                float volume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(SoundKey, volume);
                if (_sfx != null)
                    _sfx.volume = volume;
            }
        }

        /// <summary>The narrator's volume: 0 = silent, 1 = full. It starts at the Sounds volume.</summary>
        public static float NarratorVolume
        {
            get => PlayerPrefs.GetFloat(NarratorKey, SoundVolume);
            set
            {
                float volume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(NarratorKey, volume);
                if (_voice != null)
                    _voice.volume = volume;
            }
        }

        /// <summary>Writes the volumes to disk (call when the player lets go of the slider).</summary>
        public static void Save() => PlayerPrefs.Save();

        // A saved volume, or the old master volume for a player who has not moved this slider yet.
        private static float Saved(string key) => PlayerPrefs.GetFloat(key, PlayerPrefs.GetFloat(OldMasterKey, 1f));

        // ---------- Playing ----------

        private static AudioSource _music; // loops one track at a time
        private static AudioSource _sfx;   // one-shots, many at once
        private static AudioSource _voice; // the narrator, one line at a time

        /// <summary>True while the narrator is speaking.</summary>
        public static bool IsVoicePlaying => _voice != null && _voice.isPlaying;

        /// <summary>Plays a narrator line, in place of any other.</summary>
        public static void PlayVoice(AudioClip clip)
        {
            if (clip == null)
                return;
            EnsureSources();
            _voice.clip = clip;
            _voice.Play();
        }

        /// <summary>Stops the narrator.</summary>
        public static void StopVoice()
        {
            if (_voice != null)
                _voice.Stop();
        }

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
            if (_music != null && _sfx != null && _voice != null)
                return;
            var go = new GameObject("GameAudio");
            Object.DontDestroyOnLoad(go);
            _music = go.AddComponent<AudioSource>();
            _music.loop = true;
            _music.playOnAwake = false;
            _music.volume = MusicVolume;
            _sfx = go.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _sfx.volume = SoundVolume;
            _voice = go.AddComponent<AudioSource>();
            _voice.playOnAwake = false;
            _voice.volume = NarratorVolume;
        }

        // Forgets the sources on every Play, even with domain reload turned off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _music = null;
            _sfx = null;
            _voice = null;
        }
    }
}
