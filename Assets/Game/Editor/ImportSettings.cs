using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LostAndFound.EditorTools
{
    /// <summary>Import rules for generated assets: Blender FBX, synthesized audio, generated textures.</summary>
    public class ImportSettings : AssetPostprocessor
    {
        /// <summary>Re-run these rules over the models and UI textures after changing them (a fresh clone imports with them
        /// anyway): Tools/unity.sh run LostAndFound.EditorTools.ImportSettings.ReimportModelsAndUi</summary>
        public static void ReimportModelsAndUi()
        {
            var paths = AssetDatabase.FindAssets("", new[] { "Assets/Game/Resources/Models", "Assets/Game/Resources/Textures/UI" })
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => !AssetDatabase.IsValidFolder(p)).Distinct().ToList();
            AssetDatabase.StartAssetEditing();
            try { foreach (var p in paths) AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate); }
            finally { AssetDatabase.StopAssetEditing(); }
            Debug.Log($"[Import] reimported {paths.Count} models and UI textures");
        }

        void OnPreprocessModel()
        {
            if (!assetPath.Contains("/Resources/Models/")) return;
            var m = (ModelImporter)assetImporter;
            m.globalScale = 1f;
            m.useFileScale = true;
            m.bakeAxisConversion = false;
            m.importAnimation = false;
            m.animationType = ModelImporterAnimationType.None;
            m.importCameras = false;
            m.importLights = false;
            m.importBlendShapes = false;
            m.importVisibility = false;
            m.addCollider = false;
            m.isReadable = true;                       // runtime MeshColliders for inspect raycasts
            m.importNormals = ModelImporterNormals.Import;
            m.importTangents = ModelImporterTangents.CalculateMikk;
            // quantised vertices: about half the download, checked by the hotspot audit and side-by-side screenshots
            m.meshCompression = ModelImporterMeshCompression.Medium;
            m.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }

        void OnPreprocessAudio()
        {
            if (!assetPath.Contains("/Resources/")) return;
            var a = (AudioImporter)assetImporter;
            bool music = assetPath.Contains("/Resources/Music/") || assetPath.Contains("/amb_");
            var s = a.defaultSampleSettings;
            s.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = music ? 0.7f : 0.85f;
            s.preloadAudioData = !music;
            a.defaultSampleSettings = s;
            a.forceToMono = false;
            a.loadInBackground = music;
        }

        void OnPreprocessTexture()
        {
            if (assetPath.StartsWith("Assets/Game/Icon/"))
            {
                // app icons: full quality, no mips, transparent corners kept
                var icon = (TextureImporter)assetImporter;
                icon.textureType = TextureImporterType.Default;
                icon.alphaIsTransparency = true;
                icon.mipmapEnabled = false;
                icon.npotScale = TextureImporterNPOTScale.None;
                icon.textureCompression = TextureImporterCompression.Uncompressed;
                icon.isReadable = true;
                return;
            }
            if (!assetPath.Contains("/Resources/")) return;
            var t = (TextureImporter)assetImporter;
            string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            if (assetPath.Contains("/Textures/UI/"))
            {
                t.textureType = file.StartsWith("cursor_") ? TextureImporterType.Cursor : TextureImporterType.Default;
                t.mipmapEnabled = false;
                t.wrapMode = TextureWrapMode.Clamp;
                t.alphaIsTransparency = true;
                t.npotScale = TextureImporterNPOTScale.None;
                // cursors are read on the CPU and stay exact; paper and cards compress to BC7 without visible loss
                t.textureCompression = file.StartsWith("cursor_") ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
                if (file.StartsWith("cursor_")) t.isReadable = true;
                return;
            }
            if (assetPath.Contains("/Photos/"))
            {
                t.textureType = TextureImporterType.Default;
                t.wrapMode = TextureWrapMode.Clamp;
                t.mipmapEnabled = true;
                t.npotScale = TextureImporterNPOTScale.None;
                t.textureCompression = TextureImporterCompression.CompressedHQ;
                return;
            }
            if (file.EndsWith("_n"))
            {
                t.textureType = TextureImporterType.NormalMap;
                t.wrapMode = TextureWrapMode.Repeat;
            }
            else
            {
                t.textureType = TextureImporterType.Default;
                bool tiling = file.EndsWith("_a");
                t.wrapMode = tiling ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                t.alphaIsTransparency = !tiling;
            }
            t.mipmapEnabled = true;
            t.anisoLevel = 4;
            t.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
