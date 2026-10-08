using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LostAndFound
{
    /// <summary>A rubber stamp in the rack: RETURN (green), REFUSE (red) or SEAL (iron).</summary>
    public class Stamp : Interactable
    {
        public Verdict kind;
        public Vector3 rackPos;
        public Quaternion rackRot;
        public override CursorKind Cursor => CursorKind.Hand;
        public override string Hint => kind switch
        {
            Verdict.Return => "RETURN stamp: give the item on the tray to the claimant",
            Verdict.Seal => "SEAL stamp: lock the item on the tray in the Iron Drawer",
            _ => "REFUSE stamp: send the claimant away empty-handed",
        };
        public override bool Interactive => base.Interactive && StampTool.I != null && StampTool.I.Carrying == null && Director.I != null && Director.I.CanUseStamps;
        public override void OnClick() => StampTool.I.Pick(this);
    }

    /// <summary>Carrying a stamp over the slip and bringing it down with a thump.</summary>
    public class StampTool : MonoBehaviour
    {
        public static StampTool I { get; private set; }
        public Stamp Carrying { get; private set; }
        /// <summary>What the hint says while a stamp is held: over the slip, what it will do there (StampPreview). The
        /// AutoPilot checks it names what the stamp then does.</summary>
        public string Hint { get; private set; }
        public Camera cam;
        bool busy;
        int pickFrame = -1;
        Vector3 lastPos;
        Vector3 tiltVel;
        Transform shadow;

        void Awake() => I = this;

        public void Pick(Stamp s)
        {
            if (busy || Carrying != null) return;
            Carrying = s;
            pickFrame = Time.frameCount;
            InteractionSystem.I.ClearHover();
            InteractionSystem.I.Blocked = true;
            CameraRig.I.allowTurn = false;
            ClaimSlip.I.SetFocus(true);
            AudioDirector.Play("stamp_pick", 0.6f, Random.Range(0.95f, 1.05f));
            lastPos = s.transform.position;
            if (shadow == null)
            {
                shadow = new GameObject("StampShadow").transform;
                var mf = shadow.gameObject.AddComponent<MeshFilter>();
                mf.sharedMesh = ProcMesh.QuadXZ(0.08f, 0.045f);
                var mat = MaterialLibrary.Make(new Color(0f, 0f, 0f, 0.18f), Resources.Load<Texture2D>("Textures/soft_shadow"), 0f, true);
                shadow.gameObject.AddComponent<MeshRenderer>().sharedMaterial = mat;
            }
            shadow.gameObject.SetActive(true);
            Hint = StampPreview.Hint(null, false, GamepadInput.Active);
            UIRoot.I?.hint.Set(Hint);
        }

        void Update()
        {
            // the click that picked the stamp up must not also count as a click to put it down
            if (Carrying == null || busy || Time.frameCount == pickFrame) return;
            var mouse = Mouse.current;
            if (mouse == null) return;
            var tr = Carrying.transform;
            var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            var plane = new Plane(Vector3.up, new Vector3(0f, 0.765f, 0f));
            if (!plane.Raycast(ray, out float d)) return;
            Vector3 hit = ray.GetPoint(d);
            hit.x = Mathf.Clamp(hit.x, -0.7f, 0.7f);
            hit.z = Mathf.Clamp(hit.z, 0.35f, 0.92f);
            Vector3 target = hit + Vector3.up * 0.05f;
            Vector3 vel = (target - lastPos) / Mathf.Max(Time.deltaTime, 1e-3f);
            lastPos = Vector3.Lerp(lastPos, target, 1f - Mathf.Exp(-22f * Time.deltaTime));
            tiltVel = Vector3.Lerp(tiltVel, vel, 0.2f);
            tr.position = lastPos;
            tr.rotation = Quaternion.Euler(Mathf.Clamp(tiltVel.z * 18f, -18f, 18f), 0f, Mathf.Clamp(-tiltVel.x * 18f, -18f, 18f));
            bool over = ClaimSlip.I.Contains(hit, 0.0f);
            // say what it will do here before it comes down (it can't be taken back)
            Hint = Describe(Carrying.kind, over, over ? ClaimSlip.I.ClaimantAt(hit) : 0);
            UIRoot.I?.hint.Set(Hint);
            shadow.position = new Vector3(hit.x, 0.7645f, hit.z);
            shadow.gameObject.SetActive(over);
            CursorController.Want(CursorKind.Stamp);

            if (InputX.RightDown || InputX.KeyDown(Key.Escape))
            {
                StartCoroutine(PutBack());
                return;
            }
            if (InputX.LeftDown && !InteractionSystem.PointerOverUI())
            {
                if (!over) { StartCoroutine(PutBack()); return; }
                int who = ClaimSlip.I.ClaimantAt(hit);
                if (Director.I.CanStamp(Carrying.kind, who, out string why)) StartCoroutine(Slam(hit, who));
                else StartCoroutine(Refuse(why));
            }
        }

        static string Describe(Verdict kind, bool over, int who)
        {
            if (over && !Director.I.CanStamp(kind, who, out string why)) return why;
            return StampPreview.Hint(over ? StampPreview.What(kind, Desk.I.OnTray?.def, ClaimSlip.I.Claimants, who) : null, over, GamepadInput.Active);
        }

        /// <summary>Put the stamp back in the rack without using it (the AutoPilot, having read its hint).</summary>
        public void PutDown() { if (Carrying != null && !busy) StartCoroutine(PutBack()); }

        IEnumerator Refuse(string why)
        {
            busy = true;
            UIRoot.I?.hint.Flash(why);
            AudioDirector.Play("nope", 0.5f);
            var tr = Carrying.transform;
            Vector3 p = tr.position;
            yield return Tween.Run(0.35f, k => tr.position = p + Vector3.right * Mathf.Sin(k * Mathf.PI * 4f) * 0.012f * (1 - k), Ease.Linear);
            busy = false;
        }

        IEnumerator Slam(Vector3 hit, int who)
        {
            busy = true;
            var s = Carrying;
            Debug.Log($"[Stamp] {Hint}");
            var tr = s.transform;
            Vector3 up = new Vector3(hit.x, 0.83f, hit.z);
            Vector3 down = new Vector3(hit.x, 0.7652f + 0.002f, hit.z);
            Quaternion flat = Quaternion.Euler(0f, Random.Range(-10f, 10f), 0f);
            yield return Tween.MoveRotate(tr, up, flat, 0.12f, Ease.OutQuad);
            yield return Tween.Move(tr, down, 0.06f, Ease.InCubic);
            AudioDirector.Play("stamp_thump", 1f, Random.Range(0.94f, 1.04f));
            CameraRig.I.Shake(0.35f);
            ClaimSlip.I.AddImprint(s.kind, hit, Random.Range(-14f, 14f) + (s.kind == Verdict.Refuse ? 6f : 0f));
            shadow.gameObject.SetActive(false);
            var baseScale = tr.localScale;
            tr.localScale = new Vector3(baseScale.x * 1.12f, baseScale.y * 0.8f, baseScale.z * 1.12f);
            yield return new WaitForSeconds(0.07f);
            tr.localScale = baseScale;
            yield return Tween.Move(tr, up, 0.15f, Ease.OutCubic);
            Director.I.CommitStamp(s.kind, who);
            yield return PutBack(true);
        }

        IEnumerator PutBack(bool afterStamp = false)
        {
            busy = true;
            var s = Carrying;
            shadow.gameObject.SetActive(false);
            yield return Tween.Arc(s.transform, s.rackPos, s.rackRot, 0.04f, 0.32f);
            AudioDirector.Play("stamp_rack", 0.5f, Random.Range(0.95f, 1.05f));
            Carrying = null;
            Hint = null;
            busy = false;
            InteractionSystem.I.Blocked = false;
            CameraRig.I.allowTurn = true;
            UIRoot.I?.hint.Set(null);
            if (!afterStamp) ClaimSlip.I.SetFocus(false);
        }
    }
}
