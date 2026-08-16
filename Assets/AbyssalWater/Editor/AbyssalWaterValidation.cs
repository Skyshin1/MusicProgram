using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MusicProgram.AbyssalWater.Editor
{
    /// <summary>
    /// Batch-safe validation and reference renders for the isolated water sample.
    /// </summary>
    public static class AbyssalWaterValidation
    {
        const string ScenePath = "Assets/AbyssalWater/Samples/AbyssalWaterShowcase.unity";
        const string SurfaceShaderPath = "Assets/AbyssalWater/Shaders/AbyssalWaterSurface.shader";
        const string UnderwaterShaderPath = "Assets/AbyssalWater/Shaders/AbyssalUnderwater.shader";
        const string RendererPath = "Assets/Settings/PC_Renderer.asset";
        const string PreviewFolder = "Assets/AbyssalWater/Samples/Validation";

        [MenuItem("Tools/Abyssal Water/Validate And Render Previews")]
        public static void ValidateAndRenderPreviews()
        {
            var failures = new List<string>();
            ValidateShader(SurfaceShaderPath, failures);
            ValidateShader(UnderwaterShaderPath, failures);

            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rendererData == null)
                failures.Add($"URP renderer data is missing: {RendererPath}");
            else if (!rendererData.rendererFeatures.OfType<AbyssalUnderwaterRendererFeature>()
                         .Any(feature => feature != null && feature.isActive))
                failures.Add("The active URP renderer has no enabled Abyssal underwater feature.");

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            var water = UnityEngine.Object.FindFirstObjectByType<AbyssalWaterSystem>();
            var driver = UnityEngine.Object.FindFirstObjectByType<AbyssalWaterShowcaseDriver>();
            if (camera == null) failures.Add("Showcase camera is missing.");
            if (water == null) failures.Add("AbyssalWaterSystem is missing.");
            if (driver == null) failures.Add("Showcase controls are missing.");

            if (water != null)
            {
                if (water.profile == null) failures.Add("Water profile is not assigned.");
                if (water.waterMaterial == null) failures.Add("Water material is not assigned.");
                if (water.dynamicWaveCompute == null) failures.Add("Dynamic-wave compute shader is not assigned.");
                if (!water.DynamicWavesAvailable) failures.Add("Dynamic-wave render textures were not created.");
                ValidateSurfaceQuery(water, failures);
                ValidateComputeImpulse(water, failures);
            }

            if (failures.Count > 0)
                throw new InvalidOperationException("Abyssal Water validation failed:\n- " +
                                                    string.Join("\n- ", failures));

            if (camera == null || water == null) return;
            Directory.CreateDirectory(PreviewFolder);
            RenderPreview(camera, water, "AbyssalWater_Above.png",
                new Vector3(0f, 7.5f, -17f), new Vector3(0f, -0.35f, 7f));
            RenderPreview(camera, water, "AbyssalWater_Waterline.png",
                new Vector3(0f, 0.12f, -12f), new Vector3(0f, -0.1f, 5f), true);
            RenderPreview(camera, water, "AbyssalWater_Underwater.png",
                new Vector3(0f, -5.5f, -11f), new Vector3(0f, -4f, 7f));
            AssetDatabase.Refresh();
            Debug.Log("Abyssal Water validation passed: shaders, renderer feature, scene references, " +
                      "surface query, compute impulse and three visual previews are valid.");
        }

        static void ValidateShader(string path, ICollection<string> failures)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null)
            {
                failures.Add($"Shader is missing: {path}");
                return;
            }

            if (!shader.isSupported) failures.Add($"Shader is unsupported on the active graphics API: {path}");
            foreach (var message in ShaderUtil.GetShaderMessages(shader))
            {
                if (!string.Equals(message.severity.ToString(), "Error", StringComparison.OrdinalIgnoreCase))
                    continue;
                failures.Add($"{path}:{message.line} {message.message}");
            }
        }

        static void ValidateSurfaceQuery(AbyssalWaterSystem water, ICollection<string> failures)
        {
            water.SampleSurface(new Vector3(3.25f, 0f, 7.5f), out var position,
                out var normal, out var velocity);
            if (!IsFinite(position) || !IsFinite(normal) || !IsFinite(velocity))
                failures.Add("CPU surface query returned a non-finite value.");
            if (normal.sqrMagnitude < 0.5f || normal.sqrMagnitude > 1.5f)
                failures.Add("CPU surface query returned an invalid normal.");
        }

        static void ValidateComputeImpulse(AbyssalWaterSystem water, ICollection<string> failures)
        {
            if (!water.DynamicWavesAvailable) return;
            water.EnqueueImpulse(new Vector3(0f, water.waterLevel, 0f), 2f, 1.5f);
            var step = typeof(AbyssalWaterSystem).GetMethod("StepDynamicSimulation",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var current = typeof(AbyssalWaterSystem).GetField("_dynamicCurrent",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (step == null || current == null)
            {
                failures.Add("Dynamic-wave validation hooks are unavailable.");
                return;
            }

            step.Invoke(water, new object[] { 1f / 60f, true });
            var texture = current.GetValue(water) as RenderTexture;
            if (texture == null || !texture.IsCreated())
                failures.Add("Dynamic-wave compute step did not keep a valid output texture.");
        }

        static void RenderPreview(Camera camera, AbyssalWaterSystem water, string fileName,
            Vector3 position, Vector3 lookAt, bool alignToWaterline = false)
        {
            const int width = 960;
            const int height = 540;
            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "Abyssal Validation Preview",
                antiAliasing = 1
            };
            renderTexture.Create();
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var previousPosition = camera.transform.position;
            var previousRotation = camera.transform.rotation;
            try
            {
                camera.targetTexture = renderTexture;
                camera.transform.position = position;
                water.viewer = camera.transform;
                water.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
                const float validationTime = 3.5f;
                var timeField = typeof(AbyssalWaterSystem).GetField("_waterTime",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                timeField?.SetValue(water, validationTime);
                water.profile.ApplyGlobals(validationTime);
                if (alignToWaterline)
                {
                    position.y = water.GetWaterHeight(position) + 0.015f;
                    camera.transform.position = position;
                }
                camera.transform.LookAt(lookAt);
                camera.Render();
                RenderTexture.active = renderTexture;
                var image = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply(false, false);
                var path = Path.Combine(PreviewFolder, fileName).Replace('\\', '/');
                File.WriteAllBytes(path, image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                camera.transform.SetPositionAndRotation(previousPosition, previousRotation);
                RenderTexture.active = previousActive;
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }

        static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
