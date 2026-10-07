namespace LostAndFound
{
    /// <summary>
    /// What a menu choice would throw away, in words for its button, or null if it throws nothing away. A choice that
    /// would lose progress asks for a second click first and says what goes, as "A New Week" does.
    /// </summary>
    public static class ProgressGuard
    {
        /// <summary>The pause menu's "Start the day again": every claim decided today is undone.</summary>
        public static string RestartDay(int casesDone) =>
            casesDone <= 0 ? null : $"Click again to undo today's {Count(casesDone)}";

        /// <summary>"Choose a Day" while a week is under way: replaying a day rewinds the week to that day's morning. Nothing
        /// is lost after a finished week (its ending stays reached), on a later day, or on today before any claim is decided.</summary>
        public static string ReplayDay(SaveGame save, ContentDb db, int day)
        {
            if (save == null || save.finished || day > save.currentDay) return null;
            if (day == save.currentDay)
                return save.casesDone <= 0 ? null : $"click again to start it again ({Count(save.casesDone)} undone)";
            var now = db?.Day(save.currentDay);
            return $"click again to rewind the week from {now?.weekday ?? "day " + save.currentDay}";
        }

        static string Count(int n) => n switch
        {
            1 => "one claim",
            2 => "two claims",
            3 => "three claims",
            4 => "four claims",
            5 => "five claims",
            _ => n + " claims",
        };
    }
}
