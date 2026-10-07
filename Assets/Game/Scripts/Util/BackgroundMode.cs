using UnityEngine;

namespace LostAndFound
{
    /// <summary>
    /// While another window has the focus, the game draws a few frames a second instead of a full rate (a laptop's
    /// battery and fan notice), and if Settings says so it's silent there. The player keeps running in the background
    /// (the test tools need it), so without this an alt-tabbed game drew the desk at 60 fps all evening. Test tools keep
    /// their full speed: they run unwatched, often without the focus. Tools/unity.sh smallscreen checks it in a headless
    /// KWin by opening another window over the game.
    /// </summary>
    public class BackgroundMode : MonoBehaviour
    {
        public const int BackgroundFps = 10;
        static bool background;

        /// <summary>Running one of the hands-free test tools (which keep full speed), unless asked to test this.</summary>
        static bool ToolRun =>
            Game.Arg("-lafBackgroundTest") == null &&
            (Game.Arg("-lafAutopilot") != null || Game.Arg("-lafSmoke") != null || Game.Arg("-lafDemo") != null ||
             Game.Arg("-lafSoak") != null || Game.Arg("-lafTapTest") != null || Game.Arg("-lafGamepadTest") != null ||
             Game.Arg("-lafAuditHotspots") != null);

        void OnEnable() => Settings.Changed += Apply;
        void OnDisable() => Settings.Changed -= Apply;
        void Start() => Set(!Application.isFocused);   // the game is rebuilt in place: pick up where the last one was
        void OnApplicationFocus(bool focused) => Set(!focused);

        void Set(bool bg)
        {
            if (Application.isEditor || Application.platform == RuntimePlatform.WebGLPlayer || ToolRun) return;
            bool changed = bg != background;
            background = bg;
            Apply();
            if (changed) Debug.Log((bg ? $"[Background] out of focus: {BackgroundFps} fps" : "[Background] back in focus") + $", volume {AudioListener.volume:0.#}");
        }

        void Apply()
        {
            if (Application.isEditor || Application.platform == RuntimePlatform.WebGLPlayer || ToolRun) return;
            // vsync overrides the frame rate cap, so it's off while in the background
            QualitySettings.vSyncCount = background ? 0 : Game.Arg("-lafNoVsync") != null ? 0 : 1;
            Application.targetFrameRate = background ? BackgroundFps : Game.Arg("-lafUncapped") != null ? -1 : 120;
            AudioListener.volume = background && !Settings.SoundInBackground ? 0f : 1f;
        }
    }
}
