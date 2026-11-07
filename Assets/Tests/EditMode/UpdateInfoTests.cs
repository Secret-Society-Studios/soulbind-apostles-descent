using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests
{
    public class UpdateInfoTests
    {
        private static UpdateInfo Build(int code = 6, string file = "SoulBind-v1.0.5.apk") => new UpdateInfo
        {
            versionCode = code,
            versionName = "1.0.5",
            apkFile = file,
            sha256 = new string('a', 64),
            sizeBytes = 80L * 1024L * 1024L
        };

        [Test]
        public void IsNewerThan_OnlyForAHigherBuildNumber()
        {
            Assert.IsTrue(Build(6).IsNewerThan(5));
            Assert.IsFalse(Build(6).IsNewerThan(6));
            Assert.IsFalse(Build(6).IsNewerThan(7));
        }

        [Test]
        public void IsNewerThan_NeverForIncompleteInfo()
        {
            UpdateInfo noHash = Build();
            noHash.sha256 = "abc";
            UpdateInfo noName = Build();
            noName.versionName = "";

            Assert.IsFalse(noHash.IsNewerThan(1));
            Assert.IsFalse(noName.IsNewerThan(1));
            Assert.IsFalse(new UpdateInfo().IsNewerThan(0));
        }

        [TestCase("../other.apk")]
        [TestCase("folder/game.apk")]
        [TestCase("folder\\game.apk")]
        [TestCase("game.exe")]
        [TestCase("")]
        public void IsValid_RejectsAnythingButAPlainApkName(string file)
        {
            Assert.IsFalse(Build(6, file).IsValid);
        }

        [Test]
        public void SizeMegabytes_IsAtLeastOne()
        {
            UpdateInfo tiny = Build();
            tiny.sizeBytes = 10L;

            Assert.AreEqual(80, Build().SizeMegabytes);
            Assert.AreEqual(1, tiny.SizeMegabytes);
        }
    }
}
