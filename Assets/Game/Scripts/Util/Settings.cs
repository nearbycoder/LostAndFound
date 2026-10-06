using UnityEngine;

namespace LostAndFound
{
    /// <summary>Player preferences (PlayerPrefs-backed).</summary>
    public static class Settings
    {
        static float F(string k, float d) => PlayerPrefs.GetFloat("laf." + k, d);
        static void SetF(string k, float v) { PlayerPrefs.SetFloat("laf." + k, v); PlayerPrefs.Save(); Changed?.Invoke(); }
        static bool B(string k, bool d) => PlayerPrefs.GetInt("laf." + k, d ? 1 : 0) == 1;
        static void SetB(string k, bool v) { PlayerPrefs.SetInt("laf." + k, v ? 1 : 0); PlayerPrefs.Save(); Changed?.Invoke(); }

        public static event System.Action Changed;

        public static float MasterVolume { get => F("master", 0.85f); set => SetF("master", value); }
        public static float MusicVolume { get => MusicMuted ? 0f : F("music", 0.6f); set => SetF("music", value); }
        /// <summary>-lafNoMusic: silence the score for this run only (trailer captures lay their own music bed).</summary>
        public static bool MusicMuted;
        public static float SfxVolume { get => F("sfx", 0.85f); set => SetF("sfx", value); }
        public static float VoiceVolume { get => F("voice", 0.8f); set => SetF("voice", value); }
        public static float AmbienceVolume { get => F("ambience", 0.7f); set => SetF("ambience", value); }
        public static float MouseSensitivity { get => F("mouse", 1f); set => SetF("mouse", value); }
        public static float TextSpeed { get => F("textspeed", 1f); set => SetF("textspeed", value); }
        public static bool ScreenShake { get => B("shake", true); set => SetB("shake", value); }
        public static bool ReduceMotion { get => B("reducemotion", false); set => SetB("reducemotion", value); }
        public static bool PostEffects { get => B("post", true); set => SetB("post", value); }
        public static bool Fullscreen { get => B("fullscreen", false); set => SetB("fullscreen", value); }
        /// <summary>0 Normal, 1 Large: the reading UI (dialogue, hints, tags, notes) at 1.25x.</summary>
        public static int TextSize { get => Mathf.Clamp(Mathf.RoundToInt(F("textsize", 0f)), 0, 1); set => SetF("textsize", Mathf.Clamp(value, 0, 1)); }
        /// <summary>After a while stuck on a claim, the hint bar offers one of Agnes's nudges (H works either way).</summary>
        public static bool OfferNudges { get => B("offernudges", true); set => SetB("offernudges", value); }
        /// <summary>0 Low, 1 Medium, 2 High (see GraphicsQuality).</summary>
        public static int PictureQuality { get => Mathf.Clamp(Mathf.RoundToInt(F("quality", 2f)), 0, 2); set => SetF("quality", Mathf.Clamp(value, 0, 2)); }
    }
}
