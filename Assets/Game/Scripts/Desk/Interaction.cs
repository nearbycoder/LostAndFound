using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace LostAndFound
{
    public enum CursorKind { Default, Hand, Grab, Magnifier, Stamp, Look, Bell }

    /// <summary>Anything on the desk you can hover and click. Needs a collider on itself or a child.</summary>
    public abstract class Interactable : MonoBehaviour
    {
        public virtual bool Interactive => isActiveAndEnabled;
        public virtual CursorKind Cursor => CursorKind.Hand;
        public virtual string Hint => null;
        public virtual void OnHover(bool on) { }
        public virtual void OnClick() { }
        public virtual void OnRightClick() { }

        /// <summary>Renderers lit up while hovered. Defaults to everything under this object.</summary>
        public virtual Renderer[] HighlightRenderers => highlight ??= GetComponentsInChildren<Renderer>();
        Renderer[] highlight;
        public void RefreshHighlightRenderers() => highlight = null;
    }

    /// <summary>Warm emission pulse on hovered renderers (via property blocks).</summary>
    public static class Highlighter
    {
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        static MaterialPropertyBlock block;
        static readonly Dictionary<Renderer, Color> active = new();

        public static void Set(Renderer[] rs, Color c)
        {
            if (rs == null) return;
            block ??= new MaterialPropertyBlock();
            foreach (var r in rs)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(EmissionColor, c);
                r.SetPropertyBlock(block);
                if (c.maxColorComponent > 0.001f) active[r] = c; else active.Remove(r);
            }
        }

        public static void Clear(Renderer[] rs)
        {
            if (rs == null) return;
            foreach (var r in rs)
            {
                if (r == null) continue;
                r.SetPropertyBlock(null);
                active.Remove(r);
            }
        }
    }

    /// <summary>
    /// Routes the mouse to Interactables: hover (with highlight + cursor + hint) and click. Disabled
    /// while something else owns the mouse (inspecting, carrying a stamp, menus).
    /// </summary>
    public class InteractionSystem : MonoBehaviour
    {
        public static InteractionSystem I { get; private set; }
        public Camera cam;
        public Interactable Hovered { get; private set; }
        public bool Blocked { get; set; }
        public static readonly Color HoverGlow = new(0.16f, 0.11f, 0.05f);
        readonly RaycastHit[] hits = new RaycastHit[16];
        float pulse;

        void Awake() => I = this;

        public static bool PointerOverUI()
        {
            var es = EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }

        public static Vector2 MousePos => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        public static bool Clicked => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        public static bool RightClicked => Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;

        public Interactable Pick(out RaycastHit best)
        {
            best = default;
            if (cam == null) return null;
            var ray = cam.ScreenPointToRay(MousePos);
            int n = Physics.RaycastNonAlloc(ray, hits, 6f, ~0, QueryTriggerInteraction.Collide);
            float bestDist = float.MaxValue;
            Interactable found = null;
            for (int i = 0; i < n; i++)
            {
                if (hits[i].distance >= bestDist) continue;
                var it = hits[i].collider.GetComponentInParent<Interactable>();
                bool blocking = hits[i].collider.GetComponent<RayBlocker>() != null;
                if (it == null && !blocking) continue;
                if (it != null && !it.Interactive && !blocking) continue;
                bestDist = hits[i].distance;
                best = hits[i];
                found = blocking ? null : it;
            }
            return found;
        }

        void Update()
        {
            Interactable now = null;
            if (!Blocked && !PointerOverUI() && Mouse.current != null)
                now = Pick(out _);
            if (now != Hovered)
            {
                if (Hovered != null)
                {
                    Highlighter.Clear(Hovered.HighlightRenderers);
                    Hovered.OnHover(false);
                }
                Hovered = now;
                if (Hovered != null)
                {
                    Hovered.OnHover(true);
                    pulse = 0f;
                }
            }
            if (Hovered != null)
            {
                pulse += Time.deltaTime;
                float k = 0.75f + 0.25f * Mathf.Sin(pulse * 5f);
                Highlighter.Set(Hovered.HighlightRenderers, HoverGlow * k);
                CursorController.Want(Hovered.Cursor);
                if (Clicked) Hovered.OnClick();
                else if (RightClicked) Hovered.OnRightClick();
            }
        }

        public void ClearHover()
        {
            if (Hovered == null) return;
            Highlighter.Clear(Hovered.HighlightRenderers);
            Hovered.OnHover(false);
            Hovered = null;
        }
    }

    /// <summary>Marks colliders that block picking (walls, glass) without being interactive.</summary>
    public class RayBlocker : MonoBehaviour { }
}
