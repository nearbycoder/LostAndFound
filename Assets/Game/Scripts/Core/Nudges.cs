using System;
using System.Collections.Generic;
using System.Linq;

namespace LostAndFound
{
    public enum NudgeStage { Find, Examine, Ask, Decide }

    /// <summary>What the desk lights up along with a nudge ("show me").</summary>
    public enum NudgePoint { None, Storage, Detail }

    public class Nudge
    {
        public NudgeStage stage;
        public string text;
        public NudgePoint point;
        public string detailId;    // the detail an Examine or Ask nudge is about
        public int rule;           // the rule a Decide nudge cites (0 = none)
        public override string ToString() => $"{stage}: {text}";
    }

    /// <summary>How far the player has got with the current claim.</summary>
    public class NudgeProgress
    {
        public bool itemSeen;                          // picked up (or put on the tray) during this claim
        public HashSet<string> found = new();          // detail ids of the claimed object found so far (any day)
        public HashSet<string> asked = new();          // detail ids put to the claimants during this claim
        public bool pad;                               // name gamepad buttons rather than keys
    }

    /// <summary>
    /// Agnes's nudges for a stuck player. For wherever the player is in a claim (finding the object, examining it,
    /// asking, deciding) there's a short list that goes from a gentle reminder to pointing at the thing. They're
    /// built from the same on-desk information as <see cref="Rules.Solve"/>, never cite a rule Agnes hasn't handed
    /// over yet, never quote a finding the player hasn't found, and never say which stamp to use.
    /// </summary>
    public static class Nudges
    {
        public static List<Nudge> For(ContentDb db, int day, CaseDef c, StoryState state, NudgeProgress p)
        {
            var item = db.Object(c.wants);
            var rules = db.RulesKnownAt(day, c.id);
            if (item == null) return NotHere(db, c, p);
            if (!p.itemSeen) return Find(item, p);
            var missing = Deciding(db, day, c).Where(d => !p.found.Contains(d.id)).ToList();
            if (missing.Count > 0) return Examine(item, missing, p);
            var ask = ToAsk(c, p);
            if (ask.Count > 0) return Ask(db, c, item, ask, state, p);
            return Decide(db, day, c, item, rules, state, p);
        }

        // ------------------------------------------------------------------ what decides a claim

        /// <summary>The claimed object's details that bear on this claim: the ones that expose a lie first, then what the
        /// claimants say is on it (and anything dated, on something from tomorrow). None when a trait settles it under a
        /// rule already known (it hums, it's frosted, the Grey Gentleman).</summary>
        public static List<DetailDef> Deciding(ContentDb db, int day, CaseDef c)
        {
            var item = db.Object(c.wants);
            if (item == null) return new List<DetailDef>();
            var rules = db.RulesKnownAt(day, c.id);
            bool traitDecides = (item.trait == "hum" && rules.Contains(Rules.HumRule))
                || (item.trait == "frost" && rules.Contains(Rules.FrostRule))
                || (rules.Contains(Rules.GreyRule) && c.claimants.All(w => db.Commuter(w)?.grey == true));
            if (traitDecides) return new List<DetailDef>();

            var lies = new List<string>();
            var rest = new List<string>();
            foreach (var cl in c.claims)
                if (cl.checks != null && cl.checks.StartsWith("detail:")) (cl.truthful ? rest : lies).Add(cl.checks.Substring(7));
            foreach (var a in c.answers) if (!a.truthful) lies.Add(a.detail);
            // something from tomorrow gives itself away by a date somewhere on it
            if (item.trait == "tomorrow") rest.AddRange(item.CaseDetails.Select(d => d.id));
            return lies.Concat(rest).Distinct()
                .Select(item.Detail)
                .Where(d => d != null && d.kind != "secret" && (d.requires != "uv" || rules.Contains(Rules.LampRule)))
                .ToList();
        }

