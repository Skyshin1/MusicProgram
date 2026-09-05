using System;
using System.IO;
using System.Linq;
using AbstractOcclusion.WebGpuWater;
using SonicWorld.Weather;
using UniStorm;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;

namespace SonicWorld.Editor
{
    /// <summary>Idempotently installs the locked PC-VR thunderstorm into Assets/Scenes/1-VR.unity.</summary>
    [InitializeOnLoad]
    public static class UniStormThunderstormSceneInstaller
    {
        const string TargetScenePath = "Assets/Scenes/1-VR.unity";
        const string RootName = "UniStorm VR - Locked Thunderstorm";
        const string PrefabPath =
            "Assets/UniStorm Weather System/Resources/Systems/Resources/UniStorm VR System.prefab";
        const string ThunderstormPath =
            "Assets/UniStorm Weather System/Weather Types/Precipitation/Thunderstorm.asset";
        const string SkyboxPath =
            "Assets/UniStorm Weather System/Resources/Components/Resources/UniStorm Skybox.mat";
        const string GeneratedFolder = "Assets/SonicWorld/Generated/Weather";
        const string PcVrQualityPath = GeneratedFolder + "/PCVRStormWaterQuality.asset";
        const string SourceQualityPath =
            "Assets/WebGpuWaterDemo/Generated/Quest3WaterQuality.asset";
        const string SessionKey = "SonicWorld.LockedThunderstormInstalled.v2";

        static UniStormThunderstormSceneInstaller()
        {
            EditorApplication.delayCall += TryAutomaticInstall;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Tools/Sonic World/Install or Repair Locked Thunderstorm")]
        public static void InstallFromMenu() => InstallOrRepair(true);

        [MenuItem("Tools/Sonic World/Validate Locked Thunderstorm")]
        public static void ValidateFromMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != TargetScenePath)
                scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
            Validate(scene, true);
        }

