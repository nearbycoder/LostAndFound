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

        /// <summary>The browser build, which Tools/build-pages.sh turns into the GitHub Pages site. GitHub Pages can't set headers,
        /// so the files are Brotli with Unity's decompression fallback: the loader unpacks them itself when the server doesn't
        /// say they're compressed. Hashed file names, so a new build never meets an old file in a browser's cache. Building for
        /// WebGL rewrites URP's shader prefiltering in Mobile_RPAsset and leaves a Data/ folder of Burst output at the project
        /// root: build-pages.sh puts back the one and removes the other.
        /// Two builds: Builds/WebGL with the desktop's texture formats (DXT and BC7), and Builds/WebGL-astc with ASTC, which
        /// phones' and tablets' GPUs read (they have no DXT, and the player would unpack every texture to plain RGBA, four
        /// times the memory). Only the data file differs; the page picks one by what the GPU offers. -lafNoAstc skips the second.</summary>
        [MenuItem("Lost & Found/Build WebGL Player")]
        public static void BuildWebGL()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                Debug.LogError("[LostAndFound] can't build WebGL: its build support module isn't installed");
                Quit(false);
                return;
            }
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.threadsSupport = false;   // SharedArrayBuffer needs headers Pages can't send
            // the smaller WebAssembly ("Disk Size", and IL2CPP's code for size, which shares generic code): a desk game isn't short
            // of CPU, and a phone's browser needs memory to compile every megabyte of it (about 11 MB a megabyte, in headless WebKit)
            EditorUserBuildSettings.SetPlatformSettings("WebGL", "CodeOptimization", "DiskSize");
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, Il2CppCodeGeneration.OptimizeSize);
            ValidateContent();
            ProjectSetup.Apply();
            StampCommit();
            bool ok = BuildWebGLTo("Builds/WebGL", "desktop textures");
            if (ok && System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-lafNoAstc") < 0) ok = BuildWebGLAstcTo("Builds/WebGL-astc");
            Quit(ok);
        }

        /// <summary>Only the phones' and tablets' build (Builds/WebGL-astc), to try a change to it without the desktop's.</summary>
        public static void BuildWebGLPhones()
        {
            ProjectSetup.Apply();
            StampCommit();
            Quit(BuildWebGLAstcTo("Builds/WebGL-astc"));
        }

        /// <summary>The build with ASTC textures: the build's texture subtarget ASTC, for this one build (the editor's own setting is
        /// put back as it was).</summary>
        static bool BuildWebGLAstcTo(string path)
        {
            var subtarget = EditorUserBuildSettings.webGLBuildSubtarget;
            try
            {
                EditorUserBuildSettings.webGLBuildSubtarget = WebGLTextureSubtarget.ASTC;
                return BuildWebGLTo(path, "ASTC textures, for phones and tablets", (int)WebGLTextureSubtarget.ASTC);
            }
            finally { EditorUserBuildSettings.webGLBuildSubtarget = subtarget; }
        }

        static bool BuildWebGLTo(string path, string what, int subtarget = 0)
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = path,
                target = BuildTarget.WebGL,
                subtarget = subtarget,
                options = BuildOptions.None,
            });
            var s = report.summary;
            Debug.Log($"[LostAndFound] WebGL build ({what}, {path}) {s.result}: {s.totalSize / (1024 * 1024)} MB, {s.totalErrors} errors, {s.totalTime}");
            return s.result == BuildResult.Succeeded;
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
