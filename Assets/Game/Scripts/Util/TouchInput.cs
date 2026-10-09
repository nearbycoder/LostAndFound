using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace LostAndFound
{
    /// <summary>
    /// Playing by touch in a browser (a phone or a tablet). The page (WebGLTemplates/LostAndFound/index.html) reads the fingers
    /// and the on-screen buttons and sends plain commands here (SendMessage("LafTouch", "Page", ...)); this drives a virtual
    /// mouse, as GamepadInput does, so everything that reads the mouse works unchanged, and stands in for keys through InputX.
    /// A finger is the pointer: a tap is a click where it lands, a drag presses where it started and moves (it turns an object
    /// in your hands), touching and holding shows what's there without clicking (the hint, the slip, the stamp's preview), and
    /// when the finger lifts the pointer leaves the window, so nothing stays lit under where it was. A pinch is the scroll wheel.
    /// The page decides when touch is in use (a touch-first device, or a real touch; a key, a mouse or a pad hands it back) and
    /// is told which of its buttons apply (State). Elsewhere, and on a desktop, this does nothing.
    ///
    /// Commands: "mode 1|0", "move x y", "press", "release", "click x y", "park", "wheel n", "key Name 1|0", "back",
    /// with x and y in the canvas's pixels from its bottom left, as the player's mouse.
    /// </summary>
    [DefaultExecutionOrder(-1900)]   // after GamepadInput, before InputX samples the frame
    public class TouchInput : MonoBehaviour
    {
        public static TouchInput I { get; private set; }
        /// <summary>Touch is in use: the page shows its controls, and prompts name taps.</summary>
        public static bool Active { get; private set; }

        /// <summary>Where the pointer goes when the finger lifts: below the window, as a mouse that has left it.</summary>
        public static Vector2 Parked => new(Screen.width * 0.5f, -4f * Mathf.Max(1, Screen.height));

        Mouse virtualMouse;
        Vector2 pos;
        bool left;
        /// <summary>The mouse states still to send, one a frame, so each press and release is a frame's edge.</summary>
        readonly List<Step> steps = new();
        struct Step { public Vector2 pos; public bool left, right, move; public float scroll; }
        Vector2 lastSent, lastClick;
        int lastFlags = -1;
        bool padWas, carryingWas, bringStamp;

        // keys held by the page's buttons, those a frame has seen held, and those let go before any frame saw them
        static readonly HashSet<Key> held = new(), seen = new(), letGo = new();

        /// <summary>One for the whole session, by itself at the root (the page's SendMessage finds it by name): the game is rebuilt in
        /// place when it goes back to the title, and touch carries on through that.</summary>
        void Awake()
        {
            I = this;
            pos = Parked;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (virtualMouse != null) InputSystem.RemoveDevice(virtualMouse);
            if (I == this) { Active = false; I = null; }
            held.Clear();
            seen.Clear();
            letGo.Clear();
        }

        /// <summary>From the page: one command (see the class summary).</summary>
        public void Page(string command)
        {
            var a = command.Split(' ');
            float F(int i) => i < a.Length && float.TryParse(a[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : 0f;
            switch (a[0])
            {
                case "mode": SetActive(F(1) > 0.5f); break;
                case "move": Move(new Vector2(F(1), F(2))); break;
                case "press": Add(pos, true); break;
                case "release": Add(pos, false); break;
                case "click":
                    // the pointer arrives first (what a click there would land on lights up), then the button goes down and up
                    var p = lastClick = new Vector2(F(1), F(2));
                    Add(p, false);
                    Add(p, true);
                    Add(p, false);
                    break;
                case "park": Add(Parked, false); break;
                case "wheel":
                    int n = Mathf.Clamp(Mathf.RoundToInt(F(1)), -8, 8);
                    for (int i = 0; i < Mathf.Abs(n); i++) Add(pos, left, scroll: Mathf.Sign(n) * 120f);   // a frame a notch
                    break;
                case "key":
                    if (a.Length > 2 && System.Enum.TryParse(a[1], out Key k)) SetKey(k, F(2) > 0.5f);
                    break;
                case "back": Back(); break;
            }
        }

        void SetActive(bool on)
        {
            if (on == Active) return;
            Active = on;
            steps.Clear();
            if (on)
            {
                virtualMouse ??= InputSystem.AddDevice<Mouse>("TouchMouse");
                virtualMouse.MakeCurrent();
                // the browser's touches also reach the player as a touchscreen, which the menus would click with: only ours count
                if (Touchscreen.current != null && Touchscreen.current.enabled) InputSystem.DisableDevice(Touchscreen.current);
                pos = lastSent = Parked;
                left = false;
                Add(pos, false);
                Debug.Log("[Touch] touch in control");
            }
            else
            {
                held.Clear();
                letGo.Clear();
                if (left) Send(new Step { pos = pos });
                left = false;
                foreach (var d in InputSystem.devices)
                    if (d is Mouse m && m != virtualMouse && !m.name.StartsWith("Demo") && !m.name.StartsWith("PadMouse")) { m.MakeCurrent(); break; }
                Debug.Log("[Touch] mouse and keyboard in control");
            }
        }

        /// <summary>A move joins a move not yet sent (a frame sees where the finger is now); a press or release stays a step.</summary>
        void Move(Vector2 p)
        {
            if (steps.Count > 0 && steps[^1].move && steps[^1].left == left) { var s = steps[^1]; s.pos = p; steps[^1] = s; pos = p; return; }
            Add(p, left, move: true);
        }

        void Add(Vector2 p, bool l, bool right = false, float scroll = 0f, bool move = false)
        {
            pos = p;
            left = l;
            if (steps.Count < 64) steps.Add(new Step { pos = p, left = l, right = right, scroll = scroll, move = move });
        }

        void Send(Step s)
        {
            if (virtualMouse == null) return;
            var st = new MouseState { position = s.pos, delta = s.pos - lastSent, scroll = new Vector2(0f, s.scroll) }
                .WithButton(MouseButton.Left, s.left).WithButton(MouseButton.Right, s.right);
            InputSystem.QueueStateEvent(virtualMouse, st);
            lastSent = s.pos;
        }

        static void SetKey(Key k, bool down)
        {
            if (down) { held.Add(k); seen.Remove(k); letGo.Remove(k); }
            else if (held.Contains(k))
            {
                if (seen.Contains(k)) { held.Remove(k); seen.Remove(k); }
                else letGo.Add(k);   // a tap quicker than a frame: held for the next one
            }
        }

        /// <summary>The page's Back: put down what's in your hands or the stamp, leave the slip (a right click, with the pointer
        /// away so it lands on nothing in particular), or close the card or menu that's open (Esc, which closes every one).</summary>
        void Back()
        {
            bool carrying = StampTool.I != null && StampTool.I.Carrying != null;
            bool holding = InspectController.I != null && InspectController.I.Held != null;
            if (UIRoot.ModalOpen && !carrying && !holding) { SetKey(Key.Escape, true); SetKey(Key.Escape, false); return; }
            Add(Parked, false);
            Add(Parked, false, right: true);
            Add(Parked, false);
        }

        static readonly (System.Text.RegularExpressions.Regex re, string with)[] Rewords =
        {
            (new(@"\b[Rr]ight click\b"), "Back"),
            (new(@"\(scroll\)"), "(pinch)"),
            (new(@"\s*\[(Space|Tab|L|R|H|T|Esc)\]"), ""),
            (new(@"\b([Pp])ress T\b"), "$1ress Tray"),
            (new(@"\b([Pp])ress H\b"), "$1ress Nudge"),
            (new(@"\bclick or R\b"), "tap or Rules"),
            (new(@"\(Tab\)"), "(tap it)"),
            (new(@"\(A or ←\)"), "(the left arrow)"),
            (new(@"\bPress\b"), "Tap"),
            (new(@"\bpress\b"), "tap"),
            (new(@"\bHover( over)?\b"), "Touch and hold"),
            (new(@"\bhover( over)?\b"), "touch and hold"),
            (new(@"\bClick\b"), "Tap"),
            (new(@"\bclick\b"), "tap"),
        };
        static readonly Dictionary<string, string> worded = new();

        /// <summary>Words for the hands in use: while touch is, a hint's clicks are taps, hovering is touching and holding, the
        /// scroll wheel a pinch, and the keys the page's buttons (Tray, Nudge, Rules, Back).</summary>
        public static string Words(string s) => Words(s, Active);

        public static string Words(string s, bool touch)
        {
            if (!touch || string.IsNullOrEmpty(s)) return s;
            if (worded.TryGetValue(s, out var w)) return w;
            w = s;
            foreach (var (re, with) in Rewords) w = re.Replace(w, with);
            if (worded.Count > 512) worded.Clear();
            return worded[s] = w;
        }

        /// <summary>The page's buttons standing in for keys (read by InputX alongside the keyboard).</summary>
        public static bool KeyHeld(Key k) => Active && held.Contains(k);

        void Update()
        {
            // keys: one let go before any frame saw it held has been seen now; let it go
            if (letGo.Count > 0)
                foreach (var k in new List<Key>(letGo))
                    if (seen.Contains(k)) { held.Remove(k); seen.Remove(k); letGo.Remove(k); }
            foreach (var k in held) seen.Add(k);

            // a gamepad picked up hands the game to it (the page hides its controls when it hears)
            bool pad = GamepadInput.Active;
            if (pad && !padWas && Active) SetActive(false);
            padWas = pad;
            if (!Active) return;
            if (Mouse.current != virtualMouse) virtualMouse.MakeCurrent();
            // a stamp just picked up stays where the finger was (the pointer went away when it lifted), not off the desk's edge
            bool carrying = StampTool.I != null && StampTool.I.Carrying != null;
            if (carrying && !carryingWas) bringStamp = true;
            if (!carrying) bringStamp = false;
            if (bringStamp && steps.Count == 0) { bringStamp = false; if (lastSent == Parked) Add(lastClick, false); }
            // and once it's down or back in the rack, the pointer goes as after any tap (aiming left it where the finger was)
            if (!carrying && carryingWas) Add(Parked, false);
            carryingWas = carrying;
            if (steps.Count > 0)
            {
                Send(steps[0]);
                steps.RemoveAt(0);
            }
        }

        void LateUpdate()
        {
            int flags = State();
            if (flags == lastFlags) return;
            lastFlags = flags;
            WebPage.TouchState(flags);
        }

        /// <summary>Which of the page's buttons apply now, as bits (the page's lafTouchState reads them).</summary>
        public static int State()
        {
            var d = Director.I;
            bool playing = d != null && d.Running;
            bool modal = UIRoot.ModalOpen;
            bool holding = InspectController.I != null && InspectController.I.Held != null;
            bool carrying = StampTool.I != null && StampTool.I.Carrying != null;
            bool slip = ClaimSlip.I != null && ClaimSlip.I.Focused;
            var rig = CameraRig.I;
            bool turn = playing && rig != null && rig.allowTurn && !modal && !holding && !carrying;
            int f = 0;
            if (playing) f |= 1;
            if (modal) f |= 2;
            if (holding) f |= 4;
            if (turn && rig.view != View.Cabinet) f |= 8;
            if (turn && rig.view != View.Shelf) f |= 16;
            if (holding && Lamp.I != null && Lamp.I.uvUnlocked) f |= 32;
            if (playing && d.AtTheWindow) f |= 64;
            // something to go back from (the title itself is a card, but Esc doesn't put it away: only a card over it counts)
            bool card = modal && !(TitleScreen.Showing && UIRoot.I.ModalDepth <= 1);
            if (holding || carrying || slip || card) f |= 128;
            if (GamepadInput.Active) f |= 256;
            if (carrying) f |= 512;
            if (PauseMenu.Open) f |= 1024;
            if (RulesCard.IsOpen) f |= 2048;
            if (slip) f |= 4096;
            if (rig != null && rig.view == View.Cabinet) f |= 8192;
            if (rig != null && rig.view == View.Shelf) f |= 16384;
            return f;
        }
    }
}
