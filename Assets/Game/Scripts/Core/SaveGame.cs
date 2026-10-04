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

        /// <summary>-lafSave &lt;path&gt; points recordings and tests at a scratch save instead of the player's.</summary>
        public static string PathOnDisk => !string.IsNullOrEmpty(Game.Arg("-lafSave")) ? Game.Arg("-lafSave")
            : System.IO.Path.Combine(Application.persistentDataPath, "lostandfound_save.json");

        public static SaveGame Load()
        {
            try
            {
                if (File.Exists(PathOnDisk))
                {
                    var s = JsonUtility.FromJson<SaveGame>(File.ReadAllText(PathOnDisk));
                    if (s != null && s.version == 1) return s;
                }
            }
            catch (Exception e) { Debug.LogWarning("[Save] could not read save: " + e.Message); }
            return null;
        }

        public static bool Exists => File.Exists(PathOnDisk);

        public void Write()
        {
            try { File.WriteAllText(PathOnDisk, JsonUtility.ToJson(this, true)); }
            catch (Exception e) { Debug.LogWarning("[Save] could not write save: " + e.Message); }
        }

        public static void Delete()
        {
            try { if (File.Exists(PathOnDisk)) File.Delete(PathOnDisk); } catch { }
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
