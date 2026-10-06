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
        /// <summary>-lafPolicy best (default) | worst (a wrong verdict wherever there is one, to walk the other
        /// branches) | wait (best, but Thomas is refused the ring: the Long Wait).</summary>
        string policy = "best";
        int startDay = 1;
        int shotRulesDay;

        void Start()
        {
            dir = Game.Arg("-lafAutopilot");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Application.persistentDataPath, "autopilot");
            Directory.CreateDirectory(dir);
            Application.runInBackground = true;
            Application.logMessageReceived += OnLog;
            Director.I.Autopilot = true;
            policy = Game.Arg("-lafPolicy") ?? "best";
            Time.timeScale = float.TryParse(Game.Arg("-lafSpeed") ?? "", out float sp) ? sp : 2f;
            if (int.TryParse(Game.Arg("-lafDay") ?? "", out int day) && day > 1) { startDay = day; Game.I.StartFromDay(day); }
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
                    // the ending view fades in, types its lines, then hands back to the title (which
                    // rebuilds the game, this component included): shoot once it has settled, then stop
                    while (GameObject.Find("Ending") == null) yield return null;
                    yield return new WaitForSecondsRealtime(3.5f);
                    Shot("ending");
                    yield return new WaitForSecondsRealtime(0.5f);
                    break;
                }
                if (UIRoot.ModalOpen && d.DayDef != null && d.Day != lastLedgerDay && GameObject.Find("Ledger") != null)
                {
                    lastLedgerDay = d.Day;
                    yield return new WaitForSecondsRealtime(2.2f);
                    Shot($"day{d.Day}_ledger");
                }
                if (d.ChangingPhotos)
                {
                    Shot($"day{d.Day}_photos");
                    yield return new WaitForSecondsRealtime(0.9f);
                    continue;
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
            var played = d.State.records.Where(r => handled.Contains(r.caseId) || r.grade == "skip").ToList();
            int best = played.Count(r => r.grade == "best" && handled.Contains(r.caseId));
            int skipped = played.Count(r => r.grade == "skip" && !handled.Contains(r.caseId) && d.Db.root.days.Any(day => day.day >= startDay && day.cases.Any(c => c.id == r.caseId)));
            cases += skipped;
            Debug.Log($"[Auto] week done: {cases} cases, {best} best, {skipped} skipped, ending {d.Save.ending}, {problems.Count} problems, {Time.realtimeSinceStartup - t0:0}s");
            foreach (var p in problems) Debug.Log("[Auto] problem: " + p);
            bool pass = problems.Count == 0 && (policy != "best" || best + skipped == cases);
            Debug.Log(pass ? "[Auto] PASS" : "[Auto] FAIL");
            Application.Quit();
        }

        IEnumerator PlayCase(Director d, CaseDef c)
        {
            cases++;
            float slow = Director.Cinematic ? 3f : 1f;   // filmed runs take their time
            yield return new WaitForSeconds(0.3f * slow);
            yield return new WaitForSecondsRealtime(0.4f);   // let hints and UI finish fading in at any -lafSpeed
            Shot($"case{c.id}_window");
            if (!Director.Cinematic && shotRulesDay != d.Day)
            {
                // the rules known by the first case of each day: Agnes's card on the desk, held up on Friday
                shotRulesDay = d.Day;
                UIRoot.I.rulesPeek.Show();
                yield return new WaitForSecondsRealtime(0.5f);
                Shot($"day{d.Day}_rules_card");
                yield return null;   // the capture happens at the end of the frame: keep the card up until then
                UIRoot.I.rulesPeek.Hide();
                if (d.Day == d.Db.DayCount)
                {
                    RulesCard.Show();
                    yield return new WaitForSecondsRealtime(0.4f);
                    Shot($"day{d.Day}_rules_open");
                    yield return null;
                    RulesCard.Hide();
                }
                yield return new WaitForSecondsRealtime(0.3f);
            }
            var dec = Rules.Solve(d.Db, d.Day, c, d.State);
            if (policy == "wait" && c.id == "4.5")
                dec = new Decision { verdict = Verdict.Refuse, reason = "policy: the long wait" };
            if (policy == "worst")
            {
                // prefer a wrong return (it changes the most), then any other wrong verdict
                var wrong = c.verdicts.Where(v => v.grade == "wrong").OrderBy(v => v.verdict == "return" ? 0 : 1).FirstOrDefault();
                if (wrong != null && !(string.IsNullOrEmpty(c.wants) && wrong.verdict != "refuse"))
                    dec = new Decision { verdict = VerdictNames.Parse(wrong.verdict), to = string.IsNullOrEmpty(wrong.to) ? c.claimants[0] : wrong.to, reason = "policy: worst" };
            }
            var def = d.Db.Object(c.wants);
            Desk.I.items.TryGetValue(c.wants ?? "", out var item);

            // look the object over: open it, find every case detail (under the lamp if need be)
            if (item != null && item.place == ItemPlace.Storage)
            {
                InspectController.I.Begin(item);
                while (InspectController.I.Held != item || InspectController.I.Busy) yield return null;
                foreach (var p in item.parts.Where(p => p.def.kind == "hinge" || p.def.kind == "slide")) p.Toggle();
                yield return new WaitForSeconds(0.6f * slow);
                foreach (var det in def.CaseDetails)
                {
                    if (Director.Cinematic)
                    {
                        var hs = item.Hotspot(det);
                        if (!d.IsDiscovered(def, det)) InspectController.I.DiscoverForDemo(det, hs != null ? hs.position : item.Center);
                        yield return new WaitForSeconds(1.6f);
                    }
                    else d.Discover(def, det);
                }
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
            yield return new WaitForSeconds(0.4f * (slow - 1f));
            d.CommitStamp(dec.verdict, who);
            yield return new WaitForSeconds(1.2f);
            Shot($"case{c.id}_verdict");
        }

        void OnDestroy() => Application.logMessageReceived -= OnLog;
    }
}
