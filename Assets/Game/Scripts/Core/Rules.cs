using System;
using System.Collections.Generic;
using System.Linq;

namespace LostAndFound
{
    public struct Decision
    {
        public Verdict verdict;
        public string to;
        public string reason;
        public override string ToString() => verdict == Verdict.Return ? $"return->{to} ({reason})" : $"{VerdictNames.Key(verdict)} ({reason})";
    }

    /// <summary>
    /// Agnes's rules as code. Given only what the player can learn at the desk (tag, discoverable
    /// details, the claimants' claims and answers, traits, and the rules known by then), derive the
    /// correct verdict. The validator and the EditMode tests prove that this matches the authored
    /// "best" verdict for every case, so every case is solvable from on-desk information; the
    /// AutoPilot uses it to play the week.
    /// </summary>
    public static class Rules
    {
        public const int TagRule = 1, HiddenRule = 2, HumRule = 3, TwoRule = 4, GreyRule = 5, TomorrowRule = 6, FrostRule = 7, LampRule = 8;

        public static bool IsOwner(ObjectDef item, string claimant) =>
            item != null && !string.IsNullOrEmpty(item.owner) && item.owner.Split('|').Contains(claimant);

        public static bool Hums(ObjectDef item, string claimant) => item != null && item.trait == "hum" && IsOwner(item, claimant);

        public static Decision Solve(ContentDb db, int day, CaseDef c, StoryState state)
        {
            var rules = db.RulesKnownAt(day, c.id);
            var item = db.Object(c.wants);
            if (item == null) return new Decision { verdict = Verdict.Refuse, reason = "nothing in storage matches" };
            if (!state.InStorage(item, day)) return new Decision { verdict = Verdict.Refuse, reason = "the item is no longer here" };

            if (item.trait == "tomorrow" && rules.Contains(TomorrowRule))
                return new Decision { verdict = Verdict.Seal, reason = "it comes from tomorrow" };

            var eligible = new List<string>();
            var reasons = new List<string>();
            foreach (var who in c.claimants)
            {
                var cd = db.Commuter(who);
                if (cd == null) continue;
                if (cd.grey && rules.Contains(GreyRule)) { reasons.Add($"{who}: the Grey Gentleman gets nothing"); continue; }
                if (item.trait == "frost" && rules.Contains(FrostRule) && !cd.cold) { reasons.Add($"{who}: frost goes only to the cold"); continue; }
                if (item.trait == "hum" && rules.Contains(HumRule))
                {
                    if (Hums(item, who)) { eligible.Clear(); eligible.Add(who); reasons.Add($"{who}: it hums for them"); break; }
                    reasons.Add($"{who}: it does not hum for them");
                    continue;
                }
                var lie = Contradictions(c, who).FirstOrDefault();
                if (lie != null) { reasons.Add($"{who}: contradicted by {lie}"); continue; }
                eligible.Add(who);
            }

            if (eligible.Count == 1)
                return new Decision { verdict = Verdict.Return, to = eligible[0], reason = string.Join("; ", reasons.DefaultIfEmpty("story matches")) };
            if (eligible.Count == 0)
                return new Decision { verdict = Verdict.Refuse, reason = string.Join("; ", reasons) };
            return new Decision { verdict = Verdict.Refuse, reason = "AMBIGUOUS: " + string.Join(",", eligible) };
        }

        /// <summary>The checks (tag field or detail) that contradict what this claimant said.</summary>
        public static IEnumerable<string> Contradictions(CaseDef c, string who)
        {
            bool single = c.claimants.Length == 1;
            foreach (var cl in c.claims)
                if (!cl.truthful && (cl.who == who || (single && string.IsNullOrEmpty(cl.who))))
                    yield return cl.checks;
            foreach (var a in c.answers)
                if (!a.truthful && (a.who == who || (single && string.IsNullOrEmpty(a.who))))
                    yield return "detail:" + a.detail;
        }

        public static VerdictDef FindVerdict(CaseDef c, Verdict v, string to)
        {
            string key = VerdictNames.Key(v);
            foreach (var vd in c.verdicts)
                if (vd.verdict == key && (v != Verdict.Return || string.IsNullOrEmpty(vd.to) || vd.to == to))
                    return vd;
            return null;
        }

        public static VerdictDef Best(CaseDef c) => c.verdicts.FirstOrDefault(v => v.grade == "best");

        /// <summary>Cases that run on a day given the state at the start of that case.</summary>
        public static IEnumerable<CaseDef> ActiveCases(DayDef day, StoryState state)
        {
            foreach (var c in day.cases)
            {
                if (!string.IsNullOrEmpty(c.altOf)) continue; // alternates are chosen through their base case
                var alt = day.cases.FirstOrDefault(a => a.altOf == c.id && state.Check(a.condition));
                if (alt != null) { yield return alt; continue; }
                if (state.Check(c.condition)) yield return c;
            }
        }

        public static void ApplyVerdict(ContentDb db, CaseDef c, VerdictDef vd, string to, StoryState state)
        {
            var item = db.Object(c.wants);
            if (item != null && vd != null)
            {
                if (vd.verdict == "return") state.SetObjectLocation(item.id, "returned:" + to);
                else if (vd.verdict == "seal") state.SetObjectLocation(item.id, "sealed");
            }
            if (vd != null)
                foreach (var s in vd.set) state.Apply(s);
        }

