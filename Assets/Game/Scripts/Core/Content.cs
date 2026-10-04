using System;
using System.Collections.Generic;
using System.Linq;

namespace LostAndFound
{
    // All game content lives in Resources/Content/*.json and is loaded into these plain classes with
    // JsonUtility (arrays only, no dictionaries). Nothing here touches UnityEngine except loading.

    [Serializable]
    public class ContentRoot
    {
        public ObjectDef[] objects = Array.Empty<ObjectDef>();
        public CommuterDef[] commuters = Array.Empty<CommuterDef>();
        public DayDef[] days = Array.Empty<DayDef>();
        public RuleDef[] rules = Array.Empty<RuleDef>();
        public EndingDef[] endings = Array.Empty<EndingDef>();
    }

    [Serializable]
    public class ObjectDef
    {
        public string id;
        public string name;
        public string model;           // FBX name under Resources/Models/Objects (defaults to id)
        public string storage;         // drawer letter A-F, shelf slot (Shelf_1a...), "desk", or "presented" (brought by a claimant)
        public int arrives = 1;        // day the object first appears in storage
        public string trait = "";      // "", hum, frost, tomorrow
        public string owner = "";      // commuter id the object belongs to (hum target)
        public string sound = "wood";  // material family for foley: leather, metal, paper, cloth, wood, glass, ceramic
        public float inspectScale = 0; // optional override of the inspect normalisation
        public TagDef tag = new TagDef();
        public DetailDef[] details = Array.Empty<DetailDef>();
        public PartDef[] parts = Array.Empty<PartDef>();

        public DetailDef Detail(string detailId) => details.FirstOrDefault(d => d.id == detailId);
        public IEnumerable<DetailDef> CaseDetails => details.Where(d => d.kind != "secret");
        public string ModelName => string.IsNullOrEmpty(model) ? id : model;
    }

    [Serializable]
    public class TagDef
    {
        public string where = "";
        public string when = "";
        public string train = "";
        public string note = "";
    }

    [Serializable]
    public class DetailDef
    {
        public string id;
        public string kind = "case";   // case | secret
        public string node;            // hotspot empty in the model (defaults to HS_<id>)
        public string requires = "";   // "", uv (blue lamp), listen (hold to ear), wind, shake
        public string fact;            // what the player learns (written on the slip)
        public string question;        // the neutral question used when asking a claimant
        public string altIf = "";      // condition under which altFact replaces fact (e.g. "ring=thomas")
        public string altFact = "";
        public string altTex = "";     // tex_<altTex> materials swap to <altTex>_alt under the same condition
        public string NodeName => string.IsNullOrEmpty(node) ? "HS_" + id : node;
    }

    [Serializable]
    public class PartDef
    {
        public string node;            // child object in the model ("" = the whole object, for actions)
        public string kind = "hinge";  // hinge | slide | spin | shake | listen | play
        public string axis = "x";      // local axis
        public float amount = 100f;    // degrees for hinge/spin, metres for slide
        public string sound = "";      // latch, hinge, zip, paper, wind, snow, sea...
        public string reveals = "";    // detail id discovered by operating this part
        public string label = "";      // hint shown on hover ("Open the clasp")
    }

    [Serializable]
    public class CommuterDef
    {
        public string id;
        public string name;
        public string title;           // "Baker", "Constable"...
        public string model;           // FBX under Resources/Models/People (defaults to id)
        public float pitch = 1f;       // voice pitch multiplier
        public float speed = 1f;       // voice syllable rate multiplier
        public string voice = "mid";   // voice bank: low, mid, high, child, old
        public bool cold;              // has "gone on ahead": window frosts
        public bool grey;              // the Grey Gentleman
        public bool sepia;             // a stray from another time
        public float height = 1f;      // scale
        public string shrug = "";      // fallback answer when asked about something they don't know
        public string ModelName => string.IsNullOrEmpty(model) ? id : model;
    }

    [Serializable]
    public class RuleDef
    {
        public int id;
        public string text;            // Agnes's wording
        public string hint;            // one-line plain-language summary for the rules card
    }

    [Serializable]
    public class DayDef
    {
        public int day;
        public string weekday;         // Mon...
        public string date;            // "Monday 15 October 1962"
        public string title;
        public int[] rules = Array.Empty<int>();          // rules unlocked this morning
        public LineDef[] morning = Array.Empty<LineDef>(); // Gus / Agnes notes at the start of the day
        public CaseDef[] cases = Array.Empty<CaseDef>();
        public GazetteDef[] gazette = Array.Empty<GazetteDef>();
        public LineDef[] evening = Array.Empty<LineDef>();
    }

