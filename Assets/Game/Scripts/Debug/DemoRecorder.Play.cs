using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Debug = UnityEngine.Debug;

namespace LostAndFound
{
    /// <summary>
    /// -lafPlay: film whole days played like a player would, through the simulated mouse and keyboard:
    /// ring the bell, turn to the drawers or the shelf, open the drawer, read the tag, pick the object up,
    /// turn it over, work its lid or key, find each hidden detail (under the blue lamp when it needs it),
    /// ask about what gives a liar away, put it on the tray and stamp the slip with the solver's verdict.
    /// Starts at the title (or at -lafDay N) and stops on the morning after -lafUntil (default: the
    /// starting day), or after the ending. -lafVerdicts "4.2=return:vell,..." overrides verdicts.
    /// Writes &lt;dir&gt;/markers.tsv (frame, seconds, event) so trailer cuts can find each moment.
    /// </summary>
    public partial class DemoRecorder
    {
        bool playDays;
        StreamWriter markers;
        readonly Dictionary<string, string> overrides = new();
        /// <summary>Cases where we also hunt down the object's optional secret (a curio).</summary>
        static readonly HashSet<string> SecretCases = new() { "1.1", "1.4", "2.2", "3.5", "4.3", "5.2" };

        void StartPlay()
        {
            markers = new StreamWriter(Path.Combine(dir, "markers.tsv")) { AutoFlush = true };
            foreach (var kv in (Game.Arg("-lafVerdicts") ?? "").Split(',').Where(x => x.Contains('=')))
                overrides[kv.Split('=')[0].Trim()] = kv.Split('=')[1].Trim();
            Director.I.autoAdvance = true;   // dialogue, notes and the ledger move on at a reading pace
            showcase = Game.Arg("-lafShowcase") != null && realInput == null;   // the trailer's takes (DemoRecorder.Showcase.cs)
            StartCoroutine(Watchers());
            StartCoroutine(PlayDays());
        }

        void Mark(string ev)
        {
            markers?.WriteLine($"{captured}\t{captured / (float)Fps:0.00}\t{ev}");
            Debug.Log($"[Demo] mark {captured} {ev}");
        }

        /// <summary>A screen-space panel by path under the UI root (the desk has a prop called Ledger too).</summary>
        static bool UiPanel(string path) => UIRoot.I != null && UIRoot.I.root != null && UIRoot.I.root.Find(path) != null;

        /// <summary>Moments the play script doesn't cause itself: day cards, notes, the ledger, the photographs.</summary>
        IEnumerator Watchers()
        {
            int day = -1;
            bool ledger = false, photos = false, note = false, ending = false, polaroid = false;
            while (true)
            {
                var d = Director.I;
                if (d != null)
                {
                    if (d.Day != day && d.Running) { day = d.Day; Mark($"day {day} {d.DayDef.weekday} {d.DayDef.title}"); }
                    bool l = UiPanel("Ledger");
                    if (l != ledger) { ledger = l; Mark(l ? $"ledger {d.Day}" : "ledger-closed"); }
                    if (d.ChangingPhotos != photos) { photos = d.ChangingPhotos; Mark(photos ? "photos-begin" : "photos-end"); }
                    bool n = UIRoot.I != null && UIRoot.I.note.Open;
                    if (n != note) { note = n; Mark(n ? "note" : "note-closed"); }
                    bool e = UiPanel("Ending");
                    if (e && !ending) { ending = true; Mark("ending"); }
                    bool p = Desk.I != null && Desk.I.props.polaroid != null && Desk.I.props.polaroid.gameObject.activeInHierarchy;
                    if (p != polaroid) { polaroid = p; if (p) Mark("polaroid"); }
                }
                yield return null;
            }
        }

