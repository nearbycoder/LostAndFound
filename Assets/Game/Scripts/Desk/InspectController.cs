using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LostAndFound
{
    /// <summary>
    /// Holding an object up to the lamp. Drag to turn it (with inertia), scroll to bring it closer,
    /// and the magnifier cursor glints when it nears a hidden detail. Click a detail to note it; click
    /// a lid or latch to work it. Right click puts it down on the mat, T puts it on the counter tray.
    /// </summary>
    public class InspectController : MonoBehaviour
    {
        public static InspectController I { get; private set; }
        public Camera cam;
        public ItemView Held { get; private set; }
        public bool Busy { get; private set; }
        public float distance = 0.36f;

        Quaternion holdRot = Quaternion.identity;
        Vector3 angularVel;
        float targetScale = 1f, scale = 1f, zoom = 1f;
        Vector2 pressPos;
        bool pressing, dragging;
        DetailDef nearDetail;
        float nearStrength;
        readonly RaycastHit[] hits = new RaycastHit[12];
        float heldTime;

        public const float SparkleRadius = 90f, ClickRadius = 46f;

        void Awake() => I = this;

        Vector2 pan;

        Vector3 HandPosition()
        {
            var t = cam.transform;
            float d = distance / zoom;
            return t.position + t.forward * d + t.up * (-0.018f + pan.y) + t.right * pan.x;
        }

        /// <summary>Zoom so the point under the cursor stays under the cursor (lets you reach the ends of long things).</summary>
        void ZoomTo(float newZoom, Vector2 mouse)
        {
            float d0 = distance / zoom, d1 = distance / newZoom;
            float tan = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float dx = (2f * mouse.x / Mathf.Max(1, Screen.width) - 1f) * tan * cam.aspect;
            float dy = (2f * mouse.y / Mathf.Max(1, Screen.height) - 1f) * tan;
            pan += new Vector2(dx, dy) * (d1 - d0);
            float limit = 0.09f * (newZoom - 1f) / 0.65f + 0.001f;
            pan = Vector2.ClampMagnitude(pan, Mathf.Max(0f, limit));
            zoom = newZoom;
        }

        public void Begin(ItemView item)
        {
            if (Held != null || Busy) return;
            StartCoroutine(BeginRoutine(item));
        }

        IEnumerator BeginRoutine(ItemView item)
        {
            Busy = true;
            Held = item;
            heldTime = 0f;
            InteractionSystem.I.ClearHover();
            InteractionSystem.I.Blocked = true;
            CameraRig.I.allowTurn = false;
            UIRoot.I?.tagCard.Hide(item);
            Desk.I.Detach(item);
            item.place = ItemPlace.Held;
            item.EnsureMeshColliders();
            item.SetPickable(false);
            AudioDirector.PlayMaterial(item.def.sound, "pick", 0.7f);
            AudioDirector.Play("whoosh", 0.25f, Random.Range(1.0f, 1.2f));

            CameraRig.I.ClearFocus();
            Desk.I.CloseAllDrawers();
            if (CameraRig.I.view != View.Counter) CameraRig.I.SetView(View.Counter);
            PostFX.I?.SetInspect(true);
            UIRoot.I?.inspectBar.Show(item);

            float r = item.def.inspectScale > 0 ? item.def.inspectScale : 0.075f;
            targetScale = Mathf.Clamp(r / item.radius, 0.2f, 3.5f);
            zoom = 1f;
            pan = Vector2.zero;
            // present the object tilted towards the eye: its top faces us a little
            holdRot = Quaternion.Euler(-38f, 0f, 0f);
            angularVel = Vector3.zero;

            var tr = item.transform;
            Vector3 p0 = tr.position;
            Quaternion r0 = tr.rotation;
            float s0 = tr.localScale.x;
            float dur = 0.48f;
            yield return Tween.Run(dur, k =>
            {
                Vector3 target = HandPosition() - cam.transform.rotation * holdRot * (item.centerOffset * targetScale);
                Vector3 mid = Vector3.Lerp(p0, target, 0.5f) + Vector3.up * 0.12f;
                Vector3 a = Vector3.LerpUnclamped(p0, mid, k), b = Vector3.LerpUnclamped(mid, target, k);
                tr.position = Vector3.LerpUnclamped(a, b, k);
                tr.rotation = Quaternion.SlerpUnclamped(r0, cam.transform.rotation * holdRot, k);
                scale = Mathf.LerpUnclamped(s0, targetScale, k);
                tr.localScale = Vector3.one * scale;
            }, Ease.OutCubic);
            Busy = false;
            item.RefreshUV();
            Director.I?.OnItemInspected(item);
        }

        /// <summary>Put the held item somewhere. Returns once it has landed.</summary>
        public void Release(ItemPlace to)
        {
            if (Held == null || Busy) return;
            StartCoroutine(ReleaseRoutine(to));
        }

        IEnumerator ReleaseRoutine(ItemPlace to)
        {
            Busy = true;
            var item = Held;
            UIRoot.I?.inspectBar.Hide();
            UIRoot.I?.hotspotMarker.Hide();
            PostFX.I?.SetInspect(false);
            CursorController.Want(CursorKind.Default);
            var placing = Desk.I.Place(item, to);
            item.RefreshUV();
            yield return placing;
            Held = null;
            Busy = false;
            InteractionSystem.I.Blocked = false;
            CameraRig.I.allowTurn = true;
        }

        void Update()
        {
            if (Held == null || Busy) return;
            heldTime += Time.deltaTime;
            var item = Held;
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            if (mouse == null) return;
            if (UIRoot.ModalOpen) return;

            // --- input: zoom and keys
            float wheel = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) > 0.01f) ZoomTo(Mathf.Clamp(zoom + Mathf.Sign(wheel) * 0.12f, 0.75f, 1.65f), mouse.position.ReadValue());
            if (zoom <= 1.001f) pan = Vector2.Lerp(pan, Vector2.zero, 1f - Mathf.Exp(-6f * Time.deltaTime));
            if (kb != null)
            {
                if (InputX.KeyDown(Key.T)) { Release(ItemPlace.Tray); return; }
                if (InputX.KeyDown(Key.Escape) || InputX.KeyDown(Key.Backspace)) { Release(ItemPlace.Mat); return; }
                if (InputX.KeyDown(Key.L)) Lamp.I?.ToggleUV();
                // keyboard turning for accessibility
                if (InputX.KeyHeld(Key.Q)) angularVel.y += 260f * Time.deltaTime;
                if (InputX.KeyHeld(Key.E)) angularVel.y -= 260f * Time.deltaTime;
                if (InputX.KeyHeld(Key.W)) angularVel.x += 260f * Time.deltaTime;
                if (InputX.KeyHeld(Key.S)) angularVel.x -= 260f * Time.deltaTime;
            }
            if (InputX.RightDown) { Release(ItemPlace.Mat); return; }

            Vector2 mp = mouse.position.ReadValue();
            bool overUI = InteractionSystem.PointerOverUI();

            // --- hotspot proximity
            nearDetail = FindNearDetail(mp, out float px, out Vector3 hsWorld);
            nearStrength = nearDetail == null ? 0f : Mathf.Clamp01(1f - px / SparkleRadius);
            if (nearDetail != null) UIRoot.I?.hotspotMarker.Show(cam.WorldToScreenPoint(hsWorld), nearStrength, px <= ClickRadius);
            else UIRoot.I?.hotspotMarker.Hide();

            ItemPart hoverPart = null;
            if (nearDetail == null || px > ClickRadius) hoverPart = PartUnderMouse(mp);

            // --- press / drag / click
            if (InputX.LeftDown && !overUI)
            {
                pressing = true;
                dragging = false;
                pressPos = mp;
            }
            if (pressing && InputX.LeftHeld)
            {
                Vector2 delta = mouse.delta.ReadValue();
                if (!dragging && (mp - pressPos).magnitude > 5f) dragging = true;
                if (dragging)
                {
                    float sens = 0.42f * Settings.MouseSensitivity;
                    Vector3 v = new Vector3(delta.y * sens, -delta.x * sens, 0f) / Mathf.Max(Time.deltaTime, 1e-3f);
                    angularVel = Vector3.Lerp(angularVel, v, 0.6f);
                }
            }
            if (pressing && InputX.LeftUp)
            {
                pressing = false;
                if (!dragging && !overUI)
                {
                    if (nearDetail != null && px <= ClickRadius) Discover(nearDetail, hsWorld);
                    else if (hoverPart != null) OperatePart(hoverPart);
                }
                dragging = false;
            }

            // cursor
            if (dragging) CursorController.Want(CursorKind.Grab);
            else if (nearDetail != null && px <= ClickRadius) CursorController.Want(CursorKind.Magnifier);
            else if (hoverPart != null) CursorController.Want(CursorKind.Hand);
            else CursorController.Want(CursorKind.Look);
            UIRoot.I?.inspectBar.SetPartHint(hoverPart != null ? PartLabel(hoverPart) : null);

            // --- apply rotation with inertia, follow the hand anchor
            float dt = Time.deltaTime;
            if (angularVel.sqrMagnitude > 0.0001f)
            {
                var camRot = cam.transform.rotation;
                // rotate in camera space: x = pitch about camera right, y = yaw about camera up
                holdRot = Quaternion.AngleAxis(angularVel.y * dt, Vector3.up) * Quaternion.AngleAxis(angularVel.x * dt, Vector3.right) * holdRot;
            }
            if (!(pressing && dragging)) angularVel = Vector3.Lerp(angularVel, Vector3.zero, 1f - Mathf.Exp(-4.5f * dt));

            var tr = item.transform;
            scale = MathX.Damp(scale, targetScale, 10f, dt);
            tr.localScale = Vector3.one * scale;
            Quaternion want = cam.transform.rotation * holdRot;
            tr.rotation = MathX.Damp(tr.rotation, want, 18f, dt);
            float bob = Settings.ReduceMotion ? 0f : Mathf.Sin(Time.time * 1.3f) * 0.0018f;
            Vector3 target = HandPosition() + cam.transform.up * bob - tr.rotation * (item.centerOffset * scale);
            tr.position = MathX.Damp(tr.position, target, 14f, dt);
        }

        string PartLabel(ItemPart p)
        {
            if (!string.IsNullOrEmpty(p.def.label)) return p.def.label;
            return p.def.kind switch
            {
                "spin" => "Wind it",
                "shake" => "Give it a shake",
                "listen" => "Hold it to your ear",
                "play" => "Play it",
                _ => p.open ? "Close it" : "Open it",
            };
        }

        void OperatePart(ItemPart part)
        {
            part.Toggle();
            if (part.def.kind is "shake" or "listen" or "play" or "spin")
                StartCoroutine(PartAction(part));
            if (!string.IsNullOrEmpty(part.def.reveals) && (part.open || part.def.kind != "hinge"))
            {
                var d = Held.def.Detail(part.def.reveals);
                if (d != null && Director.I != null && !Director.I.IsDiscovered(Held.def, d))
                    StartCoroutine(RevealAfter(d, part.transform.position, 0.6f));
            }
        }

        IEnumerator RevealAfter(DetailDef d, Vector3 where, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (Held != null) Discover(d, where);
        }

        IEnumerator PartAction(ItemPart part)
        {
            switch (part.def.kind)
            {
                case "shake":
                    {
                        Vector3 v = angularVel;
                        yield return Tween.Run(0.7f, k => angularVel = v + new Vector3(Mathf.Sin(k * 40f) * 420f * (1 - k), 0f, 0f), Ease.Linear);
                        break;
                    }
                case "listen":
                    {
                        float z0 = zoom;
                        yield return Tween.Run(0.5f, k => zoom = Mathf.Lerp(z0, 1.65f, k), Ease.InOutSine);
                        yield return new WaitForSeconds(1.6f);
                        yield return Tween.Run(0.5f, k => zoom = Mathf.Lerp(1.65f, z0, k), Ease.InOutSine);
                        break;
                    }
                default:
                    yield return null;
                    break;
            }
        }

        DetailDef FindNearDetail(Vector2 mouse, out float bestPx, out Vector3 bestWorld)
        {
            bestPx = float.MaxValue;
            bestWorld = default;
            DetailDef best = null;
            if (Director.I == null) return null;
            foreach (var d in Held.def.details)
            {
                if (Director.I.IsDiscovered(Held.def, d)) continue;
                if (d.requires == "uv" && (Lamp.I == null || !Lamp.I.UV)) continue;
                if (d.requires is "wind" or "listen" or "shake" or "play") continue; // revealed by parts
                var hs = Held.Hotspot(d);
                if (hs == null) continue;
                Vector3 sp = cam.WorldToScreenPoint(hs.position);
                if (sp.z <= 0) continue;
                float px = Vector2.Distance(mouse, sp);
                if (px > SparkleRadius || px >= bestPx) continue;
                if (Occluded(hs)) continue;
                bestPx = px;
                best = d;
                bestWorld = hs.position;
            }
            return best;
        }

        /// <summary>A hotspot counts as visible if nothing of the item sits between it and the eye,
        /// and its outward axis (the empty's local up) roughly faces the camera.</summary>
        bool Occluded(Transform hs)
        {
            Vector3 eye = cam.transform.position;
            Vector3 to = hs.position - eye;
            float dist = to.magnitude;
            Vector3 outward = hs.up;
            if (Vector3.Dot(outward, -to.normalized) < 0.12f) return true;
            int n = Physics.RaycastNonAlloc(eye, to / dist, hits, dist - 0.004f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (hits[i].collider.transform.IsChildOf(Held.transform)) return true;
            }
            return false;
        }

        ItemPart PartUnderMouse(Vector2 mp)
        {
            var ray = cam.ScreenPointToRay(mp);
            int n = Physics.RaycastNonAlloc(ray, hits, 2f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            ItemPart found = null;
            bool hitItem = false;
            for (int i = 0; i < n; i++)
            {
                if (!hits[i].collider.transform.IsChildOf(Held.transform)) continue;
                if (hits[i].distance >= best) continue;
                best = hits[i].distance;
                hitItem = true;
                found = hits[i].collider.GetComponentInParent<ItemPart>();
            }
            if (found == null && hitItem)
            {
                // whole-object actions (shake, listen) respond anywhere on the item
                foreach (var p in Held.parts)
                    if (p.def.kind is "shake" or "listen" or "play" && string.IsNullOrEmpty(p.def.node)) return p;
            }
            return found;
        }

        /// <summary>Notice a detail of the held object as if the player had clicked it (filmed AutoPilot runs).</summary>
        public void DiscoverForDemo(DetailDef d, Vector3 world) { if (Held != null) Discover(d, world); }

        void Discover(DetailDef d, Vector3 world)
        {
            if (Director.I == null || Director.I.IsDiscovered(Held.def, d)) return;
            Director.I.Discover(Held.def, d);
            UIRoot.I?.discovery.Play(cam.WorldToScreenPoint(world), Director.I.FactText(Held.def, d), d.kind == "secret");
            AudioDirector.Play(d.kind == "secret" ? "discover_secret" : "discover", 0.8f);
            AudioDirector.Play("pen_scratch", 0.45f, Random.Range(0.95f, 1.1f), 0.25f);
            CameraRig.I?.Shake(0.08f);
            // a small push towards the detail
            StartCoroutine(PushIn());
        }

        IEnumerator PushIn()
        {
            float z0 = zoom;
            yield return Tween.Run(0.18f, k => zoom = z0 + k * 0.08f, Ease.OutQuad);
            yield return Tween.Run(0.35f, k => zoom = z0 + (1 - k) * 0.08f, Ease.InOutSine);
        }
    }
}
