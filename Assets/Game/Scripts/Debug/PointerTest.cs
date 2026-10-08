using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LostAndFound
{
    /// <summary>
    /// -lafPointerTest &lt;phase file&gt;: the real pointer at the window's sides. Unlike EdgeTest there's no virtual mouse here:
    /// Tools/unity.sh pointertest moves the headless KWin's own pointer (Tools/fakeptr.c), so the game sees what it would
    /// from a mouse, wl_pointer enter, motion and leave included. The driver writes "&lt;n&gt; &lt;name&gt; &lt;turn|still|any&gt;" to the
    /// phase file before each move, and "&lt;n&gt; judge -" when its hold is over. At each new phase the desk is put back to the
    /// counter; at "judge", "turn" phases (resting inside the window at its side) must have turned the desk, "still" ones
    /// (leaving the window by its side and staying out) must not, and "any" ones are only logged. "&lt;n&gt; end -" finishes the
    /// run with "[PointerTest] PASS" or FAIL.
    /// </summary>
    public class PointerTest : MonoBehaviour
    {
        string phaseFile, phase, expect;
        int phaseNo = -1, events;
        float phaseStart;
        View viewAtStart;
        readonly List<string> turns = new();
        readonly List<string> trail = new();   // the pointer's last few positions in this phase, as the game read them
        readonly List<string> failed = new();
        int checks;
        Vector2 lastPos = new(float.NaN, float.NaN);
        bool ready;

        void Start()
        {
            Application.runInBackground = true;
            phaseFile = Game.Arg("-lafPointerTest");
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            var d = Director.I;
            d.autoAdvance = true;   // past the morning, to the bell: the counter view, nothing open, free to turn
            float end = Time.realtimeSinceStartup + 90f;
            while (!(d.CanRing && !UIRoot.ModalOpen) && Time.realtimeSinceStartup < end) yield return null;
            if (!d.CanRing) { Debug.Log("[PointerTest] FAIL: never reached the bell"); Application.Quit(); yield break; }
            yield return new WaitForSecondsRealtime(1f);
            CameraRig.I.ViewChanged += v => turns.Add($"{v} at {Time.realtimeSinceStartup - phaseStart:0.00}s with the pointer at {Where(lastPos)}");
            Debug.Log($"[PointerTest] ready {Screen.width}x{Screen.height}, edge turning {(Settings.EdgeTurn ? "on" : "off")}, mice: {string.Join(", ", MouseNames())}");
            ready = true;
        }

        static string Where(Vector2 p) => float.IsNaN(p.x) ? "none" : $"({p.x:0.##},{p.y:0.##})";

        static IEnumerable<string> MouseNames()
        {
            foreach (var dev in InputSystem.devices) if (dev is Mouse) yield return dev.name;
        }

        void Update()
        {
            if (!ready) return;
            if (Mouse.current != null)
            {
                var p = Mouse.current.position.ReadValue();
                if (p != lastPos)
                {
                    events++; lastPos = p;
                    trail.Add($"{Where(p)} at {Time.realtimeSinceStartup - phaseStart:0.00}s");
                    if (trail.Count > 3) trail.RemoveAt(0);
                }
            }
            if (Time.frameCount % 6 != 0) return;
            string line = null;
            try { if (File.Exists(phaseFile)) line = File.ReadAllText(phaseFile).Trim(); } catch (IOException) { }
            if (string.IsNullOrEmpty(line)) return;
            var parts = line.Split(' ');
            if (parts.Length < 3 || !int.TryParse(parts[0], out int n) || n == phaseNo) return;
            phaseNo = n;
            if (parts[1] == "judge") { Judge(); phase = null; return; }   // the hold is over: judge it before the pointer goes home
            Judge();
            phase = parts[1]; expect = parts[2];
            if (phase == "end")
            {
                Debug.Log(failed.Count == 0 ? $"[PointerTest] PASS ({checks} phases)" : $"[PointerTest] FAIL: {string.Join(", ", failed)}");
                Application.Quit();
                ready = false;
                return;
            }
            if (CameraRig.I.view != View.Counter) CameraRig.I.SetView(View.Counter, true);
            viewAtStart = CameraRig.I.view;
            turns.Clear();
            trail.Clear();
            events = 0;
            phaseStart = Time.realtimeSinceStartup;
        }

        void Judge()
        {
            if (phase == null || expect == "-") return;
            bool turned = turns.Count > 0;
            string line = $"{phase}: {(turned ? "turned " + string.Join(", ", turns) : "no turn")}; {events} pointer positions read, " +
                          $"the last {string.Join(", ", trail)} ({Screen.width}x{Screen.height}, focused {Application.isFocused}, taken as gone {CameraRig.I.PointerOut})";
            if (expect == "any") { Debug.Log($"[PointerTest] note {line}"); return; }
            checks++;
            bool ok = turned == (expect == "turn");
            if (ok) Debug.Log($"[PointerTest] ok   {line}");
            else { Debug.Log($"[PointerTest] FAIL {line}: expected {(expect == "turn" ? "a turn" : "no turn")}"); failed.Add(phase); }
        }
    }
}
