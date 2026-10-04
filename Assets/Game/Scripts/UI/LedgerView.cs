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

            // the open ledger book
            var book = UiKit.Image(panel, "Book", "ledger_page", new Color(0.95f, 0.91f, 0.81f), 40f);
            book.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)).Place(new Vector2(0f, 20f), new Vector2(1500f, 900f));
            var title = UiKit.Label(book.transform, "Title", "The Day Ledger", Fonts.Title, 64f, UiKit.Ink, TextAlignmentOptions.Top);
            title.rectTransform.Fill();
            title.margin = new Vector4(60f, 40f, 60f, 0f);
            var date = UiKit.Label(book.transform, "Date", $"{d.DayDef.date}  ·  {d.DayDef.title}", Fonts.TitleItalic, 30f, UiKit.InkSoft, TextAlignmentOptions.Top);
            date.rectTransform.Fill();
            date.margin = new Vector4(60f, 118f, 60f, 0f);

            var rows = new List<(TextMeshProUGUI line, TextMeshProUGUI why, Image stamp, CaseRecord rec)>();
            float y = -190f;
            foreach (var c in d.DayDef.cases)
            {
                var rec = d.State.Record(c.id);
                if (rec == null) continue;
                var def = d.Db.Object(c.wants);
                var who = string.Join(" & ", c.claimants.Select(id => d.Db.Commuter(id)?.name ?? id));
                string verdict = rec.verdict switch { "return" => "Returned" + (string.IsNullOrEmpty(rec.to) ? "" : " to " + (d.Db.Commuter(rec.to)?.name.Split(' ')[0] ?? "")), "seal" => "Sealed in the Iron Drawer", "missing" => "Not here", _ => "Refused" };
                var line = UiKit.Label(book.transform, "Line", $"<font=\"SpecialElite\"><size=70%>{who.ToUpperInvariant()}</size></font>   {(def != null ? def.name : "<i>nothing in storage</i>")}   <color=#5a4a40>—  {verdict}</color>", Fonts.Hand, 36f, UiKit.Ink, TextAlignmentOptions.TopLeft);
                line.rectTransform.Anchor(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f)).Place(new Vector2(90f, y), new Vector2(-320f, 44f));
                var why = UiKit.Label(book.transform, "Why", rec.ledger ?? "", Fonts.Body, 24f, UiKit.InkSoft, TextAlignmentOptions.TopLeft);
                why.rectTransform.Anchor(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f)).Place(new Vector2(110f, y - 44f), new Vector2(-340f, 34f));
                why.fontStyle = FontStyles.Italic;
                why.alpha = 0f;
                var stamp = UiKit.Image(book.transform, "Mark", rec.grade == "best" ? "mark_tick" : rec.grade == "ok" ? "mark_half" : rec.grade == "skip" ? "mark_dash" : "mark_cross", Color.white);
                stamp.rectTransform.Anchor(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f)).Place(new Vector2(-160f, y - 26f), new Vector2(84f, 84f));
                stamp.color = rec.grade == "best" ? DeskMaterials.ReturnInk : rec.grade == "ok" ? new Color(0.62f, 0.45f, 0.12f) : rec.grade == "skip" ? UiKit.InkSoft : DeskMaterials.RefuseInk;
                stamp.enabled = false;
                rows.Add((line, why, stamp, rec));
                y -= 104f;
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
            var tally = UiKit.Label(book.transform, "Tally", $"{correct} of {total} by the rules", Fonts.Hand, 40f, UiKit.Ink, TextAlignmentOptions.BottomLeft);
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
            var g = d.DayDef.gazette.FirstOrDefault(x => d.State.Check(x.condition));
            if (g != null)
            {
                var paper = UiKit.Image(book.transform, "Gazette", "gazette", new Color(0.88f, 0.85f, 0.76f), 0f);
                paper.rectTransform.Anchor(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f)).Place(new Vector2(-70f, 50f), new Vector2(600f, 300f));
                paper.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 2.5f);
                var mast = UiKit.Label(paper.transform, "Mast", "The Ninefold Gazette", Fonts.Title, 30f, UiKit.Ink, TextAlignmentOptions.Top);
                mast.rectTransform.Fill();
                mast.margin = new Vector4(20f, 14f, 20f, 0f);
                var head = UiKit.Label(paper.transform, "Head", g.headline, Fonts.Title, 34f, UiKit.Ink, TextAlignmentOptions.Top);
                head.rectTransform.Fill();
                head.margin = new Vector4(26f, 60f, 26f, 0f);
                head.fontStyle = FontStyles.Bold;
                var body = UiKit.Label(paper.transform, "Body", g.body, Fonts.Body, 21f, UiKit.InkSoft, TextAlignmentOptions.TopJustified);
                body.rectTransform.Fill();
                body.margin = new Vector4(30f, 160f, 30f, 16f);
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
