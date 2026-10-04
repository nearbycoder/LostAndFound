using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace LostAndFound
{
    /// <summary>Places the desk props (lamp, stamps, slip, printer, bell, calendar, photographs...)
    /// and wires their behaviours. Positions are in booth space (player eye at 0, 1.26, -0.08).</summary>
    public class DeskProps : MonoBehaviour
    {
        public Lamp lamp;
        public Bell bell;
        public TicketPrinter printer;
        public ClaimSlip slip;
        public readonly List<Stamp> stamps = new();
        public readonly List<PhotoFrame> photos = new();
        public DeskCalendar calendar;
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
                AddBoxCollider(sg);
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

            // --- photographs (the trailer moment changes every one of them)
            var frames = new (string model, Vector3 pos, float yaw, string id)[]
            {
                ("Frame_Tall", new Vector3(-0.74f, 0.76f, 0.58f), 62f, "staff1921"),
                ("Frame_Wide", new Vector3(-0.50f, 0.76f, 0.92f), 18f, "retirement"),
                ("Frame_Small", new Vector3(0.14f, 0.76f, 0.91f), -8f, "mum"),
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
        }

        public void SetPhoto(Texture2D tex)
        {
            if (mat == null) return;
            mat.SetTexture("_BaseMap", tex != null ? tex : Texture2D.grayTexture);
        }

        public System.Collections.IEnumerator Change(Texture2D to)
        {
            if (mat == null) yield break;
            AudioDirector.Play("photo_change", 0.8f, Random.Range(0.97f, 1.03f));
            yield return Tween.Run(0.5f, k => MaterialLibrary.SetEmission(mat, new Color(1f, 0.85f, 0.6f) * k * 1.5f), Ease.InQuad);
            SetPhoto(to);
            StartCoroutine(Tween.Punch(transform, 0.08f, 0.4f));
            yield return Tween.Run(1.2f, k => MaterialLibrary.SetEmission(mat, new Color(1f, 0.85f, 0.6f) * (1 - k) * 1.5f), Ease.OutQuad);
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
}
