using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostAndFound
{
    /// <summary>
    /// The evening ledger: every case of the day is stamped right or wrong, one by one, with the
    /// reason; then the day's stamps, and the Ninefold Gazette's headline.
    /// </summary>
    public static class LedgerView
    {
        const float BookWidth = 1500f, GazetteRoom = 1830f - 1500f;

        /// <summary>The spread at full size if it fits (1080-high, 16:9), smaller on a narrower or shorter screen.</summary>
        static float FitScale(RectTransform root, float width) =>
            Mathf.Min(1f, (root.rect.width - 40f) / width, (root.rect.height / 2f - 10f) / 520f);

        public static IEnumerator Show(Director d)
        {
            var root = UIRoot.I.root;
            UIRoot.I.PushModal();
            AudioDirector.Muffle(false);
            AudioDirector.Music("ledger", 1.5f);
            var panel = UiKit.Rect("Ledger", root).Fill();
            var group = UiKit.Group(panel.gameObject);
            group.alpha = 0f;
            var dim = UiKit.Image(panel, "Dim", null, new Color(0.04f, 0.03f, 0.03f, 0.95f));
            dim.rectTransform.Fill();
            dim.raycastTarget = true;

            // the open ledger book, and the Gazette standing in its right margin beside the rows (never over them). The two
            // together are 1830 wide, and with the buttons below the book they reach 470 above the middle and 520 below: on a
            // narrower (4:3) or shorter (21:9) screen the whole spread scales down to fit. A day with no Gazette centres the book.
            var g = d.DayDef.gazette.FirstOrDefault(x => d.State.Check(x.condition));
            float spreadWidth = BookWidth + (g != null ? GazetteRoom : 0f), BookX = -(spreadWidth - BookWidth) / 2f;
            var spread = UiKit.Rect("Spread", panel).Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)).Place(Vector2.zero, new Vector2(spreadWidth, 900f));
            spread.localScale = Vector3.one * FitScale(root, spreadWidth);
            var book = UiKit.Image(spread, "Book", "ledger_page", new Color(0.95f, 0.91f, 0.81f), 40f);
            book.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)).Place(new Vector2(BookX, 20f), new Vector2(1500f, 900f));
            var title = UiKit.Label(book.transform, "Title", "The Day Ledger", Fonts.Title, 64f, UiKit.Ink, TextAlignmentOptions.Top);
            title.rectTransform.Fill();
            title.margin = new Vector4(60f, 40f, 60f, 0f);
            var date = UiKit.Label(book.transform, "Date", $"{d.DayDef.date}  ·  {d.DayDef.title}", Fonts.TitleItalic, 30f, UiKit.InkSoft, TextAlignmentOptions.Top);
            date.rectTransform.Fill();
            date.margin = new Vector4(60f, 118f, 60f, 0f);

            var rows = new List<(TextMeshProUGUI line, TextMeshProUGUI why, Image stamp, CaseRecord rec)>();
            // a row is 104 high with a one-line explanation; a longer one gets the room it needs, and if the day's rows would
            // then run into the tally, they close up a little
            const float whyWidth = 1500f - 110f - 340f, step = 104f;
            float WhyHeight(string text)
            {
                var probe = UiKit.Label(book.transform, "Probe", text, Fonts.Body, 24f, UiKit.InkSoft, TextAlignmentOptions.TopLeft);
                probe.fontStyle = FontStyles.Italic;
                float h = probe.GetPreferredValues(text, whyWidth, 0f).y;
                Object.Destroy(probe.gameObject);
                return Mathf.Max(34f, h);
            }
            var heights = d.DayDef.cases.Select(c => d.State.Record(c.id)).Where(r => r != null).Select(r => WhyHeight(r.ledger ?? "")).ToList();
            float extra = heights.Sum(h => h - 34f);
            float squeeze = Mathf.Clamp((560f - extra) / Mathf.Max(1, heights.Count) , 84f, step);
            int row = 0;
            float y = -190f;
            foreach (var c in d.DayDef.cases)
            {
                var rec = d.State.Record(c.id);
                if (rec == null) continue;
                var def = d.Db.Object(c.wants);
                var who = string.Join(" & ", c.claimants.Select(id => d.Db.Commuter(id)?.name ?? id));
                string verdict = rec.verdict switch { "return" => "Returned" + (string.IsNullOrEmpty(rec.to) ? "" : " to " + (d.Db.Commuter(rec.to)?.ShortName ?? "")), "seal" => "Sealed in the Iron Drawer", "missing" => "Not here", _ => "Refused" };
                var line = UiKit.Label(book.transform, "Line", $"<font=\"SpecialElite\"><size=70%>{who.ToUpperInvariant()}</size></font>   {(def != null ? def.name : "<i>nothing in storage</i>")}   <color=#5a4a40>—  {verdict}</color>", Fonts.Hand, 36f, UiKit.Ink, TextAlignmentOptions.TopLeft);
                // it ends where the "found" column begins (372 from the page's right edge), never under it
                line.rectTransform.Anchor(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f)).Place(new Vector2(90f, y), new Vector2(-90f - 372f, 44f));
                // one line, always: a long name, object and verdict (in the wider Plain lettering face) writes a little smaller
                line.enableAutoSizing = true;
                line.fontSizeMin = 24f;
                line.fontSizeMax = 36f;
                var why = UiKit.Label(book.transform, "Why", rec.ledger ?? "", Fonts.Body, 24f, UiKit.InkSoft, TextAlignmentOptions.TopLeft);
                float whyH = heights[row++];
                why.rectTransform.Anchor(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f)).Place(new Vector2(110f, y - 44f), new Vector2(-340f, whyH));
                why.fontStyle = FontStyles.Italic;
                why.alpha = 0f;
                var stamp = UiKit.Image(book.transform, "Mark", rec.grade == "best" ? "mark_tick" : rec.grade == "ok" ? "mark_half" : rec.grade == "skip" ? "mark_dash" : "mark_cross", Color.white);
                stamp.rectTransform.Anchor(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f)).Place(new Vector2(-160f, y - 26f), new Vector2(84f, 84f));
                stamp.color = rec.grade == "best" ? DeskMaterials.ReturnInk : rec.grade == "ok" ? new Color(0.62f, 0.45f, 0.12f) : rec.grade == "skip" ? UiKit.InkSoft : DeskMaterials.RefuseInk;
                stamp.enabled = false;
                if (rec.detailsTotal > 0)
                {
                    // how thoroughly you looked: case details noted out of those the object had
                    var found = UiKit.Label(book.transform, "Found", $"{rec.detailsFound} of {rec.detailsTotal} found", Fonts.Hand, 26f, UiKit.InkSoft, TextAlignmentOptions.TopRight);
                    found.rectTransform.Anchor(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f)).Place(new Vector2(-212f, y - 4f), new Vector2(150f, 40f));
                }
                rows.Add((line, why, stamp, rec));
                y -= squeeze + (whyH - 34f);
            }

            yield return Tween.Run(0.6f, k => group.alpha = k, Ease.InOutSine, true);
            yield return UIRoot.I.fader.FadeTo(0f, 0.01f);
            yield return new WaitForSeconds(0.4f);
            int n = 0;
            foreach (var r in rows)
            {
                r.stamp.enabled = true;
                r.stamp.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-14f, 14f));
                yield return Tween.Run(0.18f, k => r.stamp.rectTransform.localScale = Vector3.one * Mathf.Lerp(2.2f, 1f, Ease.OutCubic(k)), Ease.Linear, true);
                AudioDirector.Play(r.rec.grade == "best" ? "ledger_tick" : r.rec.grade == "ok" ? "ledger_half" : "ledger_cross", 0.8f, 1f + n * 0.06f);
                AudioDirector.Play("stamp_thump", 0.5f, 1.15f);
                d.StartCoroutine(Tween.Run(0.4f, k => r.why.alpha = k, Ease.Linear, true));
                n++;
                yield return new WaitForSeconds(0.45f);
            }

            var (stamps, correct, total) = d.Score(d.Day);
            var recs = d.DayDef.cases.Select(c => d.State.Record(c.id)).Where(r => r != null && r.detailsTotal > 0).ToList();
            string findings = recs.Count == 0 ? "" : $"   <size=70%><color=#5a4a40>·  {recs.Sum(r => r.detailsFound)} of {recs.Sum(r => r.detailsTotal)} findings noted</color></size>";
            var tally = UiKit.Label(book.transform, "Tally", $"{correct} of {total} by the rules{findings}", Fonts.Hand, 40f, UiKit.Ink, TextAlignmentOptions.BottomLeft);
            tally.rectTransform.Fill();
            tally.margin = new Vector4(90f, 0f, 60f, 130f);
            for (int i = 0; i < 3; i++)
            {
                var st = UiKit.Image(book.transform, "Stamp" + i, "brass_stamp", i < stamps ? Color.white : new Color(1f, 1f, 1f, 0.18f));
                st.rectTransform.Anchor(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 0.5f)).Place(new Vector2(130f + i * 96f, 80f), new Vector2(84f, 84f));
                if (i < stamps)
                {
                    yield return Tween.Run(0.2f, k => st.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.8f, 1f, Ease.OutBack(k)), Ease.Linear, true);
                    AudioDirector.Play("brass_stamp", 0.7f, 1f + i * 0.12f);
                }
            }

            // the Gazette
            if (g != null)
            {
                // a tall clipping: masthead, the headline in its own band, then the story
                var paper = UiKit.Image(spread, "Gazette", "gazette_tall", new Color(0.88f, 0.85f, 0.76f), 0f);
                paper.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)).Place(new Vector2(BookX + 750f - 70f + 200f, 0f), new Vector2(400f, 640f));
                paper.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 2.5f);
                var mast = UiKit.Label(paper.transform, "Mast", "The Ninefold Gazette", Fonts.Title, 30f, UiKit.Ink, TextAlignmentOptions.Top);
                mast.rectTransform.Fill();
                mast.margin = new Vector4(16f, 12f, 16f, 0f);
                mast.enableAutoSizing = true;
                mast.fontSizeMin = 22f;
                mast.fontSizeMax = 30f;
                var head = UiKit.Label(paper.transform, "Head", g.headline, Fonts.Title, 34f, UiKit.Ink, TextAlignmentOptions.Top);
                head.rectTransform.Fill();
                head.margin = new Vector4(22f, 72f, 22f, 640f - 262f);   // its own band above the story
                head.fontStyle = FontStyles.Bold;
                head.enableAutoSizing = true;   // a four-line headline writes smaller rather than over the story
                head.fontSizeMin = 22f;
                head.fontSizeMax = 34f;
                var body = UiKit.Label(paper.transform, "Body", g.body, Fonts.Body, 23f, UiKit.InkSoft, TextAlignmentOptions.TopJustified);
                body.rectTransform.Fill();
                body.margin = new Vector4(26f, 276f, 26f, 86f);   // above the small print along the foot
                body.enableAutoSizing = true;
                body.fontSizeMin = 18f;
                body.fontSizeMax = 23f;
                AudioDirector.Play("newspaper", 0.7f);
                yield return Tween.Run(0.5f, k =>
                {
                    paper.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.2f, 1f, Ease.OutBack(k));
                    paper.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-200f, 2.5f, Ease.OutCubic(k)));
                }, Ease.Linear, true);
            }

            // buttons
            bool next = false, replay = false;
            var nb = UiKit.Button(book.transform, "Next", d.Day >= d.Db.DayCount ? "Close the desk  ›" : "Next morning  ›", Fonts.Title, 40f, () => next = true);
            nb.GetComponent<RectTransform>().Anchor(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f)).Place(new Vector2(-60f, -90f), new Vector2(420f, 60f));
            nb.normal = new Color(0.95f, 0.88f, 0.74f);
            var rb = UiKit.Button(book.transform, "Replay", "Replay the day", Fonts.TitleItalic, 30f, () => replay = true);
            rb.GetComponent<RectTransform>().Anchor(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f)).Place(new Vector2(320f, -88f), new Vector2(300f, 60f));
            rb.normal = new Color(0.8f, 0.72f, 0.6f);
            float waited = 0f;
            while (!next && !replay)
            {
                waited += Time.deltaTime;
                if (Director.AutoAdvance && waited > 2.5f) next = true;
                yield return null;
            }

            yield return UIRoot.I.fader.FadeTo(1f, 0.8f);
            Object.Destroy(panel.gameObject);
            UIRoot.I.PopModal();
            if (replay) d.ReplayDay(d.Day);
            else d.FinishDay();
        }
    }
}
