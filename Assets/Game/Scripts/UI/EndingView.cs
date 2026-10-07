using System.Collections;
using TMPro;
using UnityEngine;

namespace LostAndFound
{
    /// <summary>The ending: the final photograph and the epilogue lines, then the week summed up (WeekSummary).</summary>
    public static class EndingView
    {
        public static IEnumerator Show(Director d, EndingDef e)
        {
            var root = UIRoot.I.root;
            UIRoot.I.PushModal();
            AudioDirector.Music("finale", 2f);
            var panel = UiKit.Rect("Ending", root).Fill();
            var group = UiKit.Group(panel.gameObject);
            group.alpha = 0f;
            var bg = UiKit.Image(panel, "Bg", null, new Color(0.03f, 0.025f, 0.02f, 1f));
            bg.rectTransform.Fill();
            bg.raycastTarget = true;

            var photoTex = e != null ? Resources.Load<Texture2D>("Photos/" + e.photo) : null;
            var photo = UiKit.Rect("Photo", panel).Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)).Place(new Vector2(-380f, 30f), new Vector2(620f, 740f));
            var frame = UiKit.Image(photo, "Frame", "photo_border", new Color(0.96f, 0.93f, 0.86f), 0f);
            frame.rectTransform.Fill();
            var raw = new GameObject("Img", typeof(RectTransform)).AddComponent<UnityEngine.UI.RawImage>();
            raw.transform.SetParent(photo, false);
            raw.rectTransform.Fill(30f);
            raw.rectTransform.offsetMin = new Vector2(30f, 120f);
            raw.texture = photoTex;
            raw.color = photoTex != null ? Color.white : new Color(0.2f, 0.18f, 0.16f);
            photo.localRotation = Quaternion.Euler(0f, 0f, -3f);

            var title = UiKit.Label(panel, "Title", e != null ? e.title : "The End", Fonts.Title, 84f, new Color(0.96f, 0.9f, 0.78f), TextAlignmentOptions.TopLeft);
            title.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 1f)).Place(new Vector2(20f, 360f), new Vector2(820f, 120f));
            // "[condition]text" lines only appear if that thread of the story played out
            var lines = new System.Collections.Generic.List<string>();
            if (e != null)
                foreach (var l in e.lines)
                {
                    if (l.StartsWith("[") && l.IndexOf(']') > 0)
                    {
                        int close = l.IndexOf(']');
                        if (!d.State.Check(l.Substring(1, close - 1))) continue;
                        lines.Add(l.Substring(close + 1));
                    }
                    else lines.Add(l);
                }
            string text = string.Join("\n", lines);
            var body = UiKit.Label(panel, "Body", text, Fonts.Body, 30f, new Color(0.88f, 0.82f, 0.72f), TextAlignmentOptions.TopLeft);
            body.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 1f)).Place(new Vector2(24f, 230f), new Vector2(800f, 560f));
            body.paragraphSpacing = 18f;
            body.enableAutoSizing = true;
            body.fontSizeMin = 20f;
            body.fontSizeMax = 30f;
            body.maxVisibleCharacters = 0;

            yield return Tween.Run(1.5f, k => group.alpha = k, Ease.InOutSine, true);
            yield return UIRoot.I.fader.FadeTo(0f, 0.01f);
            body.ForceMeshUpdate();
            int total = body.textInfo.characterCount;
            float shown = 0f;
            while (shown < total)
            {
                shown += Time.deltaTime * 60f;
                body.maxVisibleCharacters = (int)shown;
                if (shown > 20 && InputX.LeftDown) shown = total;
                yield return null;
            }
            body.maxVisibleCharacters = total;
            bool done = false;
            var b = UiKit.Button(panel, "Done", "Close the shutter  ›", Fonts.Title, 40f, () => done = true);
            b.GetComponent<RectTransform>().Anchor(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f)).Place(new Vector2(420f, 70f), new Vector2(460f, 60f));
            b.normal = new Color(0.95f, 0.88f, 0.74f);
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
            yield return WeekSummary.Show(d);
            Game.I.ToTitle();
        }
    }
}
