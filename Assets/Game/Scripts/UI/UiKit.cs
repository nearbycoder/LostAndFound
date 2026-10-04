using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LostAndFound
{
    /// <summary>Helpers for building uGUI in code with the game's paper-and-ink look.</summary>
    public static class UiKit
    {
        public static readonly Color Paper = new(0.95f, 0.91f, 0.82f);
        public static readonly Color PaperDark = new(0.86f, 0.79f, 0.66f);
        public static readonly Color Ink = new(0.12f, 0.12f, 0.2f);
        public static readonly Color InkSoft = new(0.3f, 0.27f, 0.25f);
        public static readonly Color Brass = new(0.79f, 0.63f, 0.35f);
        public static readonly Color Green = new(0.12f, 0.23f, 0.2f);
        public static readonly Color Oxblood = new(0.48f, 0.13f, 0.13f);
        public static readonly Color Gold = new(1f, 0.82f, 0.48f);

        static readonly Dictionary<string, Sprite> Sprites = new();

        public static Sprite Sprite(string name, float border = 0f)
        {
            string key = name + border;
            if (Sprites.TryGetValue(key, out var s)) return s;
            var tex = Resources.Load<Texture2D>("Textures/UI/" + name);
            if (tex == null) { Sprites[key] = null; return null; }
            s = UnityEngine.Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            Sprites[key] = s;
            return s;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Anchor(this RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.pivot = pivot;
            return rt;
        }

        public static RectTransform Place(this RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Fill(this RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static Image Image(Transform parent, string name, string sprite, Color color, float border = 0f)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Sprite(sprite, border);
            img.type = border > 0 ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static TextMeshProUGUI Label(Transform parent, string name, string text, string font, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = Fonts.Get(font);
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        public static CanvasGroup Group(GameObject go)
        {
            var g = go.GetComponent<CanvasGroup>();
            if (g == null) g = go.AddComponent<CanvasGroup>();
            return g;
        }

        /// <summary>A text button styled as an ink label that warms on hover.</summary>
        public static PaperButton Button(Transform parent, string name, string text, string font, float size, System.Action onClick)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0);
            img.raycastTarget = true;
            var b = rt.gameObject.AddComponent<PaperButton>();
            b.label = Label(rt, "Label", text, font, size, Ink, TextAlignmentOptions.Center);
            b.label.rectTransform.Fill();
            b.onClick = onClick;
            return b;
        }
    }

    /// <summary>Hover-warm, click-press text button (works with the EventSystem).</summary>
    public class PaperButton : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler,
        UnityEngine.EventSystems.IPointerClickHandler
    {
        public TextMeshProUGUI label;
        public System.Action onClick;
        public Color normal = UiKit.Ink, hover = UiKit.Oxblood;
        public bool interactable = true;
        bool over;
        float k;

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e)
        {
            if (!interactable) return;
            over = true;
            AudioDirector.Play("ui_hover", 0.25f, Random.Range(0.95f, 1.1f));
        }

        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) => over = false;

        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData e)
        {
            if (!interactable) return;
            AudioDirector.Play("ui_click", 0.5f);
            StartCoroutine(Tween.Punch(transform, 0.08f, 0.2f));
            onClick?.Invoke();
        }

        void Update()
        {
            k = Mathf.MoveTowards(k, over && interactable ? 1f : 0f, Time.unscaledDeltaTime * 8f);
            if (label != null)
            {
                label.color = interactable ? Color.Lerp(normal, hover, k) : new Color(normal.r, normal.g, normal.b, 0.35f);
                label.transform.localScale = Vector3.one * (1f + 0.04f * k);
            }
            if (over) CursorController.Want(CursorKind.Hand);
        }
    }
}
