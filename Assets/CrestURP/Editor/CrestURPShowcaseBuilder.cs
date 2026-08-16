using System;
using System.Collections.Generic;
using System.Linq;
using Crest;
using MusicProgram.CrestURP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;

namespace MusicProgram.CrestURP.Editor
{
    [InitializeOnLoad]
    public static class CrestURPShowcaseBuilder
    {
        const string Root = "Assets/CrestURP";
        const string SampleRoot = Root + "/Samples";
        const string MaterialsRoot = SampleRoot + "/Materials";
        const string ScenePath = SampleRoot + "/CrestURPShowcase.unity";
        const string OceanMaterialPath = MaterialsRoot + "/CrestURP_Ocean.mat";
        const string UnderwaterMaterialPath = MaterialsRoot + "/CrestURP_Underwater.mat";
        const string BuildVersionPath = SampleRoot + "/CrestURPBuildVersion.txt";
        const string DynamicWaveSettingsPath = SampleRoot + "/CrestURP_DynamicWaves.asset";
        const string FoamSettingsPath = SampleRoot + "/CrestURP_Foam.asset";
        const string AnimatedWaveSettingsPath = SampleRoot + "/CrestURP_AnimatedWaves.asset";
        const string SpectrumPath = SampleRoot + "/CrestURP_OceanSpectrum.asset";
        const string BuildVersion = "1.3.2";
        const string RendererPath = "Assets/Settings/PC_Renderer.asset";
        const string FeatureName = "Crest URP Underwater + Waterline";

        static CrestURPShowcaseBuilder()
        {
            EditorApplication.delayCall += AutoBuildIfNeeded;
        }

        static void AutoBuildIfNeeded()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AutoBuildIfNeeded;
                return;
            }

