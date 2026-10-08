using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LostAndFound
{
    /// <summary>
    /// What one step of Settings > Graphics fidelity sets. Pure data, so the table can be unit-tested; GraphicsQuality applies it.
    /// High is the game as designed (round 1's High, the pipeline asset's own values); Low and Medium trade for frame rate, and
    /// Ultra goes past it.
    /// </summary>
    public readonly struct FidelityStep
    {
        public readonly string name;
        /// <summary>Render scale (above 1 supersamples); 0 keeps the pipeline asset's own (1).</summary>
        public readonly float renderScale;
        /// <summary>Shadow map sizes; 0 keeps the asset's own (2048).</summary>
        public readonly int mainShadowRes, lampShadowRes;
        /// <summary>0 Low, 1 Medium, 2 the asset's own (High).</summary>
        public readonly int softShadows;
        public readonly int cascades;
        /// <summary>1 off, 4 for 4x MSAA.</summary>
        public readonly int msaa;
        /// <summary>0 FXAA, 1 SMAA Medium, 2 SMAA High.</summary>
        public readonly int postAA;
        /// <summary>0 off, 1 half resolution with 4 samples, 2 the asset's own (full, 8), 3 full with 12.</summary>
        public readonly int ambientOcclusion;
        public readonly bool depthOfField, hqDepthOfField;
        /// <summary>-1 each texture's own filtering, 0 the project's (forced on, up to 16x), 16 forced on at 16x everywhere.</summary>
        public readonly int anisotropic;
        /// <summary>The booth's reflection probe's cube size (0: none).</summary>
        public readonly int reflectionRes;
        /// <summary>Motes of dust in the lamp's light.</summary>
        public readonly int dust;
        /// <summary>A 64-bit HDR colour buffer (smoother gradients in the dark).</summary>
        public readonly bool hdr64;

        public FidelityStep(string name, float renderScale, int mainShadowRes, int lampShadowRes, int softShadows, int cascades, int msaa,
            int postAA, int ambientOcclusion, bool depthOfField, bool hqDepthOfField, int anisotropic, int reflectionRes, int dust, bool hdr64)
        {
            this.name = name; this.renderScale = renderScale; this.mainShadowRes = mainShadowRes; this.lampShadowRes = lampShadowRes;
            this.softShadows = softShadows; this.cascades = cascades; this.msaa = msaa; this.postAA = postAA; this.ambientOcclusion = ambientOcclusion;
            this.depthOfField = depthOfField; this.hqDepthOfField = hqDepthOfField; this.anisotropic = anisotropic;
            this.reflectionRes = reflectionRes; this.dust = dust; this.hdr64 = hdr64;
        }

        public const int Low = 0, Medium = 1, High = 2, Ultra = 3;

        public static readonly FidelityStep[] Steps =
        {
            new("Low",    0.7f,  512,  512, 0, 2, 1, 0, 0, false, false, -1,  0,   0, false),
            new("Medium", 0.85f, 1024, 1024, 1, 2, 1, 1, 1, true,  false, 0,  64,  24, false),
            new("High",   0f,    0,    0,    2, 2, 1, 2, 2, true,  false, 0, 128,  48, false),
            // (URP's high-quality bloom filtering was tried for Ultra: it about doubles the bloom here and washes out the desk)
            new("Ultra",  1.25f, 4096, 4096, 2, 4, 4, 2, 3, true,  true, 16, 256, 120, true),   // supersampled
        };

        public static int Count => Steps.Length;
        public static int Clamp(int level) => Mathf.Clamp(level, 0, Steps.Length - 1);
        public static FidelityStep At(int level) => Steps[Clamp(level)];

        /// <summary>One line for the settings card and the log.</summary>
        public string Summary => name switch
        {
            "Low" => "fastest: lower resolution, plain shadows, no ambient occlusion or depth of field",
            "Medium" => "lighter shadows and effects for a slower graphics card",
            "High" => "the desk as designed",
            _ => "sharper shadows and edges, finer ambient occlusion and blur; needs a strong graphics card",
        };
    }

    /// <summary>
    /// Settings > Graphics fidelity: Low, Medium, High (the default, the game as designed) or Ultra, applied at once (see
    /// FidelityStep). The pipeline asset's own values are put back on exit, so the editor's copy is never left altered.
    /// </summary>
    public class GraphicsQuality : MonoBehaviour
    {
        public static GraphicsQuality I { get; private set; }
        public static string[] Names => Array.ConvertAll(FidelityStep.Steps, s => s.name);

        /// <summary>A step forced for a moment by the fidelity bench (-1: none). Never saved.</summary>
        public static int BenchLevel = -1;

        /// <summary>The level in force: the bench's, -lafQuality 0..3 for test runs (not saved), or the setting.</summary>
        public static int Level => BenchLevel >= 0 ? FidelityStep.Clamp(BenchLevel)
            : int.TryParse(Game.Arg("-lafQuality") ?? "", out int q) ? FidelityStep.Clamp(q) : Settings.PictureQuality;
        public static FidelityStep Current => Without(FidelityStep.At(Level));

        /// <summary>-lafUltraWithout msaa,hdr,dof,ao,shadows,cascades (test runs): Ultra less those, to see what each adds.</summary>
        static FidelityStep Without(FidelityStep s)
        {
            string w = Game.Arg("-lafUltraWithout");
            if (w == null || s.name != "Ultra") return s;
            bool No(string k) => w.Contains(k);
            var h = FidelityStep.At(FidelityStep.High);
            return new FidelityStep(s.name, s.renderScale, No("shadows") ? h.mainShadowRes : s.mainShadowRes, No("shadows") ? h.lampShadowRes : s.lampShadowRes,
                s.softShadows, No("cascades") ? h.cascades : s.cascades, No("msaa") ? h.msaa : s.msaa, s.postAA, No("ao") ? h.ambientOcclusion : s.ambientOcclusion,
                s.depthOfField, No("dof") ? h.hqDepthOfField : s.hqDepthOfField, s.anisotropic, s.reflectionRes, s.dust, No("hdr") ? h.hdr64 : s.hdr64);
        }

        /// <summary>Raised after a step has been applied (the reflection probe and the dust follow it).</summary>
        public static event Action Applied;

        UniversalRenderPipelineAsset asset;
        float scale0, distance0;
        int mainRes0, addRes0, tierHigh0, cascades0, msaa0;
        Vector3 split4_0;
        HDRColorBufferPrecision hdr0;
        AnisotropicFiltering aniso0;
        ScriptableRendererFeature ssao;
        object ssaoSettings;
        bool ssaoActive0;
        (bool down, int samples, int blur) ssao0;
        int applied = -1;

        void Awake() => I = this;

        void Start()
        {
            asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (asset != null)
            {
                scale0 = asset.renderScale;
                mainRes0 = asset.mainLightShadowmapResolution;
                addRes0 = asset.additionalLightsShadowmapResolution;
                tierHigh0 = asset.additionalLightsShadowResolutionTierHigh;
                cascades0 = asset.shadowCascadeCount;
                split4_0 = asset.cascade4Split;
                distance0 = asset.shadowDistance;
                msaa0 = asset.msaaSampleCount;
                hdr0 = asset.hdrColorBufferPrecision;
                FindAmbientOcclusion();
            }
            aniso0 = QualitySettings.anisotropicFiltering;
            Settings.Changed += Apply;
            Apply();
        }

        void OnDestroy()
        {
            Settings.Changed -= Apply;
            if (I == this) I = null;
            QualitySettings.anisotropicFiltering = aniso0;
            Texture.SetGlobalAnisotropicFilteringLimits(-1, -1);
            if (asset == null) return;
            asset.renderScale = scale0;
            asset.mainLightShadowmapResolution = mainRes0;
            asset.additionalLightsShadowmapResolution = addRes0;
            SetTierHigh(tierHigh0);
            asset.shadowCascadeCount = cascades0;
            asset.cascade4Split = split4_0;
            asset.shadowDistance = distance0;
            asset.msaaSampleCount = msaa0;
            asset.hdrColorBufferPrecision = hdr0;
            if (ssao != null)
            {
                ssao.SetActive(ssaoActive0);
                SetAmbientOcclusion(ssao0.down, ssao0.samples, ssao0.blur);
            }
        }

        /// <summary>The renderer's ambient occlusion feature and its settings (internal in URP, so found by reflection; if a
        /// later URP renames them, ambient occlusion simply stays as the asset has it).</summary>
        void FindAmbientOcclusion()
        {
            try
            {
                foreach (var data in asset.rendererDataList)
                {
                    if (data == null) continue;
                    foreach (var f in data.rendererFeatures)
                        if (f is ScreenSpaceAmbientOcclusion) { ssao = f; break; }
                    if (ssao != null) break;
                }
                if (ssao == null) return;
                ssaoActive0 = ssao.isActive;
                ssaoSettings = typeof(ScreenSpaceAmbientOcclusion).GetField("m_Settings", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(ssao);
                if (ssaoSettings != null)
                    ssao0 = ((bool)Field("Downsample").GetValue(ssaoSettings), Convert.ToInt32(Field("Samples").GetValue(ssaoSettings)),
                             Convert.ToInt32(Field("BlurQuality").GetValue(ssaoSettings)));
            }
            catch (Exception e) { Debug.LogWarning($"[Quality] ambient occlusion can't be adjusted: {e.Message}"); ssaoSettings = null; }
        }

        FieldInfo Field(string name) => ssaoSettings.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);

        void SetAmbientOcclusion(bool downsample, int samples, int blur)
        {
            if (ssaoSettings == null) return;
            try
            {
                Field("Downsample").SetValue(ssaoSettings, downsample);
                var fs = Field("Samples");
                fs.SetValue(ssaoSettings, Enum.ToObject(fs.FieldType, samples));
                var fb = Field("BlurQuality");
                fb.SetValue(ssaoSettings, Enum.ToObject(fb.FieldType, blur));
            }
            catch (Exception e) { Debug.LogWarning($"[Quality] ambient occlusion can't be adjusted: {e.Message}"); ssaoSettings = null; }
        }

        void SetTierHigh(int res)
        {
            if (asset == null || asset.additionalLightsShadowResolutionTierHigh == res) return;
            try { typeof(UniversalRenderPipelineAsset).GetProperty("additionalLightsShadowResolutionTierHigh")?.GetSetMethod(true)?.Invoke(asset, new object[] { res }); }
            catch (Exception e) { Debug.LogWarning($"[Quality] the lamp's shadow size can't be set: {e.Message}"); }
        }

        public void Apply()
        {
            int q = Level;
            var s = Current;
            if (asset != null)
            {
                asset.renderScale = s.renderScale > 0f ? s.renderScale : scale0;
                asset.mainLightShadowmapResolution = s.mainShadowRes > 0 ? s.mainShadowRes : mainRes0;
                asset.additionalLightsShadowmapResolution = s.lampShadowRes > 0 ? s.lampShadowRes : addRes0;
                SetTierHigh(Mathf.Max(tierHigh0, s.lampShadowRes));   // the lamp asks for the High tier: let it fill a bigger atlas
                asset.shadowCascadeCount = s.cascades;
                if (s.cascades == 4) asset.cascade4Split = new Vector3(0.08f, 0.22f, 0.5f);   // most of the detail on the desk itself
                asset.msaaSampleCount = s.msaa;
                asset.hdrColorBufferPrecision = s.hdr64 ? HDRColorBufferPrecision._64Bits : hdr0;
                if (ssao != null)
                {
                    ssao.SetActive(s.ambientOcclusion > 0 && ssaoActive0);
                    switch (s.ambientOcclusion)
                    {
                        case 1: SetAmbientOcclusion(true, 2, 1); break;    // half resolution, 4 samples, Gaussian blur
                        case 3: SetAmbientOcclusion(false, 0, 0); break;   // full resolution, 12 samples, bilateral blur
                        default: SetAmbientOcclusion(ssao0.down, ssao0.samples, ssao0.blur); break;
                    }
                }
            }
            var soft = s.softShadows switch { 0 => SoftShadowQuality.Low, 1 => SoftShadowQuality.Medium, _ => SoftShadowQuality.UsePipelineSettings };
            foreach (var l in FindObjectsByType<Light>())
                if (l.shadows == LightShadows.Soft)
                {
                    if (!l.TryGetComponent<UniversalAdditionalLightData>(out var data)) data = l.gameObject.AddComponent<UniversalAdditionalLightData>();
                    data.softShadowQuality = soft;
                }
            var cam = Game.I != null ? Game.I.Cam : Camera.main;
            if (cam != null && cam.TryGetComponent<UniversalAdditionalCameraData>(out var cd))
            {
                cd.antialiasing = s.postAA == 0 ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                cd.antialiasingQuality = s.postAA == 2 ? AntialiasingQuality.High : AntialiasingQuality.Medium;
            }
            QualitySettings.anisotropicFiltering = s.anisotropic < 0 ? AnisotropicFiltering.Enable : s.anisotropic > 0 ? AnisotropicFiltering.ForceEnable : aniso0;
            if (s.anisotropic > 0) Texture.SetGlobalAnisotropicFilteringLimits(s.anisotropic, s.anisotropic);
            else Texture.SetGlobalAnisotropicFilteringLimits(-1, -1);   // Unity's defaults
            PostFX.I?.ApplySettings();
            if (q != applied)
                Debug.Log($"[Quality] {s.name}: render scale {(asset != null ? asset.renderScale : 1f):0.##}, shadows {(asset != null ? asset.mainLightShadowmapResolution : 0)}" +
                          $"/{(asset != null ? asset.additionalLightsShadowmapResolution : 0)}, MSAA {s.msaa}x, AO {s.ambientOcclusion}, reflections {s.reflectionRes}, dust {s.dust}");
            applied = q;
            Applied?.Invoke();
        }
    }
}
