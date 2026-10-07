using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace LostAndFound
{
    /// <summary>
    /// The week so far: the Day Ledger page of every day already closed this week, as it was that evening. Opens from the
    /// ledger book on the desk or the pause menu; ‹ › (or the arrows, A/D, LB/RB) turn the pages, and Esc, right click, B or
    /// "Put it back" close it. Nothing from today is in it: its marks would give the answers away.
    /// </summary>
    public static class LedgerBook
    {
        static RectTransform panel, holder;
        static List<int> days = new();
        static int index, openedFrame;
        public static bool IsOpen => panel != null;
        public static int ShownDay => IsOpen && index >= 0 && index < days.Count ? days[index] : 0;

        /// <summary>The days before <paramref name="today"/> whose evening was seen this week: each has a page, written from the
        /// state that evening left (kept as the next morning's snapshot; the current state if an older save didn't keep one).</summary>
        public static List<int> Days(ContentDb db, SaveGame save, StoryState state, int today)
        {
            var list = new List<int>();
            for (int day = 1; day < today; day++)
            {
                var def = db.Day(day);
                var st = StateAt(save, state, day);
                if (def != null && st != null && def.cases.Any(c => st.Record(c.id) != null)) list.Add(day);
            }
            return list;
        }

        /// <summary>The state a day's evening left: the next morning's snapshot (read, never changed).</summary>
        public static StoryState StateAt(SaveGame save, StoryState state, int day) =>
            save?.snapshots?.FirstOrDefault(x => x.day == day + 1)?.state ?? state;

        public static List<int> Days(Director d) =>
            d == null || d.Db == null || d.DayDef == null || d.State == null ? new List<int>() : Days(d.Db, d.Save, d.State, d.Day);

        /// <summary>Open at the given day's page, or the latest.</summary>
        public static void Show(int day = 0)
        {
            var d = Director.I;
            if (IsOpen || d == null) return;
            days = Days(d);
            if (days.Count == 0) return;
            index = day > 0 && days.Contains(day) ? days.IndexOf(day) : days.Count - 1;
            openedFrame = Time.frameCount;
            UIRoot.I.PushModal();
            AudioDirector.PlayMaterial("paper", "pick", 0.5f);
            panel = UiKit.Rect("LedgerBook", UIRoot.I.root).Fill();
            var dim = UiKit.Image(panel, "Dim", null, new Color(0.04f, 0.03f, 0.03f, 0.92f));
            dim.rectTransform.Fill();
            dim.raycastTarget = true;
            panel.gameObject.AddComponent<Closer>();
            Draw();
        }

        /// <summary>The page before (-1) or after (+1), if there is one.</summary>
        public static void Turn(int by)
        {
            if (!IsOpen) return;
            int to = Mathf.Clamp(index + by, 0, days.Count - 1);
            if (to == index) return;
            index = to;
            AudioDirector.PlayMaterial("paper", "put", 0.35f);
            Draw();
        }

        static void Draw()
        {
            var d = Director.I;
            if (holder != null) Object.Destroy(holder.gameObject);
            holder = UiKit.Rect("Page", panel).Fill();
            int day = days[index];
            var page = LedgerView.Build(d, day, StateAt(d.Save, d.State, day), holder, "[LedgerBook]");
            // below the page, on the dark, as the evening's buttons are: the day before, put it back, the day after
            PaperButton Btn(string name, string text, float x, float width, System.Action act)
            {
                var b = UiKit.Button(page.book.transform, name, text, Fonts.Title, 38f, act);
                b.GetComponent<RectTransform>().Anchor(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f)).Place(new Vector2(x, -90f), new Vector2(width, 60f));
                b.label.enableAutoSizing = true;
                b.label.fontSizeMin = 24f;
                b.label.fontSizeMax = 38f;
                b.normal = new Color(0.95f, 0.88f, 0.74f);
                b.hover = UiKit.Gold;
                return b;
            }
            Btn("Back", "Put it back", 0f, 360f, Hide);
            if (index > 0) Btn("Prev", $"‹  {d.Db.Day(days[index - 1])?.weekday}", -480f, 340f, () => Turn(-1));
            if (index < days.Count - 1) Btn("Next", $"{d.Db.Day(days[index + 1])?.weekday}  ›", 480f, 340f, () => Turn(1));
        }

        public static void Hide()
        {
            if (!IsOpen) return;
            Object.Destroy(panel.gameObject);
            panel = holder = null;
            UIRoot.I.PopModal();
            AudioDirector.PlayMaterial("paper", "put", 0.4f);
        }

        /// <summary>Keys for the open book. Runs after every Update, so the press that closes it can't also put the held
        /// object down or open the pause menu.</summary>
        class Closer : MonoBehaviour
        {
            void LateUpdate()
            {
                if (Time.frameCount == openedFrame) return;
                if (InputX.KeyDown(UnityEngine.InputSystem.Key.Escape) || InputX.RightDown) { Hide(); return; }
                if (InputX.KeyDown(UnityEngine.InputSystem.Key.LeftArrow) || InputX.KeyDown(UnityEngine.InputSystem.Key.A)) Turn(-1);
                else if (InputX.KeyDown(UnityEngine.InputSystem.Key.RightArrow) || InputX.KeyDown(UnityEngine.InputSystem.Key.D)) Turn(1);
            }

            void OnDestroy()
            {
                if (panel == (RectTransform)transform) { panel = holder = null; UIRoot.I?.PopModal(); }
            }
        }
    }

    /// <summary>The ledger book on the desk: hover says what's in it, a click opens the week so far.</summary>
    public class DeskLedger : Interactable
    {
        int pagesFrame = -1, pages;

        int Pages
        {
            get
            {
                // the hint asks every frame: count once a frame
                if (pagesFrame != Time.frameCount) { pagesFrame = Time.frameCount; pages = LedgerBook.Days(Director.I).Count; }
                return pages;
            }
        }

        public override bool Interactive => base.Interactive && Director.I != null && Director.I.Running && !Director.I.InEvening;
        public override CursorKind Cursor => Pages > 0 ? CursorKind.Look : CursorKind.Default;
        public override string Hint => Pages > 0
            ? $"The Day Ledger   ·   click to read the week so far ({(Pages == 1 ? "one day" : Pages + " days")})"
            : "The Day Ledger   ·   each evening's page is kept in here";
        public override void OnClick() => LedgerBook.Show();
    }
}