            var installedVersion = System.IO.File.Exists(BuildVersionPath)
                ? System.IO.File.ReadAllText(BuildVersionPath).Trim()
                : string.Empty;
            if (!System.IO.File.Exists(ScenePath) || installedVersion != BuildVersion)
            {
                BuildShowcase();
            }
            else
            {
                EnsureRendererFeature(AssetDatabase.LoadAssetAtPath<Material>(UnderwaterMaterialPath));
            }
        }

        [MenuItem("Tools/Crest URP/Rebuild Complete Showcase Scene")]
        public static void BuildShowcase()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                Debug.LogWarning("Crest URP: wait for Unity compilation/import to finish, then run the builder again.");
                return;
            }

            EnsureFolder(SampleRoot);
            EnsureFolder(MaterialsRoot);

            var oceanShader = Shader.Find("Crest/URP/Ocean");
            var underwaterShader = Shader.Find("Crest/URP/Underwater");
            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (oceanShader == null || underwaterShader == null || litShader == null)
            {
                Debug.LogError("Crest URP: required shaders are not available. Check shader compilation errors first.");
                return;
            }

            var oceanMaterial = GetOrCreateMaterial(OceanMaterialPath, oceanShader);
            ConfigureOceanMaterial(oceanMaterial);
            var underwaterMaterial = GetOrCreateMaterial(UnderwaterMaterialPath, underwaterShader);
            EnsureRendererFeature(underwaterMaterial);

            var sand = GetOrCreateLitMaterial(MaterialsRoot + "/Seafloor.mat", litShader,
                new Color(0.27f, 0.23f, 0.15f), 0.03f, 0.22f);
            var rock = GetOrCreateLitMaterial(MaterialsRoot + "/Rock.mat", litShader,
                new Color(0.095f, 0.13f, 0.12f), 0.05f, 0.34f);
            var metal = GetOrCreateLitMaterial(MaterialsRoot + "/Metal.mat", litShader,
                new Color(0.18f, 0.24f, 0.27f), 0.72f, 0.72f);
            var buoy = GetOrCreateLitMaterial(MaterialsRoot + "/Buoy.mat", litShader,
                new Color(0.92f, 0.19f, 0.055f), 0.18f, 0.48f);
            var coral = GetOrCreateLitMaterial(MaterialsRoot + "/Coral.mat", litShader,
                new Color(0.46f, 0.12f, 0.08f), 0.05f, 0.27f);
            var dynamicWaveSettings = GetOrCreateDynamicWaveSettings();
            var foamSettings = GetOrCreateFoamSettings();
            var animatedWaveSettings = GetOrCreateAnimatedWaveSettings();
            var spectrum = GetOrCreateSpectrum();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "CrestURPShowcase";

            var environment = new GameObject("Environment");
            var camera = CreateCamera();
            var sun = CreateLighting();
            CreateEnvironment(environment.transform, sand, rock, metal, coral);
            CreateReflectionProbe(environment.transform);

            var oceanObject = new GameObject("Crest Ocean 4 - URP Port");
            oceanObject.SetActive(false);
            var ocean = oceanObject.AddComponent<OceanRenderer>();
            ocean.OceanMaterial = oceanMaterial;
            ocean._primaryLight = sun;
            ocean._globalWindSpeed = 18f;
            ocean._globalWindDirectionAngle = 24f;
            ocean._globalWindTurbulence = 0.18f;
            ConfigureOceanRenderer(ocean, dynamicWaveSettings, foamSettings, animatedWaveSettings);

            var fft = oceanObject.AddComponent<ShapeFFT>();
            fft._resolution = 128;
            fft._weight = 1f;
            fft._overrideGlobalWindSpeed = false;
            fft._overrideGlobalWindDirection = false;
            fft._overrideGlobalWindTurbulence = false;
            fft._spectrum = spectrum;

            var timeProvider = oceanObject.AddComponent<CrestURPScaledTimeProvider>();

            var waveController = oceanObject.AddComponent<CrestURPWaveController>();
            waveController.ocean = ocean;
            waveController.fft = fft;
            waveController.spectrum = spectrum;
            waveController.dynamicWaves = dynamicWaveSettings;
            waveController.foam = foamSettings;
            waveController.timeProvider = timeProvider;
            waveController.CaptureCurrentSettings();

            var controller = oceanObject.AddComponent<CrestURPWaterController>();
            controller.ocean = ocean;
            controller.oceanMaterial = oceanMaterial;
            controller.underwaterMaterial = underwaterMaterial;
            controller.sun = sun;
            oceanObject.SetActive(true);
            controller.Apply();

            CreateFloatingObjects(buoy, metal);
            CreateDynamicWaveActors(buoy, metal);
            CreateWaterlineReferenceObjects(buoy, metal);
            CreateSceneLabels();
            CreatePostProcessing();

            Selection.activeGameObject = oceanObject;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            System.IO.File.WriteAllText(BuildVersionPath, BuildVersion + Environment.NewLine);
            AssetDatabase.ImportAsset(BuildVersionPath, ImportAssetOptions.ForceUpdate);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorApplication.delayCall += CrestURPValidation.ValidateAndCapture;

            Debug.Log($"Crest URP: complete showcase created at {ScenePath}. " +
                      "Use WASD/QE and hold right mouse button in non-XR play mode; in OpenXR the headset drives the camera.");
        }

        static Camera CreateCamera()
        {
            var origin = new GameObject("XR Origin - Waterline Test");
            origin.transform.position = new Vector3(0f, 0f, -13.5f);
            var xrOrigin = origin.AddComponent<XROrigin>();

            var cameraOffset = new GameObject("Camera Offset");
            cameraOffset.transform.SetParent(origin.transform, false);
            cameraOffset.transform.localPosition = new Vector3(0f, 0.42f, 0f);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(cameraOffset.transform, false);
            cameraObject.transform.localPosition = Vector3.zero;
            cameraObject.transform.localRotation = Quaternion.Euler(4f, 0f, 0f);

            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 1800f;
            camera.fieldOfView = 68f;
            camera.allowHDR = true;
            camera.depthTextureMode = DepthTextureMode.Depth;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<CrestURPDemoCamera>();
            var playerWake = cameraObject.AddComponent<CrestURPDynamicWaveEmitter>();
            playerWake.radius = 0.38f;
            playerWake.strength = 0.85f;
            playerWake.verticalMotionStrength = 0.45f;
            playerWake.velocityLead = 0.045f;
            playerWake.Apply();

            var planarReflection = cameraObject.AddComponent<CrestURPPlanarReflection>();
            planarReflection.resolutionScale = 0.5f;
            planarReflection.maximumResolution = 1024;
            planarReflection.reflectionStrength = 0.72f;
            planarReflection.updateEveryFrames = 1;
            planarReflection.reflectionFarClip = 520f;
            planarReflection.renderShadows = true;

            var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            cameraData.requiresDepthTexture = true;
            cameraData.requiresColorTexture = true;

            xrOrigin.Camera = camera;
            xrOrigin.CameraFloorOffsetObject = cameraOffset;
            xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
            xrOrigin.CameraYOffset = 0.42f;

            var trackedPoseDriver = cameraObject.AddComponent<TrackedPoseDriver>();
            var positionAction = new InputAction("HMD Position", InputActionType.Value,
                "<XRHMD>/centerEyePosition", expectedControlType: "Vector3");
            var rotationAction = new InputAction("HMD Rotation", InputActionType.Value,
                "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion");
            trackedPoseDriver.positionInput = new InputActionProperty(positionAction);
            trackedPoseDriver.rotationInput = new InputActionProperty(rotationAction);

            return camera;
        }

        static Light CreateLighting()
        {
            var lightObject = new GameObject("Sun");
            lightObject.transform.rotation = Quaternion.Euler(47f, -32f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.89f, 0.96f, 1f);
            light.intensity = 1.7f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.82f;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.22f, 0.37f, 0.5f);
            RenderSettings.ambientEquatorColor = new Color(0.08f, 0.16f, 0.2f);
            RenderSettings.ambientGroundColor = new Color(0.015f, 0.028f, 0.035f);
            RenderSettings.reflectionIntensity = 1.15f;

            var skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                var skyPath = MaterialsRoot + "/OceanSky.mat";
                var sky = GetOrCreateMaterial(skyPath, skyShader);
                sky.SetColor("_SkyTint", new Color(0.20f, 0.43f, 0.62f));
                sky.SetColor("_GroundColor", new Color(0.055f, 0.095f, 0.11f));
                sky.SetFloat("_AtmosphereThickness", 0.78f);
                sky.SetFloat("_Exposure", 1.32f);
                RenderSettings.skybox = sky;
            }

            return light;
        }

        static void CreateEnvironment(Transform parent, Material sand, Material rock, Material metal, Material coral)
        {
            CreatePrimitive("Seafloor", PrimitiveType.Cube, new Vector3(0f, -8.1f, 30f),
                new Vector3(180f, 1f, 180f), sand, parent);

            var rng = new System.Random(7319);
            for (var i = 0; i < 34; i++)
            {
                var x = Mathf.Lerp(-58f, 58f, (float)rng.NextDouble());
                var z = Mathf.Lerp(-5f, 100f, (float)rng.NextDouble());
                var scale = Mathf.Lerp(1.2f, 6.5f, (float)rng.NextDouble());
                var item = CreatePrimitive($"Rock {i + 1:00}", PrimitiveType.Sphere,
                    new Vector3(x, -7.2f + scale * 0.18f, z),
                    new Vector3(scale * 1.4f, scale * 0.6f, scale), rock, parent);
                item.transform.rotation = Quaternion.Euler(
                    Mathf.Lerp(-18f, 18f, (float)rng.NextDouble()),
                    Mathf.Lerp(0f, 360f, (float)rng.NextDouble()),
                    Mathf.Lerp(-12f, 12f, (float)rng.NextDouble()));
            }

            for (var i = -4; i <= 4; i++)
            {
                var pillar = CreatePrimitive($"Waterline Pillar {i + 5}", PrimitiveType.Cylinder,
                    new Vector3(i * 6f, -2.3f, 18f + Mathf.Abs(i) * 2.5f),
                    new Vector3(0.72f, 6.2f, 0.72f), metal, parent);
                pillar.transform.rotation = Quaternion.Euler(0f, i * 13f, i * 1.8f);
            }

            for (var i = 0; i < 15; i++)
            {
                var angle = i * Mathf.PI * 2f / 15f;
                var radius = 9f + (i % 3) * 2.4f;
                var stem = CreatePrimitive($"Coral {i + 1:00}", PrimitiveType.Capsule,
                    new Vector3(Mathf.Cos(angle) * radius, -6.7f, 39f + Mathf.Sin(angle) * radius),
                    new Vector3(0.35f + (i % 4) * 0.08f, 1.2f + (i % 5) * 0.3f, 0.35f), coral, parent);
                stem.transform.rotation = Quaternion.Euler(i % 2 == 0 ? 8f : -10f, i * 29f, i % 3 == 0 ? 12f : -7f);
            }

            var archLeft = CreatePrimitive("Underwater Arch Left", PrimitiveType.Cube,
                new Vector3(-5.5f, -3.7f, 49f), new Vector3(2.2f, 7.5f, 2.2f), rock, parent);
            archLeft.transform.rotation = Quaternion.Euler(0f, 0f, -7f);
            var archRight = CreatePrimitive("Underwater Arch Right", PrimitiveType.Cube,
                new Vector3(5.5f, -3.7f, 49f), new Vector3(2.2f, 7.5f, 2.2f), rock, parent);
            archRight.transform.rotation = Quaternion.Euler(0f, 0f, 7f);
            CreatePrimitive("Underwater Arch Top", PrimitiveType.Cube,
                new Vector3(0f, -0.5f, 49f), new Vector3(12.5f, 2f, 2.2f), rock, parent);
        }

        static void CreateFloatingObjects(Material buoyMaterial, Material metalMaterial)
        {
            var root = new GameObject("Crest Buoyancy Demonstration");
            for (var i = 0; i < 5; i++)
            {
                var item = CreatePrimitive($"Floating Body {i + 1}", i % 2 == 0 ? PrimitiveType.Cube : PrimitiveType.Sphere,
                    new Vector3(-9f + i * 4.5f, 1.5f + (i % 2) * 0.8f, 7f + i * 2.4f),
                    i % 2 == 0 ? new Vector3(2.1f, 1.1f, 3.2f) : Vector3.one * 1.55f,
                    i == 2 ? metalMaterial : buoyMaterial, root.transform);
                var body = item.AddComponent<Rigidbody>();
                body.mass = i % 2 == 0 ? 85f : 28f;
                body.linearDamping = 0.06f;
                body.angularDamping = 0.08f;
                var floating = item.AddComponent<SimpleFloatingObject>();
                floating._objectWidth = i % 2 == 0 ? 3.2f : 1.7f;
                floating._raiseObject = i % 2 == 0 ? 0.75f : 0.55f;
                floating._buoyancyCoeff = i % 2 == 0 ? 4.2f : 5.5f;
                floating._boyancyTorque = 7.5f;
                AddDynamicWaveEmitter(item, i % 2 == 0 ? 1.1f : 0.82f,
                    i % 2 == 0 ? 1.05f : 0.72f, 0.7f);
            }
        }

        static void CreateDynamicWaveActors(Material accentMaterial, Material metalMaterial)
        {
            var root = new GameObject("Dynamic Waves - Moving Actors");

            var submarine = new GameObject("Surface Submarine");
            submarine.transform.SetParent(root.transform, false);
            submarine.transform.position = new Vector3(0f, -0.55f, 28f);
            var submarineMotion = submarine.AddComponent<CrestURPDynamicWaveDemoActor>();
            submarineMotion.orbitRadius = new Vector2(15f, 8f);
            submarineMotion.angularSpeed = 10f;
            submarineMotion.bobAmplitude = 0.2f;
            submarineMotion.bobFrequency = 0.68f;
            submarineMotion.phase = 35f;

            CreateActorPrimitive("Pressure Hull", PrimitiveType.Capsule, submarine.transform,
                Vector3.zero, new Vector3(1.35f, 3.9f, 1.35f), Quaternion.Euler(90f, 0f, 0f), metalMaterial);
            CreateActorPrimitive("Conning Tower", PrimitiveType.Cube, submarine.transform,
                new Vector3(0f, 0.78f, -0.15f), new Vector3(0.72f, 0.55f, 1.15f), Quaternion.identity, metalMaterial);
            CreateActorPrimitive("Left Hydroplane", PrimitiveType.Cube, submarine.transform,
                new Vector3(-1.45f, 0.05f, 0.45f), new Vector3(2.0f, 0.12f, 0.72f), Quaternion.identity, accentMaterial);
            CreateActorPrimitive("Right Hydroplane", PrimitiveType.Cube, submarine.transform,
                new Vector3(1.45f, 0.05f, 0.45f), new Vector3(2.0f, 0.12f, 0.72f), Quaternion.identity, accentMaterial);
            CreateActorPrimitive("Tail Fin", PrimitiveType.Cube, submarine.transform,
                new Vector3(0f, 0.72f, -3.25f), new Vector3(0.14f, 1.6f, 0.8f), Quaternion.identity, accentMaterial);
            AddDynamicWaveEmitter(submarine, 1.15f, 1.28f, 0.65f, new Vector3(0f, 0f, 1.95f), "Bow Wake");
            AddDynamicWaveEmitter(submarine, 1.05f, 1.05f, 0.55f, Vector3.zero, "Hull Wake");
            AddDynamicWaveEmitter(submarine, 0.8f, 0.82f, 0.45f, new Vector3(0f, 0f, -2.35f), "Stern Wake");

            var fishSchool = new GameObject("Surface Fish School");
            fishSchool.transform.SetParent(root.transform, false);
            fishSchool.transform.position = new Vector3(-5f, -0.22f, 35f);
            var fishMotion = fishSchool.AddComponent<CrestURPDynamicWaveDemoActor>();
            fishMotion.orbitRadius = new Vector2(11f, 5.5f);
            fishMotion.angularSpeed = -18f;
            fishMotion.bobAmplitude = 0.16f;
            fishMotion.bobFrequency = 1.1f;
            fishMotion.phase = 140f;

            var fishPositions = new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(-1.1f, -0.08f, -0.85f),
                new Vector3(1.05f, 0.06f, -1.15f), new Vector3(-1.8f, 0.02f, -2.05f),
                new Vector3(1.75f, -0.1f, -2.35f)
            };
            for (var i = 0; i < fishPositions.Length; i++)
            {
                var fish = new GameObject($"Fish {i + 1}");
                fish.transform.SetParent(fishSchool.transform, false);
                fish.transform.localPosition = fishPositions[i];
                CreateActorPrimitive("Body", PrimitiveType.Sphere, fish.transform,
                    Vector3.zero, new Vector3(0.28f, 0.2f, 0.62f), Quaternion.identity,
                    i % 2 == 0 ? accentMaterial : metalMaterial);
                CreateActorPrimitive("Tail", PrimitiveType.Cube, fish.transform,
                    new Vector3(0f, 0f, -0.65f), new Vector3(0.05f, 0.42f, 0.34f),
                    Quaternion.Euler(0f, 0f, 45f), accentMaterial);
                AddDynamicWaveEmitter(fish, 0.24f, 0.5f, 0.4f);
            }
        }

        static void CreateWaterlineReferenceObjects(Material buoyMaterial, Material metalMaterial)
        {
            var root = new GameObject("Waterline Reference Objects");
            CreatePrimitive("Half-Submerged Red Sphere", PrimitiveType.Sphere,
                new Vector3(-5f, 0f, -1f), Vector3.one * 2.6f, buoyMaterial, root.transform);
            CreatePrimitive("Half-Submerged Metal Cube", PrimitiveType.Cube,
                new Vector3(5f, -0.2f, 1f), new Vector3(3f, 3f, 3f), metalMaterial, root.transform);
        }

        static void CreateSceneLabels()
        {
            var root = new GameObject("Showcase Notes");
            root.AddComponent<CrestURPShowcaseNotes>();
        }

        static void CreateReflectionProbe(Transform parent)
        {
            var probeObject = new GameObject("Ocean Reflection Probe");
            probeObject.transform.SetParent(parent, false);
            probeObject.transform.position = new Vector3(0f, 7f, 30f);
            var probe = probeObject.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            probe.resolution = 128;
            probe.size = new Vector3(140f, 45f, 160f);
            probe.intensity = 1.15f;
            probe.boxProjection = true;
        }

        static void CreatePostProcessing()
        {
            var volumeObject = new GameObject("Global Volume");
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;

            var profilePath = SampleRoot + "/CrestURP_ShowcaseVolume.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "Crest URP Showcase Volume";
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            volume.sharedProfile = profile;

            if (!profile.TryGet(out Bloom bloom)) bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.24f);
            bloom.threshold.Override(1.05f);
            bloom.scatter.Override(0.55f);

            if (!profile.TryGet(out ColorAdjustments color)) color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.08f);
            color.contrast.Override(7f);
            color.saturation.Override(-4f);
        }

        static void ConfigureOceanRenderer(OceanRenderer ocean, SimSettingsWave dynamicWaveSettings,
            SimSettingsFoam foamSettings, SimSettingsAnimatedWaves animatedWaveSettings)
        {
            var serialized = new SerializedObject(ocean);
            Set(serialized, "_lodDataResolution", 256);
            Set(serialized, "_geometryDownSampleFactor", 2);
            Set(serialized, "_lodCount", 6);
            Set(serialized, "_minScale", 8f);
            Set(serialized, "_maxScale", 256f);
            Set(serialized, "_createSeaFloorDepthData", false);
            Set(serialized, "_createFoamSim", true);
            Set(serialized, "_createDynamicWaveSim", true);
            Set(serialized, "_createFlowSim", false);
            Set(serialized, "_createShadowData", false);
            Set(serialized, "_createClipSurfaceData", false);
            Set(serialized, "_createAlbedoData", false);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            ocean.SimSettingsDynamicWaves = dynamicWaveSettings;
            ocean._simSettingsFoam = foamSettings;
            ocean._simSettingsAnimatedWaves = animatedWaveSettings;
        }

        static SimSettingsWave GetOrCreateDynamicWaveSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<SimSettingsWave>(DynamicWaveSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<SimSettingsWave>();
                settings.name = "Crest URP PCVR Dynamic Waves";
                AssetDatabase.CreateAsset(settings, DynamicWaveSettingsPath);
                settings._simulationFrequency = 45f;
                settings._damping = 0.075f;
                settings._courantNumber = 0.6f;
                settings._attenuationInShallows = 0.72f;
                settings._horizDisplace = 2.5f;
                settings._displaceClamp = 0.35f;
                settings._gravityMultiplier = 1f;
            }
            EditorUtility.SetDirty(settings);
            return settings;
        }

        static SimSettingsFoam GetOrCreateFoamSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<SimSettingsFoam>(FoamSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<SimSettingsFoam>();
                settings.name = "Crest URP Foam";
                settings._prewarm = true;
                settings._foamFadeRate = 0.8f;
                settings._waveFoamStrength = 1.15f;
                settings._waveFoamCoverage = 0.56f;
                settings._shorelineFoamMaxDepth = 0.65f;
                settings._shorelineFoamStrength = 2f;
                settings._simulationFrequency = 30f;
                AssetDatabase.CreateAsset(settings, FoamSettingsPath);
            }
            return settings;
        }

        static SimSettingsAnimatedWaves GetOrCreateAnimatedWaveSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<SimSettingsAnimatedWaves>(AnimatedWaveSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<SimSettingsAnimatedWaves>();
                settings.name = "Crest URP Animated Waves";
                settings.CollisionSource = SimSettingsAnimatedWaves.CollisionSources.ComputeShaderQueries;
                AssetDatabase.CreateAsset(settings, AnimatedWaveSettingsPath);
            }
            return settings;
        }

        static OceanWaveSpectrum GetOrCreateSpectrum()
        {
            var spectrum = AssetDatabase.LoadAssetAtPath<OceanWaveSpectrum>(SpectrumPath);
            if (spectrum == null)
            {
                spectrum = ScriptableObject.CreateInstance<OceanWaveSpectrum>();
                spectrum.name = "Crest URP Editable Ocean Spectrum";
                spectrum.ApplyPiersonMoskowitzSpectrum();
                spectrum._multiplier = 1f;
                spectrum._chop = 1.45f;
                spectrum._waveDirectionVariance = 78f;
                spectrum._gravityScale = 1f;
                spectrum._smallWavelengthMultiplier = 1f;
                AssetDatabase.CreateAsset(spectrum, SpectrumPath);
            }
            return spectrum;
        }

        static void ConfigureOceanMaterial(Material material)
        {
            material.SetColor("_ShallowColor", new Color(0.035f, 0.38f, 0.50f, 0.82f));
            material.SetColor("_DeepColor", new Color(0.004f, 0.055f, 0.11f, 0.96f));
            material.SetVector("_Absorption", new Vector4(0.12f, 0.045f, 0.022f, 0f));
            material.SetFloat("_Smoothness", 0.93f);
            material.SetFloat("_DetailNormalStrength", 0.52f);
            material.SetFloat("_RefractionStrength", 0.045f);
            material.SetFloat("_Alpha", 0.68f);
            material.SetFloat("_Foam", 1f);
            material.EnableKeyword("_FOAM_ON");
            material.SetFloat("_Underwater", 1f);
            material.EnableKeyword("_UNDERWATER_ON");
            material.SetFloat("_CullMode", 0f);
            EditorUtility.SetDirty(material);
        }

        static void EnsureRendererFeature(Material underwaterMaterial)
        {
            if (underwaterMaterial == null)
            {
                return;
            }

            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rendererData == null)
            {
                Debug.LogError($"Crest URP: UniversalRendererData not found at {RendererPath}.");
                return;
            }

            var feature = rendererData.rendererFeatures
                .OfType<FullScreenPassRendererFeature>()
                .FirstOrDefault(item => item != null && item.name == FeatureName);

            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feature.name = FeatureName;
                AssetDatabase.AddObjectToAsset(feature, rendererData);
                rendererData.rendererFeatures.Add(feature);

                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
                var serialized = new SerializedObject(rendererData);
                var map = serialized.FindProperty("m_RendererFeatureMap");
                map.InsertArrayElementAtIndex(map.arraySize);
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            feature.passMaterial = underwaterMaterial;
            feature.passIndex = 0;
            feature.fetchColorBuffer = true;
            feature.requirements = ScriptableRenderPassInput.Depth;
            feature.bindDepthStencilAttachment = false;
            // Tint submerged opaque geometry first, then let Crest's transparent
            // surface add physically plausible reflection/refraction on top.
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingTransparents;
            feature.SetActive(true);
            rendererData.SetDirty();
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();
        }

        static GameObject CreatePrimitive(string name, PrimitiveType type, Vector3 position, Vector3 scale,
            Material material, Transform parent = null)
        {
            var item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.transform.position = position;
            item.transform.localScale = scale;
            if (parent != null)
            {
                item.transform.SetParent(parent, true);
            }
            var renderer = item.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            return item;
        }

        static GameObject CreateActorPrimitive(string name, PrimitiveType type, Transform parent,
            Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material)
        {
            var item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = localPosition;
            item.transform.localScale = localScale;
            item.transform.localRotation = localRotation;
            item.GetComponent<Renderer>().sharedMaterial = material;
            return item;
        }

        static CrestURPDynamicWaveEmitter AddDynamicWaveEmitter(GameObject target, float radius,
            float strength, float verticalMotionStrength, Vector3? localPosition = null, string childName = null)
        {
            var emitterObject = target;
            if (localPosition.HasValue || !string.IsNullOrEmpty(childName))
            {
                emitterObject = new GameObject(childName ?? "Dynamic Wave Emitter");
                emitterObject.transform.SetParent(target.transform, false);
                emitterObject.transform.localPosition = localPosition ?? Vector3.zero;
            }

            var emitter = emitterObject.AddComponent<CrestURPDynamicWaveEmitter>();
            emitter.radius = radius;
            emitter.strength = strength;
            emitter.verticalMotionStrength = verticalMotionStrength;
            emitter.compensateForWaveMotion = 0.42f;
            emitter.velocityLead = 0.055f;
            emitter.Apply();
            return emitter;
        }

        static Material GetOrCreateMaterial(string path, Shader shader)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                material.shader = shader;
                return material;
            }
            material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static Material GetOrCreateLitMaterial(string path, Shader shader, Color color, float metallic, float smoothness)
        {
            var material = GetOrCreateMaterial(path, shader);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void Set(SerializedObject serialized, string name, int value)
        {
            var property = serialized.FindProperty(name);
            if (property != null) property.intValue = value;
        }

        static void Set(SerializedObject serialized, string name, float value)
        {
            var property = serialized.FindProperty(name);
            if (property != null) property.floatValue = value;
        }

        static void Set(SerializedObject serialized, string name, bool value)
        {
            var property = serialized.FindProperty(name);
            if (property != null) property.boolValue = value;
        }

        static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        static void AddSceneToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(item => item.path != ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, false));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }
    }

}
