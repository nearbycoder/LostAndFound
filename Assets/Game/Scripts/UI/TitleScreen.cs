namespace LostAndFound
{
    /// <summary>Title screen (built in M4).</summary>
    public static partial class TitleScreen
    {
        public static bool Exists => false;
        public static void Show(Game g) => g.BeginWeek(g.Save.currentDay);
    }
}
