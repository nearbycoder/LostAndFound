using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LostAndFound
{
    /// <summary>
    /// The menus with the keyboard and the pad. On whichever card is on top (the title and its pages, the pause menu,
    /// Settings, Controls, the ledger's and the week's pages), the arrow keys and the pad's d-pad move a focus between its
    /// buttons and sliders (up and down go round), Enter or Space presses the focused button, and left and right move a
    /// focused slider (or step the fidelity). The focus looks like the hover, with a line under it, and is heard as it is.
    /// The pad's drawn cursor follows the focus, so A presses it through the ordinary click. Moving the mouse or the pad's
    /// stick hands the menus back to the pointer; a card opened while the keys or the pad were in use starts focused.
    /// </summary>
    public class MenuNav : MonoBehaviour
    {
        public static MenuNav I { get; private set; }
        /// <summary>A card with something to press is on top: the d-pad belongs to the menu.</summary>
        public static bool InMenu => I != null && I.card != null;
        /// <summary>The focused button or slider (null while the pointer has the menus).</summary>
        public static Component Focused => I != null ? I.focus : null;
        /// <summary>The card the menus are on ("Title", "Settings", "Pause"...), for test logs.</summary>
        public static string CardName => I != null && I.card != null ? I.card.name : null;

        RectTransform card;
        Component focus;
        bool keyMode;
        Vector2Int held;
        float repeatAt;
        readonly bool[] padWas = new bool[4];
        readonly Dictionary<RectTransform, Component> remembered = new();

        void Awake() => I = this;
        void OnDestroy() { if (I == this) I = null; }

        /// <summary>The card on top: the newest child of the UI root after the HUD, if it has anything to press.</summary>
        static RectTransform TopCard(out List<Component> items)
        {
            items = null;
            var ui = UIRoot.I;
            if (ui == null || !UIRoot.ModalOpen) return null;
            for (int i = ui.root.childCount - 1; i >= ui.HudCount; i--)
            {
                var c = (RectTransform)ui.root.GetChild(i);
                if (!c.gameObject.activeInHierarchy) continue;
                if (c.TryGetComponent<CanvasGroup>(out var g) && (!g.interactable || g.alpha < 0.05f)) return null;   // on its way out
                items = Items(c);
                return items.Count > 0 ? c : null;
            }
            return null;
        }

        static List<Component> Items(RectTransform card)
        {
            var list = new List<Component>();
            foreach (var b in card.GetComponentsInChildren<PaperButton>()) if (b.interactable && b.isActiveAndEnabled) list.Add(b);
            foreach (var s in card.GetComponentsInChildren<PaperSlider>()) if (s.isActiveAndEnabled) list.Add(s);
            return list;
        }

        static Camera UiCamera => UIRoot.I != null && UIRoot.I.canvas.renderMode != RenderMode.ScreenSpaceOverlay ? UIRoot.I.canvas.worldCamera : null;

        static Vector2 ToScreen(RectTransform rt, Vector2 local) => RectTransformUtility.WorldToScreenPoint(UiCamera, rt.TransformPoint(local));

        /// <summary>Where an item sits for moving between them: the left end of its row (its words, or a slider's name), so a
        /// column of toggles and sliders lines up.</summary>
        static Vector2 Anchor(Component c)
        {
            var rt = c is PaperSlider s && s.label != null ? s.label.rectTransform : (RectTransform)c.transform;
            var r = rt.rect;
            return ToScreen(rt, new Vector2(r.xMin, r.center.y));
        }

        /// <summary>Where the pad's cursor goes: the button's words, or the slider's knob.</summary>
        static Vector2 Target(Component c)
        {
            if (c is PaperSlider s) return ToScreen(s.Knob, s.Knob.rect.center);
            var rt = (RectTransform)c.transform;
            if (c is PaperButton b && b.label != null && b.label.textBounds.size.x > 0f)
            {
                // just past the end of the words, so the hand doesn't cover them; still on the button
                var tb = b.label.textBounds;
                Vector2 p = ToScreen(b.label.rectTransform, new Vector2(tb.max.x + b.label.fontSize * 0.5f, tb.center.y));
                Vector2 lo = ToScreen(rt, rt.rect.min), hi = ToScreen(rt, rt.rect.max);
                return new Vector2(Mathf.Clamp(p.x, lo.x + 2f, hi.x - 2f), Mathf.Clamp(p.y, lo.y + 2f, hi.y - 2f));
            }
            return ToScreen(rt, rt.rect.center);
        }

        static Component First(List<Component> items)
        {
            Component best = null;
            Vector2 bp = default;
            foreach (var c in items)
            {
                var p = Anchor(c);
                if (best == null || p.y > bp.y + 4f || (Mathf.Abs(p.y - bp.y) <= 4f && p.x < bp.x)) { best = c; bp = p; }
            }
            return best;
        }

        /// <summary>The nearest item that way; up and down go round from the last row to the first.</summary>
        static Component Step(Component from, Vector2 dir, List<Component> items)
        {
            Vector2 c = Anchor(from);
            Component best = null, far = null;
            float bestScore = float.MaxValue, farScore = float.MinValue;
            bool vertical = dir.y != 0f;
            foreach (var o in items)
            {
                if (o == from) continue;
                Vector2 d = Anchor(o) - c;
                float along = Vector2.Dot(d, dir), perp = Mathf.Abs(Vector2.Dot(d, new Vector2(dir.y, -dir.x)));
                if (along > 6f)
                {
                    float score = along + perp * (vertical ? 0.5f : 2f);
                    if (score < bestScore) { bestScore = score; best = o; }
                }
                else if (vertical && along < -6f)
                {
                    float score = -along - perp * 0.5f;   // round the far side: the farthest back, nearest in line
                    if (score > farScore) { farScore = score; far = o; }
                }
            }
            return best ?? far;
        }

        void SetFocus(Component c, bool sound)
        {
            if (focus == c) return;
            Mark(focus, false);
            focus = c;
            Mark(focus, true);
            if (card != null && c != null) remembered[card] = c;
            if (c == null) return;
            if (sound) AudioDirector.Play("ui_hover", 0.25f, Random.Range(0.95f, 1.1f));
            GamepadInput.WarpTo(Target(c));
        }

        static void Mark(Component c, bool on)
        {
            if (c is PaperButton b && b != null) b.focused = on;
            else if (c is PaperSlider s && s != null) s.focused = on;
        }

        void Update()
        {
            var top = TopCard(out var items);
            if (top != card)
            {
                SetFocus(null, false);
                card = top;
                if (card != null && (keyMode || GamepadInput.Active))
                    SetFocus(remembered.TryGetValue(card, out var r) && r != null && items.Contains(r) ? r : First(items), false);
            }
            if (card == null) return;
            if (focus != null && !items.Contains(focus)) SetFocus(null, false);   // gone (a page rebuilt), or no longer pressable

            // the pointer takes the menus back: the real mouse moving, or the pad's stick steering its cursor
            var pad = Gamepad.current;
            bool mouseMoved = !GamepadInput.Active && Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 4f;
            bool stick = GamepadInput.Active && pad != null && pad.leftStick.ReadValue().sqrMagnitude > 0.09f;
            if (mouseMoved || stick)
            {
                keyMode = false;
                SetFocus(null, false);
                return;
            }

            // which way: arrows (held ones repeat), the pad's d-pad
            Vector2Int dir = Vector2Int.zero;
            if (InputX.KeyboardDown(Key.UpArrow)) dir = Vector2Int.up;
            else if (InputX.KeyboardDown(Key.DownArrow)) dir = Vector2Int.down;
            else if (InputX.KeyboardDown(Key.LeftArrow)) dir = Vector2Int.left;
            else if (InputX.KeyboardDown(Key.RightArrow)) dir = Vector2Int.right;
            if (pad != null && GamepadInput.Active)
            {
                var dp = new[] { pad.dpad.up, pad.dpad.down, pad.dpad.left, pad.dpad.right };
                var dv = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
                for (int i = 0; i < 4; i++)
                {
                    bool now = InputX.Pressed(dp[i]);
                    if (now && !padWas[i]) dir = dv[i];
                    padWas[i] = now;
                }
            }
            Vector2Int hold = InputX.KeyboardHeld(Key.UpArrow) ? Vector2Int.up : InputX.KeyboardHeld(Key.DownArrow) ? Vector2Int.down
                : InputX.KeyboardHeld(Key.LeftArrow) ? Vector2Int.left : InputX.KeyboardHeld(Key.RightArrow) ? Vector2Int.right : Vector2Int.zero;
            if (pad != null && GamepadInput.Active && hold == Vector2Int.zero)
                hold = pad.dpad.up.isPressed ? Vector2Int.up : pad.dpad.down.isPressed ? Vector2Int.down
                    : pad.dpad.left.isPressed ? Vector2Int.left : pad.dpad.right.isPressed ? Vector2Int.right : Vector2Int.zero;
            if (dir != Vector2Int.zero) { held = dir; repeatAt = Time.unscaledTime + 0.42f; }
            else if (hold != Vector2Int.zero && hold == held && Time.unscaledTime >= repeatAt) { dir = hold; repeatAt = Time.unscaledTime + 0.11f; }
            else if (hold == Vector2Int.zero) held = Vector2Int.zero;

            if (dir != Vector2Int.zero)
            {
                keyMode = true;
                if (focus == null) SetFocus(First(items), true);
                else if (dir.x != 0 && focus is PaperSlider s)
                {
                    if (s.Nudge(dir.x)) GamepadInput.WarpTo(Target(s));
                }
                else
                {
                    var next = Step(focus, dir, items);
                    if (next != null) SetFocus(next, true);
                }
            }

            // Enter or Space (the keyboard's; the pad's A clicks where its cursor is, on the focus)
            if (focus is PaperButton b && (InputX.KeyboardDown(Key.Enter) || InputX.KeyboardDown(Key.NumpadEnter) || InputX.KeyboardDown(Key.Space)))
            {
                keyMode = true;
                b.Press();
            }
        }

        /// <summary>What has the focus, in words, for test logs.</summary>
        public static string Describe(Component c)
        {
            if (c == null) return "nothing";   // Unity's null: a card's button destroyed with it
            return c switch
            {
                PaperButton b when b.label != null => b.label.GetParsedText(),
                PaperSlider s when s.label != null => $"{s.label.text} slider at {s.Value:0.##}",
                _ => c.name,
            };
        }
    }
}
