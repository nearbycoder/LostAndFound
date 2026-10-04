using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LostAndFound
{
    /// <summary>
    /// All sound: pooled one-shots (with random variants name_1..name_n), spatial loops, a crossfading
    /// music pair with ducking and a low-pass for menus, the station ambience bed, and voice babble.
    /// Every clip is synthesized by Tools/audio and lives in Resources/Audio or Resources/Music.
    /// </summary>
    public class AudioDirector : MonoBehaviour
    {
        public static AudioDirector I { get; private set; }
        readonly Dictionary<string, AudioClip[]> clips = new();
        readonly List<AudioSource> pool = new();
        AudioSource musicA, musicB, ambience, ambience2;
        AudioLowPassFilter lowPassA, lowPassB;
        bool aIsCurrent = true;
        string currentTrack;
        float duck = 1f, duckTarget = 1f, musicFade = 1f;
        float lowPassTarget = 22000f;

        public static float SfxVolume => Settings.MasterVolume * Settings.SfxVolume;
        public static float VoiceVolume => Settings.MasterVolume * Settings.VoiceVolume;

        void Awake()
        {
            I = this;
            for (int i = 0; i < 24; i++) pool.Add(NewSource("sfx" + i));
            musicA = NewSource("musicA"); musicA.loop = true;
            musicB = NewSource("musicB"); musicB.loop = true;
            lowPassA = musicA.gameObject.AddComponent<AudioLowPassFilter>();
            lowPassB = musicB.gameObject.AddComponent<AudioLowPassFilter>();
            lowPassA.cutoffFrequency = lowPassB.cutoffFrequency = 22000f;
            ambience = NewSource("ambience"); ambience.loop = true;
            ambience2 = NewSource("ambience2"); ambience2.loop = true;
        }

        AudioSource NewSource(string n)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            return s;
        }

        AudioClip[] Clips(string name, string folder = "Audio")
        {
            string key = folder + "/" + name;
            if (clips.TryGetValue(key, out var arr)) return arr;
            var list = new List<AudioClip>();
            var c = Resources.Load<AudioClip>(key);
            if (c != null) list.Add(c);
            for (int i = 1; i <= 8; i++)
            {
                var v = Resources.Load<AudioClip>($"{key}_{i}");
                if (v == null) { if (i > 1) break; continue; }
                list.Add(v);
            }
            arr = list.ToArray();
            clips[key] = arr;
            return arr;
        }

        public static AudioClip Clip(string name)
        {
            if (I == null) return null;
            var arr = I.Clips(name);
            return arr.Length == 0 ? null : arr[Random.Range(0, arr.Length)];
        }

        AudioSource FreeSource()
        {
            foreach (var s in pool) if (!s.isPlaying) return s;
            return pool[Random.Range(0, pool.Count)];
        }

        public static AudioSource Play(string name, float volume = 1f, float pitch = 1f, float delay = 0f, Vector3? at = null)
        {
            if (I == null) return null;
            var clip = Clip(name);
            if (clip == null) return null;
            var s = I.FreeSource();
            s.clip = clip;
            s.volume = volume * SfxVolume;
            s.pitch = pitch;
            s.spatialBlend = at.HasValue ? 0.6f : 0f;
            if (at.HasValue) s.transform.position = at.Value;
            s.minDistance = 0.5f;
            s.maxDistance = 6f;
            if (delay > 0f) s.PlayDelayed(delay); else s.Play();
            return s;
        }

        public static void PlayVoice(AudioClip clip, float volume, float pitch, float pan = 0f)
        {
            if (I == null || clip == null) return;
            var s = I.FreeSource();
            s.clip = clip;
            s.volume = volume * VoiceVolume;
            s.pitch = pitch;
            s.spatialBlend = 0f;
            s.panStereo = pan;
            s.Play();
        }

        /// <summary>Material foley: "leather_pick", "metal_put"... falling back to wood.</summary>
        public static void PlayMaterial(string material, string action, float volume = 1f)
        {
            if (I == null) return;
            string n = $"{material}_{action}";
            if (I.Clips(n).Length == 0) n = $"wood_{action}";
            Play(n, volume, Random.Range(0.92f, 1.08f));
        }

        public static AudioSource Loop(string name, Transform parent, float volume)
        {
            if (I == null) return null;
            var clip = Clip(name);
            if (clip == null) return null;
            var go = new GameObject("loop_" + name);
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.volume = volume * SfxVolume;
            s.spatialBlend = 0.7f;
            s.minDistance = 0.4f;
            s.maxDistance = 5f;
            s.dopplerLevel = 0f;
            s.time = Random.Range(0f, clip.length);
            s.Play();
            return s;
        }

        // ------------------------------------------------------------------ music & ambience

        public static void Music(string track, float fade = 2f)
        {
            if (I == null || track == I.currentTrack) return;
            I.currentTrack = track;
            I.StartCoroutine(I.Crossfade(track, fade));
        }

        IEnumerator Crossfade(string track, float fade)
        {
            var from = aIsCurrent ? musicA : musicB;
            var to = aIsCurrent ? musicB : musicA;
            aIsCurrent = !aIsCurrent;
            var arr = string.IsNullOrEmpty(track) ? new AudioClip[0] : Clips(track, "Music");
            to.clip = arr.Length > 0 ? arr[0] : null;
            if (to.clip != null) { to.volume = 0f; to.Play(); }
            float v0 = from.volume;
            yield return Tween.Run(fade, k =>
            {
                from.volume = v0 * (1f - k);
                if (to.clip != null) to.volume = k * MusicLevel;
            }, Ease.InOutSine, true);
            from.Stop();
        }

        float MusicLevel => Settings.MasterVolume * Settings.MusicVolume * 0.55f * duck * musicFade;

        public static void Ambience(string bed, string second = null)
        {
            if (I == null) return;
            I.SetBed(I.ambience, bed, 0.55f);
            I.SetBed(I.ambience2, second, 0.35f);
        }

        void SetBed(AudioSource s, string name, float vol)
        {
            var clip = string.IsNullOrEmpty(name) ? null : Clip(name);
            if (clip == s.clip) return;
            s.clip = clip;
            s.volume = vol * Settings.MasterVolume * Settings.AmbienceVolume;
            if (clip != null) s.Play(); else s.Stop();
        }

        /// <summary>Lower the music under dialogue or big moments (1 = full).</summary>
        public static void Duck(float level) { if (I != null) I.duckTarget = level; }

        /// <summary>Muffle the music (menus, pause).</summary>
        public static void Muffle(bool on) { if (I != null) I.lowPassTarget = on ? 900f : 22000f; }

        public static void FadeMusic(float level) { if (I != null) I.musicFade = level; }

        void Update()
        {
            duck = Mathf.MoveTowards(duck, duckTarget, Time.unscaledDeltaTime * 1.5f);
            var cur = aIsCurrent ? musicA : musicB;
            if (cur.clip != null && cur.isPlaying) cur.volume = Mathf.MoveTowards(cur.volume, MusicLevel, Time.unscaledDeltaTime * 0.6f);
            float lp = Mathf.Lerp(lowPassA.cutoffFrequency, lowPassTarget, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
            lowPassA.cutoffFrequency = lowPassB.cutoffFrequency = lp;
            ambience.volume = 0.55f * Settings.MasterVolume * Settings.AmbienceVolume;
            ambience2.volume = 0.35f * Settings.MasterVolume * Settings.AmbienceVolume;
        }
    }
}
