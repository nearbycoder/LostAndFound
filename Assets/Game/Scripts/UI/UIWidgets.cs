using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LostAndFound
{
    /// <summary>
    /// The speech strip: typewriter text with per-character voice babble, the speaker's nameplate,
    /// and highlighted claims ({brown leather|colour}). Click or Space to continue.
    /// </summary>
    public class DialogueBox : MonoBehaviour
    {
        RectTransform panel;
        TextMeshProUGUI nameLabel, body;
        Image paper, nameplate, more;
        CanvasGroup group;
        bool shown, advance, skip;
        public bool Typing { get; private set; }
        public bool Waiting { get; private set; }
        static readonly Regex ClaimRx = new(@"\{([^|{}]+)\|([^{}]+)\}");

        Image tail;
        Vector2 target;
        bool tailLeft;

        public static DialogueBox Create(RectTransform root)
        {
            var rt = UiKit.Rect("Dialogue", root).Anchor(Vector2.zero, Vector2.zero, new Vector2(0f, 1f)).Place(new Vector2(1000f, 900f), new Vector2(700f, 230f));
            var d = rt.gameObject.AddComponent<DialogueBox>();
            d.Build(rt);
            return d;
        }

        void Build(RectTransform rt)
        {
            panel = rt;
            group = UiKit.Group(rt.gameObject);
            group.alpha = 0f;
            group.blocksRaycasts = false;
            var shadow = UiKit.Image(rt, "Shadow", "paper_card", new Color(0, 0, 0, 0.35f), 28f);
            shadow.rectTransform.Fill(-6f);
            shadow.rectTransform.anchoredPosition = new Vector2(6f, -8f);
            paper = UiKit.Image(rt, "Paper", "paper_card", UiKit.Paper, 28f);
            paper.rectTransform.Fill();
            nameplate = UiKit.Image(rt, "Nameplate", "nameplate", UiKit.Green, 12f);
            nameplate.rectTransform.Anchor(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f)).Place(new Vector2(26f, 2f), new Vector2(440f, 42f));
            nameLabel = UiKit.Label(nameplate.transform, "Name", "", Fonts.Type, 22f, UiKit.Gold, TextAlignmentOptions.MidlineLeft);
            nameLabel.rectTransform.Fill();
            nameLabel.margin = new Vector4(18f, 0f, 12f, 0f);
            body = UiKit.Label(rt, "Body", "", Fonts.Body, 33f, UiKit.Ink, TextAlignmentOptions.TopLeft);
            body.rectTransform.Fill();
            body.margin = new Vector4(36f, 40f, 46f, 22f);
            body.lineSpacing = -6f;
            body.enableAutoSizing = true;
            body.fontSizeMin = 22f;
            body.fontSizeMax = 33f;
            tail = UiKit.Image(rt, "Tail", "bubble_tail", UiKit.Paper);
            tail.rectTransform.Anchor(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1f, 0.5f)).Place(new Vector2(4f, -70f), new Vector2(46f, 40f));
            tail.transform.SetAsFirstSibling();
            more = UiKit.Image(rt, "More", "quill", UiKit.Oxblood);
            more.rectTransform.Anchor(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f)).Place(new Vector2(-24f, 18f), new Vector2(34f, 34f));
            more.enabled = false;
        }

        /// <summary>Convert {text|key} markers into highlighted rich text and collect the keys.</summary>
        public static string Parse(string markup, List<string> keys)
        {
            return ClaimRx.Replace(markup ?? "", m =>
            {
                keys?.Add(m.Groups[2].Value);
                return $"<color=#7a2222><u>{m.Groups[1].Value}</u></color>";
            });
        }

        public static string Plain(string markup) => ClaimRx.Replace(markup ?? "", m => m.Groups[1].Value);

        /// <summary>Place the bubble beside the speaker's head (or low on the left for your own lines).</summary>
        void PlaceFor(Commuter who, bool player)
        {
            var canvas = UIRoot.I.root;
            float w = canvas.rect.width, h = canvas.rect.height;
            float s = UiKit.TextScale;
            panel.localScale = Vector3.one * s;
            Vector2 size = panel.sizeDelta * s;
            if (player || who == null || Camera.main == null)
            {
                target = new Vector2(70f, 470f);
                tail.enabled = false;
                return;
            }
            Vector3 sp = Camera.main.WorldToScreenPoint(who.HeadPosition);
            float k = w / Mathf.Max(1, Screen.width);
            Vector2 head = new Vector2(sp.x, sp.y) * k;
            tailLeft = head.x <= w * 0.55f;
            // with two people at the window, the bubble goes on the far side from the other one
            // (and a little narrower) so it never covers a face
            bool crowded = false;
            foreach (var other in Object.FindObjectsByType<Commuter>())
            {
                if (other == who || other == null) continue;
                float ox = Camera.main.WorldToScreenPoint(other.HeadPosition).x * k;
                if (Mathf.Abs(ox - head.x) > w * 0.45f) continue;
                crowded = true;
                tailLeft = ox < head.x;
            }
            panel.sizeDelta = new Vector2(crowded ? 490f : 700f, panel.sizeDelta.y);
            size = panel.sizeDelta * s;
            float margin = crowded ? 14f : 30f;
            float x = tailLeft ? head.x + 170f : head.x - 170f - size.x;
            x = Mathf.Clamp(x, margin, w - size.x - margin);
            float y = Mathf.Clamp(head.y + size.y * 0.55f, size.y + 40f, h - 24f);
            target = new Vector2(x, y);
            tail.enabled = true;
            float tailY = Mathf.Clamp((head.y - y) / s, -panel.sizeDelta.y + 36f, -36f);   // in the bubble's own (unscaled) units
            tail.rectTransform.anchorMin = tail.rectTransform.anchorMax = new Vector2(tailLeft ? 0f : 1f, 1f);
            tail.rectTransform.pivot = new Vector2(tailLeft ? 1f : 0f, 0.5f);
            tail.rectTransform.anchoredPosition = new Vector2(tailLeft ? 6f : -6f, tailY);
            tail.rectTransform.localScale = new Vector3(tailLeft ? 1f : -1f, 1f, 1f);
        }

        public IEnumerator Say(string speaker, string richText, CommuterDef voice, Commuter who, bool wait = true, bool player = false)
        {
            PlaceFor(who, player);
            if (!shown)
            {
                // never flash the previous speaker's line while the bubble fades in
                body.text = "";
                nameLabel.text = speaker.ToUpperInvariant();
                panel.anchoredPosition = target;
                yield return Show();
            }
            nameLabel.text = speaker.ToUpperInvariant();
            nameplate.color = player ? new Color(0.28f, 0.22f, 0.18f) : voice != null && voice.grey ? new Color(0.3f, 0.31f, 0.33f) : voice != null && voice.cold ? new Color(0.2f, 0.32f, 0.4f) : UiKit.Green;
            body.fontStyle = player ? FontStyles.Italic : FontStyles.Normal;
            body.color = player ? UiKit.InkSoft : UiKit.Ink;
            body.text = richText;
            body.maxVisibleCharacters = 0;
            body.ForceMeshUpdate();
            StartCoroutine(Tween.Punch(panel, 0.015f, 0.2f));
            int total = body.textInfo.characterCount;
            Typing = true;
            skip = false;
            advance = false;
            more.enabled = false;
            who?.SetTalking(true);
            float cps = (Director.AutoAdvance && !Director.Cinematic ? 160f : 52f) * Settings.TextSpeed * (voice != null ? Mathf.Lerp(0.85f, 1.2f, voice.speed - 0.5f) : 1f);
            float acc = 0f;
            int shownChars = 0, sinceVoice = 0;
            while (shownChars < total)
            {
                if (skip) { shownChars = total; break; }
                acc += Time.deltaTime * cps;
                while (acc >= 1f && shownChars < total)
                {
                    acc -= 1f;
                    char c = body.textInfo.characterInfo[shownChars].character;
                    shownChars++;
                    if (char.IsLetter(c))
                    {
                        if (++sinceVoice >= 2) { sinceVoice = 0; Babble(c, voice, who, player); }
                    }
                    if (c == '.' || c == '!' || c == '?') acc -= 7f;
                    else if (c == ',' || c == ';' || c == '—') acc -= 3.5f;
                }
                body.maxVisibleCharacters = shownChars;
                yield return null;
            }
            body.maxVisibleCharacters = total;
            Typing = false;
            who?.SetTalking(false);
            if (!wait) yield break;
            Waiting = true;
            more.enabled = true;
            advance = false;
            float t = 0f;
            while (!advance)
            {
                t += Time.deltaTime;
                if (Director.AutoAdvance && t > (Director.Cinematic ? 1.0f + total * 0.03f : 0.45f)) advance = true;
                more.rectTransform.anchoredPosition = new Vector2(-24f, 18f + Mathf.Abs(Mathf.Sin(t * 4f)) * 6f);
                yield return null;
            }
            Waiting = false;
            more.enabled = false;
        }

        static void Babble(char c, CommuterDef voice, Commuter who, bool player)
        {
            if (player) { AudioDirector.Play("type_key", 0.12f, Random.Range(0.9f, 1.15f)); return; }
            string bank = voice != null ? voice.voice : "mid";
            var clip = AudioDirector.Clip("voice_" + bank);
            if (clip == null) return;
            char l = char.ToLowerInvariant(c);
            float vowel = "aeiou".IndexOf(l) >= 0 ? 1.06f : 0.97f;
            float pitch = (voice != null ? voice.pitch : 1f) * vowel * Random.Range(0.93f, 1.07f);
            float vol = voice != null && voice.cold ? 0.45f : 0.6f;
            AudioDirector.PlayVoice(clip, vol, pitch);
            who?.Syllable(Random.Range(0.6f, 1f));
        }

        IEnumerator Show()
        {
            shown = true;
            yield return Tween.Run(0.22f, k =>
            {
                group.alpha = k;
                panel.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, Ease.OutBack(k));
            }, Ease.OutCubic);
        }

        public void Hide()
        {
            if (!shown) return;
            shown = false;
            StartCoroutine(Tween.Run(0.2f, k => group.alpha = 1f - k, Ease.InQuad));
        }

        public bool Shown => shown;

        /// <summary>What the box is saying (the AutoPilot waits for particular lines).</summary>

        public string Text => body != null ? body.text : "";

        void LateUpdate()
        {
            if (shown) panel.anchoredPosition = Vector2.Lerp(panel.anchoredPosition, target, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            // step aside while you read the slip or turn away to the drawers
            bool away = ClaimSlip.I != null && ClaimSlip.I.Focused && !Typing && !Waiting
                        || (CameraRig.I != null && CameraRig.I.view != View.Counter && !Waiting);
            if (shown) group.alpha = Mathf.MoveTowards(group.alpha, away ? 0f : 1f, Time.unscaledDeltaTime * 5f);
        }

        void Update()
        {
            if (!shown || UIRoot.ModalOpen) return;
            bool pressed = InputX.KeyDown(Key.Space) || InputX.KeyDown(Key.Enter)
                           || (InputX.LeftDown && !InteractionSystem.PointerOverUI());
            if (!pressed) return;
            if (Typing) skip = true;
            else if (Waiting) advance = true;
        }
    }

    /// <summary>The intake tag, enlarged near the cursor while you hover an object in storage.</summary>
    public class TagCard : MonoBehaviour
    {
        RectTransform panel;
        CanvasGroup group;
        TextMeshProUGUI text;
        ItemView current;
        float alpha;

        public static TagCard Create(RectTransform root)
        {
            var rt = UiKit.Rect("TagCard", root).Anchor(Vector2.zero, Vector2.zero, new Vector2(0f, 1f)).Place(Vector2.zero, new Vector2(440f, 320f));
            var t = rt.gameObject.AddComponent<TagCard>();
            t.panel = rt;
            t.group = UiKit.Group(rt.gameObject);
            t.group.alpha = 0f;
            t.group.blocksRaycasts = false;
            var sh = UiKit.Image(rt, "Shadow", "tag_card", new Color(0, 0, 0, 0.3f), 0f);
            sh.rectTransform.Fill();
            sh.rectTransform.anchoredPosition = new Vector2(5f, -7f);
            var bg = UiKit.Image(rt, "Card", "tag_card", new Color(0.93f, 0.82f, 0.6f), 0f);
            bg.rectTransform.Fill();
            t.text = UiKit.Label(rt, "Text", "", Fonts.Hand, 32f, UiKit.Ink, TextAlignmentOptions.Left);
            t.text.rectTransform.Fill();
            t.text.margin = new Vector4(92f, 24f, 24f, 20f);   // to the right of the hole and its margin rule
            t.text.lineSpacing = -10f;
            t.text.paragraphSpacing = 4f;
            t.text.enableAutoSizing = true;                    // long tags shrink to fit instead of spilling off the card
            t.text.fontSizeMin = 18f;
            t.text.fontSizeMax = 32f;
            return t;
        }

        public void Show(ItemView item)
        {
            current = item;
            var tag = item.def.tag;
            var sb = new StringBuilder();
            sb.Append($"<size=80%><font=\"SpecialElite\">No. {System.Array.IndexOf(Director.I.Db.root.objects, item.def) + 1:00}</font></size>  <b>{item.def.name}</b>\n");
            if (!string.IsNullOrEmpty(tag.where)) sb.Append($"<size=78%><font=\"SpecialElite\">FOUND</font></size>  {tag.where}\n");
            if (!string.IsNullOrEmpty(tag.when)) sb.Append($"<size=78%><font=\"SpecialElite\">WHEN</font></size>  {tag.when}\n");
            if (!string.IsNullOrEmpty(tag.train)) sb.Append($"<size=78%><font=\"SpecialElite\">TRAIN</font></size>  {tag.train}\n");
            if (!string.IsNullOrEmpty(tag.note)) sb.Append($"<color=#4a3a30><i>{tag.note}</i></color>");
            text.text = sb.ToString();
            AudioDirector.Play("tag_flip", 0.25f, Random.Range(0.9f, 1.1f));
        }

        public void Hide(ItemView item)
        {
            if (current == item) current = null;
        }

        void Update()
        {
            alpha = Mathf.MoveTowards(alpha, current != null ? 1f : 0f, Time.unscaledDeltaTime * 7f);
            group.alpha = Ease.OutCubic(alpha);
            if (current == null && alpha <= 0f) return;
            Vector2 mp = InteractionSystem.MousePos;
            var canvas = UIRoot.I.root;
            float scale = canvas.rect.width / Mathf.Max(1, Screen.width);
            panel.localScale = Vector3.one * UiKit.TextScale;
            Vector2 size = panel.sizeDelta * UiKit.TextScale;
            Vector2 pos = mp * scale + new Vector2(28f, -14f);
            if (pos.x + size.x > canvas.rect.width - 10f) pos.x = mp.x * scale - size.x - 28f;
            if (pos.y - size.y < 10f) pos.y = size.y + 10f;
            panel.anchoredPosition = Vector2.Lerp(panel.anchoredPosition, pos, alpha < 0.05f ? 1f : 1f - Mathf.Exp(-20f * Time.unscaledDeltaTime));
            panel.localRotation = Quaternion.Euler(0f, 0f, -3f + Mathf.Sin(Time.unscaledTime * 1.3f) * 0.6f);
        }
    }

    /// <summary>Agnes's rules held up beside the desk while you hover her card (no clicks needed, nothing paused).</summary>
    public class RulesPeek : MonoBehaviour
    {
        CanvasGroup group;
        TextMeshProUGUI body;
        bool on;
        float a;

        public static RulesPeek Create(RectTransform root)
        {
            var rt = UiKit.Rect("RulesPeek", root).Anchor(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f)).Place(new Vector2(70f, 60f), new Vector2(720f, 760f));
            var p = rt.gameObject.AddComponent<RulesPeek>();
            p.group = UiKit.Group(rt.gameObject);
            p.group.alpha = 0f;
            p.group.blocksRaycasts = false;
            var sh = UiKit.Image(rt, "Shadow", "note_paper", new Color(0, 0, 0, 0.35f), 0f);
            sh.rectTransform.Fill();
            sh.rectTransform.anchoredPosition = new Vector2(6f, -8f);
            var bg = UiKit.Image(rt, "Paper", "note_paper", new Color(0.98f, 0.95f, 0.84f), 0f);
            bg.rectTransform.Fill();
            p.body = UiKit.Label(rt, "Body", "", Fonts.Agnes, 30f, new Color(0.13f, 0.15f, 0.32f), TextAlignmentOptions.TopLeft);
            p.body.rectTransform.Fill();
            p.body.margin = new Vector4(56f, 50f, 50f, 50f);
            p.body.enableAutoSizing = true;
            p.body.fontSizeMin = 16f;
            p.body.fontSizeMax = 30f;
            rt.localRotation = Quaternion.Euler(0f, 0f, 1.5f);
            return p;
        }

        public void Show()
        {
            body.text = RulesCard.Text();
            if (!on) AudioDirector.Play("tag_flip", 0.25f, Random.Range(0.9f, 1.05f));
            on = true;
        }

        public void Hide() => on = false;

        void Update()
        {
            if (UIRoot.ModalOpen) on = false;
            transform.localScale = Vector3.one * Mathf.Min(UiKit.TextScale, 1.15f);   // it's already tall: just a little larger
            a = Mathf.MoveTowards(a, on ? 1f : 0f, Time.unscaledDeltaTime * 7f);
            group.alpha = Ease.OutCubic(a);
        }
    }

    /// <summary>Context hint at the bottom of the screen, plus short red flashes for "you can't do that".</summary>
    public class HintBar : MonoBehaviour
    {
        TextMeshProUGUI text;
        string persistent;
        string flash;
        float flashTime;
        float a;

        public static HintBar Create(RectTransform root)
        {
            var rt = UiKit.Rect("Hint", root).Anchor(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f)).Place(new Vector2(0f, 22f), new Vector2(1400f, 48f));
            var h = rt.gameObject.AddComponent<HintBar>();
            h.text = UiKit.Label(rt, "Text", "", Fonts.Body, 27f, new Color(1f, 0.95f, 0.86f), TextAlignmentOptions.Center);
            h.text.rectTransform.Fill();
            h.text.fontStyle = FontStyles.Italic;
            h.text.outlineWidth = 0.18f;
            h.text.outlineColor = new Color32(20, 12, 8, 200);
            TextBacking.Add(h.text, 0.9f, 34f, 12f);
            return h;
        }

        public void Set(string msg) => persistent = msg;

        public void Flash(string msg)
        {
            flash = msg;
            flashTime = 2.6f;
        }

        void Update()
        {
            string hover = null;
            if (InteractionSystem.I != null && InteractionSystem.I.Hovered != null) hover = InteractionSystem.I.Hovered.Hint;
            string show;
            Color c;
            if (flashTime > 0f) { flashTime -= Time.unscaledDeltaTime; show = flash; c = new Color(1f, 0.6f, 0.5f); }
            else { show = hover ?? persistent; c = new Color(1f, 0.95f, 0.86f); }
            if (UIRoot.ModalOpen) show = null;
            a = Mathf.MoveTowards(a, string.IsNullOrEmpty(show) ? 0f : 1f, Time.unscaledDeltaTime * 6f);
            if (!string.IsNullOrEmpty(show)) text.text = show;
            text.color = new Color(c.r, c.g, c.b, a);
            // while you're holding something, sit between the object's name and the control strip
            var rt = (RectTransform)transform;
            rt.localScale = Vector3.one * UiKit.TextScale;
            float y = InspectController.I != null && InspectController.I.Held != null ? 64f : 22f;
            rt.anchoredPosition = new Vector2(0f, Mathf.Lerp(rt.anchoredPosition.y, y, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime)));
        }
    }

    /// <summary>Bottom bar while inspecting: the object's name and the controls.</summary>
    public class InspectBar : MonoBehaviour
    {
        CanvasGroup group;
        TextMeshProUGUI title, controls, partHint;
        bool on;
        float a;
        ItemView item;
        int shownFound = -1;

        public static InspectBar Create(RectTransform root)
        {
            var rt = UiKit.Rect("InspectBar", root).Anchor(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f)).Place(new Vector2(0f, 64f), new Vector2(1500f, 150f));
            var b = rt.gameObject.AddComponent<InspectBar>();
            b.group = UiKit.Group(rt.gameObject);
            b.group.alpha = 0f;
            b.group.blocksRaycasts = false;
            // a soft dark band behind the text so it reads over the pale slip and desk
            var ramp = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int i = 0; i < 64; i++) ramp.SetPixel(0, i, new Color(0.03f, 0.02f, 0.02f, 0.62f * Mathf.Pow(1f - i / 63f, 1.4f)));
            ramp.Apply();
            var band = new GameObject("Band", typeof(RectTransform)).AddComponent<RawImage>();
            band.transform.SetParent(rt, false);
            band.texture = ramp;
            band.raycastTarget = false;
            band.rectTransform.Anchor(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f)).Place(new Vector2(0f, -64f), new Vector2(2400f, 300f));
            b.title = UiKit.Label(rt, "Title", "", Fonts.Title, 46f, new Color(1f, 0.93f, 0.8f), TextAlignmentOptions.Bottom);
            b.title.rectTransform.Anchor(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f)).Place(new Vector2(0f, 0f), new Vector2(0f, 60f));
            b.title.outlineWidth = 0.15f;
            b.title.outlineColor = new Color32(20, 12, 8, 200);
            b.partHint = UiKit.Label(rt, "Part", "", Fonts.Body, 26f, UiKit.Gold, TextAlignmentOptions.Center);
            b.partHint.rectTransform.Anchor(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f)).Place(new Vector2(0f, -62f), new Vector2(0f, 36f));
            b.partHint.outlineWidth = 0.2f;
            b.partHint.outlineColor = new Color32(20, 12, 8, 220);
            b.controls = UiKit.Label(rt, "Controls", "", Fonts.Type, 19f, new Color(0.92f, 0.86f, 0.74f, 0.9f), TextAlignmentOptions.Center);
            b.controls.rectTransform.Anchor(new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f)).Place(new Vector2(0f, -40f), new Vector2(0f, 30f));
            b.controls.outlineWidth = 0.2f;
            b.controls.outlineColor = new Color32(20, 12, 8, 220);
            // the name, the part under the cursor and the controls often land on the pale claim slip
            TextBacking.Add(b.title, 0.85f, 40f, 4f);
            TextBacking.Add(b.partHint, 0.85f, 26f, 6f);
            TextBacking.Add(b.controls, 0.72f, 30f, 8f);
            return b;
        }

        public void Show(ItemView item)
        {
            on = true;
            this.item = item;
            shownFound = -1;
            title.text = Title();
            controls.text = ControlsText();
            partHint.text = "";
        }

        bool padShown;

        string ControlsText()
        {
            padShown = GamepadInput.Active;
            bool lampOn = Lamp.I != null && Lamp.I.uvUnlocked;
            return GamepadInput.Prompt(
                $"DRAG  TURN   ·   SCROLL  CLOSER   ·   RIGHT CLICK  PUT DOWN   ·   T  ON THE TRAY{(lampOn ? "   ·   L  BLUE LAMP" : "")}",
                $"RIGHT STICK  TURN   ·   TRIGGERS  CLOSER   ·   A  OPEN / NOTE   ·   B  PUT DOWN   ·   X  ON THE TRAY{(lampOn ? "   ·   D-PAD UP  BLUE LAMP" : "")}");
        }

        public void SetPartHint(string s) => partHint.text = s ?? "";

        public void Hide() => on = false;

        /// <summary>How many of the object's case details you've noted (secrets stay secret: they aren't counted).</summary>
        public static (int found, int total) Findings(ObjectDef def)
        {
            var d = Director.I;
            if (def == null || d == null) return (0, 0);
            var details = def.CaseDetails.ToList();
            return (details.Count(x => d.IsDiscovered(def, x)), details.Count);
        }

        string Title()
        {
            var (found, total) = Findings(item.def);
            shownFound = found;
            if (total == 0) return item.def.name;
            string count = found >= total ? $"<color=#f0d088>·  all {total} findings noted</color>" : $"<color=#e0d0b0>·  findings {found} of {total}</color>";
            return $"{item.def.name}  <size=56%>{count}</size>";
        }

        void Update()
        {
            a = Mathf.MoveTowards(a, on ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            group.alpha = a;
            transform.localScale = Vector3.one * UiKit.TextScale;
            if (on && padShown != GamepadInput.Active) controls.text = ControlsText();   // the hands changed controls
            if (on && item != null && Findings(item.def).found != shownFound) title.text = Title();
        }
    }

    /// <summary>The glint that appears when the magnifier nears a hidden detail.</summary>
    public class HotspotMarker : MonoBehaviour
    {
        RectTransform rt;
        Image glint, ring;
        float strength, target;
        bool inRange;
        Vector2 pos;

        public static HotspotMarker Create(RectTransform root)
        {
            var rt = UiKit.Rect("Hotspot", root).Anchor(Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f)).Place(Vector2.zero, new Vector2(120f, 120f));
            var h = rt.gameObject.AddComponent<HotspotMarker>();
            h.rt = rt;
            h.ring = UiKit.Image(rt, "Ring", "ring", new Color(1f, 0.85f, 0.5f, 0f));
            h.ring.rectTransform.Fill();
            h.glint = UiKit.Image(rt, "Glint", "glint", new Color(1f, 0.92f, 0.7f, 0f));
            h.glint.rectTransform.Fill(18f);
            return h;
        }

        public void Show(Vector3 screen, float s, bool clickable)
        {
            float scale = UIRoot.I.root.rect.width / Mathf.Max(1, Screen.width);
            pos = new Vector2(screen.x, screen.y) * scale;
            target = s;
            inRange = clickable;
        }

        public void Hide() => target = 0f;

        void Update()
        {
            strength = Mathf.MoveTowards(strength, target, Time.unscaledDeltaTime * 6f);
            rt.anchoredPosition = pos;
            float t = Time.unscaledTime;
            float pulse = 0.8f + 0.2f * Mathf.Sin(t * 9f);
            glint.color = new Color(1f, 0.92f, 0.7f, strength * pulse * 0.95f);
            glint.rectTransform.localRotation = Quaternion.Euler(0f, 0f, t * 60f);
            glint.rectTransform.localScale = Vector3.one * (0.5f + strength * 0.6f) * pulse;
            ring.color = new Color(1f, 0.85f, 0.5f, inRange ? strength * 0.9f : 0f);
            ring.rectTransform.localScale = Vector3.one * (inRange ? 0.55f + 0.08f * Mathf.Sin(t * 6f) : 1f);
        }
    }

    /// <summary>"You noticed something": a handwritten card pops at the detail and flies to the slip.</summary>
    public class DiscoveryFX : MonoBehaviour
    {
        RectTransform root;

        public static DiscoveryFX Create(RectTransform parent)
        {
            var rt = UiKit.Rect("Discovery", parent).Fill();
            var d = rt.gameObject.AddComponent<DiscoveryFX>();
            d.root = rt;
            return d;
        }

        public void Play(Vector3 screen, string fact, bool secret)
        {
            StartCoroutine(Run(screen, fact, secret));
        }

        IEnumerator Run(Vector3 screen, string fact, bool secret)
        {
            float scale = root.rect.width / Mathf.Max(1, Screen.width);
            Vector2 p = new Vector2(screen.x, screen.y) * scale;
            // ring pulse at the spot
            var ring = UiKit.Image(root, "Pulse", "ring", secret ? new Color(0.75f, 0.65f, 1f) : UiKit.Gold);
            ring.rectTransform.Anchor(Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f)).Place(p, new Vector2(80f, 80f));
            StartCoroutine(Tween.Run(0.6f, k =>
            {
                ring.rectTransform.localScale = Vector3.one * (0.4f + k * 2.2f);
                ring.color = new Color(ring.color.r, ring.color.g, ring.color.b, 1f - k);
            }, Ease.OutCubic));

            var card = UiKit.Rect("Card", root).Anchor(Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            Vector2 cardPos = p + new Vector2(0f, 110f);
            cardPos.x = Mathf.Clamp(cardPos.x, 260f, root.rect.width - 260f);
            cardPos.y = Mathf.Clamp(cardPos.y, 120f, root.rect.height - 200f);
            card.Place(cardPos, new Vector2(460f, 130f));
            var g = UiKit.Group(card.gameObject);
            var sh = UiKit.Image(card, "Shadow", "paper_card", new Color(0, 0, 0, 0.35f), 28f);
            sh.rectTransform.Fill();
            sh.rectTransform.anchoredPosition = new Vector2(5f, -7f);
            var bg = UiKit.Image(card, "Paper", "paper_card", secret ? new Color(0.9f, 0.87f, 0.98f) : UiKit.Paper, 28f);
            bg.rectTransform.Fill();
            var head = UiKit.Label(card, "Head", secret ? "A SECRET" : "NOTED", Fonts.Type, 18f, secret ? new Color(0.4f, 0.3f, 0.6f) : UiKit.Oxblood, TextAlignmentOptions.TopLeft);
            head.rectTransform.Fill();
            head.margin = new Vector4(24f, 14f, 20f, 0f);
            var t = UiKit.Label(card, "Fact", fact, Fonts.Hand, 34f, UiKit.Ink, TextAlignmentOptions.MidlineLeft);
            t.rectTransform.Fill();
            t.margin = new Vector4(24f, 34f, 20f, 10f);
            t.lineSpacing = -16f;
            t.enableAutoSizing = true;
            t.fontSizeMin = 20f;
            t.fontSizeMax = 34f;
            card.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-3f, 3f));
            yield return Tween.Run(0.35f, k => { card.localScale = Vector3.one * Ease.OutBack(k); g.alpha = k; }, Ease.Linear);
            yield return new WaitForSeconds(secret ? 2.2f : 1.7f);
            Vector2 from = card.anchoredPosition;
            Vector2 to = new Vector2(root.rect.width * 0.42f, -40f);
            yield return Tween.Run(0.5f, k =>
            {
                card.anchoredPosition = Vector2.Lerp(from, to, k);
                card.localScale = Vector3.one * Mathf.Lerp(1f, 0.35f, k);
                g.alpha = 1f - k * k;
            }, Ease.InCubic);
            Destroy(card.gameObject);
            Destroy(ring.gameObject);
        }
    }

    /// <summary>Agnes's handwritten notes (rules and asides), slid in from the side.</summary>
    public class NotePopup : MonoBehaviour
    {
        RectTransform panel;
        CanvasGroup group;
        TextMeshProUGUI head, body, sign;
        bool open, dismissed;

        public bool Open => open;

        public static NotePopup Create(RectTransform root)
        {
            var rt = UiKit.Rect("Note", root).Anchor(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f)).Place(new Vector2(-40f, 40f), new Vector2(520f, 400f));
            var n = rt.gameObject.AddComponent<NotePopup>();
            n.panel = rt;
            n.group = UiKit.Group(rt.gameObject);
            n.group.alpha = 0f;
            n.group.blocksRaycasts = false;
            var sh = UiKit.Image(rt, "Shadow", "note_paper", new Color(0, 0, 0, 0.35f), 0f);
            sh.rectTransform.Fill();
            sh.rectTransform.anchoredPosition = new Vector2(7f, -9f);
            var bg = UiKit.Image(rt, "Paper", "note_paper", new Color(0.98f, 0.95f, 0.84f), 0f);
            bg.rectTransform.Fill();
            n.head = UiKit.Label(rt, "Head", "", Fonts.AgnesBold, 30f, UiKit.Oxblood, TextAlignmentOptions.TopLeft);
            n.head.rectTransform.Fill();
            n.head.margin = new Vector4(46f, 40f, 36f, 0f);
            n.body = UiKit.Label(rt, "Body", "", Fonts.Agnes, 33f, new Color(0.13f, 0.15f, 0.32f), TextAlignmentOptions.TopLeft);
            n.body.rectTransform.Fill();
            n.body.margin = new Vector4(46f, 86f, 40f, 60f);
            n.body.lineSpacing = -10f;
            n.body.enableAutoSizing = true;
            n.body.fontSizeMin = 22f;
            n.body.fontSizeMax = 33f;
            n.sign = UiKit.Label(rt, "Sign", "— A.", Fonts.Script, 30f, new Color(0.13f, 0.15f, 0.32f), TextAlignmentOptions.BottomRight);
            n.sign.rectTransform.Fill();
            n.sign.margin = new Vector4(40f, 0f, 48f, 30f);
            return n;
        }

        public IEnumerator Show(string title, string text, string signature = "— A.")
        {
            head.text = title ?? "";
            body.text = text;
            sign.text = signature;
            open = true;
            dismissed = false;
            AudioDirector.Play("paper_unfold", 0.7f);
            panel.localScale = Vector3.one * UiKit.TextScale;
            panel.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-2.5f, 1.5f));
            yield return Tween.Run(0.4f, k =>
            {
                group.alpha = k;
                panel.anchoredPosition = new Vector2(-40f + 420f * (1 - k), 40f);
            }, Ease.OutBack);
            float t = 0f;
            while (!dismissed)
            {
                t += Time.deltaTime;
                bool click = InputX.LeftDown || InputX.KeyDown(Key.Space) || InputX.KeyDown(Key.Enter);
                if (click && t > 0.35f && !UIRoot.ModalOpen) dismissed = true;
                if (Director.AutoAdvance && t > (Director.Cinematic ? 4.5f : 0.9f)) dismissed = true;
                yield return null;
            }
            AudioDirector.Play("paper_fold", 0.5f);
            yield return Tween.Run(0.3f, k =>
            {
                group.alpha = 1f - k;
                panel.anchoredPosition = new Vector2(-40f + 420f * k, 40f - 40f * k);
            }, Ease.InCubic);
            open = false;
        }
    }

    /// <summary>The day's title card.</summary>
    public class DayCard : MonoBehaviour
    {
        CanvasGroup group;
        TextMeshProUGUI day, title, date;
        Image bg;

        public static DayCard Create(RectTransform root)
        {
            var rt = UiKit.Rect("DayCard", root).Fill();
            var d = rt.gameObject.AddComponent<DayCard>();
            d.group = UiKit.Group(rt.gameObject);
            d.group.alpha = 0f;
            d.group.blocksRaycasts = false;
            d.bg = UiKit.Image(rt, "Bg", null, new Color(0.05f, 0.04f, 0.035f, 0.92f));
            d.bg.rectTransform.Fill();
            d.day = UiKit.Label(rt, "Day", "", Fonts.Type, 34f, UiKit.Brass, TextAlignmentOptions.Center);
            d.day.rectTransform.Anchor(new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f)).Place(new Vector2(0f, 110f), new Vector2(0f, 50f));
            d.day.characterSpacing = 18f;
            d.title = UiKit.Label(rt, "Title", "", Fonts.Title, 110f, new Color(0.96f, 0.9f, 0.78f), TextAlignmentOptions.Center);
            d.title.rectTransform.Anchor(new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f)).Place(new Vector2(0f, 10f), new Vector2(0f, 140f));
            d.date = UiKit.Label(rt, "Date", "", Fonts.TitleItalic, 36f, new Color(0.85f, 0.78f, 0.66f), TextAlignmentOptions.Center);
            d.date.rectTransform.Anchor(new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f)).Place(new Vector2(0f, -90f), new Vector2(0f, 50f));
            return d;
        }

        public IEnumerator Show(string dayLabel, string titleText, string dateText, float hold = 2.4f)
        {
            day.text = dayLabel.ToUpperInvariant();
            title.text = titleText;
            date.text = dateText;
            group.blocksRaycasts = true;
            yield return Tween.Run(0.7f, k => group.alpha = k, Ease.InOutSine);
            AudioDirector.Play("day_sting", 0.7f);
            float t = 0f;
            if (Director.AutoAdvance) hold = Mathf.Min(hold, 0.6f);
            while (t < hold)
            {
                t += Time.deltaTime;
                title.characterSpacing = Mathf.Lerp(4f, 0f, Ease.OutCubic(Mathf.Clamp01(t / hold)));
                if (t > 0.6f && (InputX.LeftDown || InputX.KeyDown(Key.Space))) break;
                yield return null;
            }
            yield return Tween.Run(0.8f, k => group.alpha = 1f - k, Ease.InOutSine);
            group.blocksRaycasts = false;
        }
    }

    public class Fader : MonoBehaviour
    {
        CanvasGroup group;
        Image img;

        public static Fader Create(RectTransform root)
        {
            var rt = UiKit.Rect("Fader", root).Fill();
            var f = rt.gameObject.AddComponent<Fader>();
            f.group = UiKit.Group(rt.gameObject);
            f.img = UiKit.Image(rt, "Img", null, Color.black);
            f.img.rectTransform.Fill();
            f.group.alpha = 1f;
            f.group.blocksRaycasts = false;
            return f;
        }

        public IEnumerator FadeTo(float a, float duration, Color? color = null)
        {
            if (color.HasValue) img.color = color.Value;
            float from = group.alpha;
            group.blocksRaycasts = a > 0.5f;
            yield return Tween.Run(duration, k => group.alpha = Mathf.Lerp(from, a, k), Ease.InOutSine, true);
        }

        public void Set(float a) => group.alpha = a;
    }

    /// <summary>Faint chevrons at the screen edges for turning to the cabinet or shelf.</summary>
    public class TurnArrows : MonoBehaviour
    {
        Image left, right;
        TextMeshProUGUI leftLabel, rightLabel;

        public static TurnArrows Create(RectTransform root)
        {
            var rt = UiKit.Rect("Arrows", root).Fill();
            var a = rt.gameObject.AddComponent<TurnArrows>();
            a.left = Arrow(rt, "Left", new Vector2(0f, 0.5f), 1f, () => CameraRig.I?.Turn(-1));
            a.right = Arrow(rt, "Right", new Vector2(1f, 0.5f), -1f, () => CameraRig.I?.Turn(1));
            a.leftLabel = UiKit.Label(a.left.transform.parent, "L", "", Fonts.Type, 18f, new Color(1f, 0.93f, 0.8f), TextAlignmentOptions.Center);
            a.leftLabel.rectTransform.Anchor(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f)).Place(new Vector2(0f, -4f), new Vector2(160f, 30f));
            a.rightLabel = UiKit.Label(a.right.transform.parent, "R", "", Fonts.Type, 18f, new Color(1f, 0.93f, 0.8f), TextAlignmentOptions.Center);
            a.rightLabel.rectTransform.Anchor(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f)).Place(new Vector2(0f, -4f), new Vector2(160f, 30f));
            return a;
        }

        static Image Arrow(RectTransform root, string name, Vector2 anchor, float dir, System.Action click)
        {
            var box = UiKit.Image(root, name, null, new Color(1f, 1f, 1f, 0f));
            box.rectTransform.Anchor(anchor, anchor, new Vector2(0.5f, 0.5f)).Place(new Vector2(dir * 52f, 0f), new Vector2(80f, 120f));
            box.raycastTarget = true;
            var icon = UiKit.Image(box.transform, "Icon", "chevron", new Color(1f, 0.95f, 0.85f, 0.25f));
            icon.rectTransform.Fill(10f);
            icon.rectTransform.localScale = new Vector3(dir < 0 ? 1f : -1f, 1f, 1f);
            var b = box.gameObject.AddComponent<PaperButton>();
            b.onClick = click;
            return icon;
        }

        void Update()
        {
            var rig = CameraRig.I;
            bool can = rig != null && rig.allowTurn && !UIRoot.ModalOpen && !(InspectController.I?.Held != null);
            float mx = InteractionSystem.MousePos.x / Mathf.Max(1, Screen.width);
            float nearL = Mathf.Clamp01(1f - mx / 0.12f), nearR = Mathf.Clamp01((mx - 0.88f) / 0.12f);
            bool showL = can && rig.view != View.Cabinet, showR = can && rig.view != View.Shelf;
            SetA(left, showL ? 0.12f + nearL * 0.6f : 0f);
            SetA(right, showR ? 0.12f + nearR * 0.6f : 0f);
            left.transform.parent.GetComponent<Image>().raycastTarget = showL;
            right.transform.parent.GetComponent<Image>().raycastTarget = showR;
            leftLabel.text = rig == null ? "" : rig.view == View.Shelf ? "DESK" : "DRAWERS";
            rightLabel.text = rig == null ? "" : rig.view == View.Cabinet ? "DESK" : "SHELF";
            leftLabel.color = new Color(1f, 0.93f, 0.8f, showL ? nearL : 0f);
            rightLabel.color = new Color(1f, 0.93f, 0.8f, showR ? nearR : 0f);
        }

        static void SetA(Image i, float a) => i.color = new Color(i.color.r, i.color.g, i.color.b, Mathf.MoveTowards(i.color.a, a, Time.unscaledDeltaTime * 3f));
    }
}
