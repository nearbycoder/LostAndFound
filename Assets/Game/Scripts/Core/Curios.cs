using System.Collections.Generic;
using System.Linq;

namespace LostAndFound
{
    /// <summary>The week's 25 curiosities: one optional secret detail per stored object, found or not.</summary>
    public static class Curios
    {
        public struct Entry
        {
            public ObjectDef obj;
            public DetailDef secret;
            public bool found;
        }

        /// <summary>Every object a claimant doesn't bring in, in the order they turn up, with its secret.</summary>
        public static List<Entry> All(ContentDb db, ICollection<string> discovered)
        {
            var found = new HashSet<string>(discovered);
            return db.root.objects
                .Where(o => o.storage != "presented")
                .Select((o, i) => (o, i))
                .OrderBy(x => x.o.arrives).ThenBy(x => x.i)
                .Select(x =>
                {
                    var secret = x.o.details.FirstOrDefault(d => d.kind == "secret");
                    return new Entry { obj = x.o, secret = secret, found = secret != null && found.Contains(x.o.id + "." + secret.id) };
                })
                .ToList();
        }

        public static (int found, int total) Count(ContentDb db, ICollection<string> discovered)
        {
            var all = All(db, discovered);
            return (all.Count(e => e.found), all.Count);
        }
    }
}
