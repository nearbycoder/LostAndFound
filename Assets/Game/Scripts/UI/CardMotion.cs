using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LostAndFound
{
    /// <summary>
    /// Cards that open and close instead of appearing and vanishing in one frame. Every card opened over the HUD without a
    /// fade of its own (Settings, the pause menu, Controls, Agnes's rules, the week's ledger, the Curiosities) fades in with
    /// its dim while the card itself rises a few pixels and settles from 97% to full size, in about a fifth of a second; a card
    /// put away with Close fades out in a little less, already gone as far as the game is concerned (the menus and the
    /// pointer pass through it). With Settings > Reduce motion both are instant. Time is unscaled, so the paused game
    /// animates its menus too.
    /// </summary>
    public class CardMotion : MonoBehaviour
    {
        public const float OpenTime = 0.2f, CloseTime = 0.14f;
        readonly HashSet<Transform> seen = new();

        /// <summary>A card on its way out: no longer counted as open by the menus or the text audit.</summary>
        public class Closing : MonoBehaviour { }

        void LateUpdate()
        {
            var ui = UIRoot.I;
            if (ui == null) return;
            seen.RemoveWhere(t => t == null);
            for (int i = ui.HudCount; i < ui.root.childCount; i++)
            {
                var c = ui.root.GetChild(i);
                if (!seen.Add(c)) continue;
                // a card that fades itself (the title, the day's ledger, the ending, the week) is left to it
                if (c.GetComponent<CanvasGroup>() != null || c.GetComponent<Closing>() != null) continue;
                if (!Settings.ReduceMotion) StartCoroutine(Open((RectTransform)c));
            }
        }

        static bool FullScreen(RectTransform rt) => rt.anchorMin == Vector2.zero && rt.anchorMax == Vector2.one;

        IEnumerator Open(RectTransform panel)
        {
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            // the card (and anything else that isn't a full-screen dim) rises and settles; the dim only fades
            var moving = new List<(RectTransform rt, Vector2 pos, Vector3 scale)>();
            foreach (Transform t in panel)
                if (t is RectTransform rt && !FullScreen(rt)) moving.Add((rt, rt.anchoredPosition, rt.localScale));
            for (float t = 0f; panel != null && t < OpenTime; t += Time.unscaledDeltaTime)
            {
                if (panel.GetComponent<Closing>() != null) yield break;
                float k = Ease.OutCubic(Mathf.Clamp01(t / OpenTime));
                group.alpha = k;
                foreach (var (rt, pos, scale) in moving)
                    if (rt != null) { rt.anchoredPosition = pos + new Vector2(0f, -22f * (1f - k)); rt.localScale = scale * Mathf.Lerp(0.97f, 1f, k); }
                yield return null;
            }
            if (panel == null || panel.GetComponent<Closing>() != null) yield break;
            foreach (var (rt, pos, scale) in moving) if (rt != null) { rt.anchoredPosition = pos; rt.localScale = scale; }
            Destroy(group);   // as it was built: nothing left behind that the card's own code doesn't expect
        }

        /// <summary>Put a card away: it stops taking clicks at once and fades out, then is destroyed.</summary>
        public static void Close(RectTransform panel)
        {
            if (panel == null) return;
            if (Settings.ReduceMotion || UIRoot.I == null || !panel.gameObject.activeInHierarchy) { Destroy(panel.gameObject); return; }
            panel.gameObject.AddComponent<Closing>();
            var group = panel.GetComponent<CanvasGroup>();
            if (group == null) group = panel.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            foreach (var mb in panel.GetComponents<MonoBehaviour>()) if (!(mb is Closing)) mb.enabled = false;   // its Esc handlers and the like
            var host = UIRoot.I.GetComponent<CardMotion>();
            if (host != null) host.StartCoroutine(FadeOut(panel, group));
            else Destroy(panel.gameObject);
        }

        static IEnumerator FadeOut(RectTransform panel, CanvasGroup group)
        {
            float from = group.alpha;
            for (float t = 0f; panel != null && t < CloseTime; t += Time.unscaledDeltaTime)
            {
                group.alpha = from * (1f - Ease.InQuad(t / CloseTime));
                yield return null;
            }
            if (panel != null) Destroy(panel.gameObject);
        }
    }
}
