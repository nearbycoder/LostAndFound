using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace LostAndFound
{
    /// <summary>
    /// -lafSmoke &lt;dir&gt; [-lafSeconds n]: the built game plays itself with dialogue auto-advancing and the
    /// bell auto-ringing, saving a screenshot every few seconds, then quits. A quick check that a
    /// build boots, renders, and runs the day loop.
    /// </summary>
    public class SmokeTest : MonoBehaviour
    {
        string dir;
        float seconds = 40f;

        void Start()
        {
            dir = Game.Arg("-lafSmoke");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Application.persistentDataPath, "smoke");
            if (float.TryParse(Game.Arg("-lafSeconds") ?? "", out float s)) seconds = s;
            Directory.CreateDirectory(dir);
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            yield return new WaitForSeconds(0.5f);
            if (Director.I != null) { Director.I.autoAdvance = true; Director.I.autoRing = true; }
            // -lafSetQuality N: change Settings > Picture quality mid-run, as the menu does (this one is saved)
            // -lafShowCurios: open the title's Curiosities page (screenshots of it with a given save)
            if (Game.Arg("-lafShowCurios") != null && TitleScreen.Showing) CurioLedger.Show(Game.I);
            if (Game.Arg("-lafShowSettings") != null) SettingsPanel.Open(null);   // as the menu opens it (nothing is changed)
            if (Game.Arg("-lafShowControls") != null) ControlsCard.Show();       // as the title's Controls opens it
            Debug.Log($"[Smoke] text size at launch: {Settings.TextSize} (reading UI x{UiKit.TextScale:0.##})");
            // -lafSetTextSize N: change Settings > Large text mid-run (this one is saved)
            if (int.TryParse(Game.Arg("-lafSetTextSize") ?? "", out int setT))
            {
                yield return new WaitForSeconds(1f);
                Settings.TextSize = setT;
                yield return null;
                Debug.Log($"[Smoke] text size set to {Settings.TextSize}: hint bar now x{UIRoot.I.hint.transform.localScale.x:0.##}");
            }
            if (int.TryParse(Game.Arg("-lafSetQuality") ?? "", out int setQ))
            {
                yield return new WaitForSeconds(2f);
                Settings.PictureQuality = setQ;
                Debug.Log($"[Smoke] picture quality set to {GraphicsQuality.Names[Settings.PictureQuality]}");
                SettingsPanel.Open(null);   // the screenshots from here on show the panel with the new value
            }
            // -lafFullscreenTrip: Settings > Fullscreen on, then off again, as the toggle does (only ever run inside
            // Tools/unity.sh smallscreen's headless KWin, never on a real desktop)
            if (Game.Arg("-lafFullscreenTrip") != null)
            {
                if (System.Environment.GetEnvironmentVariable("LAF_SMALLSCREEN") == "1") StartCoroutine(FullscreenTrip());
                else Debug.LogWarning("[Window] -lafFullscreenTrip ignored: only inside Tools/unity.sh smallscreen");
            }
            if (Game.Arg("-lafCardFrames") != null) { yield return CardFrames(); Application.Quit(); yield break; }
            int n = 0;
            float t = 0f;
            int lastFrame = Time.frameCount;
            float lastTime = Time.realtimeSinceStartup;
            while (t < seconds)
            {
                yield return new WaitForEndOfFrame();
                float fps = (Time.frameCount - lastFrame) / Mathf.Max(0.01f, Time.realtimeSinceStartup - lastTime);
                lastFrame = Time.frameCount;
                lastTime = Time.realtimeSinceStartup;
                Debug.Log($"[Smoke] fps {fps:0.0} ({SystemInfo.graphicsDeviceType})");
                if (Application.platform == RuntimePlatform.WebGLPlayer) Debug.Log($"[Shot] smoke_{n:00}");   // Tools/webgl_check.py takes it
                else ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"smoke_{n:00}.png"));
                if (Game.Arg("-lafTextAudit") != null) TextAudit.Check($"smoke_{n:00}");   // the title, Settings, Controls...
                Debug.Log($"[Smoke] shot {n} at {t:0.0}s phase ok, frame {Time.frameCount}");
                n++;
                if (n == 2 && TitleScreen.Showing) TitleScreen.Begin(Game.I);   // past the title, into the week
                yield return new WaitForSeconds(3f);
                t += 3f;
            }
            if (Game.Arg("-lafTextAudit") != null) TextAudit.Summary();
            Debug.Log("[Smoke] done");
            Application.Quit();
        }

        /// <summary>-lafCardFrames: Settings over the title, then the pause menu in the week, photographed 0, 60, 120 and 240 ms after
        /// each opens and 0, 70 and 140 ms after it closes (CardMotion), then the same with Reduce motion on, for one frame each.</summary>
        IEnumerator CardFrames()
        {
            yield return new WaitForSecondsRealtime(2.5f);   // the title has faded in
            IEnumerator Frames(string name, System.Action open, System.Action close)
            {
                float t0 = Time.realtimeSinceStartup;
                open();
                foreach (int ms in new[] { 0, 60, 120, 240 })
                {
                    while (Time.realtimeSinceStartup - t0 < ms / 1000f) yield return null;
                    ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"card_{name}_open_{ms:000}ms.png"));
                    Debug.Log($"[Cards] {name} open +{(Time.realtimeSinceStartup - t0) * 1000f:0} ms, frame {Time.frameCount}");
                    yield return null;
                }
                yield return new WaitForSecondsRealtime(0.4f);
                t0 = Time.realtimeSinceStartup;
                close();
                foreach (int ms in new[] { 0, 70, 140 })
                {
                    while (Time.realtimeSinceStartup - t0 < ms / 1000f) yield return null;
                    ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"card_{name}_close_{ms:000}ms.png"));
                    Debug.Log($"[Cards] {name} close +{(Time.realtimeSinceStartup - t0) * 1000f:0} ms, frame {Time.frameCount}");
                    yield return null;
                }
                yield return new WaitForSecondsRealtime(0.4f);
            }
            void PressDone() => Object.FindObjectsByType<PaperButton>().FirstOrDefault(b => b.label != null && b.label.text == "Done")?.Press();
            var pause = Object.FindAnyObjectByType<PauseMenu>();
            void PressBack() => Object.FindObjectsByType<PaperButton>().FirstOrDefault(b => b.label != null && b.label.text == "Back to the desk")?.Press();
            yield return Frames("settings", () => SettingsPanel.Open(null), PressDone);
            Settings.ReduceMotion = true;
            yield return Frames("settings_reduced", () => SettingsPanel.Open(null), PressDone);
            Settings.ReduceMotion = false;
            TitleScreen.Begin(Game.I);
            yield return new WaitForSecondsRealtime(1f);
            while (Director.I == null || !Director.I.Running || UIRoot.ModalOpen) yield return null;
            yield return new WaitForSecondsRealtime(7f);   // past the day's title card, at the desk
            yield return Frames("pause", () => pause.Show(), PressBack);
            Settings.ReduceMotion = true;
            yield return Frames("pause_reduced", () => pause.Show(), PressBack);
            Settings.ReduceMotion = false;
            Debug.Log("[Cards] done");
        }

        IEnumerator FullscreenTrip()
        {
            yield return new WaitForSecondsRealtime(8f);
            Debug.Log($"[Window] before the trip: {Screen.width}x{Screen.height} {Screen.fullScreenMode}");
            Settings.Fullscreen = true;
            Game.ApplyDisplay();
            yield return new WaitForSecondsRealtime(6f);
            Debug.Log($"[Window] fullscreen: {Screen.width}x{Screen.height} {Screen.fullScreenMode}");
            Settings.Fullscreen = false;
            Game.ApplyDisplay();
            yield return new WaitForSecondsRealtime(6f);
            Debug.Log($"[Window] back in a window: {Screen.width}x{Screen.height} {Screen.fullScreenMode}");
        }
    }
}
