using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace LostAndFound
{
    /// <summary>
    /// -lafFidelityBench [scenes] with the AutoPilot (Tools/unity.sh fidelity): at the named AutoPilot moments (default
    /// "case1.1_window,case1.1_inspect") the game holds still (time stopped) and each Graphics fidelity step is applied in turn,
    /// allowed to settle, timed over -lafBenchFrames uncapped frames (default 400) and photographed, so every step is pictured
    /// at the same moment. The steps are timed twice, Low to Ultra and back, and each step's figure is the mean of its two
    /// medians, so a change in the machine's load during the run shows as a gap between them. Logged as [Fidelity] lines with
    /// the load average; the setting itself is never changed or saved.
    /// </summary>
    public static class FidelityBench
    {
        static HashSet<string> scenes;

        public static bool On => Game.Arg("-lafFidelityBench") != null;

        public static bool Wants(string shot)
        {
            if (!On) return false;
            scenes ??= new HashSet<string>(((Game.Arg("-lafFidelityBench") is string a && !a.StartsWith("-") && a.Length > 0) ? a : "case1.1_window,case1.1_inspect").Split(','));
            return scenes.Contains(shot);
        }

        static string Load() { try { return File.ReadAllText("/proc/loadavg").Split(' ')[0]; } catch { return "?"; } }

        public static IEnumerator Run(string scene, string dir)
        {
            int frames = int.TryParse(Game.Arg("-lafBenchFrames") ?? "", out int f) ? f : 400;
            float timeScale = Time.timeScale;
            Time.timeScale = 0f;
            Debug.Log($"[Fidelity] {scene}: holding still at {Screen.width}x{Screen.height}, vSync {QualitySettings.vSyncCount}, target {Application.targetFrameRate} fps, " +
                      $"{SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}), load {Load()}");
            int n = FidelityStep.Count;
            var medians = new List<float>[n];
            var p95 = new List<float>[n];
            for (int i = 0; i < n; i++) { medians[i] = new List<float>(); p95[i] = new List<float>(); }
            var order = Enumerable.Range(0, n).Concat(Enumerable.Range(0, n).Reverse()).ToArray();
            var shot = new bool[n];
            foreach (int level in order)
            {
                GraphicsQuality.BenchLevel = level;
                GraphicsQuality.I?.Apply();
                for (int i = 0; i < 40; i++) yield return null;   // settle: render targets, the reflection probe, eased post values
                var times = new List<float>(frames);
                float last = Time.realtimeSinceStartup;
                for (int i = 0; i < frames; i++)
                {
                    yield return null;
                    float now = Time.realtimeSinceStartup;
                    times.Add((now - last) * 1000f);
                    last = now;
                }
                times.Sort();
                float med = times[times.Count / 2], q95 = times[Mathf.Min(times.Count - 1, (int)(times.Count * 0.95f))];
                medians[level].Add(med);
                p95[level].Add(q95);
                Debug.Log($"[Fidelity] {scene} {FidelityStep.At(level).name}: median {med:0.00} ms, p95 {q95:0.00} ms over {frames} frames, load {Load()}");
                if (!shot[level] && Game.Arg("-lafNoShots") == null)
                {
                    shot[level] = true;
                    ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"fidelity_{scene}_{level}_{FidelityStep.At(level).name.ToLowerInvariant()}.png"));
                    yield return null;
                    yield return null;
                }
            }
            for (int i = 0; i < n; i++)
            {
                float m = medians[i].Average();
                Debug.Log($"[Fidelity] {scene} summary {FidelityStep.At(i).name}: {m:0.00} ms ({1000f / m:0} fps), p95 {p95[i].Average():0.00} ms, " +
                          $"passes {string.Join(" / ", medians[i].Select(x => x.ToString("0.00")))} ms");
            }
            GraphicsQuality.BenchLevel = -1;
            GraphicsQuality.I?.Apply();
            for (int i = 0; i < 10; i++) yield return null;
            Time.timeScale = timeScale;
        }
    }
}
