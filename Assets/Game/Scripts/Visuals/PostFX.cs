using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LostAndFound
{
    /// <summary>
    /// Runtime post-processing: ACES, warm/teal split toning, bloom, vignette, grain, and depth of
    /// field that pulls focus to your hands while inspecting. Also drives the story's colour: each
    /// object given to the Grey Gentleman drains the station's saturation.
    /// </summary>
    public class PostFX : MonoBehaviour
    {
        public static PostFX I { get; private set; }
        Volume volume;
        VolumeProfile profile;
        DepthOfField dof;
        ColorAdjustments color;
        Vignette vignette;
        Bloom bloom;
        FilmGrain grain;
        SplitToning split;
        ChromaticAberration chroma;
        float inspectBlend, inspectTarget;
        float greyness;
        Color pulseColor;
        float pulse;
        float exposure;
        float slipFocus, slipFocusTarget;

        void Awake()
        {
            I = this;
            var go = new GameObject("PostVolume");
            go.transform.SetParent(transform, false);
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = profile;

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.72f);
            bloom.tint.Override(new Color(1f, 0.86f, 0.7f));

            vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.32f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(new Color(0.07f, 0.04f, 0.03f));

            color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.15f);
            color.contrast.Override(8f);
            color.saturation.Override(4f);
            color.colorFilter.Override(Color.white);

            split = profile.Add<SplitToning>(true);
            split.shadows.Override(new Color(0.36f, 0.52f, 0.58f));
            split.highlights.Override(new Color(0.78f, 0.62f, 0.42f));
            split.balance.Override(-12f);

            grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Medium1);
            grain.intensity.Override(0.16f);
            grain.response.Override(0.85f);

            chroma = profile.Add<ChromaticAberration>(true);
            chroma.intensity.Override(0.06f);

            dof = profile.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(3.5f);
            dof.gaussianEnd.Override(14f);
            dof.gaussianMaxRadius.Override(1.2f);
            dof.highQualitySampling.Override(true);

            Settings.Changed += ApplySettings;
            ApplySettings();
        }

        void OnDestroy() => Settings.Changed -= ApplySettings;

        void ApplySettings()
        {
            bool on = Settings.PostEffects;
            grain.active = on;
            chroma.active = on;
            dof.active = on;
        }

        public void SetInspect(bool on) => inspectTarget = on ? 1f : 0f;
        public void SetSlipFocus(bool on) => slipFocusTarget = on ? 1f : 0f;

        /// <summary>0..1: how much colour the Grey Gentleman has taken.</summary>
        public void SetGreyness(float g) => greyness = Mathf.Clamp01(g);

        public void Pulse(Color c, float strength)
        {
            pulseColor = c;
            pulse = strength;
        }

        public void SetExposure(float ev) => exposure = ev;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            inspectBlend = MathX.Damp(inspectBlend, inspectTarget, 5f, dt);
            slipFocus = MathX.Damp(slipFocus, slipFocusTarget, 5f, dt);
            // counter view: soft background (the concourse). Inspecting: everything past arm's length blurs.
            float start = Mathf.Lerp(3.5f, 0.55f, inspectBlend);
            float end = Mathf.Lerp(14f, 1.6f, inspectBlend);
            start = Mathf.Lerp(start, 1.2f, slipFocus * (1 - inspectBlend));
            end = Mathf.Lerp(end, 4.5f, slipFocus * (1 - inspectBlend));
            dof.gaussianStart.Override(start);
            dof.gaussianEnd.Override(end);
            dof.gaussianMaxRadius.Override(Mathf.Lerp(1.0f, 1.5f, inspectBlend));
            vignette.intensity.Override(0.3f + 0.12f * inspectBlend + pulse * 0.25f);
            vignette.color.Override(Color.Lerp(new Color(0.07f, 0.04f, 0.03f), pulseColor, Mathf.Clamp01(pulse)));
            color.saturation.Override(Mathf.Lerp(4f, -72f, greyness));
            color.postExposure.Override(0.35f + exposure);
            pulse = Mathf.MoveTowards(pulse, 0f, dt * 1.2f);
        }
    }
}
