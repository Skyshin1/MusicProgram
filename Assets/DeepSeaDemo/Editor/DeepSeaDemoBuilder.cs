#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AbstractOcclusion.WebGpuWater;
using DeepSeaAI;
using Unity.AI.Navigation;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Object = UnityEngine.Object;

namespace DeepSeaDemo.Editor
{
    [InitializeOnLoad]
    public static partial class DeepSeaDemoBuilder
    {
        public const string Root = "Assets/DeepSeaDemo";
        public const string ScenePath = Root + "/Scenes/DeepSeaInvestigation.unity";
        const string Request = "Logs/DeepSeaDemo/build.request";
        static DemoConfig config;
        static Scene scene;
        static int worldLayer, groundLayer, propLayer, uiLayer, playerLayer;
        static List<DemoProp> props;
        static List<string> notes;
        static Dictionary<string, Material> materials;
        static bool building;
        static double nextPoll;
        static DeepSeaDemoBuilder() { EditorApplication.update += Poll; }
        static void Poll()
        {
            if (building || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 2;
            if (!File.Exists(Request)) return;
            string command = File.ReadAllText(Request).Trim();
            File.Move(Request, Request + ".consumed." + DateTime.UtcNow.Ticks);
            if (command == "build") Build();
            else if (command == "player") BuildPlayer();
            else if (command == "validate") ValidateSaved();
            else if (command == "smoke") DemoPlaySmoke.Begin();
            else if (command == "clone1vr") Prepare1VRCopy();
            else if (command == "assemble1vr") Demo1VRFlowAssembly.Run();
            else if (command == "audit1vr") Demo1VRFlowAssembly.AuditSaved();
            else if (command == "refresh") AssetDatabase.Refresh();
            else if (command == "recover1vr") Demo1VRFlowAssembly.RecoverAuthorizedAttempt();
            else if (command == "smoke1vr") DemoPlaySmoke.Begin1VR();
            else if (command == "inspect1vr") Demo1VRFlowAssembly.InspectActive();
            else if (command == "finish1vr") Demo1VRFlowAssembly.FinishLinks();
            else if (command == "testconfigured1vr") Demo1VRFlowAssembly.SaveAndTest();
            else if (command == "player1vr") Demo1VRBuild.Build();
            else if (command == "report1vr") Demo1VRBuild.ExportLatest();
            else if (command == "realvr1vr") DemoInputModeSetup.RealVR();
            else if (command == "xrinspect") DemoXRLiveDiagnostics.Capture();
            else if (command == "handsui") DemoHandsUIRegression.Run();
            else if (command == "sonargate") DemoHandsUIRegression.RunSonar();
            else if (command == "sonarrender") DemoHandsUIRegression.RunSonarRendering();
            else if (command == "doorinspect") DemoDoorwayRepair.Inspect();
            else if (command == "doorrepair") DemoDoorwayRepair.Repair();
            else if (command == "modelinspect") DemoModelRegression.Inspect();
            else if (command == "wristrepair") DemoGloveWristRepair.Generate();
            else if (command == "modeltest") DemoModelRegression.Verify();
            else if (command == "gameplayinspect") DemoGameplayRegression.Inspect();
            else if (command == "environmentplay") DemoGameplayRegression.BeginEnvironmentPlay();
            else if (command == "qteplay") DemoGameplayRegression.BeginQtePlay();
            else if (command == "audiosetup") DemoAudioSetup.Generate();
            else if (command == "surfaceplay") DemoGameplayRegression.BeginSurfacePlay();
            else if (command == "waterlineplay") DemoGameplayRegression.BeginWaterlinePlay();
            else if (command == "interactionplay") DemoGameplayRegression.BeginInteractionPlay();
            else if (command == "boardingplay") DemoGameplayRegression.BeginBoardingPlay();
            else if (command == "repairplay") DemoGameplayRegression.BeginRepairPlay();
            else if (command == "gameplaytest") DemoGameplayRegression.Verify();
            else if (command == "gameplayplay") DemoGameplayRegression.BeginPlay();
        }
        [MenuItem("Tools/Deep Sea Demo/01 Build Isolated Demo")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            building = true; notes = new(); materials = new(); props = new();
            Scene previous = SceneManager.GetActiveScene();
            var oldSelection = Selection.activeObject;
            try
            {
                foreach (string folder in new[] { "Scenes", "Prefabs", "Settings", "Generated", "Reports" }) Directory.CreateDirectory(Root + "/" + folder);
                AssetDatabase.Refresh();
                config = Asset<DemoConfig>(Root + "/Settings/DemoConfig.asset", () => ScriptableObject.CreateInstance<DemoConfig>());
                config.chineseFont = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/NotoSansCJKsc-Regular.otf");
                worldLayer = Layer("DeepSeaWorld"); groundLayer = Layer("DeepSeaGround"); propLayer = Layer("DeepSeaProp"); uiLayer = Layer("DeepSeaUI"); playerLayer = Layer("DeepSeaPlayer");
                config.worldMask = (1 << worldLayer) | (1 << groundLayer);
                config.interactMask = 1 << propLayer; config.uiMask = 1 << uiLayer;
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                var root = Make("Deep Sea Investigation");
                var services = Make("Services", root.transform);
                var flow = services.AddComponent<DemoFlow>(); flow.config = config;
                var rp = CreatePipeline(); services.AddComponent<DemoPipeline>().pipeline = rp;
                var sun = Make("Environment Sun", root.transform).AddComponent<Light>();
                sun.type = LightType.Directional; sun.intensity = .8f; sun.color = new Color(.72f, .81f, .86f);
                sun.transform.rotation = Quaternion.Euler(36, -28, 0); sun.shadows = LightShadows.Soft;
                RenderSettings.sun = sun; RenderSettings.fog = false; RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.19f, .24f, .28f);
                RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>("Assets/OilRigAssembly/Generated/OilRig_OvercastDuskSky.mat");
                flow.player = CreatePlayer(root.transform, config);
                var uiObject = Make("VR UI", services.transform); flow.ui = uiObject.AddComponent<DemoUI>(); flow.ui.flow = flow; flow.ui.config = config;
                CreateWater(previous, root.transform, flow, sun);
                CreateServices(services, flow);
                CreateLevel(root.transform, flow);
                CreateWeather(root.transform, flow, sun);
                flow.props = props.ToArray();
                var binds = services.AddComponent<DemoSceneBindings>(); binds.flow = flow;
                binds.acoustics = services.GetComponent<DemoAcoustics>();
                CreateFish(root.transform, flow, binds);
                binds.dock = root.GetComponentInChildren<BlackBoxPlaybackDock>(true);
                EditorUtility.SetDirty(config);
                SaveReusable(root, flow);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                ValidateScene(scene, notes);
                File.WriteAllLines(Root + "/Reports/BuildReport.txt", notes);
                Debug.Log("[DeepSeaDemo] Scene built: " + ScenePath + ". Weather: " + config.weatherStatus);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex); notes.Add("BUILD FAILED: " + ex);
                Directory.CreateDirectory("Logs/DeepSeaDemo"); File.WriteAllLines("Logs/DeepSeaDemo/build-failure.txt", notes);
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                Selection.activeObject = oldSelection; building = false;
            }
        }
        static GameObject Make(string name, Transform parent = null)
        {
            var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene);
            if (parent != null) go.transform.SetParent(parent, false); return go;
        }
        static GameObject Load(string path, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new FileNotFoundException("Required asset", path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            go.transform.SetParent(parent, false); return go;
        }
        static T Asset<T>(string path, Func<T> create) where T : Object
        {
            T value = AssetDatabase.LoadAssetAtPath<T>(path);
            if (value != null) return value;
            value = create(); AssetDatabase.CreateAsset(value, path); return value;
        }
        static int Layer(string name)
        {
            int found = LayerMask.NameToLayer(name); if (found >= 0) return found;
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tags.FindProperty("layers");
            for (int i = 8; i < 32; i++) if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
            { layers.GetArrayElementAtIndex(i).stringValue = name; tags.ApplyModifiedPropertiesWithoutUndo(); return i; }
            throw new InvalidOperationException("No free layer slot for " + name);
        }
        static void Set(Object target, string name, object value)
        {
            var so = new SerializedObject(target); var p = so.FindProperty(name);
            if (p == null) { notes.Add("FIELD NOT FOUND: " + target.GetType().Name + "." + name); return; }
            if (value is Object o) p.objectReferenceValue = o;
            else if (value == null) p.objectReferenceValue = null;
            else if (value is bool b) p.boolValue = b;
            else if (value is int i) p.intValue = i;
            else if (value is float f) p.floatValue = f;
            else if (value is string s) p.stringValue = s;
            else if (value is Vector3 v) p.vector3Value = v;
            else if (value is Color c) p.colorValue = c;
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target);
        }
        static void SetLayer(GameObject go, int layer)
        { foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer; }
        static Material Mat(string name, Color color, bool emission = false)
        {
            if (materials.TryGetValue(name, out var cached)) return cached;
            var m = Asset<Material>(Root + "/Generated/" + name + ".mat", () => new Material(Shader.Find("Universal Render Pipeline/Lit")));
            m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", .28f);
            if (emission) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * 2); }
            materials[name] = m; return m;
        }
        static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material material, int layer = -1)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            SceneManager.MoveGameObjectToScene(go, scene); go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material; go.layer = layer < 0 ? worldLayer : layer; return go;
        }
        static Transform Point(string name, Transform parent, Vector3 position)
        { var go = Make(name, parent); go.transform.position = position; return go.transform; }
        static Bounds BoundsOf(GameObject go)
        {
            Renderer[] rs = go.GetComponentsInChildren<Renderer>(true); if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
        }
        static void Fit(GameObject visual, Vector3 center, float longestSize)
        {
            Bounds b = BoundsOf(visual); float max = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            visual.transform.localScale *= longestSize / Mathf.Max(.001f, max);
            b = BoundsOf(visual); visual.transform.position += center - b.center;
        }
    }
}
#endif
