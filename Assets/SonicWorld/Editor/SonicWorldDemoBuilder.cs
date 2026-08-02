#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using MK.Toon;
using SonicWorld;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace SonicWorldEditor
{
    public static class SonicWorldDemoBuilder
    {
        private const string SourceScene = "Assets/Scenes/SampleScene.unity";
        private const string RootFolder = "Assets/SonicWorld";
        private const string SceneFolder = RootFolder + "/Scenes";
        private const string ProfileFolder = RootFolder + "/Profiles";
        private const string MaterialFolder = RootFolder + "/Materials";
        private const string DemoScene = SceneFolder + "/SonicWorldDemo.unity";
        private const string RendererDataPath = "Assets/Settings/PC_Renderer.asset";
        private const string GrayscaleMaterialPath =
            MaterialFolder + "/SonicGrayscaleReveal.mat";
        private const string SurfaceRippleMaterialPath =
            MaterialFolder + "/SonicSurfaceRipple.mat";
        private const string InputActionsPath =
            "Assets/Samples/XR Interaction Toolkit/3.0.11/Starter Assets/XRI Default Input Actions.inputactions";
        private const string WaveColorLayerName = "WaveColor";
        private const string CurveControlLayerName = "CurveControl";
        private const string BuildSessionKey = "SonicWorld.BuildInProgress";

        [MenuItem("Tools/Sonic World/Build Demo Scene")]
        public static void BuildFromMenu()
        {
            Build(true);
        }

        private static void Build(bool force)
        {
            if (!force && AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoScene) != null)
                return;

            if (force && AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoScene) != null)
            {
                if (!EditorUtility.DisplayDialog(
                        "Rebuild SonicWorldDemo?",
                        "This replaces only the generated SonicWorldDemo scene. SampleScene is not changed.",
                        "Rebuild",
                        "Cancel"))
                    return;

                AssetDatabase.DeleteAsset(DemoScene);
            }

            SessionState.SetBool(BuildSessionKey, true);
            try
            {
                EnsureFolder(RootFolder);
                EnsureFolder(SceneFolder);
                EnsureFolder(ProfileFolder);
                EnsureFolder(MaterialFolder);

                ConfigureAudioImporters();
                SonicSurfaceProfile[] profiles = CreateProfiles();
                Material sharedWorldMaterial = LoadTestMaterial();
                if (sharedWorldMaterial == null)
                    throw new InvalidOperationException("Test.mat was not found.");
                Material waveMaterial = CreateWaveMaterial();
                Material rippleMaterial = CreateSurfaceRippleMaterial();
                Material grayscaleMaterial = CreateGrayscaleMaterial();
                int waveLayer = EnsureLayer(WaveColorLayerName, 29);
                int curveLayer = EnsureLayer(CurveControlLayerName, 30);
                EnsureGrayscaleRendererFeature(grayscaleMaterial);
                EnsureColorWaveRendererFeature(waveLayer, curveLayer);

                if (!AssetDatabase.CopyAsset(SourceScene, DemoScene))
                    throw new InvalidOperationException("Could not copy SampleScene.");

                AssetDatabase.SaveAssets();
                Scene scene = EditorSceneManager.OpenScene(DemoScene, OpenSceneMode.Additive);
                PopulateScene(
                    scene,
                    profiles,
                    sharedWorldMaterial,
                    waveMaterial,
                    rippleMaterial);
                EditorSceneManager.SaveScene(scene);
                EditorSceneManager.CloseScene(scene, true);
                MakeFirstBuildScene();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[SonicWorld] SonicWorldDemo scene generated successfully.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                SessionState.SetBool(BuildSessionKey, false);
            }
        }

        private static void PopulateScene(
            Scene scene,
            SonicSurfaceProfile[] profiles,
            Material sharedWorldMaterial,
            Material waveMaterial,
            Material rippleMaterial)
        {
            GameObject systemRoot = new GameObject("Sonic World");
            SceneManager.MoveGameObjectToScene(systemRoot, scene);
            SonicCollisionAudio collisionAudio = systemRoot.AddComponent<SonicCollisionAudio>();
            SonicMKToonWorldDriver driver = systemRoot.AddComponent<SonicMKToonWorldDriver>();
            SonicColorRevealDriver colorReveal =
                systemRoot.AddComponent<SonicColorRevealDriver>();
            SonicWorldController controller = systemRoot.AddComponent<SonicWorldController>();

            AudioListener listener = FindInScene<AudioListener>(scene);
            if (listener == null)
            {
                Camera camera = FindInScene<Camera>(scene);
                if (camera != null)
                    listener = camera.gameObject.AddComponent<AudioListener>();
            }
            if (listener != null && listener.GetComponent<SonicAudioBus>() == null)
                listener.gameObject.AddComponent<SonicAudioBus>();

            EnsureControllerInteractors(scene);

            GameObject musicObject = new GameObject("BGM Player");
            SceneManager.MoveGameObjectToScene(musicObject, scene);
            musicObject.transform.SetParent(systemRoot.transform);
            AudioSource musicSource = musicObject.AddComponent<AudioSource>();
            musicSource.spatialBlend = 0f;
            SonicMusicPlayer musicPlayer = musicObject.AddComponent<SonicMusicPlayer>();
            SetObjectArray(musicPlayer, "playlist", new UnityEngine.Object[]
            {
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/BGM.mp3")
            });

            GameObject wave = new GameObject("Sonic Point Wave");
            SceneManager.MoveGameObjectToScene(wave, scene);
            wave.transform.SetParent(systemRoot.transform);
            wave.transform.position = new Vector3(0f, 1.75f, 4.2f);
            int waveLayer = EnsureLayer(WaveColorLayerName, 29);
            wave.layer = waveLayer;
            SonicPointWave pointWave = wave.AddComponent<SonicPointWave>();
            int curveLayer = EnsureLayer(CurveControlLayerName, 30);
            Transform[] curvePoints =
                CreateCurveControlPoints(wave.transform, waveMaterial, curveLayer);
            pointWave.Configure(waveMaterial, curvePoints);
            EnsureTriggerCurveInteractors(scene, curveLayer);

            CreateEnvironment(scene, systemRoot.transform, profiles[3], sharedWorldMaterial);
            EnsureDemoTerrain(scene);
            List<Rigidbody> resetBodies = new List<Rigidbody>();
            PrimitiveType[] shapes =
            {
                PrimitiveType.Cube,
                PrimitiveType.Sphere,
                PrimitiveType.Capsule,
                PrimitiveType.Cube,
                PrimitiveType.Sphere
            };
            string[] names = { "Wood", "Metal", "Glass", "Stone", "Slime" };
            float[] masses = { 1.1f, 2.7f, 0.75f, 3.2f, 0.55f };

            for (int i = 0; i < profiles.Length; i++)
            {
                Vector3 position = new Vector3((i - 2) * 1.15f, 1.2f, 2.1f);
                GameObject interactable = GameObject.CreatePrimitive(shapes[i]);
                interactable.name = names[i] + " Sonic Object";
                SceneManager.MoveGameObjectToScene(interactable, scene);
                interactable.transform.SetParent(systemRoot.transform);
                interactable.transform.position = position;
                interactable.transform.localScale = i == 2
                    ? new Vector3(0.62f, 0.72f, 0.62f)
                    : Vector3.one * 0.62f;
                interactable.GetComponent<Renderer>().sharedMaterial = sharedWorldMaterial;

                Rigidbody body = interactable.AddComponent<Rigidbody>();
                body.mass = masses[i];
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                XRGrabInteractable grab = interactable.AddComponent<XRGrabInteractable>();
                grab.useDynamicAttach = true;
                grab.attachEaseInTime = 0.05f;
                grab.throwOnDetach = true;
                grab.throwVelocityScale = 1f;
                grab.throwAngularVelocityScale = 0.85f;
                grab.trackScale = false;
                grab.farAttachMode = InteractableFarAttachMode.Far;
                grab.movementType = XRBaseInteractable.MovementType.Kinematic;
                SonicImpactEmitter impact = interactable.AddComponent<SonicImpactEmitter>();
                impact.Configure(profiles[i]);
                interactable.AddComponent<SonicSwingTracker>();
                interactable.AddComponent<SonicCollisionAwareGrabTransformer>();
                SonicMKToonTarget target = interactable.AddComponent<SonicMKToonTarget>();
                target.Configure(interactable.GetComponent<Renderer>(), null);
                SonicSurfaceRipple ripple =
                    interactable.AddComponent<SonicSurfaceRipple>();
                ripple.Configure(rippleMaterial);
                resetBodies.Add(body);
            }

            SetObjectArray(collisionAudio, "profiles", profiles);
            SetObjectReference(controller, "musicPlayer", musicPlayer);
            SetObjectArray(controller, "resetBodies", resetBodies.ToArray());
            _ = driver;
        }

        private static void CreateEnvironment(
            Scene scene,
            Transform parent,
            SonicSurfaceProfile stoneProfile,
            Material stoneMaterial)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Sonic Floor";
            SceneManager.MoveGameObjectToScene(floor, scene);
            floor.transform.SetParent(parent);
            floor.transform.position = new Vector3(0f, -0.2f, 2.4f);
            floor.transform.localScale = new Vector3(10f, 0.3f, 9f);
            floor.GetComponent<Renderer>().sharedMaterial = stoneMaterial;
            SonicImpactEmitter emitter = floor.AddComponent<SonicImpactEmitter>();
            emitter.Configure(stoneProfile);
            SonicMKToonTarget target = floor.AddComponent<SonicMKToonTarget>();
            target.Configure(floor.GetComponent<Renderer>(), null);

            GameObject back = GameObject.CreatePrimitive(PrimitiveType.Cube);
            back.name = "Reactive Back Wall";
            SceneManager.MoveGameObjectToScene(back, scene);
            back.transform.SetParent(parent);
            back.transform.position = new Vector3(0f, 2.5f, 6.4f);
            back.transform.localScale = new Vector3(10f, 5f, 0.25f);
            back.GetComponent<Renderer>().sharedMaterial = stoneMaterial;
            SonicImpactEmitter backEmitter = back.AddComponent<SonicImpactEmitter>();
            backEmitter.Configure(stoneProfile);
            SonicMKToonTarget backTarget = back.AddComponent<SonicMKToonTarget>();
            backTarget.Configure(back.GetComponent<Renderer>(), null);
        }

        private static SonicSurfaceProfile[] CreateProfiles()
        {
            return new[]
            {
                CreateProfile(
                    "Wood", SonicSurfaceType.Wood, "Assets/Audio/Wood.mp3",
                    330f, 5.2f, 0.38f, 0.32f, 0.72f, 1.18f,
                    1f, 0.8f, 0.5f, 0.2f, 0.6f),
                CreateProfile(
                    "Metal", SonicSurfaceType.Metal, "Assets/Audio/Metal.mp3",
                    1180f, 1.5f, 0.92f, 0.08f, 0.78f, 1.45f,
                    1.2f, 1.25f, 0.8f, 1.5f, 0.35f),
                CreateProfile(
                    "Glass", SonicSurfaceType.Glass, "Assets/Audio/Glass.mp3",
                    2150f, 2.3f, 1f, 0.04f, 0.82f, 1.5f,
                    1.15f, 1.1f, 0.9f, 1.7f, 0.25f),
                CreateProfile(
                    "Stone", SonicSurfaceType.Stone, "Assets/Audio/Rock.mp3",
                    190f, 7.5f, 0.28f, 0.42f, 0.66f, 1.1f,
                    0.75f, 1.1f, 0.4f, 0.15f, 0.35f),
                CreateProfile(
                    "Soft", SonicSurfaceType.Soft, "Assets/Audio/Slime.mp3",
                    85f, 12f, 0.08f, 0.7f, 0.58f, 0.96f,
                    0.65f, 0.35f, 0.3f, 0.05f, 1.2f)
            };
        }

        private static SonicSurfaceProfile CreateProfile(
            string name,
            SonicSurfaceType type,
            string clipPath,
            float resonance,
            float decay,
            float brightness,
            float noise,
            float slowPitch,
            float fastPitch,
            float emission,
            float outline,
            float rim,
            float iridescence,
            float vertex)
        {
            string path = $"{ProfileFolder}/{name}.asset";
            SonicSurfaceProfile profile = AssetDatabase.LoadAssetAtPath<SonicSurfaceProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<SonicSurfaceProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            profile.surfaceType = type;
            profile.impactClip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            profile.resonance = resonance;
            profile.decay = decay;
            profile.brightness = brightness;
            profile.noise = noise;
            profile.slowPitch = slowPitch;
            profile.fastPitch = fastPitch;
            profile.emissionResponse = emission;
            profile.outlineResponse = outline;
            profile.rimResponse = rim;
            profile.iridescenceResponse = iridescence;
            profile.vertexResponse = vertex;
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static Material[] CreateWorldMaterials(SonicSurfaceProfile[] profiles)
        {
            Color[] colors =
            {
                new Color(0.48f, 0.2f, 0.07f),
                new Color(0.16f, 0.32f, 0.48f),
                new Color(0.1f, 0.65f, 0.88f),
                new Color(0.24f, 0.2f, 0.34f),
                new Color(0.42f, 0.9f, 0.35f)
            };
            Material[] materials = new Material[profiles.Length];
            Shader shader = Shader.Find("MK/Toon/URP/Standard/Simple + Outline");
            if (shader == null)
                throw new InvalidOperationException("MK Toon Simple + Outline shader was not found.");

            for (int i = 0; i < materials.Length; i++)
            {
                string path = $"{MaterialFolder}/{profiles[i].surfaceType}.mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(shader) { name = profiles[i].surfaceType.ToString() };
                    AssetDatabase.CreateAsset(material, path);
                }

                Properties.albedoColor.SetValue(material, colors[i]);
                Properties.emissionColor.SetValue(material, Color.black);
                Properties.light.SetValue(material, MK.Toon.Light.Banded);
                Properties.lightBands.SetValue(material, 4);
                Properties.lightBandsScale.SetValue(material, 0.5f);
                Properties.lightThreshold.SetValue(material, 0.35f);
                Properties.rim.SetValue(material, Rim.Default);
                Properties.rimSize.SetValue(material, 0.28f);
                Properties.rimColor.SetValue(material, Color.Lerp(colors[i], Color.white, 0.45f));
                Properties.iridescence.SetValue(material, Iridescence.On);
                Properties.iridescenceSize.SetValue(material, 1.1f);
                Properties.iridescenceColor.SetValue(material, Color.cyan);
                Properties.vertexAnimation.SetValue(material, VertexAnimation.Sine);
                Properties.vertexAnimationIntensity.SetValue(material, 0.01f);
                Properties.vertexAnimationFrequency.SetValue(material, new Vector3(1.2f, 1.8f, 1.1f));
                Properties.outline.SetValue(material, Outline.HullOrigin);
                Properties.outlineSize.SetValue(material, 1.35f);
                Properties.outlineColor.SetValue(material, colors[i] * 0.18f);
                Properties.outlineNoise.SetValue(material, 0f);
                Properties.UpdateSystemProperties(material);
                EditorUtility.SetDirty(material);
                materials[i] = material;
            }

            return materials;
        }

        private static Material CreateWaveMaterial()
        {
            string path = $"{MaterialFolder}/SonicPointWave.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            Shader shader = Shader.Find("MK/Toon/URP/Particles/Unlit");
            if (shader == null)
                throw new InvalidOperationException("MK Toon Particle Unlit shader was not found.");

            material = new Material(shader) { name = "Sonic Point Wave" };
            Properties.surface.SetValue(material, Surface.Transparent);
            Properties.blend.SetValue(material, Blend.Additive);
            Properties.albedoColor.SetValue(material, Color.white);
            Properties.emissionColor.SetValue(material, Color.white * 1.8f);
            Properties.UpdateSystemProperties(material);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Material CreateGrayscaleMaterial()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(
                $"{RootFolder}/Shaders/SonicGrayscaleReveal.shader");
            if (shader == null)
                shader = Shader.Find("Hidden/SonicWorld/Grayscale Reveal");
            if (shader == null)
                throw new InvalidOperationException(
                    "SonicWorld grayscale reveal shader was not found.");

            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(GrayscaleMaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "Sonic Grayscale Reveal" };
                material.SetFloat("_GrayscaleContrast", 1.08f);
                material.SetFloat("_GrayscaleBrightness", 0f);
                AssetDatabase.CreateAsset(material, GrayscaleMaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        private static Material CreateSurfaceRippleMaterial()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(
                $"{RootFolder}/Shaders/SonicSurfaceRipple.shader");
            if (shader == null)
                shader = Shader.Find("SonicWorld/Surface Ripple");
            if (shader == null)
                throw new InvalidOperationException(
                    "Sonic surface ripple shader was not found.");

            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(SurfaceRippleMaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "Sonic Surface Ripple" };
                material.SetColor(
                    "_RippleTint",
                    new Color(0.08f, 1.25f, 2.1f, 1f));
                material.SetFloat("_RippleDisplacement", 0.025f);
                material.SetFloat("_RippleBrightness", 3.2f);
                AssetDatabase.CreateAsset(material, SurfaceRippleMaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        private static Material LoadTestMaterial()
        {
            Material material =
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Test.mat");
            if (material != null)
                return material;
            return AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Material-Test/Test.mat");
        }

        private static void EnsureGrayscaleRendererFeature(Material material)
        {
            UniversalRendererData rendererData =
                AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererDataPath);
            if (rendererData == null)
                throw new InvalidOperationException(
                    $"Universal Renderer Data was not found at {RendererDataPath}.");

            FullScreenPassRendererFeature feature = null;
            foreach (ScriptableRendererFeature candidate in rendererData.rendererFeatures)
            {
                if (candidate is FullScreenPassRendererFeature fullScreen &&
                    candidate.name == "Sonic Grayscale Reveal")
                {
                    feature = fullScreen;
                    break;
                }
            }

            bool created = feature == null;
            if (created)
            {
                feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feature.name = "Sonic Grayscale Reveal";
                AssetDatabase.AddObjectToAsset(feature, rendererData);
                rendererData.rendererFeatures.Add(feature);
            }

            bool changed =
                created ||
                feature.injectionPoint !=
                    FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing ||
                !feature.fetchColorBuffer ||
                feature.requirements != ScriptableRenderPassInput.Depth ||
                feature.passMaterial != material ||
                feature.passIndex != 0 ||
                feature.bindDepthStencilAttachment;

            feature.injectionPoint =
                FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
            feature.fetchColorBuffer = true;
            feature.requirements = ScriptableRenderPassInput.Depth;
            feature.passMaterial = material;
            feature.passIndex = 0;
            feature.bindDepthStencilAttachment = false;
            feature.SetActive(true);
            feature.Create();

            if (changed)
            {
                EditorUtility.SetDirty(feature);
                EditorUtility.SetDirty(rendererData);
                rendererData.SetDirty();
                AssetDatabase.SaveAssets();
                UpdateRendererFeatureMap(rendererData);
                AssetDatabase.SaveAssets();
            }
        }

        private static void EnsureColorWaveRendererFeature(
            int waveLayer,
            int curveLayer)
        {
            UniversalRendererData rendererData =
                AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererDataPath);
            if (rendererData == null)
                throw new InvalidOperationException(
                    $"Universal Renderer Data was not found at {RendererDataPath}.");

            int colorLayerMask = (1 << waveLayer) | (1 << curveLayer);
            RenderObjects feature = null;
            foreach (ScriptableRendererFeature candidate in rendererData.rendererFeatures)
            {
                if (candidate is RenderObjects renderObjects &&
                    candidate.name == "Sonic Color Wave")
                {
                    feature = renderObjects;
                    break;
                }
            }

            bool created = feature == null;
            if (created)
            {
                feature = ScriptableObject.CreateInstance<RenderObjects>();
                feature.name = "Sonic Color Wave";
                AssetDatabase.AddObjectToAsset(feature, rendererData);
                rendererData.rendererFeatures.Add(feature);
            }

            RenderObjects.RenderObjectsSettings settings = feature.settings;
            RenderPassEvent colorEvent =
                (RenderPassEvent)((int)RenderPassEvent.AfterRenderingPostProcessing + 1);
            bool reordered =
                rendererData.rendererFeatures.Count == 0 ||
                rendererData.rendererFeatures[
                    rendererData.rendererFeatures.Count - 1] != feature;
            if (reordered)
            {
                rendererData.rendererFeatures.Remove(feature);
                rendererData.rendererFeatures.Add(feature);
            }

            int opaqueMask = rendererData.opaqueLayerMask.value & ~colorLayerMask;
            int transparentMask =
                rendererData.transparentLayerMask.value & ~colorLayerMask;
            bool changed =
                created ||
                reordered ||
                settings.passTag != "Sonic Color Wave" ||
                settings.Event != colorEvent ||
                settings.filterSettings.RenderQueueType !=
                    RenderQueueType.Transparent ||
                settings.filterSettings.LayerMask.value != colorLayerMask ||
                settings.filterSettings.PassNames != null ||
                settings.overrideMode !=
                    RenderObjects.RenderObjectsSettings.OverrideMaterialMode.None ||
                settings.overrideDepthState ||
                rendererData.opaqueLayerMask.value != opaqueMask ||
                rendererData.transparentLayerMask.value != transparentMask;

            settings.passTag = "Sonic Color Wave";
            settings.Event = colorEvent;
            settings.filterSettings.RenderQueueType = RenderQueueType.Transparent;
            settings.filterSettings.LayerMask = colorLayerMask;
            settings.filterSettings.PassNames = null;
            settings.overrideMode =
                RenderObjects.RenderObjectsSettings.OverrideMaterialMode.None;
            settings.overrideMaterial = null;
            settings.overrideShader = null;
            settings.overrideDepthState = false;
            rendererData.opaqueLayerMask = opaqueMask;
            rendererData.transparentLayerMask = transparentMask;
            feature.SetActive(true);
            feature.Create();

            if (changed)
            {
                EditorUtility.SetDirty(feature);
                EditorUtility.SetDirty(rendererData);
                rendererData.SetDirty();
                AssetDatabase.SaveAssets();
                UpdateRendererFeatureMap(rendererData);
                AssetDatabase.SaveAssets();
            }
        }

        private static void UpdateRendererFeatureMap(ScriptableRendererData rendererData)
        {
            SerializedObject serialized = new SerializedObject(rendererData);
            SerializedProperty map = serialized.FindProperty("m_RendererFeatureMap");
            map.arraySize = rendererData.rendererFeatures.Count;
            for (int i = 0; i < rendererData.rendererFeatures.Count; i++)
            {
                ScriptableRendererFeature feature = rendererData.rendererFeatures[i];
                long localId = 0;
                if (feature != null)
                {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        feature,
                        out string unusedGuid,
                        out localId);
                }
                map.GetArrayElementAtIndex(i).longValue = localId;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureAudioImporters()
        {
            string[] impacts = { "Wood", "Metal", "Glass", "Rock", "Slime" };
            foreach (string name in impacts)
            {
                AudioImporter importer =
                    AssetImporter.GetAtPath($"Assets/Audio/{name}.mp3") as AudioImporter;
                if (importer == null)
                    continue;

                importer.forceToMono = true;
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.PCM;
                settings.preloadAudioData = true;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }

            AudioImporter bgm = AssetImporter.GetAtPath("Assets/Audio/BGM.mp3") as AudioImporter;
            if (bgm != null)
            {
                bgm.forceToMono = false;
                AudioImporterSampleSettings settings = bgm.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.CompressedInMemory;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.9f;
                settings.preloadAudioData = true;
                bgm.defaultSampleSettings = settings;
                bgm.SaveAndReimport();
            }
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null)
                    return component;
            }
            return null;
        }

        private static void SetObjectReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectArray(
            UnityEngine.Object target,
            string propertyName,
            IReadOnlyList<UnityEngine.Object> values)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            string folder = path.Substring(slash + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }

        private static void MakeFirstBuildScene()
        {
            List<EditorBuildSettingsScene> scenes =
                new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(scene => scene.path == DemoScene);
            scenes.Insert(0, new EditorBuildSettingsScene(DemoScene, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void UpgradeGeneratedMaterials()
        {
            bool changed = false;
            foreach (SonicSurfaceType type in Enum.GetValues(typeof(SonicSurfaceType)))
            {
                Material material =
                    AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{type}.mat");
                if (material == null || Properties.outlineNoise.GetValue(material) > -0.99f)
                    continue;

                Properties.outlineNoise.SetValue(material, 0f);
                Properties.UpdateSystemProperties(material);
                EditorUtility.SetDirty(material);
                changed = true;
            }

            if (changed)
                AssetDatabase.SaveAssets();
        }

        private static void UpgradeGeneratedScene()
        {
            Scene scene = SceneManager.GetSceneByPath(DemoScene);
            bool openedForUpgrade = !scene.IsValid() || !scene.isLoaded;
            if (openedForUpgrade)
                scene = EditorSceneManager.OpenScene(DemoScene, OpenSceneMode.Additive);

            bool changed = EnsureControllerInteractors(scene);
            changed |= EnsureStableGrabSettings(scene);
            changed |= EnsureSharedTestMaterial(scene);
            changed |= UpgradeLineWave(scene);
            changed |= EnsureColorRevealDriver(scene);
            changed |= EnsureSurfaceRipples(scene);
            changed |= EnsureDemoTerrain(scene);
            changed |= EnsureBgmActive(scene);
            if (changed)
                EditorSceneManager.SaveScene(scene);
            if (openedForUpgrade)
                EditorSceneManager.CloseScene(scene, true);
        }

        private static bool EnsureControllerInteractors(Scene scene)
        {
            bool changed = false;
            changed |= EnsureControllerInteractor(
                scene,
                "LeftHand",
                "Assets/Samples/XR Interaction Toolkit/3.0.11/Starter Assets/Prefabs/Interactors/Left_NearFarInteractor.prefab");
            changed |= EnsureControllerInteractor(
                scene,
                "RightHand",
                "Assets/Samples/XR Interaction Toolkit/3.0.11/Starter Assets/Prefabs/Interactors/Right_NearFarInteractor.prefab");
            return changed;
        }

        private static bool EnsureColorRevealDriver(Scene scene)
        {
            if (FindInScene<SonicColorRevealDriver>(scene) != null)
                return false;

            SonicMKToonWorldDriver worldDriver =
                FindInScene<SonicMKToonWorldDriver>(scene);
            if (worldDriver == null)
                return false;

            worldDriver.gameObject.AddComponent<SonicColorRevealDriver>();
            return true;
        }

        private static bool EnsureControllerInteractor(
            Scene scene,
            string handName,
            string prefabPath)
        {
            GameObject hand = FindGameObjectInScene(scene, handName);
            if (hand == null || hand.GetComponentInChildren<XRBaseInteractor>(true) != null)
                return false;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[SonicWorld] Missing XRI interactor prefab: {prefabPath}");
                return false;
            }

            GameObject instance =
                PrefabUtility.InstantiatePrefab(prefab, hand.transform) as GameObject;
            if (instance == null)
                return false;

            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            return true;
        }

        private static GameObject FindGameObjectInScene(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                foreach (Transform candidate in transforms)
                {
                    if (candidate.name == objectName)
                        return candidate.gameObject;
                }
            }
            return null;
        }

        private static bool EnsureSharedTestMaterial(Scene scene)
        {
            Material testMaterial = LoadTestMaterial();
            if (testMaterial == null)
                return false;

            bool changed = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                SonicMKToonTarget[] targets =
                    root.GetComponentsInChildren<SonicMKToonTarget>(true);
                foreach (SonicMKToonTarget target in targets)
                {
                    Renderer renderer = target.TargetRenderer;
                    if (renderer == null)
                        continue;

                    Material[] current = renderer.sharedMaterials;
                    bool materialChanged = false;
                    for (int i = 0; i < current.Length; i++)
                    {
                        if (current[i] == testMaterial)
                            continue;
                        current[i] = testMaterial;
                        materialChanged = true;
                    }

                    if (materialChanged)
                    {
                        renderer.sharedMaterials = current;
                        changed = true;
                    }

                    changed |= EnsureUniformTargetResponse(target, renderer);
                }
            }
            return changed;
        }

        private static bool EnsureUniformTargetResponse(
            SonicMKToonTarget target,
            Renderer renderer)
        {
            SerializedObject serialized = new SerializedObject(target);
            bool changed =
                serialized.FindProperty("targetRenderer").objectReferenceValue != renderer ||
                !Mathf.Approximately(serialized.FindProperty("emission").floatValue, 1f) ||
                !Mathf.Approximately(serialized.FindProperty("outline").floatValue, 1f) ||
                !Mathf.Approximately(serialized.FindProperty("rim").floatValue, 1f) ||
                !Mathf.Approximately(serialized.FindProperty("iridescence").floatValue, 1f) ||
                !Mathf.Approximately(serialized.FindProperty("vertexAnimation").floatValue, 0.5f);
            if (!changed)
                return false;

            serialized.FindProperty("targetRenderer").objectReferenceValue = renderer;
            serialized.FindProperty("emission").floatValue = 1f;
            serialized.FindProperty("outline").floatValue = 1f;
            serialized.FindProperty("rim").floatValue = 1f;
            serialized.FindProperty("iridescence").floatValue = 1f;
            serialized.FindProperty("vertexAnimation").floatValue = 0.5f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool UpgradeLineWave(Scene scene)
        {
            SonicPointWave wave = FindInScene<SonicPointWave>(scene);
            if (wave == null)
                return false;

            Material waveMaterial =
                AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/SonicPointWave.mat");
            int waveLayer = EnsureLayer(WaveColorLayerName, 29);
            bool changed = wave.gameObject.layer != waveLayer;
            if (changed)
                wave.gameObject.layer = waveLayer;
            SerializedObject serializedWave = new SerializedObject(wave);
            SerializedProperty materialProperty = serializedWave.FindProperty("lineMaterial");
            changed |= materialProperty.objectReferenceValue != waveMaterial;
            materialProperty.objectReferenceValue = waveMaterial;
            serializedWave.ApplyModifiedPropertiesWithoutUndo();

            int curveLayer = EnsureLayer(CurveControlLayerName, 30);
            changed |= EnsureCurveControlPoints(wave, waveMaterial, curveLayer);
            changed |= EnsureTriggerCurveInteractors(scene, curveLayer);

            ParticleSystem legacySystem = wave.GetComponent<ParticleSystem>();
            if (legacySystem != null)
            {
                UnityEngine.Object.DestroyImmediate(legacySystem);
                changed = true;
            }

            ParticleSystemRenderer legacyRenderer = wave.GetComponent<ParticleSystemRenderer>();
            if (legacyRenderer != null)
            {
                UnityEngine.Object.DestroyImmediate(legacyRenderer);
                changed = true;
            }
            return changed;
        }

        private static bool EnsureCurveControlPoints(
            SonicPointWave wave,
            Material material,
            int curveLayer)
        {
            SonicCurveControlPoint[] existing =
                wave.GetComponentsInChildren<SonicCurveControlPoint>(true);
            Transform[] points;
            bool validCount =
                existing.Length >= SonicPointWave.MinimumControlPointCount &&
                existing.Length <= SonicPointWave.MaximumControlPointCount;
            bool changed = !validCount;
            if (validCount)
            {
                Array.Sort(
                    existing,
                    (first, second) =>
                        first.transform.GetSiblingIndex().CompareTo(
                            second.transform.GetSiblingIndex()));
                points = new Transform[existing.Length];
                for (int i = 0; i < existing.Length; i++)
                {
                    points[i] = existing[i].transform;
                    string expectedName = $"Curve Control {i + 1:00}";
                    if (existing[i].name != expectedName)
                    {
                        existing[i].name = expectedName;
                        changed = true;
                    }
                    if (existing[i].gameObject.layer != curveLayer)
                    {
                        existing[i].gameObject.layer = curveLayer;
                        changed = true;
                    }
                    Renderer renderer = existing[i].GetComponent<Renderer>();
                    if (renderer != null && renderer.sharedMaterial != material)
                    {
                        renderer.sharedMaterial = material;
                        changed = true;
                    }
                }
            }
            else
            {
                foreach (SonicCurveControlPoint point in existing)
                    UnityEngine.Object.DestroyImmediate(point.gameObject);
                points = CreateCurveControlPoints(
                    wave.transform,
                    material,
                    curveLayer);
            }

            SerializedObject serialized = new SerializedObject(wave);
            SerializedProperty controls = serialized.FindProperty("controlPoints");
            bool referencesChanged = controls.arraySize != points.Length;
            controls.arraySize = points.Length;
            for (int i = 0; i < points.Length; i++)
            {
                SerializedProperty element = controls.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue != points[i])
                {
                    element.objectReferenceValue = points[i];
                    referencesChanged = true;
                }
            }
            if (referencesChanged)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }
            return changed;
        }

        private static Transform[] CreateCurveControlPoints(
            Transform parent,
            Material material,
            int curveLayer)
        {
            Transform[] points = new Transform[6];
            for (int i = 0; i < points.Length; i++)
            {
                float t = i / (float)(points.Length - 1);
                GameObject point = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                point.name = $"Curve Control {i + 1:00}";
                point.layer = curveLayer;
                point.transform.SetParent(parent, false);
                point.transform.localPosition = new Vector3(
                    Mathf.Lerp(-6f, 6f, t),
                    Mathf.Sin(t * Mathf.PI * 2f) * 0.55f,
                    Mathf.Sin(t * Mathf.PI) * 0.65f);
                point.transform.localScale = Vector3.one * 0.1f;
                point.GetComponent<Renderer>().sharedMaterial = material;
                point.AddComponent<SonicCurveControlPoint>();
                points[i] = point.transform;
            }
            return points;
        }

        private static bool EnsureTriggerCurveInteractors(
            Scene scene,
            int curveLayer)
        {
            InputActionAsset actions =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (actions == null)
                return false;

            bool changed = false;
            changed |= EnsureTriggerCurveInteractor(
                scene,
                "LeftHand",
                actions,
                "XRI Left Interaction/Activate",
                curveLayer);
            changed |= EnsureTriggerCurveInteractor(
                scene,
                "RightHand",
                actions,
                "XRI Right Interaction/Activate",
                curveLayer);
            return changed;
        }

        private static bool EnsureTriggerCurveInteractor(
            Scene scene,
            string handName,
            InputActionAsset actions,
            string actionName,
            int curveLayer)
        {
            GameObject hand = FindGameObjectInScene(scene, handName);
            if (hand == null)
                return false;

            SonicTriggerCurveInteractor interactor =
                hand.GetComponent<SonicTriggerCurveInteractor>();
            bool changed = interactor == null;
            if (interactor == null)
                interactor = hand.AddComponent<SonicTriggerCurveInteractor>();

            SerializedObject serialized = new SerializedObject(interactor);
            SerializedProperty actionsProperty =
                serialized.FindProperty("inputActions");
            SerializedProperty actionNameProperty =
                serialized.FindProperty("triggerActionName");
            SerializedProperty layerProperty =
                serialized.FindProperty("controlLayer");
            int mask = 1 << curveLayer;
            if (actionsProperty.objectReferenceValue != actions ||
                actionNameProperty.stringValue != actionName ||
                layerProperty.intValue != mask)
            {
                actionsProperty.objectReferenceValue = actions;
                actionNameProperty.stringValue = actionName;
                layerProperty.intValue = mask;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }
            return changed;
        }

        private static bool EnsureStableGrabSettings(Scene scene)
        {
            bool changed = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                XRGrabInteractable[] interactables =
                    root.GetComponentsInChildren<XRGrabInteractable>(true);
                foreach (XRGrabInteractable grab in interactables)
                {
                    // Do not alter unrelated interactables inherited from SampleScene.
                    if (grab.GetComponent<SonicImpactEmitter>() == null)
                        continue;

                    bool grabChanged =
                        !grab.useDynamicAttach ||
                        !Mathf.Approximately(grab.attachEaseInTime, 0.05f) ||
                        grab.movementType != XRBaseInteractable.MovementType.Kinematic ||
                        grab.trackScale ||
                        !grab.throwOnDetach ||
                        !Mathf.Approximately(grab.throwVelocityScale, 1f) ||
                        !Mathf.Approximately(grab.throwAngularVelocityScale, 0.85f) ||
                        grab.farAttachMode != InteractableFarAttachMode.Far;

                    if (grabChanged)
                    {
                        grab.useDynamicAttach = true;
                        grab.attachEaseInTime = 0.05f;
                        grab.movementType = XRBaseInteractable.MovementType.Kinematic;
                        grab.trackScale = false;
                        grab.throwOnDetach = true;
                        grab.throwVelocityScale = 1f;
                        grab.throwAngularVelocityScale = 0.85f;
                        grab.farAttachMode = InteractableFarAttachMode.Far;
                        EditorUtility.SetDirty(grab);
                        changed = true;
                    }

                    Rigidbody body = grab.GetComponent<Rigidbody>();
                    if (body != null &&
                        body.collisionDetectionMode != CollisionDetectionMode.ContinuousSpeculative)
                    {
                        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                        EditorUtility.SetDirty(body);
                        changed = true;
                    }

                    if (body != null && body.interpolation != RigidbodyInterpolation.Interpolate)
                    {
                        body.interpolation = RigidbodyInterpolation.Interpolate;
                        EditorUtility.SetDirty(body);
                        changed = true;
                    }

                    if (grab.GetComponent<SonicCollisionAwareGrabTransformer>() == null)
                    {
                        grab.gameObject.AddComponent<SonicCollisionAwareGrabTransformer>();
                        changed = true;
                    }
                }
            }

            return changed;
        }

        private static bool EnsureSurfaceRipples(Scene scene)
        {
            Material material = CreateSurfaceRippleMaterial();
            bool changed = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                XRGrabInteractable[] interactables =
                    root.GetComponentsInChildren<XRGrabInteractable>(true);
                foreach (XRGrabInteractable interactable in interactables)
                {
                    if (interactable.GetComponent<MeshFilter>() == null)
                        continue;
                    SonicSurfaceRipple ripple =
                        interactable.GetComponent<SonicSurfaceRipple>();
                    if (ripple == null)
                    {
                        ripple = interactable.gameObject.AddComponent<SonicSurfaceRipple>();
                        changed = true;
                    }

                    SerializedObject serialized = new SerializedObject(ripple);
                    SerializedProperty materialProperty =
                        serialized.FindProperty("rippleMaterial");
                    if (materialProperty.objectReferenceValue != material)
                    {
                        materialProperty.objectReferenceValue = material;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        changed = true;
                    }
                }
            }
            return changed;
        }

        private static int EnsureLayer(string layerName, int preferredIndex)
        {
            UnityEngine.Object tagManager =
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
            SerializedObject serialized = new SerializedObject(tagManager);
            SerializedProperty layers = serialized.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).stringValue == layerName)
                    return i;
            }

            int target = preferredIndex;
            if (target < 8 ||
                target >= layers.arraySize ||
                !string.IsNullOrEmpty(layers.GetArrayElementAtIndex(target).stringValue))
            {
                target = -1;
                for (int i = 8; i < layers.arraySize; i++)
                {
                    if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                    {
                        target = i;
                        break;
                    }
                }
            }

            if (target < 0)
                throw new InvalidOperationException(
                    $"No free Unity layer is available for {layerName}.");
            layers.GetArrayElementAtIndex(target).stringValue = layerName;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return target;
        }

        private static bool EnsureDemoTerrain(Scene scene)
        {
            const string sourceName = "Sonic Demo Terrain Source";
            const string generatedName = "Sonic Demo Terrain MK Toon Mesh";
            GameObject existingGenerated =
                FindGameObjectInScene(scene, generatedName);
            SonicGeneratedTerrain existingMarker = existingGenerated != null
                ? existingGenerated.GetComponent<SonicGeneratedTerrain>()
                : null;
            if (existingMarker != null &&
                existingMarker.ConversionVersion >=
                    SonicTerrainConverter.ConversionVersion)
            {
                return false;
            }

            GameObject systemRoot = FindGameObjectInScene(scene, "Sonic World");
            if (systemRoot == null)
                return false;

            EnsureFolder($"{RootFolder}/GeneratedTerrain");
            EnsureFolder($"{RootFolder}/GeneratedTerrain/Demo");
            GameObject sourceObject = FindGameObjectInScene(scene, sourceName);
            Terrain sourceTerrain;
            if (sourceObject == null)
            {
                TerrainData terrainData = CreateDemoTerrainData();
                sourceObject = Terrain.CreateTerrainGameObject(terrainData);
                sourceObject.name = sourceName;
                SceneManager.MoveGameObjectToScene(sourceObject, scene);
                sourceObject.transform.SetParent(systemRoot.transform);
                sourceObject.transform.position = new Vector3(-9f, -0.35f, -4f);
                sourceTerrain = sourceObject.GetComponent<Terrain>();
            }
            else
            {
                sourceTerrain = sourceObject.GetComponent<Terrain>();
            }

            Material testMaterial = LoadTestMaterial();
            if (sourceTerrain == null || testMaterial == null)
                return sourceObject != null;

            GameObject generated = SonicTerrainConverter.Convert(
                sourceTerrain,
                testMaterial,
                $"{RootFolder}/GeneratedTerrain/Demo",
                true);
            if (generated != null)
            {
                GameObject floor = FindGameObjectInScene(scene, "Sonic Floor");
                if (floor != null)
                    floor.SetActive(false);
            }
            return true;
        }

        private static bool EnsureBgmActive(Scene scene)
        {
            SonicMusicPlayer player = FindInScene<SonicMusicPlayer>(scene);
            if (player == null || player.gameObject.activeSelf)
                return false;
            player.gameObject.SetActive(true);
            return true;
        }

        private static TerrainData CreateDemoTerrainData()
        {
            const string dataPath =
                "Assets/SonicWorld/GeneratedTerrain/DemoTerrainData.asset";
            TerrainData terrainData =
                AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
            if (terrainData != null)
                return terrainData;

            terrainData = new TerrainData
            {
                heightmapResolution = 129,
                alphamapResolution = 128,
                baseMapResolution = 128,
                size = new Vector3(18f, 2.2f, 18f)
            };
            AssetDatabase.CreateAsset(terrainData, dataPath);

            const int resolution = 129;
            float[,] heights = new float[resolution, resolution];
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float u = x / (float)(resolution - 1);
                    float v = z / (float)(resolution - 1);
                    float edge =
                        Mathf.SmoothStep(0f, 1f, Mathf.Min(
                            Mathf.Min(u, 1f - u),
                            Mathf.Min(v, 1f - v)) * 5f);
                    float hills =
                        Mathf.Sin(u * Mathf.PI * 2.2f) * 0.045f +
                        Mathf.Sin(v * Mathf.PI * 2.8f + 0.8f) * 0.035f +
                        Mathf.Sin((u + v) * Mathf.PI * 4.2f) * 0.016f;
                    float flatPlayArea = 1f - Mathf.SmoothStep(
                        0.1f,
                        0.36f,
                        Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.38f)));
                    heights[z, x] = Mathf.Max(
                        0f,
                        (0.075f + hills * edge) * (1f - flatPlayArea * 0.82f));
                }
            }
            terrainData.SetHeights(0, 0, heights);

            TerrainLayer cyanLayer = CreateDemoTerrainLayer(
                "DemoTerrain_Cyan",
                new Color(0.035f, 0.24f, 0.29f, 1f),
                new Color(0.04f, 0.42f, 0.38f, 1f));
            TerrainLayer violetLayer = CreateDemoTerrainLayer(
                "DemoTerrain_Violet",
                new Color(0.16f, 0.04f, 0.24f, 1f),
                new Color(0.43f, 0.08f, 0.45f, 1f));
            terrainData.terrainLayers = new[] { cyanLayer, violetLayer };

            const int alphaResolution = 128;
            float[,,] alpha = new float[alphaResolution, alphaResolution, 2];
            for (int z = 0; z < alphaResolution; z++)
            {
                for (int x = 0; x < alphaResolution; x++)
                {
                    float u = x / (float)(alphaResolution - 1);
                    float v = z / (float)(alphaResolution - 1);
                    float violet =
                        Mathf.Clamp01(
                            0.5f +
                            Mathf.Sin(u * 12f + Mathf.Sin(v * 7f)) * 0.28f +
                            Mathf.Sin(v * 15f) * 0.12f);
                    alpha[z, x, 0] = 1f - violet;
                    alpha[z, x, 1] = violet;
                }
            }
            terrainData.SetAlphamaps(0, 0, alpha);
            EditorUtility.SetDirty(terrainData);
            AssetDatabase.SaveAssets();
            return terrainData;
        }

        private static TerrainLayer CreateDemoTerrainLayer(
            string name,
            Color dark,
            Color bright)
        {
            string texturePath =
                $"Assets/SonicWorld/GeneratedTerrain/{name}_Texture.asset";
            Texture2D texture =
                AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                const int size = 64;
                texture = new Texture2D(
                    size,
                    size,
                    TextureFormat.RGBA32,
                    true)
                {
                    name = name + " Texture",
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear
                };
                Color[] colors = new Color[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float value =
                            Mathf.PerlinNoise(x * 0.095f, y * 0.095f);
                        colors[y * size + x] = Color.Lerp(dark, bright, value);
                    }
                }
                texture.SetPixels(colors);
                texture.Apply(true, false);
                AssetDatabase.CreateAsset(texture, texturePath);
            }

            string layerPath =
                $"Assets/SonicWorld/GeneratedTerrain/{name}.terrainlayer";
            TerrainLayer layer =
                AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (layer == null)
            {
                layer = new TerrainLayer { name = name };
                AssetDatabase.CreateAsset(layer, layerPath);
            }
            layer.diffuseTexture = texture;
            layer.tileSize = new Vector2(3.5f, 3.5f);
            layer.smoothness = 0.1f;
            layer.metallic = 0f;
            EditorUtility.SetDirty(layer);
            return layer;
        }
    }
}
#endif
