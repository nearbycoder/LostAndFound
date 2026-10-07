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
        /// branches) | wait (best, but Thomas is refused the ring: the Long Wait) | refuse (refuse everything:
        /// the fullest the shelves get, and Gus's trips to the basement).</summary>
        string policy = "best";
        int startDay = 1;
        int shotRulesDay;
        bool basementShot, noteShot;
        /// <summary>-lafNudgeTour: play each claim as a stuck player would, asking Agnes for every nudge at each stage and
        /// doing what the last one says (find it where it glows, click where it glints, ask what it says to ask).</summary>
        bool tour;
        /// <summary>-lafTranscript: ask about every finding, then read the slip and check "What they said" beside it holds
        /// every line of the claim, on screen and clear of the slip.</summary>
        bool transcript;
        /// <summary>-lafTextAudit: check every text on screen against its box at each screenshot (TextAudit).</summary>
        bool textAudit;
        int tourNudges, tourGlints, tourParts;
        Quaternion[] tourTurns;

        void Start()
        {
            dir = Game.Arg("-lafAutopilot");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Application.persistentDataPath, "autopilot");
            Directory.CreateDirectory(dir);
            Application.runInBackground = true;
            Application.logMessageReceived += OnLog;
            Director.I.Autopilot = true;
            policy = Game.Arg("-lafPolicy") ?? "best";
            tour = Game.Arg("-lafNudgeTour") != null;
            transcript = Game.Arg("-lafTranscript") != null;
            textAudit = Game.Arg("-lafTextAudit") != null;
            if (tour)
            {
                var rng = new System.Random(9);
                tourTurns = new Quaternion[800];
                for (int i = 0; i < tourTurns.Length; i++)
                {
                    // uniform random rotations (Shoemake), seeded so the tour is repeatable
                    double u1 = rng.NextDouble(), u2 = rng.NextDouble() * System.Math.PI * 2, u3 = rng.NextDouble() * System.Math.PI * 2;
                    double a = System.Math.Sqrt(1 - u1), b = System.Math.Sqrt(u1);
                    tourTurns[i] = new Quaternion((float)(a * System.Math.Sin(u2)), (float)(a * System.Math.Cos(u2)), (float)(b * System.Math.Sin(u3)), (float)(b * System.Math.Cos(u3)));
                }
            }
            Time.timeScale = float.TryParse(Game.Arg("-lafSpeed") ?? "", out float sp) ? sp : 2f;
            // -lafContinue: pick up the saved week, as the title's Continue does (after a -lafQuitAfter run)
            if (Game.Arg("-lafContinue") != null) { startDay = Mathf.Max(1, Game.I.Save.currentDay); Game.I.ContinueWeek(); }
            else if (int.TryParse(Game.Arg("-lafDay") ?? "", out int day) && day > 1) { startDay = day; Game.I.StartFromDay(day); }
            else Game.I.NewWeek();
            StartCoroutine(Run());
        }

        void OnLog(string msg, string stack, LogType type)
        {
            if (msg.Contains("down to the basement")) basementShot = true;
            if (type == LogType.Exception || type == LogType.Error) problems.Add($"{type}: {msg.Split('\n')[0]}");
        }

        void Shot(string name)
        {
            if (textAudit) TextAudit.Check(name);
            string path = Path.Combine(dir, $"{shots++:000}_{name}.png");
            // in a browser the file would only land in its virtual disk: Tools/webgl_check.py takes the picture on this line
            if (Application.platform == RuntimePlatform.WebGLPlayer) Debug.Log($"[Shot] {Path.GetFileNameWithoutExtension(path)}");
            else ScreenCapture.CaptureScreenshot(path);
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
                if (!noteShot && UIRoot.I.note.Open)
                {
                    // one of Agnes's notes, once a run
                    noteShot = true;
                    yield return new WaitForSecondsRealtime(0.12f);   // notes don't stay up long at -lafSpeed 4
                    Shot($"day{d.Day}_note");
                }
                if (basementShot)
                {
                    // Gus is about to say where yesterday's strays went: catch him saying it
                    basementShot = false;
                    float until = Time.realtimeSinceStartup + 15f;
                    while (!UIRoot.I.dialogue.Text.Contains("basement") && Time.realtimeSinceStartup < until) yield return null;
                    yield return new WaitForSecondsRealtime(0.5f);
                    Shot($"day{d.Day}_basement");
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
            if (transcript) Debug.Log($"[Transcript] checked {transcriptChecks} claims");
            if (textAudit) TextAudit.Summary();
            if (tour) Debug.Log($"[Tour] {tourNudges} nudges asked for; {tourGlints} details found by clicking the glint, {tourParts} by working the part that lit up");
            var all = d.State.records.Where(r => r.grade != "skip").ToList();
            Debug.Log($"[Auto] the week's record (this run and any before it): {all.Count(r => r.grade == "best")} best of {all.Count} decided");
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
                // and the intake tag of the object they're after, as hovering it shows
                if (Desk.I.items.TryGetValue(c.wants ?? "", out var tagged) && tagged != null && tagged.place == ItemPlace.Storage)
                {
                    UIRoot.I.tagCard.Show(tagged);
                    yield return new WaitForSecondsRealtime(0.5f);
                    Shot($"case{c.id}_tag");
                    yield return null;
                    UIRoot.I.tagCard.Hide(tagged);
                }
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
            if (Game.Arg("-lafLetteringSwitch") != null && cases == 1) yield return SwitchLettering(c);
            var dec = Rules.Solve(d.Db, d.Day, c, d.State);
            if (policy == "refuse")
                dec = new Decision { verdict = Verdict.Refuse, reason = "policy: refuse everything" };
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

            if (tour)
            {
                yield return TourCase(d, c, item, def);
                bool toTray = dec.verdict != Verdict.Refuse;
                if (item != null && InspectController.I.Held == null && toTray && Desk.I.OnTray != item)
                {
                    InspectController.I.Begin(item);
                    while (InspectController.I.Held != item || InspectController.I.Busy) yield return null;
                }
                if (InspectController.I.Held != null)
                {
                    InspectController.I.Release(toTray ? ItemPlace.Tray : ItemPlace.Storage);
                    while (InspectController.I.Held != null) yield return null;
                }
            }
            // look the object over: open it, find every case detail (under the lamp if need be)
            else if (item != null && item.place == ItemPlace.Storage)
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
            if (transcript) yield return CheckTranscript(d, c);

            int who = dec.verdict == Verdict.Return ? System.Array.IndexOf(c.claimants, dec.to) : 0;
            var best = Rules.Best(c);
            string bestText = best == null ? "?" : best.verdict + (string.IsNullOrEmpty(best.to) ? "" : "->" + best.to);
            if (!d.CanStamp(dec.verdict, who, out string why)) { problems.Add($"case {c.id}: can't stamp {dec}: {why}"); dec.verdict = Verdict.Refuse; }
            ClaimSlip.I.AddImprint(dec.verdict, ClaimSlip.I.transform.position + new Vector3(who == 1 ? 0.04f : -0.02f, 0.001f, -0.08f), Random.Range(-12f, 12f));
            Debug.Log($"[Auto] day {d.Day} case {c.id}: {dec} (authored best {bestText})");
            yield return new WaitForSeconds(0.4f * (slow - 1f));
            d.CommitStamp(dec.verdict, who);
            if (Game.Arg("-lafQuitAfter") == c.id)
            {
                // quit the moment the verdict is saved (the claimant still walking away), as a player might
                while (d.State.Record(c.id) == null) yield return null;
                yield return null;
                Debug.Log($"[Auto] quitting after case {c.id} (saved: day {d.Save.currentDay}, {d.Save.casesDone} case(s) done)");
                Application.Quit();
                yield break;
            }
            yield return new WaitForSeconds(1.2f);
            Shot($"case{c.id}_verdict");
        }

        // ------------------------------------------------------------------ plain lettering

        /// <summary>-lafLetteringSwitch: turn Settings > Plain lettering on in the first claim, as the Settings toggle does, and
        /// check the handwriting already on screen changes with it (the setting is saved in this run's scratch prefs).</summary>
        IEnumerator SwitchLettering(CaseDef c)
        {
            string Faces() => $"slip {ClaimSlip.I.Body.font.name}, rules card {UIRoot.I.rulesPeek.GetComponentInChildren<TMPro.TMP_Text>().font.name}";
            string before = Faces();
            UIRoot.I.rulesPeek.Show();
            yield return new WaitForSecondsRealtime(0.4f);
            Shot($"case{c.id}_lettering_before");
            yield return null;
            Settings.PlainLettering = !Settings.PlainLettering;
            yield return new WaitForSecondsRealtime(0.4f);
            string after = Faces();
            Shot($"case{c.id}_lettering_after");
            yield return null;
            UIRoot.I.rulesPeek.Hide();
            Debug.Log($"[Lettering] switched live to {(Fonts.Plain ? "plain" : "handwriting")}: before {before}; after {after}");
            if (before == after) problems.Add($"case {c.id}: the lettering didn't change on screen ({after})");
        }

        // ------------------------------------------------------------------ what they said

        int transcriptChecks;

        IEnumerator CheckTranscript(Director d, CaseDef c)
        {
            foreach (var id in ClaimSlip.I.ClueIds())
            {
                d.Ask(id);
                yield return null;
                while (d.Asking) yield return null;
            }
            // read the slip (as hovering it or Tab does), and give the camera time to lean in
            var slip = ClaimSlip.I;
            var card = UIRoot.I.transcript;
            int refocused = 0;
            ClaimSlip.IgnorePointerExit = true;
            for (float t = 0f; t < 0.9f; t += Time.unscaledDeltaTime)
            {
                if (!slip.Focused) { slip.SetFocus(true); refocused++; }
                yield return null;
            }
            int said = DialogueBox.SaidCount - d.TranscriptSaidAt;
            Rect r = card.ScreenRect, sr = card.SlipScreenRect;
            bool onScreen = r.xMin >= -0.5f && r.yMin >= -0.5f && r.xMax <= Screen.width + 0.5f && r.yMax <= Screen.height + 0.5f;
            bool clear = !r.Overlaps(sr);
            static string R(Rect x) => $"{x.xMin:0}-{x.xMax:0} x {x.yMin:0}-{x.yMax:0}";
            Debug.Log($"[Transcript] case {c.id}: {d.Transcript.Count} lines, {said} said; {(card.Visible ? "shown" : "NOT shown")}, " +
                      $"{card.Skipped} left out at the top; card {R(r)}, slip {R(sr)} on {Screen.width}x{Screen.height}" + (refocused > 1 ? $" (slip refocused {refocused}x)" : ""));
            if (d.Transcript.Count != said) problems.Add($"case {c.id}: transcript has {d.Transcript.Count} lines but {said} were said");
            if (!card.Visible) problems.Add($"case {c.id}: transcript card not shown (fits: {card.Fits})");
            else if (!onScreen || !clear) problems.Add($"case {c.id}: transcript card {R(r)} {(onScreen ? "" : "off screen ")}{(clear ? "" : "over the slip " + R(sr))}");
            transcriptChecks++;
            Shot($"case{c.id}_transcript");
            yield return null;   // the capture happens at the end of the frame
            slip.SetFocus(false);
            ClaimSlip.IgnorePointerExit = false;
            yield return new WaitForSecondsRealtime(0.3f);
        }

        // ------------------------------------------------------------------ the nudge tour

        static readonly HashSet<string> TourShots = new() { "1.1", "1.3", "1.4", "2.2", "4.2", "5.2", "5.3" };

        IEnumerator TourCase(Director d, CaseDef c, ItemView item, ObjectDef def)
        {
            var insp = InspectController.I;
            bool shots = TourShots.Contains(c.id);
            if (c.id == "1.2")
            {
                // a player who sits there: the hint bar offers a nudge after a while
                Director.OfferInTests = true;
                float until = Time.time + Director.OfferAfter + 10f;
                while (!d.OfferingNudge && Time.time < until) yield return null;
                Director.OfferInTests = false;
                if (!d.OfferingNudge) problems.Add($"case {c.id}: no nudge offered after {Director.OfferAfter:0}s");
                else Debug.Log($"[Tour] case {c.id}: offered a nudge after {Director.OfferAfter:0}s without progress");
                yield return new WaitForSecondsRealtime(0.4f);
                Shot($"case{c.id}_nudge_offer");
            }
            var seen = new List<NudgeStage>();
            for (int guard = 0; guard < 16; guard++)
            {
                var list = d.CurrentNudges();
                if (list == null || list.Count == 0) { problems.Add($"case {c.id}: no nudges"); yield break; }
                var stage = list[0].stage;
                seen.Add(stage);
                Nudge n = null;
                for (int i = 0; i < list.Count; i++)
                {
                    n = d.GiveNudge();
                    tourNudges++;
                    if (n == null || n.text != list[i].text) { problems.Add($"case {c.id}: nudge {i + 1} of {stage} came out as {n?.text}"); yield break; }
                    yield return null;
                    if (shots && stage == NudgeStage.Decide && i == Mathf.Max(0, list.Count - 2))
                    {
                        // the specific one: what to compare
                        yield return new WaitForSecondsRealtime(0.7f);
                        Shot($"case{c.id}_nudge_decide");
                        yield return null;   // the capture happens at the end of the frame: before the next nudge
                    }
                }
                yield return new WaitForSecondsRealtime(0.5f);
                if (item == null || stage == NudgeStage.Decide) break;

                if (stage == NudgeStage.Find)
                {
                    string want = item.drawer != null && !item.drawer.IsOpen ? "drawer" : "item";
                    if (d.ShowingWhere != want) problems.Add($"case {c.id}: the find nudge lit up {d.ShowingWhere ?? "nothing"}, not the {want}");
                    else Debug.Log($"[Tour] case {c.id}: find → the {want} glows");
                    if (shots && c.id is "1.1" or "1.3" or "1.4")
                    {
                        CameraRig.I.SetView(item.drawer != null ? View.Cabinet : item.def.storage == "desk" ? View.Counter : View.Shelf);
                        yield return new WaitForSecondsRealtime(1.2f);
                        Shot($"case{c.id}_nudge_find");
                        yield return null;
                    }
                    insp.Begin(item);
                    while (insp.Held != item || insp.Busy) yield return null;
                    continue;
                }

                if (stage == NudgeStage.Ask)
                {
                    if (shots) { Shot($"case{c.id}_nudge_ask"); yield return null; }
                    d.Ask(n.detailId);
                    yield return null;
                    while (!d.CanUseStamps) yield return null;
                    Debug.Log($"[Tour] case {c.id}: asked about {n.detailId}");
                    continue;
                }

                // Examine: do what the nudges said, then find it where the last one points
                var det = def.Detail(n.detailId);
                if (insp.Held != item)
                {
                    insp.Begin(item);
                    while (insp.Held != item || insp.Busy) yield return null;
                }
                yield return FindByNudge(d, c, item, det, shots);
                if (!d.IsDiscovered(def, det)) yield break;
            }
            Debug.Log($"[Tour] case {c.id}: {string.Join(" → ", seen)}");
            if (c.id == "4.5" && item != null)
            {
                // the hardest detail in the audit (the ring ticket's date, under the lamp) isn't needed for a hum, so
                // point at it the same way and check the glint finds it
                if (insp.Held != item) { insp.Begin(item); while (insp.Held != item || insp.Busy) yield return null; }
                var date = def.Detail("date");
                if (date != null && !d.IsDiscovered(def, date)) yield return FindByNudge(d, c, item, date, true, true);
            }
        }

        /// <summary>Open it and switch on the lamp if the nudge said so, then either work the part that lights up, or turn
        /// the object until the glint appears and click on the glint.</summary>
        IEnumerator FindByNudge(Director d, CaseDef c, ItemView item, DetailDef det, bool shot, bool direct = false)
        {
            var insp = InspectController.I;
            var part = item.parts.FirstOrDefault(p => p.def.reveals == det.id);
            if (det.requires == "uv" && !Lamp.I.UV) Lamp.I.ToggleUV();
            if (part == null)
                foreach (var p in item.parts.Where(p => p.def.kind is "hinge" or "slide" && !p.open)) insp.OperateForDemo(p);
            yield return new WaitForSecondsRealtime(0.8f);

            if (part != null)
            {
                if (d.ShowingWhere != "part") problems.Add($"case {c.id}: the nudge for {det.id} lit up {d.ShowingWhere ?? "nothing"}, not the part");
                if (shot) { Shot($"case{c.id}_nudge_part_{det.id}"); yield return null; }
                insp.OperateForDemo(part);
                float until = Time.realtimeSinceStartup + 6f;
                while (!d.IsDiscovered(item.def, det) && Time.realtimeSinceStartup < until) yield return null;
                if (d.IsDiscovered(item.def, det)) { tourParts++; Debug.Log($"[Tour] case {c.id}: {det.id}: the part lit up → worked it → discovered"); }
                else problems.Add($"case {c.id}: working the lit part didn't reveal {det.id}");
                yield break;
            }

            if (direct) insp.Pointing = det;
            else if (d.ShowingWhere != "glint") problems.Add($"case {c.id}: the nudge for {det.id} is showing {d.ShowingWhere ?? "nothing"}, not a glint");
            // turn it, as a player would, until the glint shows
            bool posed = false;
            foreach (float z in new[] { 1f, 1.3f })
            {
                foreach (var q in tourTurns)
                {
                    insp.PoseInHand(item, q, z);
                    Physics.SyncTransforms();
                    if (insp.GlintAt(item, det, out _)) { insp.HoldAt(q, z); posed = true; break; }
                }
                if (posed) break;
            }
            if (!posed) { problems.Add($"case {c.id}: no turn of {item.def.id} shows a glint on {det.id}"); yield break; }
            float wait = Time.realtimeSinceStartup + 3f;
            yield return new WaitForSecondsRealtime(0.6f);
            while (!UIRoot.I.nudgeGlint.Showing && Time.realtimeSinceStartup < wait) yield return null;
            if (!UIRoot.I.nudgeGlint.Showing) { problems.Add($"case {c.id}: the glint for {det.id} never showed"); yield break; }
            // held still, does it stay lit? (it should, unless the spot sits right at the edge of view)
            int lit = 0;
            for (int f = 0; f < 30; f++) { if (UIRoot.I.nudgeGlint.Showing) lit++; yield return null; }
            Debug.Log($"[Tour] case {c.id}: {det.id}: glint lit {lit} of 30 frames held still");
            if (shot) Shot($"case{c.id}_nudge_glint_{det.id}");
            yield return null;
            // click where the glint is, in a frame it's showing (it goes out whenever a click there wouldn't land)
            wait = Time.realtimeSinceStartup + 3f;
            while (!UIRoot.I.nudgeGlint.Showing && Time.realtimeSinceStartup < wait) yield return null;
            var got = UIRoot.I.nudgeGlint.Showing ? insp.ClickForDemo(UIRoot.I.nudgeGlint.ScreenPos) : null;
            if (direct) insp.Pointing = null;
            if (got == det) { tourGlints++; Debug.Log($"[Tour] case {c.id}: {det.id}: glint → clicked → discovered"); }
            else problems.Add($"case {c.id}: clicking the glint for {det.id} found {got?.id ?? "nothing"}");
            yield return new WaitForSecondsRealtime(0.3f);
        }

        void OnDestroy() => Application.logMessageReceived -= OnLog;
    }
}
