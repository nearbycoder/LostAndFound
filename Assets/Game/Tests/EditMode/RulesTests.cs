using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace LostAndFound.Tests
{
    /// <summary>
    /// The deduction rules, the story's consequences and the save file, checked against the real content
    /// without starting the game. A whole week plays out in memory the way the AutoPilot plays it.
    /// </summary>
    public class RulesTests
    {
        static ContentDb db;

        static ContentDb Db
        {
            get
            {
                if (db != null) return db;
                var parts = Resources.LoadAll<TextAsset>("Content").Select(t => JsonUtility.FromJson<ContentRoot>(t.text)).ToArray();
                return db = new ContentDb(ContentDb.Merge(parts));
            }
        }

        // ------------------------------------------------------------------ a week in memory

        enum Policy { Best, Worst, Wait }

        class Week
        {
            public StoryState state = new();
            public readonly List<(CaseDef c, Decision d, string grade)> played = new();
            public int skipped;
        }

        /// <summary>Plays every day with the solver's verdicts, or the AutoPilot's other policies: worst
        /// (a wrong verdict wherever there is one, preferring a wrong return) and wait (Thomas is refused the ring).</summary>
        static Week Play(Policy policy)
        {
            var w = new Week();
            foreach (var day in Db.root.days)
                foreach (var c in Rules.ActiveCases(day, w.state).ToList())
                {
                    var item = Db.Object(c.wants);
                    if (item != null && !w.state.InStorage(item, day.day))
                    {
                        w.state.SetRecord(new CaseRecord { caseId = c.id, verdict = "missing", grade = "skip" });
                        w.skipped++;
                        continue;
                    }
                    var dec = Rules.Solve(Db, day.day, c, w.state);
                    if (policy == Policy.Wait && c.id == "4.5") dec = new Decision { verdict = Verdict.Refuse };
                    if (policy == Policy.Worst)
                    {
                        var wrong = c.verdicts.Where(v => v.grade == "wrong").OrderBy(v => v.verdict == "return" ? 0 : 1).FirstOrDefault();
                        if (wrong != null && !(string.IsNullOrEmpty(c.wants) && wrong.verdict != "refuse"))
                            dec = new Decision { verdict = VerdictNames.Parse(wrong.verdict), to = string.IsNullOrEmpty(wrong.to) ? c.claimants[0] : wrong.to };
                    }
                    var vd = Rules.FindVerdict(c, dec.verdict, dec.to);
                    Assert.IsNotNull(vd, $"case {c.id}: no authored response to {dec}");
                    Rules.ApplyVerdict(Db, c, vd, dec.to, w.state);
                    w.state.SetRecord(new CaseRecord { caseId = c.id, verdict = vd.verdict, to = dec.to, grade = vd.grade });
                    w.played.Add((c, dec, vd.grade));
                }
            return w;
        }

        // ------------------------------------------------------------------ content

        [Test]
        public void ContentLoadsWholeWeek()
        {
            Assert.AreEqual(5, Db.DayCount);
            Assert.AreEqual(28, Db.root.objects.Length, "25 lost objects plus the three documents claimants bring");
            Assert.AreEqual(25, Db.root.days.Sum(d => d.cases.Length), "24 cases plus Friday's alternate");
            Assert.AreEqual(8, Db.root.rules.Length);
            CollectionAssert.AreEquivalent(new[] { "nine40", "longwait", "grey" }, Db.root.endings.Select(e => e.id).Distinct().ToArray());
        }

        [Test]
        public void ValidatorFindsNoIssues()
        {
            var issues = Rules.Validate(Db);
            Assert.IsEmpty(issues, string.Join("\n", issues));
        }

        [Test]
        public void EveryObjectHasOneSecretAndAskableCaseDetails()
        {
            foreach (var o in Db.root.objects)
            {
                Assert.AreEqual(1, o.details.Count(d => d.kind == "secret"), $"{o.id}: one secret each");
                foreach (var d in o.CaseDetails) Assert.IsNotEmpty(d.question, $"{o.id}.{d.id}: case details need a question to ask");
            }
        }

        [Test]
        public void EveryDetailHasAHotspotOrAPartThatRevealsIt()
        {
            foreach (var o in Db.root.objects)
            {
                var model = Resources.Load<GameObject>("Models/Objects/" + o.ModelName);
                Assert.IsNotNull(model, $"{o.id}: no model {o.ModelName}");
                var names = new HashSet<string>(model.GetComponentsInChildren<Transform>(true).Select(t =>
                {
                    int sep = t.name.IndexOf("__", System.StringComparison.Ordinal);
                    return sep > 0 ? t.name.Substring(0, sep) : t.name;
                }));
                foreach (var d in o.details)
                {
                    bool byPart = o.parts.Any(p => p.reveals == d.id) || d.requires is "wind" or "listen" or "shake" or "play";
                    Assert.IsTrue(byPart || names.Contains(d.NodeName), $"{o.id}.{d.id}: no {d.NodeName} in the model");
                }
                foreach (var p in o.parts.Where(p => !string.IsNullOrEmpty(p.node)))
                    Assert.IsTrue(names.Contains(p.node), $"{o.id}: part {p.node} missing from the model");
            }
        }

        [Test]
        public void ShortNamesKeepTitlesAndDropNicknames()
        {
            Assert.AreEqual("Walter", Db.Commuter("walter").ShortName);
            Assert.AreEqual("Mrs Marsh", Db.Commuter("odile").ShortName);
            Assert.AreEqual("Prof. Lark", Db.root.commuters.First(c => c.name == "Prof. Ambrose Lark").ShortName);
            Assert.AreEqual("Sid", Db.root.commuters.First(c => c.name == "Big Sid Mulroney").ShortName);
            Assert.AreEqual("Mr Vell", Db.Commuter("vell").ShortName);
        }

        [Test]
        public void StraysGoToTheBasementOnlyAfterTheirLastCase()
        {
            foreach (var o in Db.root.objects)
            {
                int last = Db.LastNeededDay(o.id);
                for (int day = 1; day <= Db.DayCount; day++)
                {
                    bool gone = Db.Archived(o, day);
                    if (last == 0 || o.storage is "desk" or "presented") Assert.IsFalse(gone, $"{o.id} is never claimed (or isn't stored): it stays all week");
                    else Assert.AreEqual(day > last, gone, $"{o.id} on day {day} (last wanted on day {last})");
                }
            }
            // the pairs that would share a shelf if everything were refused never coexist
            Assert.IsTrue(Db.Archived(Db.Object("suitcase"), 4), "the suitcase is gone before the violin case arrives");
            Assert.IsTrue(Db.Archived(Db.Object("briefcase"), 5), "the briefcase is gone before the birdcage arrives");
            Assert.IsFalse(Db.Archived(Db.Object("umbrella_black"), 5), "Thomas still needs his umbrella on Friday");
        }

        // ------------------------------------------------------------------ the solver

        static IEnumerable<string> BestPathCases() => Play(Policy.Best).played.Select(p => p.c.id);

        [TestCaseSource(nameof(BestPathCases))]
        public void SolverDerivesAuthoredBestVerdict(string caseId)
        {
            var w = Play(Policy.Best);
            var (c, d, grade) = w.played.First(p => p.c.id == caseId);
            Assert.AreEqual("best", grade, $"case {caseId}: the solver chose {d}");
        }

        [Test]
        public void HumOverridesAMuddledStory()
        {
            var suitcase = Db.Object("suitcase");
            Assert.AreEqual("hum", suitcase.trait);
            var c = Db.root.days.SelectMany(d => d.cases).First(x => x.wants == "suitcase");
            var dec = Rules.Solve(Db, 1, c, new StoryState());
            Assert.AreEqual(Verdict.Return, dec.verdict);
            StringAssert.Contains("hums", dec.reason);
        }

        [Test]
        public void GreyGentlemanNeverGetsAnything()
        {
            foreach (var day in Db.root.days)
                foreach (var c in day.cases.Where(c => c.claimants.Any(id => Db.Commuter(id)?.grey == true)))
                {
                    var dec = Rules.Solve(Db, day.day, c, new StoryState());
                    Assert.IsFalse(dec.verdict == Verdict.Return && Db.Commuter(dec.to)?.grey == true, $"case {c.id}: returned to Vell");
                }
        }

        // ------------------------------------------------------------------ the week and its endings

        [Test]
        public void BestWeekReachesTheNineForty()
        {
            var w = Play(Policy.Best);
            Assert.AreEqual(24, w.played.Count + w.skipped);
            Assert.AreEqual(0, w.skipped);
            Assert.IsTrue(w.played.All(p => p.grade == "best"));
            Assert.AreEqual("nine40", Rules.ChooseEnding(Db, w.state).id);
            Assert.AreEqual(0, w.state.GetInt("vellItems"));
        }

        [Test]
        public void GivingVellEverythingGoesGrey()
        {
            var w = Play(Policy.Worst);
            Assert.AreEqual("grey", Rules.ChooseEnding(Db, w.state).id);
            Assert.Greater(w.skipped, 0, "things given away earlier leave later claimants with nothing");
        }

        [Test]
        public void RefusingThomasIsTheLongWait()
        {
            var w = Play(Policy.Wait);
            Assert.AreEqual("longwait", Rules.ChooseEnding(Db, w.state).id);
            Assert.AreEqual(23, w.played.Count(p => p.grade == "best"));
        }

        [Test]
        public void ReturningTheRingAltersFridayAndThePhotographs()
        {
            var best = Play(Policy.Best);
            var wait = Play(Policy.Wait);
            Assert.IsTrue(best.state.Check("ring=thomas"));
            Assert.IsFalse(wait.state.Check("ring=thomas"));
            var friday = Db.Day(5);
            CollectionAssert.AreNotEqual(Rules.ActiveCases(friday, best.state).Select(c => c.id).ToList(),
                Rules.ActiveCases(friday, wait.state).Select(c => c.id).ToList(), "Thursday's choice swaps in Friday's alternate");
        }

        // ------------------------------------------------------------------ scoring

        static (int, int, int) ScoreWith(params string[] grades)
        {
            var day = Db.Day(1);
            var s = new StoryState();
            for (int i = 0; i < day.cases.Length; i++)
                s.SetRecord(new CaseRecord { caseId = day.cases[i].id, grade = i < grades.Length ? grades[i] : "best" });
            return Rules.Score(day, s);
        }

        [Test]
        public void ThreeStampsOnlyForAPerfectDay()
        {
            Assert.AreEqual(3, ScoreWith().Item1);
            Assert.AreEqual(2, ScoreWith("ok").Item1);
            Assert.AreEqual(2, ScoreWith("wrong").Item1);
            Assert.AreEqual(2, ScoreWith("wrong", "ok").Item1, "an acceptable verdict isn't a slip");
            Assert.AreEqual(1, ScoreWith("wrong", "wrong").Item1);
            Assert.AreEqual(Db.Day(1).cases.Length - 1, ScoreWith("wrong").Item2);
        }

        [Test]
        public void SkippedCasesDontCountAgainstYou()
        {
            var (stamps, correct, total) = ScoreWith("skip");
            Assert.AreEqual(3, stamps);
            Assert.AreEqual(Db.Day(1).cases.Length - 1, total);
            Assert.AreEqual(total, correct);
        }

        // ------------------------------------------------------------------ rules as they arrive

        [Test]
        public void RulesArriveThroughTheWeek()
        {
            CollectionAssert.IsEmpty(Db.RulesBefore(1));
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, Db.RulesBefore(2));
            CollectionAssert.AreEquivalent(Enumerable.Range(1, 8), Db.RulesBefore(6));
            Assert.IsFalse(Db.RulesBefore(4).Contains(Rules.LampRule), "the blue lamp is Thursday's");
            Assert.IsTrue(Db.RulesBefore(5).Contains(Rules.LampRule));
        }

        [Test]
        public void MidShiftRulesOnlyCountFromTheirCase()
        {
            Assert.IsFalse(Db.RulesKnownAt(1, "1.1").Contains(Rules.HiddenRule));
            Assert.IsTrue(Db.RulesKnownAt(1, "1.2").Contains(Rules.HiddenRule));
            Assert.IsFalse(Db.RulesKnownAt(1, "1.3").Contains(Rules.HumRule));
            Assert.IsTrue(Db.RulesKnownAt(1, "1.4").Contains(Rules.HumRule));
        }

        // ------------------------------------------------------------------ story state and the save file

        [Test]
        public void FlagExpressionsApplyAndCheck()
        {
            var s = new StoryState();
            s.Apply("vellItems+=1");
            s.Apply("vellItems+=1");
            s.Apply("ring=thomas");
            s.Apply("polaroid");
            Assert.AreEqual(2, s.GetInt("vellItems"));
            Assert.IsTrue(s.Check("vellItems>=2"));
            Assert.IsTrue(s.Check("ring=thomas,polaroid"));
            Assert.IsFalse(s.Check("ring!=thomas"));
            Assert.IsTrue(s.Check("!missingFlag"));
            Assert.IsTrue(s.Check(""));
        }

        [Test]
        public void SaveSurvivesARoundTrip()
        {
            var w = Play(Policy.Best);
            var save = new SaveGame { currentDay = 3, unlockedDay = 4, tutorialDone = true, ending = "nine40", state = w.state };
            save.discovered.Add("wallet_brown.photo");
            save.Snapshot(3, w.state);
            save.RecordBest(2, 3, 5, 5);
            var back = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(save));
            Assert.AreEqual(1, back.version);
            Assert.AreEqual(3, back.currentDay);
            Assert.AreEqual(4, back.unlockedDay);
            Assert.IsTrue(back.tutorialDone);
            CollectionAssert.AreEqual(save.discovered, back.discovered);
            Assert.AreEqual(w.state.records.Count, back.state.records.Count);
            Assert.IsTrue(back.state.Check("ring=thomas"));
            Assert.AreEqual(w.state.ObjectLocation("ring_box"), back.state.ObjectLocation("ring_box"));
            Assert.IsNotNull(back.SnapshotFor(3));
            Assert.AreEqual(3, back.Best(2).stamps);
        }

        [Test]
        public void ReplaySnapshotsAreIndependentCopies()
        {
            var save = new SaveGame();
            var s = new StoryState();
            s.Apply("a=1");
            save.Snapshot(2, s);
            s.Apply("a=2");
            Assert.IsTrue(save.SnapshotFor(2).Check("a=1"), "a day's snapshot must not change as the day is played");
        }
    }
}
