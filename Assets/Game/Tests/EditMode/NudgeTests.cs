using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace LostAndFound.Tests
{
    /// <summary>
    /// Agnes's nudges, checked on every claim of the week: each stage of a claim has one, they only cite rules
    /// already handed over, they never quote a finding the player hasn't found or name a stamp, and the last word
    /// on a decision agrees with the rules solver.
    /// </summary>
    public class NudgeTests
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

        /// <summary>One claim followed from the bell to the decision by doing what each nudge says.</summary>
        class Walk
        {
            public readonly List<Nudge> all = new();
            public readonly List<NudgeStage> stages = new();
            public List<Nudge> decide;
        }

        static Walk Follow(int day, CaseDef c, StoryState state, HashSet<string> everFound, bool pad)
        {
            var w = new Walk();
            var item = Db.Object(c.wants);
            var p = new NudgeProgress { pad = pad };
            if (item != null) foreach (var d in item.details) if (everFound.Contains(item.id + "." + d.id)) p.found.Add(d.id);

            for (int guard = 0; guard < 40; guard++)
            {
                var list = Nudges.For(Db, day, c, state, p);
                Assert.IsNotEmpty(list, $"case {c.id}: no nudge at all");
                var stage = list[0].stage;
                Assert.IsTrue(list.All(n => n.stage == stage) || item == null, $"case {c.id}: one list mixes stages");
                w.all.AddRange(list);
                if (w.stages.Count == 0 || w.stages[^1] != stage) w.stages.Add(stage);
                CheckSafe(day, c, item, p, list);

                if (item == null) { w.decide = list; break; }
                switch (stage)
                {
                    case NudgeStage.Find:
                        Assert.AreEqual(NudgePoint.Storage, list[^1].point, $"case {c.id}: the last find nudge should show where");
                        p.itemSeen = true;
                        break;
                    case NudgeStage.Examine:
                        Assert.IsFalse(p.found.Contains(list[0].detailId), $"case {c.id}: points at {list[0].detailId}, already found");
                        Assert.AreEqual(NudgePoint.Detail, list[^1].point, $"case {c.id}: the last examine nudge should show the spot");
                        p.found.Add(list[0].detailId);
                        everFound.Add(item.id + "." + list[0].detailId);
                        break;
                    case NudgeStage.Ask:
                        Assert.IsTrue(p.found.Contains(list[0].detailId), $"case {c.id}: asks about {list[0].detailId} before it's found");
                        p.asked.Add(list[0].detailId);
                        break;
                    default:
                        w.decide = list;
                        return w;
                }
            }
            Assert.IsNotNull(w.decide, $"case {c.id}: following the nudges never reached a decision");
            return w;
        }

        /// <summary>Known rules only, no unfound finding quoted, no stamp named.</summary>
        static void CheckSafe(int day, CaseDef c, ObjectDef item, NudgeProgress p, List<Nudge> list)
        {
            var known = Db.RulesKnownAt(day, c.id);
            foreach (var n in list)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(n.text), $"case {c.id}: empty nudge");
                if (n.rule != 0) Assert.IsTrue(known.Contains(n.rule), $"case {c.id}: cites rule {n.rule} before it's handed over: {n.text}");
                foreach (var r in Db.root.rules.Where(r => !known.Contains(r.id)))
                    StringAssert.DoesNotContain(r.text, n.text, $"case {c.id}: quotes rule {r.id} early");
                foreach (var stamp in new[] { "RETURN", "REFUSE", "SEAL" })
                    StringAssert.DoesNotContain(stamp, n.text, $"case {c.id}: names a stamp");
                if (item == null) continue;
                foreach (var d in item.details.Where(d => !p.found.Contains(d.id)))
                {
                    StringAssert.DoesNotContain(d.fact, n.text, $"case {c.id}: gives away {d.id} before it's found");
                    if (!string.IsNullOrEmpty(d.altFact)) StringAssert.DoesNotContain(d.altFact, n.text, $"case {c.id}: gives away {d.id}");
                }
            }
        }

        static IEnumerable<(int day, CaseDef c, StoryState state)> Week(bool worst)
        {
            var state = new StoryState();
            foreach (var day in Db.root.days)
                foreach (var c in Rules.ActiveCases(day, state).ToList())
                {
                    var item = Db.Object(c.wants);
                    if (item != null && !state.InStorage(item, day.day))
                    {
                        state.SetRecord(new CaseRecord { caseId = c.id, verdict = "missing", grade = "skip" });
                        continue;
                    }
                    yield return (day.day, c, state);
                    var dec = Rules.Solve(Db, day.day, c, state);
                    var vd = Rules.FindVerdict(c, dec.verdict, dec.to);
                    if (worst)
                    {
                        var wrong = c.verdicts.Where(v => v.grade == "wrong").OrderBy(v => v.verdict == "return" ? 0 : 1).FirstOrDefault();
                        if (wrong != null && !(string.IsNullOrEmpty(c.wants) && wrong.verdict != "refuse"))
                        {
                            vd = wrong;
                            dec = new Decision { verdict = VerdictNames.Parse(wrong.verdict), to = string.IsNullOrEmpty(wrong.to) ? c.claimants[0] : wrong.to };
                        }
                    }
                    Rules.ApplyVerdict(Db, c, vd, dec.to, state);
                    state.SetRecord(new CaseRecord { caseId = c.id, verdict = vd.verdict, to = dec.to, grade = vd.grade });
                }
        }

        [Test]
        public void EveryClaimOfTheWeekCanBeNudgedToADecision([Values(false, true)] bool worst, [Values(false, true)] bool pad)
        {
            int claims = 0;
            var found = new HashSet<string>();
            foreach (var (day, c, state) in Week(worst))
            {
                var w = Follow(day, c, state, found, pad);
                claims++;
                if (Db.Object(c.wants) != null)
                {
                    Assert.AreEqual(NudgeStage.Find, w.stages[0], $"case {c.id}: should start by finding it");
                    Assert.AreEqual(NudgeStage.Decide, w.stages[^1], $"case {c.id}");
                }
                Assert.IsTrue(w.decide.Count >= 2, $"case {c.id}: a decision needs a rule or a reason, and how to stamp");
            }
            Assert.GreaterOrEqual(claims, 19, "most of the week's claims were walked");
        }

        [Test]
        public void TheDecisionNudgeAgreesWithTheSolver()
        {
            var found = new HashSet<string>();
            foreach (var (day, c, state) in Week(false))
            {
                var dec = Rules.Solve(Db, day, c, state);
                var w = Follow(day, c, state, found, false);
                string said = string.Join(" | ", w.decide.Select(n => n.text));
                var item = Db.Object(c.wants);
                var rules = Db.RulesKnownAt(day, c.id);
                if (item == null) { StringAssert.Contains("never handed in", said, c.id); continue; }
                if (dec.verdict == Verdict.Seal)
                {
                    Assert.IsTrue(w.decide.Any(n => n.rule == Rules.TomorrowRule), $"case {c.id}: a seal should point at rule six: {said}");
                    continue;
                }
                if (c.claimants.Any(x => Db.Commuter(x).grey) && rules.Contains(Rules.GreyRule))
                {
                    Assert.IsTrue(w.decide.Any(n => n.rule == Rules.GreyRule), $"case {c.id}: {said}");
                    continue;
                }
                if (item.trait is "hum" or "frost") continue;   // their own tests below
                foreach (var who in c.claimants)
                {
                    bool lied = Rules.Contradictions(c, who).Any();
                    string name = Db.Commuter(who).ShortName;
                    if (lied) StringAssert.Contains(name, said, $"case {c.id}: should name {who}'s contradiction");
                }
                if (c.claimants.Length == 1 && !Rules.Contradictions(c, c.claimants[0]).Any())
                    StringAssert.Contains("agrees", said, $"case {c.id}: an honest claim should be called consistent");
            }
        }

        [Test]
        public void TraitsPointAtTheirRules()
        {
            var found = new HashSet<string>();
            foreach (var (day, c, state) in Week(false))
            {
                var item = Db.Object(c.wants);
                var w = Follow(day, c, state, found, false);
                if (c.claimants.Any(x => Db.Commuter(x).grey)) continue;
                if (item?.trait == "hum") Assert.AreEqual(Rules.HumRule, w.decide[0].rule, c.id);
                if (item?.trait == "frost") Assert.AreEqual(Rules.FrostRule, w.decide[0].rule, c.id);
                if (item?.trait == "tomorrow") Assert.AreEqual(Rules.TomorrowRule, w.decide[0].rule, c.id);
            }
        }

        [Test]
        public void DecidingDetailsPutTheLieFirstAndSkipWhatATraitSettles()
        {
            CaseDef Case(string id) => Db.root.days.SelectMany(d => d.cases).First(c => c.id == id);
            Assert.AreEqual("note", Nudges.Deciding(Db, 2, Case("2.2"))[0].id, "the twins: the note decides");
            Assert.AreEqual("namestrip", Nudges.Deciding(Db, 1, Case("1.3"))[0].id, "Reggie's wrong answers come first");
            Assert.IsEmpty(Nudges.Deciding(Db, 1, Case("1.4")), "the suitcase hums: nothing to dig for");
            Assert.IsEmpty(Nudges.Deciding(Db, 5, Case("5.5")), "the Grey Gentleman: nothing to dig for");
            CollectionAssert.Contains(Nudges.Deciding(Db, 4, Case("4.4")).Select(d => d.id).ToList(), "date", "the record's date gives it away");
            CollectionAssert.Contains(Nudges.Deciding(Db, 5, Case("5.3")).Select(d => d.id).ToList(), "date", "the snow globe's date, under the lamp");
        }

        [Test]
        public void FindingSaysWhereAndAskingNamesAFoundDetail()
        {
            CaseDef Case(string id) => Db.root.days.SelectMany(d => d.cases).First(c => c.id == id);
            var s = new StoryState();
            var find = Nudges.For(Db, 2, Case("2.2"), s, new NudgeProgress());
            StringAssert.Contains("Drawer C", find[^1].text);
            StringAssert.Contains("the silver locket", find[^1].text);
            var stand = Nudges.For(Db, 1, Case("1.3"), s, new NudgeProgress { pad = true });
            StringAssert.Contains("umbrella stand", stand[1].text);
            StringAssert.Contains("RB", stand[1].text);

            var p = new NudgeProgress { itemSeen = true };
            p.found.UnionWith(new[] { "namestrip", "beak" });
            var ask = Nudges.For(Db, 1, Case("1.3"), s, p);
            Assert.AreEqual(NudgeStage.Ask, ask[0].stage);
            StringAssert.Contains("Reggie", ask[0].text);
            p.asked.Add(ask[0].detailId);
            Assert.AreEqual(NudgeStage.Decide, Nudges.For(Db, 1, Case("1.3"), s, p)[0].stage, "one wrong answer is enough");
            Assert.AreEqual("the Iron Drawer key", Nudges.The(Db.Object("iron_key")));
        }
    }
}
