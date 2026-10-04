using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LostAndFound
{
    /// <summary>
    /// The title: the booth at night behind a paper card. Continue, a new week, any day you've reached,
    /// settings. Shows how many of the week's secret curiosities you've found.
    /// </summary>
    public static class TitleScreen
    {
        public static bool Exists => true;
        static RectTransform panel;
        public static bool Showing => panel != null;

        /// <summary>Start a new week as if "Begin" had been clicked (tests, recordings).</summary>
        public static void Begin(Game g) => Start(g, g.NewWeek);

        public static void Show(Game g)
        {
            if (panel != null) Object.Destroy(panel.gameObject);
            UIRoot.I.PushModal();
            AudioDirector.Music("title", 2f);
            AudioDirector.Ambience("amb_concourse", "amb_rain");
            CameraRig.I.SetView(View.Counter, true);
            Director.I.Init(g.Db, g.Save);
            Desk.I.SpawnItems(g.Db, Mathf.Clamp(g.Save.currentDay, 1, g.Db.DayCount), g.Save.state);
            Director.I.ApplyStoryVisuals();

            panel = UiKit.Rect("Title", UIRoot.I.root).Fill();
            var group = UiKit.Group(panel.gameObject);
            // darken the left of the scene behind the menu, fading out towards the middle
            var ramp = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int i = 0; i < 64; i++) ramp.SetPixel(i, 0, new Color(0.02f, 0.02f, 0.03f, 0.78f * Mathf.Pow(1f - i / 63f, 1.3f)));
            ramp.Apply();
            var shade = new GameObject("Shade", typeof(RectTransform)).AddComponent<RawImage>();
            shade.transform.SetParent(panel, false);
            shade.texture = ramp;
            shade.rectTransform.Fill();
            shade.rectTransform.anchorMax = new Vector2(0.75f, 1f);
            shade.raycastTarget = true;

            var title = UiKit.Label(panel, "Name", "Lost & Found", Fonts.Sign, 132f, new Color(0.97f, 0.9f, 0.74f), TextAlignmentOptions.TopLeft);
            title.rectTransform.Anchor(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f)).Place(new Vector2(120f, -110f), new Vector2(1000f, 170f));
            title.characterSpacing = 2f;
            var sub = UiKit.Label(panel, "Sub", "The Lost Property Office  ·  Ninefold Junction  ·  October 1962", Fonts.TitleItalic, 32f, new Color(0.85f, 0.78f, 0.66f), TextAlignmentOptions.TopLeft);
            sub.rectTransform.Anchor(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f)).Place(new Vector2(126f, -270f), new Vector2(1000f, 50f));
            var rule = UiKit.Image(panel, "Rule", null, new Color(0.79f, 0.63f, 0.35f, 0.7f));
            rule.rectTransform.Anchor(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f)).Place(new Vector2(126f, -330f), new Vector2(520f, 2f));

            var list = UiKit.Rect("Menu", panel).Anchor(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f)).Place(new Vector2(120f, -380f), new Vector2(760f, 520f));
            BuildMain(g, list);

            int found = g.Save.discovered.Count(id =>
            {
                int dot = id.IndexOf('.');
                var o = dot > 0 ? g.Db.Object(id.Substring(0, dot)) : null;
                return o != null && o.Detail(id.Substring(dot + 1))?.kind == "secret";
            });
            int total = g.Db.root.objects.Count(o => o.storage != "presented");
            var cur = UiKit.Label(panel, "Curios", $"Curiosities found: {found} of {total}", Fonts.Body, 26f, new Color(0.8f, 0.74f, 0.62f), TextAlignmentOptions.BottomLeft);
            cur.rectTransform.Anchor(Vector2.zero, Vector2.zero, Vector2.zero).Place(new Vector2(126f, 70f), new Vector2(700f, 40f));
            var credit = UiKit.Label(panel, "Credit", "Agnes's desk, as she left it.", Fonts.Agnes, 28f, new Color(0.8f, 0.74f, 0.62f, 0.8f), TextAlignmentOptions.BottomLeft);
            credit.rectTransform.Anchor(Vector2.zero, Vector2.zero, Vector2.zero).Place(new Vector2(126f, 30f), new Vector2(700f, 40f));

            group.alpha = 0f;
            g.StartCoroutine(Fade(group, 1f, 1.2f));
            g.StartCoroutine(UIRoot.I.fader.FadeTo(0f, 1.2f));
        }

        static IEnumerator Fade(CanvasGroup g, float to, float t)
        {
            float from = g.alpha;
            yield return Tween.Run(t, k => g.alpha = Mathf.Lerp(from, to, k), Ease.InOutSine, true);
        }

        static void Clear(RectTransform list)
        {
            foreach (Transform c in list) Object.Destroy(c.gameObject);
        }

        static PaperButton Item(RectTransform list, int i, string text, System.Action act, float size = 50f)
        {
            var b = UiKit.Button(list, "Item", text, Fonts.Title, size, act);
            b.normal = new Color(0.95f, 0.89f, 0.76f);
            b.hover = UiKit.Gold;
            b.label.alignment = TextAlignmentOptions.MidlineLeft;
            b.GetComponent<RectTransform>().Anchor(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f)).Place(new Vector2(0f, -i * 76f), new Vector2(760f, 70f));
            return b;
        }

        static void BuildMain(Game g, RectTransform list)
        {
            Clear(list);
            int i = 0;
            bool started = SaveGame.Exists && (g.Save.currentDay > 1 || g.Save.state.records.Count > 0);
            if (started && !g.Save.finished)
            {
                var day = g.Db.Day(Mathf.Clamp(g.Save.currentDay, 1, g.Db.DayCount));
                Item(list, i++, $"Continue  <size=60%><color=#c8b898>·  {day.weekday}, {day.title}</color></size>", () => Start(g, () => g.BeginWeek(g.Save.currentDay)));
            }
            bool confirm = false;
            PaperButton nw = null;
            nw = Item(list, i++, started ? "A New Week" : "Begin", () =>
            {
                if (started && !confirm)
                {
                    confirm = true;
                    nw.label.text = "A New Week  <size=60%><color=#e0a080>·  click again to start over</color></size>";
                    return;
                }
                Start(g, g.NewWeek);
            });
            if (g.Save.unlockedDay > 1 || g.Save.finished) Item(list, i++, "Choose a Day", () => BuildDays(g, list));
            Item(list, i++, "Settings", () => SettingsPanel.Open(() => { }));
            if (!Application.isEditor && Application.platform != RuntimePlatform.WebGLPlayer) Item(list, i++, "Close the Office", Application.Quit);
        }

        static void BuildDays(Game g, RectTransform list)
        {
            Clear(list);
            for (int d = 1; d <= g.Db.DayCount; d++)
            {
                var def = g.Db.Day(d);
                int day = d;
                bool open = d <= g.Save.unlockedDay;
                var best = g.Save.best.FirstOrDefault(b => b.day == d);
                string stamps = best == null ? "" : "  <size=60%><color=#e0c070>" + new string('●', best.stamps) + new string('○', 3 - best.stamps) + "</color></size>";
                var b = Item(list, d - 1, $"{def.weekday}  <size=60%><color=#c8b898>·  {def.title}</color></size>{stamps}", () => Start(g, () => g.ReplayDay(day)), 44f);
                b.interactable = open;
            }
            Item(list, g.Db.DayCount, "‹  Back", () => BuildMain(g, list), 40f);
        }

        static void Start(Game g, System.Action begin)
        {
            g.StartCoroutine(StartRoutine(g, begin));
        }

        static IEnumerator StartRoutine(Game g, System.Action begin)
        {
            var group = panel.GetComponent<CanvasGroup>();
            group.interactable = false;
            AudioDirector.Play("stamp_thump", 0.5f);
            yield return UIRoot.I.fader.FadeTo(1f, 0.8f);
            Object.Destroy(panel.gameObject);
            panel = null;
            UIRoot.I.PopModal();
            begin();
        }
    }

    /// <summary>Volume, text speed, mouse, comfort and display options. Opens over the title or the pause menu.</summary>
    public static class SettingsPanel
    {
        public static void Open(System.Action onClose)
        {
            UIRoot.I.PushModal();
            var panel = UiKit.Rect("Settings", UIRoot.I.root).Fill();
            var dim = UiKit.Image(panel, "Dim", null, new Color(0f, 0f, 0f, 0.55f));
            dim.rectTransform.Fill();
            dim.raycastTarget = true;
            var card = UiKit.Image(panel, "Card", "paper_card", UiKit.Paper, 28f);
            card.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)).Place(Vector2.zero, new Vector2(900f, 860f));
            var t = UiKit.Label(card.transform, "Title", "Settings", Fonts.Title, 56f, UiKit.Ink, TextAlignmentOptions.Top);
            t.rectTransform.Fill();
            t.margin = new Vector4(0f, 34f, 0f, 0f);

            float y = -130f;
            void Slider(string label, float min, float max, System.Func<float> get, System.Action<float> set)
            {
                var l = UiKit.Label(card.transform, label, label, Fonts.Body, 30f, UiKit.Ink, TextAlignmentOptions.MidlineLeft);
                l.rectTransform.Anchor(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f)).Place(new Vector2(80f, y), new Vector2(300f, 44f));
                var s = PaperSlider.Create(card.transform, new Vector2(380f, y), 420f, Mathf.InverseLerp(min, max, get()), v => set(Mathf.Lerp(min, max, v)));
                y -= 58f;
            }
            void Toggle(string label, System.Func<bool> get, System.Action<bool> set)
            {
                PaperButton b = null;
                string Text() => $"{label}:  <b>{(get() ? "On" : "Off")}</b>";
                b = UiKit.Button(card.transform, label, Text(), Fonts.Body, 30f, () => { set(!get()); b.label.text = Text(); });
                b.label.alignment = TextAlignmentOptions.MidlineLeft;
                b.GetComponent<RectTransform>().Anchor(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f)).Place(new Vector2(80f, y), new Vector2(720f, 44f));
                y -= 54f;
            }
            Slider("Volume", 0f, 1f, () => Settings.MasterVolume, v => Settings.MasterVolume = v);
            Slider("Music", 0f, 1f, () => Settings.MusicVolume, v => Settings.MusicVolume = v);
            Slider("Sound effects", 0f, 1f, () => Settings.SfxVolume, v => Settings.SfxVolume = v);
            Slider("Voices", 0f, 1f, () => Settings.VoiceVolume, v => Settings.VoiceVolume = v);
            Slider("Station sounds", 0f, 1f, () => Settings.AmbienceVolume, v => Settings.AmbienceVolume = v);
            Slider("Text speed", 0.5f, 2.5f, () => Settings.TextSpeed, v => Settings.TextSpeed = v);
            Slider("Turning speed", 0.4f, 2f, () => Settings.MouseSensitivity, v => Settings.MouseSensitivity = v);
            y -= 10f;
            Toggle("Screen shake", () => Settings.ScreenShake, v => Settings.ScreenShake = v);
            Toggle("Reduce motion", () => Settings.ReduceMotion, v => Settings.ReduceMotion = v);
            Toggle("Film effects (grain, blur, vignette)", () => Settings.PostEffects, v => Settings.PostEffects = v);
            Toggle("Fullscreen", () => Settings.Fullscreen, v => { Settings.Fullscreen = v; Game.ApplyDisplay(); });

            var back = UiKit.Button(card.transform, "Back", "Done", Fonts.Title, 44f, () =>
            {
                Object.Destroy(panel.gameObject);
                UIRoot.I.PopModal();
                onClose?.Invoke();
            });
            back.GetComponent<RectTransform>().Anchor(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f)).Place(new Vector2(0f, 34f), new Vector2(300f, 60f));
        }
    }

    /// <summary>An ink-line slider with a brass knob.</summary>
    public class PaperSlider : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        RectTransform track, fill, knob;
        float value;
        System.Action<float> onChange;

        public static PaperSlider Create(Transform parent, Vector2 pos, float width, float value, System.Action<float> onChange)
        {
            var rt = UiKit.Rect("Slider", parent).Anchor(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f)).Place(pos, new Vector2(width, 40f));
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            var s = rt.gameObject.AddComponent<PaperSlider>();
            var line = UiKit.Image(rt, "Track", null, new Color(0.3f, 0.27f, 0.25f, 0.35f));
            line.rectTransform.Anchor(new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f)).Place(Vector2.zero, new Vector2(0f, 4f));
            var f = UiKit.Image(rt, "Fill", null, UiKit.Oxblood);
            f.rectTransform.Anchor(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f)).Place(Vector2.zero, new Vector2(0f, 6f));
            var k = UiKit.Image(rt, "Knob", "ring", UiKit.Brass);
            k.rectTransform.Anchor(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f)).Place(Vector2.zero, new Vector2(30f, 30f));
            s.track = rt;
            s.fill = f.rectTransform;
            s.knob = k.rectTransform;
            s.onChange = onChange;
            s.Set(value, false);
            return s;
        }

        void Set(float v, bool notify)
        {
            value = Mathf.Clamp01(v);
            float w = track.rect.width > 0 ? track.rect.width : track.sizeDelta.x;
            fill.sizeDelta = new Vector2(w * value, 6f);
            knob.anchoredPosition = new Vector2(w * value, 0f);
            if (notify) onChange?.Invoke(value);
        }

        void FromPointer(PointerEventData e)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(track, e.position, e.pressEventCamera, out var local)) return;
            float w = track.rect.width;
            Set((local.x - track.rect.xMin) / Mathf.Max(1f, w), true);
        }

        public void OnPointerDown(PointerEventData e) { FromPointer(e); AudioDirector.Play("ui_click", 0.3f); }
        public void OnDrag(PointerEventData e) => FromPointer(e);
        void Update() { if (track != null) CursorController.Want(CursorKind.Default); }
    }

    /// <summary>Esc pauses the shift: resume, settings, Agnes's rules, start the day again, or go back to the title.</summary>
    public class PauseMenu : MonoBehaviour
    {
        public static bool Open { get; private set; }
        RectTransform panel;
        float prevScale = 1f;

        void Update()
        {
            if (Open || UIRoot.ModalOpen || Director.I == null || !Director.I.Running) return;
            if (!InputX.KeyDown(UnityEngine.InputSystem.Key.Escape)) return;
            if (InspectController.I != null && InspectController.I.Held != null) return;   // Esc puts the object down
            if (StampTool.I != null && StampTool.I.Carrying != null) return;               // Esc puts the stamp back
            Show();
        }

        void Show()
        {
            Open = true;
            UIRoot.I.PushModal();
            prevScale = Time.timeScale;
            Time.timeScale = 0f;
            AudioDirector.Muffle(true);
            AudioDirector.Play("ui_click", 0.4f);
            panel = UiKit.Rect("Pause", UIRoot.I.root).Fill();
            var dim = UiKit.Image(panel, "Dim", null, new Color(0.02f, 0.02f, 0.03f, 0.6f));
            dim.rectTransform.Fill();
            dim.raycastTarget = true;
            var card = UiKit.Image(panel, "Card", "paper_card", UiKit.Paper, 28f);
            card.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)).Place(Vector2.zero, new Vector2(620f, 640f));
            var t = UiKit.Label(card.transform, "Title", "Shutter down", Fonts.Title, 54f, UiKit.Ink, TextAlignmentOptions.Top);
            t.rectTransform.Fill();
            t.margin = new Vector4(0f, 40f, 0f, 0f);
            var d = Director.I;
            var sub = UiKit.Label(card.transform, "Sub", d.DayDef != null ? $"{d.DayDef.date}" : "", Fonts.TitleItalic, 26f, UiKit.InkSoft, TextAlignmentOptions.Top);
            sub.rectTransform.Fill();
            sub.margin = new Vector4(0f, 112f, 0f, 0f);
            int i = 0;
            void Btn(string text, System.Action a)
            {
                var b = UiKit.Button(card.transform, text, text, Fonts.Title, 42f, a);
                b.GetComponent<RectTransform>().Anchor(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f)).Place(new Vector2(0f, -190f - i++ * 72f), new Vector2(480f, 62f));
            }
            Btn("Back to the desk", Hide);
            Btn("Agnes's rules", () => RulesCard.Show());
            Btn("Settings", () => SettingsPanel.Open(null));
            Btn("Start the day again", () => { Hide(); Game.I.Restart(Director.I.Day); });
            Btn("Back to the title", () => { Hide(); Game.I.Restart(0); });
        }

        void Hide()
        {
            if (!Open) return;
            Open = false;
            Time.timeScale = prevScale <= 0f ? 1f : prevScale;
            AudioDirector.Muffle(false);
            if (panel != null) Destroy(panel.gameObject);
            UIRoot.I.PopModal();
        }
    }

    /// <summary>Every rule Agnes has left you so far, on a card in her hand.</summary>
    public static class RulesCard
    {
        public static void Show()
        {
            var d = Director.I;
            UIRoot.I.PushModal();
            var panel = UiKit.Rect("Rules", UIRoot.I.root).Fill();
            var dim = UiKit.Image(panel, "Dim", null, new Color(0f, 0f, 0f, 0.5f));
            dim.rectTransform.Fill();
            dim.raycastTarget = true;
            var card = UiKit.Image(panel, "Card", "note_paper", new Color(1f, 0.98f, 0.9f), 30f);
            card.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)).Place(Vector2.zero, new Vector2(1100f, 900f));
            var known = d.Db.RulesKnownAt(d.Day, d.Current?.id ?? "");
            if (d.Current == null && d.DayDef != null) foreach (int r in d.DayDef.rules) known.Add(r);
            var sb = new System.Text.StringBuilder();
            foreach (var r in d.Db.root.rules.Where(r => known.Contains(r.id)).OrderBy(r => r.id))
                sb.Append($"<b>{r.id}.</b>  {r.text}\n<size=70%><color=#6a5a50><i>{r.hint}</i></color></size>\n\n");
            if (sb.Length == 0) sb.Append("No rules yet, love. Ring the bell.");
            var body = UiKit.Label(card.transform, "Body", sb.ToString(), Fonts.Agnes, 34f, DeskMaterials.InkColor, TextAlignmentOptions.TopLeft);
            body.rectTransform.Fill();
            body.margin = new Vector4(80f, 70f, 80f, 110f);
            body.enableAutoSizing = true;
            body.fontSizeMin = 20f;
            body.fontSizeMax = 34f;
            var back = UiKit.Button(card.transform, "Back", "Put it back", Fonts.Title, 40f, () => { Object.Destroy(panel.gameObject); UIRoot.I.PopModal(); });
            back.GetComponent<RectTransform>().Anchor(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f)).Place(new Vector2(0f, 30f), new Vector2(360f, 60f));
        }
    }
}
