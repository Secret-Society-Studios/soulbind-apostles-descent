using System.IO;
using PlasticGui.Configuration;
using Unity.Services.Lobbies.Models;
using Unity.VisualScripting;
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

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"AndroidBuilder: build {version} failed ({report.summary.result}). " + "The version number was put back. See the errors above.");
                return false;
            }

            long megabytes = (long)(report.summary.totalSize / (1024 * 1024));
            Debug.Log($"AndroidBuilder: built {path} ({megabytes} MB), version {version}, " + $"build {PlayerSettings.Android.bundleVersionCode}.");
            EditorUtility.RevealInFinder(path);
            return true;
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