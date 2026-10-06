using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LostAndFound
{
    /// <summary>
    /// Settings > Picture quality. High is the game as designed; Medium and Low trade render scale, shadow
    /// resolution and softness, anti-aliasing and (on Low) depth of field for frame rate on weaker GPUs.
    /// Changes apply at once. The pipeline asset's own values are put back on exit, so the editor's copy
    /// is never left altered.
    /// </summary>
    public class GraphicsQuality : MonoBehaviour
    {
        public static readonly string[] Names = { "Low", "Medium", "High" };

        /// <summary>The level in force: the setting, or -lafQuality 0|1|2 for test runs (not saved).</summary>
        public static int Level => int.TryParse(Game.Arg("-lafQuality") ?? "", out int q) ? Mathf.Clamp(q, 0, 2) : Settings.PictureQuality;
        UniversalRenderPipelineAsset asset;
        float scale0;
        int mainRes0, addRes0;
        int applied = -1;

        void Start()
        {
            asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (asset != null)
            {
                scale0 = asset.renderScale;
                mainRes0 = asset.mainLightShadowmapResolution;
                addRes0 = asset.additionalLightsShadowmapResolution;
            }
            Settings.Changed += Apply;
            Apply();
        }

        void OnDestroy()
        {
            Settings.Changed -= Apply;
            if (asset == null) return;
            asset.renderScale = scale0;
            asset.mainLightShadowmapResolution = mainRes0;
            asset.additionalLightsShadowmapResolution = addRes0;
        }

        void Apply()
        {
            int q = Level;
            if (q != applied) Debug.Log($"[Quality] {Names[q]}{(asset != null ? $": render scale {(q switch { 0 => 0.7f, 1 => 0.85f, _ => scale0 }):0.##}" : "")}");
            applied = q;
            if (asset != null)
            {
                asset.renderScale = q switch { 0 => 0.7f, 1 => 0.85f, _ => scale0 };
                asset.mainLightShadowmapResolution = q switch { 0 => 512, 1 => 1024, _ => mainRes0 };
                asset.additionalLightsShadowmapResolution = q switch { 0 => 512, 1 => 1024, _ => addRes0 };
            }
            var soft = q switch { 0 => SoftShadowQuality.Low, 1 => SoftShadowQuality.Medium, _ => SoftShadowQuality.UsePipelineSettings };
            foreach (var l in FindObjectsByType<Light>())
                if (l.shadows == LightShadows.Soft)
                {
                    if (!l.TryGetComponent<UniversalAdditionalLightData>(out var data)) data = l.gameObject.AddComponent<UniversalAdditionalLightData>();
                    data.softShadowQuality = soft;
                }
            var cam = Game.I != null ? Game.I.Cam : Camera.main;
            if (cam != null && cam.TryGetComponent<UniversalAdditionalCameraData>(out var cd))
            {
                cd.antialiasing = q == 0 ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                cd.antialiasingQuality = q == 2 ? AntialiasingQuality.High : AntialiasingQuality.Medium;
            }
        }
    }
}
