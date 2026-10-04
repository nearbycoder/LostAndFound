using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LostAndFound
{
    /// <summary>
    /// The booth and everything on it. Builds the environment from the Blender models, wires the
    /// drawers and anchors, and owns where every object is: in storage, in your hand, on the mat,
    /// on the counter tray or locked in the Iron Drawer.
    /// </summary>
    public class Desk : MonoBehaviour
    {
        public static Desk I { get; private set; }

        public Transform booth, concourse;
        public readonly Dictionary<string, Drawer> drawers = new();
        public Drawer iron;
        public readonly Dictionary<string, Transform> shelfSlots = new();
        public Transform tray;
        public readonly List<Vector3> matSpots = new()
        {
            new Vector3(0.30f, 0.762f, 0.58f), new Vector3(0.12f, 0.762f, 0.66f), new Vector3(0.46f, 0.762f, 0.50f),
        };
        public readonly Dictionary<string, ItemView> items = new();
        readonly List<ItemView> onMat = new();
        public ItemView OnTray { get; private set; }
        public DeskProps props;
        int tagCounter;

        void Awake() => I = this;

        // ------------------------------------------------------------------ build

        public void Build()
        {
            booth = ModelLibrary.Spawn("Booth", transform).transform;
            concourse = ModelLibrary.Spawn("Concourse", transform).transform;
            foreach (var r in concourse.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            foreach (var t in ModelLibrary.Walk(booth).ToList())
            {
                if (t.GetComponent<MeshFilter>() != null && !t.name.StartsWith("Drawer_") && t.name != "IronDrawer")
                {
                    var mc = t.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = t.GetComponent<MeshFilter>().sharedMesh;
                    t.gameObject.AddComponent<RayBlocker>();
                }
                if (t.name.StartsWith("Shelf_") && t.GetComponent<MeshFilter>() == null) shelfSlots[t.name] = t;
                if (t.name == "TRAY") tray = t;
                if (t.name == "Glass")
                {
                    foreach (var r in t.GetComponentsInChildren<Renderer>())
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    Destroy(t.GetComponent<MeshCollider>());
                    Destroy(t.GetComponent<RayBlocker>());
                }
            }
            foreach (var letter in new[] { "A", "B", "C", "D", "E", "F" })
            {
                var t = ModelLibrary.Find(booth, "Drawer_" + letter);
                if (t == null) continue;
                var d = SetupDrawer(t, letter, Vector3.right, 0.25f, false);
                drawers[letter] = d;
                d.label = Text.World(ModelLibrary.Find(t, $"Drawer_{letter}_LABEL") ?? t, letter, Fonts.Hand, 0.03f, DeskMaterials.InkColor);
                d.label.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
                d.label.transform.localPosition = new Vector3(0.0005f, 0f, 0f);
                d.label.rectTransform.sizeDelta = new Vector2(0.085f, 0.04f);
            }
            var ironT = ModelLibrary.Find(booth, "IronDrawer");
            if (ironT != null)
            {
                iron = SetupDrawer(ironT, "Iron", Vector3.left, 0.22f, true);
                iron.locked = true;
            }

            props = gameObject.AddComponent<DeskProps>();
            props.Build(this);
        }

        Drawer SetupDrawer(Transform t, string letter, Vector3 dir, float travel, bool isIron)
        {
            var d = t.gameObject.AddComponent<Drawer>();
            d.letter = letter;
            d.slideDir = dir;
            d.travel = travel;
            d.isIron = isIron;
            // anchors are exported under the booth root; adopt them so they slide with the drawer
            string prefix = isIron ? "IronDrawer" : "Drawer_" + letter;
            foreach (var suffix in new[] { "_FLOOR", "_LABEL", "_KEYHOLE" })
            {
                var a = ModelLibrary.Find(booth, prefix + suffix);
                if (a != null) a.SetParent(t, true);
            }
            d.floor = ModelLibrary.Find(t, prefix + "_FLOOR");
            d.Init();
            // click target: the front panel only (thin box at the drawer's origin)
            var bc = t.gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(isIron ? -0.01f : 0.008f, 0f, 0f);
            bc.size = isIron ? new Vector3(0.03f, 0.26f, 0.40f) : new Vector3(0.03f, 0.25f, 0.29f);
            return d;
        }

        // ------------------------------------------------------------------ items

        public void ClearItems()
        {
            foreach (var it in items.Values) if (it != null) Destroy(it.gameObject);
            items.Clear();
            onMat.Clear();
            OnTray = null;
            foreach (var d in drawers.Values) { d.SetOpen(false, true); foreach (var c in d.Contents.ToList()) d.Remove(c); }
            tagCounter = 0;
        }

        /// <summary>Spawn every object in storage for this day (and sealed ones into the Iron Drawer).</summary>
        public void SpawnItems(ContentDb db, int day, StoryState state)
        {
            ClearItems();
            var byDrawer = new Dictionary<string, List<ItemView>>();
            foreach (var def in db.root.objects)
            {
                if (def.storage == "desk") continue;
                bool sealedHere = state.ObjectLocation(def.id) == "sealed";
                if (!state.InStorage(def, day) && !sealedHere) continue;
                var view = ItemView.Create(def, transform);
                view.ApplyStory(state);
                items[def.id] = view;
                tagCounter++;
                if (!sealedHere) view.tagView = ItemTag.Create(view, TagNumber(db, def));
                if (sealedHere)
                {
                    view.place = ItemPlace.Iron;
                    view.transform.SetParent(iron.transform, false);
                    view.transform.localPosition = iron.floor.localPosition + new Vector3(Random.Range(-0.08f, 0.08f), 0f, Random.Range(-0.1f, 0.1f));
                    view.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    view.SetPickable(false);
                    continue;
                }
                if (drawers.ContainsKey(def.storage))
                {
                    if (!byDrawer.TryGetValue(def.storage, out var list)) byDrawer[def.storage] = list = new List<ItemView>();
                    list.Add(view);
                }
                else PutOnShelf(view);
            }
            foreach (var kv in byDrawer)
            {
                var d = drawers[kv.Key];
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    var v = kv.Value[i];
                    d.Add(v);
                    v.drawer = d;
                    v.place = ItemPlace.Storage;
                    v.transform.SetParent(d.transform, false);
                    v.transform.localPosition = d.SlotPosition(i, kv.Value.Count);
                    v.transform.localRotation = Quaternion.Euler(0f, 90f + Random.Range(-12f, 12f), 0f);
                    v.tagView?.PlaceBeside(new Vector3(0f, 0.001f, -0.01f) + v.transform.InverseTransformDirection(d.transform.TransformDirection(new Vector3(0, 0, 0.07f))) * 0f + new Vector3(0.075f, 0f, 0f), Random.Range(-20f, 20f));
                }
            }
        }

        static int TagNumber(ContentDb db, ObjectDef def) => System.Array.IndexOf(db.root.objects, def) + 1;

        void PutOnShelf(ItemView v)
        {
            v.drawer = null;
            v.place = ItemPlace.Storage;
            shelfSlots.TryGetValue(v.def.storage ?? "", out var anchor);
            if (anchor == null) anchor = shelfSlots.Values.FirstOrDefault();
            v.shelfAnchor = anchor;
            v.transform.SetParent(anchor, false);
            v.transform.localPosition = Vector3.zero;
            v.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            v.tagView?.PlaceBeside(new Vector3(0.0f, 0.001f, -0.12f), Random.Range(-25f, 25f));
        }

        public void PickUp(ItemView item) => InspectController.I.Begin(item);

        /// <summary>Unparent an item (keeping its world pose) and forget where it was.</summary>
        public void Detach(ItemView item)
        {
            if (item.drawer != null) item.drawer.Remove(item);
            onMat.Remove(item);
            if (OnTray == item) OnTray = null;
            item.transform.SetParent(transform, true);
            if (item.tagView != null) item.tagView.gameObject.SetActive(false);
        }

        public IEnumerator Place(ItemView item, ItemPlace to)
        {
            var tr = item.transform;
            item.SetPickable(true);
            switch (to)
            {
                case ItemPlace.Tray:
                    if (OnTray != null && OnTray != item) yield return Place(OnTray, ItemPlace.Mat);
                    OnTray = item;
                    item.place = ItemPlace.Tray;
                    yield return FlyTo(item, tray.position, Quaternion.Euler(0f, Random.Range(-8f, 8f), 0f), 1f, 0.5f);
                    AudioDirector.PlayMaterial(item.def.sound, "put", 0.8f);
                    AudioDirector.Play("tray_clink", 0.4f, Random.Range(0.95f, 1.08f));
                    item.Jiggle(0.6f);
                    Director.I?.OnTrayChanged(item);
                    break;
                case ItemPlace.Mat:
                    {
                        int slot = 0;
                        for (int i = 0; i < matSpots.Count; i++) if (!onMat.Any(m => Vector3.Distance(m.transform.position, matSpots[i]) < 0.05f)) { slot = i; break; }
                        onMat.Add(item);
                        item.place = ItemPlace.Mat;
                        yield return FlyTo(item, matSpots[slot], Quaternion.Euler(0f, Random.Range(-15f, 15f), 0f), MatScale(item), 0.45f);
                        AudioDirector.PlayMaterial(item.def.sound, "put", 0.8f);
                        item.Jiggle(0.7f);
                        break;
                    }
                case ItemPlace.Storage:
                    yield return ReturnToStorage(item);
                    break;
                case ItemPlace.Iron:
                    yield return SealRoutine(item);
                    break;
            }
        }

        /// <summary>Items on the mat/tray are shown at a reduced size if they're large (suitcase on a desk).</summary>
        static float MatScale(ItemView item) => item.radius > 0.16f ? Mathf.Clamp(0.16f / item.radius, 0.35f, 1f) : 1f;

        IEnumerator FlyTo(ItemView item, Vector3 restPos, Quaternion restRot, float scale, float duration)
        {
            var tr = item.transform;
            float s0 = tr.localScale.x;
            Vector3 p0 = tr.position;
            Quaternion r0 = tr.rotation;
            Vector3 mid = (p0 + restPos) * 0.5f + Vector3.up * 0.08f;
            yield return Tween.Run(duration, k =>
            {
                Vector3 a = Vector3.LerpUnclamped(p0, mid, k), b = Vector3.LerpUnclamped(mid, restPos, k);
                tr.position = Vector3.LerpUnclamped(a, b, k);
                tr.rotation = Quaternion.SlerpUnclamped(r0, restRot, k);
                tr.localScale = Vector3.one * Mathf.Lerp(s0, scale, k);
            }, Ease.InOutCubic);
        }

        public IEnumerator ReturnToStorage(ItemView item)
        {
            Detach(item);
            var tr = item.transform;
            item.place = ItemPlace.Storage;
            var d = drawers.TryGetValue(item.def.storage ?? "", out var dd) ? dd : null;
            if (d != null)
            {
                bool wasOpen = d.IsOpen;
                d.SetOpen(true, true);
                d.Add(item);
                item.drawer = d;
                int i = d.Contents.Count - 1;
                Vector3 local = d.SlotPosition(i, Mathf.Max(2, d.Contents.Count));
                Vector3 world = d.transform.TransformPoint(local + d.slideDir * d.travel);
                yield return FlyTo(item, world, d.transform.rotation * Quaternion.Euler(0f, 90f, 0f), 1f, 0.55f);
                item.transform.SetParent(d.transform, true);
                item.transform.localPosition = local;
                AudioDirector.PlayMaterial(item.def.sound, "put", 0.6f);
                if (!wasOpen) { yield return new WaitForSeconds(0.25f); d.SetOpen(false); }
            }
            else
            {
                shelfSlots.TryGetValue(item.def.storage ?? "", out var anchor);
                anchor ??= shelfSlots.Values.First();
                yield return FlyTo(item, anchor.position, anchor.rotation * Quaternion.Euler(0f, -90f, 0f), 1f, 0.55f);
                item.transform.SetParent(anchor, true);
                AudioDirector.PlayMaterial(item.def.sound, "put", 0.6f);
            }
            if (item.tagView != null) item.tagView.gameObject.SetActive(true);
        }

        /// <summary>Swivel to the shelf, open the Iron Drawer, drop the item in, slam and lock.</summary>
        public IEnumerator SealRoutine(ItemView item)
        {
            Detach(item);
            item.place = ItemPlace.Iron;
            item.SetPickable(false);
            var prevView = CameraRig.I.view;
            CameraRig.I.allowTurn = false;
            CameraRig.I.SetView(View.Shelf);
            yield return new WaitForSeconds(0.45f);
            AudioDirector.Play("key_turn", 0.8f);
            yield return new WaitForSeconds(0.25f);
            iron.locked = false;
            iron.SetOpen(true);
            yield return new WaitForSeconds(0.45f);
            Vector3 dest = iron.transform.TransformPoint(iron.floor.localPosition + iron.slideDir * iron.travel);
            yield return FlyTo(item, dest + Vector3.up * 0.02f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), Mathf.Min(1f, 0.12f / item.radius), 0.6f);
            item.transform.SetParent(iron.transform, true);
            AudioDirector.PlayMaterial(item.def.sound, "put", 0.7f);
            yield return new WaitForSeconds(0.3f);
            iron.SetOpen(false);
            yield return new WaitForSeconds(0.32f);
            AudioDirector.Play("iron_slam", 1f);
            CameraRig.I.Shake(0.5f);
            PostFX.I?.Pulse(new Color(0.45f, 0.4f, 0.7f), 0.5f);
            yield return new WaitForSeconds(0.2f);
            AudioDirector.Play("lock_clunk", 0.9f);
            iron.locked = true;
            yield return new WaitForSeconds(0.45f);
            CameraRig.I.SetView(prevView == View.Shelf ? View.Counter : prevView);
            CameraRig.I.allowTurn = true;
        }

        public void OnDrawerToggled(Drawer d, bool open)
        {
            if (d.isIron) return;
            // lean up and over the most recently opened drawer so you can see inside it
            var target = open ? d : drawers.Values.LastOrDefault(x => x.IsOpen);
            if (target == null || CameraRig.I.view != View.Cabinet) { if (!drawers.Values.Any(x => x.IsOpen)) CameraRig.I.ClearFocus(); return; }
            Vector3 floor = target.transform.TransformPoint((target.floor != null ? target.floor.localPosition : Vector3.zero) + target.slideDir * target.travel);
            Vector3 front = target.transform.TransformPoint(target.slideDir * target.travel);
            Vector3 eye = new Vector3(front.x + 0.36f, front.y + 0.5f, front.z + 0.04f);
            CameraRig.I.Focus(floor + Vector3.up * 0.02f + Vector3.right * 0.03f, eye, 50f);
        }

        public void CloseAllDrawers()
        {
            foreach (var d in drawers.Values) d.SetOpen(false, !d.IsOpen);
            CameraRig.I?.ClearFocus();
        }

        /// <summary>Gather stray items from the mat and tray back into storage (end of a case).</summary>
        public IEnumerator Tidy()
        {
            var loose = onMat.ToList();
            if (OnTray != null) loose.Add(OnTray);
            foreach (var it in loose)
            {
                StartCoroutine(ReturnToStorage(it));
                yield return new WaitForSeconds(0.12f);
            }
            if (loose.Count > 0) yield return new WaitForSeconds(0.7f);
        }

        /// <summary>Slide the tray item under the glass to the commuter, who takes it.</summary>
        public IEnumerator HandOver(ItemView item, Vector3 handPos)
        {
            Detach(item);
            item.place = ItemPlace.Gone;
            item.SetPickable(false);
            var tr = item.transform;
            Vector3 under = tray.position + new Vector3(0f, 0f, 0.16f);
            AudioDirector.Play("tray_slide", 0.7f);
            yield return Tween.Move(tr, under, 0.4f, Ease.InOutCubic);
            yield return FlyTo(item, handPos, tr.rotation * Quaternion.Euler(0f, 0f, 15f), tr.localScale.x, 0.45f);
            yield return Tween.Scale(tr, Vector3.zero, 0.25f, Ease.InBack);
            item.gameObject.SetActive(false);
        }

        /// <summary>Objects hum when their owner is at the window (louder while that person speaks).</summary>
        public void SetHums(IEnumerable<string> present, float amount, bool additive = false)
        {
            var set = new HashSet<string>(present);
            var drawerHum = new Dictionary<Drawer, float>();
            foreach (var it in items.Values)
            {
                if (it == null) continue;
                bool hums = it.place != ItemPlace.Iron && it.place != ItemPlace.Gone && set.Any(id => Rules.Hums(it.def, id));
                if (additive && !hums) continue;
                it.SetHum(hums ? amount : 0f);
                if (it.drawer != null)
                    drawerHum[it.drawer] = Mathf.Max(drawerHum.TryGetValue(it.drawer, out var v) ? v : 0f, hums ? amount : 0f);
            }
            foreach (var d in drawers.Values)
            {
                if (additive && !drawerHum.ContainsKey(d)) continue;
                d.SetHum(drawerHum.TryGetValue(d, out var v) ? v : 0f);
            }
        }
    }
}
