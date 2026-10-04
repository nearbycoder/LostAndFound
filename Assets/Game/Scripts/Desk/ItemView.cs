using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace LostAndFound
{
    public enum ItemPlace { Storage, Held, Mat, Tray, Iron, Gone }

    /// <summary>
    /// One lost object on the desk. Root = resting position (model bottom-centre). The model sits under
    /// a pivot at its bounds centre so it rotates nicely in the hand. Hotspots are the model's HS_*
    /// empties; parts (lids, latches, keys) toggle with springs.
    /// </summary>
    public class ItemView : Interactable
    {
        public ObjectDef def;
        public Transform pivot;
        public GameObject model;
        public float radius = 0.1f;
        public Vector3 centerOffset;
        public ItemPlace place = ItemPlace.Storage;
        public Drawer drawer;
        public Transform shelfAnchor;
        public ItemTag tagView;
        public readonly Dictionary<string, Transform> hotspots = new();
        public readonly List<ItemPart> parts = new();
        BoxCollider pickCollider;
        bool meshCollidersBuilt;
        Spring jiggleSpring;
        Vector3 jiggleAxis = Vector3.forward;
        float humAmount, humTarget;
        AudioSource humSource;
        Renderer[] renderers;
        float frost;
        /// <summary>Brought to the window by a claimant (a chit, a certificate): taken away again after the case.</summary>
        public bool presented;
        readonly List<Transform> spinners = new();

        public override CursorKind Cursor => CursorKind.Grab;
        public override string Hint => place == ItemPlace.Tray ? $"{def.name} (on the tray)" : $"Pick up";
        public override bool Interactive => base.Interactive && place != ItemPlace.Held && place != ItemPlace.Iron && place != ItemPlace.Gone;
        public override Renderer[] HighlightRenderers => renderers ??= model != null ? model.GetComponentsInChildren<Renderer>() : new Renderer[0];

        public static ItemView Create(ObjectDef def, Transform parent)
        {
            var go = new GameObject("Item_" + def.id);
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<ItemView>();
            view.def = def;
            view.Build();
            return view;
        }

        void Build()
        {
            pivot = new GameObject("Pivot").transform;
            pivot.SetParent(transform, false);
            model = ModelLibrary.Spawn("Objects/" + def.ModelName, pivot);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            // centre the pivot on the model's bounds so it spins about its middle
            var b = LocalBounds();
            centerOffset = b.center;
            pivot.localPosition = centerOffset;
            model.transform.localPosition = -centerOffset;
            radius = Mathf.Max(0.02f, b.extents.magnitude);

            pickCollider = gameObject.AddComponent<BoxCollider>();
            pickCollider.center = b.center;
            pickCollider.size = b.size + Vector3.one * 0.01f;

            foreach (var t in ModelLibrary.Walk(model.transform).ToList())
            {
                if (!t.name.StartsWith("HS_")) continue;
                // HS_<id>__<Part>: the hotspot rides on a moving part
                int sep = t.name.IndexOf("__", System.StringComparison.Ordinal);
                string key = sep > 0 ? t.name.Substring(0, sep) : t.name;
                if (sep > 0)
                {
                    var part = ModelLibrary.Find(model.transform, t.name.Substring(sep + 2));
                    if (part != null) t.SetParent(part, true);
                }
                hotspots[key] = t;
            }
            foreach (var pd in def.parts)
            {
                // actions on the whole object (listen to the shell, shake the globe) have no node
                var t = string.IsNullOrEmpty(pd.node) ? model.transform : ModelLibrary.Find(model.transform, pd.node);
                if (t == null) { Debug.LogWarning($"[Item] {def.id}: missing part {pd.node}"); continue; }
                var part = t.gameObject.AddComponent<ItemPart>();
                part.Init(this, pd);
                parts.Add(part);
            }
            renderers = model.GetComponentsInChildren<Renderer>();
            // Spin_* children turn on their own (the watch that runs backwards)
            foreach (var t in ModelLibrary.Walk(model.transform))
                if (t.name.StartsWith("Spin_")) spinners.Add(t);
            if (def.trait == "frost") MakeFrosted();
            // hidden ink: UV_* children only show under Agnes's blue lamp
            foreach (var t in ModelLibrary.Walk(model.transform))
                if (t.name.StartsWith("UV_") && t.TryGetComponent<Renderer>(out var r)) uvRenderers.Add(r);
            foreach (var r in uvRenderers)
            {
                var m = new Material(r.sharedMaterial);
                MaterialLibrary.MakeUvInk(m);
                r.sharedMaterial = m;
                r.enabled = false;
            }
            Lamp.UVChanged += OnUV;
        }

        readonly List<Renderer> uvRenderers = new();

        /// <summary>Frosted objects: an icy cast over every surface, a faint cold glow, and a breath of mist.</summary>
        void MakeFrosted()
        {
            var ice = new Color(0.78f, 0.88f, 1f);
            foreach (var r in renderers)
            {
                if (r.name.StartsWith("UV_")) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !mats[i].HasProperty("_BaseColor")) continue;
                    var m = new Material(mats[i]);
                    var c = m.GetColor("_BaseColor");
                    if (c.a < 0.99f) continue;   // leave glass and frost alone
                    m.SetColor("_BaseColor", new Color(Mathf.Lerp(c.r, ice.r, 0.18f), Mathf.Lerp(c.g, ice.g, 0.18f), Mathf.Lerp(c.b, ice.b, 0.22f), c.a));
                    mats[i] = m;
                }
                r.sharedMaterials = mats;
            }
            var mist = new GameObject("FrostMist").AddComponent<FrostMist>();
            mist.transform.SetParent(pivot, false);
            mist.Init(radius);
        }

        void OnDestroy() => Lamp.UVChanged -= OnUV;

        public void RefreshUV() => OnUV(Lamp.I != null && Lamp.I.UV);

        void OnUV(bool on)
        {
            bool show = on && place == ItemPlace.Held;
            foreach (var r in uvRenderers) if (r != null) r.enabled = show;
        }

        /// <summary>Details whose world has changed (after the ring) swap their texture to the _alt version.</summary>
        public void ApplyStory(StoryState state)
        {
            foreach (var d in def.details)
            {
                if (string.IsNullOrEmpty(d.altTex) || !state.Check(d.altIf)) continue;
                var alt = MaterialLibrary.ItemTexture(d.altTex + "_alt");
                if (alt == null) continue;
                foreach (var r in model.GetComponentsInChildren<Renderer>())
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                        if (mats[i] != null && mats[i].name.StartsWith("tex_" + d.altTex))
                        {
                            var m = new Material(mats[i]);
                            m.SetTexture("_BaseMap", alt);
                            mats[i] = m;
                        }
                    r.sharedMaterials = mats;
                }
            }
        }

        Bounds LocalBounds()
        {
            var rs = model.GetComponentsInChildren<MeshFilter>();
            bool any = false;
            var b = new Bounds();
            foreach (var mf in rs)
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                var m = transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = m.MultiplyPoint3x4(c);
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                    else b.Encapsulate(p);
                }
            }
            return any ? b : new Bounds(Vector3.up * 0.03f, Vector3.one * 0.06f);
        }

        /// <summary>Mesh colliders on every part so inspect raycasts (parts, occlusion) are exact.</summary>
        public void EnsureMeshColliders()
        {
            if (meshCollidersBuilt) return;
            meshCollidersBuilt = true;
            ModelLibrary.AddMeshColliders(model);
        }

        public void SetPickable(bool on)
        {
            if (pickCollider != null) pickCollider.enabled = on;
            foreach (var c in model.GetComponentsInChildren<MeshCollider>(true)) c.enabled = !on;
        }

        public override void OnHover(bool on)
        {
            if (tagView != null) tagView.SetLifted(on);
            if (on) UIRoot.I?.tagCard.Show(this);
            else UIRoot.I?.tagCard.Hide(this);
        }

        public override void OnClick() => Desk.I.PickUp(this);

        public void Jiggle(float amount)
        {
            jiggleAxis = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;
            jiggleSpring.velocity += amount * 2.5f;
        }

        public void SetHum(float target) => humTarget = target;
        public float Hum => humAmount;

        public void SetFrost(float amount) => frost = amount;

        void Update()
        {
            foreach (var sp in spinners) sp.localRotation *= Quaternion.AngleAxis(-70f * Time.deltaTime, Vector3.up);
            // jiggle: a small tilt that springs back (only while resting)
            jiggleSpring.Step(0f, 3.5f, 0.25f, Time.deltaTime);
            if (place != ItemPlace.Held)
                pivot.localRotation = Quaternion.AngleAxis(jiggleSpring.value * 6f, jiggleAxis);

            humAmount = MathX.Damp(humAmount, humTarget, 3f, Time.deltaTime);
            if (humAmount > 0.01f)
            {
                float t = Time.time;
                float glow = (0.55f + 0.45f * Mathf.Sin(t * 6.3f)) * humAmount;
                Highlighter.Set(HighlightRenderers, new Color(1f, 0.72f, 0.28f) * glow * 0.45f);
                if (place != ItemPlace.Held)
                    pivot.localPosition = centerOffset + new Vector3(Mathf.Sin(t * 71f), Mathf.Sin(t * 53f), Mathf.Sin(t * 64f)) * 0.0015f * humAmount;
                if (humSource == null) humSource = AudioDirector.Loop("hum", transform, 0f);
                if (humSource != null) humSource.volume = humAmount * 0.55f * AudioDirector.SfxVolume;
            }
            else
            {
                if (humSource != null) humSource.volume = 0f;
                if (place != ItemPlace.Held) pivot.localPosition = centerOffset;
            }
        }

        /// <summary>World-space centre of the model (for flying, focusing).</summary>
        public Vector3 Center => pivot.position;

        public Transform Hotspot(DetailDef d) => hotspots.TryGetValue(d.NodeName, out var t) ? t : null;
    }

    /// <summary>A moving part of an object (lid, flap, latch, wind key). Click to toggle while inspecting.</summary>
    public class ItemPart : MonoBehaviour
    {
        public ItemView owner;
        public PartDef def;
        public bool open;
        Quaternion closedRot;
        Vector3 closedPos;
        Spring spring;
        Vector3 axis;

        public void Init(ItemView owner, PartDef def)
        {
            this.owner = owner;
            this.def = def;
            closedRot = transform.localRotation;
            closedPos = transform.localPosition;
            axis = def.axis switch { "y" => Vector3.up, "z" => Vector3.forward, "-x" => Vector3.left, "-y" => Vector3.down, "-z" => Vector3.back, _ => Vector3.right };
        }

        public void Toggle()
        {
            open = !open;
            string s = string.IsNullOrEmpty(def.sound) ? "hinge" : def.sound;
            if (def.kind == "spin") AudioDirector.Play("wind", 0.6f);
            else if (def.kind is "shake" or "listen" or "play") AudioDirector.Play(s, 0.8f);
            else AudioDirector.Play(open ? s + "_open" : s + "_close", 0.7f, Random.Range(0.94f, 1.06f));
        }

        void Update()
        {
            if (def.kind is "shake" or "listen" or "play") return;   // the inspect controller animates these
            float target = open ? 1f : 0f;
            if (def.kind == "spin" && open)
            {
                // wind keys keep turning a little then stop
                spring.Step(target, 1.2f, 1f, Time.deltaTime);
            }
            else spring.Step(target, 3.4f, 0.6f, Time.deltaTime);
            float v = spring.value;
            switch (def.kind)
            {
                case "slide":
                    transform.localPosition = closedPos + axis * def.amount * v;
                    break;
                default:
                    transform.localRotation = closedRot * Quaternion.AngleAxis(def.amount * v, axis);
                    break;
            }
        }
    }

    /// <summary>The manila intake tag that sits beside an object in storage.</summary>
    public class ItemTag : MonoBehaviour
    {
        TextMeshPro number;
        Quaternion restRot;
        Vector3 restPos;
        float lift, liftTarget;

        public static ItemTag Create(ItemView item, int tagNo)
        {
            var go = new GameObject("Tag");
            go.transform.SetParent(item.transform, false);
            var tag = go.AddComponent<ItemTag>();
            tag.Build(item, tagNo);
            return tag;
        }

        void Build(ItemView item, int tagNo)
        {
            var card = new GameObject("Card");
            card.transform.SetParent(transform, false);
            var mf = card.AddComponent<MeshFilter>();
            mf.sharedMesh = ProcMesh.TagMesh(0.042f, 0.072f, 0.0008f);
            var mr = card.AddComponent<MeshRenderer>();
            mr.sharedMaterial = DeskMaterials.Tag;
            var eyelet = new GameObject("Eyelet");
            eyelet.transform.SetParent(transform, false);
            eyelet.transform.localPosition = new Vector3(0f, 0.0012f, 0.028f);
            var emf = eyelet.AddComponent<MeshFilter>();
            emf.sharedMesh = ProcMesh.Ring(0.0055f, 0.0028f, 0.001f, 16);
            eyelet.AddComponent<MeshRenderer>().sharedMaterial = MaterialLibrary.Get("brass_C9A15A");

            number = Text.World(transform, $"No. {tagNo:00}", Fonts.Hand, 0.012f, DeskMaterials.InkColor);
            number.transform.localPosition = new Vector3(0f, 0.0011f, 0.004f);
            number.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            number.rectTransform.sizeDelta = new Vector2(0.04f, 0.02f);
            var line = Text.World(transform, item.def.tag.where, Fonts.Hand, 0.0062f, DeskMaterials.InkColor * new Color(1, 1, 1, 0.85f));
            line.transform.localPosition = new Vector3(0f, 0.0011f, -0.014f);
            line.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            line.rectTransform.sizeDelta = new Vector2(0.036f, 0.02f);
            line.textWrappingMode = TextWrappingModes.Normal;
        }

        public void PlaceBeside(Vector3 localPos, float yaw)
        {
            restPos = localPos;
            restRot = Quaternion.Euler(0f, yaw, 0f);
            transform.localPosition = restPos;
            transform.localRotation = restRot;
        }

        public void SetLifted(bool on) => liftTarget = on ? 1f : 0f;

        void Update()
        {
            lift = MathX.Damp(lift, liftTarget, 12f, Time.deltaTime);
            transform.localPosition = restPos + Vector3.up * lift * 0.012f;
            transform.localRotation = restRot * Quaternion.Euler(-lift * 18f, 0f, lift * 4f);
        }
    }

    /// <summary>A few soft puffs of cold vapour curling off a frosted object.</summary>
    public class FrostMist : MonoBehaviour
    {
        struct Puff { public Transform t; public float age, life; public Vector3 vel; }
        readonly List<Puff> puffs = new();
        Material mat;
        float radius;

        public void Init(float r)
        {
            radius = r;
            mat = MaterialLibrary.Make(new Color(0.9f, 0.95f, 1f, 0f), SoftDot(), 0f, true);
            for (int i = 0; i < 6; i++)
            {
                var go = new GameObject("Puff");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = ProcMesh.QuadXY(1f, 1f);
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = new Material(mat);
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var p = new Puff { t = go.transform, life = Random.Range(2.2f, 3.4f) };
                p.age = p.life * i / 6f;
                Respawn(ref p, false);
                puffs.Add(p);
            }
        }

        static Texture2D softDot;

        /// <summary>A white disc that fades to nothing at the edge.</summary>
        static Texture2D SoftDot()
        {
            if (softDot != null) return softDot;
            const int N = 64;
            softDot = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(N / 2f - 0.5f, N / 2f - 0.5f)) / (N / 2f);
                    softDot.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - d), 2f)));
                }
            softDot.Apply();
            return softDot;
        }

        void Respawn(ref Puff p, bool reset = true)
        {
            if (reset) p.age = 0f;
            p.t.localPosition = new Vector3(Random.Range(-1f, 1f), Random.Range(0f, 0.6f), Random.Range(-1f, 1f)) * radius * 0.6f;
            p.vel = new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(0.25f, 0.5f), Random.Range(-0.2f, 0.2f)) * radius * 0.35f;
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            for (int i = 0; i < puffs.Count; i++)
            {
                var p = puffs[i];
                p.age += Time.deltaTime;
                if (p.age > p.life) Respawn(ref p);
                float k = p.age / p.life;
                p.t.localPosition += p.vel * Time.deltaTime;
                float size = radius * Mathf.Lerp(0.5f, 1.3f, k);
                p.t.localScale = new Vector3(size, size, size) / Mathf.Max(0.001f, transform.lossyScale.x);
                if (cam != null) p.t.rotation = Quaternion.LookRotation(p.t.position - cam.transform.position);
                var r = p.t.GetComponent<MeshRenderer>();
                r.sharedMaterial.SetColor("_BaseColor", new Color(0.9f, 0.95f, 1f, 0.16f * Mathf.Sin(k * Mathf.PI)));
                puffs[i] = p;
            }
        }
    }
}
