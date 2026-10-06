using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace LostAndFound
{
    /// <summary>The brass counter bell: ring for the next claimant.</summary>
    public class Bell : Interactable
    {
        public Transform plunger;
        public override CursorKind Cursor => CursorKind.Bell;
        public override string Hint => Director.I != null && Director.I.CanRing ? GamepadInput.Prompt("Ring for the next claimant  [Space]", "Ring for the next claimant  [A]") : null;
        public override bool Interactive => base.Interactive && Director.I != null && Director.I.CanRing;

        public override void OnClick() => Ring();

        public void Ring()
        {
            StartCoroutine(Press());
            AudioDirector.Play("bell", 0.9f, Random.Range(0.98f, 1.02f));
            Director.I?.RingBell();
        }

        IEnumerator Press()
        {
            var t = plunger != null ? plunger : transform;
            Vector3 p = t.localPosition;
            yield return Tween.Run(0.06f, k => t.localPosition = p + Vector3.down * 0.006f * k, Ease.OutQuad);
            yield return Tween.Run(0.25f, k => t.localPosition = p + Vector3.down * 0.006f * (1 - k), Ease.OutElastic);
            t.localPosition = p;
        }
    }

    /// <summary>Agnes's banker's lamp. From Thursday its blue filter shows hidden ink.</summary>
    public class Lamp : Interactable
    {
        public static Lamp I { get; private set; }
        public Light spot;
        public Renderer bulb;
        public bool uvUnlocked;
        public bool UV { get; private set; }
        public static event System.Action<bool> UVChanged;
        readonly Color warm = new(1f, 0.78f, 0.52f);
        readonly Color blue = new(0.42f, 0.32f, 1f);
        float warmIntensity;
        float flicker;

        public override CursorKind Cursor => CursorKind.Hand;
        public override string Hint => uvUnlocked ? (UV ? GamepadInput.Prompt("Switch the blue filter off  [L]", "Switch the blue filter off  [D-pad up]") : GamepadInput.Prompt("Switch on Agnes's blue filter  [L]", "Switch on Agnes's blue filter  [D-pad up]")) : "Agnes's lamp";

        void Awake() => I = this;

        public void Init(Light l)
        {
            spot = l;
            warmIntensity = l.intensity;
        }

        public override void OnClick()
        {
            if (uvUnlocked) ToggleUV();
            else { AudioDirector.Play("lamp_click", 0.6f); flicker = 0.4f; }
        }

        public void ToggleUV()
        {
            if (!uvUnlocked) return;
            UV = !UV;
            AudioDirector.Play("lamp_click", 0.7f);
            AudioDirector.Play(UV ? "uv_on" : "uv_off", 0.5f);
            flicker = 0.25f;
            UVChanged?.Invoke(UV);
        }

        public void ForceOff()
        {
            if (!UV) return;
            UV = false;
            UVChanged?.Invoke(false);
        }

        void Update()
        {
            if (spot == null) return;
            Color target = UV ? blue : warm;
            spot.color = Color.Lerp(spot.color, target, 1f - Mathf.Exp(-10f * Time.deltaTime));
            float i = UV ? warmIntensity * 0.75f : warmIntensity;
            if (flicker > 0f)
            {
                flicker -= Time.deltaTime;
                i *= Random.value < 0.5f ? 0.35f : 1f;
            }
            spot.intensity = i;
            if (bulb != null)
            {
                var m = bulb.material;
                MaterialLibrary.SetEmission(m, (UV ? blue : warm) * (i / Mathf.Max(0.01f, warmIntensity)) * 2.5f);
            }
        }
    }

    /// <summary>The ticket printer: chatters out a receipt for each decision, then spikes it.</summary>
    public class TicketPrinter : MonoBehaviour
    {
        public static TicketPrinter I { get; private set; }
        public Transform slot;      // where paper emerges (its forward = out)
        public Transform spike;     // receipts get impaled here
        public Transform lever;
        readonly List<GameObject> spiked = new();
        public bool Printing { get; private set; }

        void Awake() => I = this;

        public IEnumerator Print(string[] lines, Verdict v)
        {
            Printing = true;
            if (lever != null) StartCoroutine(Pull());
            var go = new GameObject("Receipt");
            go.transform.SetParent(slot, false);
            const float w = 0.058f, h = 0.10f;
            var paper = new GameObject("Paper");
            paper.transform.SetParent(go.transform, false);
            paper.transform.localPosition = new Vector3(0f, 0f, h / 2f);
            paper.AddComponent<MeshFilter>().sharedMesh = ProcMesh.Paper(w, h, 0.0003f);
            paper.AddComponent<MeshRenderer>().sharedMaterial = DeskMaterials.Receipt;
            var t = Text.World(paper.transform, "", Fonts.Receipt, 0.0042f, new Color(0.12f, 0.12f, 0.14f));
            t.alignment = TextAlignmentOptions.Top;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.rectTransform.sizeDelta = new Vector2(w - 0.006f, h - 0.006f);
            t.transform.localRotation = Quaternion.Euler(90f, 180f, 0f);
            t.transform.localPosition = new Vector3(0f, 0.0005f, 0f);
            string ink = v switch { Verdict.Return => "#2a6b3a", Verdict.Seal => "#2e2a3c", _ => "#9a2a22" };
            t.text = string.Join("\n", lines).Replace("{V}", $"<color={ink}><b>") .Replace("{/V}", "</b></color>");
            // start hidden inside the printer and feed out in steps
            go.transform.localPosition = new Vector3(0f, 0f, -h);
            AudioDirector.Play("printer", 0.8f);
            int steps = 9;
            for (int i = 1; i <= steps; i++)
            {
                float z = Mathf.Lerp(-h, -0.006f, i / (float)steps);
                yield return Tween.Run(0.07f, k => go.transform.localPosition = new Vector3(0f, 0f, Mathf.Lerp(go.transform.localPosition.z, z, k)), Ease.OutQuad);
                yield return new WaitForSeconds(0.035f);
            }
            yield return new WaitForSeconds(0.25f);
            AudioDirector.Play("paper_tear", 0.7f, Random.Range(0.95f, 1.05f));
            go.transform.SetParent(null, true);
            // fly to the spike and drop onto it
            Vector3 top = spike.position + Vector3.up * (0.09f - spiked.Count * 0.0018f);
            Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            yield return Tween.Arc(go.transform, top + Vector3.up * 0.04f, rot, 0.06f, 0.4f);
            yield return Tween.Move(go.transform, top - Vector3.up * 0.065f, 0.12f, Ease.InQuad);
            go.transform.SetParent(spike, true);
            AudioDirector.Play("paper_spike", 0.5f);
            spiked.Add(go);
            Printing = false;
        }

        IEnumerator Pull()
        {
            Quaternion r = lever.localRotation;
            yield return Tween.Run(0.15f, k => lever.localRotation = r * Quaternion.Euler(-35f * k, 0f, 0f), Ease.OutQuad);
            yield return Tween.Run(0.4f, k => lever.localRotation = r * Quaternion.Euler(-35f * (1 - k), 0f, 0f), Ease.OutElastic);
        }

        public void ClearSpike()
        {
            foreach (var g in spiked) Destroy(g);
            spiked.Clear();
        }
    }
}