        /// <summary>Lies only asking can expose: a claimant whose claims all hold up but who will answer wrongly
        /// about something the player has found and not yet asked about.</summary>
        static List<string> ToAsk(CaseDef c, NudgeProgress p)
        {
            var ask = new List<string>();
            bool single = c.claimants.Length == 1;
            foreach (var who in c.claimants)
            {
                bool Mine(string w) => w == who || (single && string.IsNullOrEmpty(w));
                if (c.claims.Any(cl => !cl.truthful && Mine(cl.who))) continue;   // the slip already shows it
                var lying = c.answers.Where(a => !a.truthful && Mine(a.who)).Select(a => a.detail).ToList();
                if (lying.Count == 0 || lying.Any(p.asked.Contains)) continue;
                ask.AddRange(lying.Where(d => p.found.Contains(d)));
            }
            return ask.Distinct().ToList();
        }

        // ------------------------------------------------------------------ the stages

        static List<Nudge> Find(ObjectDef item, NudgeProgress p)
        {
            string name = The(item);
            var n = new List<Nudge>
            {
                new() { stage = NudgeStage.Find, text = p.pad
                    ? "Read what they've described on the claim slip, then point at things in the drawers and on the shelf. Every stray's tag says where and when it was found."
                    : "Read what they've described on the claim slip, then hover over things in the drawers and on the shelf. Every stray's tag says where and when it was found." },
            };
            string s = item.storage ?? "";
            string area, exact;
            if (s.Length == 1)
            {
                area = p.pad ? "Try the drawers on your left (LB)." : "Try the drawers on your left (A or the left arrow key).";
                exact = $"Drawer {s}: {name}. Open the drawer and pick it up.";
            }
            else if (s.StartsWith("Stand"))
            {
                area = p.pad ? "Try the umbrella stand, beside the shelves on your right (RB)." : "Try the umbrella stand, beside the shelves on your right (D or the right arrow key).";
                exact = $"In the umbrella stand: {name}. Pick it up.";
            }
            else if (s.StartsWith("Shelf"))
            {
                area = p.pad ? "Try the shelves on your right (RB)." : "Try the shelves on your right (D or the right arrow key).";
                exact = $"On the shelves: {name}. Pick it up.";
            }
            else
            {
                area = "Look on the desk in front of you.";
                exact = $"On the desk: {name}. Pick it up.";
            }
            n.Add(new Nudge { stage = NudgeStage.Find, text = area });
            n.Add(new Nudge { stage = NudgeStage.Find, text = exact, point = NudgePoint.Storage });
            return n;
        }

        static List<Nudge> Examine(ObjectDef item, List<DetailDef> missing, NudgeProgress p)
        {
            var d = missing[0];
            string name = The(item);
            string more = missing.Count == 1
                ? "There's something on it that bears on this claim, and you haven't found it yet."
                : $"There are {Words(missing.Count)} things on it that bear on this claim, and you haven't found them yet.";
            var reveal = item.parts.FirstOrDefault(x => x.reveals == d.id);
            bool opens = item.parts.Any(x => x.kind is "hinge" or "slide");
            string how, show;
            if (reveal != null)
            {
                how = Sentence(string.IsNullOrEmpty(reveal.label) ? PartVerb(reveal.kind) : reveal.label) + " Something happens when you do.";
                show = string.IsNullOrEmpty(reveal.node) ? "Click anywhere on it to do that." : "Hover over it until that part lights up, then click it.";
            }
            else if (d.requires == "uv")
            {
                how = p.pad ? "Hold it under my blue lamp (d-pad up). It shows what ink tries to hide." : "Hold it under my blue lamp (L). It shows what ink tries to hide.";
                show = "With the blue lamp on, turn it slowly and watch for the glint. Click there.";
            }
            else if (opens)
            {
                how = "Open it up and look inside, as well as all round the outside.";
                show = "Open it, then turn it slowly and watch for the glint. Click there.";
            }
            else
            {
                how = p.pad ? "Turn it right round with the right stick, and lean in close with the triggers." : "Turn it right round, and lean in close (scroll).";
                show = "Turn it slowly and watch for the glint. Click there.";
            }
            return new List<Nudge>
            {
                new() { stage = NudgeStage.Examine, detailId = d.id, text = $"Look harder at {name}. {more}" },
                new() { stage = NudgeStage.Examine, detailId = d.id, text = how },
                new() { stage = NudgeStage.Examine, detailId = d.id, text = show, point = NudgePoint.Detail },
            };
        }

