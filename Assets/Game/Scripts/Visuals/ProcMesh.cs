using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace LostAndFound
{
    /// <summary>Small procedural meshes for paper things (tags, slips, receipts) and rings.</summary>
    public static class ProcMesh
    {
        static readonly Dictionary<string, Mesh> Cache = new();

        /// <summary>Manila luggage tag lying in the XZ plane (top edge towards +z), clipped top corners.</summary>
        public static Mesh TagMesh(float w, float h, float thick)
        {
            string key = $"tag{w}{h}{thick}";
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            float c = w * 0.28f;
            var outline = new List<Vector2>
            {
                new(-w / 2, -h / 2), new(w / 2, -h / 2), new(w / 2, h / 2 - c), new(w / 2 - c, h / 2),
                new(-w / 2 + c, h / 2), new(-w / 2, h / 2 - c),
            };
            m = Slab(outline, thick);
            Cache[key] = m;
            return m;
        }

        /// <summary>A flat rectangle of paper (XZ plane, +y up) with UVs 0..1.</summary>
        public static Mesh Paper(float w, float h, float thick = 0.0006f)
        {
            string key = $"paper{w}{h}{thick}";
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            var outline = new List<Vector2> { new(-w / 2, -h / 2), new(w / 2, -h / 2), new(w / 2, h / 2), new(-w / 2, h / 2) };
            m = Slab(outline, thick);
            Cache[key] = m;
            return m;
        }

        /// <summary>Extrude a convex outline (XZ) by `thick` upwards; top face UVs map the bounds to 0..1.</summary>
        public static Mesh Slab(List<Vector2> outline, float thick)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var norms = new List<Vector3>();
            var tris = new List<int>();
            Vector2 min = outline[0], max = outline[0];
            foreach (var p in outline) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            Vector2 size = max - min;
            int n = outline.Count;
            // top
            int top = verts.Count;
            foreach (var p in outline)
            {
                verts.Add(new Vector3(p.x, thick, p.y));
                uvs.Add(new Vector2((p.x - min.x) / size.x, (p.y - min.y) / size.y));
                norms.Add(Vector3.up);
            }
            for (int i = 1; i < n - 1; i++) { tris.Add(top); tris.Add(top + i + 1); tris.Add(top + i); }
            // bottom
            int bot = verts.Count;
            foreach (var p in outline)
            {
                verts.Add(new Vector3(p.x, 0f, p.y));
                uvs.Add(new Vector2((p.x - min.x) / size.x, (p.y - min.y) / size.y));
                norms.Add(Vector3.down);
            }
            for (int i = 1; i < n - 1; i++) { tris.Add(bot); tris.Add(bot + i); tris.Add(bot + i + 1); }
            // sides
            for (int i = 0; i < n; i++)
            {
                var a = outline[i];
                var b = outline[(i + 1) % n];
                Vector3 nrm = new Vector3(b.y - a.y, 0f, -(b.x - a.x)).normalized;
                int s = verts.Count;
                verts.Add(new Vector3(a.x, 0, a.y)); verts.Add(new Vector3(b.x, 0, b.y));
                verts.Add(new Vector3(b.x, thick, b.y)); verts.Add(new Vector3(a.x, thick, a.y));
                for (int k = 0; k < 4; k++) { norms.Add(nrm); uvs.Add(Vector2.zero); }
                tris.Add(s); tris.Add(s + 2); tris.Add(s + 1);
                tris.Add(s); tris.Add(s + 3); tris.Add(s + 2);
            }
            var mesh = new Mesh { name = "slab" };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        /// <summary>Flat ring (washer) in the XZ plane.</summary>
        public static Mesh Ring(float outer, float inner, float thick, int seg)
        {
            string key = $"ring{outer}{inner}{thick}{seg}";
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts.Add(d * outer + Vector3.up * thick);
                verts.Add(d * inner + Vector3.up * thick);
            }
            for (int i = 0; i < seg; i++)
            {
                int j = (i + 1) % seg;
                tris.AddRange(new[] { i * 2, j * 2, i * 2 + 1, i * 2 + 1, j * 2, j * 2 + 1 });
            }
            m = new Mesh { name = "ring" };
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            Cache[key] = m;
            return m;
        }

        /// <summary>A unit quad in XY facing -z (for photos in frames, imprints on paper use XZ).</summary>
        public static Mesh QuadXZ(float w, float h)
        {
            string key = $"qxz{w}{h}";
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Mesh { name = "quad" };
            m.vertices = new[] { new Vector3(-w / 2, 0, -h / 2), new Vector3(w / 2, 0, -h / 2), new Vector3(w / 2, 0, h / 2), new Vector3(-w / 2, 0, h / 2) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            m.RecalculateBounds();
            m.RecalculateTangents();
            Cache[key] = m;
            return m;
        }
    }

    public static class DeskMaterials
    {
        public static readonly Color InkColor = new(0.11f, 0.13f, 0.26f);
        public static readonly Color PencilColor = new(0.28f, 0.26f, 0.25f);
        public static readonly Color ReturnInk = new(0.16f, 0.42f, 0.22f);
        public static readonly Color RefuseInk = new(0.62f, 0.13f, 0.12f);
        public static readonly Color SealInk = new(0.18f, 0.16f, 0.24f);
        static Material tag, paper, receipt;

        // (Unity objects need an explicit == null check: ?? doesn't see destroyed objects)
        public static Material Tag => tag != null ? tag : tag = MaterialLibrary.Make(new Color(0.86f, 0.74f, 0.52f), Resources.Load<Texture2D>("Textures/tag_paper"), 0.1f);
        public static Material Paper => paper != null ? paper : paper = MaterialLibrary.Make(new Color(0.96f, 0.93f, 0.85f), Resources.Load<Texture2D>("Textures/slip_paper"), 0.1f);
        public static Material Receipt => receipt != null ? receipt : receipt = MaterialLibrary.Make(new Color(0.97f, 0.95f, 0.9f), Resources.Load<Texture2D>("Textures/paper_a"), 0.12f);
    }

    public static class Text
    {
        /// <summary>World-space TextMeshPro, centred, facing -z of its own transform.</summary>
        public static TextMeshPro World(Transform parent, string text, string font, float size, Color color)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshPro>();
            t.font = Fonts.Get(font);
            t.text = text;
            t.fontSize = size * 10f; // TMP world units: fontSize 10 ~ 1 unit cap height... scale via transform instead
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.rectTransform.sizeDelta = new Vector2(1f, 0.2f);
            t.fontSize = size / 0.1f; // TextMeshPro: fontSize 1 ~ 0.1 units tall line
            t.margin = Vector4.zero;
            return t;
        }
    }
}
