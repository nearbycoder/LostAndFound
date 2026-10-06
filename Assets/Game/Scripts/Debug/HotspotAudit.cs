using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace LostAndFound
{
    /// <summary>
    /// -lafAuditHotspots &lt;dir&gt;: proves every hidden detail can actually be clicked in the hand. Each object
    /// is held up as the inspect controller holds it, with its lids shut and then open, and turned through
    /// a fixed set of random orientations at two zooms. At each one the detail counts as reachable if the
    /// inspect controller's own picking (screen distance, facing, self-occlusion) would take a click on it.
    /// Writes a coverage table to the log and a picture of every detail below the bar into &lt;dir&gt;.
    /// </summary>
    public class HotspotAudit : MonoBehaviour
    {
        /// <summary>A detail must be clickable from at least this share of orientations.</summary>
        public const float MinCoverage = 0.10f;
        const int Samples = 1500;
        static readonly float[] Zooms = { 1f, 1.3f };
        const float ScreenMargin = 60f;

        string dir;
        readonly List<string> storageRows = new();

        void Start()
        {
            dir = Game.Arg("-lafAuditHotspots");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Application.persistentDataPath, "hotspot_audit");
            Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir, "*.png")) File.Delete(f);
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            // let the title settle, then get it out of the way
            for (int i = 0; i < 30; i++) yield return null;
            UIRoot.I.canvas.enabled = false;
            InteractionSystem.I.Blocked = true;
            CameraRig.I.allowTurn = false;
            PostFX.I?.SetInspect(true);
            Desk.I.CloseAllDrawers();
            yield return new WaitForSeconds(0.5f);

            int storageProblems = 0;
            yield return AuditStorage(n => storageProblems = n);
            CameraRig.I.SetView(View.Counter, true);
            Desk.I.ClearItems();
            yield return new WaitForSeconds(0.3f);

            var cam = Camera.main;
            var insp = InspectController.I;
            var root = new GameObject("HotspotAudit").transform;
            var rows = new List<string>();
            var low = new List<string>();
            int checkedCount = 0;

            Random.InitState(9);
            var turns = new Quaternion[Samples];
            for (int i = 0; i < Samples; i++) turns[i] = Random.rotationUniform;

            foreach (var def in Game.I.Db.root.objects)
            {
                var item = ItemView.Create(def, root);
                item.place = ItemPlace.Held;   // keeps the resting jiggle off the pivot
                item.EnsureMeshColliders();
                item.SetPickable(false);
                foreach (var tag in item.GetComponentsInChildren<ItemTag>()) tag.gameObject.SetActive(false);
                var lids = item.parts.Where(p => p.def.kind is "hinge" or "slide").ToList();
                var revealed = new HashSet<string>(item.parts.Where(p => !string.IsNullOrEmpty(p.def.reveals)).Select(p => p.def.reveals));

                var details = def.details.Where(d => !revealed.Contains(d.id) && !(d.requires is "wind" or "listen" or "shake" or "play")).ToList();
                var hitsOpen = new Dictionary<string, int>();
                var hitsShut = new Dictionary<string, int>();
                var bestTurn = new Dictionary<string, (Quaternion q, float z, bool open)>();
                foreach (var d in details) { hitsOpen[d.id] = 0; hitsShut[d.id] = 0; }

                foreach (bool open in lids.Count > 0 ? new[] { false, true } : new[] { false })
                {
                    foreach (var p in lids) p.Snap(open);
                    foreach (float z in Zooms)
                        foreach (var q in turns)
                        {
                            insp.PoseInHand(item, q, z);
                            Physics.SyncTransforms();
                            foreach (var d in details)
                            {
                                var hs = item.Hotspot(d);
                                if (hs == null) continue;
                                Vector3 sp = cam.WorldToScreenPoint(hs.position);
                                if (sp.z <= 0 || sp.x < ScreenMargin || sp.y < ScreenMargin || sp.x > Screen.width - ScreenMargin || sp.y > Screen.height - ScreenMargin) continue;
                                var got = insp.NearDetail(item, sp, true, out float px, out _);
                                if (got != d || px > InspectController.ClickRadius) continue;
                                if (open) hitsOpen[d.id]++; else hitsShut[d.id]++;
                                if (!bestTurn.ContainsKey(d.id)) bestTurn[d.id] = (q, z, open);
                            }
                        }
                }

                int perState = Samples * Zooms.Length;
                foreach (var d in details)
                {
                    checkedCount++;
                    float shut = hitsShut[d.id] / (float)perState, opened = hitsOpen[d.id] / (float)perState;
                    float cov = Mathf.Max(shut, opened);
                    bool hasHs = item.Hotspot(d) != null;
                    bool ok = hasHs && cov >= MinCoverage;
                    string what = $"{def.id}.{d.id}";
                    string row = $"{what,-28} {d.kind,-6} {(string.IsNullOrEmpty(d.requires) ? "" : d.requires),-4} {cov * 100f,5:0.0}%  (shut {shut * 100f:0.0}%, open {(lids.Count > 0 ? (opened * 100f).ToString("0.0") + "%" : "-")})  {(hasHs ? (ok ? "ok" : "LOW") : "NO HOTSPOT")}";
                    rows.Add(row);
                    Debug.Log("[Audit] " + row);
                    if (ok) continue;
                    low.Add(what);
                    if (!hasHs) continue;
                    // a picture: the best orientation found, else the hotspot turned square to the eye
                    if (bestTurn.TryGetValue(d.id, out var b))
                    {
                        foreach (var p in lids) p.Snap(b.open);
                        insp.PoseInHand(item, b.q, b.z);
                    }
                    else
                    {
                        foreach (var p in lids) p.Snap(true);
                        insp.PoseInHand(item, Quaternion.identity, 1f);
                        var hs = item.Hotspot(d);
                        var face = Quaternion.FromToRotation(hs.up, -cam.transform.forward);
                        insp.PoseInHand(item, Quaternion.Inverse(cam.transform.rotation) * face * item.transform.rotation, 1f);
                    }
                    yield return null;
                    var mark = cam.WorldToScreenPoint(item.Hotspot(d).position);
                    Debug.Log($"[Audit]   picture {what}: hotspot at screen ({mark.x:0}, {mark.y:0})");
                    ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"{what}.png"));
                    yield return null;
                    yield return null;
                }
                Destroy(item.gameObject);
                yield return null;
            }

            Debug.Log($"[Audit] {checkedCount} details checked, {low.Count} below {MinCoverage * 100f:0}%{(low.Count > 0 ? ": " + string.Join(", ", low) : "")}");
            File.WriteAllLines(Path.Combine(dir, "coverage.txt"), rows.Concat(storageRows));
            Debug.Log(low.Count == 0 && storageProblems == 0 ? "[Audit] PASS" : "[Audit] FAIL");
            Application.Quit();
        }

        /// <summary>
        /// Every day with everything that could still be in storage (nothing returned yet, the fullest the
        /// shelves and drawers get): every object needs a real shelf slot, and must be something a click lands
        /// on from at least MinCoverage of its face, from where you'd look for it (the shelf, or its drawer
        /// pulled open). Shelf objects whose bounds overlap are listed as warnings.
        /// </summary>
        IEnumerator AuditStorage(System.Action<int> done)
        {
            var db = Game.I.Db;
            var ip = InteractionSystem.I;
            var cam = Camera.main;
            int problems = 0;
            void Report(string line, bool bad)
            {
                storageRows.Add(line);
                Debug.Log("[Audit] " + line);
                if (bad) problems++;
            }
            for (int day = 1; day <= db.DayCount; day++)
            {
                Desk.I.SpawnItems(db, day, new StoryState());
                yield return null;
                Physics.SyncTransforms();
                var stored = Desk.I.items.Values.Where(v => v != null && v.place == ItemPlace.Storage && !v.presented).ToList();

                var shelf = stored.Where(v => v.shelfAnchor != null).ToList();
                foreach (var v in shelf)
                    if (v.shelfAnchor.name != v.def.storage) Report($"day {day}: {v.def.id} wants {v.def.storage}, which the shelf doesn't have (put on {v.shelfAnchor.name})", true);
                for (int i = 0; i < shelf.Count; i++)
                    for (int j = i + 1; j < shelf.Count; j++)
                    {
                        // umbrellas share the stand, leaning apart: their boxes overlap by design
                        if (shelf[i].shelfAnchor.name.StartsWith("Stand_") && shelf[j].shelfAnchor.name.StartsWith("Stand_")) continue;
                        Bounds a = Box(shelf[i]), b = Box(shelf[j]);
                        a.Expand(-0.01f);
                        b.Expand(-0.01f);
                        if (a.Intersects(b)) Report($"day {day}: warning: {shelf[i].def.id} ({shelf[i].def.storage}) overlaps {shelf[j].def.id} ({shelf[j].def.storage})", false);
                    }

                CameraRig.I.SetView(View.Shelf, true);
                yield return new WaitForSeconds(0.8f);
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"shelf_day{day}.png"));   // the fullest the shelf gets that day
                yield return null;
                foreach (var v in shelf)
                {
                    var bx = Box(v);
                    float p = Pickable(ip, cam, v);
                    Report($"day {day}: {v.def.id,-16} {v.def.storage,-9} hoverable from {p * 100f:0}% of its face{(p < MinCoverage ? "  LOW" : "")}   (spans z {bx.min.z:0.00}..{bx.max.z:0.00}, x {bx.min.x:0.00}..{bx.max.x:0.00}, y {bx.min.y:0.00}..{bx.max.y:0.00})", p < MinCoverage);
                }

                CameraRig.I.SetView(View.Cabinet, true);
                yield return new WaitForSeconds(0.6f);
                foreach (var d in Desk.I.drawers.Values)
                {
                    var inside = stored.Where(v => v.drawer == d).ToList();
                    if (inside.Count == 0) continue;
                    d.SetOpen(true, true);
                    yield return new WaitForSeconds(1.4f);
                    Physics.SyncTransforms();
                    foreach (var v in inside)
                    {
                        float p = Pickable(ip, cam, v);
                        Report($"day {day}: {v.def.id,-16} drawer {d.letter,-4} hoverable from {p * 100f:0}% of its face{(p < MinCoverage ? "  LOW" : "")}", p < MinCoverage);
                    }
                    d.SetOpen(false, true);
                    yield return new WaitForSeconds(0.5f);
                }
            }
            Debug.Log($"[Audit] storage: {problems} problems");
            done(problems);
        }

        static Bounds Box(ItemView v)
        {
            var rs = v.model.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Share of a grid over the object's on-screen bounds where a click would land on it.</summary>
        static float Pickable(InteractionSystem ip, Camera cam, ItemView v)
        {
            var b = Box(v);
            int hit = 0, total = 0;
            for (int ix = 0; ix <= 6; ix++)
                for (int iy = 0; iy <= 6; iy++)
                    for (int iz = 0; iz <= 2; iz++)
                    {
                        var w = b.min + Vector3.Scale(b.size, new Vector3(0.1f + 0.8f * ix / 6f, 0.1f + 0.8f * iy / 6f, 0.1f + 0.8f * iz / 2f));
                        Vector3 s = cam.WorldToScreenPoint(w);
                        if (s.z <= 0 || s.x < 0 || s.y < 0 || s.x > Screen.width || s.y > Screen.height) continue;
                        total++;
                        if (ip.PickAt(s, out _) == v) hit++;
                    }
            return total == 0 ? 0f : hit / (float)total;
        }
    }
}