        static List<Nudge> Ask(ContentDb db, CaseDef c, ObjectDef item, List<string> ask, StoryState state, NudgeProgress p)
        {
            var d = item.Detail(ask[0]);
            string them = c.claimants.Length == 1 ? Who(db, c.claimants[0]) : "them";
            return new List<Nudge>
            {
                new() { stage = NudgeStage.Ask, detailId = d.id, text = p.pad
                    ? $"You can put what you've found to {them}. Bring up the claim slip (View) and press A on a finding to ask about it."
                    : $"You can put what you've found to {them}. Open the claim slip (Tab) and click a finding to ask about it." },
                new() { stage = NudgeStage.Ask, detailId = d.id, text = $"Ask {them} about this one: {Quote(Fact(d, state))}" },
            };
        }

        static List<Nudge> Decide(ContentDb db, int day, CaseDef c, ObjectDef item, HashSet<int> rules, StoryState state, NudgeProgress p)
        {
            string name = The(item);
            bool two = c.claimants.Length > 1;
            int rule;
            var specific = new List<string>();
            var grey = c.claimants.Where(w => db.Commuter(w)?.grey == true && rules.Contains(Rules.GreyRule)).ToList();

            if (item.trait == "tomorrow" && rules.Contains(Rules.TomorrowRule))
            {
                rule = Rules.TomorrowRule;
                specific.Add($"Look for a date, on {name} and on its tag, and then at the calendar. Today is {db.Day(day)?.date}.");
            }
            else if (grey.Count > 0)
            {
                rule = Rules.GreyRule;
                specific.Add("Look who's at the window. Rule five has no exceptions, however good the story.");
            }
            else if (item.trait == "frost" && rules.Contains(Rules.FrostRule))
            {
                rule = Rules.FrostRule;
                specific.Add(two ? "Which of them frosted the glass when they came to the window?"
                                 : $"Did the glass frost over when {Who(db, c.claimants[0])} came to the window?");
            }
            else if (item.trait == "hum" && rules.Contains(Rules.HumRule))
            {
                rule = Rules.HumRule;
                specific.Add(two ? $"Listen as each of them speaks. Which of them does {name} hum for?"
                                 : $"Listen while {Who(db, c.claimants[0])} speaks. Does {name} hum for them?");
            }
            else
            {
                bool tag = false, detail = false;
                foreach (var who in c.claimants)
                {
                    bool Mine(string w) => w == who || (!two && string.IsNullOrEmpty(w));
                    var claim = c.claims.FirstOrDefault(cl => !cl.truthful && Mine(cl.who));
                    var answer = c.answers.FirstOrDefault(a => !a.truthful && Mine(a.who));
                    if (claim != null && claim.checks.StartsWith("tag."))
                    {
                        tag = true;
                        specific.Add($"Compare what {Who(db, who)} told you, {Quote(claim.text)}, with {TagField(claim.checks)} on the tag.");
                    }
                    else if (claim != null)
                    {
                        detail = true;
                        specific.Add($"Compare {Who(db, who)}'s claim, {Quote(claim.text)}, with what you found on {name}.");
                    }
                    else if (answer != null)
                    {
                        detail = true;
                        var d = item.Detail(answer.detail);
                        specific.Add($"Think back to what {Who(db, who)} said when you asked about this: {Quote(Fact(d, state))}");
                    }
                }
                if (specific.Count == 0)
                    specific.Add(two ? $"Everything both of them have told you agrees with the tag and with {name}. Look again at what each of them claims."
                                     : $"Everything {Who(db, c.claimants[0])} has told you agrees with the tag and with {name}.");
                rule = two && rules.Contains(Rules.TwoRule) ? Rules.TwoRule
                     : detail && rules.Contains(Rules.HiddenRule) ? Rules.HiddenRule
                     : Rules.TagRule;
                if (tag && !detail) rule = Rules.TagRule;
            }

            var n = new List<Nudge> { RuleNudge(db, rule) };
            n.AddRange(specific.Select(s => new Nudge { stage = NudgeStage.Decide, text = s, rule = rule }));
            n.Add(Stamping(p));
            return n;
        }

