using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace LostAndFound
{
    /// <summary>
    /// Playing with a gamepad. The left stick moves a cursor the game draws itself (it slows over anything you
    /// can click), and drives a virtual mouse: A and B are its left and right buttons, the triggers its scroll
    /// wheel. So everything that reads the mouse (picking, inspecting, the slip, the stamps, the menus) works
    /// unchanged. The other buttons stand in for keys through InputX: LB/RB turn, X the tray, Y the rules, View the
    /// slip, Menu pauses, the d-pad's up the blue lamp, down rings the bell or moves the dialogue on, left asks Agnes for a
    /// nudge. The right
    /// stick turns a held object. Touch the real mouse and it takes over again.
    /// </summary>
    [DefaultExecutionOrder(-2000)]   // before InputX samples the frame
    public class GamepadInput : MonoBehaviour
    {
        public static GamepadInput I { get; private set; }
        /// <summary>The gamepad is driving (the drawn cursor is up, prompts name pad buttons).</summary>
        public static bool Active { get; private set; }
        public static Vector2 CursorPos => I != null ? I.pos : Vector2.zero;
        public static Vector2 RightStick => Active && Gamepad.current != null ? Dead(Gamepad.current.rightStick.ReadValue()) : Vector2.zero;

        Mouse virtualMouse;
        Vector2 pos, lastSent;
        float scrollTimer;
        RawImage cursorImage;
        CursorKind cursorKind = (CursorKind)(-1);

        const float Speed = 1150f;          // pixels per second at full tilt, at 1080p

        void Awake()
        {
            I = this;
            var go = new GameObject("PadCursor");
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 31000;
            cursorImage = new GameObject("Cursor").AddComponent<RawImage>();
            cursorImage.transform.SetParent(go.transform, false);
            cursorImage.raycastTarget = false;
            cursorImage.rectTransform.anchorMin = cursorImage.rectTransform.anchorMax = Vector2.zero;
            cursorImage.enabled = false;
        }

        void OnDestroy()
        {
            if (virtualMouse != null) InputSystem.RemoveDevice(virtualMouse);
            if (Active) Cursor.visible = true;
            Active = false;
        }

        static Vector2 Dead(Vector2 v)
        {
            float m = v.magnitude;
            if (m < 0.15f) return Vector2.zero;
            float k = Mathf.InverseLerp(0.15f, 1f, Mathf.Min(m, 1f));
            return v / m * k * k;   // fine control near the centre, full speed at the rim
        }

        /// <summary>The buttons the game uses (InputX latches quick presses of these).</summary>
        public static UnityEngine.InputSystem.Controls.ButtonControl[] Buttons(Gamepad pad) => new[]
        {
            pad.buttonSouth, pad.buttonEast, pad.buttonWest, pad.buttonNorth, pad.leftShoulder, pad.rightShoulder,
            pad.startButton, pad.selectButton, pad.dpad.up, pad.dpad.down, pad.dpad.left, pad.dpad.right,
        };

        static bool Used(Gamepad pad)
        {
            if (pad.leftStick.ReadValue().sqrMagnitude > 0.09f || pad.rightStick.ReadValue().sqrMagnitude > 0.09f) return true;
            if (pad.leftTrigger.ReadValue() > 0.3f || pad.rightTrigger.ReadValue() > 0.3f) return true;
            foreach (var b in Buttons(pad))
                if (InputX.Pressed(b)) return true;
            return false;
        }

        /// <summary>A real mouse (any of them: a laptop has its touchpad as well) that has just moved or clicked.</summary>
        Mouse MovedMouse()
        {
            foreach (var d in InputSystem.devices)
                if (d is Mouse m && m != virtualMouse && m.added && !m.name.StartsWith("Demo")
                    && (m.delta.ReadValue().sqrMagnitude > 9f || InputX.Pressed(m.leftButton) || InputX.Pressed(m.rightButton))) return m;
            return null;
        }

        void Update()
        {
            var pad = Gamepad.current;
            if (!Active && pad != null && Used(pad)) Activate();
            if (Active)
            {
                var moved = MovedMouse();
                if (moved != null) { Deactivate(moved); return; }
                if (pad == null) { Deactivate(null); return; }
                Drive(pad);
            }
        }

        void Activate()
        {
            Active = true;
            virtualMouse ??= InputSystem.AddDevice<Mouse>("PadMouse");
            pos = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(Screen.width / 2f, Screen.height / 2f);
            if (pos == Vector2.zero) pos = new Vector2(Screen.width / 2f, Screen.height / 2f);
            lastSent = pos;
            Cursor.visible = false;
            cursorImage.enabled = true;
            Debug.Log("[Pad] gamepad in control");
        }

        void Deactivate(Mouse real)
        {
            Active = false;
            Cursor.visible = true;
            cursorImage.enabled = false;
            real?.MakeCurrent();
            Debug.Log("[Pad] mouse in control");
        }

        void Drive(Gamepad pad)
        {
            float dt = Time.unscaledDeltaTime;
            Vector2 s = Dead(pad.leftStick.ReadValue());
            // slow over anything clickable, so a hidden detail or a finding on the slip is easy to settle on
            bool overSomething = (InteractionSystem.I != null && InteractionSystem.I.Hovered != null) || CursorController.Shown is CursorKind.Magnifier or CursorKind.Hand;
            float speed = Speed * Screen.height / 1080f * (overSomething ? 0.45f : 1f);
            pos += s * speed * dt;
            pos = new Vector2(Mathf.Clamp(pos.x, 0f, Screen.width - 1f), Mathf.Clamp(pos.y, 0f, Screen.height - 1f));

            // the triggers lean in and out a notch at a time, like a scroll wheel
            float trig = pad.rightTrigger.ReadValue() - pad.leftTrigger.ReadValue();
            float scroll = 0f;
            scrollTimer -= dt;
            if (Mathf.Abs(trig) > 0.4f && scrollTimer <= 0f) { scroll = Mathf.Sign(trig) * 120f; scrollTimer = 0.12f; }
            if (Mathf.Abs(trig) <= 0.4f) scrollTimer = 0f;

            virtualMouse.MakeCurrent();
            var st = new MouseState { position = pos, delta = pos - lastSent, scroll = new Vector2(0f, scroll) }
                .WithButton(MouseButton.Left, InputX.Pressed(pad.buttonSouth))
                .WithButton(MouseButton.Right, InputX.Pressed(pad.buttonEast));
            InputSystem.QueueStateEvent(virtualMouse, st);
            lastSent = pos;
        }

        /// <summary>Gamepad buttons standing in for keys (read by InputX alongside the keyboard).</summary>
        public static bool KeyHeld(Key k)
        {
            var pad = Gamepad.current;
            if (pad == null || !Active) return false;
            return k switch
            {
                Key.A => InputX.Pressed(pad.leftShoulder),
                Key.D => InputX.Pressed(pad.rightShoulder),
                Key.T => InputX.Pressed(pad.buttonWest),
                Key.R => InputX.Pressed(pad.buttonNorth),
                Key.Tab => InputX.Pressed(pad.selectButton),
                Key.Escape => InputX.Pressed(pad.startButton),
                Key.L => InputX.Pressed(pad.dpad.up),
                Key.Space => InputX.Pressed(pad.dpad.down),
                Key.H => InputX.Pressed(pad.dpad.left),
                _ => false,
            };
        }

        /// <summary>A prompt in whichever words fit the hands on the controls.</summary>
        public static string Prompt(string keyboard, string pad) => Active ? pad : keyboard;

        void LateUpdate()
        {
            if (!Active) return;
            var k = CursorController.Shown;
            var (tex, hot) = CursorController.Image(k);
            if (k != cursorKind && tex != null)
            {
                cursorKind = k;
                cursorImage.texture = tex;
                cursorImage.rectTransform.sizeDelta = new Vector2(tex.width, tex.height);
                cursorImage.rectTransform.pivot = new Vector2(hot.x / tex.width, 1f - hot.y / tex.height);
            }
            cursorImage.rectTransform.anchoredPosition = pos;
        }
    }
}
