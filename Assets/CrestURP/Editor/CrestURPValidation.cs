using System;
using System.IO;
using System.Linq;
using System.Text;
using Crest;
using MusicProgram.CrestURP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.XR.CoreUtils;

namespace MusicProgram.CrestURP.Editor
{
    /// <summary>
    /// Runs inside the real project Editor so shader import is checked by the
    /// same Unity/URP version and graphics backend that will render the sample.
    /// </summary>
    [InitializeOnLoad]
    public static class CrestURPValidation
    {
        const string ScenePath = "Assets/CrestURP/Samples/CrestURPShowcase.unity";
        const string ReportPath = "Assets/CrestURP/Samples/CrestURPValidationReport.txt";
        const string PreviewPath = "Assets/CrestURP/Samples/CrestURPShowcasePreview.png";
        const string SurfacePreviewPath = "Assets/CrestURP/Samples/CrestURPSurfacePreview.png";
        const string PhysicalCausticsPreviewPath = "Assets/CrestURP/Samples/CrestURPPhysicalCausticsPreview.png";
        const string CausticGainDebugPath = "Assets/CrestURP/Samples/CrestURPCausticGainDebug.png";
        const string WaveSlopeDebugPath = "Assets/CrestURP/Samples/CrestURPWaveSlopeDebug.png";
        const string DisplacementHeightDebugPath = "Assets/CrestURP/Samples/CrestURPDisplacementHeightDebug.png";
        const string PlayPreviewPath = "Assets/CrestURP/Samples/CrestURPPlayModePreview.png";
        const string PlayValidationPath = "Assets/CrestURP/Samples/CrestURPPlayModeValidation.txt";
        const string PlayValidationVersion = "1.3.9";
        const string RendererPath = "Assets/Settings/PC_Renderer.asset";
        const string ShowcaseBuildVersionPath = "Assets/CrestURP/Samples/CrestURPBuildVersion.txt";
        const string RequiredShowcaseBuildVersion = "1.3.2";
        const string SessionKey = "MusicProgram.CrestURP.Validation.2026-08-16.11";
        const string PlayTestKey = "MusicProgram.CrestURP.PlayTest.Running";
        const string PlayTestErrorsKey = "MusicProgram.CrestURP.PlayTest.Errors";

        static int s_CaptureFrame;
        static bool s_WaitingForCapture;
        static bool s_PreserveLiveCausticsCaptures;

        static CrestURPValidation()
        {
            EditorApplication.delayCall += ValidateAfterImport;
            EditorApplication.delayCall += AutoRunPlayModeTestOnce;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            if (SessionState.GetBool(PlayTestKey, false))
            {
                Application.logMessageReceived += OnPlayModeLog;
            }
        }

        static void ValidateAfterImport()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += ValidateAfterImport;
                return;
            }

            if (SessionState.GetBool(SessionKey, false) || !File.Exists(ScenePath)) return;
            SessionState.SetBool(SessionKey, true);

