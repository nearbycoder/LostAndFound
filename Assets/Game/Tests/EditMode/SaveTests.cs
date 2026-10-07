using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LostAndFound.Tests
{
    /// <summary>
    /// The save on disk survives a crash: writes swap in whole, a save that can't be read falls back to the one before,
    /// and a damaged save is never deleted or overwritten. Every file lives in a scratch folder under Logs/, never the
    /// player's real save.
    /// </summary>
    public class SaveTests
    {
        string dir, path;

        [SetUp]
        public void SetUp()
        {
            dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "savetests", System.Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(dir);
            path = Path.Combine(dir, "lostandfound_save.json");
            LogAssert.ignoreFailingMessages = true;   // the warnings about damaged saves are expected here
            SaveGame.TakeLoadNote();
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        static SaveGame Day(int day) => new SaveGame { currentDay = day, unlockedDay = day, casesDone = 1 };

        [Test]
        public void RoundTripLeavesNoTemporaryFile()
        {
            Day(3).WriteTo(path);
            var back = SaveGame.LoadFrom(path);
            Assert.IsNotNull(back);
            Assert.AreEqual(3, back.currentDay);
            Assert.AreEqual(1, back.casesDone);
            Assert.IsNull(SaveGame.LoadNote);
            Assert.IsFalse(File.Exists(path + ".tmp"));
        }

        [Test]
        public void EndingsReachedSurviveARoundTrip()
        {
            var s = Day(5);
            s.NoteEnding("nine40");
            s.NoteEnding("grey");
            s.NoteEnding("nine40");   // twice is still once
            s.NoteEnding("");
            s.WriteTo(path);
            var back = SaveGame.LoadFrom(path);
            CollectionAssert.AreEqual(new[] { "nine40", "grey" }, back.endingsSeen);
        }

        [Test]
        public void ASaveFromBeforeEndingsWereRememberedStartsWithItsEnding()
        {
            // round 5's save format: a finished week, no endingsSeen
            File.WriteAllText(path, "{\"version\":1,\"currentDay\":5,\"unlockedDay\":6,\"finished\":true,\"ending\":\"longwait\",\"state\":{}}");
            var back = SaveGame.LoadFrom(path);
            Assert.IsNotNull(back);
            Assert.IsTrue(back.finished);
            CollectionAssert.AreEqual(new[] { "longwait" }, back.endingsSeen);
            File.WriteAllText(path, "{\"version\":1,\"currentDay\":2,\"state\":{}}");
            CollectionAssert.IsEmpty(SaveGame.LoadFrom(path).endingsSeen, "a week not yet finished has reached no ending");
        }

        [Test]
        public void NoSaveMeansNothingToLoad()
        {
            Assert.IsNull(SaveGame.LoadFrom(path));
            Assert.IsNull(SaveGame.LoadNote);
            Assert.AreEqual(0, Directory.GetFiles(dir).Length, "loading nothing creates nothing");
        }

        [Test]
        public void EachWriteKeepsThePreviousSaveAsTheBackup()
        {
            Day(2).WriteTo(path);
            Day(3).WriteTo(path);
            Assert.AreEqual(3, SaveGame.LoadFrom(path).currentDay);
            Assert.AreEqual(2, JsonUtility.FromJson<SaveGame>(File.ReadAllText(SaveGame.BackupOf(path))).currentDay);
        }

        [Test]
        public void ATruncatedSaveFallsBackToTheBackup()
        {
            Day(2).WriteTo(path);
            Day(3).WriteTo(path);
            string full = File.ReadAllText(path);
            File.WriteAllText(path, full.Substring(0, full.Length / 2));   // a crash halfway through an in-place write
            var s = SaveGame.LoadFrom(path);
            Assert.IsNotNull(s);
            Assert.AreEqual(2, s.currentDay, "the save before the damaged one");
            Assert.IsNotNull(SaveGame.TakeLoadNote(), "the title says what happened");
            Assert.AreEqual(full.Substring(0, full.Length / 2), File.ReadAllText(path + ".damaged"), "the damaged save is kept as it was");
            Assert.AreEqual(2, SaveGame.LoadFrom(path).currentDay, "the restored save is the save again (Continue finds it)");
            Assert.IsNull(SaveGame.LoadNote, "and reads cleanly");
        }

        [Test]
        public void AnEmptySaveFallsBackToTheBackup()
        {
            Day(4).WriteTo(path);
            Day(5).WriteTo(path);
            File.WriteAllText(path, "");
            Assert.AreEqual(4, SaveGame.LoadFrom(path).currentDay);
        }

        [Test]
        public void AMissingSaveWithABackupLoadsTheBackup()
        {
            Day(2).WriteTo(path);
            Day(3).WriteTo(path);
            File.Delete(path);
            Assert.AreEqual(2, SaveGame.LoadFrom(path).currentDay);
        }

        [Test]
        public void ADamagedSaveWithNoBackupIsSetAsideNeverDeleted()
        {
            File.WriteAllText(path, "{\"version\":1,\"currentDay\":4,\"sta");
            Assert.IsNull(SaveGame.LoadFrom(path));
            StringAssert.Contains("lostandfound_save.json.damaged", SaveGame.TakeLoadNote());
            Assert.IsFalse(File.Exists(path), "out of the way of a new week");
            Assert.AreEqual("{\"version\":1,\"currentDay\":4,\"sta", File.ReadAllText(path + ".damaged"));

            // a second damaged save doesn't replace the first one set aside
            File.WriteAllText(path, "not json");
            Assert.IsNull(SaveGame.LoadFrom(path));
            Assert.IsTrue(File.Exists(path + ".damaged"));
            Assert.AreEqual("not json", File.ReadAllText(path + ".damaged-2"));

            // and a new week writes cleanly beside them
            Day(1).WriteTo(path);
            Assert.AreEqual(1, SaveGame.LoadFrom(path).currentDay);
            Assert.IsTrue(File.Exists(path + ".damaged") && File.Exists(path + ".damaged-2"));
        }

        [Test]
        public void AFailedWriteLeavesTheSaveAsItWas()
        {
            Day(2).WriteTo(path);
            string before = File.ReadAllText(path);
            Directory.CreateDirectory(path + ".tmp");   // the temporary file can't be written
            Day(3).WriteTo(path);
            Assert.AreEqual(before, File.ReadAllText(path));
            Assert.AreEqual(2, SaveGame.LoadFrom(path).currentDay);
        }

        [Test]
        public void ASaveFromAnotherVersionIsNotLoaded()
        {
            File.WriteAllText(path, "{\"version\":7,\"currentDay\":2}");
            Assert.IsNull(SaveGame.LoadFrom(path));
            Assert.IsTrue(File.Exists(path + ".damaged"), "kept, in case a later version can read it");
        }
    }
}
