using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LostAndFound
{
    /// <summary>
    /// -lafHitches: logs every frame that took longer than 100 ms of real time (and counts those over 50), with what was going on (the day, the claim,
    /// the phase, the view, what's in your hands, the AutoPilot's last step), and on the way out a summary: how many frames
    /// went over 100 and 150 ms, and the worst. Lives across the game's rebuilds. With the AutoPilot, -lafNoShots leaves out
    /// its screenshots, which stall a frame themselves.
    /// </summary>
    public class HitchLog : MonoBehaviour
    {
        static HitchLog instance;
        static string lastStep = "start";
        readonly List<(float ms, string what)> hitches = new();
        int frames, over50;
        float lastRealtime = -1f, maxMs;
        string maxWhat = "";

        public static void Ensure()
        {
            if (instance != null) return;
            var go = new GameObject("HitchLog") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            instance = go.AddComponent<HitchLog>();
        }

        /// <summary>What the AutoPilot just did (its screenshot names), for context.</summary>
        public static void Note(string step) => lastStep = step;

        void Update()
        {
            // real time between frames, unaffected by -lafSpeed
            float now = Time.realtimeSinceStartup;
            float ms = lastRealtime < 0f ? 0f : (now - lastRealtime) * 1000f;
            lastRealtime = now;
            frames++;
            if (frames < 10 || ms <= 50f) return;
            over50++;
            if (ms <= 100f && ms <= maxMs) return;
            var d = Director.I;
            string held = InspectController.I != null && InspectController.I.Held != null ? InspectController.I.Held.def.id : "-";
            string what = d == null ? "no game" : $"day {d.Day} claim {d.Current?.id ?? "-"} {d.PhaseName}, view {CameraRig.I?.view}, holding {held}, modal {UIRoot.ModalOpen}, title {TitleScreen.Showing}, after \"{lastStep}\"";
            if (ms > maxMs) { maxMs = ms; maxWhat = what; }
            if (ms <= 100f) return;
            hitches.Add((ms, what));
            Debug.Log($"[Hitch] {ms:0} ms at frame {Time.frameCount}: {what}");
        }

        void OnApplicationQuit()
        {
            var worst = hitches.OrderByDescending(h => h.ms).Take(12).ToList();
            Debug.Log($"[Hitch] summary: {frames} frames, {over50} over 50 ms, {hitches.Count} over 100 ms, {hitches.Count(h => h.ms > 150f)} over 150 ms, {hitches.Count(h => h.ms > 300f)} over 300 ms");
            Debug.Log($"[Hitch] the slowest frame: {maxMs:0} ms, {maxWhat}");
            foreach (var h in worst) Debug.Log($"[Hitch]   {h.ms:0} ms: {h.what}");
        }
    }
}
