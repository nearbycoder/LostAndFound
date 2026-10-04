using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LostAndFound
{
    /// <summary>
    /// Per-frame input edges computed from the held state (pressed now, not pressed last frame).
    /// More robust than wasPressedThisFrame when events arrive between player updates (simulated
    /// devices in the headless editor, the autopilot), and gives one place to read input.
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
            Key.LeftArrow, Key.RightArrow, Key.Backspace, Key.F1, Key.F12, Key.M,
        };

        void Awake() => inst = this;

        void Update() => Sample();

        void Sample()
        {
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            lPrev = l;
            rPrev = r;
            var m = Mouse.current;
            l = m != null && m.leftButton.isPressed;
            r = m != null && m.rightButton.isPressed;
            var kb = Keyboard.current;
            foreach (var k in Watched)
            {
                bool now = kb != null && kb[k].isPressed;
                bool prev = keys.TryGetValue(k, out var s) && s.now;
                keys[k] = (now, prev);
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
    }
}
