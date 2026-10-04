using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace LostAndFound
{
    /// <summary>
    /// -lafAutopilot &lt;dir&gt;: plays the whole week by itself with the rules solver's verdicts, through
    /// the same desk systems a player uses (pick up, inspect, tray, stamp), screenshotting every case,
    /// ledger and the ending into &lt;dir&gt;. Logs "[Auto] PASS" when every case got its best grade.
    /// </summary>
    public class AutoPilot : MonoBehaviour
    {
        string dir;
        int shots;
        readonly HashSet<string> handled = new();
        readonly List<string> problems = new();
        int cases;

        void Start()
        {
            dir = Game.Arg("-lafAutopilot");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Application.persistentDataPath, "autopilot");
            Directory.CreateDirectory(dir);
            Application.runInBackground = true;
            Application.logMessageReceived += OnLog;
            Director.I.Autopilot = true;
            Time.timeScale = float.TryParse(Game.Arg("-lafSpeed") ?? "", out float sp) ? sp : 2f;
            if (int.TryParse(Game.Arg("-lafDay") ?? "", out int day) && day > 1) Game.I.StartFromDay(day);
            else Game.I.NewWeek();
            StartCoroutine(Run());
        }

        void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error) problems.Add($"{type}: {msg.Split('\n')[0]}");
        }

        void Shot(string name)
        {
            string path = Path.Combine(dir, $"{shots++:000}_{name}.png");
            ScreenCapture.CaptureScreenshot(path);
        }

        IEnumerator Run()
        {
            var d = Director.I;
            int lastLedgerDay = 0;
            float t0 = Time.realtimeSinceStartup;
            while (true)
            {
                if (d.Save.finished)
                {
                    yield return new WaitForSecondsRealtime(6f);
                    Shot("ending");
                    yield return new WaitForSecondsRealtime(1f);
                    break;
                }
                if (UIRoot.ModalOpen && d.DayDef != null && d.Day != lastLedgerDay && GameObject.Find("Ledger") != null)
                {
                    lastLedgerDay = d.Day;
                    yield return new WaitForSecondsRealtime(2.2f);
                    Shot($"day{d.Day}_ledger");
                }
                if (d.CanUseStamps && d.Current != null && !handled.Contains(d.Current.id))
                {
                    var c = d.Current;
                    handled.Add(c.id);
                    yield return PlayCase(d, c);
                }
                if (Time.realtimeSinceStartup - t0 > 1800f) { problems.Add("timed out"); break; }
                yield return null;
            }
            int best = d.State.records.Count(r => r.grade == "best");
            int skipped = d.State.records.Count(r => r.grade == "skip");
            Debug.Log($"[Auto] week done: {cases} cases, {best} best, {skipped} skipped, ending {d.Save.ending}, {problems.Count} problems, {Time.realtimeSinceStartup - t0:0}s");
            foreach (var p in problems) Debug.Log("[Auto] problem: " + p);
            Debug.Log(problems.Count == 0 && best + skipped == cases ? "[Auto] PASS" : "[Auto] FAIL");
            Application.Quit();
        }

        IEnumerator PlayCase(Director d, CaseDef c)
        {
            cases++;
            yield return new WaitForSeconds(0.3f);
            Shot($"case{c.id}_window");
            var dec = Rules.Solve(d.Db, d.Day, c, d.State);
            var def = d.Db.Object(c.wants);
            Desk.I.items.TryGetValue(c.wants ?? "", out var item);

            // look the object over: open it, find every case detail (under the lamp if need be)
            if (item != null && item.place == ItemPlace.Storage)
            {
                InspectController.I.Begin(item);
                while (InspectController.I.Held != item || InspectController.I.Busy) yield return null;
                foreach (var p in item.parts.Where(p => p.def.kind == "hinge" || p.def.kind == "slide")) p.Toggle();
                yield return new WaitForSeconds(0.6f);
                foreach (var det in def.CaseDetails) d.Discover(def, det);
                Shot($"case{c.id}_inspect");
                bool tray = dec.verdict != Verdict.Refuse;
                InspectController.I.Release(tray ? ItemPlace.Tray : ItemPlace.Storage);
                while (InspectController.I.Held != null) yield return null;
            }
            // read the documents they brought
            foreach (var pid in c.presents)
                if (Desk.I.items.TryGetValue(pid, out var doc))
                    foreach (var det in doc.def.CaseDetails) d.Discover(doc.def, det);
            yield return new WaitForSeconds(0.4f);

            int who = dec.verdict == Verdict.Return ? System.Array.IndexOf(c.claimants, dec.to) : 0;
            var best = Rules.Best(c);
            string bestText = best == null ? "?" : best.verdict + (string.IsNullOrEmpty(best.to) ? "" : "->" + best.to);
            if (!d.CanStamp(dec.verdict, who, out string why)) { problems.Add($"case {c.id}: can't stamp {dec}: {why}"); dec.verdict = Verdict.Refuse; }
            ClaimSlip.I.AddImprint(dec.verdict, ClaimSlip.I.transform.position + new Vector3(who == 1 ? 0.04f : -0.02f, 0.001f, -0.08f), Random.Range(-12f, 12f));
            Debug.Log($"[Auto] day {d.Day} case {c.id}: {dec} (authored best {bestText})");
            d.CommitStamp(dec.verdict, who);
            yield return new WaitForSeconds(1.2f);
            Shot($"case{c.id}_verdict");
        }

        void OnDestroy() => Application.logMessageReceived -= OnLog;
    }
}
