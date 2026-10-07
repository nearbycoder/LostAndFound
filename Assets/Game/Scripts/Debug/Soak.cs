using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;

namespace LostAndFound
{
    /// <summary>
    /// -lafSoak &lt;dir&gt; [-lafCycles N]: plays on without ever quitting, the way someone who leaves the game open all
    /// evening does. Each cycle goes through the menus' own buttons: Continue (or Choose a Day) from the title, decide a
    /// claim, then Start the day again or Back to the title from the pause menu. Every one of those tears the game down
    /// and builds it again in the same process (Game.Restart). After each cycle it logs the managed heap, Unity's
    /// allocated memory, the process's resident memory and the live Materials, Meshes, Textures and GameObjects, then
    /// says whether the last ten cycles held steady. Lives outside the Game object, so it survives the rebuilds.
    /// </summary>
    public class Soak : MonoBehaviour
    {
        public static Soak I { get; private set; }
        string dir;
        int cycles = 30;
        float speed = 4f;
        int shots;
        readonly List<Sample> samples = new();
        readonly List<string> problems = new();
        readonly HashSet<string> confirmsShot = new();
        int asks;

        struct Sample
        {
            public int cycle;
            public string what;
            public long mono, unity, rss;
            public int materials, meshes, textures, objects;
        }

        public static void Ensure()
        {
            if (I != null) return;
            var go = new GameObject("Soak");
            DontDestroyOnLoad(go);
            I = go.AddComponent<Soak>();
        }

        void Start()
        {
            dir = Game.Arg("-lafSoak");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Application.persistentDataPath, "soak");
            Directory.CreateDirectory(dir);
            if (int.TryParse(Game.Arg("-lafCycles") ?? "", out int n) && n > 0) cycles = n;
            if (float.TryParse(Game.Arg("-lafSpeed") ?? "", out float s) && s > 0f) speed = s;
            Application.runInBackground = true;
            Application.logMessageReceived += (msg, stack, type) =>
            {
                if (type == LogType.Exception || type == LogType.Error) problems.Add($"{type}: {msg.Split('\n')[0]}");
            };
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            float t0 = Time.realtimeSinceStartup;
            yield return UntilTitle();
            yield return Measure(0, "title, first time");
            if (Button("Choose a Day") != null)
            {
                // a save with days behind it (a finished week, say): replay Tuesday from the title
                Debug.Log($"[Soak] starting from a save on day {Game.I.Save.currentDay}{(Game.I.Save.finished ? ", week finished" : "")}");
                yield return Click("Choose a Day");
                yield return Click("Tuesday", Expect(2));
            }
            else
            {
                // a fresh save has no Choose a Day: start the week on Tuesday, as Choose a Day would, so there's a day to go back to
                yield return Click("Begin");
                float until = Time.realtimeSinceStartup + 30f;
                while ((Director.I == null || !Director.I.Running) && Time.realtimeSinceStartup < until) yield return null;
                Game.I.StartFromDay(2);
            }
            yield return UntilDay();
            for (int cycle = 1; cycle <= cycles; cycle++)
            {
                string what;
                switch (cycle % 3)
                {
                    case 1:
                        // decide a claim, then start the day again: the game rebuilds straight into the day's morning
                        yield return DecideOne();
                        yield return Pause("Start the day again", Director.I.Save.casesDone > 0);
                        yield return UntilDay();
                        what = "start the day again";
                        break;
                    case 2:
                        // decide a claim, back to the title, then Continue
                        yield return DecideOne();
                        yield return Pause("Back to the title");
                        yield return UntilTitle();
                        yield return Click("Continue");
                        yield return UntilDay();
                        what = "back to the title, Continue";
                        break;
                    default:
                        // back to the title, Choose a Day, an earlier day
                        yield return Pause("Back to the title");
                        yield return UntilTitle();
                        yield return Click("Choose a Day");
                        int day = Mathf.Max(1, Game.I.Save.currentDay - 1);
                        yield return Click(Game.I.Db.Day(day).weekday, Expect(day));
                        yield return UntilDay();
                        what = $"Choose a Day ({Game.I.Db.Day(day).weekday})";
                        break;
                }
                yield return Measure(cycle, what);
                if (Time.realtimeSinceStartup - t0 > 3000f) { problems.Add("timed out"); break; }
            }
            Summary(Time.realtimeSinceStartup - t0);
            Application.Quit();
        }

        // ------------------------------------------------------------------ steps

        IEnumerator UntilTitle()
        {
            float until = Time.realtimeSinceStartup + 60f;
            while ((UIRoot.I == null || UIRoot.I.root.Find("Title/Menu") == null) && Time.realtimeSinceStartup < until) yield return null;
            if (UIRoot.I == null || UIRoot.I.root.Find("Title/Menu") == null) { problems.Add("the title never came"); yield break; }
            yield return new WaitForSecondsRealtime(1.2f);   // the title fades in
        }