            try
            {
                ValidateAndCapture();
            }
            catch (Exception exception)
            {
                File.WriteAllText(ReportPath,
                    $"Crest URP validation failed at {DateTime.Now:O}\n{exception}\n");
                AssetDatabase.ImportAsset(ReportPath, ImportAssetOptions.ForceUpdate);
                Debug.LogException(exception);
            }
        }

        [MenuItem("Tools/Crest URP/Validate Shaders And Capture Preview")]
        public static void ValidateAndCapture()
        {
            AssetDatabase.ImportAsset("Assets/CrestURP/Shaders/CrestOceanURP.shader",
                ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset("Assets/CrestURP/Shaders/CrestUnderwaterURP.shader",
                ImportAssetOptions.ForceUpdate);

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var report = new StringBuilder(2048);
            var hasErrors = false;

            report.AppendLine("Crest Water 4 -> URP validation");
            report.AppendLine($"Generated: {DateTime.Now:O}");
            report.AppendLine($"Unity: {Application.unityVersion}");
            report.AppendLine($"Graphics API: {SystemInfo.graphicsDeviceType}");
            report.AppendLine($"Scene: {scene.path}");
            report.AppendLine();

            ValidateShader("Crest/URP/Ocean", report, ref hasErrors);
            ValidateShader("Crest/URP/Underwater", report, ref hasErrors);

            var ocean = UnityEngine.Object.FindFirstObjectByType<OceanRenderer>();
            var fft = UnityEngine.Object.FindFirstObjectByType<ShapeFFT>();
            var controller = UnityEngine.Object.FindFirstObjectByType<CrestURPWaterController>();
            var waveController = UnityEngine.Object.FindFirstObjectByType<CrestURPWaveController>();
            var camera = Camera.main;
            var planarReflection = camera != null ? camera.GetComponent<CrestURPPlanarReflection>() : null;
            var dynamicWaveEmitters = UnityEngine.Object.FindObjectsByType<CrestURPDynamicWaveEmitter>(
                FindObjectsSortMode.None);
            var dynamicWavesReady = ocean != null && ocean.CreateDynamicWaveSim &&
                                    ocean.SimSettingsDynamicWaves != null;
            var xrOrigin = UnityEngine.Object.FindFirstObjectByType<XROrigin>();
            var trackedPoseDriver = camera != null ? camera.GetComponent<TrackedPoseDriver>() : null;
            var xrReady = camera != null && xrOrigin != null && xrOrigin.Camera == camera &&
                          xrOrigin.CameraFloorOffsetObject != null && trackedPoseDriver != null;
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            var underwaterFeature = renderer != null
                ? renderer.rendererFeatures.FirstOrDefault(feature =>
                    feature != null && feature.name == "Crest URP Underwater + Waterline")
                : null;

            report.AppendLine($"Crest OceanRenderer: {(ocean != null ? "PASS" : "FAIL")}");
            report.AppendLine($"Crest FFT waves: {(fft != null ? "PASS" : "FAIL")}");
            report.AppendLine($"URP water controller: {(controller != null ? "PASS" : "FAIL")}");
            report.AppendLine($"Physical Snell/Jacobian caustics: {(controller != null && controller.physicalCaustics ? "PASS" : "FAIL")}");
            report.AppendLine($"Custom wave controller + editable spectrum: {(waveController != null && waveController.spectrum != null && fft != null && fft._spectrum == waveController.spectrum ? "PASS" : "FAIL")}");
            report.AppendLine($"Crest Dynamic Waves simulation: {(dynamicWavesReady ? "PASS" : "FAIL")}");
            report.AppendLine($"Player/vehicle/fish/object wave emitters: {(dynamicWaveEmitters.Length >= 4 ? "PASS" : "FAIL")} ({dynamicWaveEmitters.Length})");
            report.AppendLine($"URP planar reflection: {(planarReflection != null && planarReflection.enabled ? "PASS" : "FAIL")}");
            report.AppendLine($"OpenXR origin + tracked main camera: {(xrReady ? "PASS" : "FAIL")}");
            report.AppendLine($"Underwater renderer feature: {(underwaterFeature != null && underwaterFeature.isActive ? "PASS" : "FAIL")}");

            hasErrors |= ocean == null || fft == null || controller == null || !controller.physicalCaustics ||
                         waveController == null || waveController.spectrum == null || fft._spectrum != waveController.spectrum || !dynamicWavesReady ||
                         dynamicWaveEmitters.Length < 4 || planarReflection == null || !planarReflection.enabled || !xrReady ||
                         underwaterFeature == null || !underwaterFeature.isActive;

            if (camera != null)
            {
                Capture(camera, PreviewPath);
                CaptureAtPose(camera, new Vector3(0f, 5.8f, -18f), Quaternion.Euler(9f, 0f, 0f),
                    SurfacePreviewPath);
                // After the 90-frame test, retain the live simulation captures.
                // Edit mode has no evolved FFT texture and would overwrite them
                // with misleading flat diagnostics.
                if (!s_PreserveLiveCausticsCaptures)
                {
                    CaptureAtPose(camera, new Vector3(0f, -3.5f, 7f), Quaternion.Euler(22f, 0f, 0f),
                        PhysicalCausticsPreviewPath);
                    if (controller != null)
                    {
                        var previousDebugView = controller.underwaterDebugView;
                        controller.underwaterDebugView = CrestURPWaterController.UnderwaterDebugView.CausticGain;
                        controller.Apply();
                        CaptureAtPose(camera, new Vector3(0f, -3.5f, 7f), Quaternion.Euler(22f, 0f, 0f),
                            CausticGainDebugPath);
                        controller.underwaterDebugView = CrestURPWaterController.UnderwaterDebugView.WaveSlope;
                        controller.Apply();
                        CaptureAtPose(camera, new Vector3(0f, -3.5f, 7f), Quaternion.Euler(22f, 0f, 0f),
                            WaveSlopeDebugPath);
                        controller.underwaterDebugView = CrestURPWaterController.UnderwaterDebugView.DisplacementHeight;
                        controller.Apply();
                        CaptureAtPose(camera, new Vector3(0f, -3.5f, 7f), Quaternion.Euler(22f, 0f, 0f),
                            DisplacementHeightDebugPath);
                        controller.underwaterDebugView = previousDebugView;
                        controller.Apply();
                    }
                }
                report.AppendLine($"Waterline preview: PASS ({PreviewPath})");
                report.AppendLine($"Surface preview: PASS ({SurfacePreviewPath})");
                report.AppendLine($"Physical caustics preview: PASS ({PhysicalCausticsPreviewPath})");
                report.AppendLine($"Caustic Jacobian-gain debug: PASS ({CausticGainDebugPath})");
                report.AppendLine($"Live wave-slope debug: PASS ({WaveSlopeDebugPath})");
                report.AppendLine($"Live displacement-height debug: PASS ({DisplacementHeightDebugPath})");
            }

            if (File.Exists(PlayValidationPath))
            {
                var playValidation = File.ReadAllText(PlayValidationPath).Trim();
                var playFailed = playValidation.Contains("RESULT: FAIL");
                hasErrors |= playFailed;
                report.AppendLine();
                report.AppendLine("Latest play mode validation:");
                report.AppendLine(playValidation);
            }

            report.AppendLine();
            report.AppendLine(hasErrors ? "RESULT: FAIL" : "RESULT: PASS");
            File.WriteAllText(ReportPath, report.ToString());
            AssetDatabase.ImportAsset(ReportPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(PreviewPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(SurfacePreviewPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(PhysicalCausticsPreviewPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(CausticGainDebugPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(WaveSlopeDebugPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(DisplacementHeightDebugPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();

            if (hasErrors)
            {
                Debug.LogError($"Crest URP validation reported errors. See {ReportPath}");
            }
            else
            {
                Debug.Log($"Crest URP validation passed. Preview: {PreviewPath}");
            }
        }

        [MenuItem("Tools/Crest URP/Run 90-Frame Play Mode Visual Test %#v")]
        public static void RunPlayModeVisualTest()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SessionState.SetBool(PlayTestKey, true);
            SessionState.SetInt(PlayTestErrorsKey, 0);
            Application.logMessageReceived -= OnPlayModeLog;
            Application.logMessageReceived += OnPlayModeLog;
            EditorApplication.isPlaying = true;
        }

        static void AutoRunPlayModeTestOnce()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AutoRunPlayModeTestOnce;
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(PlayTestKey, false)) return;
            var showcaseVersion = File.Exists(ShowcaseBuildVersionPath)
                ? File.ReadAllText(ShowcaseBuildVersionPath).Trim()
                : string.Empty;
            if (showcaseVersion != RequiredShowcaseBuildVersion)
            {
                EditorApplication.delayCall += AutoRunPlayModeTestOnce;
                return;
            }
            var installedVersion = File.Exists(PlayValidationPath)
                ? File.ReadAllLines(PlayValidationPath).FirstOrDefault()?.Trim()
                : string.Empty;
            if (installedVersion != PlayValidationVersion && File.Exists(ScenePath))
            {
                RunPlayModeVisualTest();
            }
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(PlayTestKey, false)) return;

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                s_CaptureFrame = Time.frameCount + 90;
                s_WaitingForCapture = true;
                EditorApplication.update -= WaitForPlayModeCapture;
                EditorApplication.update += WaitForPlayModeCapture;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                var errorCount = SessionState.GetInt(PlayTestErrorsKey, 0);
                s_WaitingForCapture = false;
                EditorApplication.update -= WaitForPlayModeCapture;
                Application.logMessageReceived -= OnPlayModeLog;
                SessionState.SetBool(PlayTestKey, false);
                AssetDatabase.ImportAsset(PlayPreviewPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(PlayValidationPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(ReportPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.Refresh();
                s_PreserveLiveCausticsCaptures = true;
                try
                {
                    ValidateAndCapture();
                }
                finally
                {
                    s_PreserveLiveCausticsCaptures = false;
                }
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(errorCount == 0 ? 0 : 1);
                }
            }
        }

        static void WaitForPlayModeCapture()
        {
            if (!s_WaitingForCapture || !EditorApplication.isPlaying || Time.frameCount < s_CaptureFrame) return;
            s_WaitingForCapture = false;
            EditorApplication.update -= WaitForPlayModeCapture;

            var camera = Camera.main;
            if (camera != null)
            {
                // In batch mode there is no Game View, so this render is what
                // exercises the planar reflection callback before diagnostics.
                Capture(camera, PlayPreviewPath);
                // Fixed underwater view: exercises live FFT/dynamic displacement,
                // Snell refraction and the light-map Jacobian after 90 simulated frames.
                CaptureAtPose(camera, new Vector3(0f, -3.5f, 7f), Quaternion.Euler(22f, 0f, 0f),
                    PhysicalCausticsPreviewPath);
                var controller = UnityEngine.Object.FindFirstObjectByType<CrestURPWaterController>();
                if (controller != null)
                {
                    var previousDebugView = controller.underwaterDebugView;
                    controller.underwaterDebugView = CrestURPWaterController.UnderwaterDebugView.CausticGain;
                    controller.Apply();
                    CaptureAtPose(camera, new Vector3(0f, -3.5f, 7f), Quaternion.Euler(22f, 0f, 0f),
                        CausticGainDebugPath);
                    controller.underwaterDebugView = CrestURPWaterController.UnderwaterDebugView.WaveSlope;
                    controller.Apply();
                    CaptureAtPose(camera, new Vector3(0f, -3.5f, 7f), Quaternion.Euler(22f, 0f, 0f),
                        WaveSlopeDebugPath);
                    controller.underwaterDebugView = CrestURPWaterController.UnderwaterDebugView.DisplacementHeight;
                    controller.Apply();
                    CaptureAtPose(camera, new Vector3(0f, -3.5f, 7f), Quaternion.Euler(22f, 0f, 0f),
                        DisplacementHeightDebugPath);
                    controller.underwaterDebugView = previousDebugView;
                    controller.Apply();
                }
            }

            var errorCount = SessionState.GetInt(PlayTestErrorsKey, 0);
            var ocean = OceanRenderer.Instance;
            var dynamicTextureReady = ocean != null && ocean._lodDataDynWaves != null &&
                                      ocean._lodDataDynWaves.DataTexture != null &&
                                      ocean._lodDataDynWaves.DataTexture.IsCreated();
            var animatedWaveDiagnostics = ocean != null && ocean._lodDataAnimWaves != null
                ? AnalyzeAnimatedWaves(ocean._lodDataAnimWaves.DataTexture)
                : "Animated Waves diagnostics: unavailable";
            var emitterCount = UnityEngine.Object.FindObjectsByType<CrestURPDynamicWaveEmitter>(
                FindObjectsSortMode.None).Length;
            var planarReflection = camera != null ? camera.GetComponent<CrestURPPlanarReflection>() : null;
            var planarReflectionReady = planarReflection != null && planarReflection.HasRenderedTexture;
            var causticGainReady = AnalyzeDebugCapture(CausticGainDebugPath, out var causticGainDiagnostics);
            var waveSlopeReady = AnalyzeDebugCapture(WaveSlopeDebugPath, out var waveSlopeDiagnostics);
            var displacementReady = AnalyzeDebugCapture(DisplacementHeightDebugPath, out var displacementDiagnostics);
            var runtimeFeaturesReady = dynamicTextureReady && emitterCount >= 4 && planarReflectionReady &&
                                       causticGainReady && waveSlopeReady && displacementReady;
            if (!runtimeFeaturesReady)
            {
                errorCount++;
                SessionState.SetInt(PlayTestErrorsKey, errorCount);
            }
            File.AppendAllText(ReportPath,
                $"\n90-frame play mode test: {(camera != null && errorCount == 0 ? "PASS" : "FAIL")} " +
                $"(errors/runtime failures: {errorCount}, Dynamic Waves RT: {dynamicTextureReady}, " +
                $"emitters: {emitterCount}, planar RT: {planarReflectionReady}, preview: {PlayPreviewPath})\n");
            File.WriteAllText(PlayValidationPath,
                $"{PlayValidationVersion}\nRESULT: {(camera != null && errorCount == 0 ? "PASS" : "FAIL")}\n" +
                $"Errors/runtime failures: {errorCount}\n" +
                $"Dynamic Waves render texture: {(dynamicTextureReady ? "PASS" : "FAIL")}\n" +
                $"Interaction emitters: {(emitterCount >= 4 ? "PASS" : "FAIL")} ({emitterCount})\n" +
                $"Planar reflection render texture: {(planarReflectionReady ? "PASS" : "FAIL")}\n" +
                causticGainDiagnostics + "\n" +
                waveSlopeDiagnostics + "\n" +
                displacementDiagnostics + "\n" +
                animatedWaveDiagnostics + "\n" +
                $"Generated: {DateTime.Now:O}\n");
            EditorApplication.isPlaying = false;
        }

        static bool AnalyzeDebugCapture(string path, out string diagnostics)
        {
            if (!File.Exists(path))
            {
                diagnostics = $"Debug variation {Path.GetFileName(path)}: FAIL (capture missing)";
                return false;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path), false))
                {
                    diagnostics = $"Debug variation {Path.GetFileName(path)}: FAIL (decode failed)";
                    return false;
                }

                var minimum = Vector3.one;
                var maximum = Vector3.zero;
                var luminanceSum = 0d;
                var luminanceSquaredSum = 0d;
                var count = 0;
                // Camera debug views reserve the upper region for sky. Analyze
                // the central/lower receiver surfaces where caustics are valid.
                var xStart = Mathf.RoundToInt(texture.width * 0.08f);
                var xEnd = Mathf.RoundToInt(texture.width * 0.92f);
                var yEnd = Mathf.RoundToInt(texture.height * 0.64f);
                for (var y = 0; y < yEnd; y += 2)
                {
                    for (var x = xStart; x < xEnd; x += 2)
                    {
                        var color = texture.GetPixel(x, y);
                        var rgb = new Vector3(color.r, color.g, color.b);
                        minimum = Vector3.Min(minimum, rgb);
                        maximum = Vector3.Max(maximum, rgb);
                        var luminance = Vector3.Dot(rgb, new Vector3(0.2126f, 0.7152f, 0.0722f));
                        luminanceSum += luminance;
                        luminanceSquaredSum += luminance * luminance;
                        count++;
                    }
                }

                var range = maximum - minimum;
                var maximumChannelRange = Mathf.Max(range.x, Mathf.Max(range.y, range.z));
                var mean = count > 0 ? luminanceSum / count : 0d;
                var variance = count > 0 ? luminanceSquaredSum / count - mean * mean : 0d;
                var deviation = Mathf.Sqrt((float)Math.Max(0d, variance));
                var passed = maximumChannelRange >= 0.06f && deviation >= 0.006f;
                diagnostics = $"Debug variation {Path.GetFileName(path)}: {(passed ? "PASS" : "FAIL")} " +
                              $"(RGB range {range.x:0.###}/{range.y:0.###}/{range.z:0.###}, luminance sigma {deviation:0.####})";
                return passed;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        static string AnalyzeAnimatedWaves(RenderTexture texture)
        {
            if (texture == null || !texture.IsCreated()) return "Animated Waves diagnostics: texture unavailable";

            var report = new StringBuilder(256);
            report.Append("Animated Waves displacement ranges:");
            var previousActive = RenderTexture.active;
            var readback = new Texture2D(texture.width, texture.height, TextureFormat.RGBAFloat, false, true);
            try
            {
                for (var slice = 0; slice < texture.volumeDepth; slice++)
                {
                    Graphics.SetRenderTarget(texture, 0, CubemapFace.Unknown, slice);
                    readback.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0, false);
                    readback.Apply(false, false);
                    var pixels = readback.GetPixels();
                    var minimumY = float.PositiveInfinity;
                    var maximumY = float.NegativeInfinity;
                    var sumY2 = 0d;
                    var maximumHorizontal = 0f;
                    foreach (var pixel in pixels)
                    {
                        minimumY = Mathf.Min(minimumY, pixel.g);
                        maximumY = Mathf.Max(maximumY, pixel.g);
                        sumY2 += pixel.g * pixel.g;
                        maximumHorizontal = Mathf.Max(maximumHorizontal,
                            Mathf.Sqrt(pixel.r * pixel.r + pixel.b * pixel.b));
                    }
                    var rmsY = Mathf.Sqrt((float)(sumY2 / pixels.Length));
                    report.Append($" slice {slice} Y[{minimumY:0.####},{maximumY:0.####}] rms={rmsY:0.####} XZmax={maximumHorizontal:0.####};");
                }
            }
            finally
            {
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(readback);
            }
            return report.ToString();
        }

        static void OnPlayModeLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (Application.isBatchMode &&
                (condition.Contains("XR_ERROR_RUNTIME_UNAVAILABLE") ||
                 condition.Contains("XR_ERROR_FUNCTION_UNSUPPORTED"))) return;
            SessionState.SetInt(PlayTestErrorsKey, SessionState.GetInt(PlayTestErrorsKey, 0) + 1);
        }

        static void ValidateShader(string shaderName, StringBuilder report, ref bool hasErrors)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                report.AppendLine($"Shader {shaderName}: FAIL (not found)");
                hasErrors = true;
                return;
            }

            var messages = ShaderUtil.GetShaderMessages(shader);
            var shaderHasError = ShaderUtil.ShaderHasError(shader) ||
                                 messages.Any(message =>
                                     message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error);
            hasErrors |= shaderHasError;
            report.AppendLine($"Shader {shaderName}: {(shaderHasError ? "FAIL" : "PASS")} ({messages.Length} messages)");
            foreach (var message in messages)
            {
                report.AppendLine(
                    $"  [{message.severity}] {message.platform}: line {message.line}: {message.message}");
            }
        }

        static void Capture(Camera camera, string path)
        {
            const int width = 1280;
            const int height = 720;
            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf)
            {
                name = "Crest URP Validation Preview",
                antiAliasing = 1
            };
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            var previousActive = RenderTexture.active;
            var previousTarget = camera.targetTexture;

            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                image.Apply(false, false);
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        static void CaptureAtPose(Camera camera, Vector3 position, Quaternion rotation, string path)
        {
            var previousPosition = camera.transform.position;
            var previousRotation = camera.transform.rotation;
            var poseDriver = camera.GetComponent<TrackedPoseDriver>();
            var poseDriverWasEnabled = poseDriver != null && poseDriver.enabled;
            try
            {
                if (poseDriver != null) poseDriver.enabled = false;
                camera.transform.SetPositionAndRotation(position, rotation);
                Capture(camera, path);
            }
            finally
            {
                camera.transform.SetPositionAndRotation(previousPosition, previousRotation);
                if (poseDriver != null) poseDriver.enabled = poseDriverWasEnabled;
            }
        }
    }
}
