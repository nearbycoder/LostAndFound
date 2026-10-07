using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;

namespace LostAndFound
{
    /// <summary>
    /// After Friday's ending: the week on one page of the ledger book. Each day's tally by the rules and its stamps, the
    /// findings noted, the curiosities found, and the endings reached so far (the ones not yet reached are only "another
    /// ending", so nothing is spoiled), with a pointer to Choose a Day.
    /// </summary>
    public static class WeekSummary
    {
        public static IEnumerator Show(Director d)
        {
            var root = UIRoot.I.root;
            UIRoot.I.PushModal();
            var panel = UiKit.Rect("Week", root).Fill();
            var group = UiKit.Group(panel.gameObject);
            group.alpha = 0f;
            var bg = UiKit.Image(panel, "Bg", null, new Color(0.03f, 0.025f, 0.02f, 1f));
            bg.rectTransform.Fill();
            bg.raycastTarget = true;

            var book = UiKit.Image(panel, "Book", "ledger_page", new Color(0.95f, 0.91f, 0.81f), 40f);
            book.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)).Place(new Vector2(0f, 20f), new Vector2(1500f, 900f));
            // with its button below, the page reaches 470 above the middle and 520 below: a short (21:9) or narrow screen scales it
            book.rectTransform.localScale = Vector3.one * Mathf.Min(1f, (root.rect.height / 2f - 10f) / 520f, (root.rect.width - 40f) / 1500f);
            var title = UiKit.Label(book.transform, "Title", "The Week", Fonts.Title, 64f, UiKit.Ink, TextAlignmentOptions.Top);
            title.rectTransform.Fill();
            title.margin = new Vector4(60f, 40f, 60f, 0f);
            var first = d.Db.Day(1);
            var last = d.Db.Day(d.Db.DayCount);
            var sub = UiKit.Label(book.transform, "Date", $"Ninefold Junction, Lost Property  ·  {Short(first?.date)} to {Short(last?.date)}", Fonts.TitleItalic, 30f, UiKit.InkSoft, TextAlignmentOptions.Top);
            sub.rectTransform.Fill();
            sub.margin = new Vector4(60f, 118f, 60f, 0f);

            // a row a day: name and title, the tally, and the day's brass stamps
            var log = new System.Text.StringBuilder("[Week]");
            int allCorrect = 0, allTotal = 0;
            float y = -200f;
            for (int day = 1; day <= d.Db.DayCount; day++)
            {
                var def = d.Db.Day(day);
                if (def == null) continue;
                var (stamps, correct, total) = Rules.Score(def, d.State);
                allCorrect += correct;
                allTotal += total;
                log.Append($" day{day} {correct}/{total} ({stamps} stamps);");
                var name = UiKit.Label(book.transform, "Day", $"<font=\"SpecialElite\"><size=70%>{Weekday(def.date).ToUpperInvariant()}</size></font>   {def.title}", Fonts.Hand, 36f, UiKit.Ink, TextAlignmentOptions.MidlineLeft);
                name.rectTransform.Anchor(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f)).Place(new Vector2(110f, y), new Vector2(640f, 56f));
                name.enableAutoSizing = true;
                name.fontSizeMin = 24f;
                name.fontSizeMax = 36f;
                var tally = UiKit.Label(book.transform, "Tally", total > 0 ? $"{correct} of {total} by the rules" : "nothing decided", Fonts.Hand, 34f, UiKit.Ink, TextAlignmentOptions.MidlineLeft);
                tally.rectTransform.Anchor(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f)).Place(new Vector2(780f, y), new Vector2(340f, 56f));
                for (int i = 0; i < 3; i++)
                {
                    var st = UiKit.Image(book.transform, "Stamp", "brass_stamp", i < stamps ? Color.white : new Color(1f, 1f, 1f, 0.18f));
                    st.rectTransform.Anchor(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f)).Place(new Vector2(1292f + i * 54f, y), new Vector2(50f, 50f));   // right of the margin rule, like the ledger's marks
                }
                y -= 74f;
            }

            // the rest of the week: findings, curiosities, endings
            var recs = d.State.records.Where(r => r.detailsTotal > 0 && r.grade != "skip").ToList();
            var (curios, curioTotal) = Curios.Count(d.Db, d.Save.discovered);
            var endings = d.Db.root.endings.Select(e => (e.id, e.title)).GroupBy(e => e.id).Select(g => g.First()).ToList();
            var seen = d.Save.endingsSeen ?? new System.Collections.Generic.List<string>();
            int reached = endings.Count(e => seen.Contains(e.id));
            string endingList = string.Join("   ·   ", endings.Select(e => seen.Contains(e.id) ? e.title : "<color=#6a5a50><i>another ending</i></color>"));
            log.Append($" {allCorrect}/{allTotal} by the rules; findings {recs.Sum(r => r.detailsFound)}/{recs.Sum(r => r.detailsTotal)}; curios {curios}/{curioTotal}; endings {reached}/{endings.Count} ({string.Join(",", seen)})");
            var facts = UiKit.Label(book.transform, "Facts",
                $"{allCorrect} of {allTotal} by the rules this week   <color=#5a4a40>·  {recs.Sum(r => r.detailsFound)} of {recs.Sum(r => r.detailsTotal)} findings noted  ·  {curios} of {curioTotal} curiosities</color>\n" +
                $"Endings reached: {reached} of {endings.Count}   <size=85%>{endingList}</size>",
                Fonts.Hand, 32f, UiKit.Ink, TextAlignmentOptions.TopLeft);
            facts.rectTransform.Anchor(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f)).Place(new Vector2(110f, y - 10f), new Vector2(-220f, 110f));
            facts.enableAutoSizing = true;
            facts.fontSizeMin = 22f;
            facts.fontSizeMax = 32f;
            facts.lineSpacing = 10f;
            var hint = UiKit.Label(book.transform, "Hint", "Any day can be played again from its morning: Choose a Day, on the title.", Fonts.Body, 24f, UiKit.InkSoft, TextAlignmentOptions.TopLeft);
            hint.rectTransform.Anchor(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f)).Place(new Vector2(110f, y - 130f), new Vector2(-220f, 40f));
            hint.fontStyle = FontStyles.Italic;
            Debug.Log(log.ToString());

            bool done = false;
            var b = UiKit.Button(book.transform, "Done", "Back to the title  ›", Fonts.Title, 40f, () => done = true);
            b.GetComponent<RectTransform>().Anchor(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f)).Place(new Vector2(0f, -90f), new Vector2(460f, 60f));
            b.normal = new Color(0.95f, 0.88f, 0.74f);

            yield return Tween.Run(0.8f, k => group.alpha = k, Ease.InOutSine, true);
            yield return UIRoot.I.fader.FadeTo(0f, 0.01f);
            float waited = 0f;
            while (!done)
            {
                waited += Time.deltaTime;
                if (Director.AutoAdvance && waited > 8f) done = true;
                yield return null;
            }
            yield return UIRoot.I.fader.FadeTo(1f, 1f);
            Object.Destroy(panel.gameObject);
            UIRoot.I.PopModal();
        }

        /// <summary>"Monday 15 October 1962" → "Monday".</summary>
        static string Weekday(string date) => string.IsNullOrEmpty(date) ? "" : date.Split(' ')[0];

        /// <summary>"Monday 15 October 1962" → "15 October 1962".</summary>
        static string Short(string date)
        {
            if (string.IsNullOrEmpty(date)) return "";
            int sp = date.IndexOf(' ');
            return sp > 0 ? date.Substring(sp + 1) : date;
        }
    }
}
