using System;
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
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : (i >= 0 ? "" : null);
        }

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 120;
            QualitySettings.vSyncCount = 1;
            // remove whatever the scene had (template camera / light / volume)
            foreach (var c in FindObjectsByType<Camera>()) if (c.transform.root != transform) Destroy(c.gameObject);
            foreach (var l in FindObjectsByType<Light>()) if (l.transform.root != transform) Destroy(l.gameObject);
            foreach (var v in FindObjectsByType<Volume>()) if (v.transform.root != transform) Destroy(v.gameObject);

            Db = new ContentDb(LoadContent());
            gameObject.AddComponent<AudioDirector>();
            gameObject.AddComponent<CursorController>();

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
            hl.shadows = LightShadows.Soft;
            hl.shadowStrength = 0.6f;
            hl.intensity = 0f;
            camRig.handLight = hl;

            SetupLighting();
            gameObject.AddComponent<PostFX>();

            var ui = new GameObject("UI");
            ui.transform.SetParent(transform, false);
            ui.AddComponent<UIRoot>();

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
        }

        void Start()
        {
            Save = SaveGame.Load() ?? new SaveGame();
            string dayArg = Arg("-lafDay");
            if (dayArg != null && int.TryParse(dayArg, out int day))
            {
                Save = new SaveGame { currentDay = day, unlockedDay = day, tutorialDone = day > 1 };
                FastForward(day);
                BeginWeek(day);
                return;
            }
            if (Arg("-lafAutopilot") != null) return; // the autopilot starts the week itself
            ToTitle();
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
            Director.I.Init(Db, Save);
            Director.I.StartDay(day);
        }

        public void NewWeek()
        {
            SaveGame.Delete();
            Save = new SaveGame();
            BeginWeek(1);
        }

        public void ToTitle()
        {
            if (TitleScreen.Exists) TitleScreen.Show(this);
            else BeginWeek(Save.currentDay);
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
            fill.shadows = LightShadows.Soft;
            fill.shadowStrength = 0.5f;
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
