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

        public static TMP_FontAsset Get(string name)
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
}
