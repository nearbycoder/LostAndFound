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
        /// <summary>The top menu entry (recordings click it like a player would).</summary>
        public static RectTransform FirstButton => panel != null ? panel.Find("Menu")?.GetChild(0) as RectTransform : null;

        /// <summary>Start a new week as if "Begin" had been clicked (tests, recordings).</summary>
        public static void Begin(Game g) => Start(g, g.NewWeek);

        public static void Show(Game g)
        {
            Debug.Log($"[Title] show (frame {Time.frameCount})");
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

            string saveNote = SaveGame.TakeLoadNote();
            if (saveNote != null)
            {
                var note = UiKit.Label(panel, "SaveNote", saveNote, Fonts.TitleItalic, 28f, new Color(0.96f, 0.72f, 0.56f), TextAlignmentOptions.BottomLeft);
                note.rectTransform.Anchor(Vector2.zero, Vector2.zero, Vector2.zero).Place(new Vector2(126f, 118f), new Vector2(760f, 80f));
                TextBacking.Add(note, 0.8f, 22f, 10f);
            }
            var (found, total) = Curios.Count(g.Db, g.Save.discovered);
            var cur = UiKit.Label(panel, "Curios", $"Curiosities found: {found} of {total}", Fonts.Body, 26f, new Color(0.8f, 0.74f, 0.62f), TextAlignmentOptions.BottomLeft);
            cur.rectTransform.Anchor(Vector2.zero, Vector2.zero, Vector2.zero).Place(new Vector2(126f, 70f), new Vector2(700f, 40f));
            var credit = UiKit.Label(panel, "Credit", "Agnes's desk, as she left it.", Fonts.Agnes, 28f, new Color(0.8f, 0.74f, 0.62f, 0.8f), TextAlignmentOptions.BottomLeft);
            credit.rectTransform.Anchor(Vector2.zero, Vector2.zero, Vector2.zero).Place(new Vector2(126f, 30f), new Vector2(700f, 40f));
            // which build this is, for a bug report: small, in the corner
            var build = UiKit.Label(panel, "Build", BuildInfo.Line, Fonts.Type, 18f, new Color(0.8f, 0.74f, 0.62f, 0.75f), TextAlignmentOptions.BottomRight);
            build.rectTransform.Anchor(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f)).Place(new Vector2(-34f, 22f), new Vector2(520f, 30f));
            TextBacking.Add(build, 0.7f, 14f, 6f);

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

        static float itemStep = 76f;   // a little closer when the menu is at its longest, to stay clear of the notes below it

        static PaperButton Item(RectTransform list, int i, string text, System.Action act, float size = 50f)
        {
            var b = UiKit.Button(list, "Item", text, Fonts.Title, size, act);
            b.normal = new Color(0.95f, 0.89f, 0.76f);
            b.hover = UiKit.Gold;
            b.label.alignment = TextAlignmentOptions.MidlineLeft;
            b.GetComponent<RectTransform>().Anchor(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f)).Place(new Vector2(0f, -i * itemStep), new Vector2(760f, 70f));
            return b;
        }

        static void BuildMain(Game g, RectTransform list)
        {
            Clear(list);
            int i = 0;
            bool started = SaveGame.Exists && (g.Save.currentDay > 1 || g.Save.state.records.Count > 0);
            bool canQuit = !Application.isEditor && Application.platform != RuntimePlatform.WebGLPlayer;
            int items = (started && !g.Save.finished ? 1 : 0) + 1 + (g.Save.unlockedDay > 1 || g.Save.finished ? 1 : 0) + 3 + (canQuit ? 1 : 0);
            itemStep = items >= 7 ? 70f : 76f;
            if (started && !g.Save.finished)
            {
                var day = g.Db.Day(Mathf.Clamp(g.Save.currentDay, 1, g.Db.DayCount));
                int total = Rules.ActiveCases(day, g.Save.state).Count();
                // mid-day, say where you'll pick up (instead of the day's title, to fit on one line)
                string where = g.Save.casesDone <= 0 ? $"{day.weekday}, {day.title}" : g.Save.casesDone >= total ? $"{day.weekday}, the evening" : $"{day.weekday}, claimant {g.Save.casesDone + 1} of {total}";
                Item(list, i++, $"Continue  <size=60%><color=#c8b898>·  {where}</color></size>", () => Start(g, g.ContinueWeek));
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
            Item(list, i++, "Curiosities", () => CurioLedger.Show(g));
            Item(list, i++, "Controls", ControlsCard.Show);
            Item(list, i++, "Settings", () => SettingsPanel.Open(() => { }));
            if (canQuit) Item(list, i++, "Close the Office", Application.Quit);
        }

        static void BuildDays(Game g, RectTransform list)
        {
            Clear(list);
            itemStep = 76f;
            for (int d = 1; d <= g.Db.DayCount; d++)
            {
                var def = g.Db.Day(d);
                int day = d;
                bool open = d <= g.Save.unlockedDay;
                var best = g.Save.best.FirstOrDefault(b => b.day == d);
                string stamps = best == null ? "" : "  <size=60%><color=#e0c070>" + new string('●', best.stamps) + new string('○', 3 - best.stamps) + "</color></size>";
                PaperButton b = null;
                bool asked = false;
                b = Item(list, d - 1, $"{def.weekday}  <size=60%><color=#c8b898>·  {def.title}</color></size>{stamps}", () =>
                {
                    // going back during a week rewinds it to that morning: say so, and ask for a second click
                    string loses = ProgressGuard.ReplayDay(g.Save, g.Db, day);
                    if (loses != null && !asked)
                    {
                        asked = true;
                        b.label.text = $"{def.weekday}  <size=60%><color=#e0a080>·  {loses}</color></size>";
                        return;
                    }
                    Start(g, () => g.ReplayDay(day));
                }, 44f);
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
            Debug.Log($"[Title] dismissed (frame {Time.frameCount})");
            Object.Destroy(panel.gameObject);
            panel = null;
            UIRoot.I.PopModal();
            begin();
        }
    }

    /// <summary>
    /// Agnes's curio book: every object's one optional secret. Found ones are written out; the rest name the
    /// object and the day it turns up, so a replay has something to look for without giving it away.
    /// </summary>
    public static class CurioLedger
    {
        static RectTransform panel;

        public static void Show(Game g)
        {
            if (panel != null) return;
            UIRoot.I.PushModal();
            AudioDirector.PlayMaterial("paper", "pick", 0.5f);
            panel = UiKit.Rect("Curios", UIRoot.I.root).Fill();
            var dim = UiKit.Image(panel, "Dim", null, new Color(0f, 0f, 0f, 0.55f));
            dim.rectTransform.Fill();
            dim.raycastTarget = true;
            var book = UiKit.Image(panel, "Book", "ledger_page", new Color(0.95f, 0.91f, 0.81f), 40f);
            book.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)).Place(new Vector2(0f, 10f), new Vector2(1640f, 960f));
            var all = Curios.All(g.Db, g.Save.discovered);
            int found = all.Count(e => e.found);
            var title = UiKit.Label(book.transform, "Title", "Curiosities", Fonts.Title, 58f, UiKit.Ink, TextAlignmentOptions.Top);
            title.rectTransform.Fill();
            title.margin = new Vector4(60f, 30f, 60f, 0f);
            var sub = UiKit.Label(book.transform, "Sub", $"{found} of {all.Count} found  ·  one small secret in every object on the shelves and in the drawers", Fonts.TitleItalic, 26f, UiKit.InkSoft, TextAlignmentOptions.Top);
            sub.rectTransform.Fill();
            sub.margin = new Vector4(60f, 100f, 60f, 0f);

            int perColumn = (all.Count + 1) / 2;
            for (int k = 0; k < all.Count; k++)
            {
                var e = all[k];
                int col = k / perColumn, row = k % perColumn;
                float x = 80f + col * 640f, y = -140f - row * 54f;   // both columns inside the page's printed margin
                string day = g.Db.Day(e.obj.arrives)?.weekday ?? "";
                string text = e.found
                    ? $"<b>{e.obj.name}</b>\n<size=78%><color=#4a3a30>{e.secret.fact}</color></size>"
                    : $"<color=#8a7a68><b>{e.obj.name}</b>\n<size=78%><i>Something still hidden.  Turns up {day}.</i></size></color>";
                var line = UiKit.Label(book.transform, "Curio", text, Fonts.Hand, 26f, UiKit.Ink, TextAlignmentOptions.TopLeft);
                line.rectTransform.Anchor(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f)).Place(new Vector2(x + 40f, y), new Vector2(590f, 54f));
                line.lineSpacing = -18f;
                line.enableAutoSizing = true;
                line.fontSizeMin = 16f;
                line.fontSizeMax = 26f;
                var mark = UiKit.Image(book.transform, "Mark", e.found ? "mark_tick" : "mark_dash", e.found ? DeskMaterials.ReturnInk : new Color(0.55f, 0.5f, 0.45f, 0.6f));
                mark.rectTransform.Anchor(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f)).Place(new Vector2(x + 14f, y - 20f), new Vector2(34f, 34f));
            }
            var back = UiKit.Button(book.transform, "Back", "Close the book", Fonts.Title, 38f, Hide);
            back.GetComponent<RectTransform>().Anchor(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f)).Place(new Vector2(0f, 22f), new Vector2(380f, 56f));
            panel.gameObject.AddComponent<EscCloses>();
        }

        public static void Hide()
        {
            if (panel == null) return;
            Object.Destroy(panel.gameObject);
            panel = null;
            UIRoot.I.PopModal();
            AudioDirector.PlayMaterial("paper", "put", 0.4f);
        }

        class EscCloses : MonoBehaviour
        {
            void LateUpdate() { if (InputX.KeyDown(UnityEngine.InputSystem.Key.Escape)) Hide(); }
            void OnDestroy() { if (panel == (RectTransform)transform) { panel = null; UIRoot.I?.PopModal(); } }
        }
    }

    /// <summary>Volume, text speed, mouse, comfort and display options. Opens over the title or the pause menu.</summary>
    public static class SettingsPanel
    {
        const float CardHeight = 1050f;

        public static void Open(System.Action onClose)
        {
            UIRoot.I.PushModal();
            var panel = UiKit.Rect("Settings", UIRoot.I.root).Fill();
            var dim = UiKit.Image(panel, "Dim", null, new Color(0f, 0f, 0f, 0.55f));
            dim.rectTransform.Fill();
            dim.raycastTarget = true;
            var card = UiKit.Image(panel, "Card", "paper_card", UiKit.Paper, 28f);
            card.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)).Place(Vector2.zero, new Vector2(900f, CardHeight));
            // a wide, short window (21:9) has less than the card's height: scale it to fit
            Canvas.ForceUpdateCanvases();
            card.rectTransform.localScale = Vector3.one * Mathf.Min(1f, (UIRoot.I.root.rect.height - 30f) / CardHeight);
            var t = UiKit.Label(card.transform, "Title", "Settings", Fonts.Title, 56f, UiKit.Ink, TextAlignmentOptions.Top);
            t.rectTransform.Fill();
            t.margin = new Vector4(0f, 34f, 0f, 0f);

            // rows are 44 high: a slider's step is 46 and a toggle's 44, so twenty rows fit above "Done"
            float y = -124f;
            void Slider(string label, float min, float max, System.Func<float> get, System.Action<float> set)
            {
                var l = UiKit.Label(card.transform, label, label, Fonts.Body, 30f, UiKit.Ink, TextAlignmentOptions.MidlineLeft);
                l.rectTransform.Anchor(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f)).Place(new Vector2(80f, y), new Vector2(300f, 44f));
                var s = PaperSlider.Create(card.transform, new Vector2(380f, y), 420f, Mathf.InverseLerp(min, max, get()), v => set(Mathf.Lerp(min, max, v)));
                y -= 46f;
            }
            void Toggle(string label, System.Func<bool> get, System.Action<bool> set)
            {
                PaperButton b = null;
                string Text() => $"{label}:  <b>{(get() ? "On" : "Off")}</b>";
                b = UiKit.Button(card.transform, label, Text(), Fonts.Body, 30f, () => { set(!get()); b.label.text = Text(); });
                b.label.alignment = TextAlignmentOptions.MidlineLeft;
                b.GetComponent<RectTransform>().Anchor(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f)).Place(new Vector2(80f, y), new Vector2(720f, 44f));
                y -= 44f;
            }
            Slider("Volume", 0f, 1f, () => Settings.MasterVolume, v => Settings.MasterVolume = v);
            Slider("Music", 0f, 1f, () => Settings.MusicVolume, v => Settings.MusicVolume = v);
            Slider("Sound effects", 0f, 1f, () => Settings.SfxVolume, v => Settings.SfxVolume = v);
            Slider("Voices", 0f, 1f, () => Settings.VoiceVolume, v => Settings.VoiceVolume = v);
            Slider("Station sounds", 0f, 1f, () => Settings.AmbienceVolume, v => Settings.AmbienceVolume = v);
            Toggle("Sound when in the background", () => Settings.SoundInBackground, v => Settings.SoundInBackground = v);
            y -= 4f;
            Slider("Text speed", 0.5f, 2.5f, () => Settings.TextSpeed, v => Settings.TextSpeed = v);
            Slider("Turning speed", 0.4f, 2f, () => Settings.MouseSensitivity, v => Settings.MouseSensitivity = v);
            Toggle("Turn at the screen's edge", () => Settings.EdgeTurn, v => Settings.EdgeTurn = v);
            y -= 10f;
            Slider("Brightness", -1f, 1f, () => Settings.Brightness, v => Settings.Brightness = v);
            Toggle("Screen shake", () => Settings.ScreenShake, v => Settings.ScreenShake = v);
            Toggle("Reduce motion", () => Settings.ReduceMotion, v => Settings.ReduceMotion = v);
            Toggle("Film effects (grain, blur, vignette)", () => Settings.PostEffects, v => Settings.PostEffects = v);
            Toggle("Fullscreen", () => Settings.Fullscreen, v => { Settings.Fullscreen = v; Game.ApplyDisplay(); });
            {
                // Graphics fidelity: a slider of four steps, its step named beside it (round 1's Picture quality button)
                var l = UiKit.Label(card.transform, "Graphics fidelity", "Graphics fidelity", Fonts.Body, 30f, UiKit.Ink, TextAlignmentOptions.MidlineLeft);
                l.rectTransform.Anchor(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f)).Place(new Vector2(80f, y), new Vector2(300f, 44f));
                var name = UiKit.Label(card.transform, "FidelityStep", "", Fonts.Body, 30f, UiKit.Ink, TextAlignmentOptions.MidlineLeft);
                name.rectTransform.Anchor(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f)).Place(new Vector2(704f, y), new Vector2(120f, 44f));
                void Name() => name.text = $"<b>{GraphicsQuality.Names[Settings.PictureQuality]}</b>";
                Name();
                int n = FidelityStep.Count;
                PaperSlider.Create(card.transform, new Vector2(380f, y), 300f, Settings.PictureQuality / (n - 1f),
                    v => { Settings.PictureQuality = Mathf.RoundToInt(v * (n - 1)); Name(); }, n);
                y -= 46f;
            }
            Toggle("Large text (dialogue, hints, tags, notes)", () => Settings.TextSize == 1, v => Settings.TextSize = v ? 1 : 0);
            Toggle("Plain lettering (no handwriting on tags and notes)", () => Settings.PlainLettering, v => Settings.PlainLettering = v);
            Toggle("Offer Agnes's nudges when stuck (H)", () => Settings.OfferNudges, v => Settings.OfferNudges = v);

            var back = UiKit.Button(card.transform, "Back", "Done", Fonts.Title, 44f, () =>
            {
                Object.Destroy(panel.gameObject);
                UIRoot.I.PopModal();
                onClose?.Invoke();
            });
            back.GetComponent<RectTransform>().Anchor(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f)).Place(new Vector2(0f, 34f), new Vector2(300f, 60f));
        }
    }

    /// <summary>An ink-line slider with a brass knob; with steps, it snaps to that many marks and reports only a change of mark.</summary>
    public class PaperSlider : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        RectTransform track, fill, knob;
        float value;
        int steps;
        System.Action<float> onChange;
        public float Value => value;
        public int Steps => steps;

        public static PaperSlider Create(Transform parent, Vector2 pos, float width, float value, System.Action<float> onChange, int steps = 0)
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
            s.steps = steps;
            for (int i = 0; i < steps; i++)
            {
                // a mark at each step, under the knob
                var m = UiKit.Image(rt, "Mark", null, new Color(0.3f, 0.27f, 0.25f, 0.55f));
                m.rectTransform.Anchor(new Vector2(i / (steps - 1f), 0.5f), new Vector2(i / (steps - 1f), 0.5f), new Vector2(0.5f, 0.5f)).Place(Vector2.zero, new Vector2(4f, 16f));
            }
            k.transform.SetAsLastSibling();
            s.knob = k.rectTransform;
            s.onChange = onChange;
            s.Set(value, false);
            return s;
        }

        /// <summary>Move by one step (or a twentieth of the way), as the keyboard and the pad do; true if it moved.</summary>
        public bool Nudge(int dir)
        {
            float before = value;
            Set(value + dir * (steps > 1 ? 1f / (steps - 1) : 0.05f), true);
            return !Mathf.Approximately(before, value);
        }

        void Set(float v, bool notify)
        {
            v = Mathf.Clamp01(v);
            if (steps > 1) v = Mathf.Round(v * (steps - 1)) / (steps - 1);
            if (notify && steps > 1 && Mathf.Approximately(v, value)) return;   // the same mark: nothing to report
            if (notify && steps > 1) AudioDirector.Play("ui_hover", 0.3f, 0.9f + 0.1f * v);
            value = v;
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

    /// <summary>Esc pauses the shift: resume, Agnes's rules, the controls, settings, start the day again, or go back to the title.</summary>
    public class PauseMenu : MonoBehaviour
    {
        public static bool Open { get; private set; }
        RectTransform panel;
        float prevScale = 1f;

        int depth, depthLastFrame;

        void Update()
        {
            if (Open)
            {
                // Esc again (the pad's Menu too) carries on, as Esc puts away every other card; not while a card opened from
                // the menu is over it, nor in the frame that card's Esc put it away
                if (InputX.KeyDown(UnityEngine.InputSystem.Key.Escape) && UIRoot.I.ModalDepth == depth && depthLastFrame == depth) Hide();
                return;
            }
            if (UIRoot.ModalOpen || Director.I == null || !Director.I.Running) return;
            if (!InputX.KeyDown(UnityEngine.InputSystem.Key.Escape)) return;
            if (InspectController.I != null && InspectController.I.Held != null) return;   // Esc puts the object down
            if (StampTool.I != null && StampTool.I.Carrying != null) return;               // Esc puts the stamp back
            Show();
        }

        public void Show()
        {
            if (Open) return;
            Open = true;
            UIRoot.I.PushModal();
            depth = depthLastFrame = UIRoot.I.ModalDepth;
            prevScale = Time.timeScale;
            Time.timeScale = 0f;
            AudioDirector.Muffle(true);
            AudioDirector.Play("ui_click", 0.4f);
            panel = UiKit.Rect("Pause", UIRoot.I.root).Fill();
            var dim = UiKit.Image(panel, "Dim", null, new Color(0.02f, 0.02f, 0.03f, 0.6f));
            dim.rectTransform.Fill();
            dim.raycastTarget = true;
            var card = UiKit.Image(panel, "Card", "paper_card", UiKit.Paper, 28f);
            // the week so far, once there's a closed day to read: one row more
            bool book = LedgerBook.Days(Director.I).Count > 0;
            card.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)).Place(Vector2.zero, new Vector2(620f, book ? 772f : 700f));
            var t = UiKit.Label(card.transform, "Title", "Shutter down", Fonts.Title, 54f, UiKit.Ink, TextAlignmentOptions.Top);
            t.rectTransform.Fill();
            t.margin = new Vector4(0f, 40f, 0f, 0f);
            var d = Director.I;
            var sub = UiKit.Label(card.transform, "Sub", d.DayDef != null ? $"{d.DayDef.date}" : "", Fonts.TitleItalic, 26f, UiKit.InkSoft, TextAlignmentOptions.Top);
            sub.rectTransform.Fill();
            sub.margin = new Vector4(0f, 112f, 0f, 0f);
            int i = 0;
            PaperButton Btn(string text, System.Action a)
            {
                var b = UiKit.Button(card.transform, text, text, Fonts.Title, 42f, a);
                b.GetComponent<RectTransform>().Anchor(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f)).Place(new Vector2(0f, -190f - i++ * 72f), new Vector2(480f, 62f));
                return b;
            }
            Btn("Back to the desk", Hide);
            Btn("Agnes's rules", () => RulesCard.Show());
            if (book) Btn("The week so far", () => LedgerBook.Show());
            Btn("Controls", ControlsCard.Show);
            Btn("Settings", () => SettingsPanel.Open(null));
            PaperButton again = null;
            bool asked = false;
            again = Btn("Start the day again", () =>
            {
                // the claims decided today would be undone: say how many, and ask for a second click
                string loses = ProgressGuard.RestartDay(Director.I.Save.casesDone);
                if (loses != null && !asked)
                {
                    asked = true;
                    again.label.text = loses;
                    again.label.enableAutoSizing = true;
                    again.label.fontSizeMin = 24f;
                    again.label.fontSizeMax = 42f;
                    again.normal = UiKit.Oxblood;
                    return;
                }
                Hide();
                Game.I.Restart(Director.I.Day);
            });
            Btn("Back to the title", () => { Hide(); Game.I.Restart(0); });
        }

        void LateUpdate()
        {
            if (UIRoot.I != null) depthLastFrame = UIRoot.I.ModalDepth;
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

    /// <summary>Every rule Agnes has left you so far, on a card in her hand. Opens from the pause menu, the card
    /// on the desk, or R from anywhere (holding something included); R, Esc or "Put it back" closes it.</summary>
    public static class RulesCard
    {
        static RectTransform panel;
        static int openedFrame;
        public static bool IsOpen => panel != null;

        /// <summary>The rules known right now, numbered, each with Agnes's wording and a plain gloss.</summary>
        public static string Text()
        {
            var d = Director.I;
            if (d == null || d.Db == null) return "";
            var known = d.KnownRules();
            var sb = new System.Text.StringBuilder();
            // the gloss sits close under its rule, and a short spacer (not a whole blank line) parts one rule from the next,
            // so all eight still fit at a size you can read on Friday
            foreach (var r in d.Db.root.rules.Where(r => known.Contains(r.id)).OrderBy(r => r.id))
            {
                if (sb.Length > 0) sb.Append("\n<size=45%> </size>\n");
                sb.Append($"<b>{r.id}.</b>  {r.text}\n<size=80%><color=#5e4f46><i>{r.hint}</i></color></size>");
            }
            if (sb.Length == 0) sb.Append("No rules yet, love. Ring the bell.");
            return sb.ToString();
        }

        public static void Toggle()
        {
            if (IsOpen) Hide();
            else Show();
        }

        public static void Show()
        {
            if (IsOpen) return;
            openedFrame = Time.frameCount;
            UIRoot.I.PushModal();
            AudioDirector.PlayMaterial("paper", "pick", 0.5f);
            panel = UiKit.Rect("Rules", UIRoot.I.root).Fill();
            var dim = UiKit.Image(panel, "Dim", null, new Color(0f, 0f, 0f, 0.5f));
            dim.rectTransform.Fill();
            dim.raycastTarget = true;
            var card = UiKit.Image(panel, "Card", "note_paper", new Color(1f, 0.98f, 0.9f), 30f);
            card.rectTransform.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)).Place(Vector2.zero, new Vector2(1500f, 940f));
            // a wide, short window (21:9) has less than the card's height: scale it to fit, as Settings does
            card.rectTransform.localScale = Vector3.one * Mathf.Min(1f, (UIRoot.I.root.rect.height - 30f) / 940f, (UIRoot.I.root.rect.width - 30f) / 1500f);
            var body = UiKit.Label(card.transform, "Body", Text(), Fonts.Agnes, 34f, DeskMaterials.InkColor, TextAlignmentOptions.TopLeft);
            body.rectTransform.Fill();
            // the text runs to just above "Put it back" (its top is 90 up from the card's foot), so Friday's eight rules get
            // every unit of height the card has
            body.margin = new Vector4(80f, 44f, 80f, 96f);
            body.enableAutoSizing = true;
            body.fontSizeMin = 20f;
            body.fontSizeMax = 34f;
            var back = UiKit.Button(card.transform, "Back", "Put it back", Fonts.Title, 40f, Hide);
            back.GetComponent<RectTransform>().Anchor(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f)).Place(new Vector2(0f, 30f), new Vector2(360f, 60f));
            var keys = UiKit.Label(card.transform, "Keys", "R  OR  ESC  TO PUT IT BACK", Fonts.Type, 18f, UiKit.InkSoft, TextAlignmentOptions.Bottom);
            keys.rectTransform.Fill();
            keys.margin = new Vector4(0f, 0f, 0f, 12f);
            panel.gameObject.AddComponent<Closer>();
        }

        public static void Hide()
        {
            if (!IsOpen) return;
            Object.Destroy(panel.gameObject);
            panel = null;
            UIRoot.I.PopModal();
            AudioDirector.PlayMaterial("paper", "put", 0.4f);
        }

        /// <summary>Closes on R or Esc. Runs after every Update, so the same key press can't also put down
        /// the held object or open the pause menu.</summary>
        class Closer : MonoBehaviour
        {
            void LateUpdate()
            {
                if (Time.frameCount == openedFrame) return;
                if (InputX.KeyDown(UnityEngine.InputSystem.Key.R) || InputX.KeyDown(UnityEngine.InputSystem.Key.Escape)) Hide();
            }

            void OnDestroy()
            {
                if (panel == (RectTransform)transform) { panel = null; UIRoot.I?.PopModal(); }
            }
        }
    }

    /// <summary>R opens Agnes's rules from anywhere during a shift.</summary>
    public class RulesHotkey : MonoBehaviour
    {
        void LateUpdate()
        {
            if (RulesCard.IsOpen || PauseMenu.Open || UIRoot.ModalOpen || Director.I == null || !Director.I.Running) return;
            if (InputX.KeyDown(UnityEngine.InputSystem.Key.R)) RulesCard.Show();
        }
    }
}
