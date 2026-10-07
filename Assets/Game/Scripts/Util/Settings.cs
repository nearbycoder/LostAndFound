using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LostAndFound
{
    /// <summary>
    /// Player preferences, in settings.json beside the save (Application.persistentDataPath). They used to be PlayerPrefs,
    /// but Unity's Linux player kept the game's keys in ~/.config/unity3d/unknown/unknown/prefs, a folder other games
    /// with the same problem share, rather than the game's own. Values saved there before are brought over once.
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

        static readonly string[] FloatKeys = { "master", "music", "sfx", "voice", "ambience", "mouse", "textspeed", "textsize", "quality" };
        static readonly string[] BoolKeys = { "shake", "reducemotion", "post", "fullscreen", "plain", "offernudges" };

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
        /// <summary>0 Low, 1 Medium, 2 High (see GraphicsQuality).</summary>
        public static int PictureQuality { get => Mathf.Clamp(Mathf.RoundToInt(F("quality", 2f)), 0, 2); set => SetF("quality", Mathf.Clamp(value, 0, 2)); }
    }
}
