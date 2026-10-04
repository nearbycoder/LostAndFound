using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LostAndFound.EditorTools
{
    /// <summary>
    /// One-shot project wiring: URP quality (soft shadows from the desk lamp, HDR, MSAA), material
    /// templates kept in Resources so their shader variants ship, the empty Main scene, and build
    /// settings. Run from the menu or with -executeMethod LostAndFound.EditorTools.ProjectSetup.Apply.
    /// </summary>
    public static class ProjectSetup
    {
        const string MatDir = "Assets/Game/Resources/Materials";
        const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Lost & Found/Apply Project Setup")]
        public static void Apply()
        {
            Directory.CreateDirectory(MatDir);
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var opaque = Ensure("M_Lit", lit);
            opaque.SetFloat("_Surface", 0f);
            opaque.EnableKeyword("_NORMALMAP");
            opaque.EnableKeyword("_EMISSION");
            opaque.SetColor("_EmissionColor", Color.black);
            opaque.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            opaque.SetTexture("_BumpMap", Resources.Load<Texture2D>("Textures/wood_n"));
            EditorUtility.SetDirty(opaque);

            var trans = Ensure("M_LitTransparent", lit);
            trans.SetFloat("_Surface", 1f);
            trans.SetFloat("_Blend", 0f);
            trans.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            trans.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            trans.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            trans.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            trans.SetFloat("_ZWrite", 0f);
            trans.SetOverrideTag("RenderType", "Transparent");
            trans.renderQueue = (int)RenderQueue.Transparent;
            trans.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            trans.EnableKeyword("_NORMALMAP");
            trans.EnableKeyword("_EMISSION");
            trans.SetColor("_EmissionColor", Color.black);
            trans.SetShaderPassEnabled("DepthOnly", false);
            trans.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(trans);

            ConfigureUrp();
            EnsureScene();

            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.companyName = "Nearby";
            PlayerSettings.productName = "Lost & Found";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SplashScreen.show = false;
            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSetup] applied");
        }

        static Material Ensure(string name, Shader shader)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            return m;
        }

        static void ConfigureUrp()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/")) continue; // never touch package defaults
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (asset == null) continue;
                var so = new SerializedObject(asset);
                void Set(string prop, float v)
                {
                    var p = so.FindProperty(prop);
                    if (p == null) { Debug.LogWarning($"[ProjectSetup] {path}: no {prop}"); return; }
                    switch (p.propertyType)
                    {
                        case SerializedPropertyType.Boolean: p.boolValue = v > 0.5f; break;
                        case SerializedPropertyType.Integer: case SerializedPropertyType.Enum: p.intValue = (int)v; break;
                        default: p.floatValue = v; break;
                    }
                }
                Set("m_SupportsHDR", 1);
                Set("m_MSAA", 4);
                Set("m_RenderScale", 1f);
                Set("m_MainLightRenderingMode", 1);
                Set("m_MainLightShadowsSupported", 1);
                Set("m_MainLightShadowmapResolution", 2048);
                Set("m_AdditionalLightsRenderingMode", 1);
                Set("m_AdditionalLightsPerObjectLimit", 8);
                Set("m_AdditionalLightShadowsSupported", 1);
                Set("m_AdditionalLightsShadowmapResolution", 4096);
                Set("m_AdditionalLightsShadowResolutionTierLow", 512);
                Set("m_AdditionalLightsShadowResolutionTierMedium", 1024);
                Set("m_AdditionalLightsShadowResolutionTierHigh", 2048);
                Set("m_ShadowDistance", 9f);
                Set("m_ShadowCascadeCount", 2);
                Set("m_SoftShadowsSupported", 1);
                Set("m_SoftShadowQuality", 3);
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(asset);
            }
            // SSAO tuning on the PC renderer (template includes the feature)
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                string dpath = AssetDatabase.GUIDToAssetPath(guid);
                if (!dpath.StartsWith("Assets/")) continue;
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(dpath);
                if (data == null) continue;
                foreach (var f in data.rendererFeatures)
                {
                    if (f == null || f.GetType().Name != "ScreenSpaceAmbientOcclusion") continue;
                    var so = new SerializedObject(f);
                    void S(string prop, float v)
                    {
                        var p = so.FindProperty("m_Settings." + prop);
                        if (p == null) return;
                        if (p.propertyType == SerializedPropertyType.Float) p.floatValue = v;
                        else if (p.propertyType == SerializedPropertyType.Boolean) p.boolValue = v > 0.5f;
                        else p.intValue = (int)v;
                    }
                    S("Intensity", 1.2f);
                    S("Radius", 0.12f);
                    S("DirectLightingStrength", 0.3f);
                    S("Falloff", 25f);
                    so.ApplyModifiedProperties();
                    f.SetActive(true);
                    EditorUtility.SetDirty(f);
                }
                EditorUtility.SetDirty(data);
            }
        }

        static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            if (File.Exists("Assets/Scenes/SampleScene.unity")) AssetDatabase.DeleteAsset("Assets/Scenes/SampleScene.unity");
        }
    }
}
