using System;
using System.Collections;
using UnityEngine;

namespace LostAndFound
{
    /// <summary>Easing curves (t in 0..1).</summary>
    public static class Ease
    {
        public static float Linear(float t) => t;
        public static float InQuad(float t) => t * t;
        public static float OutQuad(float t) => 1f - (1f - t) * (1f - t);
        public static float InOutQuad(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
        public static float OutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        public static float InCubic(float t) => t * t * t;
        public static float InOutCubic(float t) => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        public static float OutQuint(float t) => 1f - Mathf.Pow(1f - t, 5f);
        public static float InOutSine(float t) => -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f;
        public static float OutSine(float t) => Mathf.Sin(t * Mathf.PI / 2f);

        public static float OutBack(float t) => OutBack(t, 1.70158f);
        public static float OutBack(float t, float s)
        {
            float c3 = s + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + s * Mathf.Pow(t - 1f, 2f);
        }

        public static float InBack(float t) => InBack(t, 1.70158f);
        public static float InBack(float t, float s) => (s + 1f) * t * t * t - s * t * t;

        public static float OutElastic(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            const float c4 = 2f * Mathf.PI / 3f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
        }

        public static float OutBounce(float t)
        {
            const float n1 = 7.5625f, d1 = 2.75f;
            if (t < 1f / d1) return n1 * t * t;
            if (t < 2f / d1) return n1 * (t -= 1.5f / d1) * t + 0.75f;
            if (t < 2.5f / d1) return n1 * (t -= 2.25f / d1) * t + 0.9375f;
            return n1 * (t -= 2.625f / d1) * t + 0.984375f;
        }
    }

    /// <summary>Coroutine-based tweens. Run them with a MonoBehaviour's StartCoroutine.</summary>
    public static class Tween
    {
        public static IEnumerator Run(float duration, Action<float> step, Func<float, float> ease = null, bool unscaled = false)
        {
            ease ??= Ease.OutCubic;
            float t = 0f;
            while (t < duration)
            {
                step(ease(Mathf.Clamp01(t / duration)));
                yield return null;
                t += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            }
            step(ease(1f));
        }

        public static IEnumerator Move(Transform tr, Vector3 to, float duration, Func<float, float> ease = null, bool local = false)
        {
            Vector3 from = local ? tr.localPosition : tr.position;
            return Run(duration, k =>
            {
                if (tr == null) return;
                var p = Vector3.LerpUnclamped(from, to, k);
                if (local) tr.localPosition = p; else tr.position = p;
            }, ease);
        }

        public static IEnumerator MoveRotate(Transform tr, Vector3 toPos, Quaternion toRot, float duration, Func<float, float> ease = null)
        {
            Vector3 p0 = tr.position;
            Quaternion r0 = tr.rotation;
            return Run(duration, k =>
            {
                if (tr == null) return;
                tr.position = Vector3.LerpUnclamped(p0, toPos, k);
                tr.rotation = Quaternion.SlerpUnclamped(r0, toRot, k);
            }, ease);
        }

        /// <summary>Move along a quadratic arc that rises `height` above the straight line.</summary>
        public static IEnumerator Arc(Transform tr, Vector3 toPos, Quaternion toRot, float height, float duration, Func<float, float> ease = null)
        {
            Vector3 p0 = tr.position;
            Quaternion r0 = tr.rotation;
            Vector3 mid = (p0 + toPos) * 0.5f + Vector3.up * height;
            return Run(duration, k =>
            {
                if (tr == null) return;
                float u = Mathf.Clamp01(k);
                Vector3 a = Vector3.LerpUnclamped(p0, mid, k);
                Vector3 b = Vector3.LerpUnclamped(mid, toPos, k);
                tr.position = Vector3.LerpUnclamped(a, b, k);
                tr.rotation = Quaternion.SlerpUnclamped(r0, toRot, u);
            }, ease ?? Ease.InOutCubic);
        }

        public static IEnumerator Scale(Transform tr, Vector3 to, float duration, Func<float, float> ease = null)
        {
            Vector3 from = tr.localScale;
            return Run(duration, k => { if (tr != null) tr.localScale = Vector3.LerpUnclamped(from, to, k); }, ease);
        }

        /// <summary>Squash-and-stretch punch: scales by (1 + amount * decaying sine).</summary>
        public static IEnumerator Punch(Transform tr, float amount, float duration, int vibrato = 2)
        {
            Vector3 baseScale = tr.localScale;
            yield return Run(duration, k =>
            {
                if (tr == null) return;
                float s = Mathf.Sin(k * Mathf.PI * vibrato) * (1f - k) * amount;
                tr.localScale = baseScale * (1f + s);
            }, Ease.Linear);
            if (tr != null) tr.localScale = baseScale;
        }

        public static IEnumerator Delay(float seconds, Action then)
        {
            yield return new WaitForSeconds(seconds);
            then?.Invoke();
        }

        public static IEnumerator Fade(CanvasGroup g, float to, float duration, bool unscaled = true)
        {
            float from = g.alpha;
            return Run(duration, k => { if (g != null) g.alpha = Mathf.Lerp(from, to, k); }, Ease.InOutSine, unscaled);
        }
    }

    /// <summary>A critically-damped-ish spring for one float (use for drawers, lids, camera).</summary>
    [Serializable]
    public struct Spring
    {
        public float value, velocity;

        /// <summary>Semi-implicit spring step. frequency in Hz, damping ratio (1 = critical, &lt;1 = overshoot).</summary>
        public float Step(float target, float frequency, float damping, float dt)
        {
            float w = 2f * Mathf.PI * frequency;
            float f = 1f + 2f * dt * damping * w;
            float oo = w * w;
            float hoo = dt * oo;
            float hhoo = dt * hoo;
            float detInv = 1f / (f + hhoo);
            float detX = f * value + dt * velocity + hhoo * target;
            float detV = velocity + hoo * (target - value);
            value = detX * detInv;
            velocity = detV * detInv;
            return value;
        }
    }

    public static class MathX
    {
        public static float Damp(float a, float b, float lambda, float dt) => Mathf.Lerp(a, b, 1f - Mathf.Exp(-lambda * dt));
        public static Vector3 Damp(Vector3 a, Vector3 b, float lambda, float dt) => Vector3.Lerp(a, b, 1f - Mathf.Exp(-lambda * dt));
        public static Quaternion Damp(Quaternion a, Quaternion b, float lambda, float dt) => Quaternion.Slerp(a, b, 1f - Mathf.Exp(-lambda * dt));
        public static float DampAngle(float a, float b, float lambda, float dt) => Mathf.LerpAngle(a, b, 1f - Mathf.Exp(-lambda * dt));
        public static float Remap(float v, float a0, float a1, float b0, float b1) => Mathf.Lerp(b0, b1, Mathf.InverseLerp(a0, a1, v));
    }
}
