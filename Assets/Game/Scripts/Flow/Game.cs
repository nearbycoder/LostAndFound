using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LostAndFound
{
    /// <summary>Spawns the whole game at startup (the scene is empty on purpose).</summary>
    public static class Boot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            if (UnityEngine.Object.FindAnyObjectByType<Game>() != null) return;
            new GameObject("Game").AddComponent<Game>();
        }
    }

    public class Game : MonoBehaviour
    {
        public static Game I { get; private set; }
        public ContentDb Db { get; private set; }
        public Camera Cam { get; private set; }
        public SaveGame Save { get; private set; }

        public static string Arg(string name)
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer) return UrlArgs.TryGetValue(name, out var v) ? v : null;
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : (i >= 0 ? "" : null);
        }

        static Dictionary<string, string> urlArgs;

        /// <summary>-lafPrefsProbe "music=0.2,plain=1": write settings exactly as v0.1.0 did (PlayerPrefs "laf." keys, floats
        /// as floats and switches as ints, saved at once) and quit, so a test can see where they land and whether this
        /// version brings them over. Only ever run with a scratch XDG_CONFIG_HOME.</summary>
        static void PrefsProbe(string spec)
        {
            string[] switches = { "shake", "reducemotion", "post", "fullscreen", "plain", "offernudges" };
            foreach (var pair in spec.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = pair.Split('=');
                if (kv.Length != 2 || !float.TryParse(kv[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v)) continue;
                if (Array.IndexOf(switches, kv[0]) >= 0) PlayerPrefs.SetInt("laf." + kv[0], v >= 0.5f ? 1 : 0);
                else PlayerPrefs.SetFloat("laf." + kv[0], v);
            }
            PlayerPrefs.Save();
            Debug.Log($"[PrefsProbe] wrote {spec} to PlayerPrefs and saved; quitting");
            Application.Quit();
        }

        /// <summary>In a browser there's no command line, so the page's query string stands in for it:
        /// index.html?lafSmoke&amp;lafSeconds=40 is -lafSmoke -lafSeconds 40.</summary>
        static Dictionary<string, string> UrlArgs
        {
            get
            {
                if (urlArgs != null) return urlArgs;
                urlArgs = new Dictionary<string, string>();
                string url = Application.absoluteURL ?? "";
                int q = url.IndexOf('?');
                if (q < 0) return urlArgs;
                string query = url.Substring(q + 1);
                int hash = query.IndexOf('#');
                if (hash >= 0) query = query.Substring(0, hash);
                foreach (var part in query.Split('&'))
                {
                    if (part.Length == 0) continue;
                    int eq = part.IndexOf('=');
                    string key = Uri.UnescapeDataString(eq < 0 ? part : part.Substring(0, eq));
                    urlArgs["-" + key.TrimStart('-')] = eq < 0 ? "" : Uri.UnescapeDataString(part.Substring(eq + 1));
                }
                return urlArgs;
            }
        }

        void Awake()
        {
            I = this;
            if (Arg("-lafPrefsProbe") != null) { PrefsProbe(Arg("-lafPrefsProbe")); return; }
            Application.targetFrameRate = Arg("-lafUncapped") != null ? -1 : 120;   // smoke runs measure the headroom above the cap
#if UNITY_EDITOR
            if (Application.isBatchMode)
            {
                // headless editor testing: simulated devices should drive the game, focus or not
                UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode =
                    UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            }
#endif
            QualitySettings.vSyncCount = Arg("-lafNoVsync") != null ? 0 : 1;
            Settings.MusicMuted = Arg("-lafNoMusic") != null;
            // remove whatever the scene had (template camera / light / volume)
            foreach (var c in FindObjectsByType<Camera>()) if (c.transform.root != transform) Destroy(c.gameObject);
            foreach (var l in FindObjectsByType<Light>()) if (l.transform.root != transform) Destroy(l.gameObject);
            foreach (var v in FindObjectsByType<Volume>()) if (v.transform.root != transform) Destroy(v.gameObject);

            Db = new ContentDb(LoadContent());
            gameObject.AddComponent<AudioDirector>();
            gameObject.AddComponent<CursorController>();
            gameObject.AddComponent<GamepadInput>();
            gameObject.AddComponent<BackgroundMode>();

            // camera
            var rig = new GameObject("CameraRig");
            rig.transform.SetParent(transform, false);
            Cam = rig.AddComponent<Camera>();
            Cam.tag = "MainCamera";
            Cam.nearClipPlane = 0.02f;
            Cam.farClipPlane = 60f;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = new Color(0.06f, 0.08f, 0.1f);
            Cam.allowHDR = true;
            rig.AddComponent<AudioListener>();
            var data = rig.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            var camRig = rig.AddComponent<CameraRig>();
            camRig.cam = Cam;
            var hl = new GameObject("HandLight").AddComponent<Light>();
            hl.transform.SetParent(rig.transform, false);
            hl.transform.localPosition = new Vector3(0.22f, 0.16f, 0.02f);
            hl.type = LightType.Point;
            hl.range = 1.3f;
            hl.color = new Color(1f, 0.85f, 0.66f);
            hl.shadows = LightShadows.None;
            hl.intensity = 0f;
            camRig.handLight = hl;

            SetupLighting();
            gameObject.AddComponent<PostFX>();
            gameObject.AddComponent<GraphicsQuality>();

            var ui = new GameObject("UI");
            ui.transform.SetParent(transform, false);
            ui.AddComponent<UIRoot>();
            ui.AddComponent<PauseMenu>();
            ui.AddComponent<RulesHotkey>();
            ApplyDisplay();
            if (!rebuilt) LogKeyLabels();

            var deskGo = new GameObject("Desk");
            deskGo.transform.SetParent(transform, false);
            var desk = deskGo.AddComponent<Desk>();
            desk.Build();

            var inter = gameObject.AddComponent<InteractionSystem>();
            inter.cam = Cam;
            var insp = gameObject.AddComponent<InspectController>();
            insp.cam = Cam;
            var stamps = gameObject.AddComponent<StampTool>();
            stamps.cam = Cam;
            var wfx = gameObject.AddComponent<WindowFX>();
            wfx.Init(desk.booth);
            gameObject.AddComponent<Director>();
            if (Arg("-lafAutopilot") != null) gameObject.AddComponent<AutoPilot>();
            if (Arg("-lafSmoke") != null) gameObject.AddComponent<SmokeTest>();
            if (Arg("-lafDemo") != null) gameObject.AddComponent<DemoRecorder>();
            if (Arg("-lafAuditHotspots") != null) gameObject.AddComponent<HotspotAudit>();
            if (Arg("-lafGamepadTest") != null) gameObject.AddComponent<GamepadTest>();
            if (Arg("-lafTapTest") != null) gameObject.AddComponent<TapTest>();
            if (Arg("-lafEdgeTest") != null) gameObject.AddComponent<EdgeTest>();
            if (Arg("-lafSoak") != null) Soak.Ensure();
        }

        void Start()
        {
            Debug.Log($"[Save] {SaveGame.PathOnDisk}");
            Save = SaveGame.Load() ?? new SaveGame();
            string dayArg = Arg("-lafDay");
            if (dayArg != null && int.TryParse(dayArg, out int day) && Arg("-lafAutopilot") == null)
            {
                Save = new SaveGame { currentDay = day, unlockedDay = day, tutorialDone = day > 1 };
                FastForward(day);
                BeginWeek(day);
                return;
            }
            if (Arg("-lafAutopilot") != null) return; // the autopilot starts the week itself
            int pending = pendingDay;
            pendingDay = 0;
            if (pending > 0) { ReplayDay(pending); return; }
            if (rebuilt) UnloadLeftovers();   // back at the title: the game this one replaced is gone (a day does this itself)
            ToTitle();
        }

        static int pendingDay;
        static bool rebuilt;

        /// <summary>Let go of whatever the game made at runtime (materials for objects, stamp marks, the window glass's
        /// frost, textures) that nothing uses any more. The game is rebuilt in place rather than by loading a scene, so
        /// nothing else does this: without it each Restart and each day left a few behind (Tools/unity.sh soak).</summary>
        public static AsyncOperation UnloadLeftovers() => Resources.UnloadUnusedAssets();

        /// <summary>Tear the whole game down and build it again: back to the title (day 0) or straight into a replay of a day.</summary>
        public void Restart(int day)
        {
            Time.timeScale = 1f;
            pendingDay = day;
            rebuilt = true;
            var fresh = new GameObject("Game");
            Destroy(gameObject);
            fresh.AddComponent<Game>();
        }

        public void ReplayDay(int day)
        {
            Save = SaveGame.Load() ?? Save;
            Director.I.Init(Db, Save);
            if (Save.SnapshotFor(day) == null) { FastForward(day); }
            Director.I.ReplayDay(day);
        }

        static bool windowChecked;

        /// <summary>What the keyboard's letter keys are printed with, as the player's keyboard layout says (a diagnostic).</summary>
        static void LogKeyLabels()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            var keys = new[] { "A", "D", "Q", "E", "W", "S", "Z", "H", "R", "L", "T", "M" };
            Debug.Log("[Keys] printed on: " + string.Join(" ", keys.Select(k =>
                $"{k}='{kb[(UnityEngine.InputSystem.Key)Enum.Parse(typeof(UnityEngine.InputSystem.Key), k)].displayName}'")) + $" (layout '{kb.keyboardLayout}')");
        }

        /// <summary>Fullscreen or a window, as Settings says. A window is made to fit the desktop (WindowFit) the first time
        /// the game starts in one, and whenever it leaves fullscreen, which otherwise keeps the whole screen's size as a
        /// window. A size given on the command line (-screen-width, as the test tools do) is left as it is.</summary>
        public static void ApplyDisplay()
        {
            if (Application.isEditor || Application.platform == RuntimePlatform.WebGLPlayer) return;
            var mode = Settings.Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            bool leaving = mode == FullScreenMode.Windowed && Screen.fullScreenMode != FullScreenMode.Windowed;
            bool first = !windowChecked;
            windowChecked = true;
            var desk = Screen.currentResolution;
            if (mode != FullScreenMode.Windowed)
            {
                // on Wayland, changing fullScreenMode alone leaves an ordinary window (KWin says so in Tools/unity.sh smallscreen)
                if (Screen.fullScreenMode != mode)
                {
                    Debug.Log($"[Window] fullscreen on a {desk.width}x{desk.height} desktop");
                    Screen.SetResolution(desk.width, desk.height, mode);
                }
                return;
            }
            if (!(first || leaving)) return;
            var size = leaving ? WindowFit.Default : new Vector2Int(Screen.width, Screen.height);
            bool given = Arg("-screen-width") != null || Arg("-screen-height") != null;
            var fit = given && !leaving ? size : WindowFit.Fit(size, desk.width, desk.height);
            Debug.Log($"[Window] desktop {desk.width}x{desk.height}, window {Screen.width}x{Screen.height}" +
                      (leaving ? " leaving fullscreen" : "") + (given ? " (size given on the command line)" : "") +
                      (fit == size && !leaving ? ": fits" : $": now {fit.x}x{fit.y}"));
            if (leaving || fit != size) Screen.SetResolution(fit.x, fit.y, FullScreenMode.Windowed);
        }

        /// <summary>Play earlier days with the solver's verdicts so a later day can start from a sensible state.</summary>
        void FastForward(int day)
        {
            var state = Save.state;
            for (int d = 1; d < day; d++)
            {
                Save.Snapshot(d, state);
                foreach (var c in Rules.ActiveCases(Db.Day(d), state).ToList())
                {
                    var dec = Rules.Solve(Db, d, c, state);
                    var vd = Rules.FindVerdict(c, dec.verdict, dec.to) ?? Rules.Best(c);
                    Rules.ApplyVerdict(Db, c, vd, dec.to, state);
                    state.SetRecord(new CaseRecord { caseId = c.id, verdict = vd?.verdict, to = dec.to, grade = vd?.grade, ledger = vd?.ledger });
                }
            }
        }

        public void BeginWeek(int day)
        {
            Debug.Log($"[Game] begin week at day {day} (frame {Time.frameCount})");
            Director.I.Init(Db, Save);
            Director.I.StartDay(day);
        }

        /// <summary>Pick the saved week up where it was left: mid-day after the last decided case, or the day's morning.</summary>
        public void ContinueWeek()
        {
            Debug.Log($"[Game] continue at day {Save.currentDay}, after {Save.casesDone} case(s) (frame {Time.frameCount})");
            Director.I.Init(Db, Save);
            Director.I.StartDay(Mathf.Clamp(Save.currentDay, 1, Db.DayCount), Save.casesDone);
        }

        /// <summary>Begin at a later day, earlier days played with the solver's verdicts.</summary>
        public void StartFromDay(int day)
        {
            Save = new SaveGame { currentDay = day, unlockedDay = day, tutorialDone = true };
            FastForward(day);
            BeginWeek(day);
        }

        public void NewWeek()
        {
            var seen = Save?.endingsSeen;
            SaveGame.Delete();
            Save = new SaveGame();
            if (seen != null) Save.endingsSeen.AddRange(seen);   // the endings you've reached stay reached
            BeginWeek(1);
        }

        public void ToTitle()
        {
            if (Director.I != null && Director.I.Running) { Restart(0); return; }
            Save = SaveGame.Load() ?? Save;
            TitleScreen.Show(this);
        }

        static ContentRoot LoadContent()
        {
            var parts = Resources.LoadAll<TextAsset>("Content").Select(t =>
            {
                try { return JsonUtility.FromJson<ContentRoot>(t.text); }
                catch (Exception e) { Debug.LogError($"[Content] {t.name}: {e.Message}"); return new ContentRoot(); }
            }).ToArray();
            return ContentDb.Merge(parts);
        }

        void SetupLighting()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.30f, 0.34f, 0.40f);
            RenderSettings.ambientEquatorColor = new Color(0.27f, 0.22f, 0.18f);
            RenderSettings.ambientGroundColor = new Color(0.1f, 0.08f, 0.07f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.13f, 0.17f, 0.21f);
            RenderSettings.fogDensity = 0.045f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;

            var hall = new GameObject("HallLight").AddComponent<Light>();
            hall.transform.SetParent(transform, false);
            hall.type = LightType.Directional;
            hall.transform.rotation = Quaternion.Euler(28f, 200f, 0f);
            hall.color = new Color(0.58f, 0.7f, 0.9f);
            hall.intensity = 0.45f;
            hall.shadows = LightShadows.Soft;
            hall.shadowStrength = 0.6f;

            // warm pools of light out in the concourse
            foreach (var p in new[] { new Vector3(-4.5f, 3.2f, 3f), new Vector3(4.5f, 3.2f, 3f), new Vector3(-4.5f, 3.2f, 7.5f), new Vector3(4.5f, 3.2f, 7.5f), new Vector3(0f, 3.2f, 11.5f), new Vector3(0f, 5f, 5.5f) })
            {
                var l = new GameObject("ConcourseLight").AddComponent<Light>();
                l.transform.SetParent(transform, false);
                l.transform.position = p;
                l.type = LightType.Point;
                l.range = 7f;
                l.intensity = 2.2f;
                l.color = new Color(1f, 0.8f, 0.55f);
                l.shadows = LightShadows.None;
            }
            // a warm, soft ceiling fill inside the booth so the drawers and shelf read
            var fill = new GameObject("BoothFill").AddComponent<Light>();
            fill.transform.SetParent(transform, false);
            fill.type = LightType.Point;
            fill.transform.position = new Vector3(0f, 2.25f, 0.1f);
            fill.range = 4.2f;
            fill.intensity = 1.6f;
            fill.color = new Color(1f, 0.82f, 0.62f);
            fill.shadows = LightShadows.None;
            // a cool rim from behind the commuters
            var rim = new GameObject("RimLight").AddComponent<Light>();
            rim.transform.SetParent(transform, false);
            rim.type = LightType.Spot;
            rim.transform.position = new Vector3(0.6f, 2.6f, 3.4f);
            rim.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 1.3f, 1.55f) - rim.transform.position);
            rim.spotAngle = 60f;
            rim.range = 6f;
            rim.intensity = 2.2f;
            rim.color = new Color(0.62f, 0.78f, 1f);
            rim.shadows = LightShadows.None;
        }
    }
}
