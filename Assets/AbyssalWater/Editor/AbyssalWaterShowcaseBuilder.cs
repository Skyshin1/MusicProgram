using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace MusicProgram.AbyssalWater.Editor
{
    public static class AbyssalWaterShowcaseBuilder
    {
        const string Root = "Assets/AbyssalWater/Samples";
        const string ScenePath = Root + "/AbyssalWaterShowcase.unity";
        const string ProfilePath = Root + "/AbyssalWater_PC_VR_High.asset";
        const string WaterMaterialPath = Root + "/AbyssalWater_Surface.mat";
        const string RendererPath = "Assets/Settings/PC_Renderer.asset";
        const string FeatureName = "Abyssal Underwater — Absorption Waterline Caustics";

        [MenuItem("Tools/Abyssal Water/Build Complete Showcase")]
        public static void BuildShowcase()
        {
            EnsureFolder("Assets/AbyssalWater");
            EnsureFolder(Root);
            var profile = CreateOrUpdateProfile();
            var waterMaterial = CreateOrUpdateWaterMaterial();
            var seafloorMaterial = CreateLitMaterial(Root + "/Abyssal_Seafloor.mat",
                new Color(0.16f, 0.26f, 0.22f), 0f, 0.24f);
            var rockMaterial = CreateLitMaterial(Root + "/Abyssal_Rock.mat",
                new Color(0.12f, 0.15f, 0.16f), 0.08f, 0.32f);
            var metalMaterial = CreateLitMaterial(Root + "/Abyssal_Metal.mat",
                new Color(0.18f, 0.24f, 0.27f), 0.78f, 0.72f);
            var buoyMaterial = CreateLitMaterial(Root + "/Abyssal_Buoy.mat",
                new Color(0.88f, 0.23f, 0.055f), 0.18f, 0.58f);
            var sandMaterial = CreateLitMaterial(Root + "/Abyssal_Sand.mat",
                new Color(0.42f, 0.45f, 0.34f), 0f, 0.18f);

            EnsureRendererFeature();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "AbyssalWaterShowcase";

            var environment = new GameObject("Environment");
            CreateLighting(environment.transform);
            CreateSeafloor(environment.transform, seafloorMaterial, rockMaterial, sandMaterial);

            var cameraObject = new GameObject("Abyssal Showcase Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 1200f;
            camera.allowHDR = true;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.transform.position = new Vector3(0f, 7.5f, -17f);
            camera.transform.LookAt(new Vector3(0f, 0f, 7f));
            var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.requiresDepthTexture = true;
            cameraData.requiresColorTexture = true;
            cameraData.renderPostProcessing = true;
            var reflection = cameraObject.AddComponent<AbyssalPlanarReflection>();
            reflection.reflectionLayers = ~(1 << 4);
            reflection.useProfileQuality = true;
            reflection.renderShadows = true;

            var waterObject = new GameObject("Abyssal Water System");
            waterObject.layer = 4;
            var water = waterObject.AddComponent<AbyssalWaterSystem>();
            water.profile = profile;
            water.waterMaterial = waterMaterial;
            water.dynamicWaveCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(
                "Assets/AbyssalWater/Shaders/AbyssalDynamicWaves.compute");
            water.viewer = camera.transform;
            water.waterLevel = 0f;
            water.showDynamicWaveArea = true;

            var actors = new GameObject("Water Interaction Actors");
            CreateBuoyantActor("Orange Buoy", PrimitiveType.Sphere, new Vector3(-3.5f, 0.15f, 3.5f),
                new Vector3(1.25f, 1.25f, 1.25f), buoyMaterial, actors.transform, 0.9f, 0.62f);
            CreateBuoyantActor("Steel Cargo", PrimitiveType.Cube, new Vector3(4f, -0.1f, 6f),
                new Vector3(1.6f, 1.1f, 2.1f), metalMaterial, actors.transform, 1.35f, 0.8f);
            CreatePylons(actors.transform, metalMaterial);
            var submarine = CreateSubmarine(actors.transform, metalMaterial, buoyMaterial);

            var driverObject = new GameObject("Showcase Controls");
            var target = new GameObject("Camera Look Target");
            target.transform.SetParent(driverObject.transform, false);
            target.transform.position = new Vector3(0f, -1f, 7f);
            var driver = driverObject.AddComponent<AbyssalWaterShowcaseDriver>();
            driver.showcaseCamera = camera;
            driver.lookTarget = target.transform;
            driver.movingInteractor = submarine.transform;
            driver.water = water;

            CreateSkybox();
            RenderSettings.fog = false;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            Selection.activeObject = waterObject;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Abyssal Water: complete showcase built at {ScenePath}");
        }

        static AbyssalWaterProfile CreateOrUpdateProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<AbyssalWaterProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<AbyssalWaterProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            profile.quality = AbyssalWaterQuality.PcVrHigh;
            profile.showAdvanced = false;
            profile.waveHeight = 0.9f;
            profile.choppiness = 0.62f;
            profile.enableAntiTiling = true;
            profile.phaseWarpStrength = 0.65f;
            profile.phaseWarpPatchSize = 55f;
            profile.stochasticNormalBlend = 1f;
            profile.antiTilingSeed = 19373;
            profile.enableMicroSpectrum = true;
            profile.microWaveCount = 8;
            profile.microWaveAmplitude = 0.055f;
            profile.microMinimumWavelength = 0.35f;
            profile.microMaximumWavelength = 2.8f;
            profile.microDirectionSpread = 150f;
            profile.microChoppiness = 0.22f;
            profile.microWaveSpeed = 1.15f;
            profile.microSpectrumSeed = 27183;
            profile.normalStrength = 1f;
            profile.foamStrength = 0.35f;
            profile.crestFoamThreshold = 0.9f;
            profile.crestFoamFeather = 0.08f;
            profile.shorelineFoamDistance = 0.55f;
            profile.causticIntensity = 0.5f;
            profile.causticColor = new Color(0.24f, 0.72f, 0.78f, 1f);
            profile.causticScale = 1f;
            profile.causticFocus = 1.45f;
            profile.underwaterDistortion = 0.14f;
            profile.waterlineThickness = 0.045f;
            profile.waterlineMeniscus = 0.38f;
            profile.dynamicResolution = 256;
            profile.dynamicSubsteps = 2;
            profile.lodLevels = 6;
            profile.verticesPerLevel = 64;
            EditorUtility.SetDirty(profile);
            return profile;
        }

        static Material CreateOrUpdateWaterMaterial()
        {
            var shader = Shader.Find("MusicProgram/Abyssal Water/Surface");
            if (shader == null)
            {
                Debug.LogError("Abyssal Water surface shader is missing or failed to import.");
                return null;
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(WaterMaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "Abyssal Water Surface" };
                AssetDatabase.CreateAsset(material, WaterMaterialPath);
            }
            else material.shader = shader;

            material.SetTexture("_NormalMapA", AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/ThirdParty/UberStylizedWater/Textures/Normal 1.png"));
            material.SetTexture("_NormalMapB", AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/ThirdParty/UberStylizedWater/Textures/Normal 2.png"));
            material.SetTexture("_FoamNoise", AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/ThirdParty/UberStylizedWater/Textures/Foam 1.png"));
            material.SetVector("_NormalTiling", new Vector4(0.12f, 0.035f, 0f, 0f));
            material.SetVector("_NormalSpeeds", new Vector4(0.018f, -0.012f, -0.009f, 0.014f));
            material.SetFloat("_FineNormalStrength", 0.32f);
            material.SetFloat("_BroadNormalStrength", 0.46f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void EnsureRendererFeature()
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rendererData == null)
            {
                Debug.LogError($"Abyssal Water: renderer data not found at {RendererPath}");
                return;
            }
            var feature = rendererData.rendererFeatures
                .OfType<AbyssalUnderwaterRendererFeature>()
                .FirstOrDefault(item => item != null);
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<AbyssalUnderwaterRendererFeature>();
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
            feature.SetActive(true);
            feature.Create();
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(rendererData);
        }

        static void CreateLighting(Transform parent)
        {
            var lightObject = new GameObject("Sun");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.rotation = Quaternion.Euler(42f, -32f, 0f);
            var sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.94f, 0.82f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;

            var fillObject = new GameObject("Underwater Fill");
            fillObject.transform.SetParent(parent, false);
            fillObject.transform.position = new Vector3(-4f, -3f, 2f);
            var fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.color = new Color(0.035f, 0.34f, 0.44f);
            fill.range = 14f;
            fill.intensity = 0.55f;
        }

        static void CreateSeafloor(Transform parent, Material seafloor, Material rock, Material sand)
        {
            CreatePrimitive("Seafloor", PrimitiveType.Plane, new Vector3(0f, -8f, 30f),
                new Vector3(42f, 1f, 42f), seafloor, parent);
            CreatePrimitive("Shallow Sand Shelf", PrimitiveType.Cube, new Vector3(19f, -3.2f, 13f),
                new Vector3(17f, 1.8f, 22f), sand, parent);
            var positions = new[]
            {
                new Vector3(-10f, -6.5f, 8f), new Vector3(9f, -6.8f, 10f),
                new Vector3(-17f, -5.8f, 18f), new Vector3(4f, -6.4f, 24f),
                new Vector3(16f, -4.4f, 26f), new Vector3(-5f, -6.9f, 33f)
            };
            for (var i = 0; i < positions.Length; i++)
            {
                var item = CreatePrimitive($"Rock {i + 1}", PrimitiveType.Sphere, positions[i],
                    new Vector3(2.2f + i % 3, 1.4f + (i % 2) * 0.8f, 2.6f + (i + 1) % 3), rock, parent);
                item.transform.rotation = Quaternion.Euler(12f * i, 31f * i, 7f * i);
            }
        }

        static GameObject CreateBuoyantActor(string name, PrimitiveType type, Vector3 position,
            Vector3 scale, Material material, Transform parent, float radius, float strength)
        {
            var actor = CreatePrimitive(name, type, position, scale, material, parent);
            var body = actor.AddComponent<Rigidbody>();
            body.mass = Mathf.Max(1f, scale.x * scale.y * scale.z);
            body.linearDamping = 0.08f;
            body.angularDamping = 0.12f;
            actor.AddComponent<AbyssalBuoyancy>();
            var interactor = actor.AddComponent<AbyssalWaterInteractor>();
            interactor.radius = radius;
            interactor.strength = strength;
            interactor.activationDepth = 2f;
            return actor;
        }

        static void CreatePylons(Transform parent, Material material)
        {
            for (var i = 0; i < 7; i++)
            {
                CreatePrimitive($"Contact Pylon {i + 1}", PrimitiveType.Cylinder,
                    new Vector3(-10f + i * 3.2f, -0.8f, 11f + Mathf.Sin(i) * 1.5f),
                    new Vector3(0.32f, 2.5f, 0.32f), material, parent);
            }
        }

        static GameObject CreateSubmarine(Transform parent, Material metal, Material accent)
        {
            var root = new GameObject("Animated Surface Submarine");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(0f, 0f, 5f);
            var hull = CreatePrimitive("Hull", PrimitiveType.Capsule, Vector3.zero,
                new Vector3(1.15f, 2.8f, 1.15f), metal, root.transform);
            hull.transform.localPosition = Vector3.zero;
            hull.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var tower = CreatePrimitive("Tower", PrimitiveType.Cube, Vector3.zero,
                new Vector3(0.65f, 0.5f, 0.8f), accent, root.transform);
            tower.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            var interactor = root.AddComponent<AbyssalWaterInteractor>();
            interactor.radius = 1.6f;
            interactor.strength = 0.72f;
            interactor.speedToStrength = 0.12f;
            interactor.activationDepth = 3.5f;
            return root;
        }

        static GameObject CreatePrimitive(string name, PrimitiveType type, Vector3 position,
            Vector3 scale, Material material, Transform parent)
        {
            var item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.transform.SetParent(parent, true);
            item.transform.position = position;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            return item;
        }

        static Material CreateLitMaterial(string path, Color colour, float metallic, float smoothness)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void CreateSkybox()
        {
            const string path = Root + "/Abyssal_Skybox.mat";
            var shader = Shader.Find("Skybox/Procedural");
            var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (sky == null)
            {
                sky = new Material(shader) { name = "Abyssal Ocean Sky" };
                AssetDatabase.CreateAsset(sky, path);
            }
            sky.shader = shader;
            sky.SetColor("_SkyTint", new Color(0.32f, 0.55f, 0.72f));
            sky.SetColor("_GroundColor", new Color(0.13f, 0.2f, 0.25f));
            sky.SetFloat("_AtmosphereThickness", 0.82f);
            sky.SetFloat("_SunSize", 0.035f);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.28f, 0.43f, 0.58f);
            RenderSettings.ambientEquatorColor = new Color(0.12f, 0.24f, 0.3f);
            RenderSettings.ambientGroundColor = new Color(0.035f, 0.06f, 0.07f);
            EditorUtility.SetDirty(sky);
        }

        static void AddSceneToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(scene => scene.path != path))
                scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
