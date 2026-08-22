using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AbstractOcclusion.WebGpuWater;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbstractOcclusion.WebGpuWater.Editor
{
    /// <summary>
    /// Project integration sample. Kept in the package editor assembly so it can compose the same
    /// internal build primitives as the Water Wizard instead of duplicating package wiring.
    /// </summary>
    public static class WebGpuWaterProjectDemoBuilder
    {
        const string DemoRoot = "Assets/WebGpuWaterDemo";
        const string GeneratedRoot = DemoRoot + "/Generated";
        const string SceneFolder = DemoRoot + "/Scenes";
        const string ScenePath = SceneFolder + "/WebGpuWaterInteractivePool.unity";
        const string PcRendererPath = "Assets/Settings/PC_Renderer.asset";
        const string BuildRequestPath = DemoRoot + "/BuildRequest.txt";
        const string MenuPath = WaterBuildKit.MenuRoot + "Rebuild Project Demo";

        static readonly Vector3 PoolExtent = new Vector3(7f, 2.6f, 5f);

        [MenuItem(MenuPath, priority = 900)]
        public static void BuildFromMenu() => Build();

        [InitializeOnLoadMethod]
        static void ScheduleRequestedBuild()
        {
            if (File.Exists(BuildRequestPath)) EditorApplication.delayCall += TryRunRequestedBuild;
        }

        static void TryRunRequestedBuild()
        {
            if (!File.Exists(BuildRequestPath)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += TryRunRequestedBuild;
                return;
            }

            // Consume first so a build failure cannot create a domain-reload retry loop.
            AssetDatabase.DeleteAsset(BuildRequestPath);
            BuildFromCommandLine();
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                Build();
                Debug.Log("[WebGpuWaterDemo] BUILD_SUCCEEDED " + ScenePath);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("[WebGpuWaterDemo] BUILD_FAILED");
                throw;
            }
        }

        public static void ValidateFromCommandLine()
        {
            try
            {
                if (!File.Exists(ScenePath))
                    throw new FileNotFoundException("Generated demo scene not found.", ScenePath);

                Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                ValidateScene(scene);
                ValidateRendererFeatures();
                ValidateBuildSettings();
                Debug.Log("[WebGpuWaterDemo] VALIDATION_SUCCEEDED " + ScenePath);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("[WebGpuWaterDemo] VALIDATION_FAILED");
                throw;
            }
        }

        static void Build()
        {
            EnsureDemoFolders();
            EnsureRendererFeatures();

            Scene previousActive = SceneManager.GetActiveScene();
            Scene scene = default;
            bool additiveBuild = !Application.isBatchMode;
            try
            {
                // Interactive editor: additive construction keeps the user's currently open (possibly
                // dirty) scene intact. Batch mode owns its default untitled scene, so Single is both safe
                // and required (Unity refuses Additive beside an unsaved untitled scene).
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                    additiveBuild ? NewSceneMode.Additive : NewSceneMode.Single);
                SceneManager.SetActiveScene(scene);
                var root = new GameObject("WebGPU Water - Interactive Pool Demo");

                if (!WaterBuildKit.TryBuildSharedAssets(GeneratedRoot, buildPoolMaterial: true,
                                                        out BuildContext context))
                    throw new InvalidOperationException("WebGPU Water could not create its shared assets.");

                CreateDemoRig(context, root.transform);
                ConfigureCamera(context);
                WaterVolume water = WaterBuildKit.CreateWaterBody(
                    context, root.transform, "Hero Interactive Pool", Vector3.zero, PoolExtent,
                    primary: true, withPool: true, withGodRays: true,
                    withFoamParticles: true, withSplash: true);
                ConfigureWater(water);
                ConfigureFoamAndSplash(water);

                Material deckMaterial = CreateMaterial("Pool Deck", new Color(0.15f, 0.19f, 0.21f), 0.72f);
                Material accentMaterial = CreateMaterial("Pool Accent", new Color(0.03f, 0.34f, 0.43f), 0.78f);
                Material coralMaterial = CreateMaterial("Floater Coral", new Color(0.95f, 0.24f, 0.14f), 0.55f);
                Material goldMaterial = CreateMaterial("Floater Gold", new Color(1.00f, 0.64f, 0.08f), 0.48f);
                Material pearlMaterial = CreateMaterial("Floater Pearl", new Color(0.82f, 0.94f, 1.00f), 0.82f);

                Transform environment = new GameObject("Pool Architecture").transform;
                environment.SetParent(root.transform);
                CreatePoolArchitecture(environment, deckMaterial, accentMaterial);

                Transform props = new GameObject("Interactive Floaters").transform;
                props.SetParent(root.transform);
                var rigidbodies = new List<Rigidbody>
                {
                    CreateFloater("Coral Cube", PrimitiveType.Cube, new Vector3(-2.8f, 3.1f, -0.8f),
                                  new Vector3(0.95f, 0.95f, 0.95f), coralMaterial, props, water, 1.3f),
                    CreateFloater("Golden Sphere", PrimitiveType.Sphere, new Vector3(0.2f, 4.0f, 1.25f),
                                  Vector3.one * 1.05f, goldMaterial, props, water, 0.9f),
                    CreateFloater("Pearl Capsule", PrimitiveType.Capsule, new Vector3(3.0f, 3.45f, -1.6f),
                                  new Vector3(0.75f, 1.05f, 0.75f), pearlMaterial, props, water, 1.1f),
                };

                CreateWaveReflectors(props, accentMaterial);
                AddDemoController(root, water, rigidbodies);

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new InvalidOperationException("Failed to save scene: " + ScenePath);

                AddSceneToBuildSettings(ScenePath);
                ValidateScene(scene);
                ValidateRendererFeatures();
                ValidateBuildSettings();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[WebGpuWaterDemo] Rebuilt interactive pool demo at " + ScenePath);
            }
            finally
            {
                if (additiveBuild && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
                if (additiveBuild && previousActive.IsValid() && previousActive.isLoaded)
                    SceneManager.SetActiveScene(previousActive);
            }
        }

        static void EnsureDemoFolders()
        {
            WaterBuildKit.EnsureFolder(DemoRoot);
            WaterBuildKit.EnsureFolder(GeneratedRoot);
            WaterBuildKit.EnsureFolder(SceneFolder);
        }

        static void ConfigureCamera(BuildContext context)
        {
            context.Camera.gameObject.name = "Main Camera - drag background to orbit";
            context.Camera.allowHDR = true;
            context.Camera.backgroundColor = new Color(0.025f, 0.055f, 0.075f);
            context.Orbit.pivot = new Vector3(0f, -0.45f, 0f);
            context.Orbit.minDistance = 5f;
            context.Orbit.maxDistance = 28f;
            context.Orbit.SetView(-27f, -202f, 15.5f);
            EditorUtility.SetDirty(context.Camera);
            EditorUtility.SetDirty(context.Orbit);
        }

        static void CreateDemoRig(BuildContext context, Transform root)
        {
            var cameraObject = new GameObject("Main Camera - drag background to orbit");
            cameraObject.transform.SetParent(root);
            cameraObject.tag = WaterBuildKit.MainCameraTag;
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = WaterVolume.CameraFieldOfView;
            camera.nearClipPlane = WaterVolume.CameraNearClip;
            camera.farClipPlane = 250f;
            cameraObject.AddComponent<AudioListener>();

            context.Camera = camera;
            context.Orbit = cameraObject.AddComponent<OrbitCamera>();
            context.Sun = WaterBuildKit.CreateSun(root);
        }

        static void ConfigureWater(WaterVolume water)
        {
            water.rippleQuality = WaterVolume.RippleQuality.High;
            water.RippleRadius = 0.09f;
            water.RippleStrength = 0.02f;
            water.Foam = true;
            // A pool at rest should actually settle. Wind waves remain available in the Inspector,
            // but the demo starts with only physically-triggered interactive ripples.
            water.WindWaves = false;
            water.WaterFog = true;
            water.foamBorderWidth = 0.065f;
            water.refractShadows = false;
            water.foamPatternTexture = WaterBuildKit.LoadDefaultTexture("OceanWhitecap.png");
            water.oceanWhitecapTexture = WaterBuildKit.LoadDefaultTexture("Foam2.png");

            var serialized = new SerializedObject(water);
            Set(serialized, "reflectionSettings.useScreenSpaceReflection", true);
            Set(serialized, "reflectionSettings.usePlanarReflection", false);
            Set(serialized, "reflectionSettings.realRefraction", true);
            Set(serialized, "reflectionSettings.reflectionStrength", 0.92f);
            Set(serialized, "reflectionSettings.fresnelFloor", 0.025f);
            Set(serialized, "reflectionSettings.sunRoughness", 0.10f);
            Set(serialized, "reflectionSettings.refractionDistortion", 0.035f);
            Set(serialized, "detailNormalSettings.texture",
                WaterBuildKit.LoadDefaultTexture("water detail.png"));
            Set(serialized, "detailNormalSettings.strength", 0.22f);

            Set(serialized, "waterFogSettings.fogDensity", 0.28f);
            Set(serialized, "waterFogSettings.waterOpacity", 0.025f);
            Set(serialized, "depthAttenuation.depthDarken", true);
            Set(serialized, "depthAttenuation.depthDarkenStrength", 0.65f);
            Set(serialized, "depthAttenuation.screenSpaceCaustics", true);
            Set(serialized, "depthAttenuation.screenCausticIntensity", 1.0f);

            Set(serialized, "rippleSettings.waveSpeed", 0.52f);
            Set(serialized, "rippleSettings.damping", 0.965f);
            Set(serialized, "rippleSettings.seedRipplesOnStart", false);
            Set(serialized, "rippleSettings.rippleChoppiness", 0.08f);
            Set(serialized, "windWaveSettings.windSpeed", 1.2f);
            Set(serialized, "windWaveSettings.windFromDegrees", 28f);
            Set(serialized, "windWaveSettings.waveScaleMeters", 5f);
            Set(serialized, "windWaveSettings.waveAmplitudeScale", 0.45f);
            Set(serialized, "foamSettings.foamGenRate", 0.42f);
            Set(serialized, "foamSettings.foamDecay", 0.945f);
            Set(serialized, "foamSettings.foamStrength", 0.65f);
            Set(serialized, "foamSettings.foamFromSpeed", 3.0f);
            Set(serialized, "foamSettings.foamWakeStrength", 0.28f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(water);
        }

        static void ConfigureFoamAndSplash(WaterVolume water)
        {
            WaterFoamParticles particles = water.GetComponent<WaterFoamParticles>();
            if (particles == null) return;

            var particleSerialized = new SerializedObject(particles);
            Set(particleSerialized, "sprayChance", 0.04f);
            Set(particleSerialized, "sprayLaunchSpeed", 0.45f);
            Set(particleSerialized, "sprayLifeRange", new Vector2(0.45f, 0.9f));
            Set(particleSerialized, "spraySizeRange", new Vector2(0.012f, 0.028f));
            particleSerialized.ApplyModifiedPropertiesWithoutUndo();

            Material sprayMaterial = particles.sprayMaterial;
            if (sprayMaterial != null)
            {
                if (sprayMaterial.HasProperty("_VelocityStretch"))
                    sprayMaterial.SetFloat("_VelocityStretch", 1.15f);
                if (sprayMaterial.HasProperty("_ParticleOpacity"))
                    sprayMaterial.SetFloat("_ParticleOpacity", 0.46f);
                EditorUtility.SetDirty(sprayMaterial);
            }

            WaterFoamProfile profile = particleSerialized.FindProperty("profile")
                ?.objectReferenceValue as WaterFoamProfile;
            if (profile == null) return;

            var profileSerialized = new SerializedObject(profile);
            Set(profileSerialized, "look.opacity", 0.46f);
            Set(profileSerialized, "ambient.sprayChance", 0.04f);
            Set(profileSerialized, "ambient.sprayLaunchSpeed", 0.45f);
            Set(profileSerialized, "ambient.sprayLifeRange", new Vector2(0.45f, 0.9f));
            Set(profileSerialized, "ambient.spraySizeRange", new Vector2(0.012f, 0.028f));
            Set(profileSerialized, "splash.maxParticlesPerBurst", 24);
            Set(profileSerialized, "splash.upwardBias", 0.68f);
            Set(profileSerialized, "splash.outwardSpread", 0.9f);
            Set(profileSerialized, "splash.dropletSize", 0.014f);
            Set(profileSerialized, "splash.lifetime", new Vector2(0.4f, 0.8f));
            Set(profileSerialized, "splash.crownBaseSize", 0.3f);
            Set(profileSerialized, "splash.crownOpacity", 0.72f);
            profileSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
        }

        static void CreatePoolArchitecture(Transform parent, Material deck, Material accent)
        {
            float x = PoolExtent.x;
            float z = PoolExtent.z;
            CreateBlock("Deck North", new Vector3(0f, 0.12f, z + 0.72f),
                        new Vector3(x * 2f + 2.8f, 0.34f, 1.45f), deck, parent);
            CreateBlock("Deck South", new Vector3(0f, 0.12f, -z - 0.72f),
                        new Vector3(x * 2f + 2.8f, 0.34f, 1.45f), deck, parent);
            CreateBlock("Deck East", new Vector3(x + 0.72f, 0.12f, 0f),
                        new Vector3(1.45f, 0.34f, z * 2f), deck, parent);
            CreateBlock("Deck West", new Vector3(-x - 0.72f, 0.12f, 0f),
                        new Vector3(1.45f, 0.34f, z * 2f), deck, parent);

            CreateBlock("North Accent", new Vector3(0f, 0.34f, z + 0.08f),
                        new Vector3(x * 2f, 0.08f, 0.16f), accent, parent);
            CreateBlock("South Accent", new Vector3(0f, 0.34f, -z - 0.08f),
                        new Vector3(x * 2f, 0.08f, 0.16f), accent, parent);
            CreateBlock("East Accent", new Vector3(x + 0.08f, 0.34f, 0f),
                        new Vector3(0.16f, 0.08f, z * 2f), accent, parent);
            CreateBlock("West Accent", new Vector3(-x - 0.08f, 0.34f, 0f),
                        new Vector3(0.16f, 0.08f, z * 2f), accent, parent);

            CreateInvisiblePoolColliders(parent);
            WaterBuildKit.CreateFloorCollider(parent, new Vector3(0f, -PoolExtent.y - 0.12f, 0f),
                                               new Vector3(x * 2f, 0.24f, z * 2f));
        }

        static void CreateInvisiblePoolColliders(Transform parent)
        {
            float x = PoolExtent.x;
            float z = PoolExtent.z;
            float height = PoolExtent.y;
            CreateCollider("Pool Wall North", new Vector3(0f, -height * 0.5f, z + 0.08f),
                           new Vector3(x * 2f, height, 0.16f), parent);
            CreateCollider("Pool Wall South", new Vector3(0f, -height * 0.5f, -z - 0.08f),
                           new Vector3(x * 2f, height, 0.16f), parent);
            CreateCollider("Pool Wall East", new Vector3(x + 0.08f, -height * 0.5f, 0f),
                           new Vector3(0.16f, height, z * 2f), parent);
            CreateCollider("Pool Wall West", new Vector3(-x - 0.08f, -height * 0.5f, 0f),
                           new Vector3(0.16f, height, z * 2f), parent);
        }

        static GameObject CreateBlock(string name, Vector3 position, Vector3 scale,
                                      Material material, Transform parent)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent);
            block.transform.position = position;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().sharedMaterial = material;
            return block;
        }

        static void CreateCollider(string name, Vector3 position, Vector3 size, Transform parent)
        {
            var wall = new GameObject(name);
            wall.transform.SetParent(parent);
            wall.transform.position = position;
            wall.AddComponent<BoxCollider>().size = size;
        }

        static Rigidbody CreateFloater(string name, PrimitiveType type, Vector3 position, Vector3 scale,
                                       Material material, Transform parent, WaterVolume water, float mass)
        {
            GameObject floater = GameObject.CreatePrimitive(type);
            floater.name = name;
            floater.transform.SetParent(parent);
            floater.transform.position = position;
            floater.transform.localScale = scale;
            floater.GetComponent<Renderer>().sharedMaterial = material;

            Rigidbody body = floater.AddComponent<Rigidbody>();
            body.mass = mass;
            body.linearDamping = 0.12f;
            body.angularDamping = 0.28f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            WaterBuoyancy buoyancy = floater.AddComponent<WaterBuoyancy>();
            buoyancy.buoyancy = 2.5f;
            buoyancy.waterLinearDamping = 4.2f;
            buoyancy.waterAngularDamping = 2.2f;
            buoyancy.samplesPerAxis = 3;
            buoyancy.waveDriftStrength = 0.35f;
            buoyancy.verticalSettleDamping = 3.2f;
            buoyancy.objectWidth = Mathf.Max(scale.x, scale.z);
            buoyancy.maxBuoyancyForce = 18f;
            buoyancy.ignoreInteractiveRipples = true;
            buoyancy.drawDebugGizmos = false;

            WaterInteractable interactable = floater.AddComponent<WaterInteractable>();
            interactable.displaceScale = 0.4f;
            interactable.verticalEmitSpacing = 0.04f;
            interactable.horizontalEmitSpacing = 0.065f;
            interactable.rippleRadiusScale = 1.4f;

            WaterSplash splash = floater.AddComponent<WaterSplash>();
            splash.emitter = water.splashEmitter;
            splash.minImpactSpeed = 0.7f;
            splash.maxImpactSpeed = 4f;
            splash.rippleStrength = 0.025f;
            return body;
        }

        static void CreateWaveReflectors(Transform parent, Material material)
        {
            for (int i = 0; i < 3; i++)
            {
                GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = "Wave Reflector " + (i + 1);
                post.transform.SetParent(parent);
                post.transform.position = new Vector3(-5.5f + i * 5.5f, -0.75f, 3.65f);
                post.transform.localScale = new Vector3(0.28f, 1.35f, 0.28f);
                post.GetComponent<Renderer>().sharedMaterial = material;
                WaterInteractable interactable = post.AddComponent<WaterInteractable>();
                interactable.displaceScale = 0f;
                interactable.reflectsWaves = true;
            }
        }

        static Material CreateMaterial(string name, Color color, float smoothness)
        {
            string path = GeneratedRoot + "/" + name.Replace(" ", string.Empty) + ".mat";
            Shader shader = WaterBuildKit.DefaultPipelineMaterial().shader;
            return WaterBuildKit.LoadOrCreateMaterial(path, shader, material =>
            {
                material.name = name;
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
                if (material.HasProperty("_Color")) material.SetColor("_Color", color);
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
                if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.02f);
            });
        }

        static void AddDemoController(GameObject root, WaterVolume water, IReadOnlyList<Rigidbody> bodies)
        {
            Type type = Type.GetType(
                "MusicProgram.WebGpuWaterDemo.InteractiveWaterDemoController, Assembly-CSharp");
            if (type == null)
                throw new InvalidOperationException("InteractiveWaterDemoController has not compiled.");

            Component controller = root.AddComponent(type);
            var serialized = new SerializedObject(controller);
            Set(serialized, "water", water);
            SerializedProperty array = Required(serialized, "resetBodies");
            array.arraySize = bodies.Count;
            for (int i = 0; i < bodies.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = bodies[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureRendererFeatures()
        {
            UnityEngine.Object renderer = AssetDatabase.LoadMainAssetAtPath(PcRendererPath);
            if (renderer == null)
                throw new InvalidOperationException("PC URP renderer not found at " + PcRendererPath);

            EnsureRendererFeature(renderer, typeof(WaterUnderwaterFogFeature),
                                  "WebGPU Water - Underwater Fog", "underwaterFogShader",
                                  "AbstractOcclusion/WebGpuWater/WaterUnderwaterFog");
            EnsureRendererFeature(renderer, typeof(WaterCausticProjectionFeature),
                                  "WebGPU Water - Caustic Projection", "causticProjectionShader",
                                  "AbstractOcclusion/WebGpuWater/WaterCausticProjection");
            RepairRendererFeatureMap(renderer);
        }

        static void RepairRendererFeatureMap(UnityEngine.Object renderer)
        {
            MethodInfo validate = renderer.GetType().GetMethod(
                "ValidateRendererFeatures", BindingFlags.Instance | BindingFlags.NonPublic);
            if (validate == null)
                throw new MissingMethodException(renderer.GetType().FullName,
                                                 "ValidateRendererFeatures");

            if (!(bool)validate.Invoke(renderer, null))
                throw new InvalidOperationException("PC URP renderer contains a missing Renderer Feature.");

            EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssetIfDirty(renderer);
        }

        static void EnsureRendererFeature(UnityEngine.Object renderer, Type featureType, string featureName,
                                          string shaderProperty, string shaderName)
        {
            UnityEngine.Object feature = AssetDatabase.LoadAllAssetsAtPath(PcRendererPath)
                .FirstOrDefault(asset => asset != null && asset.GetType() == featureType);
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance(featureType);
                feature.name = featureName;
                AssetDatabase.AddObjectToAsset(feature, renderer);

                var rendererSerialized = new SerializedObject(renderer);
                SerializedProperty features = Required(rendererSerialized, "m_RendererFeatures");
                features.InsertArrayElementAtIndex(features.arraySize);
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
                rendererSerialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(renderer);
            }

            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Shader not found: " + shaderName);
            var featureSerialized = new SerializedObject(feature);
            Required(featureSerialized, shaderProperty).objectReferenceValue = shader;
            featureSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(feature);
        }

        static void AddSceneToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            int existing = scenes.FindIndex(scene => scene.path == path);
            if (existing >= 0) scenes[existing] = new EditorBuildSettingsScene(path, true);
            else scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void ValidateScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Demo scene is not loaded for validation.");

            GameObject[] roots = scene.GetRootGameObjects();
            int missingScripts = roots.Sum(CountMissingScripts);
            if (missingScripts != 0)
                throw new InvalidOperationException($"Demo scene has {missingScripts} missing script(s).");

            RequireCount<WaterVolume>(roots, 1);
            RequireCount<WaterBuoyancy>(roots, 3);
            RequireCount<WaterSplash>(roots, 3);
            RequireCount<WaterInteractable>(roots, 6);
            RequireCount<Camera>(roots, 1);

            Type controllerType = Type.GetType(
                "MusicProgram.WebGpuWaterDemo.InteractiveWaterDemoController, Assembly-CSharp");
            if (controllerType == null || roots.Sum(root =>
                    root.GetComponentsInChildren(controllerType, true).Length) != 1)
                throw new InvalidOperationException("Demo controller is missing or duplicated.");

            Debug.Log("[WebGpuWaterDemo] Scene validation passed: 1 water body, 3 buoyant " +
                      "splash props, 6 interactors, 1 camera, 0 missing scripts.");
        }

        static int CountMissingScripts(GameObject gameObject)
        {
            int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
            foreach (Transform child in gameObject.transform)
                count += CountMissingScripts(child.gameObject);
            return count;
        }

        static void RequireCount<T>(IEnumerable<GameObject> roots, int expected) where T : Component
        {
            int actual = roots.Sum(root => root.GetComponentsInChildren<T>(true).Length);
            if (actual != expected)
                throw new InvalidOperationException(
                    $"Demo scene expected {expected} {typeof(T).Name} component(s), found {actual}.");
        }

        static void ValidateRendererFeatures()
        {
            UnityEngine.Object renderer = AssetDatabase.LoadMainAssetAtPath(PcRendererPath);
            if (renderer == null)
                throw new InvalidOperationException("PC URP renderer not found at " + PcRendererPath);

            RepairRendererFeatureMap(renderer);
            var serialized = new SerializedObject(renderer);
            int featureCount = Required(serialized, "m_RendererFeatures").arraySize;
            int mapCount = Required(serialized, "m_RendererFeatureMap").arraySize;
            if (featureCount != mapCount)
                throw new InvalidOperationException(
                    $"PC renderer feature map mismatch: {featureCount} features, {mapCount} mappings.");

            foreach (Type type in new[] { typeof(WaterUnderwaterFogFeature),
                                          typeof(WaterCausticProjectionFeature) })
            {
                int count = AssetDatabase.LoadAllAssetsAtPath(PcRendererPath)
                    .Count(asset => asset != null && asset.GetType() == type);
                if (count != 1)
                    throw new InvalidOperationException(
                        $"PC renderer expected exactly one {type.Name}, found {count}.");
            }
        }

        static void ValidateBuildSettings()
        {
            int count = EditorBuildSettings.scenes.Count(scene =>
                scene.enabled && scene.path == ScenePath);
            if (count != 1)
                throw new InvalidOperationException(
                    "Demo scene must appear exactly once and enabled in Build Settings.");
        }

        static SerializedProperty Required(SerializedObject serialized, string path)
        {
            SerializedProperty property = serialized.FindProperty(path);
            if (property == null)
                throw new InvalidOperationException(serialized.targetObject.GetType().Name +
                                                    " has no serialized property '" + path + "'.");
            return property;
        }

        static void Set(SerializedObject serialized, string path, bool value) =>
            Required(serialized, path).boolValue = value;
        static void Set(SerializedObject serialized, string path, float value) =>
            Required(serialized, path).floatValue = value;
        static void Set(SerializedObject serialized, string path, int value) =>
            Required(serialized, path).intValue = value;
        static void Set(SerializedObject serialized, string path, Vector2 value) =>
            Required(serialized, path).vector2Value = value;
        static void Set(SerializedObject serialized, string path, UnityEngine.Object value) =>
            Required(serialized, path).objectReferenceValue = value;
    }
}