    [Serializable]
    public class CaseDef
    {
        public string id;
        public string[] claimants = Array.Empty<string>();
        public string[] companions = Array.Empty<string>(); // stand at the window but make no claim
        public string[] presents = Array.Empty<string>();   // documents the claimant puts on the counter
        public string wants = "";      // object id the case is about ("" = asks for something not in storage)
        public string condition = "";  // story condition for the case to run at all
        public string altOf = "";      // this case replaces another case id when its condition holds
        public bool guided;            // tutorial highlights
        public string unlockRule = ""; // rule id revealed when this case starts (as an Agnes note)
        public string hint = "";       // a gentle tip shown when the investigation starts
        public LineDef[] intro = Array.Empty<LineDef>();
        public ClaimDef[] claims = Array.Empty<ClaimDef>();
        public AnswerDef[] answers = Array.Empty<AnswerDef>();
        public VerdictDef[] verdicts = Array.Empty<VerdictDef>();
        public LineDef[] notMine = Array.Empty<LineDef>();   // when handed the wrong item
        public LineDef[] missing = Array.Empty<LineDef>();   // when the item is gone (earlier choice)
        public string missingLedger = "";
    }

    [Serializable]
    public class LineDef
    {
        public string who = "";        // commuter id, "agnes" (note), "gus", "you"
        public string text = "";
        public string emote = "";      // happy, sad, angry, surprised, sly, shy
        public string condition = "";  // story condition for the line to be said at all
    }

    [Serializable]
    public class ClaimDef
    {
        public string who = "";        // which claimant said it (two-claimant cases)
        public string key = "";
        public string text = "";       // what goes on the slip
        public string checks = "";     // tag.where | tag.when | tag.train | detail:<id> | visible
        public bool truthful = true;
    }

    [Serializable]
    public class AnswerDef
    {
        public string who = "";
        public string detail = "";
        public string text = "";
        public bool truthful = true;
    }

    [Serializable]
    public class VerdictDef
    {
        public string verdict;         // return | refuse | seal
        public string to = "";         // claimant id for return
        public string grade = "wrong"; // best | ok | wrong
        public LineDef[] reply = Array.Empty<LineDef>();
        public string ledger = "";     // the Day Ledger's explanation line
        public string[] set = Array.Empty<string>(); // story flags: "key=value" or "key+=1"
    }

    [Serializable]
    public class GazetteDef
    {
        public string condition = "";
        public string headline;
        public string body;
    }

    [Serializable]
    public class EndingDef
    {
        public string id;
        public string condition;
        public string title;
        public string photo;
        public string[] lines = Array.Empty<string>();
    }

    public enum Verdict { Return, Refuse, Seal }

    public static class VerdictNames
    {
        public static Verdict Parse(string s) => s switch
        {
            "return" => Verdict.Return,
            "seal" => Verdict.Seal,
            _ => Verdict.Refuse,
        };

        public static string Key(Verdict v) => v switch
        {
            Verdict.Return => "return",
            Verdict.Seal => "seal",
            _ => "refuse",
        };
    }

    /// <summary>Indexed view over the loaded content.</summary>
    public class ContentDb
    {
        public readonly ContentRoot root;
        readonly Dictionary<string, ObjectDef> objects = new();
        readonly Dictionary<string, CommuterDef> commuters = new();

        public ContentDb(ContentRoot root)
        {
            this.root = root;
            foreach (var o in root.objects) objects[o.id] = o;
            foreach (var c in root.commuters) commuters[c.id] = c;
        }

        public ObjectDef Object(string id) => id != null && objects.TryGetValue(id, out var o) ? o : null;
        public CommuterDef Commuter(string id) => id != null && commuters.TryGetValue(id, out var c) ? c : null;
        public DayDef Day(int day) => root.days.FirstOrDefault(d => d.day == day);
        public RuleDef Rule(int id) => root.rules.FirstOrDefault(r => r.id == id);
        public int DayCount => root.days.Length;

        public static ContentRoot Merge(params ContentRoot[] parts)
        {
            var r = new ContentRoot();
            r.objects = parts.SelectMany(p => p.objects ?? Array.Empty<ObjectDef>()).ToArray();
            r.commuters = parts.SelectMany(p => p.commuters ?? Array.Empty<CommuterDef>()).ToArray();
            r.days = parts.SelectMany(p => p.days ?? Array.Empty<DayDef>()).OrderBy(d => d.day).ToArray();
            r.rules = parts.SelectMany(p => p.rules ?? Array.Empty<RuleDef>()).OrderBy(x => x.id).ToArray();
            r.endings = parts.SelectMany(p => p.endings ?? Array.Empty<EndingDef>()).ToArray();
            return r;
        }

        /// <summary>Rules known by the start of a case (morning rules of every day so far plus earlier case unlocks).</summary>
        public HashSet<int> RulesKnownAt(int day, string caseId)
        {
            var known = new HashSet<int>();
            foreach (var d in root.days.Where(d => d.day <= day))
            {
                foreach (var r in d.rules) known.Add(r);
                foreach (var c in d.cases)
                {
                    if (!string.IsNullOrEmpty(c.unlockRule) && int.TryParse(c.unlockRule, out int rid)) known.Add(rid);
                    if (d.day == day && c.id == caseId) break;
                }
            }
            return known;
        }
    }
}
