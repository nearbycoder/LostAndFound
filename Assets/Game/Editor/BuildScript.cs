using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LostAndFound.EditorTools
{
    /// <summary>Menu items and batch-mode entry points for building the player.</summary>
    public static class BuildScript
    {
        static readonly string[] Scenes = { "Assets/Scenes/Main.unity" };

        [MenuItem("Lost & Found/Build Linux Player")]
        public static void BuildLinux()
        {
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
