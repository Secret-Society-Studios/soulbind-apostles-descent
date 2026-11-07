using System;
using System.IO;
using System.Security.Cryptography;
using SoulBind.Core;
using SoulBind.Managers;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SoulBind.EditorTools
{
    /// <summary>
    /// SoulBind > Build Android APK: builds the APK with a version number, so every APK we hand out
    /// can be told apart. It raises the version (1.0.3 -> 1.0.4) and Android's build number (which must
    /// go up for a phone to accept the APK as an update), then builds
    /// Builds/Android/SoulBind-v1.0.4.apk from the scenes in File > Build Profiles.
    /// The game shows the same number (Application.version) on the main menu and in the Quit box.
    /// If the build fails, the version is put back. Commit ProjectSettings/ProjectSettings.asset after a
    /// build, so the whole team continues from the same number.
    /// It signs with the team's keystore and writes version.json for the in-game updater: see Docs/Updates.md.
    /// </summary>

    public static class AndroidBuilder
    {
        private const string OutputFolder = "Builds/Android";
        private const string FilePrefix = "SoulBind-v";

        [MenuItem("SoulBind/Build Android APK...")]
        private static void BuildApk()
        {
            string current = Normalize(PlayerSettings.bundleVersion);
            string next = NextPatch(current);
            int code = PlayerSettings.Android.bundleVersionCode;

            int choice = EditorUtility.DisplayDialogComplex("Build Android APK",
$"Current version: {current} (build {code})\n\n" + $"Build the next version, {next} (build {code + 1})?\n\n" + "\"Same version\" rebuilds " + current + " without changing the number " + "(only for testing: a phone with this version installed may refuse it).",$"Build {next}", "Cancel", "Same version");

            if (choice == 1)
                return;

            string oldVersion = PlayerSettings.bundleVersion;
            int oldCode = code;
            if (choice == 0)
            {
                PlayerSettings.bundleVersion = next;
                PlayerSettings.Android.bundleVersionCode = code + 1;
            }
            else
            {
                PlayerSettings.bundleVersion = current;
            }

            if (!Build(PlayerSettings.bundleVersion))
            {
                // Nothing was handed out, so the number is free to use again
                PlayerSettings.bundleVersion = oldVersion;
                PlayerSettings.Android.bundleVersionCode = oldCode;
            }
            AssetDatabase.SaveAssets(); // writes the versioni into ProjectSettings.asset
        }

        private static bool Build(string version)
        {
            string[] scenes = EnabledScenes();
            if (scenes.Length == 0)
            {
                EditorUtility.DisplayDialog("Build Android APK", "No scenes are ticked in the build list. Add MainMenu first, then SampleScene.", "OK");
                return false;
            }

            Directory.CreateDirectory(OutputFolder);
            string path = Path.Combine(OutputFolder, FilePrefix + version + ".apk");
            EditorUserBuildSettings.buildAppBundle = false;

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };

            // The keystore is only set for this build, so its path and passwords never reach ProjectSettings.
            bool signed = UseKeystore();
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                ForgetKeystore();
            }
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"AndroidBuilder: build {version} failed ({report.summary.result}). " + "The version number was put back. See the errors above.");
                return false;
            }

            WriteUpdateInfo(path, version);
            if (!signed)
                Debug.LogWarning("AndroidBuilder: signed with this PC's debug key. Don't hand this build out: " +
                                 "players couldn't update from it. See Docs/Updates.md.");
            long megabytes = (long)(report.summary.totalSize / (1024 * 1024));
            Debug.Log($"AndroidBuilder: built {path} ({megabytes} MB), version {version}, " + $"build {PlayerSettings.Android.bundleVersionCode}.");
            EditorUtility.RevealInFinder(path);
            return true;
        }

        // Uses the team's keystore from the environment. False if it isn't set up on this PC.
        private static bool UseKeystore()
        {
            string keystore = Environment.GetEnvironmentVariable("SOULBIND_KEYSTORE");
            if (string.IsNullOrEmpty(keystore) || !File.Exists(keystore))
                return false;
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystore;
            PlayerSettings.Android.keystorePass = Environment.GetEnvironmentVariable("SOULBIND_KEYSTORE_PASS");
            PlayerSettings.Android.keyaliasName = Environment.GetEnvironmentVariable("SOULBIND_KEY_ALIAS");
            PlayerSettings.Android.keyaliasPass = Environment.GetEnvironmentVariable("SOULBIND_KEY_PASS");
            return true;
        }

        private static void ForgetKeystore()
        {
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.Android.keystoreName = "";
            PlayerSettings.Android.keystorePass = "";
            PlayerSettings.Android.keyaliasName = "";
            PlayerSettings.Android.keyaliasPass = "";
        }

        // version.json goes up with the APK: see Docs/Updates.md.
        private static void WriteUpdateInfo(string apkPath, string version)
        {
            string hash;
            using (FileStream file = File.OpenRead(apkPath))
            using (SHA256 sha = SHA256.Create())
                hash = BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant();

            var info = new UpdateInfo
            {
                versionCode = PlayerSettings.Android.bundleVersionCode,
                versionName = version,
                apkFile = Path.GetFileName(apkPath),
                sha256 = hash,
                sizeBytes = new FileInfo(apkPath).Length
            };
            File.WriteAllText(Path.Combine(OutputFolder, GameUpdater.InfoFile), JsonUtility.ToJson(info, true));
        }

        private static string[] EnabledScenes()
        {
            EditorBuildSettingsScene[] all = EditorBuildSettings.scenes;
            var enabled = new System.Collections.Generic.List<string>(all.Length);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].enabled)
                    enabled.Add(all[i].path);
            }
            return enabled.ToArray();
        }

        // "1.0" -> "1.0.0", so every version has three parts: major.minor.patch.
        private static string Normalize(string version)
        {
            int[] parts = Parse(version);
            return $"{parts[0]}.{parts[1]}.{parts[2]}";
        }

        // "1.0.3" -> "1.0.4"
        private static string NextPatch(string version)
        {
            int[] parts = Parse(version);
            return $"{parts[0]}.{parts[1]}.{parts[2] + 1}";
        }

        private static int[] Parse(string version)
        {
            var parts = new int[3];
            string[] pieces = (version ?? string.Empty).Split('.');
            for (int i = 0; i < 3 && i < pieces.Length; i++)
                int.TryParse(pieces[i], out parts[i]);
            return parts;
        }
    }
}
