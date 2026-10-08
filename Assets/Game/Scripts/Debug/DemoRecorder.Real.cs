using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using Debug = UnityEngine.Debug;

namespace LostAndFound
{
    /// <summary>
    /// -lafPlay -lafRealInput &lt;file&gt;: the whole-day play, with real input. Every other input test queues events on virtual
    /// Input System devices inside the game; here the play's pointer moves, clicks, drags and key presses are written to
    /// &lt;file&gt;, and Tools/unity.sh realplay has the headless KWin's own pointer and keyboard do them (Tools/fakeptr.c), so the
    /// game reads them as it would a mouse and keyboard: from the compositor, through SDL. Nothing is recorded. On the first
    /// claim H, Tab and Esc are pressed too, and the first object held is brought closer and back with the wheel, put down
    /// with a right click and picked up again. Each time the play has to do something directly because the input didn't (a
    /// bell that didn't ring, a pick-up or stamp that missed) it's logged as "[RealInput] fallback"; a hidden detail its
    /// turning search didn't bring into view is listed apart. Ends with "[RealInput] PASS" when no input needed a fallback
    /// and every claim got the verdict the play meant to give.
    ///
    /// Lines written: "m X Y" (pointer at window pixel X, Y, from the bottom left as Unity counts), "b 1|0" (left button),
    /// "r 1|0" (right button), "k CODE 1|0" (a key, by its Linux evdev code), "w V" (the wheel; negative brings it closer).
    /// </summary>
    public partial class DemoRecorder
    {
        string realInput;
        StreamWriter realOut;
        Vector2 realSent = new(float.NaN, float.NaN);
        bool realLeft;
        int fallbacks, hoverMisses, realLines;
        bool wheelChecked;
        readonly List<string> realProblems = new();
        /// <summary>Details the play's turning search didn't bring into view (not the input's doing: see FinishReal).</summary>
        readonly List<string> searchMisses = new();

