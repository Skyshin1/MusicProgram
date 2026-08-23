// Quest 3 setup for the embedded WebGpuWater package.
// This intentionally does NOT create or reposition an XR Origin: projects can use
// their own XRI rig. It only prepares the water, its mobile renderer and Android API.
using System;
using System.Reflection;
using AbstractOcclusion.WebGpuWater;
using MusicProgram.WebGpuWaterDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MusicProgram.WebGpuWaterDemo.Editor
{
    public static class Quest3WaterSetup
    {
        const string MobileRendererPath = "Assets/Settings/Mobile_Renderer.asset";
        const string QualityPath = "Assets/WebGpuWaterDemo/Generated/Quest3WaterQuality.asset";
        const string SourceQualityPath = "Assets/WebGpuWater/Generated/WaterQuality.asset";

        [MenuItem("Tools/WebGPU Water/Configure Active Scene For Meta Quest 3")]
        static void ConfigureActiveScene()
        {
            WaterQuality quality = GetOrCreateQuestQuality();
            int waterCount = ConfigureWaterVolumes(quality);
            EnsureMobileRendererFeatures();
            ConfigureAndroidVulkan();
            AddActiveSceneToBuildSettings();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log($"[Quest3 Water] Configured {waterCount} WaterVolume component(s). " +
                      "XR Origin was not changed. Assign its child Main Camera to Water Volume > Camera " +
                      "if you replace the scene camera.");
            EditorUtility.DisplayDialog("Quest 3 water configured",
                $"Prepared {waterCount} water volume(s) for Quest 3.\n\n" +
                "• Vulkan-only Android graphics API\n" +
                "• Mobile Renderer water fog + caustic features\n" +
                "• Forced Quest low water quality (no real refraction / no god rays)\n\n" +
                "Your XR Origin was not created or moved. When you add it, use its child Main Camera " +
                "as the Water Volume Camera.", "OK");
        }

        [MenuItem("Tools/WebGPU Water/Configure Active Scene For Meta Quest 3", true)]
        static bool CanConfigureActiveScene() => !EditorApplication.isPlayingOrWillChangePlaymode;

        static WaterQuality GetOrCreateQuestQuality()
        {
            var quality = AssetDatabase.LoadAssetAtPath<WaterQuality>(QualityPath);
            if (quality == null)
            {
                if (AssetDatabase.LoadAssetAtPath<WaterQuality>(SourceQualityPath) != null)
                    AssetDatabase.CopyAsset(SourceQualityPath, QualityPath);
                else
                {
                    quality = ScriptableObject.CreateInstance<WaterQuality>();
                    AssetDatabase.CreateAsset(quality, QualityPath);
                }
                quality = AssetDatabase.LoadAssetAtPath<WaterQuality>(QualityPath);
            }

            var serialized = new SerializedObject(quality);
            serialized.FindProperty("selection").intValue = (int)WaterQuality.Selection.ForceLow;
            // Quest renders two high-resolution views. Keep the inexpensive water appearance,
            // but remove the two most expensive fullscreen/extra-camera paths.
            serialized.FindProperty("lowGodRays").boolValue = false;
            serialized.FindProperty("lowGodRaySteps").intValue = 0;
            serialized.FindProperty("lowRichReflections").boolValue = false;
            serialized.FindProperty("lowRealRefraction").boolValue = false;
            serialized.FindProperty("lowUnderwaterFog").intValue = (int)WaterQuality.UnderwaterMode.Simple;
            serialized.FindProperty("lowMaxFoamParticles").intValue = 512;
            serialized.FindProperty("lowRenderScale").floatValue = 0.8f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(quality);
            return quality;
        }

        static int ConfigureWaterVolumes(WaterQuality quality)
        {
            var xrOrigin = UnityEngine.Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
            Camera camera = xrOrigin != null && xrOrigin.Camera != null ? xrOrigin.Camera : Camera.main;
            var waters = UnityEngine.Object.FindObjectsByType<WaterVolume>(FindObjectsInactive.Include,
                                                                             FindObjectsSortMode.None);
            int configured = 0;
            foreach (WaterVolume water in waters)
            {
                if (water.gameObject.scene != SceneManager.GetActiveScene()) continue;

                var serialized = new SerializedObject(water);
                serialized.FindProperty("quality").objectReferenceValue = quality;
                serialized.FindProperty("configureCamera").boolValue = false;
                if (camera != null)
                    serialized.FindProperty("targetCamera").objectReferenceValue = camera;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                if (water.GetComponent<WebGpuWaterXRCameraBinder>() == null)
                    Undo.AddComponent<WebGpuWaterXRCameraBinder>(water.gameObject);
                EditorUtility.SetDirty(water);
                configured++;
            }
            return configured;
        }

        static void EnsureMobileRendererFeatures()
        {
            UnityEngine.Object renderer = AssetDatabase.LoadMainAssetAtPath(MobileRendererPath);
            if (renderer == null)
                throw new InvalidOperationException("Mobile URP renderer was not found at " + MobileRendererPath);

            EnsureFeature(renderer, typeof(WaterUnderwaterFogFeature), "WebGPU Water - Underwater Fog",
                          "underwaterFogShader", "AbstractOcclusion/WebGpuWater/WaterUnderwaterFog");
            EnsureFeature(renderer, typeof(WaterCausticProjectionFeature), "WebGPU Water - Caustic Projection",
                          "causticProjectionShader", "AbstractOcclusion/WebGpuWater/WaterCausticProjection");

            MethodInfo validate = renderer.GetType().GetMethod("ValidateRendererFeatures",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (validate == null || !(bool)validate.Invoke(renderer, null))
                throw new InvalidOperationException("Could not validate the Mobile Renderer features.");
            EditorUtility.SetDirty(renderer);
        }

        static void EnsureFeature(UnityEngine.Object renderer, Type featureType, string featureName,
                                  string shaderProperty, string shaderName)
        {
            UnityEngine.Object feature = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(MobileRendererPath))
                if (asset != null && asset.GetType() == featureType) { feature = asset; break; }

            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance(featureType);
                feature.name = featureName;
                AssetDatabase.AddObjectToAsset(feature, renderer);
                var rendererSerialized = new SerializedObject(renderer);
                var features = rendererSerialized.FindProperty("m_RendererFeatures");
                features.InsertArrayElementAtIndex(features.arraySize);
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
                rendererSerialized.ApplyModifiedPropertiesWithoutUndo();
            }

            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Shader not found: " + shaderName);
            var featureSerialized = new SerializedObject(feature);
            featureSerialized.FindProperty(shaderProperty).objectReferenceValue = shader;
            featureSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(feature);
        }

        static void ConfigureAndroidVulkan()
        {
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
        }

        static void AddActiveSceneToBuildSettings()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrWhiteSpace(scene.path)) return;
            foreach (var existing in EditorBuildSettings.scenes)
                if (existing.path == scene.path) return;

            var scenes = EditorBuildSettings.scenes;
            Array.Resize(ref scenes, scenes.Length + 1);
            scenes[scenes.Length - 1] = new EditorBuildSettingsScene(scene.path, true);
            EditorBuildSettings.scenes = scenes;
        }
    }
}
