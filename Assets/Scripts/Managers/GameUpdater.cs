using System;
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using SoulBind.Core;
using SoulBind.UI;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Networking;

namespace SoulBind.Managers
{
    /// <summary>
    /// Keeps a sideloaded Android game up to date: on launch it reads version.json from the newest GitHub
    /// release and, if that build is newer, offers to download it. The APK's hash is checked before Android's
    /// own install screen opens, and Android refuses an update not signed with the same key.
    /// Anything that goes wrong while checking is ignored, so the game plays on without it.
    /// See Docs/Updates.md for publishing a build. It starts itself, so it needs no setup.
    /// </summary>
    public sealed class GameUpdater : MonoBehaviour
    {
        /// <summary>Where each release's files are. Blank turns the updater off.</summary>
        public const string ReleaseUrl =
            "https://github.com/Secret-Society-Studios/soulbind-apostles-descent/releases/latest/download/";

        /// <summary>The file that describes the newest build.</summary>
        public const string InfoFile = "version.json";

        private const int CheckTimeoutSeconds = 10;
        private const string DownloadFolder = "update"; // also named in soulbind_update_paths.xml
        private const string DownloadName = "SoulBind.apk";
        private const string ProviderSuffix = ".soulbindupdater"; // also in the androidlib's manifest
        private const string ApkType = "application/vnd.android.package-archive";
        private const int GrantReadUri = 1;              // Intent.FLAG_GRANT_READ_URI_PERMISSION
        private const int NewTask = 0x10000000;          // Intent.FLAG_ACTIVITY_NEW_TASK

        private UpdateInfo _info;
        private UpdatePrompt _prompt;
        private string _apkPath;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Begin()
        {
            if (Application.platform != RuntimePlatform.Android || string.IsNullOrEmpty(ReleaseUrl))
                return;
            var go = new GameObject("GameUpdater");
            DontDestroyOnLoad(go);
            go.AddComponent<GameUpdater>();
        }

        private IEnumerator Start()
        {
            yield return Check();
            if (_info == null)
            {
                Destroy(gameObject);
                yield break;
            }

            _apkPath = Path.Combine(Application.temporaryCachePath, DownloadFolder, DownloadName);
            _prompt = UpdatePrompt.Create(() => Destroy(gameObject));
            Offer();
        }

        private void OnDestroy()
        {
            if (_prompt != null)
                Destroy(_prompt.gameObject);
        }

        // Fills _info if a newer build is out.
        private IEnumerator Check()
        {
            using UnityWebRequest request = UnityWebRequest.Get(ReleaseUrl + InfoFile);
            request.timeout = CheckTimeoutSeconds;
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
                yield break;

            try
            {
                UpdateInfo info = JsonUtility.FromJson<UpdateInfo>(request.downloadHandler.text);
                if (info != null && info.IsNewerThan(InstalledVersionCode()))
                    _info = info;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("GameUpdater: could not read the update info. " + exception.Message);
            }
        }

        private void Offer()
        {
            _prompt.Show($"Version {_info.versionName} is ready to download ({_info.SizeMegabytes} MB).", "Update",
                () => StartCoroutine(Download()));
        }

        private IEnumerator Download()
        {
            _prompt.ShowBusy("Downloading... 0%");
            Directory.CreateDirectory(Path.GetDirectoryName(_apkPath));
            File.Delete(_apkPath);

            using (var request = new UnityWebRequest(ReleaseUrl + _info.apkFile, UnityWebRequest.kHttpVerbGET))
            {
                request.downloadHandler = new DownloadHandlerFile(_apkPath) { removeFileOnAbort = true };
                UnityWebRequestAsyncOperation sending = request.SendWebRequest();
                while (!sending.isDone)
                {
                    _prompt.ShowPercent(request.downloadProgress);
                    yield return null;
                }
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Failed("The download did not finish.");
                    yield break;
                }
            }

            _prompt.ShowBusy("Checking the download...");
            Task<bool> check = Task.Run(() => HashMatches(_apkPath, _info.sha256));
            while (!check.IsCompleted)
                yield return null;
            if (check.IsFaulted || !check.Result)
            {
                File.Delete(_apkPath);
                Failed("The download was damaged.");
                yield break;
            }

            Install();
        }

        private void Failed(string message) =>
            _prompt.Show(message + " Check your connection and try again.", "Retry", () => StartCoroutine(Download()));

        private void Install()
        {
            try
            {
                OpenInstaller(_apkPath);
                // They may back out of Android's screen, so the way back in stays here.
                _prompt.Show($"Tap Install to finish updating to {_info.versionName}.", "Install", Install);
            }
            catch (Exception exception)
            {
                Debug.LogError("GameUpdater: could not open the installer. " + exception);
                Failed("The installer did not open.");
            }
        }

        private static bool HashMatches(string path, string expected)
        {
            using FileStream file = File.OpenRead(path);
            using SHA256 sha = SHA256.Create();
            string actual = BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "");
            return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
        }

        // ---------- Android ----------

        // Android's build number, which AndroidBuilder raises on every version.
        private static int InstalledVersionCode()
        {
            AndroidJavaObject activity = AndroidApplication.currentActivity;
            using AndroidJavaObject manager = activity.Call<AndroidJavaObject>("getPackageManager");
            using AndroidJavaObject package =
                manager.Call<AndroidJavaObject>("getPackageInfo", activity.Call<string>("getPackageName"), 0);
            return package.Get<int>("versionCode");
        }

        // Android's own install screen. The first time, it asks the player to allow installs from this game.
        private static void OpenInstaller(string path)
        {
            AndroidJavaObject activity = AndroidApplication.currentActivity;
            using var file = new AndroidJavaObject("java.io.File", path);
            using var provider = new AndroidJavaClass("androidx.core.content.FileProvider");
            using AndroidJavaObject uri = provider.CallStatic<AndroidJavaObject>("getUriForFile", activity,
                Application.identifier + ProviderSuffix, file);
            using var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.VIEW");
            intent.Call<AndroidJavaObject>("setDataAndType", uri, ApkType).Dispose();
            intent.Call<AndroidJavaObject>("addFlags", GrantReadUri | NewTask).Dispose();
            activity.Call("startActivity", intent);
        }
    }
}