        /// <summary>Wait for the day to reach the bell or a claimant at the window, ringing and talking along at speed.</summary>
        IEnumerator UntilDay()
        {
            float until = Time.realtimeSinceStartup + 120f;
            while (Time.realtimeSinceStartup < until)
            {
                var d = Director.I;
                if (d != null && d.Running)
                {
                    d.autoAdvance = true;
                    d.autoRing = true;
                    Time.timeScale = speed;
                    if (d.CanUseStamps && d.Current != null && !UIRoot.ModalOpen) yield break;
                }
                yield return null;
            }
            problems.Add($"no claimant came (day {Director.I?.Day})");
        }

        /// <summary>Refuse the claim at the window (the stamp itself isn't what's being soaked), and wait for the next.</summary>
        IEnumerator DecideOne()
        {
            yield return UntilDay();
            var d = Director.I;
            var c = d.Current;
            if (c == null) yield break;
            int done = d.Save.casesDone;
            d.CommitStamp(Verdict.Refuse, 0);
            float until = Time.realtimeSinceStartup + 60f;
            while (d != null && d.Save.casesDone == done && Time.realtimeSinceStartup < until) yield return null;
            if (d == null || d.Save.casesDone == done) problems.Add($"claim {c.id} was never decided");
            yield return new WaitForSecondsRealtime(0.3f);
        }

        /// <summary>Whether replaying this day from the title should ask first: during an unfinished week, going back to an
        /// earlier day, or to today once a claim has been decided (worked out here, not by the game's own ProgressGuard).</summary>
        static bool Expect(int day)
        {
            var s = Game.I.Save;
            return !s.finished && (day < s.currentDay || (day == s.currentDay && s.casesDone > 0));
        }

        IEnumerator Pause(string button, bool expectAsk = false)
        {
            var menu = FindAnyObjectByType<PauseMenu>();
            menu.Show();
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Click(button, expectAsk);
        }

        /// <summary>Click the menu item that starts with this text, as a player's click does. If it asks to be clicked
        /// again (it says what would be lost), check that the one click changed nothing, shoot it once and click again.
        /// It should ask exactly when <paramref name="expectAsk"/> says so.</summary>
        IEnumerator Click(string text, bool expectAsk = false)
        {
            var b = Button(text);
            if (b == null) { problems.Add($"no \"{text}\" to click"); yield break; }
            var game = Game.I;
            string before = Plain(b.label.text);
            if (text == "Continue" && confirmsShot.Add("Continue")) { Shot("title_continue"); yield return null; }
            int day = Director.I != null ? Director.I.Day : 0, done = game.Save.casesDone;
            b.OnPointerClick(null);
            yield return new WaitForSecondsRealtime(0.2f);
            bool asked = b != null && Game.I == game && Plain(b.label.text) != before && Plain(b.label.text).ToLowerInvariant().Contains("click again");
            if (asked)
            {
                Debug.Log($"[Soak] \"{before}\" asked first: \"{Plain(b.label.text)}\"");
                // one click mustn't have done anything: same game, same day, same claims decided
                if (Game.I != game || (Director.I != null ? Director.I.Day : 0) != day || game.Save.casesDone != done)
                    problems.Add($"one click on \"{before}\" already acted");
                if (confirmsShot.Add(before.Split(' ')[0])) Shot("confirm_" + before.Split(' ')[0].ToLowerInvariant());
                yield return new WaitForSecondsRealtime(0.3f);
                asks++;
                b.OnPointerClick(null);
            }
            if (asked != expectAsk) problems.Add($"\"{before}\" {(asked ? "asked first, but nothing would be lost" : "acted on one click, losing progress")}");
            yield return new WaitForSecondsRealtime(0.3f);
        }

        static PaperButton Button(string text) => UIRoot.I == null ? null : UIRoot.I.root.GetComponentsInChildren<PaperButton>()
            .FirstOrDefault(b => b.interactable && b.label != null && Plain(b.label.text).StartsWith(text));

        static string Plain(string s) => System.Text.RegularExpressions.Regex.Replace(s ?? "", "<[^>]*>", "").Trim();