        // ------------------------------------------------------------------ validation

        /// <summary>
        /// Plays the whole week with the solver's verdicts and checks every case. Returns a list of
        /// problems (empty = every case is solvable from on-desk information and consistent).
        /// </summary>
        public static List<string> Validate(ContentDb db, List<string> log = null)
        {
            var issues = new List<string>();
            var state = new StoryState();
            foreach (var o in db.root.objects)
            {
                if (string.IsNullOrEmpty(o.name)) issues.Add($"object {o.id}: no name");
                if (o.details.Length == 0) issues.Add($"object {o.id}: no details");
                if (!o.details.Any(d => d.kind == "secret")) issues.Add($"object {o.id}: no secret detail");
                foreach (var d in o.details)
                {
                    if (string.IsNullOrEmpty(d.fact)) issues.Add($"object {o.id}.{d.id}: no fact");
                    if (d.kind != "secret" && string.IsNullOrEmpty(d.question)) issues.Add($"object {o.id}.{d.id}: no question");
                }
                if (o.trait == "hum" && string.IsNullOrEmpty(o.owner)) issues.Add($"object {o.id}: hums but has no owner");
            }

            foreach (var day in db.root.days)
            {
                foreach (var c in ActiveCases(day, state).ToList())
                {
                    string where = $"day {day.day} case {c.id}";
                    foreach (var who in c.claimants)
                        if (db.Commuter(who) == null) issues.Add($"{where}: unknown claimant {who}");
                    var item = db.Object(c.wants);
                    if (!string.IsNullOrEmpty(c.wants) && item == null) issues.Add($"{where}: unknown object {c.wants}");
                    if (c.intro.Length == 0) issues.Add($"{where}: no intro lines");

                    var rules = db.RulesKnownAt(day.day, c.id);
                    foreach (var cl in c.claims) CheckRef(issues, where, item, cl.checks, rules, day.day, "claim " + cl.key, cl.truthful);
                    foreach (var a in c.answers)
                    {
                        if (item?.Detail(a.detail) == null) issues.Add($"{where}: answer about unknown detail {a.detail}");
                        else CheckRef(issues, where, item, "detail:" + a.detail, rules, day.day, "answer " + a.detail, a.truthful);
                        if (c.claimants.Length > 1 && string.IsNullOrEmpty(a.who)) issues.Add($"{where}: answer {a.detail} needs 'who' in a two-claimant case");
                    }

                    var best = c.verdicts.Where(v => v.grade == "best").ToList();
                    if (best.Count != 1) { issues.Add($"{where}: needs exactly one best verdict (has {best.Count})"); continue; }

                    bool present = item != null && state.InStorage(item, day.day);
                    var decision = Solve(db, day.day, c, state);
                    if (decision.reason.StartsWith("AMBIGUOUS")) issues.Add($"{where}: {decision.reason}");
                    log?.Add($"{where}: solver -> {decision}");

                    if (item != null && !present)
                    {
                        if (c.missing.Length == 0) issues.Add($"{where}: item absent on the solver path but no 'missing' lines");
                        state.SetRecord(new CaseRecord { caseId = c.id, verdict = "missing", grade = "skip" });
                        continue;
                    }

                    var bv = best[0];
                    bool match = VerdictNames.Parse(bv.verdict) == decision.verdict &&
                                 (decision.verdict != Verdict.Return || bv.to == decision.to || (string.IsNullOrEmpty(bv.to) && c.claimants.Length == 1 && c.claimants[0] == decision.to));
                    if (!match) issues.Add($"{where}: authored best '{bv.verdict}{(string.IsNullOrEmpty(bv.to) ? "" : "->" + bv.to)}' but rules derive {decision}");

                    // every action the player can take must have a response
                    foreach (var who in c.claimants)
                        if (item != null && FindVerdict(c, Verdict.Return, who) == null) issues.Add($"{where}: no verdict for return->{who}");
                    if (FindVerdict(c, Verdict.Refuse, null) == null) issues.Add($"{where}: no refuse verdict");
                    if (item != null && FindVerdict(c, Verdict.Seal, null) == null) issues.Add($"{where}: no seal verdict");

                    var vd = FindVerdict(c, decision.verdict, decision.to) ?? bv;
                    ApplyVerdict(db, c, vd, decision.to, state);
                    state.SetRecord(new CaseRecord { caseId = c.id, verdict = vd.verdict, to = decision.to, grade = vd.grade });
                }
            }
            return issues;
        }

        static void CheckRef(List<string> issues, string where, ObjectDef item, string check, HashSet<int> rules, int day, string what, bool truthful)
        {
            if (string.IsNullOrEmpty(check) || check == "visible")
            {
                if (!truthful) issues.Add($"{where}: {what} is a lie but checks nothing the player can verify");
                return;
            }
            if (check.StartsWith("tag.")) return;
            if (check.StartsWith("detail:"))
            {
                var d = item?.Detail(check.Substring(7));
                if (d == null) { issues.Add($"{where}: {what} checks unknown {check}"); return; }
                if (d.kind == "secret" && !truthful) issues.Add($"{where}: {what} relies on a secret detail");
                if (d.requires == "uv" && !rules.Contains(LampRule)) issues.Add($"{where}: {what} needs the blue lamp before it is unlocked");
                return;
            }
            issues.Add($"{where}: {what} has unknown check '{check}'");
        }
    }
}
