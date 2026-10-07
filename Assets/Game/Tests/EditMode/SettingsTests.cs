using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LostAndFound.Tests
{
    /// <summary>Settings live in settings.json beside the save (not PlayerPrefs): they come back as they were left, and a
    /// file that can't be read gives the defaults rather than an error. Every file is in a scratch folder under Logs/.</summary>
    public class SettingsTests
    {
        string dir, path;

        [SetUp]
        public void SetUp()
        {
            dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "settingstests", System.Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(dir);
            path = Path.Combine(dir, "settings.json");
            Settings.UseFile(path);
        }

        [TearDown]
        public void TearDown()
        {
            Settings.UseFile(null);
            LogAssert.ignoreFailingMessages = false;
            Directory.Delete(dir, true);
        }

        [Test]
        public void DefaultsWithNoFile()
        {
            Assert.IsFalse(Settings.PlainLettering);
            Assert.AreEqual(0, Settings.TextSize);
            Assert.AreEqual(2, Settings.PictureQuality);
            Assert.IsTrue(Settings.OfferNudges);
            Assert.IsFalse(File.Exists(path), "reading alone writes nothing");
        }

        [Test]
        public void ComeBackAsTheyWereLeft()
        {
            Settings.PlainLettering = true;
            Settings.TextSize = 1;
            Settings.PictureQuality = 0;
            Settings.MusicVolume = 0.25f;
            Settings.OfferNudges = false;
            Assert.IsTrue(File.Exists(path));
            Assert.IsFalse(File.Exists(path + ".tmp"), "the temporary file is swapped in, not left behind");
            Settings.UseFile(path);   // as the next launch reads them
            Assert.IsTrue(Settings.PlainLettering);
            Assert.AreEqual(1, Settings.TextSize);
            Assert.AreEqual(0, Settings.PictureQuality);
            Assert.AreEqual(0.25f, Settings.MusicVolume, 1e-5f);
            Assert.IsFalse(Settings.OfferNudges);
        }

        [Test]
        public void ADamagedFileGivesTheDefaults()
        {
            File.WriteAllText(path, "{\"values\": [ {\"key\": \"plain\", \"val");
            LogAssert.ignoreFailingMessages = true;
            Settings.UseFile(path);
            Assert.IsFalse(Settings.PlainLettering);
            Assert.AreEqual(2, Settings.PictureQuality);
            Settings.PlainLettering = true;   // and the next change writes a good file over it
            Settings.UseFile(path);
            Assert.IsTrue(Settings.PlainLettering);
        }
    }
}
