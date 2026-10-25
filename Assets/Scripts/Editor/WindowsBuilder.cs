using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SoulBind.EditorTools
{
    /// <summary>
    /// SoulBind > Build Windows: builds the PC version with the
    /// current version number (it does NOT raise it; the Android
    /// build does that), into Builds/Windows/SoulBind-v1.0.3/.
    /// </summary>
    public static class WindowsBuilder
    {
        [MenuItem("SoulBind/Build Windows")]
        private static void Build()
        {
            string version = PlayerSettings.bundleVersion;
            string folder = Path.Combine("Builds", "Windows",
                "SoulBind-v" + version);
            Directory.CreateDirectory(folder);

            var scenes = new System.Collections.Generic.List<string>();
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            {
                if (s.enabled)
                    scenes.Add(s.path);
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = Path.Combine(folder, "SoulBind.exe"),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("WindowsBuilder: build failed. " + "See the errors above.");
                return;
            }
            Debug.Log($"WindowsBuilder: built version {version} " + $"in {folder}.");
            EditorUtility.RevealInFinder(folder);
        }
    }
}