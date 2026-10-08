using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LostAndFound
{
    /// <summary>
    /// -lafShowcase (with -lafPlay; the trailer's takes only): the filmed play also does what a player finds in the menus and at
    /// hand, so the trailer can show it. At the title it opens Settings with the arrow keys and steps the Graphics fidelity down
    /// to Low and back up to Ultra; on Tuesday morning it opens the pause menu and its Controls card from the keyboard; on
    /// Wednesday morning it reads the ledger book on the desk; on Monday's second claim it asks Agnes for nudges (until the
    /// drawer glows, then until the glint shows on the scarf) before doing what they say; on Thursday's second claim it reads
    /// Agnes's rules with R; it holds the stamp over the slip long enough to read what it will do; and after the ending it
    /// stays for the week's page. Each is marked in markers.tsv. Holds here count frames (30 a second of film), since the
    /// title's and the pause menu's cards run with game time stopped.
    /// </summary>
    public partial class DemoRecorder
    {
        bool showcase;
        bool shownPause, shownLedger;

        static IEnumerator Frames(float seconds)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(seconds * Fps));
            for (int i = 0; i < n; i++) yield return null;
        }

        IEnumerator Tap(Key k)
        {
            while (keyHeld != Key.None) yield return null;
            keyHeld = k;
            InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(k));
            yield return Frames(0.1f);
            keyHeld = Key.None;
            InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
            yield return Frames(0.1f);
        }

        static string MenuFocus => MenuNav.Describe(MenuNav.Focused) ?? "";

        /// <summary>Press a key, at the given pace, until the menu's focus reads as wanted (at most n presses).</summary>
        IEnumerator SeekFocus(Key k, string wanted, float pace = 0.3f, int n = 24)
        {
            for (int i = 0; i < n && !MenuFocus.StartsWith(wanted); i++) { yield return Tap(k); yield return Frames(pace); }
            if (!MenuFocus.StartsWith(wanted)) Fallback($"the arrows didn't reach \"{wanted}\" (focus: {MenuFocus})");
        }

        /// <summary>The title by the keys: Settings, the Graphics fidelity down to Low and back to Ultra, Esc, then Begin.</summary>
        IEnumerator TitleShowcase()
        {
            yield return Tap(Key.DownArrow);
            Mark("menu-keys");
            yield return Frames(0.6f);
            yield return SeekFocus(Key.DownArrow, "Settings");
            yield return Frames(0.4f);
            yield return Tap(Key.Enter);
            Mark("settings");
            yield return Frames(1.0f);
            yield return SeekFocus(Key.DownArrow, "Graphics fidelity", 0.1f);
            if (!MenuFocus.StartsWith("Graphics fidelity")) { yield return Tap(Key.Escape); yield return SeekFocus(Key.UpArrow, "Begin"); yield return Tap(Key.Enter); yield break; }
            Mark($"settings-fidelity {GraphicsQuality.Names[Settings.PictureQuality]}");
            yield return Frames(1.4f);
            for (int i = 0; i < 3 && Settings.PictureQuality > 0; i++) { yield return Tap(Key.LeftArrow); yield return Frames(0.8f); }
            Mark($"fidelity {GraphicsQuality.Names[Settings.PictureQuality]}");
            yield return Frames(0.6f);
            for (int i = 0; i < 3 && Settings.PictureQuality < 3; i++)
            {
                yield return Tap(Key.RightArrow);
                Mark($"fidelity {GraphicsQuality.Names[Settings.PictureQuality]}");
                yield return Frames(0.8f);
            }
            yield return Frames(0.8f);
            yield return Tap(Key.Escape);
            Mark("settings-closed");
            yield return Frames(0.7f);
            yield return SeekFocus(Key.UpArrow, "Begin");
            yield return Frames(0.5f);
            Mark("begin");
            yield return Tap(Key.Enter);
        }

        /// <summary>Before a day's first claim: Tuesday's pause menu and Controls card, Wednesday's ledger book.</summary>
        IEnumerator MorningShowcase(Director d)
        {
            if (d.Day == 2 && !shownPause)
            {
                shownPause = true;
                yield return Tap(Key.Escape);
                yield return Frames(0.2f);
                Mark("pause");
                yield return Frames(1.4f);
                yield return SeekFocus(Key.DownArrow, "Controls");
                yield return Frames(0.4f);
                yield return Tap(Key.Enter);
                Mark("controls");
                yield return Frames(3.2f);
                yield return Tap(Key.Escape);
                yield return Frames(0.8f);
                yield return Tap(Key.Escape);
                Mark("pause-closed");
                yield return Frames(0.8f);
            }
            if (d.Day == 3 && !shownLedger)
            {
                shownLedger = true;
                var book = Object.FindAnyObjectByType<DeskLedger>();
                if (book == null) yield break;
                yield return GlideTo(book, 0.9f);
                yield return Frames(1.0f);   // its hint: how many days are in it
                yield return Click();
                yield return Until(() => LedgerBook.IsOpen, 2f);
                if (!LedgerBook.IsOpen) { Fallback("the click on the ledger book missed"); yield break; }
                Mark($"ledger-book {LedgerBook.ShownDay}");
                yield return Frames(3.0f);
                yield return Tap(Key.LeftArrow);
                Mark($"ledger-book {LedgerBook.ShownDay}");
                yield return Frames(2.6f);
                yield return Tap(Key.Escape);
                Mark("ledger-book-closed");
                yield return Glide(() => new Vector2(Screen.width * 0.62f, Screen.height * 0.3f), 0.6f);
            }
        }

        /// <summary>Ask Agnes (H) until her nudge shows the way (where: "drawer", "item", "part" or "glint").</summary>
        IEnumerator NudgeUntil(Director d, string where)
        {
            for (int i = 0; i < 4 && d.ShowingWhere != where; i++)
            {
                yield return Tap(Key.H);
                Mark($"nudge {d.Current?.id} {d.ActiveNudge?.stage}");
                yield return Frames(2.2f);
            }
            if (d.ShowingWhere == where) Mark($"nudge-shows {where}");
            yield return Frames(0.8f);
        }

        /// <summary>Agnes's rules on the desk: R, a read, R again.</summary>
        IEnumerator RulesShowcase()
        {
            yield return Tap(Key.R);
            Mark("rules");
            yield return Frames(4.0f);
            yield return Tap(Key.R);
            Mark("rules-closed");
            yield return Frames(0.6f);
        }

        /// <summary>After the ending: stay for the week's page.</summary>
        IEnumerator WeekShowcase()
        {
            yield return Until(() => UiPanel("Week"), 40f);
            if (!UiPanel("Week")) yield break;
            Mark("week");
            yield return Frames(6.0f);
        }
    }
}
