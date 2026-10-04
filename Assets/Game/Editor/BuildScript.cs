using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LostAndFound.EditorTools
{
    /// <summary>Menu items and batch-mode entry points for building the player.</summary>
    public static class BuildScript
    {
        static readonly string[] Scenes = { "Assets/Scenes/Main.unity" };

        [MenuItem("Lost & Found/Validate Content")]
        static void ValidateMenu() => ValidateContent();

        /// <summary>Load every content file and prove each case is solvable from what's on the desk.</summary>
        public static int ValidateContent()
        {
            var parts = new System.Collections.Generic.List<ContentRoot>();
            foreach (var guid in AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/Game/Resources/Content" }))
            {
                var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetDatabase.GUIDToAssetPath(guid));
                parts.Add(JsonUtility.FromJson<ContentRoot>(ta.text));
            }
            var db = new ContentDb(ContentDb.Merge(parts.ToArray()));
            var log = new System.Collections.Generic.List<string>();
            var issues = Rules.Validate(db, log);
            foreach (var l in log) Debug.Log("[Validate] " + l);
            foreach (var i in issues) Debug.LogWarning("[Validate] ISSUE " + i);
            Debug.Log($"[Validate] {db.root.objects.Length} objects, {db.root.days.Length} days, {db.root.days.Sum(d => d.cases.Length)} cases, {issues.Count} issues");
            return issues.Count;
        }

        [MenuItem("Lost & Found/Build Linux Player")]
        public static void BuildLinux()
        {
            ValidateContent();
            ProjectSetup.Apply();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = "Builds/Linux/LostAndFound.x86_64",
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.None,
            });
            var s = report.summary;
            Debug.Log($"[LostAndFound] build {s.result}: {s.totalSize / (1024 * 1024)} MB, {s.totalErrors} errors, {s.totalTime}");
            // quit only when launched as a one-shot (-executeMethod), not inside a resident editor
            if (Application.isBatchMode && System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-executeMethod") >= 0)
                EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
