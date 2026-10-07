using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace LostAndFound
{
    /// <summary>Dynamic TMP font assets created at runtime from the OFL/Apache TTFs in Resources/Fonts.</summary>
    public static class Fonts
    {
        public const string Hand = "Caveat";          // the clerk's handwriting: tags, slip
        public const string Agnes = "Kalam";          // Agnes's notes
        public const string AgnesBold = "KalamBold";
        public const string Type = "SpecialElite";    // typewritten forms
        public const string Receipt = "CourierPrime"; // ticket printer
        public const string ReceiptBold = "CourierPrimeBold";
        public const string Title = "IMFell";         // titles, gazette
        public const string TitleItalic = "IMFellItalic";
        public const string Sign = "Limelight";       // station signage
        public const string Body = "CrimsonPro";      // UI body
        public const string Script = "HomemadeApple"; // signatures

        static readonly Dictionary<string, TMP_FontAsset> Cache = new();

        /// <summary>Settings > Plain lettering (or -lafPlainLettering for one run): the two hands, the clerk's on tags and the
        /// slip and Agnes's on her notes, rules and nudges, are set in the clear book face. Signatures stay signatures.</summary>
        public static bool Plain => Settings.PlainLettering || Game.Arg("-lafPlainLettering") != null;

        static bool IsHand(string name) => name == Hand || name == Agnes || name == AgnesBold;

        /// <summary>Set a text's font by role, so it follows the Plain lettering setting from then on.</summary>
        public static void Assign(TMP_Text t, string font)
        {
            t.font = Get(font);
            if (!IsHand(font)) return;
            var role = t.gameObject.AddComponent<FontRole>();
            role.logical = font;
            role.ApplyStyle(t);
        }

        static bool appliedPlain;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Watch()
        {
            appliedPlain = Plain;
            Debug.Log($"[Fonts] plain lettering {(Plain ? "on" : "off")}");
            Settings.Changed += () =>
            {
                if (Plain == appliedPlain) return;
                appliedPlain = Plain;
                foreach (var r in Object.FindObjectsByType<FontRole>(FindObjectsInactive.Include)) r.Apply();
            };
        }

        public static TMP_FontAsset Get(string name) => Load(Plain && IsHand(name) ? Body : name);

        /// <summary>Extra line spacing (TMP's hundredths of an em) that gives the book face a hand's line pitch, so plain
        /// lettering still sits on a note's or a tag's ruled lines.</summary>
        public static float PitchFix(string hand)
        {
            var h = Load(hand).faceInfo;
            var b = Load(Body).faceInfo;
            if (h.pointSize <= 0 || b.pointSize <= 0) return 0f;
            return (h.lineHeight / h.pointSize - b.lineHeight / b.pointSize) * 100f;
        }

        static TMP_FontAsset Load(string name)
        {
            if (Cache.TryGetValue(name, out var fa) && fa != null) return fa;
            var font = Resources.Load<Font>("Fonts/" + name);
            if (font == null)
            {
                Debug.LogWarning($"[Fonts] missing {name}");
                return TMP_Settings.defaultFontAsset;
            }
            fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            fa.name = name;
            fa.hashCode = TMP_TextUtilities.GetHashCode(name); // computed before the rename otherwise
            if (fa.material != null)
            {
                fa.material.name = name + " Material";
                fa.materialHashCode = TMP_TextUtilities.GetSimpleHashCode(fa.material.name);
            }
            Cache[name] = fa;
            // lets rich text switch fonts with <font="Name">
            if (!MaterialReferenceManager.TryGetFontAsset(fa.hashCode, out _)) MaterialReferenceManager.AddFontAsset(fa);
            return fa;
        }

        /// <summary>Material for world-space text that should sit on paper: no bloom, slight softness.</summary>
        public static Material Ink(string font) => Get(font).material;
    }

    /// <summary>Which hand a text is written in, so it can switch with Settings > Plain lettering while it's on screen.</summary>
    public class FontRole : MonoBehaviour
    {
        public string logical;
        float pitchApplied;   // the line spacing this has added for the book face, taken back off on the way back

        // after whoever made the text has set its own line spacing
        void Start() => Pitch(GetComponent<TMP_Text>());

        public void Apply()
        {
            var t = GetComponent<TMP_Text>();
            if (t == null) return;
            t.font = Fonts.Get(logical);
            ApplyStyle(t);
            Pitch(t);
        }

        void Pitch(TMP_Text t)
        {
            if (t == null) return;
            float want = Fonts.Plain ? Fonts.PitchFix(logical) : 0f;
            t.lineSpacing += want - pitchApplied;
            pitchApplied = want;
        }

        /// <summary>Agnes's bold hand becomes the book face in bold.</summary>
        public void ApplyStyle(TMP_Text t)
        {
            if (logical != Fonts.AgnesBold) return;
            if (Fonts.Plain) t.fontStyle |= FontStyles.Bold;
            else t.fontStyle &= ~FontStyles.Bold;
        }
    }
}