        IEnumerator PlayDays()
        {
            yield return Frame();
            var d = Director.I;
            int until = int.TryParse(Game.Arg("-lafUntil") ?? "", out int u) ? u : -1;

            // the title: let it breathe, wander over the menu, then Begin
            yield return Until(() => TitleScreen.Showing || d.Running, 10f);
            if (TitleScreen.Showing)
            {
                Mark("title");
                pos = new Vector2(Screen.width * 0.62f, Screen.height * 0.3f);
                yield return Hold(4.5f);
                var begin = TitleScreen.FirstButton;
                if (showcase) yield return TitleShowcase();
                else if (begin != null)
                {
                    yield return Glide(() => RectTransformUtility.WorldToScreenPoint(null, begin.TransformPoint(begin.rect.center + new Vector2(-begin.rect.width * 0.4f, 0f))), 1.2f);
                    yield return Hold(0.8f);
                    Mark("begin");
                    yield return Click();
                }
                else { Fallback("no Begin on the title; beginning directly"); TitleScreen.Begin(Game.I); }
                yield return Until(() => !TitleScreen.Showing, 5f);
                if (TitleScreen.Showing) { Fallback("the click on Begin missed; beginning directly"); TitleScreen.Begin(Game.I); }
            }
            yield return Until(() => d.Running, 20f);
            if (until < 0) until = d.Day;

            while (true)
            {
                if (d.Save.finished)
                {
                    yield return Until(() => UiPanel("Ending"), 60f);
                    yield return Until(() => UiPanel("Ending/Done"), 40f);
                    yield return Hold(2.5f);
                    if (showcase) yield return WeekShowcase();
                    Mark("end");
                    yield return UIRoot.I.fader.FadeTo(1f, 1.2f);
                    yield return Finish();
                    yield break;
                }
                if (d.CanRing && d.Upcoming != null)
                {
                    if (d.Day > until)
                    {
                        // the next morning has played (day card, Gus, Agnes's rule): stop here
                        Mark("stop");
                        yield return Hold(1.0f);
                        yield return UIRoot.I.fader.FadeTo(1f, 1.0f);
                        yield return Finish();
                        yield break;
                    }
                    if (showcase) yield return MorningShowcase(d);
                    yield return PlayCase(d, d.Upcoming);
                    continue;
                }
                yield return Frame();
            }
        }

        IEnumerator PlayCase(Director d, CaseDef c)
        {
            var desk = Desk.I;
            yield return Hold(0.6f);
            Mark($"bell {c.id}");
            yield return GlideTo(desk.props.bell, 0.9f);
            yield return Hold(0.25f);
            yield return Click();
            yield return Hold(0.3f);
            if (d.CanRing) { Fallback($"the bell click for {c.id} missed; ringing directly"); desk.props.bell.Ring(); }
            yield return Until(() => d.Current == c, 20f);
            Mark($"arrive {c.id} {string.Join("+", c.claimants)}");
            yield return Until(() => d.CanUseStamps || d.Current != c, 120f);
            if (d.Current != c) { Mark($"vignette {c.id}"); yield return Until(() => d.CanRing || d.InEvening, 60f); yield break; }
            Mark($"investigate {c.id}");
            if (realInput != null && !keysChecked) yield return KeysCheck(d);
            if (showcase && c.id == "1.2") yield return NudgeUntil(d, "drawer");
            if (showcase && c.id == "4.2") yield return RulesShowcase();

            // the decision this case gets (the solver's, unless told otherwise)
            var dec = Rules.Solve(d.Db, d.Day, c, d.State);
            if (overrides.TryGetValue(c.id, out var ov))
            {
                var parts = ov.Split(':');
                dec = new Decision { verdict = VerdictNames.Parse(parts[0]), to = parts.Length > 1 ? parts[1] : c.claimants[0], reason = "override" };
            }

            // read the slip
            yield return GlideTo(ClaimSlip.I.transform.position + new Vector3(0f, 0f, 0.03f), 0.8f);
            yield return Until(() => ClaimSlip.I.Focused, 2f);
            Mark($"slip {c.id}");
            yield return Hold(2.0f);
            yield return Glide(() => new Vector2(Screen.width * 0.5f, Screen.height * 0.97f), 0.5f);
            yield return Hold(0.5f);

            // documents they slid under the glass
            foreach (var pid in c.presents)
                if (desk.items.TryGetValue(pid, out var doc) && doc != null)
                {
                    yield return Inspect(c, doc, false);
                    if (InspectController.I.Held == doc) yield return PutDown();
                }

            // the object itself
            ItemView item = null;
            if (!string.IsNullOrEmpty(c.wants)) desk.items.TryGetValue(c.wants, out item);
            if (item != null && item.place == ItemPlace.Storage)
            {
                yield return Fetch(c, item);
                if (InspectController.I.Held == item)
                {
                    yield return Inspect(c, item, SecretCases.Contains(c.id));
                    if (dec.verdict != Verdict.Refuse)
                    {
                        yield return Press(Key.T);
                        yield return Until(() => desk.OnTray == item && InspectController.I.Held == null, 5f);
                        if (desk.OnTray != item) Fallback($"T didn't put the {item.def.id} on the tray");
                        Mark($"tray {item.def.id}");
                    }
                    else yield return PutDown();
                    yield return Hold(0.5f);
                }
            }

            // ask about whatever a liar can't know (or, in a two-claimant case, what settles it)
            var ask = c.answers.FirstOrDefault(a => !a.truthful)?.detail;
            if (ask == null && c.id == "1.1") ask = "photo";
            if (ask != null && ClaimSlip.I.HasClue(ask)) yield return Ask(d, ask);

            yield return StampIt(d, c, dec);
            yield return Until(() => d.CanRing || d.InEvening || d.Save.finished, 90f);
            Mark($"done {c.id}");
        }

