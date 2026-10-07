using UnityEngine;

namespace LostAndFound
{
    /// <summary>
    /// Which build this is, for a bug report: the version and the commit the player was built from. The build script writes
    /// the commit into Resources/BuildInfo.txt (not committed) just before each build, with a "+" if the working tree had
    /// changes; in the editor there's none, and it says so.
    /// </summary>
    public static class BuildInfo
    {
        static string commit;

        public static string Commit => commit ??= Resources.Load<TextAsset>("BuildInfo")?.text.Trim() is { Length: > 0 } c ? c : Application.isEditor ? "editor" : "unknown build";

        /// <summary>"v0.1.0 · ed71667"</summary>
        public static string Line => $"v{Application.version}  ·  {Commit}";
    }
}
