using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostAndFound
{
    /// <summary>A nudge in Agnes's hand, pinned at the right, over the dark wall above the ticket printer (the top of the
    /// screen belongs to whoever is talking). Unlike her notes it doesn't wait for a click and doesn't take the mouse:
    /// it stays while you carry on, until the claim moves on or it's been up a while.</summary>
    public class NudgeNote : MonoBehaviour
    {
        RectTransform panel;
        CanvasGroup group;
        TextMeshProUGUI head, body;
        bool on;
        float a, shownAt, drop;
        readonly Vector3[] corners = new Vector3[4];

        public const float Lifetime = 45f;
        const float X = -96f, Y = 70f, W = 470f;
        /// <summary>Large text grows it a little less than the rest: it sits beside the object in your hand.</summary>
        static float Scale => Mathf.Min(UiKit.TextScale, 1.15f);
        public bool Shown => on;
        public string Text => body.text;

        public static NudgeNote Create(RectTransform root)
        {
            var rt = UiKit.Rect("Nudge", root).Anchor(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f)).Place(new Vector2(X, Y), new Vector2(W, 240f));
            var n = rt.gameObject.AddComponent<NudgeNote>();
            n.panel = rt;
            n.group = UiKit.Group(rt.gameObject);
            n.group.alpha = 0f;
            n.group.blocksRaycasts = false;
            n.group.interactable = false;
            var sh = UiKit.Image(rt, "Shadow", "note_paper", new Color(0, 0, 0, 0.35f), 0f);
            sh.rectTransform.Fill();
            sh.rectTransform.anchoredPosition = new Vector2(6f, -8f);
            var bg = UiKit.Image(rt, "Paper", "note_paper", new Color(0.98f, 0.95f, 0.84f), 0f);
            bg.rectTransform.Fill();
            n.head = UiKit.Label(rt, "Head", "", Fonts.AgnesBold, 22f, UiKit.Oxblood, TextAlignmentOptions.TopLeft);
            n.head.rectTransform.Fill();
            n.head.margin = new Vector4(38f, 26f, 30f, 0f);
            n.body = UiKit.Label(rt, "Body", "", Fonts.Agnes, 28f, new Color(0.13f, 0.15f, 0.32f), TextAlignmentOptions.TopLeft);
            n.body.rectTransform.Fill();
            n.body.margin = new Vector4(38f, 62f, 34f, 24f);
            n.body.lineSpacing = -12f;
            n.body.enableAutoSizing = true;
            n.body.fontSizeMin = 18f;
            n.body.fontSizeMax = 28f;
            foreach (var g in rt.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
            return n;
        }

        /// <summary>Show nudge <paramref name="index"/> (from 1) of <paramref name="count"/> for this stage of the claim.</summary>
        public void Show(string text, int index, int count)
        {
            head.text = count > 1 ? $"A nudge from Agnes   <size=80%>{index} of {count}</size>" : "A nudge from Agnes";
            body.text = TouchInput.Words(text);
            if (!on) panel.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-1.8f, 1.2f));
            on = true;
            shownAt = Time.unscaledTime;
            AudioDirector.Play("paper_unfold", 0.5f, Random.Range(0.95f, 1.08f));
        }

        public void Hide() => on = false;

        void Update()
        {
            if (on && Time.unscaledTime - shownAt > Lifetime) on = false;
            // make way for a menu, and while someone at the window is speaking
            bool talking = UIRoot.I != null && (UIRoot.I.dialogue.Typing || UIRoot.I.dialogue.Waiting);
            bool visible = on && !UIRoot.ModalOpen && !talking;
            a = Mathf.MoveTowards(a, visible ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            float k = Ease.OutCubic(a);
            group.alpha = k;
            panel.localScale = Vector3.one * Scale;
            drop = Mathf.Lerp(drop, BelowBubble(), 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
            panel.anchoredPosition = new Vector2(X + 300f * (1f - k), Y - drop);
        }

        /// <summary>How far to drop so the note's top clears a speech bubble lingering over it (canvas units).</summary>
        float BelowBubble()
        {
            var dlg = UIRoot.I != null ? UIRoot.I.dialogue : null;
            var canvas = UIRoot.I != null ? UIRoot.I.canvas : null;
            if (dlg == null || canvas == null || !dlg.Shown || dlg.Alpha < 0.05f || canvas.renderMode != RenderMode.ScreenSpaceOverlay) return 0f;
            float s = canvas.scaleFactor;
            dlg.Panel.GetWorldCorners(corners);   // screen pixels on an overlay canvas
            float bubbleBottom = corners[0].y, bubbleLeft = corners[0].x;
            // where the note sits with no drop, in screen pixels
            float ts = Scale;
            float noteTop = Screen.height * 0.5f + (Y + 120f * ts) * s;
            float noteLeft = Screen.width + (X - W * ts) * s;
            if (bubbleLeft > Screen.width || corners[2].x < noteLeft || bubbleBottom > noteTop) return 0f;
            // but stay clear of the object's name and the controls along the bottom
            return Mathf.Min((noteTop - bubbleBottom) / s + 18f, 380f + Y - 240f * ts);
        }
    }

    /// <summary>"Show me": a slow, steady glint on the hidden detail a nudge is pointing at, whenever a click there
    /// would find it. Bigger and calmer than the magnifier's sparkle, so it reads as Agnes pointing.</summary>
    public class NudgeGlint : MonoBehaviour
    {
        RectTransform rt;
        Image glint, ring;
        float a, target;
        Vector2 pos;

        /// <summary>Where it's glinting, in screen pixels (when <see cref="Showing"/>).</summary>
        public Vector2 ScreenPos { get; private set; }
        public bool Showing => target > 0f;

        public static NudgeGlint Create(RectTransform root)
        {
            var rt = UiKit.Rect("NudgeGlint", root).Anchor(Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f)).Place(Vector2.zero, new Vector2(150f, 150f));
            var g = rt.gameObject.AddComponent<NudgeGlint>();
            g.rt = rt;
            g.ring = UiKit.Image(rt, "Ring", "ring", new Color(1f, 0.9f, 0.62f, 0f));
            g.ring.rectTransform.Fill();
            g.glint = UiKit.Image(rt, "Glint", "glint", new Color(1f, 0.96f, 0.82f, 0f));
            g.glint.rectTransform.Fill(30f);
            g.ring.raycastTarget = g.glint.raycastTarget = false;
            return g;
        }

        public void Show(Vector3 screen)
        {
            ScreenPos = screen;
            float scale = UIRoot.I.root.rect.width / Mathf.Max(1, Screen.width);
            pos = new Vector2(screen.x, screen.y) * scale;
            target = 1f;
        }

        public void Hide() => target = 0f;

        void Update()
        {
            a = Mathf.MoveTowards(a, target, Time.unscaledDeltaTime * 4f);
            rt.anchoredPosition = pos;
            float t = Time.unscaledTime;
            float breathe = 0.5f + 0.5f * Mathf.Sin(t * 3.2f);
            glint.color = new Color(1f, 0.96f, 0.82f, a * (0.55f + 0.45f * breathe));
            glint.rectTransform.localRotation = Quaternion.Euler(0f, 0f, t * 25f);
            ring.color = new Color(1f, 0.9f, 0.62f, a * (0.35f + 0.5f * (1f - breathe)));
            ring.rectTransform.localScale = Vector3.one * (0.55f + 0.45f * breathe);
        }
    }
}
