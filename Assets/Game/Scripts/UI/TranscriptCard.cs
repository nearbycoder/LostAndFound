using TMPro;
using UnityEngine;

namespace LostAndFound
{
    /// <summary>
    /// "What they said": the claim's transcript on a card beside the slip while you read it (hover, Tab or View, and
    /// while a stamp is over it). It sits in the space to the right of the slip, measured from where the slip is on
    /// screen this frame, so it never covers the slip or the stamps. Nothing to click and no key of its own.
    /// </summary>
    public class TranscriptCard : MonoBehaviour
    {
        const float Gap = 34f, Edge = 30f, Top = 44f, Bottom = 124f, MaxWidth = 640f, MinWidth = 300f;
        const float PadX = 30f, PadTop = 64f, PadBottom = 26f;

        RectTransform card;
        CanvasGroup group;
        TextMeshProUGUI head, body;
        float a;
        int laidOutVersion = -1;
        Vector2 laidOutSize;
        float scaleUsed;

        /// <summary>Lines left out at the top (after the opening) because the claim ran too long to fit.</summary>
        public int Skipped { get; private set; }
        /// <summary>There's room for it beside the slip at this window size.</summary>
        public bool Fits { get; private set; }
        public bool Visible => a > 0.98f;
        /// <summary>Where the card and the slip are, in screen pixels (the AutoPilot checks they don't overlap).</summary>
        public Rect ScreenRect { get; private set; }
        public Rect SlipScreenRect { get; private set; }

        public static TranscriptCard Create(RectTransform root)
        {
            var rt = UiKit.Rect("Transcript", root).Anchor(Vector2.zero, Vector2.zero, new Vector2(0f, 1f)).Place(new Vector2(1200f, 900f), new Vector2(560f, 400f));
            var t = rt.gameObject.AddComponent<TranscriptCard>();
            t.card = rt;
            t.group = UiKit.Group(rt.gameObject);
            t.group.alpha = 0f;
            t.group.blocksRaycasts = false;
            var shadow = UiKit.Image(rt, "Shadow", "paper_card", new Color(0, 0, 0, 0.35f), 28f);
            shadow.rectTransform.Fill(-4f);
            shadow.rectTransform.anchoredPosition = new Vector2(5f, -7f);
            var paper = UiKit.Image(rt, "Paper", "paper_card", UiKit.Paper, 28f);
            paper.rectTransform.Fill();
            t.head = UiKit.Label(rt, "Head", "WHAT THEY SAID", Fonts.Type, 19f, UiKit.Oxblood, TextAlignmentOptions.TopLeft);
            t.head.rectTransform.Fill();
            t.head.margin = new Vector4(PadX, 24f, PadX, 0f);
            t.body = UiKit.Label(rt, "Body", "", Fonts.Body, 26f, UiKit.Ink, TextAlignmentOptions.TopLeft);
            t.body.rectTransform.Fill();
            t.body.margin = new Vector4(PadX, PadTop, PadX, PadBottom);
            t.body.lineSpacing = -8f;
            t.body.paragraphSpacing = 8f;
            return t;
        }

        void LateUpdate()
        {
            var d = Director.I;
            var slip = ClaimSlip.I;
            bool want = d != null && slip != null && slip.Focused && d.Current != null && d.Transcript.Count > 0
                        && !UIRoot.ModalOpen && !d.Asking && !UIRoot.I.dialogue.Typing && !UIRoot.I.dialogue.Waiting;
            if (want) Layout(d.Transcript, slip);
            want &= Fits;
            a = Mathf.MoveTowards(a, want ? 1f : 0f, Time.unscaledDeltaTime * 6f);
            group.alpha = Ease.OutCubic(a);
        }

        void Layout(Transcript tr, ClaimSlip slip)
        {
            var cam = Camera.main;
            var root = UIRoot.I.root;
            if (cam == null || root == null) { Fits = false; return; }
            float cw = root.rect.width, ch = root.rect.height;
            float k = cw / Mathf.Max(1, Screen.width);   // screen pixels to canvas units

            // the slip's corners on screen, this frame (the camera eases in, so follow it)
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                var local = new Vector3((i & 1) == 0 ? -ClaimSlip.W / 2 : ClaimSlip.W / 2, 0f, (i & 2) == 0 ? -ClaimSlip.H / 2 : ClaimSlip.H / 2);
                var sp = cam.WorldToScreenPoint(slip.transform.TransformPoint(local));
                minX = Mathf.Min(minX, sp.x); maxX = Mathf.Max(maxX, sp.x);
                minY = Mathf.Min(minY, sp.y); maxY = Mathf.Max(maxY, sp.y);
            }
            SlipScreenRect = Rect.MinMaxRect(minX, minY, maxX, maxY);

            float left = maxX * k + Gap;
            float avail = cw - Edge - left;
            Fits = avail >= MinWidth;
            if (!Fits) return;
            float width = Mathf.Min(MaxWidth, avail);
            float maxHeight = ch - Top - Bottom;

            float s = UiKit.TextScale;
            if (tr.Version != laidOutVersion || !Mathf.Approximately(width, laidOutSize.x) || !Mathf.Approximately(maxHeight, laidOutSize.y) || s != scaleUsed)
            {
                laidOutVersion = tr.Version;
                laidOutSize = new Vector2(width, maxHeight);
                scaleUsed = s;
                head.fontSize = 19f * s;
                body.fontSize = 26f * s;
                float headRoom = PadTop * s, innerW = width - 2 * PadX, innerH = maxHeight - headRoom - PadBottom;
                body.margin = new Vector4(PadX, headRoom, PadX, PadBottom);
                // a long claim: a size smaller first, then keep the opening and as many of the newest lines as fit
                int skip = 0;
                string text = tr.Format(0);
                float h = body.GetPreferredValues(text, innerW, 0f).y;
                if (h > innerH)
                {
                    body.fontSize = 23f * s;
                    h = body.GetPreferredValues(text, innerW, 0f).y;
                }
                while (h > innerH && skip < tr.Count - 2)
                {
                    skip++;
                    text = tr.Format(skip);
                    h = body.GetPreferredValues(text, innerW, 0f).y;
                }
                Skipped = skip;
                body.text = text;
                card.sizeDelta = new Vector2(width, Mathf.Min(maxHeight, h + headRoom + PadBottom + 6f));
            }
            // level with the top of the slip, rising above it only as far as a long claim needs
            float top = Mathf.Clamp(maxY * k, Bottom + card.sizeDelta.y, ch - Top);
            card.anchoredPosition = new Vector2(left, top);
            ScreenRect = new Rect(left / k, (top - card.sizeDelta.y) / k, card.sizeDelta.x / k, card.sizeDelta.y / k);
        }
    }
}
