using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LostAndFound
{
    /// <summary>
    /// Runs the week: the morning, the queue of claimants, each case from bell to receipt, and the
    /// evening ledger. Owns the story state and the record of what the player has discovered.
    /// </summary>
    public partial class Director : MonoBehaviour
    {
        public static Director I { get; private set; }
        public ContentDb Db { get; private set; }
        public StoryState State { get; private set; }
        public SaveGame Save { get; private set; }
        public int Day { get; private set; }
        public DayDef DayDef { get; private set; }
        public CaseDef Current { get; private set; }

        enum Phase { Idle, Morning, AwaitBell, Intro, Investigate, Resolving, Evening }
        Phase phase = Phase.Idle;
        readonly List<Commuter> window = new();
        readonly List<Commuter> companions = new();
        HashSet<string> discovered = new();
        int caseIndex;
        bool bellRung;
        bool committed;
        Verdict committedVerdict;
        int committedWho;
        bool asking;
        int detailsFoundThisCase;
        readonly List<Renderer[]> glows = new();
        float glowT;
        public bool Autopilot;
        /// <summary>Dialogue and notes advance by themselves (autopilot, recordings, headless tests).</summary>
        public static bool AutoAdvance => I != null && (I.Autopilot || I.autoAdvance);
        public bool autoAdvance;
        /// <summary>Auto-advancing, but at a pace a viewer can read (recordings of the AutoPilot).</summary>
        public static bool Cinematic;
        public bool autoRing;

        public static readonly Vector3 WindowSpot = new(0f, -0.3f, 1.6f);

        void Awake() => I = this;

        public void Init(ContentDb db, SaveGame save)
        {
            Db = db;
            Save = save;
            State = save.state;
            discovered = new HashSet<string>(save.discovered);
        }

        // ------------------------------------------------------------------ queries used by the desk

        public bool CanRing => phase == Phase.AwaitBell && !bellRung;
        /// <summary>The case the bell will call up next (set while waiting for the bell).</summary>
        public CaseDef Upcoming { get; private set; }
        /// <summary>The day's cases are done: the evening, the ledger and the Gazette.</summary>
        public bool InEvening => phase == Phase.Evening;
        /// <summary>A day is under way (the pause menu is available).</summary>
        public bool Running => phase != Phase.Idle && DayDef != null;
        public bool CanUseStamps => phase == Phase.Investigate && !asking;
        /// <summary>A question is being put to the claimant and answered.</summary>
        public bool Asking => asking;
        /// <summary>Everything said in the current claim, shown beside the slip while you read it.</summary>
        public readonly Transcript Transcript = new();
        /// <summary>DialogueBox.SaidCount when this claim began: the AutoPilot checks every line since is in the transcript.</summary>
        public int TranscriptSaidAt { get; private set; }
        public string TodayText => DayDef != null ? DayDef.date : "";
        public bool IsDiscovered(ObjectDef o, DetailDef d) => discovered.Contains(o.id + "." + d.id);

        public string FactText(ObjectDef o, DetailDef d) =>
            !string.IsNullOrEmpty(d.altIf) && State.Check(d.altIf) && !string.IsNullOrEmpty(d.altFact) ? d.altFact : d.fact;

        public void Discover(ObjectDef o, DetailDef d)
        {
            if (!discovered.Add(o.id + "." + d.id)) return;
            Save.discovered = discovered.ToList();
            if (Current != null && o.id == Current.wants && d.kind != "secret")
            {
                ClaimSlip.I.AddClue(d.id, FactText(o, d));
                detailsFoundThisCase++;
            }
            else if (Current != null && d.kind != "secret")
            {
                // a finding about some other object still goes on the slip, so the player can ask about it
                ClaimSlip.I.AddClue(o.id + ":" + d.id, $"{o.name}: {FactText(o, d)}");
            }
            if (Current != null) OnClaimProgress();
        }

        public void OnItemInspected(ItemView item)
        {
            if (Current == null) return;
            if (item.def.id == Current.wants) wantedSeen = true;
            OnClaimProgress();
        }

        public void OnTrayChanged(ItemView item) { if (Current != null) OnClaimProgress(); }

        // ------------------------------------------------------------------ day flow

        /// <summary>Begin a day from its morning, or (<paramref name="resumeAt"/> &gt; 0) pick it up after that many cases,
        /// with the state as the last verdict left it (Continue after quitting mid-day).</summary>
        public void StartDay(int day, int resumeAt = 0)
        {
            StopAllCoroutines();
            StartCoroutine(DayRoutine(day, resumeAt));
        }

        IEnumerator DayRoutine(int day, int resumeAt = 0)
        {
            phase = Phase.Morning;
            Day = day;
            rulesShownToday.Clear();
            DayDef = Db.Day(day);
            if (resumeAt > 0)
                Debug.Log($"[Day] {day}: resuming after {resumeAt} case(s)");
            else
            {
                // the state at the start of a day is kept so the day can be replayed
                var snap = Save.SnapshotFor(day);
                if (snap != null) State = snap;
                else Save.Snapshot(day, State);
            }
            Save.state = State;
            Save.currentDay = day;
            Save.casesDone = resumeAt;
            Save.Write();

            CleanupWindow();
            Desk.I.SpawnItems(Db, day, State);
            ClaimSlip.I.Clear();
            TicketPrinter.I.ClearSpike();
            Desk.I.props.calendar.Set(DayDef.weekday, DayNumber(DayDef.date), "October 1962");
            Lamp.I.uvUnlocked = Db.RulesKnownAt(day, "").Contains(Rules.LampRule) || DayDef.rules.Contains(Rules.LampRule);
            Lamp.I.ForceOff();
            ApplyStoryVisuals();
            CameraRig.I.SetView(View.Counter, true);
            CameraRig.I.ClearFocus();
            AudioDirector.Ambience("amb_concourse", "amb_clock");
            AudioDirector.Music("day" + Mathf.Clamp(day, 1, 5), 3f);
            // while the screen is still dark: yesterday's objects (or the game this one replaced) are gone by the next
            // frame, so let go of the materials and textures made for them at runtime, as a scene load would
            yield return null;
            yield return Game.UnloadLeftovers();

            yield return UIRoot.I.dayCard.Show($"Day {ToWords(day)}  ·  {DayDef.weekday}", DayDef.title, DayDef.date);
            yield return UIRoot.I.fader.FadeTo(0f, 1.2f);

            if (resumeAt > 0)
            {
                // picking up mid-day: no morning again, but today's rules (and any a case handed over) still count
                foreach (int rid in DayDef.rules) rulesShownToday.Add(rid);
                foreach (var done in Rules.ActiveCases(DayDef, State).Take(resumeAt))
                    if (int.TryParse(done.unlockRule ?? "", out int urid)) rulesShownToday.Add(urid);
                yield return CasesAndEvening(resumeAt);
                yield break;
            }

            // the morning: Gus drops off the night's intake, Agnes's note for the day
            Debug.Log($"[Day] {day}: morning");
            bool gus = DayDef.morning.Any(l => l.who == "gus" && State.Check(l.condition));
            if (gus) yield return Visit("gus", true);
            foreach (var line in DayDef.morning)
                yield return SayLine(line, null);
            // and takes yesterday's unclaimed strays down to the basement
            var strays = Db.root.objects.Where(o => State.InStorage(o, day) && Db.Archived(o, day) && !Db.Archived(o, day - 1)).ToList();
            if (strays.Count > 0)
            {
                Debug.Log($"[Day] {day}: down to the basement: {string.Join(", ", strays.Select(o => o.id))}");
                if (!gus) yield return Visit("gus", true);
                string what = strays.Count == 1 ? TheName(strays[0].name) : $"{Count(strays.Count)} of yesterday's strays";
                yield return SayLine(new LineDef { who = "gus", text = $"Nobody came back for {what}, so I've taken {(strays.Count == 1 ? "it" : "them")} down to the basement. Makes a bit of room on the shelves." }, null);
            }
            if (window.Count > 0) yield return LeaveAll();
            foreach (int rid in DayDef.rules)
                yield return ShowRule(rid);
            UIRoot.I.dialogue.Hide();

            yield return CasesAndEvening(0);
        }

        IEnumerator CasesAndEvening(int from)
        {
            var cases = Rules.ActiveCases(DayDef, State).ToList();
            caseIndex = from;
            if (Day == 1 && from == 0 && !Save.tutorialDone) StartCoroutine(Tutorial());
            while (caseIndex < cases.Count)
            {
                // cases can be replaced by alternates as flags change during the day
                cases = Rules.ActiveCases(DayDef, State).ToList();
                if (caseIndex >= cases.Count) break;
                var c = cases[caseIndex];
                Upcoming = c;
                phase = Phase.AwaitBell;
                bellRung = false;
                UIRoot.I.hint.Set("Ring the bell for the next claimant");
                while (!bellRung)
                {
                    if (InputX.KeyDown(Key.Space) && !UIRoot.ModalOpen && !UIRoot.I.dialogue.Shown)
                        Desk.I.props.bell.Ring();
                    if (Autopilot || autoRing) Desk.I.props.bell.Ring();
                    yield return null;
                }
                UIRoot.I.hint.Set(null);
                yield return RunCase(c);
                caseIndex++;
                Save.casesDone = caseIndex;
            }
            yield return Evening();
        }

        public void RingBell()
        {
            if (phase == Phase.AwaitBell) bellRung = true;
        }

        static int DayNumber(string date)
        {
            foreach (var w in date.Split(' ')) if (int.TryParse(w, out int n) && n < 32) return n;
            return 15;
        }

        static string Count(int n) => n switch { 2 => "two", 3 => "three", 4 => "four", 5 => "five", 6 => "six", _ => n.ToString() };

        static string ToWords(int d) => d switch { 1 => "One", 2 => "Two", 3 => "Three", 4 => "Four", 5 => "Five", _ => d.ToString() };

        // ------------------------------------------------------------------ a case

        IEnumerator RunCase(CaseDef c)
        {
            phase = Phase.Intro;
            Current = c;
            committed = false;
            detailsFoundThisCase = 0;
            var item = Db.Object(c.wants);
            bool present = item == null || State.InStorage(item, Day);

            // claimants walk up to the window
            yield return ArriveAll(c);
            var names = c.claimants.Select(id => Db.Commuter(id)?.name ?? id).ToArray();
            int no = 100 + Day * 10 + caseIndex + 1;
            ClaimSlip.I.Begin($"{no:0000}", $"{DayDef.weekday} {DayNumber(DayDef.date)} Oct", names);
            Transcript.Clear();
            TranscriptSaidAt = DialogueBox.SaidCount;
            UpdateHums(null);

            // documents the claimant slides under the glass (a chit, a certificate, an order)
            foreach (var pid in c.presents)
            {
                var pdef = Db.Object(pid);
                if (pdef != null) yield return Desk.I.Present(pdef);
            }

            if (!string.IsNullOrEmpty(c.unlockRule) && int.TryParse(c.unlockRule, out int rid))
                yield return ShowRule(rid);

            if (item != null && !present)
            {
                // an earlier decision took the item away: a short vignette, then they go
                foreach (var l in c.missing) yield return SayLine(l, c);
                State.SetRecord(new CaseRecord { caseId = c.id, verdict = "missing", grade = "skip", ledger = c.missingLedger });
                Save.casesDone = caseIndex + 1;
                Save.Write();
                yield return LeaveAll();
                Current = null;
                yield break;
            }

            // the description: claims are written onto the slip as they are spoken
            var written = new HashSet<string>();
            foreach (var line in c.intro)
            {
                var keys = new List<string>();
                yield return SayLine(line, c, keys);
                foreach (var k in keys)
                    foreach (var cl in c.claims.Where(x => x.key == k && (string.IsNullOrEmpty(x.who) || x.who == line.who)))
                        if (written.Add(cl.who + "/" + cl.key))
                        {
                            ClaimSlip.I.AddClaim(cl.text, Db.Commuter(cl.who)?.ShortName);
                            yield return new WaitForSeconds(0.12f);
                        }
            }
            foreach (var cl in c.claims)
                if (written.Add(cl.who + "/" + cl.key)) ClaimSlip.I.AddClaim(cl.text, Db.Commuter(cl.who)?.ShortName);

            // already-known findings about this object go straight onto the slip
            if (item != null)
                foreach (var d in item.CaseDetails)
                    if (IsDiscovered(item, d)) ClaimSlip.I.AddClue(d.id, FactText(item, d));

            phase = Phase.Investigate;
            UIRoot.I.hint.Set(string.IsNullOrEmpty(c.hint) ? null : c.hint);
            BeginNudges(c);
            while (true)
            {
                while (!committed) yield return null;
                committed = false;
                UIRoot.I.hint.Set(null);
                bool done = false;
                yield return Resolve(c, committedVerdict, committedWho, ok => done = ok);
                if (done) break;
                phase = Phase.Investigate;
            }
            Current = null;
        }

        IEnumerator ArriveAll(CaseDef c)
        {
            var ids = c.claimants.ToList();
            int n = ids.Count;
            bool fromLeft = (caseIndex + Day) % 2 == 0;
            AudioDirector.Play("footsteps", 0.6f);
            var routines = new List<Coroutine>();
            for (int i = 0; i < n; i++)
            {
                var def = Db.Commuter(ids[i]);
                var cm = Commuter.Spawn(def, transform);
                float x = n == 1 ? 0f : (i == 0 ? -0.34f : 0.34f);
                cm.standPos = WindowSpot + new Vector3(x, 0f, i * 0.02f);
                Vector3 start = new Vector3(fromLeft ? -3.4f : 3.4f, WindowSpot.y, 2.6f + i * 0.4f);
                cm.transform.position = start;
                window.Add(cm);
                routines.Add(StartCoroutine(cm.WalkTo(cm.standPos, 1.2f + i * 0.1f)));
                if (def.cold) WindowFX.I?.SetFrost(true);
                yield return new WaitForSeconds(0.25f);
            }
            foreach (var cid in c.companions)
            {
                var def = Db.Commuter(cid);
                if (def == null) continue;
                var cm = Commuter.Spawn(def, transform);
                cm.standPos = WindowSpot + new Vector3(0.48f, 0f, 0.35f);
                cm.transform.position = new Vector3(fromLeft ? -3.6f : 3.6f, WindowSpot.y, 2.9f);
                companions.Add(cm);
                routines.Add(StartCoroutine(cm.WalkTo(cm.standPos, 1.1f)));
            }
            foreach (var r in routines) yield return r;
        }

        /// <summary>A non-case visitor (Gus, epilogue guests) walks up to the window.</summary>
        IEnumerator Visit(string id, bool fromLeft)
        {
            var def = Db.Commuter(id);
            if (def == null) yield break;
            var cm = Commuter.Spawn(def, transform);
            cm.standPos = WindowSpot;
            cm.transform.position = new Vector3(fromLeft ? -3.4f : 3.4f, WindowSpot.y, 2.6f);
            window.Add(cm);
            AudioDirector.Play("footsteps", 0.5f);
            yield return cm.WalkTo(cm.standPos, 1.3f);
        }

        IEnumerator LeaveAll()
        {
            bool toRight = (caseIndex + Day) % 2 == 0;
            var all = window.Concat(companions).ToList();
            var routines = all.Select(cm => StartCoroutine(cm.WalkTo(new Vector3(toRight ? 3.6f : -3.6f, WindowSpot.y, 2.8f), 1.3f))).ToList();
            UpdateHums(null, true);
            WindowFX.I?.SetFrost(false);
            AudioDirector.Play("footsteps", 0.45f);
            foreach (var r in routines) yield return r;
            CleanupWindow();
        }

        void CleanupWindow()
        {
            foreach (var cm in window.Concat(companions)) if (cm != null) Destroy(cm.gameObject);
            window.Clear();
            companions.Clear();
            Desk.I.SetHums(new string[0], 0f);
        }

        void UpdateHums(string speaking, bool off = false)
        {
            if (off || Current == null) { Desk.I.SetHums(new string[0], 0f); return; }
            Desk.I.SetHums(Current.claimants.Concat(Current.companions), 0.65f);
            if (!string.IsNullOrEmpty(speaking)) Desk.I.SetHums(new[] { speaking }, 1f, additive: true);
        }

        Commuter WindowCommuter(string id) => window.Concat(companions).FirstOrDefault(c => c.def.id == id);

        /// <summary>Say one line. Speakers: a commuter id, "agnes" (a note), "gus", "you".</summary>
        IEnumerator SayLine(LineDef line, CaseDef c, List<string> keys = null)
        {
            if (!State.Check(line.condition)) yield break;
            if (line.who == "agnes")
            {
                yield return UIRoot.I.note.Show(null, line.text);
                yield break;
            }
            if (line.who == "you")
            {
                if (c != null) Transcript.Add("You", DialogueBox.Parse(line.text, null), true);
                yield return UIRoot.I.dialogue.Say("You", DialogueBox.Parse(line.text, keys), null, null, true, true);
                yield break;
            }
            var def = Db.Commuter(line.who);
            var cm = WindowCommuter(line.who);
            cm?.Emote(line.emote);
            if (c != null && cm != null) UpdateHums(line.who);
            string speaker = def != null ? (string.IsNullOrEmpty(def.title) ? def.name : $"{def.name}  ·  {def.title}") : line.who;
            if (c != null) Transcript.Add(def?.ShortName ?? line.who, DialogueBox.Parse(line.text, null));
            yield return UIRoot.I.dialogue.Say(speaker, DialogueBox.Parse(line.text, keys), def, cm, !Autopilot || true);
            if (c != null) UpdateHums(null);
        }

        readonly HashSet<int> rulesShownToday = new();

        /// <summary>The rules Agnes has actually handed you by now: every earlier day's, and today's so far.</summary>
        public HashSet<int> KnownRules()
        {
            var known = Db.RulesBefore(Day);
            known.UnionWith(rulesShownToday);
            return known;
        }

        IEnumerator ShowRule(int rid)
        {
            var r = Db.Rule(rid);
            if (r == null) yield break;
            rulesShownToday.Add(rid);
            UIRoot.I.dialogue.Hide();
            yield return UIRoot.I.note.Show($"Rule {ToWords(rid).ToLowerInvariant()}", r.text);
        }

        // ------------------------------------------------------------------ asking

        public void Ask(string detailId)
        {
            if (phase != Phase.Investigate || asking || Current == null) return;
            askedThisCase.Add(detailId);
            OnClaimProgress();
            StartCoroutine(AskRoutine(detailId));
        }

        IEnumerator AskRoutine(string clueId)
        {
            asking = true;
            ObjectDef obj = Db.Object(Current.wants);
            string detailId = clueId;
            int colon = clueId.IndexOf(':');
            if (colon > 0) { obj = Db.Object(clueId.Substring(0, colon)); detailId = clueId.Substring(colon + 1); }
            var d = obj?.Detail(detailId);
            if (d == null) { asking = false; yield break; }
            ClaimSlip.I.SetFocus(false);
            string q = colon > 0 ? $"About {TheName(obj.name)}: {d.question}" : d.question;
            Transcript.Add("You", q, true);
            yield return UIRoot.I.dialogue.Say("You", q, null, null, true, true);
            foreach (var who in Current.claimants)
            {
                var ans = colon > 0 ? null : Current.answers.FirstOrDefault(a => a.detail == detailId && (a.who == who || (string.IsNullOrEmpty(a.who) && Current.claimants.Length == 1)));
                var def = Db.Commuter(who);
                string text = ans != null ? ans.text : Shrug(def);
                yield return SayLine(new LineDef { who = who, text = text, emote = ans != null && !ans.truthful ? "" : "" }, Current);
            }
            asking = false;
        }

        /// <summary>An object's name mid-sentence: "the silver locket", "the Iron Drawer key", "Mr Vell's chit". A later word
        /// with a capital makes it a name, kept as written; one that starts with a title takes no "the".</summary>
        public static string TheName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "it";
            var words = name.Split(' ');
            if (words.Length > 1 && System.Array.IndexOf(new[] { "Mr", "Mrs", "Miss", "Prof.", "Lt." }, words[0]) >= 0) return name;
            for (int i = 1; i < words.Length; i++)
                if (words[i].Length > 0 && char.IsUpper(words[i][0])) return "the " + name;
            return "the " + char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        static string Shrug(CommuterDef def)
        {
            if (def == null) return "I couldn't say.";
            if (!string.IsNullOrEmpty(def.shrug)) return def.shrug;
            return "I couldn't tell you, I'm afraid.";
        }

        // ------------------------------------------------------------------ stamping

        public bool CanStamp(Verdict v, int who, out string reason)
        {
            reason = null;
            if (phase != Phase.Investigate || Current == null) { reason = "There's no claim to stamp."; return false; }
            if (asking) { reason = "Let them finish."; return false; }
            if ((v == Verdict.Return || v == Verdict.Seal) && Desk.I.OnTray == null)
            {
                reason = v == Verdict.Return ? "Put the item on the counter tray first (pick it up, then press T)." : "Put the item to be sealed on the tray first.";
                return false;
            }
            return true;
        }

        public void CommitStamp(Verdict v, int who)
        {
            committedVerdict = v;
            committedWho = who;
            committed = true;
            phase = Phase.Resolving;
            OnClaimProgress();
        }

        IEnumerator Resolve(CaseDef c, Verdict v, int whoIdx, System.Action<bool> done)
        {
            string to = v == Verdict.Return ? c.claimants[Mathf.Clamp(whoIdx, 0, c.claimants.Length - 1)] : null;
            var trayItem = Desk.I.OnTray;
            ClaimSlip.I.SetFocus(false);
            if (InspectController.I.Held != null) InspectController.I.Release(ItemPlace.Mat);

            if (v == Verdict.Return && trayItem != null && trayItem.def.id != c.wants)
            {
                // the wrong thing: the claimant pushes it back
                var lines = c.notMine.Length > 0 ? c.notMine : new[] { new LineDef { who = to, text = "That's not mine, I'm afraid.", emote = "shy" } };
                foreach (var l in lines) yield return SayLine(new LineDef { who = string.IsNullOrEmpty(l.who) ? to : l.who, text = l.text, emote = l.emote }, c);
                UIRoot.I.dialogue.Hide();
                ClaimSlip.I.VoidLastImprint();
                done(false);
                yield break;
            }

            var vd = Rules.FindVerdict(c, v, to);
            if (vd == null) vd = new VerdictDef { verdict = VerdictNames.Key(v), grade = "wrong" };

            // the receipt
            var item = Db.Object(c.wants);
            string verdictWord = v switch { Verdict.Return => "RETURNED", Verdict.Seal => "SEALED", _ => "REFUSED" };
            var receipt = new[]
            {
                "NINEFOLD JUNCTION", "LOST PROPERTY", "- - - - - - - - - -",
                $"{DayDef.weekday.ToUpperInvariant()} {DayNumber(DayDef.date)} OCT 1962",
                $"CLAIM  {100 + Day * 10 + caseIndex + 1:0000}",
                item != null ? item.name.ToUpperInvariant() : "(NO ITEM)",
                "{V}" + verdictWord + "{/V}",
                v == Verdict.Return ? "TO " + (Db.Commuter(to)?.name.ToUpperInvariant() ?? "") : "",
                "- - - - - - - - - -", "THANK YOU",
            };
            var printing = StartCoroutine(TicketPrinter.I.Print(receipt, v));

            // reactions and the hand-over / seal
            foreach (var l in vd.reply) yield return SayLine(l, c);
            if (v == Verdict.Return && trayItem != null)
            {
                var cm = WindowCommuter(to);
                yield return Desk.I.HandOver(trayItem, cm != null ? cm.HandPosition : WindowSpot + Vector3.up * 1.1f);
                AudioDirector.Play("receive", 0.5f);
            }
            else if (v == Verdict.Seal && trayItem != null)
            {
                yield return Desk.I.SealRoutine(trayItem);
            }
            UIRoot.I.dialogue.Hide();
            yield return printing;
            var first = WindowCommuter(c.claimants[0]);
            yield return Desk.I.ReturnPresented(first != null ? first.HandPosition : WindowSpot + Vector3.up * 1.1f);

            // record and apply
            bool ringBefore = State.Check("ring=thomas");
            Rules.ApplyVerdict(Db, c, vd, to, State);
            bool worldChanged = !ringBefore && State.Check("ring=thomas");
            if (v == Verdict.Seal && trayItem != null && trayItem.def.id != c.wants) State.SetObjectLocation(trayItem.def.id, "sealed");
            int total = item != null ? item.CaseDetails.Count() : 0;
            int found = item != null ? item.CaseDetails.Count(d => IsDiscovered(item, d)) : 0;
            State.SetRecord(new CaseRecord
            {
                caseId = c.id, verdict = vd.verdict, to = to, grade = vd.grade, ledger = vd.ledger,
                detailsFound = found, detailsTotal = total,
            });
            if (!worldChanged) ApplyStoryVisuals();
            // the verdict and "this case is done" go into the save together, so Continue can't play it twice
            Save.casesDone = caseIndex + 1;
            if (Day == 1 && c == DayDef.cases[0]) Save.tutorialDone = true;
            Save.Write();

            yield return LeaveAll();
            if (worldChanged) yield return PhotographsChange();
            yield return Desk.I.Tidy();
            Desk.I.CloseAllDrawers();
            ClaimSlip.I.Clear();
            if (Day == 1 && c == DayDef.cases[0]) { Save.tutorialDone = true; ClearGlows(); }
            done(true);
        }

        // ------------------------------------------------------------------ evening

        IEnumerator Evening()
        {
            phase = Phase.Evening;
            UIRoot.I.hint.Set(null);
            if (DayDef.evening.Any(l => l.who == "gus" && State.Check(l.condition))) yield return Visit("gus", Day % 2 == 0);
            foreach (var line in DayDef.evening)
                yield return SayLine(line, null);
            if (Day == 1 && Desk.I.props.polaroid != null && !Desk.I.props.polaroid.gameObject.activeSelf)
            {
                // the photograph off the last train: you, at this desk
                UIRoot.I.dialogue.Hide();
                yield return Desk.I.props.ArrivePolaroid(Desk.I.tray.position);
                yield return new WaitForSeconds(2.2f);
            }
            if (window.Count > 0) yield return LeaveAll();
            UIRoot.I.dialogue.Hide();
            AudioDirector.Play("shutter_down", 0.8f);
            yield return UIRoot.I.fader.FadeTo(1f, 1.2f);
            yield return LedgerView.Show(this);
        }

        /// <summary>Stamps for a day's records: 3 = all best, 2 = at most one slip, 1 = finished.</summary>
        public (int stamps, int correct, int total) Score(int day) => Rules.Score(Db.Day(day), State);

        public void FinishDay()
        {
            var (stamps, correct, total) = Score(Day);
            Save.RecordBest(Day, stamps, correct, total);
            Save.unlockedDay = Mathf.Max(Save.unlockedDay, Day + 1);
            if (Day >= Db.DayCount) { Save.finished = true; Save.Write(); StartCoroutine(Ending()); return; }
            Save.currentDay = Day + 1;
            Save.casesDone = 0;
            Save.Snapshot(Day + 1, State);
            Save.Write();
            StartDay(Day + 1);
        }

        // ------------------------------------------------------------------ the tutorial (case 1.1)

        IEnumerator Tutorial()
        {
            var bell = Desk.I.props.bell;
            while (phase == Phase.AwaitBell || phase == Phase.Morning) { Glow(bell.HighlightRenderers); yield return null; }
            ClearGlows();
            while (phase == Phase.Intro) yield return null;
            var wallet = Desk.I.items.Values.FirstOrDefault(i => i.def.id == Current?.wants);
            if (wallet == null) yield break;
            var drawer = wallet.drawer;
            while (!Save.tutorialDone && Current != null && Current.id == DayDef.cases[0].id)
            {
                ClearGlows();
                string h;
                bool discoveredAny = wallet.def.CaseDetails.Any(d => IsDiscovered(wallet.def, d));
                if (phase != Phase.Investigate) h = null;
                else if (InspectController.I.Held == wallet)
                    h = discoveredAny ? GamepadInput.Prompt("Good. Press T to put it on the counter tray.", "Good. Press X to put it on the counter tray.") : GamepadInput.Prompt("Drag to turn it over. Click the clasp to open it, and look closely.", "Turn it over with the right stick. Press A on the clasp to open it, and look closely.");
                else if (Desk.I.OnTray == wallet)
                {
                    h = "Pick up the green RETURN stamp and stamp the claim slip.";
                    Glow(Desk.I.props.stamps[0].HighlightRenderers);
                }
                else if (CameraRig.I.view != View.Cabinet && wallet.place == ItemPlace.Storage)
                    h = GamepadInput.Prompt("Walter's tag says drawer A. Turn left to the drawers  (A or ←)", "Walter's tag says drawer A. Turn left to the drawers  (LB)");
                else if (drawer != null && !drawer.IsOpen && wallet.place == ItemPlace.Storage)
                {
                    h = "Open drawer A.";
                    Glow(drawer.HighlightRenderers);
                }
                else if (wallet.place == ItemPlace.Storage || wallet.place == ItemPlace.Mat)
                {
                    h = "Hover the wallet to read its tag, then click to pick it up.";
                    Glow(wallet.HighlightRenderers);
                }
                else h = null;
                UIRoot.I.hint.Set(h);
                yield return null;
            }
            ClearGlows();
            UIRoot.I.hint.Set(null);
        }

        void Glow(Renderer[] rs)
        {
            if (rs == null) return;
            glows.Add(rs);
            glowT += Time.deltaTime;
            if (InteractionSystem.I.Hovered != null && InteractionSystem.I.Hovered.HighlightRenderers == rs) return;
            Highlighter.Set(rs, new Color(0.5f, 0.38f, 0.12f) * (0.5f + 0.5f * Mathf.Sin(Time.time * 4f)));
        }

        void ClearGlows()
        {
            foreach (var g in glows)
                if (InteractionSystem.I.Hovered == null || InteractionSystem.I.Hovered.HighlightRenderers != g)
                    Highlighter.Clear(g);
            glows.Clear();
        }

        // ------------------------------------------------------------------ story visuals

        public void ApplyStoryVisuals()
        {
            PostFX.I?.SetGreyness(State.GetInt("vellItems") * 0.14f + (State.Check("ring=vell") ? 0.2f : 0f));
            foreach (var p in Desk.I.props.photos) p.SetPhoto(PhotoTexture(p.photoId));
            if (Desk.I.props.polaroid != null) Desk.I.props.polaroid.gameObject.SetActive(Day >= 2);
        }

        /// <summary>
        /// The ring has gone home. The music stops; one by one every photograph on the desk glows and
        /// becomes the world where Thomas made it to the platform.
        /// </summary>
        public bool ChangingPhotos { get; private set; }

        IEnumerator PhotographsChange()
        {
            ChangingPhotos = true;
            UIRoot.I.hint.Set(null);
            InteractionSystem.I.Blocked = true;
            CameraRig.I.allowTurn = false;
            CameraRig.I.SetView(View.Counter);
            AudioDirector.FadeMusic(0f);
            yield return new WaitForSeconds(1.4f);
            AudioDirector.Music("ring", 0.5f);
            AudioDirector.FadeMusic(1f);
            yield return new WaitForSeconds(0.8f);
            var all = Desk.I.props.photos.Where(p => p.gameObject.activeInHierarchy).OrderBy(p => p.transform.position.x).ToList();
            foreach (var p in all)
            {
                Vector3 at = p.photo != null ? p.photo.bounds.center : p.transform.position + Vector3.up * 0.07f;
                // look square at the picture, from a little above, like leaning in to it
                Vector3 toEye = CameraRig.I.eye - at;
                toEye.y = 0f;
                Vector3 face = p == Desk.I.props.polaroid
                    ? (-p.transform.forward * 0.55f + Vector3.up).normalized      // lying flat: lean over it from its bottom edge
                    : (-p.transform.forward * 0.8f + Vector3.up * 0.55f).normalized;  // framed: from above, clear of pens and cups
                Vector3 fromEye = at + face * (p == Desk.I.props.polaroid ? 0.3f : 0.24f);
                CameraRig.I.Focus(at, fromEye, 38f);
                // nothing on the desk may stand between the close-up and the picture
                var hidden = new List<Renderer>();
                var sight = new Ray(fromEye, (at - fromEye).normalized);
                float reach = Vector3.Distance(fromEye, at) - 0.02f;
                foreach (var r in Desk.I.props.GetComponentsInChildren<Renderer>())
                    if (r.enabled && r.bounds.size.magnitude < 0.5f && !r.transform.IsChildOf(p.transform)
                        && !r.transform.IsChildOf(Desk.I.booth) && !r.transform.IsChildOf(Desk.I.concourse)
                        && r.bounds.IntersectRay(sight, out float hit) && hit < reach)
                    { r.enabled = false; hidden.Add(r); }
                yield return new WaitForSeconds(1.1f);
                yield return p.Change(PhotoTexture(p.photoId));
                yield return new WaitForSeconds(0.6f);
                foreach (var r in hidden) if (r != null) r.enabled = true;
            }
            foreach (var it in Desk.I.items.Values) if (it != null) it.ApplyStory(State);
            CameraRig.I.ClearFocus();
            yield return new WaitForSeconds(1.0f);
            InteractionSystem.I.Blocked = false;
            CameraRig.I.allowTurn = true;
            AudioDirector.Music("day" + Mathf.Clamp(Day, 1, 5), 4f);
            ChangingPhotos = false;
        }

        public Texture2D PhotoTexture(string id)
        {
            string variant = State.Check("ring=thomas") ? "after" : "before";
            var t = Resources.Load<Texture2D>($"Photos/{id}_{variant}");
            return t != null ? t : Resources.Load<Texture2D>($"Photos/{id}_before");
        }

        public string PhotoCaption(string id)
        {
            bool after = State.Check("ring=thomas");
            return id switch
            {
                "staff1921" => after ? "Lost Property, 1921. Agnes and Thomas behind this very desk." : "Lost Property staff, 1921. Agnes on her first day.",
                "retirement" => after ? "Forty-one years. Agnes and Thomas, with cake." : "Agnes's retirement, last Friday. Forty-one years.",
                "mum" => after ? "Mum and me, 1951, with Grandma Agnes and Grandpa Tom." : "Mum and me at the seaside, 1951.",
                "platform9" => after ? "Platform 9, 1921. Thomas is waving." : "The opening of Platform 9, 1921.",
                "polaroid" => after ? "You, at this desk. Behind you: Grandma Agnes and Grandpa Tom." : "A photograph of you, at this desk. It came off the last train from Platform 9.",
                _ => null,
            };
        }
    }
}
