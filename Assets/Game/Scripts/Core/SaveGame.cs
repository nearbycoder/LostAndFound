using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace LostAndFound
{
    [Serializable]
    public class DaySnapshot
    {
        public int day;
        public StoryState state;
    }

    [Serializable]
    public class DayBest
    {
        public int day;
        public int stamps;
        public int correct;
        public int total;
    }

    /// <summary>The week on disk: progress, story state, per-day snapshots for replays, best stamps,
    /// and every detail and secret ever found (those persist across replays).</summary>
    [Serializable]
    public class SaveGame
    {
        public int version = 1;
        public int currentDay = 1;       // the next day to play
        public int unlockedDay = 1;      // highest day reachable from Day Select
        public bool finished;
        public string ending = "";
        public StoryState state = new();
        public List<DaySnapshot> snapshots = new();
        public List<DayBest> best = new();
        public List<string> discovered = new();   // "objectId.detailId"
        public bool tutorialDone;
        public int casesDone;                    // cases already decided today: Continue picks up from the next one

        /// <summary>-lafSave &lt;path&gt; points recordings and tests at a scratch save instead of the player's.</summary>
        public static string PathOnDisk => !string.IsNullOrEmpty(Game.Arg("-lafSave")) ? Game.Arg("-lafSave")
            : System.IO.Path.Combine(Application.persistentDataPath, "lostandfound_save.json");

        /// <summary>What a load had to do about a damaged save, kept until the title has said so (TakeLoadNote).</summary>
        public static string LoadNote { get; private set; }

        public static string TakeLoadNote()
        {
            string n = LoadNote;
            LoadNote = null;
            return n;
        }

        public static SaveGame Load() => LoadFrom(PathOnDisk);

        public static bool Exists => File.Exists(PathOnDisk);

        public void Write() => WriteTo(PathOnDisk);

        public static string BackupOf(string path) => path + ".bak";

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void LafSyncFS();
        /// <summary>In a browser the files live in memory until they're flushed to IndexedDB (Plugins/WebGL/LafSyncFS.jslib).</summary>
        static void Persist() => LafSyncFS();
#else
        static void Persist() { }
#endif

        /// <summary>The save at path. If it can't be read (a crash mid-write, a full disk), the previous save from the
        /// backup instead. If neither can be read, the damaged file is moved aside (never deleted) so a new week can't
        /// overwrite it, and null comes back.</summary>
        public static SaveGame LoadFrom(string path)
        {
            string bak = BackupOf(path);
            if (!File.Exists(path) && !File.Exists(bak)) return null;
            var s = Read(path, out string why);
            if (s != null) return s;
            var b = Read(bak, out string bakWhy);
            if (b != null)
            {
                Debug.LogWarning($"[Save] {Path.GetFileName(path)} {why}; using the previous save from the backup");
                SetAside(path);
                b.WriteTo(path);   // the restored save becomes the save again, so Continue finds it
                LoadNote = "Your last save couldn't be read, so the one before it was loaded: the last claim may need deciding again.";
                return b;
            }
            Debug.LogWarning($"[Save] {Path.GetFileName(path)} {why}, and the backup {bakWhy}");
            string aside = SetAside(path);
            if (aside != null) LoadNote = $"Your save couldn't be read. It's been kept as {Path.GetFileName(aside)}, next to where it was.";
            return null;
        }

        static SaveGame Read(string path, out string why)
        {
            why = "is missing";
            try
            {
                if (!File.Exists(path)) return null;
                var s = JsonUtility.FromJson<SaveGame>(File.ReadAllText(path));
                if (s != null && s.version == 1 && s.state != null) return s;
                why = "isn't a save this version understands";
            }
            catch (Exception e) { why = "can't be read (" + e.Message + ")"; }
            return null;
        }

        /// <summary>Move a damaged save to "name.damaged" (or "name.damaged-2" and so on), and return where it went.</summary>
        static string SetAside(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                string to = path + ".damaged";
                for (int i = 2; File.Exists(to); i++) to = $"{path}.damaged-{i}";
                File.Move(path, to);
                Persist();
                return to;
            }
            catch (Exception e) { Debug.LogWarning("[Save] couldn't move the damaged save aside: " + e.Message); return null; }
        }

        /// <summary>Write the whole save to a temporary file, flush it to disk, then swap it in with one rename, keeping
        /// the previous save as the backup. A crash at any point leaves either the old save or the new one, never half.</summary>
        public void WriteTo(string path)
        {
            try
            {
                string tmp = path + ".tmp";
                using (var f = new FileStream(tmp, FileMode.Create, FileAccess.Write))
                using (var w = new StreamWriter(f))
                {
                    w.Write(JsonUtility.ToJson(this, true));
                    w.Flush();
                    f.Flush(true);
                }
                if (File.Exists(path)) File.Replace(tmp, path, BackupOf(path));
                else File.Move(tmp, path);
                Persist();
            }
            catch (Exception e) { Debug.LogWarning("[Save] could not write save: " + e.Message); }
        }

        /// <summary>A new week: the save and its backup go (damaged files set aside are left alone).</summary>
        public static void Delete()
        {
            try
            {
                if (File.Exists(PathOnDisk)) File.Delete(PathOnDisk);
                if (File.Exists(BackupOf(PathOnDisk))) File.Delete(BackupOf(PathOnDisk));
                Persist();
            }
            catch { }
        }

        public void Snapshot(int day, StoryState s)
        {
            snapshots.RemoveAll(x => x.day == day);
            snapshots.Add(new DaySnapshot { day = day, state = s.Clone() });
        }

        public StoryState SnapshotFor(int day) => snapshots.FirstOrDefault(x => x.day == day)?.state?.Clone();

        public DayBest Best(int day) => best.FirstOrDefault(b => b.day == day);

        public void RecordBest(int day, int stamps, int correct, int total)
        {
            var b = Best(day);
            if (b == null) best.Add(new DayBest { day = day, stamps = stamps, correct = correct, total = total });
            else if (stamps > b.stamps || (stamps == b.stamps && correct > b.correct)) { b.stamps = stamps; b.correct = correct; b.total = total; }
        }
    }
}
