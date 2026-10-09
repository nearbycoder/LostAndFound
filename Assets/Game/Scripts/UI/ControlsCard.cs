using TMPro;
using UnityEngine;

namespace LostAndFound
{
    /// <summary>Every control on one card, from the pause menu or the title: keyboard and mouse words, or the pad's or the
    /// touch controls' while they're in use (the same lists as the README). Esc, right click (B) or "Back" puts it away.</summary>
    public static class ControlsCard
    {
        static RectTransform panel;
        static int openedFrame;
        public static bool IsOpen => panel != null;

        static readonly (string action, string keys)[] Keyboard =
        {
            ("Point, pick up, open, press", "Left click"),
            ("Turn to the drawers / the shelf", "A / D, ← / →, screen edge"),
            ("Turn the object over", "Drag, or Q E W S"),
            ("Look closer", "Scroll wheel"),
            ("Put it down", "Right click, Esc or Backspace"),
            ("On the counter tray", "T"),
            ("Read the slip and what they said", "Hover it, or Tab"),
            ("Ask about a finding", "Click it on the slip"),
            ("Agnes's blue lamp, once you have it", "L, holding something"),
            ("Ring the bell", "Click it, or Space"),
            ("Move the conversation on", "Click, Space or Enter"),
            ("Agnes's rules", "Hover her card, or R"),
            ("A nudge from Agnes", "H, during a claim"),
            ("Earlier days' ledger pages", "The ledger on the desk, or pause"),
            ("Pause", "Esc"),
            ("Menus", "Arrows, then Enter"),
        };

        static readonly (string action, string keys)[] Pad =
        {
            ("Move the cursor", "Left stick"),
            ("Point, pick up, open, press", "A"),
            ("Put it down, or the stamp back", "B"),
            ("Turn to the drawers / the shelf", "LB / RB"),
            ("Turn the object over", "Right stick"),
            ("Lean in / out", "RT / LT"),
            ("On the counter tray", "X"),
            ("Read the slip and what they said", "View"),
            ("Ask about a finding", "A on it, on the slip"),
            ("Agnes's blue lamp, once you have it", "D-pad up"),
            ("Ring the bell, move the conversation on", "D-pad down"),
            ("Agnes's rules", "Y"),
            ("A nudge from Agnes", "D-pad left"),
            ("Earlier days' ledger pages", "A on the desk's ledger, or Menu"),
            ("Pause", "Menu"),
            ("Menus", "D-pad, then A"),
        };

        /// <summary>A phone or tablet in a browser: the game's own taps and the page's buttons around it (TouchInput).</summary>
        static readonly (string action, string keys)[] Touch =
        {
            ("Point, pick up, open, press", "Tap"),
            ("See what something is, or read it", "Touch and hold"),
            ("Turn to the drawers / the shelf", "The arrow buttons"),
            ("Turn the object over", "Drag"),
            ("Look closer", "Pinch"),
            ("Put it down, leave the slip, close a card", "Back"),
            ("On the counter tray", "Tray"),
            ("Read the slip and what they said", "Tap it, or Slip"),
            ("Ask about a finding", "Tap it on the slip"),
            ("Stamp the slip", "Tap the spot, then tap again"),
            ("Agnes's blue lamp, once you have it", "Lamp, holding something"),
            ("Ring the bell, move the conversation on", "Tap"),
            ("Agnes's rules", "Rules"),
            ("A nudge from Agnes", "Nudge, during a claim"),
            ("Earlier days' ledger pages", "The ledger on the desk, or Menu"),
            ("Pause", "Menu"),
        };

        /// <summary>The card's text, two columns: what you want to do, and how.</summary>
        public static string Text(bool pad) => Text(pad, false);

        public static string Text(bool pad, bool touch)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var (action, keys) in pad ? Pad : touch ? Touch : Keyboard)
                sb.Append($"{action}<pos=58%><color=#7a2e26>{keys}</color>\n");
            return sb.ToString();
        }

        public static void Show()
        {
            if (IsOpen) return;
            openedFrame = Time.frameCount;
            bool pad = GamepadInput.Active, touch = !pad && TouchInput.Active;
            UIRoot.I.PushModal();
            AudioDirector.PlayMaterial("paper", "pick", 0.5f);
            panel = UiKit.Rect("Controls", UIRoot.I.root).Fill();
            var dim = UiKit.Image(panel, "Dim", null, new Color(0f, 0f, 0f, 0.5f));
            dim.rectTransform.Fill();
            dim.raycastTarget = true;
            float scale = UiKit.TextScale > 1f ? 1.12f : 1f;   // Large text, as on the rules card
            var card = UiKit.Image(panel, "Card", "paper_card", UiKit.Paper, 28f);
            card.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)).Place(Vector2.zero, new Vector2(1240f, 920f));
            var title = UiKit.Label(card.transform, "Title", pad ? "Controls  ·  gamepad" : touch ? "Controls  ·  touch" : "Controls  ·  keyboard and mouse", Fonts.Title, 50f, UiKit.Ink, TextAlignmentOptions.Top);
            title.rectTransform.Fill();
            title.margin = new Vector4(0f, 36f, 0f, 0f);
            var body = UiKit.Label(card.transform, "Body", Text(pad, touch), Fonts.Body, 32f * scale, UiKit.Ink, TextAlignmentOptions.TopLeft);
            body.rectTransform.Fill();
            body.margin = new Vector4(80f, 120f, 70f, 110f);
            body.lineSpacing = 14f;
            body.enableAutoSizing = true;
            body.fontSizeMin = 20f;
            body.fontSizeMax = 32f * scale;
            var back = UiKit.Button(card.transform, "Back", "Back", Fonts.Title, 40f, Hide);
            back.GetComponent<RectTransform>().Anchor(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f)).Place(new Vector2(0f, 30f), new Vector2(360f, 60f));
            var keys = UiKit.Label(card.transform, "Keys", pad ? "B TO PUT IT AWAY" : touch ? "BACK TO PUT IT AWAY" : "ESC OR RIGHT CLICK TO PUT IT AWAY", Fonts.Type, 18f, UiKit.InkSoft, TextAlignmentOptions.Bottom);
            keys.rectTransform.Fill();
            keys.margin = new Vector4(0f, 0f, 0f, 12f);
            panel.gameObject.AddComponent<Closer>();
        }

        public static void Hide()
        {
            if (!IsOpen) return;
            CardMotion.Close(panel);
            panel = null;
            UIRoot.I.PopModal();
            AudioDirector.PlayMaterial("paper", "put", 0.4f);
        }

        class Closer : MonoBehaviour
        {
            void LateUpdate()
            {
                if (Time.frameCount == openedFrame) return;
                if (InputX.KeyDown(UnityEngine.InputSystem.Key.Escape) || InputX.RightDown) Hide();
            }

            void OnDestroy()
            {
                if (panel == (RectTransform)transform) { panel = null; UIRoot.I?.PopModal(); }
            }
        }
    }
}