        /// <summary>Turn to wherever it's kept, open its drawer, read the tag and pick it up.</summary>
        IEnumerator Fetch(CaseDef c, ItemView item)
        {
            var rig = CameraRig.I;
            View want = item.drawer != null ? View.Cabinet : item.shelfAnchor != null ? View.Shelf : View.Counter;   // shelves and the umbrella stand
            if (want != rig.view)
            {
                yield return Glide(() => new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), 0.4f);
                yield return Press(want == View.Cabinet ? Key.A : Key.D);
                Mark($"turn {want}");
                yield return Hold(1.0f);
            }
            if (item.drawer != null && !item.drawer.IsOpen)
            {
                yield return GlideTo(item.drawer, 0.8f);
                yield return Hold(0.25f);
                yield return Click();
                Mark($"drawer {item.drawer.name}");
                yield return Hold(1.1f);
            }
            yield return GlideTo(item, 0.8f);
            Mark($"tag {item.def.id}");
            yield return Hold(1.8f);
            yield return Click();
            yield return Until(() => InspectController.I.Held == item && !InspectController.I.Busy, 4f);
            if (InspectController.I.Held != item)
            {
                Fallback($"pick-up of {item.def.id} missed; picking up directly");
                InspectController.I.Begin(item);
                yield return Until(() => InspectController.I.Held == item && !InspectController.I.Busy, 4f);
            }
            Mark($"pickup {item.def.id}");
            yield return Hold(0.5f);
        }

        /// <summary>Turn it over, work its parts, and find every case detail (and maybe its secret).</summary>
        IEnumerator Inspect(CaseDef c, ItemView item, bool secret)
        {
            if (InspectController.I.Held != item)
            {
                yield return GlideTo(item, 0.8f);
                yield return Hold(0.4f);
                yield return Click();
                yield return Until(() => InspectController.I.Held == item && !InspectController.I.Busy, 4f);
                if (InspectController.I.Held != item) { Fallback($"pick-up of {item.def.id} missed; picking up directly"); InspectController.I.Begin(item); yield return Until(() => InspectController.I.Held == item && !InspectController.I.Busy, 4f); }
                Mark($"pickup {item.def.id}");
            }
            if (realInput != null && !wheelChecked) yield return WheelAndRightClick(item);

            // a turn in the hands
            yield return Glide(() => new Vector2(Screen.width * 0.42f, Screen.height * 0.5f), 0.35f);
            yield return Drag(new Vector2(240f, 0f), 0.6f);
            yield return Drag(new Vector2(-240f, 0f), 0.6f);

            // lids, latches, keys; whole-object actions (shake it) by clicking the object itself
            foreach (var part in item.parts)
            {
                if (part.def.kind is "hinge" or "slide" && part.open) continue;
                if (string.IsNullOrEmpty(part.def.node) || part.transform == item.transform)
                {
                    yield return Glide(() => ToScreen(item.Center), 0.6f);
                    yield return Hold(0.3f);
                    yield return Click();
                }
                else yield return FindPart(item, part);
                Mark($"part {item.def.id}.{part.def.kind}");
                yield return Hold(1.3f);
            }

            if (showcase && c.id == "1.2") yield return NudgeUntil(Director.I, "glint");
            var dets = item.def.CaseDetails.ToList();
            if (secret) dets.AddRange(item.def.details.Where(x => x.kind == "secret"));
            // plain details first, then the ones that need Agnes's lamp
            foreach (var det in dets.OrderBy(x => x.requires == "uv" ? 1 : 0))
            {
                if (Director.I.IsDiscovered(item.def, det)) continue;
                if (det.requires is "wind" or "shake" or "listen" or "play") continue;  // revealed by the parts
                if (det.requires == "uv" && !Lamp.I.UV)
                {
                    if (!Lamp.I.uvUnlocked) continue;
                    yield return Press(Key.L);
                    Mark($"lamp-on {item.def.id}");
                    yield return Hold(1.0f);
                }
                var hs = item.Hotspot(det);
                if (hs == null) continue;
                yield return FindDetail(item, det, hs);
                if (!Director.I.IsDiscovered(item.def, det))
                {
                    InspectController.I.DiscoverForDemo(det, hs.position);
                    yield return Hold(1.6f);
                }
                Mark($"discover {item.def.id}.{det.id}{(det.kind == "secret" ? " secret" : "")}{(det.requires == "uv" ? " uv" : "")}");
            }
            if (Lamp.I.UV)
            {
                yield return Hold(0.6f);
                yield return Press(Key.L);
                Mark("lamp-off");
            }
            yield return Hold(0.6f);
        }

