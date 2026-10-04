using System.Collections;
using TMPro;
using UnityEngine;

namespace LostAndFound
{
    /// <summary>The ending: the final photograph, the epilogue lines, and the credits.</summary>
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
            string text = e != null ? string.Join("\n\n", e.lines) : "";
            var body = UiKit.Label(panel, "Body", text, Fonts.Body, 30f, new Color(0.88f, 0.82f, 0.72f), TextAlignmentOptions.TopLeft);
            body.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 1f)).Place(new Vector2(24f, 230f), new Vector2(800f, 560f));
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
                if (shown > 20 && (UnityEngine.InputSystem.Mouse.current?.leftButton.wasPressedThisFrame ?? false)) shown = total;
                yield return null;
            }
            body.maxVisibleCharacters = total;
            bool done = false;
            var b = UiKit.Button(panel, "Done", "Close the shutter  ›", Fonts.Title, 40f, () => done = true);
            b.GetComponent<RectTransform>().Anchor(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f)).Place(new Vector2(420f, 70f), new Vector2(460f, 60f));
            b.normal = new Color(0.95f, 0.88f, 0.74f);
            while (!done) yield return null;
            yield return UIRoot.I.fader.FadeTo(1f, 1f);
            Object.Destroy(panel.gameObject);
            UIRoot.I.PopModal();
            Game.I.ToTitle();
        }
    }
}
