using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace LostAndFound.Tests
{
    /// <summary>
    /// Menu choices that would throw progress away ask for a second click and say what goes; choices that lose nothing
    /// act at once. A replay after a finished week is a day under way again, so Continue picks it up.
    /// </summary>
    public class ProgressGuardTests
    {
        static ContentDb db;

        static ContentDb Db => db ??= new ContentDb(ContentDb.Merge(
            Resources.LoadAll<TextAsset>("Content").Select(t => JsonUtility.FromJson<ContentRoot>(t.text)).ToArray()));

        static SaveGame At(int day, int casesDone, bool finished = false) =>
            new SaveGame { currentDay = day, unlockedDay = day, casesDone = casesDone, finished = finished };

        [Test]
        public void StartingTheDayAgainAsksOnlyOnceAClaimIsDecided()
        {
            Assert.IsNull(ProgressGuard.RestartDay(0), "nothing decided today: nothing to lose");
            Assert.AreEqual("Click again to undo today's one claim", ProgressGuard.RestartDay(1));
            Assert.AreEqual("Click again to undo today's three claims", ProgressGuard.RestartDay(3));
        }

        [Test]
        public void ChoosingAnEarlierDayDuringAWeekSaysWhereTheWeekIs()
        {
            var thursday = At(4, 2);
            Assert.AreEqual("click again to rewind the week from Thursday", ProgressGuard.ReplayDay(thursday, Db, 1));
            Assert.AreEqual("click again to rewind the week from Thursday", ProgressGuard.ReplayDay(At(4, 0), Db, 3),
                "even on Thursday morning, yesterday's replay throws Thursday away");
            Assert.AreEqual("click again to start it again (two claims undone)", ProgressGuard.ReplayDay(thursday, Db, 4));
        }

        [Test]
        public void ChoicesThatLoseNothingDontAsk()
        {
            Assert.IsNull(ProgressGuard.ReplayDay(At(4, 0), Db, 4), "today, before any claim");
            Assert.IsNull(ProgressGuard.ReplayDay(At(4, 3, finished: true), Db, 1), "the week is finished and its ending kept");
            Assert.IsNull(ProgressGuard.ReplayDay(At(2, 1), Db, 3), "a later day isn't a rewind (and is locked anyway)");
        }

        [Test]
        public void AReplayAfterAFinishedWeekCanBeContinued()
        {
            var save = At(5, 5, finished: true);
            save.ending = "the940";
            save.NoteEnding(save.ending);
            var tuesday = new StoryState();
            save.Snapshot(2, tuesday);
            var back = save.BeginReplay(2);
            Assert.IsNotNull(back, "Tuesday's starting state comes back");
            Assert.IsFalse(save.finished, "the replay is a day under way, so the title offers Continue");
            Assert.AreEqual(2, save.currentDay);
            Assert.AreEqual(0, save.casesDone);
            CollectionAssert.Contains(save.endingsSeen, "the940", "the ending reached stays reached");
            Assert.IsNull(save.BeginReplay(3), "no state was kept for Wednesday");
        }
    }
}
