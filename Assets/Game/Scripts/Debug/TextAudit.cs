using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace LostAndFound
{
    /// <summary>
    /// -lafTextAudit (with the AutoPilot): at every screenshot, every piece of text on screen is checked against its box.
    /// Text that runs outside it (past a card's edge, off a tag) is logged as "[TextAudit] … overflows". Autosized text
    /// that had to shrink below 60% of its size is logged as "small", which isn't a failure but is worth a look.
    /// Texts are also checked against each other: two texts on the same card or panel whose drawn letters cross on screen
    /// are logged as "[TextAudit] … runs into …". Texts on different panels (a hint bar over the slip, a modal over the
    /// desk) are layered by design and aren't compared.
    /// </summary>
    public static class TextAudit
    {
        public static int Shots, Overflows, Small, Overlaps;
        static readonly HashSet<string> seenOverflow = new(), seenSmall = new(), seenOverlap = new();

        public static void Check(string shot)
        {
            Shots++;
            var drawn = new List<(TMP_Text t, Transform group, List<Rect> glyphs, Rect all)>();
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
                var glyphs = Glyphs(t);
                if (glyphs.Count > 0) drawn.Add((t, Group(t), glyphs, Enclose(glyphs)));
            }
            for (int i = 0; i < drawn.Count; i++)
                for (int j = i + 1; j < drawn.Count; j++)
                {
                    var (a, b) = (drawn[i], drawn[j]);
                    if (a.group != b.group || !a.all.Overlaps(b.all)) continue;
                    int crossing = 0;
                    foreach (var ga in a.glyphs)
                        if (ga.Overlaps(b.all))
                            foreach (var gb in b.glyphs) if (ga.Overlaps(gb)) { crossing++; break; }
                    if (crossing == 0) continue;
                    Overlaps++;
                    string pair = Describe(a.t) + " runs into " + Describe(b.t);
                    if (seenOverlap.Add(pair)) Debug.Log($"[TextAudit] {shot}: {pair} ({crossing} letters cross)");
                }
        }

        /// <summary>Where each visible letter is drawn on screen, trimmed of the padding around the glyph, so letters that only
        /// kiss (a descender over the next line's capital) don't count.</summary>
        static List<Rect> Glyphs(TMP_Text t)
        {
            var list = new List<Rect>();
            var ti = t.textInfo;
            int max = Mathf.Min(ti.characterCount, t.maxVisibleCharacters);
            var canvas = t is TextMeshProUGUI ui ? ui.canvas?.rootCanvas : null;
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? (canvas.worldCamera != null ? canvas.worldCamera : Camera.main)
                : canvas == null ? Camera.main : null;
            // a 3D text the camera isn't looking at (its renderer may still count as visible, for a shadow) isn't checked
            if (canvas == null && cam != null && t.TryGetComponent<Renderer>(out var rend)
                && !GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(cam), rend.bounds)) return list;
            for (int i = 0; i < max; i++)
            {
                var c = ti.characterInfo[i];
                if (!c.isVisible || c.lineNumber >= t.maxVisibleLines) continue;
                Vector3 p0 = t.transform.TransformPoint(c.vertex_BL.position), p1 = t.transform.TransformPoint(c.vertex_TR.position);
                if (cam != null) { p0 = cam.WorldToScreenPoint(p0); p1 = cam.WorldToScreenPoint(p1); if (p0.z <= 0f || p1.z <= 0f) continue; }
                var r = Rect.MinMaxRect(Mathf.Min(p0.x, p1.x), Mathf.Min(p0.y, p1.y), Mathf.Max(p0.x, p1.x), Mathf.Max(p0.y, p1.y));
                float ix = r.width * 0.15f, iy = r.height * 0.2f;
                r = Rect.MinMaxRect(r.xMin + ix, r.yMin + iy, r.xMax - ix, r.yMax - iy);
                // only what's on screen: a 3D text can count as visible for casting a shadow into view while it's behind the
                // turned camera, where its letters project wildly
                r = Rect.MinMaxRect(Mathf.Max(r.xMin, 0f), Mathf.Max(r.yMin, 0f), Mathf.Min(r.xMax, Screen.width), Mathf.Min(r.yMax, Screen.height));
                if (r.width > 0f && r.height > 0f) list.Add(r);
            }
            return list;
        }

        static Rect Enclose(List<Rect> rs)
        {
            var r = rs[0];
            foreach (var g in rs) r = Rect.MinMaxRect(Mathf.Min(r.xMin, g.xMin), Mathf.Min(r.yMin, g.yMin), Mathf.Max(r.xMax, g.xMax), Mathf.Max(r.yMax, g.yMax));
            return r;
        }

        /// <summary>The card or panel a text belongs to: the canvas's own child that holds it, or a 3D text's parent.</summary>
        static Transform Group(TMP_Text t)
        {
            if (!(t is TextMeshProUGUI)) return t.transform.parent;
            var tr = t.transform;
            while (tr.parent != null && tr.parent.GetComponent<Canvas>() == null) tr = tr.parent;
            return tr;
        }

        public static void Summary() =>
            Debug.Log($"[TextAudit] {Shots} screenshots checked: {Overflows} overflowing texts ({seenOverflow.Count} distinct), {seenSmall.Count} distinct texts autosized below 60%, {Overlaps} texts running into another ({seenOverlap.Count} distinct pairs)");

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
