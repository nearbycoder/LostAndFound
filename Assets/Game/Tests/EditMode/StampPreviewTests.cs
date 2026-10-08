using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace LostAndFound.Tests
{
    /// <summary>
    /// Before a stamp comes down, the hint says what it will do there: whom RETURN gives the item on the tray to (with two
    /// claimants, the one whose half of the slip it's over), whom REFUSE sends away, what SEAL locks up. Only what's on the desk
    /// is named; nothing says whether it's the right verdict.
    /// </summary>
    public class StampPreviewTests
    {
        static ContentDb db;

        static ContentDb Db => db ??= new ContentDb(ContentDb.Merge(
            Resources.LoadAll<TextAsset>("Content").Select(t => JsonUtility.FromJson<ContentRoot>(t.text)).ToArray()));

        static readonly string[] One = { "Walter Bix" };
        static readonly string[] Two = { "Ada Finch", "Edie Finch" };

        [Test]
        public void ReturnNamesWhatsOnTheTrayAndWhoGetsIt()
        {
            var wallet = Db.Object("wallet_brown");
            Assert.AreEqual("RETURN: give the brown leather wallet to Walter Bix", StampPreview.What(Verdict.Return, wallet, One, 0));
            Assert.AreEqual("RETURN: give the brown leather wallet to Walter Bix", StampPreview.What(Verdict.Return, wallet, One, 1), "one claimant: the whole slip is theirs");
        }

        [Test]
        public void WithTwoClaimantsReturnFollowsTheHalfOfTheSlip()
        {
            var locket = Db.Object("locket");
            Assert.IsNotNull(locket);
            Assert.AreEqual("RETURN: give the silver locket to Ada Finch", StampPreview.What(Verdict.Return, locket, Two, 0));
            Assert.AreEqual("RETURN: give the silver locket to Edie Finch", StampPreview.What(Verdict.Return, locket, Two, 1));
        }

        [Test]
        public void RefuseSendsEveryClaimantAway()
        {
            Assert.AreEqual("REFUSE: send Walter Bix away with nothing", StampPreview.What(Verdict.Refuse, null, One, 0));
            Assert.AreEqual("REFUSE: send Ada Finch and Edie Finch away with nothing", StampPreview.What(Verdict.Refuse, null, Two, 1));
            Assert.AreEqual("REFUSE: send Walter Bix away with nothing", StampPreview.What(Verdict.Refuse, Db.Object("wallet_brown"), One, 0), "whatever's on the tray");
        }

        [Test]
        public void SealNamesWhatGoesInTheIronDrawer()
        {
            Assert.AreEqual("SEAL: lock the silver pocket watch in the Iron Drawer", StampPreview.What(Verdict.Seal, Db.Object("pocket_watch"), One, 0));
            Assert.AreEqual("SEAL: lock the Iron Drawer key in the Iron Drawer", StampPreview.What(Verdict.Seal, new ObjectDef { name = "Iron Drawer key" }, One, 0), "a proper name keeps its capitals");
        }

        [Test]
        public void NothingOnTheTrayIsLeftToTheDirectorsReason()
        {
            Assert.IsNull(StampPreview.What(Verdict.Return, null, One, 0));
            Assert.IsNull(StampPreview.What(Verdict.Seal, null, Two, 0));
        }

        [Test]
        public void TheHintSaysHowToStampInTheControlsInHand()
        {
            string what = StampPreview.What(Verdict.Refuse, null, One, 0);
            Assert.AreEqual(what + "   ·   Click to stamp", StampPreview.Hint(what, true, false));
            Assert.AreEqual(what + "   ·   A to stamp", StampPreview.Hint(what, true, true));
            StringAssert.Contains("Click on the slip", StampPreview.Hint(null, false, false));
            StringAssert.Contains("A to stamp", StampPreview.Hint(null, false, true));
        }

        [Test]
        public void EveryCaseOfTheWeekReadsCleanly()
        {
            // every claim's people and wanted item, every stamp, every half of the slip: a name, never an id or a blank
            foreach (var day in Db.root.days)
                foreach (var c in day.cases)
                {
                    var names = c.claimants.Select(id => Db.Commuter(id)?.name ?? id).ToArray();
                    var item = Db.Object(c.wants);
                    for (int who = 0; who < names.Length; who++)
                        foreach (var v in new[] { Verdict.Return, Verdict.Refuse, Verdict.Seal })
                        {
                            string s = StampPreview.What(v, item, names, who);
                            if (item == null && v != Verdict.Refuse) { Assert.IsNull(s); continue; }
                            Assert.IsFalse(string.IsNullOrWhiteSpace(s), $"{c.id} {v}");
                            Assert.IsFalse(s.Contains("  "), $"{c.id} {v}: {s}");
                            foreach (var id in c.claimants) if (Db.Commuter(id) != null) StringAssert.DoesNotContain(" " + id + " ", s + " ", $"{c.id}: an id, not a name");
                            if (v == Verdict.Return) StringAssert.EndsWith(names[who], s);
                        }
                }
        }
    }
}
