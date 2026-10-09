using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LostAndFound
{
    /// <summary>
    /// Player preferences, in settings.json beside the save (Application.persistentDataPath). v0.1.0 kept them in PlayerPrefs,
    /// which the Linux player writes to the game's own folder as "prefs", mixed in with Unity's own keys. Values saved there
    /// are brought over once (Tools/prefs_probe.sh shows where v0.1.0-style saves land and that they're imported).
    /// Writes swap in whole like the save's, and come at most a few times a second while a slider is dragged.
    /// </summary>
    public static class Settings
    {
        [Serializable] class Entry { public string key; public float value; }
        [Serializable] class Store { public List<Entry> values = new(); }

        static Dictionary<string, float> values;
        static string path;
        static bool dirty;
        static float lastWrite = -10f;

        /// <summary>Where the settings live: beside the save, or a test's scratch file (UseFile).</summary>
        public static string PathOnDisk => path ??= Path.Combine(Application.persistentDataPath, "settings.json");

        /// <summary>Read the settings from another file from now on (unit tests use a scratch folder).</summary>
        public static void UseFile(string file)
        {
            path = string.IsNullOrEmpty(file) ? null : file;
            values = null;
            dirty = false;
        }

        static readonly string[] FloatKeys = { "master", "music", "sfx", "voice", "ambience", "mouse", "textspeed", "textsize", "quality", "brightness" };
        static readonly string[] BoolKeys = { "shake", "reducemotion", "post", "fullscreen", "plain", "offernudges", "bgsound", "edgeturn" };

        static Dictionary<string, float> Values
        {
            get
            {
                if (values != null) return values;
                values = new Dictionary<string, float>();
                bool found = false;
                try
                {
                    if (File.Exists(PathOnDisk))
                    {
                        var store = JsonUtility.FromJson<Store>(File.ReadAllText(PathOnDisk));
                        if (store?.values != null)
                            foreach (var e in store.values) if (!string.IsNullOrEmpty(e.key)) values[e.key] = e.value;
                        found = true;
                    }
                }
                catch (Exception e) { Debug.LogWarning($"[Settings] couldn't read {PathOnDisk} ({e.Message}); using the defaults"); }
                if (!found && !Application.isEditor) Import();
                return values;
            }
        }

        /// <summary>Bring over what an earlier version kept in PlayerPrefs, once.</summary>
        static void Import()
        {
            int n = 0;
            foreach (var k in FloatKeys) if (PlayerPrefs.HasKey("laf." + k)) { values[k] = PlayerPrefs.GetFloat("laf." + k); n++; }
            foreach (var k in BoolKeys) if (PlayerPrefs.HasKey("laf." + k)) { values[k] = PlayerPrefs.GetInt("laf." + k); n++; }
            if (n == 0) return;
            Debug.Log($"[Settings] brought {n} setting(s) over from PlayerPrefs to {PathOnDisk}");
            Write();
        }

        static float F(string k, float d) => Values.TryGetValue(k, out var v) ? v : d;
        static void SetF(string k, float v) { Values[k] = v; Changed?.Invoke(); Save(); }
        static bool B(string k, bool d) => Values.TryGetValue(k, out var v) ? v >= 0.5f : d;
        static void SetB(string k, bool v) { Values[k] = v ? 1f : 0f; Changed?.Invoke(); Save(); }

        /// <summary>Write now, or (if the last write was a moment ago, as while dragging a slider) very soon.</summary>
        static void Save()
        {
            dirty = true;
            if (!Application.isPlaying || Time.unscaledTime - lastWrite > 0.3f) Write();
            else Flusher.Ensure();
        }

        /// <summary>Write any change not yet on disk (the flusher calls it, and so does quitting).</summary>
        public static void Flush() { if (dirty) Write(); }

        static void Write()
        {
            dirty = false;
            lastWrite = Application.isPlaying ? Time.unscaledTime : 0f;
            try
            {
                var store = new Store();
                foreach (var kv in Values) store.values.Add(new Entry { key = kv.Key, value = kv.Value });
                Directory.CreateDirectory(Path.GetDirectoryName(PathOnDisk));
                string tmp = PathOnDisk + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(store, true));
                // one rename, as the save does: the old settings or the new, never half
                if (File.Exists(PathOnDisk)) File.Replace(tmp, PathOnDisk, PathOnDisk + ".bak");
                else File.Move(tmp, PathOnDisk);
                SaveGame.Persist();
            }
            catch (Exception e) { Debug.LogWarning("[Settings] could not write settings: " + e.Message); }
        }

        /// <summary>Writes settings changed in quick succession once things settle, and on the way out.</summary>
        class Flusher : MonoBehaviour
        {
            static Flusher instance;

            public static void Ensure()
            {
                if (instance != null) return;
                var go = new GameObject("SettingsFlusher") { hideFlags = HideFlags.HideAndDontSave };
                DontDestroyOnLoad(go);
                instance = go.AddComponent<Flusher>();
            }

            void LateUpdate() { if (dirty && Time.unscaledTime - lastWrite > 0.3f) Write(); }
            void OnApplicationPause(bool paused) { if (paused) Flush(); }
            void OnApplicationQuit() => Flush();
        }

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
        /// <summary>The clerk's and Agnes's handwriting in the game's clear book face instead (see Fonts.Plain).</summary>
        public static bool PlainLettering { get => B("plain", false); set => SetB("plain", value); }
        /// <summary>After a while stuck on a claim, the hint bar offers one of Agnes's nudges (H works either way).</summary>
        public static bool OfferNudges { get => B("offernudges", true); set => SetB("offernudges", value); }
        /// <summary>Turn to the drawers or the shelf when the pointer rests at the side of the screen (the keys, the arrows on
        /// screen and the pad turn it either way).</summary>
        public static bool EdgeTurn { get => B("edgeturn", true); set => SetB("edgeturn", value); }
        /// <summary>Keep the sound on while another window has the focus (see BackgroundMode).</summary>
        public static bool SoundInBackground { get => B("bgsound", true); set => SetB("bgsound", value); }
        /// <summary>-1 darkest, 0 the game as lit, 1 brightest: the 3D scene's exposure (see BrightnessEV), never the paper UI.</summary>
        public static float Brightness { get => Mathf.Clamp(F("brightness", 0f), -1f, 1f); set => SetF("brightness", Mathf.Clamp(value, -1f, 1f)); }
        /// <summary>The brightness in force (the setting, or -lafBrightness for one run, not saved) as stops of exposure:
        /// down to -0.6 for a bright panel in a dark room, up to +1.2 for a dim one, since the desk is mostly lit for mood.</summary>
        public static float BrightnessEV
        {
            get
            {
                brightnessArg ??= float.TryParse(Game.Arg("-lafBrightness") ?? "", System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float o) ? Mathf.Clamp(o, -1f, 1f) : float.NaN;
                float b = float.IsNaN(brightnessArg.Value) ? Brightness : brightnessArg.Value;
                return b < 0f ? b * 0.6f : b * 1.2f;
            }
        }
        static float? brightnessArg;   // read once: PostFX asks every frame
        /// <summary>Graphics fidelity: 0 Low, 1 Medium, 2 High (the default), 3 Ultra (see FidelityStep). Round 1's "Picture quality"
        /// kept 0..2 under the same key, and those mean the same steps.</summary>
        public static int PictureQuality { get => Mathf.Clamp(Mathf.RoundToInt(F("quality", DefaultQuality)), 0, 3); set => SetF("quality", Mathf.Clamp(value, 0, 3)); }
        /// <summary>High on the desktop; Medium in a browser, which draws more slowly and is often on a laptop's graphics; Low on a
        /// phone or tablet, whose browser tab has far less memory to spare (smaller render targets, no ambient occlusion,
        /// reflections or depth of field).</summary>
        public static int DefaultQuality => Application.platform != RuntimePlatform.WebGLPlayer ? FidelityStep.High
            : WebPage.TouchDevice ? FidelityStep.Low : FidelityStep.Medium;
    }
}
