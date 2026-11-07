using System;

namespace SoulBind.Core
{
    /// <summary>
    /// What version.json says about the newest build. AndroidBuilder writes it next to the APK, and the
    /// game reads it on launch. Field names match the file, so they stay lower case.
    /// </summary>
    [Serializable]
    public sealed class UpdateInfo
    {
        private const int HashLength = 64; // SHA-256, in hex

        public int versionCode;
        public string versionName;
        public string apkFile;
        public string sha256;
        public long sizeBytes;

        /// <summary>Complete, and apkFile is a plain file name: it is put on the end of the download address.</summary>
        public bool IsValid =>
            versionCode > 0 && !string.IsNullOrEmpty(versionName) && IsPlainFileName(apkFile) && IsHash(sha256);

        /// <summary>True if a game with this Android build number should update.</summary>
        public bool IsNewerThan(int installedCode) => IsValid && versionCode > installedCode;

        /// <summary>The download's size for the prompt, at least 1.</summary>
        public int SizeMegabytes => (int)Math.Max(1L, sizeBytes / (1024L * 1024L));

        private static bool IsPlainFileName(string name)
        {
            if (string.IsNullOrEmpty(name) || !name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
                return false;
            return name.IndexOf('/') < 0 && name.IndexOf('\\') < 0 && name.IndexOf("..", StringComparison.Ordinal) < 0;
        }

        private static bool IsHash(string hash)
        {
            if (hash == null || hash.Length != HashLength)
                return false;
            foreach (char c in hash)
            {
                if (!Uri.IsHexDigit(c))
                    return false;
            }
            return true;
        }
    }
}
