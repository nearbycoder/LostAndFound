using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace LostAndFound
{
    /// <summary>
    /// -lafGamepadTest &lt;dir&gt;: plays Monday's first case with nothing but a virtual Input System gamepad. It steers
    /// the drawn cursor with the left stick and turns the wallet with the right, and presses A, LB, X and the d-pad as
    /// a player would. It counts keyboard and mouse events from any other device (there should be none), screenshots
    /// the gamepad cursor along the way into &lt;dir&gt;, and logs "[PadTest] PASS" if the case ends RETURN to Walter.
    /// </summary>
    public class GamepadTest : MonoBehaviour
    {
        Gamepad pad;
        GamepadState st;
        string dir;
        int shots, foreign;

        void Start()
        {
            dir = Game.Arg("-lafGamepadTest");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Application.persistentDataPath, "padtest");
            Directory.CreateDirectory(dir);
            Application.runInBackground = true;
            pad = InputSystem.AddDevice<Gamepad>("TestPad");
            InputSystem.onEvent += OnEvent;
            StartCoroutine(Run());
        }

        void OnDestroy()
        {
            InputSystem.onEvent -= OnEvent;
            if (pad != null) InputSystem.RemoveDevice(pad);
        }

        void OnEvent(UnityEngine.InputSystem.LowLevel.InputEventPtr e, InputDevice d)
        {
            if (d == pad || d.name.StartsWith("PadMouse")) return;
            if ((d is Keyboard || d is Mouse) && (e.IsA<StateEvent>() || e.IsA<DeltaStateEvent>()))
            {
                // the window opening sends the real mouse's state once; what counts is anything once play begins
                bool playing = GamepadInput.Active;
                if (playing) foreign++;
                Debug.Log($"[PadTest] {(playing ? "" : "(before play) ")}event from {d.name} at {Time.realtimeSinceStartup:0.00}s (frame {Time.frameCount})");
            }
        }

        void Update()
        {
            pad.MakeCurrent();
            InputSystem.QueueStateEvent(pad, st);
        }

        void Shot(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"{shots++:00}_{name}.png"));

        /// <summary>Press and release a button. Held for a few frames as well as a little time, so the game sees it even
        /// when the machine is busy and a frame takes longer than the press (the test's presses used to go missing then).</summary>
        IEnumerator Press(GamepadButton b, float hold = 0.12f)
        {
            st = st.WithButton(b, true);
            yield return Hold(hold);
            st = st.WithButton(b, false);
            yield return Hold(0.2f);
        }

        static IEnumerator Hold(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            int f0 = Time.frameCount;
            while (Time.realtimeSinceStartup < end || Time.frameCount - f0 < 3) yield return null;
        }

        /// <summary>Push the left stick towards a screen point until the cursor sits on it (a player's thumb, closed loop).</summary>
        IEnumerator SteerTo(System.Func<Vector2> target, float timeout = 8f)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (Time.realtimeSinceStartup < end)
            {
                Vector2 d = target() - GamepadInput.CursorPos;
                if (d.magnitude < 4f) break;
                float m = Mathf.Clamp01(d.magnitude / 260f);
                st.leftStick = d.normalized * (0.15f + 0.85f * Mathf.Sqrt(m));   // undo the stick's response curve
                yield return null;
            }
            st.leftStick = Vector2.zero;
            yield return null;
            yield return null;
        }

        static Vector2 Screen2(Vector3 w) => Camera.main.WorldToScreenPoint(w);

        /// <summary>The visible point nearest an interactable's middle where a click would land on it.</summary>
        internal static Vector2 AimPoint(Interactable t)
        {
            var rs = t.HighlightRenderers;
            var b = rs[0].bounds;
            foreach (var r in rs) if (r != null) b.Encapsulate(r.bounds);
            Vector2 centre = Screen2(b.center), best = centre;
            float bestD = float.MaxValue;
            for (int ix = 0; ix <= 6; ix++)
                for (int iy = 0; iy <= 6; iy++)
                    for (int iz = 0; iz <= 2; iz++)
                    {
                        Vector2 s = Screen2(b.min + Vector3.Scale(b.size, new Vector3(0.1f + 0.8f * ix / 6f, 0.1f + 0.8f * iy / 6f, 0.1f + 0.8f * iz / 2f)));
                        float d = (s - centre).sqrMagnitude;
                        if (d < bestD && InteractionSystem.I.PickAt(s, out _) == t) { bestD = d; best = s; }
                    }
            return best;
        }

        static bool Facing(ItemView item, ItemPart part)
        {
            var r = part.GetComponentInChildren<Renderer>();
            Vector3 c = r != null ? r.bounds.center : part.transform.position;
            var ray = Camera.main.ScreenPointToRay(Screen2(c));
            var hits = Physics.RaycastAll(ray, 2f).Where(h => h.collider.transform.IsChildOf(item.transform)).OrderBy(h => h.distance).ToArray();
            return hits.Length > 0 && hits[0].collider.transform.IsChildOf(part.transform);
        }

        /// <summary>Nudge the right stick to bring the hotspot's outward side round to the eye.</summary>
        IEnumerator TurnToward(Transform hs, float lean)
        {
            var cam = Camera.main.transform;
            Vector3 n = cam.InverseTransformDirection(hs.up);
            float yaw = Mathf.Atan2(n.x, -n.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(n.y, new Vector2(n.x, n.z).magnitude) * Mathf.Rad2Deg + lean;
            st.rightStick = new Vector2(Mathf.Abs(yaw) > 8f ? -Mathf.Sign(yaw) * Mathf.Clamp01(Mathf.Abs(yaw) / 90f + 0.35f) : 0f,
                                        Mathf.Abs(pitch) > 8f ? Mathf.Sign(pitch) * Mathf.Clamp01(Mathf.Abs(pitch) / 90f + 0.35f) : 0f);
            yield return new WaitForSecondsRealtime(0.18f);
            st.rightStick = Vector2.zero;
            yield return new WaitForSecondsRealtime(0.5f);
        }

        IEnumerator Until(System.Func<bool> cond, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!cond() && Time.realtimeSinceStartup < end) yield return null;
        }

        IEnumerator Fail(string why)
        {
            Debug.Log($"[PadTest] FAIL: {why} (other keyboard/mouse events: {foreign})");
            Debug.Log($"[PadTest]   at the time: pad active {GamepadInput.Active}, dialogue shown {UIRoot.I.dialogue.Shown} typing {UIRoot.I.dialogue.Typing} waiting {UIRoot.I.dialogue.Waiting}, note open {UIRoot.I.note.Open}, modal {UIRoot.ModalOpen}, frame {Time.frameCount}, {Time.realtimeSinceStartup:0}s");
            Shot("fail");
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit();
        }

        IEnumerator Run()
        {
            var d = Director.I;
            yield return Until(() => d != null && d.Running, 20f);
            // the morning: d-pad down moves Gus and Agnes's note along
            for (float end = Time.realtimeSinceStartup + 90f; !d.CanRing && Time.realtimeSinceStartup < end;)
            {
                yield return Press(GamepadButton.DpadDown);
                yield return new WaitForSecondsRealtime(0.4f);
            }
            if (!d.CanRing) { yield return Fail("never reached the bell"); yield break; }
            if (!GamepadInput.Active) { yield return Fail("the gamepad never took control"); yield break; }

            // ring with the cursor and A
            var bell = Desk.I.props.bell;
            yield return SteerTo(() => AimPoint(bell));
            Shot("cursor_on_bell");
            yield return Press(GamepadButton.South);
            yield return Until(() => !d.CanRing, 3f);
            if (d.CanRing) { yield return Fail("A on the bell didn't ring it"); yield break; }

            // Walter's description: d-pad down moves him along
            for (float end = Time.realtimeSinceStartup + 90f; !d.CanUseStamps && Time.realtimeSinceStartup < end;)
            {
                yield return Press(GamepadButton.DpadDown);
                yield return new WaitForSecondsRealtime(0.5f);
            }
            if (!d.CanUseStamps) { yield return Fail("the claimant never finished"); yield break; }

            // d-pad left: a nudge from Agnes, in gamepad words; again for the next one
            yield return Press(GamepadButton.DpadLeft);
            yield return Until(() => UIRoot.I.nudge.Shown, 2f);
            if (!UIRoot.I.nudge.Shown || d.NudgesTaken != 1) { yield return Fail("d-pad left didn't give a nudge"); yield break; }
            yield return Press(GamepadButton.DpadLeft);
            yield return Until(() => d.NudgesTaken == 2, 2f);
            string nudge = UIRoot.I.nudge.Text;
            Debug.Log($"[PadTest] d-pad left twice: {d.NudgesTaken} nudges, the second: {nudge}");
            if (d.NudgesTaken != 2 || !nudge.Contains("LB")) { yield return Fail("the second nudge didn't come, or doesn't name the pad's button"); yield break; }
            yield return new WaitForSecondsRealtime(0.6f);
            Shot("nudge");

            // LB: turn to the drawers; open A; pick up the wallet
            yield return Press(GamepadButton.LeftShoulder);
            yield return Until(() => CameraRig.I.view == View.Cabinet, 3f);
            yield return new WaitForSecondsRealtime(1.0f);
            var drawer = Desk.I.drawers["A"];
            yield return SteerTo(() => AimPoint(drawer));
            yield return Press(GamepadButton.South);
            yield return Until(() => drawer.IsOpen, 3f);
            yield return new WaitForSecondsRealtime(1.2f);
            var wallet = Desk.I.items["wallet_brown"];
            yield return SteerTo(() => AimPoint(wallet));
            yield return new WaitForSecondsRealtime(0.6f);
            Shot("cursor_on_wallet_tag");
            yield return Press(GamepadButton.South);
            yield return Until(() => InspectController.I.Held == wallet && !InspectController.I.Busy, 4f);
            if (InspectController.I.Held != wallet) { yield return Fail("A didn't pick up the wallet"); yield break; }
            yield return new WaitForSecondsRealtime(0.6f);

            // open it: the cursor to the flap, A (turning it with the right stick if the flap faces away)
            var flap = wallet.parts[0];
            for (int tries = 0; tries < 8 && !flap.open; tries++)
            {
                if (Facing(wallet, flap))
                {
                    var r = flap.GetComponentInChildren<Renderer>();
                    yield return SteerTo(() => Screen2(r.bounds.center));
                    yield return Press(GamepadButton.South);
                    yield return new WaitForSecondsRealtime(0.8f);
                }
                else
                {
                    st.rightStick = new Vector2(tries % 2 == 0 ? 0.8f : 0f, tries % 2 == 0 ? 0f : 0.8f);
                    yield return new WaitForSecondsRealtime(0.2f);
                    st.rightStick = Vector2.zero;
                    yield return new WaitForSecondsRealtime(0.5f);
                }
            }
            if (!flap.open) { yield return Fail("couldn't open the wallet"); yield break; }

            // the photo of Biscuit: turn it round with the right stick, then the cursor onto the glint and A
            var det = wallet.def.Detail("photo");
            var hs = wallet.Hotspot(det);
            float[] lean = { 0f, 18f, -18f, 30f, -30f, 10f, -10f, 40f };
            for (int tries = 0; tries < 24 && !d.IsDiscovered(wallet.def, det); tries++)
            {
                Vector2 sp = Screen2(hs.position);
                var near = InspectController.I.NearDetail(wallet, sp, false, out float px, out _);
                if (near == det && px <= InspectController.ClickRadius)
                {
                    yield return SteerTo(() => Screen2(hs.position));
                    yield return new WaitForSecondsRealtime(0.3f);
                    Shot("cursor_on_detail");
                    yield return Press(GamepadButton.South);
                    yield return new WaitForSecondsRealtime(1.2f);
                }
                else yield return TurnToward(hs, lean[tries % lean.Length]);
            }
            if (!d.IsDiscovered(wallet.def, det)) { yield return Fail("never found the photograph"); yield break; }
            Shot("photo_found");

            // X: on the tray. Then the RETURN stamp and the slip, with the cursor and A.
            yield return Press(GamepadButton.West);
            yield return Until(() => Desk.I.OnTray == wallet && InspectController.I.Held == null, 4f);
            if (Desk.I.OnTray != wallet) { yield return Fail("X didn't put it on the tray"); yield break; }
            yield return new WaitForSecondsRealtime(0.6f);
            var stamp = Desk.I.props.stamps.First(s => s.kind == Verdict.Return);
            yield return SteerTo(() => AimPoint(stamp));
            yield return Press(GamepadButton.South);
            yield return Until(() => StampTool.I.Carrying == stamp, 3f);
            if (StampTool.I.Carrying != stamp) { yield return Fail("A didn't take up the RETURN stamp"); yield break; }
            yield return SteerTo(() => Screen2(ClaimSlip.I.transform.position + new Vector3(0.0f, 0f, 0.03f)));
            yield return new WaitForSecondsRealtime(0.4f);
            Shot("stamp_over_slip");
            yield return Press(GamepadButton.South);
            Shot("stamp_down");
            // Walter's thanks: d-pad down moves him along until the verdict is in
            for (float end = Time.realtimeSinceStartup + 40f; d.State.Record("1.1") == null && Time.realtimeSinceStartup < end;)
            {
                yield return new WaitForSecondsRealtime(0.6f);
                yield return Press(GamepadButton.DpadDown);
            }
            var rec = d.State.Record("1.1");
            if (rec == null) { yield return Fail("the slip was never stamped"); yield break; }
            yield return new WaitForSecondsRealtime(1.5f);
            Shot("stamped");
            bool ok = rec.verdict == "return" && rec.to == "walter" && rec.grade == "best" && foreign == 0;
            Debug.Log($"[PadTest] case 1.1 decided with the gamepad: {rec.verdict}->{rec.to} ({rec.grade}); keyboard/mouse events during play: {foreign}");

            // and a mouse takes over the moment it moves
            InputSystem.onEvent -= OnEvent;
            var hand = InputSystem.AddDevice<Mouse>("HandMouse");
            InputSystem.QueueStateEvent(hand, new MouseState { position = new Vector2(800f, 450f), delta = new Vector2(40f, 10f) });
            yield return Until(() => !GamepadInput.Active, 2f);
            bool handedBack = !GamepadInput.Active && Cursor.visible;
            Debug.Log($"[PadTest] moving a mouse hands control back: {handedBack}");
            InputSystem.RemoveDevice(hand);
            ok &= handedBack;
            Debug.Log(ok ? "[PadTest] PASS" : "[PadTest] FAIL");
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit();
        }
    }
}
