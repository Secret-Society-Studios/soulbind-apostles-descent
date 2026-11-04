using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SoulBind.Managers
{
    /// <summary>
    /// Keeps memory low on cheap phones. When the system warns that memory is short, art nothing uses
    /// any more is let go and the garbage collector runs. After each scene loads (menu to match and
    /// back) the same clean-up runs once, so leftovers from the last scene never pile up.
    /// </summary>
    public static class MemoryCare
    {
        private static bool _started;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Start()
        {
            if (_started)
                return;
            _started = true;
            Application.lowMemory += CleanUp;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // Domain reload may be off in the editor: start fresh on every Play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _started = false;
            Application.lowMemory -= CleanUp;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => CleanUp();

        /// <summary>Lets go of unused art and runs the garbage collector. A short pause: never call it mid-turn.</summary>
        public static void CleanUp()
        {
            Resources.UnloadUnusedAssets();
            GC.Collect();
        }
    }
}
