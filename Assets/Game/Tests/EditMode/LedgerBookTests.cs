using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace LostAndFound.Tests
{
    /// <summary>The ledger book on the desk holds the page of every day already closed this week, written from the state that
    /// evening left, and nothing from today: its marks would give today's answers away.</summary>
    public class LedgerBookTests
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

        /// <summary>A day played in memory, by the solver or refusing everything, then closed as FinishDay closes it.</summary>
        static void PlayDay(SaveGame save, StoryState state, int day, bool refuseAll = false)
        {
            save.Snapshot(day, state);
            foreach (var c in Rules.ActiveCases(Db.Day(day), state).ToList())
            {
                var item = Db.Object(c.wants);
                if (item != null && !state.InStorage(item, day)) { state.SetRecord(new CaseRecord { caseId = c.id, verdict = "missing", grade = "skip" }); continue; }
                var dec = Rules.Solve(Db, day, c, state);
                var vd = (refuseAll ? Rules.FindVerdict(c, Verdict.Refuse, null) : null) ?? Rules.FindVerdict(c, dec.verdict, dec.to) ?? Rules.Best(c);
                Rules.ApplyVerdict(Db, c, vd, dec.to, state);
                state.SetRecord(new CaseRecord { caseId = c.id, verdict = vd.verdict, to = dec.to, grade = vd.grade, ledger = vd.ledger });
            }
            save.Snapshot(day + 1, state);
        }

        [Test]
        public void MondayHasNothingInItYet()
        {
            var save = new SaveGame();
            var state = new StoryState();
            save.Snapshot(1, state);
            CollectionAssert.IsEmpty(LedgerBook.Days(Db, save, state, 1));
        }

        [Test]
        public void HoldsTheClosedDaysAndNeverToday()
        {
            var save = new SaveGame();
            var state = new StoryState();
            for (int day = 1; day <= 3; day++) PlayDay(save, state, day);
            // Thursday morning: Monday to Wednesday are closed
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, LedgerBook.Days(Db, save, state, 4));
            // halfway through Thursday, a claim decided today still isn't in the book
            var c = Rules.ActiveCases(Db.Day(4), state).First();
            state.SetRecord(new CaseRecord { caseId = c.id, verdict = "refuse", grade = "wrong" });
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, LedgerBook.Days(Db, save, state, 4));
        }

        [Test]
        public void EachPageIsWrittenFromItsOwnEvening()
        {
            var save = new SaveGame();
            var state = new StoryState();
            PlayDay(save, state, 1);
            PlayDay(save, state, 2);
            // what Tuesday's page is drawn from is Wednesday morning's snapshot, not whatever the week has done since
            state.Set("vellItems", "5");
            var tuesday = LedgerBook.StateAt(save, state, 2);
            Assert.AreSame(save.snapshots.First(s => s.day == 3).state, tuesday);
            Assert.AreNotEqual("5", tuesday.Get("vellItems"));
            foreach (var c in Db.Day(2).cases.Where(c => state.Record(c.id) != null))
                Assert.AreEqual(state.Record(c.id).grade, tuesday.Record(c.id).grade, $"case {c.id}");
        }

        [Test]
        public void AReplayDropsTheLaterDaysAndRewritesItsOwn()
        {
            var save = new SaveGame();
            var state = new StoryState();
            for (int day = 1; day <= 3; day++) PlayDay(save, state, day);
            // Choose a Day: Tuesday again. The book holds Monday only
            state = save.BeginReplay(2);
            CollectionAssert.AreEqual(new[] { 1 }, LedgerBook.Days(Db, save, state, 2));
            // Tuesday played differently this time: Wednesday morning's book has the new page
            PlayDay(save, state, 2, refuseAll: true);
            CollectionAssert.AreEqual(new[] { 1, 2 }, LedgerBook.Days(Db, save, state, 3));
            var page = LedgerBook.StateAt(save, state, 2);
            Assert.IsTrue(Db.Day(2).cases.Where(c => page.Record(c.id) != null).All(c => page.Record(c.id).verdict == "refuse" || page.Record(c.id).verdict == "missing"),
                "Tuesday's page is the replay's");
        }

        [Test]
        public void AnOlderSaveWithoutSnapshotsReadsTheWeeksRecords()
        {
            var save = new SaveGame();
            var state = new StoryState();
            for (int day = 1; day <= 2; day++) PlayDay(save, state, day);
            save.snapshots.Clear();
            CollectionAssert.AreEqual(new[] { 1, 2 }, LedgerBook.Days(Db, save, state, 3));
        }
    }
}
