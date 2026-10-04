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
                yield return new WaitForSeconds(3f);
                t += 3f;
            }
            Debug.Log("[Smoke] done");
            Application.Quit();
        }
    }
}
