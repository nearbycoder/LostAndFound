using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LostAndFound.EditorTools
{
    /// <summary>Menu items and batch-mode entry points for building the players.</summary>
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
        public static void BuildLinux() => Build(BuildTarget.StandaloneLinux64, "Builds/Linux/LostAndFound.x86_64");

        /// <summary>A universal (Intel + Apple silicon) Mono build, unsigned and not notarised.</summary>
        [MenuItem("Lost & Found/Build macOS Player")]
        public static void BuildMac()
        {
            EditorUserBuildSettings.SetPlatformSettings(BuildPipeline.GetBuildTargetName(BuildTarget.StandaloneOSX), "Architecture", "x64ARM64");
            // Unity turns the "&" into "_" in the bundle and menu-bar name, and the bundle is signed as it's
            // written, so it can't be patched afterwards: spell it out for the Mac
            Build(BuildTarget.StandaloneOSX, "Builds/macOS/LostAndFound.app", "Lost and Found");
        }

        /// <summary>Needs Unity's Windows Build Support module, which isn't installed on the machine this was made on.</summary>
        [MenuItem("Lost & Found/Build Windows Player")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/LostAndFound.exe");

        /// <summary>A WebGL player for measuring (the round 3 spike): download size, load time, frame rate. Not shipped.
        /// Uses the project's WebGL settings as they are (Brotli, no decompression fallback), so a host has to send the .br
        /// files with "Content-Encoding: br". Building for WebGL rewrites URP's shader prefiltering in Mobile_RPAsset and
        /// leaves a Data/ folder of Burst output at the project root: revert the one and delete the other afterwards.</summary>
        [MenuItem("Lost & Found/Build WebGL Player (spike)")]
        public static void BuildWebGL()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                Debug.LogError("[LostAndFound] can't build WebGL: its build support module isn't installed");
                Quit(false);
                return;
            }
            ValidateContent();
            ProjectSetup.Apply();
            StampCommit();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = "Builds/WebGL",
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });
            var s = report.summary;
            Debug.Log($"[LostAndFound] WebGL build {s.result}: {s.totalSize / (1024 * 1024)} MB, {s.totalErrors} errors, {s.totalTime}");
            Quit(s.result == BuildResult.Succeeded);
        }

        static void Build(BuildTarget target, string path, string productName = null)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
            {
                Debug.LogError($"[LostAndFound] can't build {target}: its build support module isn't installed (Unity Hub > Installs > Add modules)");
                Quit(false);
                return;
            }
            ValidateContent();
            ProjectSetup.Apply();
            StampCommit();
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            string product = PlayerSettings.productName;
            if (productName != null) PlayerSettings.productName = productName;
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = Scenes,
                    locationPathName = path,
                    target = target,
                    options = BuildOptions.None,
                });
            }
            finally { PlayerSettings.productName = product; }
            var s = report.summary;
            Debug.Log($"[LostAndFound] {target} build {s.result}: {s.totalSize / (1024 * 1024)} MB, {s.totalErrors} errors, {s.totalTime}");
            Quit(s.result == BuildResult.Succeeded);
        }

        /// <summary>Writes the commit being built (with "+" if the working tree has changes) to Resources/BuildInfo.txt, which
        /// the title shows beside the version (BuildInfo). The file is in .gitignore: it changes with every commit.</summary>
        static void StampCommit()
        {
            static string Git(string args)
            {
                try
                {
                    var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("git", args)
                    {
                        RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true,
                        WorkingDirectory = System.IO.Path.GetDirectoryName(Application.dataPath),
                    });
                    string o = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();
                    return p.ExitCode == 0 ? o.Trim() : null;
                }
                catch { return null; }
            }
            string head = Git("rev-parse --short HEAD");
            string dirty = Git("status --porcelain --untracked-files=no");
            string stamp = head == null ? "unknown build" : head + (string.IsNullOrEmpty(dirty) ? "" : "+");
            const string file = "Assets/Game/Resources/BuildInfo.txt";
            System.IO.File.WriteAllText(file, stamp + "\n");
            AssetDatabase.ImportAsset(file, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[LostAndFound] building commit {stamp}");
        }

        // quit only when launched as a one-shot (-executeMethod), not inside a resident editor
        static void Quit(bool ok)
        {
            if (Application.isBatchMode && System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-executeMethod") >= 0)
                EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
