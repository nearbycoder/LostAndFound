using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace LostAndFound
{
    /// <summary>
    /// Per-frame input edges computed from the held state (pressed now, not pressed last frame).
    /// More robust than wasPressedThisFrame when events arrive between player updates (simulated
    /// devices in the headless editor, the autopilot), and gives one place to read input.
    /// Polling alone misses a press that goes down and comes back up between two frames: a touchpad's tap-to-click
    /// sends both at once, and at a low frame rate an ordinary click fits between frames. So presses are also
    /// latched from the event stream, and a button that went down since the last frame counts as held for one frame.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class InputX : MonoBehaviour
    {
        static InputX inst;
        bool l, r, lPrev, rPrev;
        int frame = -1;
        readonly Dictionary<Key, (bool now, bool prev)> keys = new();
        static readonly Key[] Watched =
        {
            Key.Space, Key.Enter, Key.Escape, Key.Tab, Key.A, Key.D, Key.Q, Key.E, Key.W, Key.S, Key.T, Key.L,
            Key.LeftArrow, Key.RightArrow, Key.Backspace, Key.F1, Key.F12, Key.M, Key.R, Key.H,
            Key.UpArrow, Key.DownArrow, Key.NumpadEnter,
        };
        // the keyboard's own keys, without the gamepad's or the touch controls' stand-ins (the menus read the pad's d-pad themselves)
        readonly Dictionary<Key, (bool now, bool prev)> kbKeys = new();

        // buttons that went down in an input event since the last sample, and those the current frame is counting
        static readonly HashSet<ButtonControl> latched = new(), latchedNow = new();

        void Awake() => inst = this;
        void OnEnable() => InputSystem.onEvent += OnEvent;
        void OnDisable() => InputSystem.onEvent -= OnEvent;

        void Update() => Sample();

        // listeners run before the event is applied, so isPressed is still the state the event changes
        static void OnEvent(InputEventPtr e, InputDevice d)
        {
            if (!e.IsA<StateEvent>() && !e.IsA<DeltaStateEvent>()) return;
            switch (d)
            {
                case Mouse m: Latch(m.leftButton, e); Latch(m.rightButton, e); break;
                case Keyboard kb: foreach (var k in Watched) Latch(kb[k], e); break;
                case Gamepad p: foreach (var b in GamepadInput.Buttons(p)) Latch(b, e); break;
            }
        }

        static void Latch(ButtonControl b, InputEventPtr e)
        {
            if (!b.isPressed && b.ReadValueFromEvent(e, out float v) && b.IsValueConsideredPressed(v)) latched.Add(b);
        }

        /// <summary>Held now, or pressed at some point since the last frame (however briefly).</summary>
        public static bool Pressed(ButtonControl b) => b != null && (b.isPressed || latched.Contains(b) || latchedNow.Contains(b));

        static bool LatchedMouse(bool left)
        {
            foreach (var c in latchedNow)
                if (c.device is Mouse m && c == (left ? m.leftButton : m.rightButton)) return true;
            return false;
        }

        static bool LatchedKey(Key k)
        {
            foreach (var c in latchedNow)
                if (c is KeyControl kc && kc.keyCode == k) return true;
            return false;
        }

        void Sample()
        {
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            latchedNow.Clear();
            latchedNow.UnionWith(latched);
            latched.Clear();
            lPrev = l;
            rPrev = r;
            var m = Mouse.current;
            l = (m != null && m.leftButton.isPressed) || LatchedMouse(true);
            r = (m != null && m.rightButton.isPressed) || LatchedMouse(false);
            var kb = Keyboard.current;
            foreach (var k in Watched)
            {
                bool own = (kb != null && kb[k].isPressed) || LatchedKey(k);
                bool now = own || GamepadInput.KeyHeld(k) || TouchInput.KeyHeld(k);
                bool prev = keys.TryGetValue(k, out var s) && s.now;
                keys[k] = (now, prev);
                kbKeys[k] = (own, kbKeys.TryGetValue(k, out var o) && o.now);
            }
        }

        static InputX I
        {
            get
            {
                if (inst == null) inst = new GameObject("InputX").AddComponent<InputX>();
                inst.Sample();
                return inst;
            }
        }

        public static bool LeftDown => I.l && !I.lPrev;
        public static bool LeftUp => !I.l && I.lPrev;
        public static bool LeftHeld => I.l;
        public static bool RightDown => I.r && !I.rPrev;
        public static Vector2 MousePos => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        public static Vector2 MouseDelta => Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        public static float Scroll => Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;

        public static bool KeyDown(Key k) => I.keys.TryGetValue(k, out var s) && s.now && !s.prev;
        public static bool KeyHeld(Key k) => I.keys.TryGetValue(k, out var s) && s.now;
        /// <summary>Pressed on a keyboard this frame (a quick tap too), not by a gamepad button standing in for the key.</summary>
        public static bool KeyboardDown(Key k) => I.kbKeys.TryGetValue(k, out var s) && s.now && !s.prev;
        public static bool KeyboardHeld(Key k) => I.kbKeys.TryGetValue(k, out var s) && s.now;
    }
}
