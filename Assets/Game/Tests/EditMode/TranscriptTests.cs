using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace LostAndFound.Tests
{
    /// <summary>"What they said" beside the slip: how the transcript reads, how a long claim is trimmed, and that every
    /// line in the content has a speaker the card can name (two claimants never share a name).</summary>
    public class TranscriptTests
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

        static Transcript Sample()
        {
            var t = new Transcript();
            t.Add("Walter", "I've lost my wallet.");
            t.Add("You", "Is there anything inside it?", true);
            t.Add("Walter", "A photo of Biscuit.");
            t.Add("You", "Where did you lose it?", true);
            t.Add("Walter", "On the Harwick stopper.");
            return t;
        }

        [Test]
        public void ReadsInOrderWithSpeakers()
        {
            var t = Sample();
            Assert.AreEqual(5, t.Count);
            var lines = t.Format().Split('\n');
            Assert.AreEqual(5, lines.Length);
            StringAssert.StartsWith("<b>Walter:</b> I've lost my wallet.", lines[0]);
            StringAssert.Contains("<i>You: Is there anything inside it?</i>", lines[1]);
            StringAssert.Contains("A photo of Biscuit.", lines[2]);
        }

        [Test]
        public void LongClaimKeepsTheOpeningAndTheNewest()
        {
            var t = Sample();
            var lines = t.Format(2).Split('\n');
            Assert.AreEqual(4, lines.Length, "opening, the ellipsis, then the last two lines");
            StringAssert.Contains("I've lost my wallet.", lines[0]);
            StringAssert.Contains("…", lines[1]);
            StringAssert.Contains("Where did you lose it?", lines[2]);
            StringAssert.Contains("Harwick", lines[3]);
            Assert.IsFalse(t.Format(2).Contains("Biscuit"));
        }

        [Test]
        public void EveryChangeBumpsTheVersion()
        {
            var t = new Transcript();
            int v = t.Version;
            t.Add("Walter", "Hello.");
            Assert.Greater(t.Version, v);
            v = t.Version;
            t.Add("Walter", "");   // nothing said: nothing added
            Assert.AreEqual(v, t.Version);
            t.Clear();
            Assert.AreEqual(0, t.Count);
            Assert.Greater(t.Version, v);
        }

        [Test]
        public void QuestionsNameOtherObjectsProperly()
        {
            Assert.AreEqual("the silver locket", Director.TheName("Silver locket"));
            Assert.AreEqual("the Iron Drawer key", Director.TheName("Iron Drawer key"));
            Assert.AreEqual("Mr Vell's chit", Director.TheName("Mr Vell's chit"));
            foreach (var o in Db.root.objects)
                StringAssert.DoesNotContain("mr ", Director.TheName(o.name), o.id);
        }

        [Test]
        public void EverySpeakerHasANameAndClaimantsDiffer()
        {
            foreach (var day in Db.root.days)
                foreach (var c in day.cases)
                {
                    var spoken = c.intro.Concat(c.missing).Concat(c.notMine).Concat(c.verdicts.SelectMany(v => v.reply ?? new LineDef[0]))
                        .Select(l => l.who).Concat(c.answers.Select(a => a.who)).Where(w => !string.IsNullOrEmpty(w) && w != "you" && w != "agnes");
                    foreach (var who in spoken.Distinct())
                    {
                        var def = Db.Commuter(who);
                        Assert.IsNotNull(def, $"case {c.id}: line by '{who}', who isn't a commuter");
                        Assert.IsFalse(string.IsNullOrWhiteSpace(def.ShortName), $"case {c.id}: '{who}' has no short name");
                    }
                    var names = c.claimants.Select(id => Db.Commuter(id)?.ShortName).ToArray();
                    Assert.AreEqual(names.Length, names.Distinct().Count(), $"case {c.id}: two claimants called {string.Join(" and ", names)}");
                }
        }
    }
}