        void Shot(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"{shots++:000}_{name}.png"));

        // ------------------------------------------------------------------ measuring

        IEnumerator Measure(int cycle, string what)
        {
            yield return new WaitForSecondsRealtime(1.5f);
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
            yield return null;
            var s = new Sample
            {
                cycle = cycle,
                what = what,
                mono = System.GC.GetTotalMemory(false),
                unity = Profiler.GetTotalAllocatedMemoryLong(),
                rss = Rss(),
                materials = Resources.FindObjectsOfTypeAll<Material>().Length,
                meshes = Resources.FindObjectsOfTypeAll<Mesh>().Length,
                textures = Resources.FindObjectsOfTypeAll<Texture>().Length,
                objects = Resources.FindObjectsOfTypeAll<GameObject>().Length,
            };
            samples.Add(s);
            LogGrowth(cycle);
            Debug.Log($"[Soak] cycle {cycle} ({what}): managed {Mb(s.mono)}, unity {Mb(s.unity)}, resident {Mb(s.rss)}; " +
                      $"materials {s.materials}, meshes {s.meshes}, textures {s.textures}, gameobjects {s.objects}; day {Director.I?.Day}, load {Load()}");
            if (cycle == 1 || cycle == cycles) Shot($"cycle{cycle}");
        }

        /// <summary>One line of the same numbers, for other runs (the AutoPilot's -lafWeeks) to log.</summary>
        public static string MemoryLine()
        {
            System.GC.Collect();
            return $"managed {Mb(System.GC.GetTotalMemory(false))}, unity {Mb(Profiler.GetTotalAllocatedMemoryLong())}, resident {Mb(Rss())}; " +
                   $"materials {Resources.FindObjectsOfTypeAll<Material>().Length}, meshes {Resources.FindObjectsOfTypeAll<Mesh>().Length}, " +
                   $"textures {Resources.FindObjectsOfTypeAll<Texture>().Length}, gameobjects {Resources.FindObjectsOfTypeAll<GameObject>().Length}; load {Load()}";
        }

        Dictionary<string, int> lastNames;

        /// <summary>Which kinds of material and texture there are more of than after the last cycle, by name.</summary>
        void LogGrowth(int cycle)
        {
            var names = new Dictionary<string, int>();
            foreach (var o in Resources.FindObjectsOfTypeAll<Material>().Cast<Object>().Concat(Resources.FindObjectsOfTypeAll<Texture>()).Concat(Resources.FindObjectsOfTypeAll<Mesh>()))
            {
                string key = $"{o.GetType().Name} \"{o.name}\"";
                names[key] = names.TryGetValue(key, out int k) ? k + 1 : 1;
            }
            if (lastNames != null && cycle >= 2)
            {
                var grew = names.Where(kv => kv.Value > (lastNames.TryGetValue(kv.Key, out int was) ? was : 0))
                    .Select(kv => $"{kv.Key} +{kv.Value - (lastNames.TryGetValue(kv.Key, out int was) ? was : 0)}").ToList();
                if (grew.Count > 0) Debug.Log($"[Soak]   more than last cycle: {string.Join(", ", grew.Take(16))}");
            }
            lastNames = names;
        }

        static string Mb(long b) => $"{b / 1048576.0:0.0} MB";

        static long Rss()
        {
            try
            {
                foreach (var line in File.ReadLines("/proc/self/status"))
                    if (line.StartsWith("VmRSS:")) return long.Parse(line.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries)[1]) * 1024;
            }
            catch { }
            return 0;
        }

        static string Load()
        {
            try { return File.ReadAllText("/proc/loadavg").Split(' ')[0]; }
            catch { return "?"; }
        }

        void Summary(float seconds)
        {
            // steady: over the last ten cycles, nothing climbs by more than 2% (the first cycles warm caches up)
            var tail = samples.Where(x => x.cycle > 0).Skip(System.Math.Max(0, samples.Count(x => x.cycle > 0) - 10)).ToList();
            bool steady = true;
            void Check(string name, System.Func<Sample, double> f)
            {
                if (tail.Count < 2) return;
                double first = f(tail[0]), last = f(tail[^1]), max = tail.Max(f);
                double growth = first > 0 ? (last - first) / first : 0;
                Debug.Log($"[Soak] {name}: {first:0.#} → {last:0.#} over the last {tail.Count} cycles ({growth * 100:+0.0;-0.0}%), most {max:0.#}");
                if (growth > 0.02) steady = false;
            }
            var all = samples.Where(x => x.cycle > 0).ToList();
            if (all.Count > 0)
                Debug.Log($"[Soak] from cycle 1 to {all[^1].cycle}: materials {all[0].materials} → {all[^1].materials}, meshes {all[0].meshes} → {all[^1].meshes}, " +
                          $"textures {all[0].textures} → {all[^1].textures}, gameobjects {all[0].objects} → {all[^1].objects}, unity {Mb(all[0].unity)} → {Mb(all[^1].unity)}, resident {Mb(all[0].rss)} → {Mb(all[^1].rss)}");
            Check("materials", x => x.materials);
            Check("meshes", x => x.meshes);
            Check("textures", x => x.textures);
            Check("gameobjects", x => x.objects);
            Check("unity MB", x => x.unity / 1048576.0);
            Check("managed MB", x => x.mono / 1048576.0);
            foreach (var p in problems.Distinct()) Debug.Log("[Soak] problem: " + p);
            // a short run is still loading days for the first time: too soon to call memory steady or growing
            bool judged = all.Count >= 20;
            if (!judged) steady = true;
            Debug.Log($"[Soak] {samples.Count - 1} cycles in {seconds:0}s; {asks} menu choices asked for a second click; " +
                      (judged ? steady ? "steady" : "GROWING" : "too few cycles to judge memory (20 or more)"));
            Debug.Log(problems.Count == 0 && steady ? "[Soak] PASS" : "[Soak] FAIL");
        }
    }
}
