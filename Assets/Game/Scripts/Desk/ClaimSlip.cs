using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LostAndFound
{
    /// <summary>
    /// The claim slip on the desk: a typewritten form where the claimant's claims are written in as
    /// they speak and your findings are added as you discover them. Hover (or Tab) to lean in and
    /// read it; click a finding to ask the claimant about it; stamp it to decide.
    /// </summary>
    public class ClaimSlip : Interactable
    {
        public static ClaimSlip I { get; private set; }
        public const float W = 0.19f, H = 0.26f;
        TextMeshPro header, body;
        Transform paper;
        readonly List<GameObject> imprints = new();
        readonly List<string> claimLines = new();
        readonly List<(string id, string text)> clueLines = new();
        string[] claimants = new string[0];
        string caseNo = "", date = "";
        bool focused;
        float hoverTime;
        int hoverLink = -1;
        Camera cam;
        public bool Active { get; private set; }
        public bool Focused => focused;
        /// <summary>For the AutoPilot: a pointer it isn't using (left at the top of the window) doesn't lean back out.</summary>
        public static bool IgnorePointerExit;

        public override CursorKind Cursor => CursorKind.Look;
        public override string Hint => focused ? null : GamepadInput.Prompt("Read the claim slip  [Tab]", "Read the claim slip  [View]");

        public static ClaimSlip Create(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("ClaimSlip");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var s = go.AddComponent<ClaimSlip>();
            s.Build();
            return s;
        }

        void Awake() => I = this;

        void Build()
        {
            cam = Camera.main;
            paper = new GameObject("Paper").transform;
            paper.SetParent(transform, false);
            var mf = paper.gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = ProcMesh.Paper(W, H, 0.0006f);
            paper.gameObject.AddComponent<MeshRenderer>().sharedMaterial = DeskMaterials.Paper;
            var bc = gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 0.002f, 0f);
            bc.size = new Vector3(W, 0.006f, H);

            header = Text.World(transform, "", Fonts.Type, 0.0085f, new Color(0.15f, 0.14f, 0.16f));
            header.alignment = TextAlignmentOptions.Top;
            header.rectTransform.sizeDelta = new Vector2(W - 0.016f, 0.05f);
            header.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            header.transform.localPosition = new Vector3(0f, 0.0009f, H / 2 - 0.037f);   // below the printed border rule
            header.textWrappingMode = TextWrappingModes.Normal;

            body = Text.World(transform, "", Fonts.Hand, 0.012f, DeskMaterials.InkColor);
            body.alignment = TextAlignmentOptions.TopLeft;
            body.rectTransform.sizeDelta = new Vector2(W - 0.022f, H - 0.07f);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            body.transform.localPosition = new Vector3(0f, 0.0009f, -0.022f);
            body.textWrappingMode = TextWrappingModes.Normal;
            body.overflowMode = TextOverflowModes.Overflow;
            body.lineSpacing = -12f;
            body.enableAutoSizing = true;
            body.fontSizeMin = 0.05f;
            body.fontSizeMax = 0.12f;
            Clear();
        }

        public void Clear()
        {
            claimLines.Clear();
            clueLines.Clear();
            claimants = new string[0];
            foreach (var g in imprints) Destroy(g);
            imprints.Clear();
            Active = false;
            Redraw();
        }

        public void Begin(string caseNumber, string dateText, string[] claimantNames)
        {
            Clear();
            Active = true;
            caseNo = caseNumber;
            date = dateText;
            claimants = claimantNames;
            Redraw();
            StartCoroutine(Tween.Punch(transform, 0.04f, 0.3f));
            AudioDirector.Play("paper_slide", 0.6f);
        }

        public void AddClaim(string text, string who = null)
        {
            claimLines.Add(claimants.Length > 1 && !string.IsNullOrEmpty(who) ? $"<b>{who}:</b> {text}" : text);
            Redraw();
            AudioDirector.Play("pen_scratch", 0.35f, Random.Range(0.95f, 1.1f));
        }

        public void AddClue(string detailId, string text)
        {
            foreach (var c in clueLines) if (c.id == detailId) return;
            clueLines.Add((detailId, text));
            Redraw();
        }

        public bool HasClue(string id) => clueLines.Exists(c => c.id == id);

        /// <summary>The findings on the slip, in order (each can be asked about).</summary>
        public List<string> ClueIds() => clueLines.ConvertAll(c => c.id);

        /// <summary>World position a few letters into a finding's link, for scripted demos to point at.</summary>
        public bool LinkPosition(string id, out Vector3 world)
        {
            world = default;
            body.ForceMeshUpdate();
            var ti = body.textInfo;
            for (int i = 0; i < ti.linkCount; i++)
            {
                if (ti.linkInfo[i].GetLinkID() != id) continue;
                int first = ti.linkInfo[i].linkTextfirstCharacterIndex;
                int k = Mathf.Min(first + 6, first + ti.linkInfo[i].linkTextLength - 1);
                var c = ti.characterInfo[k];
                world = body.transform.TransformPoint((c.bottomLeft + c.topRight) * 0.5f);
                return true;
            }
            return false;
        }

        /// <summary>An invisible full-size handwritten glyph: gives a small typed label a full line height,
        /// so the handwriting on the next line doesn't ride up into it.</summary>
        const string Strut = "<alpha=#00>|<alpha=#FF>";

        void Redraw()
        {
            if (header == null) return;
            if (!Active)
            {
                header.text = "NINEFOLD JUNCTION\n<size=70%>LOST PROPERTY OFFICE</size>";
                body.text = "\n\n<color=#00000040><i>Ring the bell for the next claimant.</i></color>";
                return;
            }
            header.text = $"NINEFOLD JUNCTION · LOST PROPERTY\n<size=78%>CLAIM No. {caseNo}   ·   {date}</size>";
            var sb = new StringBuilder();
            sb.Append("<font=\"SpecialElite\"><size=62%><color=#26222a>CLAIMANT:</color></size></font> ");
            sb.Append(string.Join(" & ", claimants));
            sb.Append("\n<font=\"SpecialElite\"><size=62%><color=#26222a>THEY SAY:</color></size></font>").Append(Strut).Append('\n');
            if (claimLines.Count == 0) sb.Append("<color=#00000050>...</color>\n");
            foreach (var l in claimLines) sb.Append("— ").Append(l).Append('\n');
            sb.Append("<font=\"SpecialElite\"><size=62%><color=#26222a>I FOUND:</color></size></font>");
            if (clueLines.Count > 0) sb.Append(" <size=60%><color=#5a4a3a>(click to ask)</color></size>");
            sb.Append(Strut).Append('\n');
            if (clueLines.Count == 0) sb.Append("<color=#00000050>(look closely at the item)</color>\n");
            for (int i = 0; i < clueLines.Count; i++)
            {
                string col = i == hoverLink ? "#8a2a1a" : "#3a3330";
                string u = i == hoverLink ? "<u>" : "";
                string eu = i == hoverLink ? "</u>" : "";
                sb.Append($"<link=\"{clueLines[i].id}\"><color={col}>{u}• {clueLines[i].text}{eu}</color></link>\n");
            }
            body.text = sb.ToString();
            body.ForceMeshUpdate();
        }

        public override void OnHover(bool on)
        {
            if (!on) hoverTime = 0f;
        }

        public override void OnClick()
        {
            if (!focused) SetFocus(true);
            else TryAsk();
        }

        public void SetFocus(bool on)
        {
            if (focused == on) return;
            focused = on;
            if (on)
            {
                // frame the slip with the stamp rack to its left and room on its right for what they said (TranscriptCard);
                // on a screen narrower than 16:9 the view widens to keep that framing side to side
                Vector3 c = transform.position + new Vector3(-0.02f, 0f, 0.01f);
                float aspect = Mathf.Max(0.5f, (float)Screen.width / Mathf.Max(1, Screen.height));
                float fov = aspect >= 16f / 9f ? 50f : 2f * Mathf.Atan(Mathf.Tan(25f * Mathf.Deg2Rad) * (16f / 9f) / aspect) * Mathf.Rad2Deg;
                CameraRig.I.Focus(c, new Vector3(c.x * 0.6f, 1.14f, c.z - 0.24f), fov);
                AudioDirector.Play("paper_lift", 0.4f);
            }
            else CameraRig.I.ClearFocus();
            PostFX.I?.SetSlipFocus(on);
        }

        void Update()
        {
            if (cam == null) cam = Camera.main;
            var kb = Keyboard.current;
            bool busy = InspectController.I != null && InspectController.I.Held != null;
            if (kb != null && InputX.KeyDown(Key.Tab) && !busy && !UIRoot.ModalOpen) SetFocus(!focused);

            if (focused)
            {
                // leave focus: right click, or moving the mouse to the top of the screen
                var m = Mouse.current;
                if (m != null)
                {
                    float y = m.position.ReadValue().y / Mathf.Max(1, Screen.height);
                    if (InputX.RightDown || (y > 0.93f && StampTool.I?.Carrying == null && !IgnorePointerExit)) SetFocus(false);
                }
                if (busy) SetFocus(false);
                UpdateLinkHover();
            }
            else if (InteractionSystem.I != null && InteractionSystem.I.Hovered == this && !busy && StampTool.I?.Carrying == null)
            {
                hoverTime += Time.deltaTime;
                if (hoverTime > 0.45f) SetFocus(true);
            }
        }

        void UpdateLinkHover()
        {
            if (body == null || cam == null || Mouse.current == null) return;
            int idx = TMP_TextUtilities.FindIntersectingLink(body, Mouse.current.position.ReadValue(), cam);
            if (StampTool.I?.Carrying != null) idx = -1;
            if (idx != hoverLink)
            {
                hoverLink = idx;
                Redraw();
                if (idx >= 0) AudioDirector.Play("tick_soft", 0.2f, 1.4f);
            }
            if (idx >= 0) CursorController.Want(CursorKind.Hand);
            if (idx >= 0 && InputX.LeftDown && !InteractionSystem.PointerOverUI()) TryAsk();
        }

        void TryAsk()
        {
            if (hoverLink < 0 || hoverLink >= body.textInfo.linkCount) return;
            string id = body.textInfo.linkInfo[hoverLink].GetLinkID();
            Director.I?.Ask(id);
        }

        /// <summary>Which claimant's half of the slip a point falls on (two-claimant cases split left/right).</summary>
        public int ClaimantAt(Vector3 world)
        {
            if (claimants.Length < 2) return 0;
            Vector3 local = transform.InverseTransformPoint(world);
            return local.x < 0f ? 0 : 1;
        }

        public bool Contains(Vector3 world, float margin = 0.01f)
        {
            Vector3 local = transform.InverseTransformPoint(world);
            return Mathf.Abs(local.x) < W / 2 + margin && Mathf.Abs(local.z) < H / 2 + margin;
        }

        /// <summary>A stamp that didn't stick (wrong item): fade the last imprint away.</summary>
        public void VoidLastImprint()
        {
            if (imprints.Count == 0) return;
            var g = imprints[imprints.Count - 1];
            imprints.RemoveAt(imprints.Count - 1);
            var r = g.GetComponent<Renderer>();
            if (r != null)
            {
                var m = r.material;
                StartCoroutine(Tween.Run(0.6f, k => { if (m != null) m.SetColor("_BaseColor", new Color(m.color.r, m.color.g, m.color.b, 1f - k)); }, Ease.InQuad));
            }
            Destroy(g, 0.7f);
        }

        public void AddImprint(Verdict v, Vector3 world, float angle)
        {
            var go = new GameObject("Imprint");
            go.transform.SetParent(transform, false);
            Vector3 local = transform.InverseTransformPoint(world);
            local.x = Mathf.Clamp(local.x, -W / 2 + 0.035f, W / 2 - 0.035f);
            local.z = Mathf.Clamp(local.z, -H / 2 + 0.02f, H / 2 - 0.02f);
            go.transform.localPosition = new Vector3(local.x, 0.0011f, local.z);
            go.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
            var tex = Resources.Load<Texture2D>("Textures/stamp_" + VerdictNames.Key(v));
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = ProcMesh.QuadXZ(0.075f, 0.036f);
            var col = v switch { Verdict.Return => DeskMaterials.ReturnInk, Verdict.Seal => DeskMaterials.SealInk, _ => DeskMaterials.RefuseInk };
            var mat = MaterialLibrary.Make(col, tex, 0.25f, true);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (tex == null)
            {
                Destroy(mf);
                var t = Text.World(go.transform, v switch { Verdict.Return => "RETURNED", Verdict.Seal => "SEALED", _ => "REFUSED" }, Fonts.Type, 0.016f, col);
                t.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                t.fontStyle = FontStyles.Bold;
            }
            imprints.Add(go);
            StartCoroutine(Tween.Punch(go.transform, 0.18f, 0.25f));
        }
    }
}