        /// <summary>Headless entry point for automation after importing a newer UniStorm package.</summary>
        public static void InstallBatch()
        {
            Scene scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
            InstallOrRepairScene(scene);
        }

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += TryAutomaticInstall;
        }

        static void TryAutomaticInstall()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryAutomaticInstall;
                return;
            }
            if (SceneManager.GetActiveScene().path != TargetScenePath)
                return;
            if (SessionState.GetBool(SessionKey, false) && GameObject.Find(RootName) != null)
                return;

            InstallOrRepair(false);
        }

        static void InstallOrRepair(bool explicitRequest)
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (explicitRequest)
                    EditorUtility.DisplayDialog("Locked Thunderstorm",
                        "Exit Play Mode, then run the installer again.", "OK");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != TargetScenePath)
            {
                if (!explicitRequest)
                    return;
                scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
            }

            InstallOrRepairScene(scene);
        }

        static void InstallOrRepairScene(Scene scene)
        {
            bool renderGraphSupport = HasImportedRenderGraphSupport();
            bool rendererFeatureReady = HasUniStormCloudRendererFeature();
            if (renderGraphSupport && !rendererFeatureReady &&
                EditorApplication.ExecuteMenuItem("Window/UniStorm/Add URP Renderer Features"))
            {
                AssetDatabase.SaveAssets();
                rendererFeatureReady = HasUniStormCloudRendererFeature();
            }
            bool compatibilityReady = renderGraphSupport && rendererFeatureReady;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            WeatherType thunderstorm = AssetDatabase.LoadAssetAtPath<WeatherType>(ThunderstormPath);
            Material skybox = AssetDatabase.LoadAssetAtPath<Material>(SkyboxPath);
            if (prefab == null || thunderstorm == null || skybox == null)
            {
                Debug.LogError("[Locked Thunderstorm] UniStorm prefab, Thunderstorm weather, or " +
                               "UniStorm Skybox material is missing. Import/update UniStorm first.");
                return;
            }

            XROrigin origin = UnityEngine.Object.FindObjectsByType<XROrigin>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(item => item.gameObject.scene == scene);
            Camera camera = origin != null && origin.Camera != null
                ? origin.Camera
                : UnityEngine.Object.FindObjectsByType<Camera>(
                        FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .FirstOrDefault(item => item.gameObject.scene == scene && item.CompareTag("MainCamera"));
            if (origin == null || camera == null)
            {
                Debug.LogError("[Locked Thunderstorm] The 1-VR XR Origin or Main Camera was not found.");
                return;
            }

            UniStormSystem system = FindSceneComponent<UniStormSystem>(scene);
            GameObject weatherRoot;
            if (system == null)
            {
                weatherRoot = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                if (weatherRoot == null)
                    throw new InvalidOperationException("Could not instantiate the UniStorm VR prefab.");
                weatherRoot.name = RootName;
                weatherRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                system = weatherRoot.GetComponent<UniStormSystem>();
            }
            else
            {
                weatherRoot = system.gameObject;
                weatherRoot.name = RootName;
            }

            Light stormSun = weatherRoot.GetComponentsInChildren<Light>(true)
                .FirstOrDefault(light => light.name == "UniStorm Sun");
            if (stormSun == null)
                throw new InvalidOperationException("UniStorm Sun was not found in the VR prefab.");

            Light[] legacyLights = UnityEngine.Object.FindObjectsByType<Light>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(light => light.gameObject.scene == scene &&
                                !light.transform.IsChildOf(weatherRoot.transform) &&
                                light.type == LightType.Directional &&
                                (light.name == "Sun" || light.name == "Directional Light"))
                .ToArray();

            ConfigureUniStorm(system, thunderstorm, origin.transform, camera, stormSun);
            for (int i = 0; i < legacyLights.Length; i++)
            {
                Undo.RecordObject(legacyLights[i], "Configure scene sun compatibility");
                legacyLights[i].enabled = !compatibilityReady;
                EditorUtility.SetDirty(legacyLights[i]);
            }

            Light fallbackSun = legacyLights.FirstOrDefault(light => light.name == "Sun") ??
                                legacyLights.FirstOrDefault();
            Light activeSun = compatibilityReady ? stormSun : fallbackSun ?? stormSun;

            WaterQuality quality = EnsurePcVrWaterQuality();
            WaterVolume water = ConfigurePrimaryWater(scene, quality, camera, activeSun);

            LockedThunderstormEnvironment bridge =
                weatherRoot.GetComponent<LockedThunderstormEnvironment>();
            if (bridge == null)
                bridge = Undo.AddComponent<LockedThunderstormEnvironment>(weatherRoot);
            Undo.RecordObject(bridge, "Configure locked thunderstorm bridge");
            bridge.Configure(system, thunderstorm, origin.transform, camera, skybox,
                             stormSun, water, legacyLights);
            bridge.enabled = compatibilityReady;
            EditorUtility.SetDirty(bridge);

            // UniStorm 5.1's legacy URP passes render as black quads/magenta streaks in
            // Unity 6 Render Graph. Stage the complete setup, but do not render it until
            // the matching 5.4+ Render Graph support package has been imported.
            weatherRoot.SetActive(compatibilityReady);

            RenderSettings.skybox = skybox;
            RenderSettings.sun = activeSun;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.fog = false;

            EditorUtility.SetDirty(system);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            SessionState.SetBool(SessionKey, true);
            Selection.activeGameObject = weatherRoot;

            Validate(scene, false);
            if (compatibilityReady)
                Debug.Log("[Locked Thunderstorm] Installed the fixed Thunderstorm preset, XR bindings, " +
                          "single UniStorm sun, PC VR water quality, SSR and planar sky reflection.",
                          weatherRoot);
            else
                Debug.LogWarning("[Locked Thunderstorm] The complete storm setup is staged but inactive " +
                                 "to prevent broken Unity 6 rendering. Import UniStorm 5.4+ Render Graph " +
                                 "Support, run Window > UniStorm > Add URP Renderer Features if needed, " +
                                 "then run Tools > Sonic World > Install or Repair Locked Thunderstorm.",
                                 weatherRoot);
        }

        static void ConfigureUniStorm(UniStormSystem system, WeatherType thunderstorm,
                                      Transform player, Camera camera, Light stormSun)
        {
            Undo.RecordObject(system, "Configure locked UniStorm thunderstorm");
            system.PlatformType = UniStormSystem.PlatformTypeEnum.VR;
            system.PlayerTransform = player;
            system.PlayerCamera = camera;
            system.GetPlayerAtRuntime = UniStormSystem.EnableFeature.Disabled;
            system.UseRuntimeDelay = UniStormSystem.EnableFeature.Disabled;
            system.StartingHour = 17;
            system.StartingMinute = 0;
            system.Hour = 17;
            system.Minute = 0;
            system.TimeFlow = UniStormSystem.EnableFeature.Disabled;
            system.RealWorldTime = UniStormSystem.EnableFeature.Disabled;
            system.WeatherGeneration = UniStormSystem.EnableFeature.Disabled;
            system.CurrentWeatherType = thunderstorm;
            system.NextWeatherType = thunderstorm;
            system.CloudShadows = UniStormSystem.EnableFeature.Disabled;
            system.SunShaftsEffect = UniStormSystem.EnableFeature.Disabled;
            system.MoonShaftsEffect = UniStormSystem.EnableFeature.Disabled;
            system.UseUniStormMenu = UniStormSystem.EnableFeature.Disabled;
            system.FogType = UniStormSystem.FogTypeEnum.UnityFog;
            system.LightningStrikes = UniStormSystem.EnableFeature.Enabled;
            system.CloudType = UniStormSystem.CloudTypeEnum.Volumetric;
            system.CloudQuality = UniStormSystem.CloudQualityEnum.Medium;
            system.CustomizeQuality = UniStormSystem.CustomizeQualityEnum.Yes;
            system.NearMarchSteps = 64;
            system.DistantMarchSteps = 10;
            system.RendersPerFrame = 1;
            system.m_SunLight = stormSun;
        }

        static WaterQuality EnsurePcVrWaterQuality()
        {
            EnsureFolder(GeneratedFolder);
            WaterQuality quality = AssetDatabase.LoadAssetAtPath<WaterQuality>(PcVrQualityPath);
            if (quality == null)
            {
                if (AssetDatabase.LoadAssetAtPath<WaterQuality>(SourceQualityPath) != null)
                    AssetDatabase.CopyAsset(SourceQualityPath, PcVrQualityPath);
                else
                    AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<WaterQuality>(),
                                              PcVrQualityPath);
                quality = AssetDatabase.LoadAssetAtPath<WaterQuality>(PcVrQualityPath);
            }

            SerializedObject serialized = new SerializedObject(quality);
            Set(serialized, "selection", (int)WaterQuality.Selection.ForceMedium);
            Set(serialized, "mediumRichReflections", true);
            Set(serialized, "mediumRenderScale", 1f);
            Set(serialized, "mediumRealRefraction", true);
            Set(serialized, "mediumUnderwaterFog", (int)WaterQuality.UnderwaterMode.Full);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(quality);
            return quality;
        }

        static WaterVolume ConfigurePrimaryWater(Scene scene, WaterQuality quality,
                                                  Camera camera, Light stormSun)
        {
            WaterVolume[] waters = UnityEngine.Object.FindObjectsByType<WaterVolume>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            WaterVolume primary = null;
            foreach (WaterVolume water in waters)
            {
                if (water.gameObject.scene != scene)
                    continue;
                SerializedObject serialized = new SerializedObject(water);
                SerializedProperty isPrimary = serialized.FindProperty("isPrimary");
                if (primary == null || (isPrimary != null && isPrimary.boolValue))
                    primary = water;
                if (isPrimary != null && !isPrimary.boolValue)
                    continue;

                Set(serialized, "quality", quality);
                Set(serialized, "targetCamera", camera);
                Set(serialized, "sun", stormSun);
                Set(serialized, "configureCamera", false);
                Set(serialized, "reflectionSettings.useScreenSpaceReflection", true);
                Set(serialized, "reflectionSettings.usePlanarReflection", true);
                Set(serialized, "reflectionSettings.reflectUrpProbe", false);
                Set(serialized, "reflectionSettings.reflectionStrength", 1f);
                Set(serialized, "reflectionSettings.envReflectionIntensity", 0.45f);
                Set(serialized, "reflectionSettings.planarResolutionScale", 0.5f);
                Set(serialized, "reflectionSettings.planarFrameInterval", 1);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(water);
            }
            return primary;
        }

        static void Validate(Scene scene, bool logSuccess)
        {
            UniStormSystem system = FindSceneComponent<UniStormSystem>(scene);
            LockedThunderstormEnvironment bridge =
                FindSceneComponent<LockedThunderstormEnvironment>(scene);
            WaterVolume water = FindSceneComponent<WaterVolume>(scene);
            bool baseReady = system != null && bridge != null && water != null &&
                             system.CurrentWeatherType != null &&
                             system.CurrentWeatherType.name == "Thunderstorm" &&
                             system.PlayerTransform != null && system.PlayerCamera != null;

            bool renderGraphSupport = HasImportedRenderGraphSupport();
            bool rendererFeatureReady = HasUniStormCloudRendererFeature();

            if (!baseReady)
                Debug.LogError("[Locked Thunderstorm] Validation failed: scene wiring is incomplete.");
            else if (!renderGraphSupport || !rendererFeatureReady)
                Debug.LogWarning("[Locked Thunderstorm] Scene and water reflection are wired, but this " +
                                 "project is missing UniStorm 5.4+ Render Graph Support or its Clouds " +
                                 "Renderer Feature. Import the support package, run Window > UniStorm > " +
                                 "Add URP Renderer Features, then repair this scene again.", system);
            else if (logSuccess)
                Debug.Log("[Locked Thunderstorm] Validation passed.", system);
        }

        static bool HasImportedRenderGraphSupport()
        {
            const string root = "Assets/UniStorm Weather System";
            if (!Directory.Exists(root))
                return false;

            foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                string extension = Path.GetExtension(path);
                if (extension.Equals(".unitypackage", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".meta", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (path.IndexOf("Render Graph", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf("RenderGraph", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

                if (!extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
                    continue;

                string source = File.ReadAllText(path);
                if (source.IndexOf("RecordRenderGraph", StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
        }

        static bool HasUniStormCloudRendererFeature()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                UnityEngine.Object rendererData =
                    AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                if (rendererData == null)
                    continue;

                SerializedProperty features =
                    new SerializedObject(rendererData).FindProperty("m_RendererFeatures");
                if (features == null || !features.isArray)
                    continue;

                for (int i = 0; i < features.arraySize; i++)
                {
                    UnityEngine.Object feature =
                        features.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (feature == null)
                        continue;

                    string identity = feature.name + " " + feature.GetType().FullName;
                    if (identity.IndexOf("UniStorm", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        identity.IndexOf("Cloud", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }

            return false;
        }

        static T FindSceneComponent<T>(Scene scene) where T : Component =>
            UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(component => component.gameObject.scene == scene);

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        static void Set(SerializedObject target, string path, bool value)
        {
            SerializedProperty property = target.FindProperty(path);
            if (property != null) property.boolValue = value;
        }

        static void Set(SerializedObject target, string path, int value)
        {
            SerializedProperty property = target.FindProperty(path);
            if (property != null) property.intValue = value;
        }

        static void Set(SerializedObject target, string path, float value)
        {
            SerializedProperty property = target.FindProperty(path);
            if (property != null) property.floatValue = value;
        }

        static void Set(SerializedObject target, string path, UnityEngine.Object value)
        {
            SerializedProperty property = target.FindProperty(path);
            if (property != null) property.objectReferenceValue = value;
        }
    }
}
