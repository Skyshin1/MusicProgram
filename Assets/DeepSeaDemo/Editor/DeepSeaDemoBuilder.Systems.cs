#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using AbstractOcclusion.WebGpuWater;
using DeepSeaAI;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UniStorm;
using Object = UnityEngine.Object;

namespace DeepSeaDemo.Editor
{
    public static partial class DeepSeaDemoBuilder
    {
        static UniversalRenderPipelineAsset CreatePipeline()
        {
            string rpPath = Root + "/Settings/DemoPCVRPipeline.asset";
            string rendererPath = Root + "/Settings/DemoPCVRRenderer.asset";
            var source = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            if (source == null) throw new InvalidOperationException("PC URP asset missing");
            var sourceData = new SerializedObject(source).FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue as ScriptableRendererData;
            var renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(rendererPath);
            if (renderer == null)
            {
                renderer = Object.Instantiate(sourceData); renderer.name = "DemoPCVRRenderer";
                renderer.rendererFeatures.Clear(); AssetDatabase.CreateAsset(renderer, rendererPath);
                foreach (var feature in sourceData.rendererFeatures)
                {
                    if (feature == null || feature.GetType().FullName.Contains("VolumetricFog")) continue;
                    var copy = Object.Instantiate(feature); copy.name = feature.name;
                    AssetDatabase.AddObjectToAsset(copy, renderer); renderer.rendererFeatures.Add(copy);
                }
            }
            // Do not inherit MK Toon/test full-screen effects/old independent fog from 1-VR.
            renderer.rendererFeatures.RemoveAll(f => f == null || !new[] {
                "WaterUnderwaterFogFeature", "WaterCausticProjectionFeature", "LargeBodyAtmosphereFeature",
                "SonarWhiteOutlineRendererFeature", "WaterExitDropletsRendererFeature", "DecalRendererFeature"
            }.Contains(f.GetType().Name));
            EnsureFeature<SonarWhiteOutlineRendererFeature>(renderer);
            EnsureFeature<WaterExitDropletsRendererFeature>(renderer);
            EnsureFeature<DecalRendererFeature>(renderer);
            config.runtimeShaders = AssetDatabase.FindAssets("t:Shader", new[] { "Assets/SonicWorld" })
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Shader>).Where(s => s != null).ToArray();
            foreach (var feature in renderer.rendererFeatures)
            {
                if (feature is SonarWhiteOutlineRendererFeature) Set(feature, "injectionPoint", (int)RenderPassEvent.AfterRenderingPostProcessing);
                if (feature is WaterExitDropletsRendererFeature) Set(feature, "injectionPoint", (int)RenderPassEvent.AfterRenderingPostProcessing + 1);
            }
            var rp = Asset<UniversalRenderPipelineAsset>(rpPath, () => Object.Instantiate(source));
            var uiRenderer = Asset<ScriptableRendererData>(Root + "/Settings/DemoUIRenderer.asset", () => Object.Instantiate(sourceData));
            uiRenderer.rendererFeatures.Clear(); EditorUtility.SetDirty(uiRenderer);
            var so = new SerializedObject(rp); var list = so.FindProperty("m_RendererDataList"); list.arraySize = 2;
            list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            list.GetArrayElementAtIndex(1).objectReferenceValue = uiRenderer;
            so.FindProperty("m_DefaultRendererIndex").intValue = 0; so.ApplyModifiedPropertiesWithoutUndo();
            rp.supportsCameraDepthTexture = true; rp.supportsCameraOpaqueTexture = true;
            rp.msaaSampleCount = 2; EditorUtility.SetDirty(rp); EditorUtility.SetDirty(renderer);
            notes.Add("Dedicated PCVR renderer features: " + string.Join(", ", renderer.rendererFeatures.Where(x => x != null).Select(x => x.GetType().Name)));
            return rp;
        }
        static void EnsureFeature<T>(ScriptableRendererData renderer) where T : ScriptableRendererFeature
        {
            if (renderer.rendererFeatures.Any(f => f is T)) return;
            var feature = ScriptableObject.CreateInstance<T>(); feature.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(feature, renderer); renderer.rendererFeatures.Add(feature);
        }
        static XROrigin CreatePlayer(Transform parent, DemoConfig cfg)
        {
            var playerRoot = Load("Assets/Prefab/Player.prefab", parent); playerRoot.name = "VR Player";
            PrefabUtility.UnpackPrefabInstance(playerRoot, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            playerRoot.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity); playerRoot.transform.localScale = Vector3.one;
            foreach (var b in playerRoot.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (b == null) continue;
                string name = b.GetType().Name;
                if (!new[] { "XROrigin", "InputActionManager", "TrackedPoseDriver", "UniversalAdditionalCameraData", "XRInteractionManager" }.Contains(name)) Object.DestroyImmediate(b);
            }
            foreach (var r in playerRoot.GetComponentsInChildren<Renderer>(true)) Object.DestroyImmediate(r);
            foreach (var m in playerRoot.GetComponentsInChildren<MeshFilter>(true)) Object.DestroyImmediate(m);
            foreach (var c in playerRoot.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            var origin = playerRoot.GetComponentInChildren<XROrigin>(true);
            if (origin == null) throw new InvalidOperationException("Player prefab lacks XR Origin");
            origin.transform.localPosition = new Vector3(0, 2.3f, -14); origin.transform.localRotation = Quaternion.identity; origin.transform.localScale = Vector3.one;
            origin.CameraFloorOffsetObject.transform.localPosition = new Vector3(0, 1.65f, 0);
            origin.Camera.transform.localPosition = Vector3.zero; origin.Camera.transform.localRotation = Quaternion.identity;
            origin.Camera.nearClipPlane = .06f; origin.Camera.farClipPlane = 220f;
            origin.Camera.GetUniversalAdditionalCameraData().SetRenderer(0);
            origin.Camera.cullingMask &= ~(1 << uiLayer);
            var uiCamera = Make("DeepSeaDemo UI Camera", origin.Camera.transform).AddComponent<Camera>();
            uiCamera.CopyFrom(origin.Camera); uiCamera.cullingMask = 1 << uiLayer; uiCamera.tag = "Untagged";
            var uiData = uiCamera.GetUniversalAdditionalCameraData(); uiData.renderType = CameraRenderType.Overlay; uiData.SetRenderer(1); Set(uiData, "m_ClearDepth", false);
            origin.Camera.GetUniversalAdditionalCameraData().cameraStack.Add(uiCamera);
            var cc = origin.gameObject.AddComponent<CharacterController>(); cc.height = 1.7f; cc.radius = .25f;
            cc.center = Vector3.up * .85f; cc.skinWidth = .025f; cc.stepOffset = .22f;
            origin.gameObject.AddComponent<WaterSurfaceStateTracker>();
            var movement = origin.gameObject.AddComponent<QuestLeftStickLocomotion>(); Set(movement, "moveSpeed", cfg.moveSpeed); Set(movement, "verticalSwimSpeed", cfg.swimSpeed);
            origin.gameObject.AddComponent<XRHandSonarInput>().enabled = false;
            origin.gameObject.AddComponent<SurfaceDocumentReader>().enabled = false;
            origin.gameObject.AddComponent<UnderwaterAmbienceController>(); origin.gameObject.AddComponent<WaterExitLensEffect>();
            var lantern = origin.gameObject.AddComponent<SonarFogLantern>(); Set(lantern, "radius", 1f);
            var oxygen = origin.gameObject.AddComponent<PlayerOxygen>(); Set(oxygen, "drainAutomatically", false); Set(oxygen, "respawnWhenDepleted", false);
            origin.gameObject.AddComponent<PlayerRespawnController>();
            var safety = origin.gameObject.AddComponent<DemoPlayerSafety>(); safety.config = cfg;
            var input = origin.gameObject.AddComponent<DemoInputRouter>(); input.config = cfg;
            Transform right = origin.GetComponentsInChildren<Transform>(true).First(t => t.name == "Right");
            Transform left = origin.GetComponentsInChildren<Transform>(true).First(t => t.name == "left");
            input.rightHand = right; input.leftHand = left;
            CreateHand(left, false); CreateHand(right, true);
            var meter = Make("Oxygen Meter", right); meter.transform.localPosition = new Vector3(.04f, .045f, -.02f);
            Box("Frame", meter.transform, Vector3.zero, new Vector3(.1f, .012f, .035f), Mat("MeterFrame", new Color(.025f, .03f, .035f)), playerLayer);
            var fill = Box("Scale controlled oxygen", meter.transform, new Vector3(0, .009f, 0), new Vector3(.088f, .009f, .024f), Mat("Oxygen", Color.cyan, true), playerLayer);
            var indicator = meter.AddComponent<DemoOxygenIndicator>(); indicator.oxygen = oxygen; indicator.fill = fill.transform;
            SetLayer(playerRoot, playerLayer);
            return origin;
        }
        static void CreateHand(Transform pose, bool right)
        {
            const string starter = "Assets/Samples/XR Interaction Toolkit/3.3.1/Starter Assets/Prefabs/Interactors/";
            var interactor = Load(starter + (right ? "Right_NearFarInteractor.prefab" : "Left_NearFarInteractor.prefab"), pose);
            foreach (var component in interactor.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null) continue;
                var so = new SerializedObject(component);
                var distance = so.FindProperty("m_CastDistance"); if (distance != null) distance.floatValue = 3f;
                var ray = so.FindProperty("m_RaycastMask"); if (ray != null) ray.intValue = config.worldMask | config.interactMask | config.uiMask;
                var physics = so.FindProperty("m_PhysicsLayerMask"); if (physics != null) physics.intValue = config.interactMask;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            var visual = Load("Assets/Animvs Game Studio/VR Hands/Prefabs/" + (right ? "SK_LeatherGlove.prefab" : "SK_LeatherGlove_left.prefab"), pose);
            visual.name = "Visual Leather Glove " + (right ? "Right" : "Left");
            visual.transform.localPosition = new Vector3(0, -.025f, -.05f);
            visual.transform.localRotation = Quaternion.Euler(0, right ? -90 : 90, 0);
            UpgradeMaterials(visual);
            var profile = Asset<DemoHandPose>(Root + "/Settings/LeatherGlove" + (right ? "Right" : "Left") + ".asset", () => ScriptableObject.CreateInstance<DemoHandPose>());
            profile.joints = visual.GetComponentsInChildren<Transform>(true)
                .Where(t => new[] { "thumb_", "index_", "middle_", "ring_", "pinky_" }.Any(p => t.name.StartsWith(p)))
                .Select(t => new DemoHandPose.Joint { name = t.name, index = t.name.StartsWith("index_"), curlAxis = Vector3.forward,
                    // Both supplied glove rigs have the same finger-local axes. The supplied
                    // left wrist handles mirroring; reversing the finger angles bends outward.
                    curlDegrees = -(t.name.StartsWith("thumb_") ? 30f : 65f) }).ToArray();
            EditorUtility.SetDirty(profile);
            var animation = visual.AddComponent<DemoHandAnimator>(); animation.right = right; animation.poses = profile;
            notes.Add("Hand " + visual.name + ": " + profile.joints.Length + " bone pose channels; exact selected left/right models.");
            PrefabUtility.SaveAsPrefabAsset(visual, Root + "/Prefabs/LeatherGlove" + (right ? "Right" : "Left") + ".prefab");
        }
        static void CreateWater(Scene previous, Transform parent, DemoFlow flow, Light sun)
        {
            var sourceScene = SceneManager.GetSceneByPath("Assets/Scenes/1-VR.unity"); bool close = !sourceScene.isLoaded;
            if (close) sourceScene = EditorSceneManager.OpenScene("Assets/Scenes/1-VR.unity", OpenSceneMode.Additive);
            var source = sourceScene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<WaterVolume>(true)).FirstOrDefault();
            if (source == null) throw new InvalidOperationException("No configured WaterVolume in 1-VR");
            var waterRoot = Object.Instantiate(source.transform.root.gameObject); waterRoot.name = "Water Volume Environment";
            SceneManager.MoveGameObjectToScene(waterRoot, scene); waterRoot.transform.SetParent(parent, false); waterRoot.transform.position = Vector3.zero;
            foreach (var t in waterRoot.GetComponentsInChildren<Transform>(true).ToArray())
                if (t != null && (t.name == "Floor Collider" || t.GetComponent<Camera>() != null || t.GetComponent<Light>() != null)) Object.DestroyImmediate(t.gameObject);
            var volume = waterRoot.GetComponentInChildren<WaterVolume>(true); volume.TargetCamera = flow.player.Camera;
            volume.transform.position = Vector3.zero; volume.IsPrimary = true;
            Set(volume, "sun", sun); Set(volume, "orbit", null); Set(volume, "volumeExtent", new Vector3(75, 40, 75));
            Set(volume, "depthAttenuation.depthDarken", true); Set(volume, "depthAttenuation.depthDarkenStrength", .65f);
            Set(volume, "depthAttenuation.linkDepthToFog", true);
            Set(volume, "bedDepthSettings.bedTerrain", null);
            foreach (var component in waterRoot.GetComponentsInChildren<MonoBehaviour>(true))
                if (component != null && component.GetType().Name.Contains("Orbit")) Object.DestroyImmediate(component);
            notes.Add("Water copied from configured 1-VR, camera/sun rebound, authored source unchanged.");
            if (close) EditorSceneManager.CloseScene(sourceScene, true);
            SceneManager.SetActiveScene(scene);
        }
        static void CreateServices(GameObject services, DemoFlow flow)
        {
            var pulse = services.AddComponent<VolumetricFogPulseEmitter>(); Set(pulse, "enableKeyboardTest", false);
            Set(pulse, "propagationSpeed", config.sonarSpeed); Set(pulse, "ringWidth", config.sonarWidth); Set(pulse, "maximumRadius", config.sonarRadius);
            var reveal = services.AddComponent<SonarRevealManager>(); Set(reveal, "targetLayers", (1 << worldLayer) | (1 << propLayer));
            Set(reveal, "ignoredLayers", (1 << groundLayer) | (1 << playerLayer)); Set(reveal, "outlineHoldDelay", 2f); Set(reveal, "outlineFadeDuration", 1f);
            var noise = services.AddComponent<NoiseSystem>(); Set(noise, "forwardAllPulses", false); Set(noise, "autoDiscoverImpacts", false);
            var acoustics = services.AddComponent<DemoAcoustics>(); acoustics.config = config;
        }
        static void CreateWeather(Transform parent, DemoFlow flow, Light sun)
        {
            var weather = Make("Weather Controller", parent).AddComponent<DemoWeather>(); weather.config = config; weather.head = flow.player.Camera.transform;
            flow.weather = weather;
            bool hasSupport = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/UniStorm Weather System" })
                .Select(AssetDatabase.GUIDToAssetPath).Any(p => p.IndexOf("RendererFeature", StringComparison.OrdinalIgnoreCase) >= 0 && File.ReadAllText(p).Contains("RecordRenderGraph"));
            config.weatherReady = hasSupport;
            config.weatherStatus = hasSupport ? "Official RenderGraph source found; headset storm verification still required." : "BLOCKED: Import UniStorm 5.4+ and its official Render Graph URP support package. No replacement weather system is used.";
            var weatherRoot = Load("Assets/UniStorm Weather System/Resources/Systems/Resources/UniStorm VR System.prefab", parent);
            weatherRoot.name = "UniStorm Official VR - Pending Verification"; weather.weatherRoot = weatherRoot;
            weather.system = weatherRoot.GetComponent<UniStormSystem>();
            weather.thunderstorm = AssetDatabase.LoadAssetAtPath<WeatherType>("Assets/UniStorm Weather System/Weather Types/Precipitation/Thunderstorm.asset");
            var candidates = AssetDatabase.FindAssets("t:WeatherType", new[] { "Assets/UniStorm Weather System" }).Select(AssetDatabase.GUIDToAssetPath);
            string overcast = candidates.FirstOrDefault(p => p.Contains("Overcast")) ?? candidates.FirstOrDefault(p => p.Contains("Cloudy"));
            weather.overcast = AssetDatabase.LoadAssetAtPath<WeatherType>(overcast ?? "");
            if (weather.system != null)
            {
                weather.system.PlayerCamera = flow.player.Camera; weather.system.PlayerTransform = flow.player.transform;
                weather.system.GetPlayerAtRuntime = UniStormSystem.EnableFeature.Disabled;
                weather.system.PlatformType = UniStormSystem.PlatformTypeEnum.VR;
                weather.system.TimeFlow = UniStormSystem.EnableFeature.Disabled; weather.system.WeatherGeneration = UniStormSystem.EnableFeature.Disabled;
                weather.system.CloudShadows = UniStormSystem.EnableFeature.Disabled;
                weather.system.SunShaftsEffect = UniStormSystem.EnableFeature.Disabled; weather.system.MoonShaftsEffect = UniStormSystem.EnableFeature.Disabled;
            }
            weatherRoot.SetActive(hasSupport); notes.Add(config.weatherStatus);
        }
        static void UpgradeMaterials(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    var original = slots[i]; if (original == null || original.shader == null || original.shader.name.StartsWith("Universal") || original.GetTag("RenderPipeline", false) == "UniversalPipeline") continue;
                    string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(original));
                    string path = Root + "/Generated/" + original.name.Replace('/', '_') + "_" + guid.Substring(0, Mathf.Min(8, guid.Length)) + "_URP.mat";
                    var copy = Asset<Material>(path, () => new Material(original));
                    Texture albedo = original.HasProperty("_MainTex") ? original.GetTexture("_MainTex") : null;
                    Color color = original.HasProperty("_Color") ? original.GetColor("_Color") : Color.white;
                    copy.shader = Shader.Find("Universal Render Pipeline/Lit"); copy.SetTexture("_BaseMap", albedo); copy.SetColor("_BaseColor", color);
                    if (albedo != null) { copy.SetTextureScale("_BaseMap", original.GetTextureScale("_MainTex")); copy.SetTextureOffset("_BaseMap", original.GetTextureOffset("_MainTex")); }
                    if (copy.HasProperty("_BumpMap") && copy.GetTexture("_BumpMap") != null) copy.EnableKeyword("_NORMALMAP");
                    EditorUtility.SetDirty(copy); slots[i] = copy;
                }
                r.sharedMaterials = slots;
            }
        }
    }
}
#endif