        void OpenRealInput()
        {
            realOut = new StreamWriter(new FileStream(realInput, FileMode.Create, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
            Debug.Log($"[RealInput] ready {Screen.width}x{Screen.height}: input goes to {realInput}");
        }

        /// <summary>The driver writes &lt;file&gt;.go once it knows where the window is and is reading the file.</summary>
        IEnumerator WhenTheDriverIsReady()
        {
            float end = Time.realtimeSinceStartup + 90f;
            while (!File.Exists(realInput + ".go") && Time.realtimeSinceStartup < end) yield return null;
            if (!File.Exists(realInput + ".go")) { Debug.Log("[RealInput] FAIL: the driver never said go"); Application.Quit(); yield break; }
            Debug.Log("[RealInput] the driver is ready: playing");
            StartPlay();
        }

        void Emit(string line)
        {
            if (realOut == null) return;
            realOut.WriteLine(line);
            realLines++;
        }

        /// <summary>Each frame: where the play wants the pointer and the left button, sent when they change.</summary>
        void SendReal()
        {
            Vector2 p = new(Mathf.Round(pos.x * 4f) / 4f, Mathf.Round(pos.y * 4f) / 4f);
            if (p != realSent)
            {
                Emit($"m {p.x:0.##} {p.y:0.##}");
                realSent = p;
            }
            if (leftHeld != realLeft)
            {
                Emit($"b {(leftHeld ? 1 : 0)}");
                realLeft = leftHeld;
            }
        }

        void RealKey(Key k, bool down)
        {
            int code = EvdevCode(k);
            if (code == 0) { realProblems.Add($"no key code for {k}"); return; }
            Emit($"k {code} {(down ? 1 : 0)}");
        }

        /// <summary>The Linux key code of each key the play presses (where the key sits, as Unity's Key names it).</summary>
        static int EvdevCode(Key k) => k switch
        {
            Key.Escape => 1, Key.Backspace => 14, Key.Tab => 15, Key.Enter => 28, Key.Space => 57,
            Key.A => 30, Key.D => 32, Key.H => 35, Key.L => 38, Key.R => 19, Key.T => 20,
            Key.LeftArrow => 105, Key.RightArrow => 106,
            _ => 0,
        };

        void Fallback(string what)
        {
            fallbacks++;
            Debug.LogWarning($"[{(realInput != null ? "RealInput" : "Demo")}] fallback: {what}");
        }

        /// <summary>The first object held: the wheel brings it closer and back, and a right click puts it down; a click takes it
        /// up again. The play itself uses neither.</summary>
        IEnumerator WheelAndRightClick(ItemView item)
        {
            wheelChecked = true;
            var insp = InspectController.I;
            yield return Glide(() => new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), 0.4f);
            yield return Hold(0.3f);
            float z0 = insp.Zoom;
            for (int i = 0; i < 3; i++) { Emit("w -10"); yield return Hold(0.15f); }
            yield return Hold(0.3f);
            float z1 = insp.Zoom;
            for (int i = 0; i < 3; i++) { Emit("w 10"); yield return Hold(0.15f); }
            yield return Hold(0.3f);
            float z2 = insp.Zoom;
            bool wheel = z1 > z0 + 0.2f && z2 < z1 - 0.2f;
            Debug.Log($"[RealInput] {(wheel ? "ok  " : "FAIL")} the wheel: the {item.def.id} brought closer and back, zoom {z0:0.00} -> {z1:0.00} -> {z2:0.00}");
            if (!wheel) realProblems.Add("the wheel");

            Emit("r 1");
            yield return Hold(0.1f);
            Emit("r 0");
            yield return Until(() => insp.Held == null && !insp.Busy, 3f);
            bool down = insp.Held == null && item.place == ItemPlace.Mat;
            Debug.Log($"[RealInput] {(down ? "ok  " : "FAIL")} a right click puts the {item.def.id} down (it's {item.place})");
            if (!down) { realProblems.Add("right click"); yield break; }
            yield return Hold(0.6f);
            yield return GlideTo(item, 0.6f);
            yield return Hold(0.3f);
            yield return Click();
            yield return Until(() => insp.Held == item && !insp.Busy, 4f);
            bool again = insp.Held == item;
            Debug.Log($"[RealInput] {(again ? "ok  " : "FAIL")} a click picks the {item.def.id} up again from the desk");
            if (!again) { Fallback($"picking the {item.def.id} up again missed; picking up directly"); insp.Begin(item); yield return Until(() => insp.Held == item && !insp.Busy, 4f); }
        }

        bool keysChecked;

        /// <summary>On the first claim, keys the play doesn't otherwise press: H for a nudge, Tab to read the slip and put it
        /// back, Esc to pause and Esc again to carry on.</summary>
        IEnumerator KeysCheck(Director d)
        {
            keysChecked = true;
            yield return Hold(0.5f);
            int n = d.NudgesTaken;
            yield return Press(Key.H);
            yield return Until(() => d.NudgesTaken > n, 3f);
            Check(d.NudgesTaken > n, "H asks Agnes for a nudge");
            yield return Hold(0.8f);
            yield return Press(Key.Tab);
            yield return Until(() => ClaimSlip.I.Focused, 3f);
            Check(ClaimSlip.I.Focused, "Tab reads the slip");
            yield return Hold(1.0f);
            yield return Press(Key.Tab);
            yield return Until(() => !ClaimSlip.I.Focused, 3f);
            Check(!ClaimSlip.I.Focused, "Tab again puts it back");
            yield return Hold(0.6f);
            // the pause stops game time, which the play's own waits count in: these wait in real time
            yield return PressRealtime(Key.Escape);
            yield return UntilRealtime(() => PauseMenu.Open, 3f);
            Check(PauseMenu.Open, "Esc pauses");
            yield return new WaitForSecondsRealtime(0.8f);
            yield return PressRealtime(Key.Escape);
            yield return UntilRealtime(() => !PauseMenu.Open, 3f);
            Check(!PauseMenu.Open, "Esc again carries on");
            yield return Hold(0.6f);
        }

        IEnumerator PressRealtime(Key k)
        {
            RealKey(k, true);
            yield return new WaitForSecondsRealtime(0.1f);
            RealKey(k, false);
            yield return new WaitForSecondsRealtime(0.1f);
        }

        static IEnumerator UntilRealtime(System.Func<bool> cond, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!cond() && Time.realtimeSinceStartup < end) yield return null;
        }

        void Check(bool ok, string what)
        {
            Debug.Log($"[RealInput] {(ok ? "ok  " : "FAIL")} {what}");
            if (!ok) realProblems.Add(what);
        }

        void FinishReal()
        {
            var d = Director.I;
            int decided = 0, meant = 0;
            if (d != null)
                foreach (var r in d.State.records)
                {
                    if (r.grade == "skip") continue;
                    decided++;
                    if (r.grade == "best") meant++;
                }
            Debug.Log($"[RealInput] {realLines} input lines sent; {decided} claims decided, {meant} as the play meant (best); details found by hand {detailsByHand}, " +
                      $"by the fallback {detailsFallback}; {fallbacks} input fallback(s), {hoverMisses} hover miss(es)");
            foreach (var p in realProblems) Debug.Log($"[RealInput] problem: {p}");
            // The input passes when everything it was asked to do, it did. A detail the play's turning search didn't bring into
            // view is the search's luck, not the input's (a drag turns the object by what it should: "[Demo] drag by" lines), so
            // it's listed rather than failed.
            bool pass = fallbacks == 0 && realProblems.Count == 0 && wheelChecked && keysChecked && decided > 0 && meant == decided;
            if (searchMisses.Count > 0) Debug.Log($"[RealInput] details the search couldn't bring into view (found directly): {string.Join(", ", searchMisses)}");
            Debug.Log(pass ? "[RealInput] PASS" : "[RealInput] FAIL");
            if (realOut != null) { Emit("end"); realOut.Close(); }
            markers?.Close();
            Application.Quit();
        }
    }
}
