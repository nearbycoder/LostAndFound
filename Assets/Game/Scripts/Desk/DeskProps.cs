using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace LostAndFound
{
    /// <summary>Places the desk props (lamp, stamps, slip, printer, bell, calendar, photographs...)
    /// and wires their behaviours. Positions are in booth space (player eye at 0, 1.26, -0.08).</summary>
    public partial class DeskProps : MonoBehaviour
    {
        public Lamp lamp;
        public Bell bell;
        public TicketPrinter printer;
        public ClaimSlip slip;
        public readonly List<Stamp> stamps = new();
        public readonly List<PhotoFrame> photos = new();
        /// <summary>The photograph of you at this desk that arrives off the last train on Monday.</summary>
        public PhotoFrame polaroid;
        static readonly Vector3 PolaroidPos = new(-0.43f, 0.7628f, 0.70f);
        static readonly Quaternion PolaroidRot = Quaternion.Euler(0f, 24f, 0f);
        public DeskCalendar calendar;
        public DeskRulesCard rulesCard;
        public Light lampLight, boothLight;

        GameObject Prop(string name, Vector3 pos, float yaw, Transform parent)
        {
            GameObject go;
            if (ModelLibrary.Exists("Props/" + name)) go = ModelLibrary.Spawn("Props/" + name, parent);
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name + " (placeholder)";
                go.transform.SetParent(parent, false);
                go.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);
                Destroy(go.GetComponent<Collider>());
                go.GetComponent<Renderer>().sharedMaterial = MaterialLibrary.Get("brass_C9A15A");
            }
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }

        static void AddBoxCollider(GameObject go)
        {
            var b = ModelLibrary.Bounds(go);
            var bc = go.AddComponent<BoxCollider>();
            bc.center = go.transform.InverseTransformPoint(b.center);
            var s = go.transform.InverseTransformVector(b.size);
            bc.size = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        }

        /// <summary>A block for the rubber and wooden base and a capsule for the handle and knob. One bounding
        /// box reached up into the empty air beside the knob, and from the chair the stamp in front's box
        /// covered part of the knob behind it, so a click on the green knob could pick up the red stamp.</summary>
        static void AddStampColliders(GameObject go)
        {
            var b = ModelLibrary.Bounds(go);
            var t = go.transform;
            Vector3 c = t.InverseTransformPoint(b.center);
            var s = t.InverseTransformVector(b.size);
            s = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            float bottom = c.y - s.y * 0.5f, baseH = s.y * 0.28f;
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(c.x, bottom + baseH * 0.5f, c.z);
            box.size = new Vector3(s.x, baseH, s.z);
            var cap = go.AddComponent<CapsuleCollider>();
            cap.direction = 1;
            cap.radius = t.InverseTransformVector(Vector3.right * 0.021f).magnitude;   // the knob's radius is 2 cm
            cap.height = s.y - baseH + 0.004f;
            cap.center = new Vector3(c.x, bottom + baseH + (s.y - baseH) * 0.5f, c.z);
        }

        public void Build(Desk desk)
        {
            var root = desk.transform;

            // --- lamp (key light)
            var lampGo = Prop("Lamp", new Vector3(-0.60f, 0.76f, 0.80f), 25f, root);
            AddBoxCollider(lampGo);
            lamp = lampGo.AddComponent<Lamp>();
            var lt = new GameObject("LampLight").AddComponent<Light>();
            lt.transform.SetParent(lampGo.transform, false);
            lt.transform.position = new Vector3(-0.50f, 1.17f, 0.72f);
            lt.transform.rotation = Quaternion.LookRotation(new Vector3(0.05f, 0.76f, 0.58f) - lt.transform.position);
            lt.type = LightType.Spot;
            lt.spotAngle = 105f;
            lt.innerSpotAngle = 40f;
            lt.range = 3.2f;
            lt.intensity = 4.2f;
            lt.color = new Color(1f, 0.78f, 0.52f);
            lt.shadows = LightShadows.Soft;
            lt.shadowStrength = 0.85f;
            lt.shadowBias = 0.02f;
            lt.shadowNormalBias = 0.2f;
            lampLight = lt;
            lamp.Init(lt);
            lamp.bulb = ModelLibrary.Find(lampGo.transform, "Bulb")?.GetComponent<Renderer>();

            // --- stamps in their rack
            var rack = Prop("StampRack", new Vector3(-0.235f, 0.76f, 0.56f), 78f, root);
            var kinds = new[] { (Verdict.Return, "Stamp_Return", -0.055f), (Verdict.Refuse, "Stamp_Refuse", 0f), (Verdict.Seal, "Stamp_Seal", 0.055f) };
            foreach (var (kind, model, dx) in kinds)
            {
                Vector3 p = rack.transform.TransformPoint(new Vector3(dx, 0.012f, 0f));
                var sg = Prop(model, p, 8f, root);
                sg.transform.rotation = Quaternion.Euler(0f, 8f, 0f);
                AddStampColliders(sg);
                var st = sg.AddComponent<Stamp>();
                st.kind = kind;
                st.rackPos = sg.transform.position;
                st.rackRot = sg.transform.rotation;
                stamps.Add(st);
            }

            // --- claim slip
            slip = ClaimSlip.Create(root, new Vector3(0.0f, 0.7625f, 0.52f), 3f);

            // --- printer + spike
            var pr = Prop("Printer", new Vector3(0.56f, 0.76f, 0.76f), -24f, root);
            printer = pr.AddComponent<TicketPrinter>();
            printer.slot = ModelLibrary.Find(pr.transform, "PRINTER_SLOT");
            if (printer.slot == null)
            {
                printer.slot = new GameObject("PRINTER_SLOT").transform;
                printer.slot.SetParent(pr.transform, false);
                printer.slot.localPosition = new Vector3(0f, 0.05f, -0.06f);
                printer.slot.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }
            printer.lever = ModelLibrary.Find(pr.transform, "PrinterLever");
            var spike = Prop("Spike", new Vector3(0.66f, 0.76f, 0.47f), 0f, root);
            printer.spike = spike.transform;

            // --- bell
            var bellGo = Prop("Bell", new Vector3(0.30f, 0.76f, 0.84f), 0f, root);
            AddBoxCollider(bellGo);
            bell = bellGo.AddComponent<Bell>();
            bell.plunger = ModelLibrary.Find(bellGo.transform, "BellPlunger");

            // --- calendar and the rules card
            var cal = Prop("Calendar", new Vector3(-0.30f, 0.76f, 0.86f), 12f, root);
            calendar = cal.AddComponent<DeskCalendar>();
            calendar.Init(cal.transform);
            rulesCard = DeskRulesCard.Create(root, new Vector3(-0.45f, 0.7612f, 0.68f), -16f);

            // --- photographs (the trailer moment changes every one of them)
            var frames = new (string model, Vector3 pos, float yaw, string id)[]
            {
                ("Frame_Tall", new Vector3(-0.74f, 0.76f, 0.58f), 62f, "staff1921"),
                ("Frame_Wide", new Vector3(-0.50f, 0.76f, 0.92f), 18f, "retirement"),
                ("Frame_Small", new Vector3(0.16f, 0.76f, 0.855f), -8f, "mum"),   // clear of the counter tray
                ("Frame_Oval", new Vector3(0.74f, 0.76f, 0.56f), -60f, "platform9"),
            };
            foreach (var f in frames)
            {
                var fg = Prop(f.model, f.pos, f.yaw, root);
                AddBoxCollider(fg);
                var pf = fg.AddComponent<PhotoFrame>();
                pf.Init(f.id, ModelLibrary.Find(fg.transform, "PHOTO"));
                photos.Add(pf);
            }

            // --- the Polaroid (hidden until it arrives at the end of Monday)
            var pol = new GameObject("Polaroid");
            pol.transform.SetParent(root, false);
            pol.transform.position = PolaroidPos;
            pol.transform.rotation = PolaroidRot;
            var card = new GameObject("PHOTO");
            card.transform.SetParent(pol.transform, false);
            card.AddComponent<MeshFilter>().sharedMesh = ProcMesh.QuadXZ(0.088f, 0.107f);
            card.AddComponent<MeshRenderer>();
            var pbc = pol.AddComponent<BoxCollider>();
            pbc.size = new Vector3(0.09f, 0.01f, 0.11f);
            polaroid = pol.AddComponent<PhotoFrame>();
            polaroid.Init("polaroid", card.transform);
            photos.Add(polaroid);
            pol.SetActive(false);

            // --- quiet set dressing
            Prop("TeaCup", new Vector3(0.42f, 0.76f, 0.40f), 30f, root);
            Prop("Inkwell", new Vector3(-0.18f, 0.76f, 0.80f), 0f, root);
            Prop("Ledger", new Vector3(0.62f, 0.76f, 0.40f) + new Vector3(-0.06f, 0f, 0.0f), -14f, root);

            // --- booth practical over the window: lights the commuter's face warmly
            var bl = new GameObject("BoothLight").AddComponent<Light>();
            bl.transform.SetParent(root, false);
            // just under the lintel, so the window frame doesn't shadow their faces
            bl.transform.position = new Vector3(0.12f, 1.86f, 0.86f);
            bl.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 1.25f, 1.6f) - bl.transform.position);
            bl.type = LightType.Spot;
            bl.spotAngle = 88f;
            bl.innerSpotAngle = 30f;
            bl.range = 4f;
            bl.intensity = 2.8f;
            bl.color = new Color(1f, 0.83f, 0.62f);
            bl.shadows = LightShadows.None;
            boothLight = bl;
        }
    }

    /// <summary>A framed photograph whose picture can change (with a ripple dissolve).</summary>
    public class PhotoFrame : Interactable
    {
        public string photoId;
        public Renderer photo;
        Material mat;
        public override CursorKind Cursor => CursorKind.Look;
        public override string Hint => Director.I?.PhotoCaption(photoId);

        public void Init(string id, Transform anchor)
        {
            photoId = id;
            if (anchor == null) return;
            photo = anchor.GetComponent<Renderer>();
            if (photo == null)
            {
                var mf = anchor.gameObject.AddComponent<MeshFilter>();
                mf.sharedMesh = ProcMesh.QuadXZ(1f, 1f);
                photo = anchor.gameObject.AddComponent<MeshRenderer>();
            }
            mat = MaterialLibrary.Make(Color.white, null, 0.35f);
            photo.sharedMaterial = mat;
            photo.receiveShadows = false;   // the lamp threw the calendar's shadow straight across one picture
        }

        static readonly Color Glow = new(0.2f, 0.19f, 0.17f);

        public void SetPhoto(Texture2D tex)
        {
            if (mat == null) return;
            var t = tex != null ? tex : Texture2D.grayTexture;
            mat.SetTexture("_BaseMap", t);
            // a faint glow of its own so a photograph reads even in the desk's shadows
            mat.SetTexture("_EmissionMap", t);
            MaterialLibrary.SetEmission(mat, Glow);
        }

        /// <summary>The direction the picture faces (towards whoever looks at it).</summary>
        public Vector3 Facing
        {
            get
            {
                if (photo == null) return -transform.forward;
                var mf = photo.GetComponent<MeshFilter>();
                Vector3 n = mf != null && mf.sharedMesh != null && mf.sharedMesh.normals.Length > 0 ? mf.sharedMesh.normals[0] : Vector3.back;
                return photo.transform.TransformDirection(n).normalized;
            }
        }

        public System.Collections.IEnumerator Change(Texture2D to)
        {
            if (mat == null) yield break;
            AudioDirector.Play("photo_change", 0.8f, Random.Range(0.97f, 1.03f));
            yield return Tween.Run(0.5f, k => MaterialLibrary.SetEmission(mat, Color.Lerp(Glow, new Color(1f, 0.85f, 0.6f) * 1.5f, k)), Ease.InQuad);
            SetPhoto(to);
            StartCoroutine(Tween.Punch(transform, 0.08f, 0.4f));
            yield return Tween.Run(1.2f, k => MaterialLibrary.SetEmission(mat, Color.Lerp(new Color(1f, 0.85f, 0.6f) * 1.5f, Glow, k)), Ease.OutQuad);
        }
    }

    public partial class DeskProps
    {
        /// <summary>The Polaroid slides out of the tray, lands on the desk, and the camera looks at it.</summary>
        public System.Collections.IEnumerator ArrivePolaroid(Vector3 from)
        {
            var t = polaroid.transform;
            polaroid.SetPhoto(Director.I.PhotoTexture("polaroid"));
            t.gameObject.SetActive(true);
            AudioDirector.Play("tray_slide", 0.6f);
            yield return Tween.Run(0.9f, k =>
            {
                t.position = Vector3.Lerp(from, PolaroidPos, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.08f;
                t.rotation = Quaternion.Slerp(Quaternion.Euler(0f, -60f, 0f), PolaroidRot, k);
            }, Ease.OutCubic);
            AudioDirector.PlayMaterial("paper", "put", 0.6f);
            CameraRig.I.Focus(PolaroidPos, Vector3.Lerp(PolaroidPos, CameraRig.I.eye, 0.45f) + Vector3.up * 0.04f, 38f);
            yield return polaroid.Change(Director.I.PhotoTexture("polaroid"));
            yield return new WaitForSeconds(1.6f);
            CameraRig.I.ClearFocus();
        }
    }

    /// <summary>The flip calendar showing today's date (so "tomorrow" is always checkable).</summary>
    public class DeskCalendar : Interactable
    {
        TextMeshPro weekday, day, month;
        public override CursorKind Cursor => CursorKind.Look;
        public override string Hint => $"Today is {Director.I?.TodayText}";

        public void Init(Transform root)
        {
            var anchor = ModelLibrary.Find(root, "CAL_PAGE") ?? root;
            weekday = Text.World(anchor, "MONDAY", Fonts.Type, 0.009f, new Color(0.55f, 0.12f, 0.1f));
            weekday.transform.localPosition = new Vector3(0f, 0.035f, 0f);
            day = Text.World(anchor, "15", Fonts.Title, 0.04f, new Color(0.12f, 0.1f, 0.1f));
            day.transform.localPosition = new Vector3(0f, 0.0f, 0f);
            month = Text.World(anchor, "OCTOBER 1962", Fonts.Type, 0.0075f, new Color(0.2f, 0.18f, 0.18f));
            month.transform.localPosition = new Vector3(0f, -0.035f, 0f);
            foreach (var t in new[] { weekday, day, month }) t.transform.localRotation = Quaternion.identity;
            var bc = gameObject.AddComponent<BoxCollider>();
            var b = ModelLibrary.Bounds(gameObject);
            bc.center = transform.InverseTransformPoint(b.center);
            bc.size = b.size;
        }

        public void Set(string wd, int d, string m)
        {
            weekday.text = wd.ToUpperInvariant();
            day.text = d.ToString();
            month.text = m.ToUpperInvariant();
        }
    }

    /// <summary>Agnes's rules on a card by the lamp, the way she kept them: hover to read, click (or R) to hold it up.</summary>
    public class DeskRulesCard : Interactable
    {
        const float W = 0.082f, H = 0.108f;
        TextMeshPro body;
        int shown = -1;
        float refresh;

        public override CursorKind Cursor => CursorKind.Look;
        public override string Hint => "Agnes's rules   ·   click or R to read them";

        public static DeskRulesCard Create(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("RulesCard");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var c = go.AddComponent<DeskRulesCard>();
            c.Build();
            return c;
        }

        void Build()
        {
            var paper = new GameObject("Paper");
            paper.transform.SetParent(transform, false);
            paper.AddComponent<MeshFilter>().sharedMesh = ProcMesh.Paper(W, H, 0.0006f);
            paper.AddComponent<MeshRenderer>().sharedMaterial = DeskMaterials.Tag;
            var bc = gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 0.002f, 0f);
            bc.size = new Vector3(W, 0.006f, H);
            var head = Text.World(transform, "My rules", Fonts.AgnesBold, 0.0115f, new Color(0.45f, 0.12f, 0.12f));
            head.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            head.transform.localPosition = new Vector3(0f, 0.0009f, H / 2 - 0.013f);
            head.rectTransform.sizeDelta = new Vector2(W - 0.01f, 0.016f);
            body = Text.World(transform, "", Fonts.Agnes, 0.0042f, new Color(0.13f, 0.15f, 0.32f));
            body.alignment = TextAlignmentOptions.TopLeft;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.overflowMode = TextOverflowModes.Truncate;
            body.lineSpacing = -18f;
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            body.rectTransform.sizeDelta = new Vector2(W - 0.012f, H - 0.03f);
            body.transform.localPosition = new Vector3(0f, 0.0009f, -0.01f);
        }

        void Update()
        {
            // the card fills up as Agnes's notes turn up through the week
            refresh -= Time.unscaledDeltaTime;
            if (refresh > 0f || Director.I == null || Director.I.Db == null) return;
            refresh = 0.5f;
            var known = Director.I.KnownRules();
            int key = known.Count == 0 ? 0 : known.Sum(r => 1 << r);
            if (key == shown) return;
            shown = key;
            body.text = string.Join("\n", Director.I.Db.root.rules.Where(r => known.Contains(r.id)).OrderBy(r => r.id).Select(r => $"{r.id}. {r.text}"));
        }

        public override void OnHover(bool on)
        {
            if (on) UIRoot.I?.rulesPeek.Show();
            else UIRoot.I?.rulesPeek.Hide();
        }

        public override void OnClick()
        {
            UIRoot.I?.rulesPeek.Hide();
            RulesCard.Show();
        }
    }
}
