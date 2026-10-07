using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace LostAndFound
{
    /// <summary>
    /// -lafTextAudit (with the AutoPilot): at every screenshot, every piece of text on screen is checked against its box.
    /// Text that runs outside it (past a card's edge, off a tag) is logged as "[TextAudit] … overflows". Autosized text
    /// that had to shrink below 60% of its size is logged as "small", which isn't a failure but is worth a look.
    /// </summary>
    public static class TextAudit
    {
        public static int Shots, Overflows, Small;
        static readonly HashSet<string> seenOverflow = new(), seenSmall = new();

        public static void Check(string shot)
        {
            Shots++;
            foreach (var t in Object.FindObjectsByType<TMP_Text>())
            {
                if (!t.isActiveAndEnabled || string.IsNullOrEmpty(t.text) || !Visible(t)) continue;
                t.ForceMeshUpdate();
                var ti = t.textInfo;
                if (ti == null || ti.characterCount == 0 || ti.lineCount == 0) continue;
                Bounds b = t.textBounds;
                Rect r = t.rectTransform.rect;
                Vector4 m = t.margin;
                var inner = Rect.MinMaxRect(r.xMin + m.x, r.yMin + m.w, r.xMax - m.z, r.yMax - m.y);
                // a third of a line's height of slack: descenders, italics and outlines may lean out a little
                float tol = Mathf.Abs(ti.lineInfo[0].lineHeight) * 0.33f;
                float dx = Mathf.Max(inner.xMin - b.min.x, b.max.x - inner.xMax);
                float dy = Mathf.Max(inner.yMin - b.min.y, b.max.y - inner.yMax);
                string who = Describe(t);
                if (dx > tol || dy > tol)
                {
                    Overflows++;
                    if (seenOverflow.Add(who))
                        Debug.Log($"[TextAudit] {shot}: {who} overflows its box by {Pct(dx, inner.width)} across, {Pct(dy, inner.height)} down (font {t.font?.name}, size {t.fontSize:0.###})");
                }
                if (t.enableAutoSizing && t.fontSizeMax > 0f && t.fontSize < t.fontSizeMax * 0.6f)
                {
                    Small++;
                    if (seenSmall.Add(who))
                        Debug.Log($"[TextAudit] {shot}: {who} is small: autosized to {t.fontSize / t.fontSizeMax:P0} of its size ({t.fontSize:0.###} of {t.fontSizeMax:0.###}, {ti.lineCount} lines, text {b.size.y:0.###} high in a box {inner.height:0.###} high)");
                }
            }
        }

        public static void Summary() =>
            Debug.Log($"[TextAudit] {Shots} screenshots checked: {Overflows} overflowing texts ({seenOverflow.Count} distinct), {seenSmall.Count} distinct texts autosized below 60%");

        static bool Visible(TMP_Text t)
        {
            if (t.color.a < 0.05f) return false;
            if (t is TextMeshProUGUI ui) return ui.canvasRenderer != null && ui.canvasRenderer.GetInheritedAlpha() > 0.05f;
            var rend = t.GetComponent<Renderer>();
            return rend != null && rend.enabled && rend.isVisible;
        }

        static string Pct(float over, float size) => over <= 0f ? "0" : size > 1e-5f ? $"{over / size:P0}" : $"{over:0.###}";

        static string Describe(TMP_Text t)
        {
            var path = t.name;
            var p = t.transform.parent;
            for (int i = 0; i < 3 && p != null; i++, p = p.parent) path = p.name + "/" + path;
            string plain = System.Text.RegularExpressions.Regex.Replace(t.text, "<[^>]+>", "").Replace('\n', ' ');
            if (plain.Length > 40) plain = plain.Substring(0, 40) + "…";
            return $"{path} \"{plain}\"";
        }
    }
}
