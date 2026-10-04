using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LostAndFound
{
    [Serializable]
    public class KV
    {
        public string k;
        public string v;
        public KV() { }
        public KV(string k, string v) { this.k = k; this.v = v; }
    }

    [Serializable]
    public class CaseRecord
    {
        public string caseId;
        public string verdict;     // return | refuse | seal | missing
        public string to;          // claimant for return
        public string grade;       // best | ok | wrong | skip
        public string ledger;
        public int detailsFound;
        public int detailsTotal;
    }

    /// <summary>
    /// Everything that a week remembers: story flags, where every object is, and the verdict record
    /// per day. Pure data; serialised into the save file. Days are replayable, so a snapshot is kept
    /// at the start of each day.
    /// </summary>
    [Serializable]
    public class StoryState
    {
        public List<KV> flags = new();
        public List<KV> objects = new();   // object id -> storage | returned:<who> | sealed | given:<who>
        public List<CaseRecord> records = new();

        public StoryState Clone()
        {
            return new StoryState
            {
                flags = flags.Select(f => new KV(f.k, f.v)).ToList(),
                objects = objects.Select(f => new KV(f.k, f.v)).ToList(),
                records = records.Select(r => new CaseRecord
                {
                    caseId = r.caseId, verdict = r.verdict, to = r.to, grade = r.grade, ledger = r.ledger,
                    detailsFound = r.detailsFound, detailsTotal = r.detailsTotal
                }).ToList(),
            };
        }

        // ------------------------------------------------------------------ flags

        public string Get(string key) => flags.FirstOrDefault(f => f.k == key)?.v;

        public int GetInt(string key) => int.TryParse(Get(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0;

        public void Set(string key, string value)
        {
            var f = flags.FirstOrDefault(x => x.k == key);
            if (f == null) flags.Add(new KV(key, value));
            else f.v = value;
        }

        /// <summary>Apply "key=value", "key+=n" or "key" (sets "1").</summary>
        public void Apply(string expr)
        {
            if (string.IsNullOrWhiteSpace(expr)) return;
            expr = expr.Trim();
            int plus = expr.IndexOf("+=", StringComparison.Ordinal);
            if (plus > 0)
            {
                string key = expr.Substring(0, plus).Trim();
                int add = int.Parse(expr.Substring(plus + 2).Trim(), CultureInfo.InvariantCulture);
                Set(key, (GetInt(key) + add).ToString(CultureInfo.InvariantCulture));
                return;
            }
            int eq = expr.IndexOf('=');
            if (eq > 0) Set(expr.Substring(0, eq).Trim(), expr.Substring(eq + 1).Trim());
            else Set(expr, "1");
        }

        /// <summary>
        /// Comma-separated AND of terms: key=value, key!=value, key>=n, key&lt;n, key (truthy), !key.
        /// "obj:ring@storage" tests an object's location. Empty condition is true.
        /// </summary>
        public bool Check(string condition)
        {
            if (string.IsNullOrWhiteSpace(condition)) return true;
            foreach (var raw in condition.Split(','))
            {
                string term = raw.Trim();
                if (term.Length == 0) continue;
                if (!CheckTerm(term)) return false;
            }
            return true;
        }

        bool CheckTerm(string term)
        {
            if (term.StartsWith("obj:"))
            {
                // obj:<id>@<location prefix>, or obj:<id>!@<prefix>
                string body = term.Substring(4);
                bool neg = body.Contains("!@");
                string[] parts = body.Split(new[] { neg ? "!@" : "@" }, StringSplitOptions.None);
                string loc = ObjectLocation(parts[0]) ?? "";
                bool match = loc.StartsWith(parts[1], StringComparison.Ordinal);
                return neg ? !match : match;
            }
            if (term.StartsWith("!")) return !Truthy(Get(term.Substring(1)));
            foreach (var op in new[] { "!=", ">=", "<=", "=", ">", "<" })
            {
                int i = term.IndexOf(op, StringComparison.Ordinal);
                if (i <= 0) continue;
                string key = term.Substring(0, i).Trim();
                string val = term.Substring(i + op.Length).Trim();
                string cur = Get(key) ?? "";
                switch (op)
                {
                    case "=": return cur == val;
                    case "!=": return cur != val;
                    default:
                        int a = GetInt(key), b = int.Parse(val, CultureInfo.InvariantCulture);
                        return op switch { ">=" => a >= b, "<=" => a <= b, ">" => a > b, _ => a < b };
                }
            }
            return Truthy(Get(term));
        }

        static bool Truthy(string v) => !string.IsNullOrEmpty(v) && v != "0" && v != "false";

        // ------------------------------------------------------------------ objects

        public string ObjectLocation(string id) => objects.FirstOrDefault(o => o.k == id)?.v;

        public void SetObjectLocation(string id, string location)
        {
            var o = objects.FirstOrDefault(x => x.k == id);
            if (o == null) objects.Add(new KV(id, location));
            else o.v = location;
        }

        /// <summary>True when the object has arrived by `day` and is still in storage.</summary>
        public bool InStorage(ObjectDef def, int day)
        {
            if (def.arrives > day) return false;
            string loc = ObjectLocation(def.id);
            return loc == null || loc == "storage";
        }

        public CaseRecord Record(string caseId) => records.FirstOrDefault(r => r.caseId == caseId);

        public void SetRecord(CaseRecord rec)
        {
            records.RemoveAll(r => r.caseId == rec.caseId);
            records.Add(rec);
        }
    }
}