        static List<Nudge> NotHere(ContentDb db, CaseDef c, NudgeProgress p) => new()
        {
            new() { stage = NudgeStage.Find, text = p.pad
                ? "Search the drawers and the shelves for anything like what's on the claim slip, and point at things to read their tags."
                : "Search the drawers and the shelves for anything like what's on the claim slip, and hover over things to read their tags." },
            new() { stage = NudgeStage.Decide, rule = Rules.TagRule, text = "Every stray in storage has a tag. If nothing matches what they lost, where and when, it was never handed in here." },
            new() { stage = NudgeStage.Decide, text = "If it isn't here, you can't give it back." },
            Stamping(p),
        };

        static Nudge RuleNudge(ContentDb db, int rule)
        {
            var r = db.Rule(rule);
            return new Nudge { stage = NudgeStage.Decide, rule = rule, text = $"Remember rule {Words(rule)}: {r?.text}" };
        }

        static Nudge Stamping(NudgeProgress p) => new()
        {
            stage = NudgeStage.Decide,
            text = p.pad
                ? "To give it back or seal it away, put it on the counter tray first (X), then stamp the claim slip. A refusal needs nothing on the tray."
                : "To give it back or seal it away, put it on the counter tray first (T), then stamp the claim slip. A refusal needs nothing on the tray.",
        };

        // ------------------------------------------------------------------ words

        /// <summary>"the silver locket", but "the Iron Drawer key".</summary>
        public static string The(ObjectDef o)
        {
            string n = o?.name ?? "it";
            var w = n.Split(' ');
            bool proper = w.Length > 1 && w[1].Length > 0 && char.IsUpper(w[1][0]);
            if (n.StartsWith("Mr ") || n.StartsWith("Mrs ")) return n;
            return "the " + (proper ? n : char.ToLowerInvariant(n[0]) + n.Substring(1));
        }

        static string Who(ContentDb db, string id) => db.Commuter(id)?.ShortName ?? id;

        static string Fact(DetailDef d, StoryState state) =>
            d == null ? "" : !string.IsNullOrEmpty(d.altIf) && state.Check(d.altIf) && !string.IsNullOrEmpty(d.altFact) ? d.altFact : d.fact;

        /// <summary>In double quotes, or single ones if it already has double quotes inside.</summary>
        static string Quote(string s) => s.Contains('"') ? $"'{s}'" : $"\"{s}\"";

        static string TagField(string check) => check switch
        {
            "tag.where" => "where it was found",
            "tag.when" => "when it was found",
            "tag.train" => "the train",
            _ => "what's written",
        };

        static string PartVerb(string kind) => kind switch
        {
            "shake" => "Give it a shake",
            "listen" => "Hold it to your ear",
            "spin" => "Wind it",
            "play" => "Play it",
            _ => "Open it",
        };

        static string Sentence(string s) => string.IsNullOrEmpty(s) ? "" : Cap(s.TrimEnd('.')) + ".";
        static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        static string Words(int n) => n switch
        {
            1 => "one", 2 => "two", 3 => "three", 4 => "four", 5 => "five", 6 => "six", 7 => "seven", 8 => "eight", _ => n.ToString(),
        };
    }
}
