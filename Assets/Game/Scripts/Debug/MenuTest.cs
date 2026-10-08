using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace LostAndFound
{
    /// <summary>
    /// -lafMenuTest &lt;dir&gt; (Tools/unity.sh menutest): the menus with only a keyboard, then only a gamepad, through fresh
    /// virtual devices. Keyboard: from the title's first item down to Settings, Enter, down to Graphics fidelity, right to
    /// Ultra (checked applied and saved), left to High, Esc, the focus back on Settings, up to Begin and Enter; in the week,
    /// Esc for the pause menu (focused at once), down to Controls, Enter, Esc, and back to the desk. Gamepad: Menu opens the
    /// pause menu focused, the d-pad goes to Settings and A opens it, the d-pad takes the fidelity to Ultra and back, down to
    /// Done and A, then up to Back to the desk and A. Every step is logged and checked; screenshots on the way.
    /// Logs "[MenuTest] PASS" when every check held.
    /// </summary>
    public class MenuTest : MonoBehaviour
    {
        Keyboard kb;
        Gamepad pad;
        string dir;
        int shots, checks;
        readonly List<string> failed = new();

        void Start()
        {
            dir = Game.Arg("-lafMenuTest");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Application.persistentDataPath, "menutest");
            Directory.CreateDirectory(dir);
            Application.runInBackground = true;
            kb = InputSystem.AddDevice<Keyboard>("MenuKeyboard");
            pad = InputSystem.AddDevice<Gamepad>("MenuPad");
            StartCoroutine(Run());
        }

        void OnDestroy()
        {
            foreach (InputDevice d in new InputDevice[] { kb, pad }) if (d != null && d.added) InputSystem.RemoveDevice(d);
        }

        void Shot(string name)
        {
            if (Game.Arg("-lafTextAudit") != null) TextAudit.Check($"menu_{name}");
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"{shots++:00}_{name}.png"));
        }

        static IEnumerator Hold(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            int f0 = Time.frameCount;
            while (Time.realtimeSinceStartup < end || Time.frameCount - f0 < 3) yield return null;
        }

        static IEnumerator Until(System.Func<bool> cond, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!cond() && Time.realtimeSinceStartup < end) yield return null;
        }

        void Check(bool ok, string what)
        {
            checks++;
            Debug.Log($"[MenuTest] {(ok ? "ok" : "FAIL")} {what} (card {MenuNav.CardName ?? "none"}, focus: {MenuNav.Describe(MenuNav.Focused)})");
            if (!ok) failed.Add(what);
        }

        static string Focus => MenuNav.Describe(MenuNav.Focused);

        IEnumerator Key(Key k)
        {
            kb.MakeCurrent();
            InputSystem.QueueStateEvent(kb, new KeyboardState(k));
            yield return Hold(0.1f);
            InputSystem.QueueStateEvent(kb, new KeyboardState());
            yield return Hold(0.15f);
        }

        IEnumerator Pad(GamepadButton b)
        {
            pad.MakeCurrent();
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(b));
            yield return Hold(0.1f);
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return Hold(0.25f);
        }

        /// <summary>Press a direction until the focus reads as wanted (at most n presses).</summary>
        IEnumerator Seek(System.Func<IEnumerator> press, string wanted, int n = 24)
        {
            for (int i = 0; i < n && !Focus.StartsWith(wanted); i++) yield return press();
        }

        IEnumerator Run()
        {
            yield return Until(() => TitleScreen.Showing && UIRoot.I != null, 30f);
            yield return Hold(2f);   // the title fades in
            Debug.Log($"[MenuTest] fidelity at the start: {GraphicsQuality.Names[Settings.PictureQuality]}");

            // ---- keyboard
            yield return Key(UnityEngine.InputSystem.Key.DownArrow);
            Check(MenuNav.CardName == "Title" && Focus.StartsWith("Begin"), "the first arrow focuses the title's first item");
            Shot("title_focused");
            yield return Seek(() => Key(UnityEngine.InputSystem.Key.DownArrow), "Settings");
            Check(Focus.StartsWith("Settings"), "down reaches Settings");
            yield return Key(UnityEngine.InputSystem.Key.Enter);
            yield return Hold(0.3f);
            Check(MenuNav.CardName == "Settings" && Focus.StartsWith("Volume"), "Enter opens Settings, focused on its first row");
            yield return Seek(() => Key(UnityEngine.InputSystem.Key.DownArrow), "Graphics fidelity");
            Check(Focus.StartsWith("Graphics fidelity"), "down reaches Graphics fidelity");
            Shot("settings_fidelity_high");
            yield return Key(UnityEngine.InputSystem.Key.RightArrow);
            yield return Hold(0.6f);   // settings are written a moment after a change
            Settings.Flush();
            string file = File.Exists(Settings.PathOnDisk) ? File.ReadAllText(Settings.PathOnDisk) : "";
            Check(Settings.PictureQuality == FidelityStep.Ultra && GraphicsQuality.Level == FidelityStep.Ultra, "right takes it to Ultra, applied");
            Check(file.Contains("\"quality\"") && file.Contains("\"value\": 3.0"), "Ultra is saved in settings.json");
            Shot("settings_fidelity_ultra");
            yield return Key(UnityEngine.InputSystem.Key.RightArrow);
            Check(Settings.PictureQuality == FidelityStep.Ultra, "right again stays at Ultra (the end)");
            yield return Key(UnityEngine.InputSystem.Key.LeftArrow);
            Check(Settings.PictureQuality == FidelityStep.High && GraphicsQuality.Level == FidelityStep.High, "left takes it back to High");
            yield return Key(UnityEngine.InputSystem.Key.Escape);
            yield return Hold(0.3f);
            Check(MenuNav.CardName == "Title" && Focus.StartsWith("Settings"), "Esc closes Settings, the focus back where it was");
            yield return Seek(() => Key(UnityEngine.InputSystem.Key.UpArrow), "Begin");
            Check(Focus.StartsWith("Begin"), "up reaches Begin");
            yield return Key(UnityEngine.InputSystem.Key.Enter);
            yield return Until(() => !TitleScreen.Showing, 10f);
            Check(!TitleScreen.Showing, "Enter on Begin starts the week");
            yield return Until(() => Director.I != null && Director.I.Running && !UIRoot.ModalOpen, 30f);
            yield return Hold(1.5f);
            yield return Key(UnityEngine.InputSystem.Key.Escape);
            yield return Hold(0.3f);
            Check(PauseMenu.Open && MenuNav.CardName == "Pause" && Focus.StartsWith("Back to the desk"), "Esc opens the pause menu, focused");
            Shot("pause_focused");
            yield return Seek(() => Key(UnityEngine.InputSystem.Key.DownArrow), "Controls");
            yield return Key(UnityEngine.InputSystem.Key.Enter);
            yield return Hold(0.3f);
            Check(MenuNav.CardName == "Controls", "Enter on Controls opens the controls card");
            Shot("controls_from_keys");
            yield return Key(UnityEngine.InputSystem.Key.Escape);
            yield return Hold(0.3f);
            Check(PauseMenu.Open && Focus.StartsWith("Controls"), "Esc puts it away, the pause menu still focused on Controls");
            yield return Seek(() => Key(UnityEngine.InputSystem.Key.UpArrow), "Back to the desk");
            yield return Key(UnityEngine.InputSystem.Key.Enter);
            yield return Hold(0.3f);
            Check(!PauseMenu.Open && !UIRoot.ModalOpen, "Enter on Back to the desk closes it");

            // ---- gamepad
            bool uv0 = Lamp.I != null && Lamp.I.UV;
            int nudges0 = Director.I.NudgesTaken;
            yield return Pad(GamepadButton.Start);
            yield return Hold(0.3f);
            Check(GamepadInput.Active && PauseMenu.Open && Focus.StartsWith("Back to the desk"), "the pad's Menu opens the pause menu, focused");
            yield return Seek(() => Pad(GamepadButton.DpadDown), "Settings");
            Check(Focus.StartsWith("Settings"), "the d-pad reaches Settings");
            Shot("pad_pause_settings");
            yield return Pad(GamepadButton.South);
            yield return Hold(0.3f);
            Check(MenuNav.CardName == "Settings", "A opens Settings");
            yield return Seek(() => Pad(GamepadButton.DpadDown), "Graphics fidelity");
            yield return Pad(GamepadButton.DpadRight);
            Check(Settings.PictureQuality == FidelityStep.Ultra && GraphicsQuality.Level == FidelityStep.Ultra, "d-pad right takes the fidelity to Ultra");
            Shot("pad_settings_ultra");
            yield return Pad(GamepadButton.DpadLeft);
            Check(Settings.PictureQuality == FidelityStep.High, "d-pad left takes it back to High");
            yield return Seek(() => Pad(GamepadButton.DpadDown), "Done");
            Check(Focus.StartsWith("Done"), "the d-pad reaches Done");
            yield return Pad(GamepadButton.South);
            yield return Hold(0.3f);
            Check(MenuNav.CardName == "Pause" && Focus.StartsWith("Settings"), "A on Done closes Settings, back on the pause menu's Settings");
            yield return Seek(() => Pad(GamepadButton.DpadUp), "Back to the desk");
            yield return Pad(GamepadButton.South);
            yield return Hold(0.3f);
            Check(!PauseMenu.Open && !UIRoot.ModalOpen, "A on Back to the desk closes it");
            Check((Lamp.I == null || Lamp.I.UV == uv0) && Director.I.NudgesTaken == nudges0 && !UIRoot.ModalOpen,
                "the d-pad did nothing else on the way (the blue lamp and Agnes's nudges, its other jobs, untouched)");

            if (Game.Arg("-lafTextAudit") != null) TextAudit.Summary();
            Debug.Log(failed.Count == 0 ? $"[MenuTest] PASS ({checks} checks)" : $"[MenuTest] FAIL ({failed.Count} of {checks}): {string.Join("; ", failed)}");
            Application.Quit();
        }
    }
}
