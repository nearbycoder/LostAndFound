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

        /// <summary>Scale of the reading UI: 1.25 with Settings > Text size: Large (or -lafTextSize 1 for one run).</summary>
        public static float TextScale => (int.TryParse(Game.Arg("-lafTextSize") ?? "", out int t) ? t : Settings.TextSize) >= 1 ? 1.25f : 1f;

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
            Fonts.Assign(t, font);
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

namespace LostAndFound
{
    /// <summary>
    /// A soft dark pill behind a light label, sized to its text every frame and fading with it, so hints
    /// stay legible wherever they land (over the pale claim slip, a tag, the lamp's pool of light).
    /// </summary>
    public class TextBacking : MonoBehaviour
    {
        static Sprite pill;
        TextMeshProUGUI label;
        Image image;
        float alpha;
        Vector2 pad;

        public static TextBacking Add(TextMeshProUGUI label, float alpha = 0.84f, float padX = 34f, float padY = 10f)
        {
            var rt = UiKit.Rect(label.name + "Backing", label.transform.parent);
            rt.SetSiblingIndex(label.transform.GetSiblingIndex());   // drawn just before (behind) the label
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            var b = rt.gameObject.AddComponent<TextBacking>();
            b.label = label;
            b.alpha = alpha;
            b.pad = new Vector2(padX, padY);
            b.image = rt.gameObject.AddComponent<Image>();
            b.image.sprite = Pill;
            b.image.type = Image.Type.Sliced;
            b.image.raycastTarget = false;
            b.image.color = new Color(0.03f, 0.02f, 0.02f, 0f);
            return b;
        }

        /// <summary>A rounded rectangle with a soft edge; the middle stretches.</summary>
        static Sprite Pill
        {
            get
            {
                if (pill != null) return pill;
                const int N = 64;
                const float R = 22f, Soft = 12f;
                var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        // distance outside a rounded square inset by the soft edge
                        float qx = Mathf.Max(Mathf.Abs(x + 0.5f - N / 2f) - (N / 2f - Soft - R), 0f);
                        float qy = Mathf.Max(Mathf.Abs(y + 0.5f - N / 2f) - (N / 2f - Soft - R), 0f);
                        float d = Mathf.Sqrt(qx * qx + qy * qy) - R;
                        float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(d / Soft));
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                tex.Apply();
                pill = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(31, 31, 31, 31));
                return pill;
            }
        }

        void LateUpdate()
        {
            if (label == null) { Destroy(gameObject); return; }
            bool show = label.isActiveAndEnabled && !string.IsNullOrWhiteSpace(label.text) && label.color.a > 0.01f;
            var c = image.color;
            c.a = show ? alpha * label.color.a : 0f;
            image.color = c;
            if (!show) return;
            var bounds = label.textBounds;
            if (bounds.size.x <= 0f) { image.color = new Color(c.r, c.g, c.b, 0f); return; }
            var rt = (RectTransform)transform;
            rt.localPosition = label.rectTransform.localPosition + label.rectTransform.localRotation * bounds.center;
            rt.sizeDelta = new Vector2(bounds.size.x, bounds.size.y) + pad * 2f;
        }
    }
}
