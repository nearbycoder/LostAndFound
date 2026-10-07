using System.Collections;
using System.IO;
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
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"smoke_{n:00}.png"));
                Debug.Log($"[Smoke] shot {n} at {t:0.0}s phase ok, frame {Time.frameCount}");
                n++;
                if (n == 2 && TitleScreen.Showing) TitleScreen.Begin(Game.I);   // past the title, into the week
                yield return new WaitForSeconds(3f);
                t += 3f;
            }
            Debug.Log("[Smoke] done");
            Application.Quit();
        }
    }
}