        IEnumerator PutDown()
        {
            var held = InspectController.I.Held;
            yield return Press(Key.Backspace);
            yield return Until(() => InspectController.I.Held == null, 4f);
            if (InspectController.I.Held != null) Fallback("Backspace didn't put the object down");
            if (held != null) Mark($"putdown {held.def.id}");
            yield return Hold(0.3f);
        }

        IEnumerator Ask(Director d, string detailId)
        {
            yield return GlideTo(ClaimSlip.I.transform.position + new Vector3(0f, 0f, 0.03f), 0.8f);
            yield return Until(() => ClaimSlip.I.Focused, 3f);
            yield return Hold(0.8f);
            if (!ClaimSlip.I.LinkPosition(detailId, out _)) yield break;
            yield return Glide(() => ClaimSlip.I.LinkPosition(detailId, out var w) ? ToScreen(w) : pos, 0.7f);
            yield return Hold(0.4f);
            yield return Click();
            Mark($"ask {detailId}");
            yield return Hold(0.5f);
            yield return Until(() => d.CanUseStamps, 40f);
            Mark("answered");
            yield return Hold(0.6f);
        }

        IEnumerator StampIt(Director d, CaseDef c, Decision dec)
        {
            int who = dec.verdict == Verdict.Return ? Mathf.Max(0, System.Array.IndexOf(c.claimants, dec.to)) : 0;
            var stamp = Desk.I.props.stamps.First(s => s.kind == dec.verdict);
            for (int tries = 0; tries < 3 && StampTool.I.Carrying != stamp; tries++)
            {
                if (StampTool.I.Carrying != null)
                {
                    Fallback($"picked up the {StampTool.I.Carrying.kind} stamp, not {dec.verdict}; putting it back");
                    yield return Press(Key.Escape);
                    yield return Until(() => StampTool.I.Carrying == null, 2f);
                    yield return Hold(0.3f);
                }
                yield return GlideTo(stamp, tries == 0 ? 0.8f : 0.4f);
                yield return Hold(0.3f);
                // the camera breathes: make sure it's still this stamp under the pointer before clicking
                for (int i = 0; i < 20 && InteractionSystem.I.Hovered != stamp; i++) { pos = AimPoint(stamp); yield return Frame(); }
                yield return Click();
                yield return Until(() => StampTool.I.Carrying != null, 2f);
            }
            Mark($"stamp-pick {dec.verdict}");
            yield return Hold(0.7f);
            var slip = ClaimSlip.I.transform;
            float x = c.claimants.Length > 1 ? (who == 0 ? -0.045f : 0.045f) : 0.02f;
            yield return Glide(() => ToScreen(slip.position + slip.rotation * new Vector3(x, 0f, 0.05f)), 0.7f);
            yield return Hold(showcase ? 1.3f : 0.4f);   // the hint says what the stamp will do
            yield return Click();
            yield return Until(() => !d.CanUseStamps || d.Current != c, 2.5f);
            if (d.CanUseStamps && d.Current == c)
            {
                Fallback($"stamping {c.id} missed; committing directly");
                if (StampTool.I.Carrying != null) { yield return Press(Key.Escape); yield return Hold(0.6f); }
                ClaimSlip.I.AddImprint(dec.verdict, slip.position + slip.rotation * new Vector3(x, 0.001f, 0.05f), Random.Range(-12f, 12f));
                d.CommitStamp(dec.verdict, who);
            }
            Mark($"stamp {c.id} {dec.verdict}{(dec.verdict == Verdict.Return ? " " + dec.to : "")}");
            yield return Glide(() => new Vector2(Screen.width * 0.62f, Screen.height * 0.3f), 0.8f);
        }
    }
}
