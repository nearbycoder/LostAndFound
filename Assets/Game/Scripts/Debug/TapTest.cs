using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace LostAndFound
{
    /// <summary>
    /// -lafTapTest &lt;dir&gt;: plays Monday's first case with quick taps, each press and its release queued together so
    /// they reach the game in the same input update, with nothing held across a frame. That's a touchpad's
    /// tap-to-click, or an ordinary click when frames are slow. Fresh virtual devices tap keys (the bell, a nudge, the
    /// drawers, the tray, the pause menu), the mouse (a drawer, the wallet, a menu button) and a gamepad (a nudge, the
    /// stamp, the slip). A tap that does nothing is logged as MISSED and repeated as a held press, so the run carries
    /// on and lists every miss. Logs "[TapTest] PASS" when every tap worked.
    /// </summary>
    public class TapTest : MonoBehaviour
    {
        Keyboard kb;
        Mouse mouse;
        Gamepad pad;
        GamepadState st;
        Vector2 mousePos;
        string dir;
        int shots;
        readonly List<string> missed = new();
        int checks;
        readonly Dictionary<string, int> eventFrame = new();

        void Start()
        {
            dir = Game.Arg("-lafTapTest");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Application.persistentDataPath, "taptest");
            Directory.CreateDirectory(dir);
            Application.runInBackground = true;
            kb = InputSystem.AddDevice<Keyboard>("TapKeyboard");
            mouse = InputSystem.AddDevice<Mouse>("TapMouse");
            pad = InputSystem.AddDevice<Gamepad>("TapPad");
            InputSystem.onEvent += OnEvent;
            StartCoroutine(Run());
        }

        void OnDestroy()
        {
            InputSystem.onEvent -= OnEvent;
            foreach (InputDevice d in new InputDevice[] { kb, mouse, pad }) if (d != null && d.added) InputSystem.RemoveDevice(d);
        }

        // which frame each of our devices' events was processed in, to prove a tap's press and release arrived together
        void OnEvent(InputEventPtr e, InputDevice d)
        {
            if (d == kb || d == mouse || d == pad) eventFrame[d.name] = Time.frameCount;
        }

        void Shot(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"{shots++:00}_{name}.png"));

        static IEnumerator Hold(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            int f0 = Time.frameCount;
            while (Time.realtimeSinceStartup < end || Time.frameCount - f0 < 3) yield return null;
        }

        static IEnumerator Until(System.Func<bool> cond, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!cond() && Time.realtimeSinceStartup < end) yield return null;
        }

        // ---- taps: the press and the release queued in the same frame, so one input update carries both ----

        void QueueKeyTap(Key k)
        {
            kb.MakeCurrent();
            InputSystem.QueueStateEvent(kb, new KeyboardState(k));
            InputSystem.QueueStateEvent(kb, new KeyboardState());
        }

        IEnumerator HoldKey(Key k)
        {
            kb.MakeCurrent();
            InputSystem.QueueStateEvent(kb, new KeyboardState(k));
            yield return Hold(0.12f);
            InputSystem.QueueStateEvent(kb, new KeyboardState());
            yield return Hold(0.2f);
        }

        MouseState MouseAt(Vector2 p, bool left) => new MouseState { position = p, delta = p - mousePos }.WithButton(MouseButton.Left, left);

        IEnumerator MoveMouse(Vector2 p)
        {
            mouse.MakeCurrent();
            InputSystem.QueueStateEvent(mouse, MouseAt(p, false));
            mousePos = p;
            yield return Hold(0.25f);   // let hover catch up, as a hand would pause
        }

        void QueueMouseTap()
        {
            mouse.MakeCurrent();
            InputSystem.QueueStateEvent(mouse, MouseAt(mousePos, true));
            InputSystem.QueueStateEvent(mouse, MouseAt(mousePos, false));
        }

        IEnumerator HoldMouse()
        {
            mouse.MakeCurrent();
            InputSystem.QueueStateEvent(mouse, MouseAt(mousePos, true));
            yield return Hold(0.12f);
            InputSystem.QueueStateEvent(mouse, MouseAt(mousePos, false));
            yield return Hold(0.2f);
        }

        void QueuePadTap(GamepadButton b)
        {
            pad.MakeCurrent();
            InputSystem.QueueStateEvent(pad, Down(b));
            InputSystem.QueueStateEvent(pad, st);
        }

        // WithButton changes the struct it's called on, so press a copy and leave st as it was
        GamepadState Down(GamepadButton b)
        {
            var copy = st;
            return copy.WithButton(b, true);
        }

        IEnumerator HoldPad(GamepadButton b)
        {
            pad.MakeCurrent();
            InputSystem.QueueStateEvent(pad, Down(b));
            yield return Hold(0.12f);
            InputSystem.QueueStateEvent(pad, st);
            yield return Hold(0.2f);
        }

        /// <summary>Tap, and see whether it did its job. If not, note the miss and press properly so the run carries on.</summary>
        IEnumerator Check(string what, string device, System.Action tap, System.Func<IEnumerator> held, System.Func<bool> worked, float timeout = 3f)
        {
            checks++;
            int f0 = Time.frameCount;
            eventFrame.Remove(device);
            tap();
            yield return Until(worked, timeout);
            bool ok = worked();
            Debug.Log($"[TapTest] {what}: {(ok ? "ok" : "MISSED")} (tapped at frame {f0}, press and release both processed in frame {(eventFrame.TryGetValue(device, out int f) ? f : -1)})");
            if (ok) yield break;
            missed.Add(what);
            yield return held();
            yield return Until(worked, timeout);
            if (!worked()) Debug.Log($"[TapTest]   a held press didn't do it either: {what}");
        }

        IEnumerator SteerPad(System.Func<Vector2> target, float timeout = 8f)
        {
            float end = Time.realtimeSinceStartup + timeout;
            pad.MakeCurrent();
            while (Time.realtimeSinceStartup < end)
            {
                Vector2 d = target() - GamepadInput.CursorPos;
                if (d.magnitude < 4f) break;
                float m = Mathf.Clamp01(d.magnitude / 260f);
                st.leftStick = d.normalized * (0.15f + 0.85f * Mathf.Sqrt(m));
                InputSystem.QueueStateEvent(pad, st);
                yield return null;
            }
            st.leftStick = Vector2.zero;
            InputSystem.QueueStateEvent(pad, st);
            yield return Hold(0.1f);
        }

        IEnumerator Run()
        {
            var d = Director.I;
            yield return Until(() => d != null && d.Running, 20f);
            for (float end = Time.realtimeSinceStartup + 90f; !d.CanRing && Time.realtimeSinceStartup < end;)
            {
                yield return HoldKey(Key.Space);
                yield return new WaitForSecondsRealtime(0.4f);
            }
            if (!d.CanRing) { Debug.Log("[TapTest] FAIL: never reached the bell"); Application.Quit(); yield break; }

            yield return Check("key tap (Space) rings the bell", "TapKeyboard", () => QueueKeyTap(Key.Space), () => HoldKey(Key.Space), () => !d.CanRing);
            for (float end = Time.realtimeSinceStartup + 90f; !d.CanUseStamps && Time.realtimeSinceStartup < end;)
            {
                yield return HoldKey(Key.Space);
                yield return new WaitForSecondsRealtime(0.5f);
            }
            int n0 = d.NudgesTaken;
            yield return Check("key tap (H) asks Agnes for a nudge", "TapKeyboard", () => QueueKeyTap(Key.H), () => HoldKey(Key.H), () => d.NudgesTaken > n0);
            yield return Check("key tap (A) turns to the drawers", "TapKeyboard", () => QueueKeyTap(Key.A), () => HoldKey(Key.A), () => CameraRig.I.view == View.Cabinet);
            yield return new WaitForSecondsRealtime(1.0f);

            var drawer = Desk.I.drawers["A"];
            yield return MoveMouse(GamepadTest.AimPoint(drawer));
            yield return Check("mouse tap opens drawer A", "TapMouse", QueueMouseTap, HoldMouse, () => drawer.IsOpen);
            yield return new WaitForSecondsRealtime(1.2f);
            var wallet = Desk.I.items["wallet_brown"];
            yield return MoveMouse(GamepadTest.AimPoint(wallet));
            Shot("mouse_on_wallet");
            yield return Check("mouse tap picks up the wallet", "TapMouse", QueueMouseTap, HoldMouse, () => InspectController.I.Held == wallet && !InspectController.I.Busy, 4f);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Check("key tap (T) puts it on the tray", "TapKeyboard", () => QueueKeyTap(Key.T), () => HoldKey(Key.T), () => Desk.I.OnTray == wallet && InspectController.I.Held == null, 4f);
            yield return new WaitForSecondsRealtime(0.6f);

            int n1 = d.NudgesTaken;
            yield return Check("pad tap (d-pad left) takes control and asks Agnes", "TapPad", () => QueuePadTap(GamepadButton.DpadLeft), () => HoldPad(GamepadButton.DpadLeft), () => GamepadInput.Active && d.NudgesTaken > n1);
            var stamp = Desk.I.props.stamps.First(s => s.kind == Verdict.Return);
            yield return SteerPad(() => GamepadTest.AimPoint(stamp));
            yield return Check("pad tap (A) takes up the RETURN stamp", "TapPad", () => QueuePadTap(GamepadButton.South), () => HoldPad(GamepadButton.South), () => StampTool.I.Carrying == stamp);
            yield return SteerPad(() => Camera.main.WorldToScreenPoint(ClaimSlip.I.transform.position + new Vector3(0.0f, 0f, 0.03f)));
            yield return new WaitForSecondsRealtime(0.4f);
            Shot("pad_stamp_over_slip");
            yield return Check("pad tap (A) stamps the slip", "TapPad", () => QueuePadTap(GamepadButton.South), () => HoldPad(GamepadButton.South), () => StampTool.I.Carrying == null);
            for (float end = Time.realtimeSinceStartup + 40f; d.State.Record("1.1") == null && Time.realtimeSinceStartup < end;)
            {
                yield return new WaitForSecondsRealtime(0.6f);
                yield return HoldPad(GamepadButton.DpadDown);
            }
            var rec = d.State.Record("1.1");
            Debug.Log($"[TapTest] case 1.1: {(rec == null ? "not decided" : $"{rec.verdict}->{rec.to} ({rec.grade})")}");
            yield return new WaitForSecondsRealtime(1.5f);

            // the mouse takes back over, then the pause menu: a key tap opens it, a mouse tap on a button closes it
            yield return MoveMouse(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            yield return Until(() => !GamepadInput.Active, 2f);
            yield return Until(() => d.CanRing, 40f);
            yield return Check("key tap (Esc) pauses", "TapKeyboard", () => QueueKeyTap(Key.Escape), () => HoldKey(Key.Escape), () => PauseMenu.Open);
            yield return new WaitForSecondsRealtime(0.3f);
            var back = UIRoot.I.root.GetComponentsInChildren<PaperButton>().FirstOrDefault(b => b.name == "Back to the desk");
            if (back != null)
            {
                var corners = new Vector3[4];
                ((RectTransform)back.transform).GetWorldCorners(corners);
                yield return MoveMouse((corners[0] + corners[2]) * 0.5f);
                Shot("mouse_on_menu_button");
                yield return Check("mouse tap on a menu button (Back to the desk)", "TapMouse", QueueMouseTap, HoldMouse, () => !PauseMenu.Open);
            }
            else { checks++; missed.Add("couldn't find the pause menu's button"); }

            bool ok = missed.Count == 0 && rec != null && rec.verdict == "return" && rec.to == "walter";
            Debug.Log($"[TapTest] {checks - missed.Count} of {checks} taps worked" + (missed.Count > 0 ? "; missed: " + string.Join("; ", missed) : ""));
            Debug.Log(ok ? "[TapTest] PASS" : "[TapTest] FAIL");
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit();
        }
    }
}
