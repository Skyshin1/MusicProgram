#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AbstractOcclusion.WebGpuWater;
using DeepSeaAI;
using Unity.AI.Navigation;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DeepSeaDemo.Editor
{
    // Explicit, one-time module migration, not an automatic scene rebuilder.
    // Re-running only audits the existing assembly: hand-edited layout is never regenerated.
    public static class Demo1VRFlowAssembly
    {
        const string Folder = "Assets/DeepSeaDemo/Settings/1VR";
        const string Module = "1VR Complete Investigation Modules";
        static readonly List<string> report = new();
        static string Hash(string p) { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(p))); }
        static T[] All<T>(Scene s) where T : Component => s.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
        static T Ensure<T>(GameObject g) where T : Component => g.GetComponent<T>() ?? g.AddComponent<T>();

        [MenuItem("Tools/Deep Sea Demo/14 Assemble Complete Flow in 1-VR Copy")]
        public static void Run()
        {
            Guard(); report.Clear();
            string sourceHash = Hash(DeepSeaDemoBuilder.Source1VR);
            if (!File.Exists(DeepSeaDemoBuilder.Copy1VR)) DeepSeaDemoBuilder.Prepare1VRCopy();
            var previous = SceneManager.GetActiveScene();
            var target = SceneManager.GetSceneByPath(DeepSeaDemoBuilder.Copy1VR);
            bool opened = !target.isLoaded;
            if (opened) target = EditorSceneManager.OpenScene(DeepSeaDemoBuilder.Copy1VR, OpenSceneMode.Additive);
            var donor = default(Scene); bool closeDonor = false;
            try
            {
                SceneManager.SetActiveScene(target);
                if (target.GetRootGameObjects().Any(g => g.name == Module))
                { report.Add("Existing assembly retained; no objects regenerated or repositioned."); Audit(target); return; }
                var inherited = target.GetRootGameObjects();
                var origin = All<XROrigin>(target).Single();
                BindOrigin(origin);
                var water = All<WaterVolume>(target).Single(w => w.gameObject.activeInHierarchy);
                float waterY = water.transform.position.y;
                report.Add("Preserved saved 1-VR player/tracking/movement components and water parameters. Water surface Y=" + waterY);
                report.Add("Source root objects before migration: " + string.Join(", ", inherited.Select(g => g.name)));

                // Instantiate reusable modules INACTIVE: do not load a second live water scene.
                var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DeepSeaDemo/Prefabs/CompleteDemoAssembly.prefab");
                if (template == null) throw new InvalidOperationException("Complete gameplay module prefab missing.");
                var staging = new GameObject("Temporary inactive assembly staging"); staging.SetActive(false);
                SceneManager.MoveGameObjectToScene(staging, target);
                var root = Object.Instantiate(template, staging.transform, false); root.name = Module;
                root.SetActive(false); root.transform.SetParent(null, true); Object.DestroyImmediate(staging);
                var flow = root.GetComponentInChildren<DemoFlow>(true);
                Ensure<DemoPauseCoordinator>(flow.gameObject);
                var oldPlayer = flow.player;
                var oldPlayerRoot = oldPlayer.transform;
                while (oldPlayerRoot.parent != root.transform) oldPlayerRoot = oldPlayerRoot.parent;
                var oldWaterRoot = root.GetComponentInChildren<WaterVolume>(true).transform;
                while (oldWaterRoot.parent != root.transform) oldWaterRoot = oldWaterRoot.parent;
                Object.DestroyImmediate(oldWaterRoot.gameObject);
                var duplicateSun = root.transform.Find("Environment Sun");
                if (duplicateSun != null) Object.DestroyImmediate(duplicateSun.gameObject);

                var cache = new Dictionary<Object, Object>();
                flow.config = Isolate(flow.config, cache) as DemoConfig;
                flow.config.saveFileName = "DeepSeaInvestigation_1VR.checkpoint.json";
                flow.config.text = GetText();
                // Isolate all mutable configuration assets used by the imported gameplay modules.
                foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (component == null) continue;
                    var so = new SerializedObject(component); var it = so.GetIterator();
                    while (it.Next(true))
                        if (IsUserReference(it) && it.objectReferenceValue is ScriptableObject asset &&
                            AssetDatabase.GetAssetPath(asset).StartsWith("Assets/DeepSeaDemo/Settings/"))
                            it.objectReferenceValue = Isolate(asset, cache);
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                // Copy only missing functional components; never overwrite inherited locomotion settings.
                var remap = new Dictionary<Object, Object> { [oldPlayer] = origin, [oldPlayer.transform] = origin.transform,
                    [oldPlayer.gameObject] = origin.gameObject, [oldPlayer.Camera] = origin.Camera,
                    [oldPlayer.Camera.transform] = origin.Camera.transform, [oldPlayer.Camera.gameObject] = origin.Camera.gameObject };
                var oldRouter = oldPlayer.GetComponent<DemoInputRouter>();
                var oldLeft = oldRouter.leftHand; var oldRight = oldRouter.rightHand;
                Transform left = Hand(origin, false), right = Hand(origin, true);
                remap[oldRouter.leftHand] = left; remap[oldRouter.rightHand] = right;
                foreach (var component in oldPlayer.GetComponents<MonoBehaviour>())
                {
                    if (component == null || component is XROrigin) continue;
                    string type = component.GetType().Name;
                    if (type == "InputActionManager" || type == "UniversalAdditionalCameraData") continue;
                    var existing = origin.GetComponent(component.GetType());
                    if (existing == null) { existing = origin.gameObject.AddComponent(component.GetType()); EditorUtility.CopySerializedManagedFieldsOnly(component, existing); }
                    remap[component] = existing;
                }
                var cc = Ensure<CharacterController>(origin.gameObject);
                if (cc.radius > .4f || cc.radius < .1f) cc.radius = .25f;
                cc.skinWidth = .025f; cc.stepOffset = Mathf.Min(cc.stepOffset, .22f);
                foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true).Concat(origin.GetComponents<MonoBehaviour>()))
                {
                    if (component == null) continue;
                    var so = new SerializedObject(component); var it = so.GetIterator();
                    while (it.Next(true))
                        if (IsUserReference(it) && it.objectReferenceValue != null && remap.TryGetValue(it.objectReferenceValue, out var replacement))
                            it.objectReferenceValue = replacement;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                flow.player = origin;
                var router = Ensure<DemoInputRouter>(origin.gameObject); router.config = flow.config; router.leftHand = left; router.rightHand = right;
                Ensure<DemoPlayerSafety>(origin.gameObject).config = flow.config;
                ConfigureGlove(oldLeft, left, false);
                ConfigureGlove(oldRight, right, true);
                Object.DestroyImmediate(oldPlayerRoot.gameObject);
                // Original test content stays recoverable, disabled only in this copy.
                var retired = new HashSet<string> { "Plane", "Cube", "Cube (1)", "Cube (2)", "Cube (3)", "Cube (4)", "Cube (5)",
                    "Deep Sea Fish Demo School", "Deep Sea Stalker AI", "Water Gameplay Props", "SK_LeatherGlove",
                    "Sonar Reveal Manager", "Sonar Wave Visual System", "UniStorm VR - Locked Thunderstorm" };
                foreach (var g in inherited.Where(g => retired.Contains(g.name)))
                { g.SetActive(false); report.Add("DISABLED (recoverable in copy): " + g.name); }
                foreach (var pulse in origin.GetComponentsInChildren<VolumetricFogPulseEmitter>(true)) pulse.enabled = false;
                foreach (var legacy in origin.GetComponentsInChildren<SurfaceDocumentReader>(true)) legacy.enabled = false;
                foreach (var legacy in origin.GetComponentsInChildren<XRHandSonarInput>(true)) legacy.enabled = false;
                foreach (var c in water.transform.root.GetComponentsInChildren<Collider>(true))
                    if (c.name == "Floor Collider") { c.enabled = false; report.Add("Disabled water test floor collider; authored gameplay seabed supplies collision."); }
                Set(origin.GetComponent<PlayerOxygen>(), "drainAutomatically", false);
                Set(origin.GetComponent<PlayerOxygen>(), "respawnWhenDepleted", false);
                // Reuse original authored model instances in the imported functional region layout.
                ReplaceSub(inherited, root, flow);
                root.transform.position += Vector3.up * waterY;
                ConfigureExistingPlatform(inherited.Single(g => g.name == "__OilRigGenerated"), root, flow, waterY);
                water.TargetCamera = origin.Camera; Set(water, "orbit", null); Set(water, "configureCamera", false);
                Set(water, "volumeExtent", new Vector3(75, 45, 75));
                report.Add("Copy-only water bounds extended to (75,45,75) to contain all routes. Surface and shader settings inherited.");
                // Keep the existing terrain as distant context; no original terrain height data is overwritten.
                var pipeline = root.GetComponentInChildren<DemoPipeline>(true);
                if (pipeline != null) IsolatePipeline(pipeline, cache);
                ConfigureUICamera(origin, flow.config);
                root.SetActive(true);
                SceneManager.SetActiveScene(target);
                // Close the donor before validation/baking so its colliders and services cannot participate.
                if (closeDonor) { EditorSceneManager.CloseScene(donor, true); closeDonor = false; }
                Demo1VRInputSetup.Configure(target);
                English(root, flow.config.text);
                Demo1VRPresentationSetup.Apply();
                var tracker = Ensure<WaterSurfaceStateTracker>(origin.gameObject);
                Set(tracker, "probe", origin.Camera.transform);
                // Place the inherited tracked rig at the first real checkpoint without resetting local tracking bindings.
                origin.transform.position += flow.checkpoints[0].position - origin.Camera.transform.position;
                SetLayers(origin.transform.root.gameObject, LayerMask.NameToLayer("DeepSeaPlayer"));
                foreach (var nav in root.GetComponentsInChildren<NavMeshSurface>(true))
                {
                    nav.RemoveData(); nav.navMeshData = null; nav.BuildNavMesh();
                    if (nav.navMeshData == null) throw new InvalidOperationException("Navigation bake produced no data.");
                    string path = Folder + "/GameplayNav.asset";
                    var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
                    if (existing == null) AssetDatabase.CreateAsset(nav.navMeshData, path);
                    else { EditorUtility.CopySerialized(nav.navMeshData, existing); nav.RemoveData(); nav.navMeshData = existing; nav.AddData(); }
                }
                var binds = flow.GetComponent<DemoSceneBindings>();
                foreach (var point in binds.route)
                    if (NavMesh.SamplePosition(point.position, out var hit, 3, NavMesh.AllAreas)) point.position = hit.position;
                flow.enemy.transform.position = binds.route[0].position;
                Audit(target);
                EditorUtility.SetDirty(flow.config); EditorUtility.SetDirty(flow.config.text);
                AssetDatabase.SaveAssets();
                if (Hash(DeepSeaDemoBuilder.Source1VR) != sourceHash) throw new InvalidOperationException("Source scene changed unexpectedly.");
                EditorSceneManager.SaveScene(target);
                report.Add("PASS saved source scene SHA256 unchanged. Assembly saved to " + target.path);
                report.Add("NOT YET ACCEPTED: controller-driven end-to-end playthrough, hatch clearance, headset stereo/performance, official UniStorm RG support.");
            }
            catch (Exception ex) { report.Add("ASSEMBLY FAILED (scene not saved): " + ex); Debug.LogException(ex); throw; }
            finally
            {
                Directory.CreateDirectory("Assets/DeepSeaDemo/Reports");
                File.WriteAllLines("Assets/DeepSeaDemo/Reports/1VR-FlowAssembly.txt", report);
                if (closeDonor && donor.isLoaded) EditorSceneManager.CloseScene(donor, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (opened && target.isLoaded) EditorSceneManager.CloseScene(target, true);
            }
        }

        static void Guard()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save or back up open scenes first. No unsaved work is discarded.");
        }
        public static void InspectActive()
        {
            var s = SceneManager.GetActiveScene();
            if (s.path != DeepSeaDemoBuilder.Copy1VR) throw new InvalidOperationException("Only inspect the user-confirmed active copy.");
            var lines = new List<string> { "READ ONLY " + s.path + " dirty=" + s.isDirty };
            foreach (var t in All<Transform>(s).Where(t => t.name.StartsWith("Room_") || t.name.Contains("Deck") || t.name == "__OilRigGenerated"))
                lines.Add(t.name + " world=" + t.position + " scale=" + t.lossyScale + " colliders=" + t.GetComponentsInChildren<Collider>(true).Length);
            foreach (var c in All<Camera>(s)) lines.Add("CAMERA " + c.name + " active=" + c.gameObject.activeInHierarchy + " enabled=" + c.enabled);
            File.WriteAllLines("Assets/DeepSeaDemo/Reports/1VR-ExistingPlatform.txt", lines);
        }
        static Scene ConfiguredActiveCopy()
        {
            var s = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || s.path != DeepSeaDemoBuilder.Copy1VR || SceneManager.sceneCount != 1)
                throw new InvalidOperationException("Keep only the configured 1-VR copy open, outside Play Mode.");
            if (!s.GetRootGameObjects().Any(g => g.name == Module)) throw new InvalidOperationException("Assemble first.");
            return s;
        }
        [MenuItem("Tools/Deep Sea Demo/16 Finish Existing Platform Interaction Links")]
        public static void FinishLinks()
        {
            var s = ConfiguredActiveCopy();
            var flow = All<DemoFlow>(s).Single(f => f.gameObject.activeInHierarchy);
            ConfigureUICamera(flow.player, flow.config);
            var rig = s.GetRootGameObjects().Single(g => g.name == "__OilRigGenerated");
            var interactions = rig.transform.Find("Investigation Interactions - Existing Platform");
            int world = LayerMask.NameToLayer("DeepSeaWorld");
            foreach (var c in rig.GetComponentsInChildren<Collider>(true))
                if (!c.transform.IsChildOf(interactions)) c.gameObject.layer = world;
            // Static collision masks now include the actual platform, not only generated route geometry.
            var lines = new List<string>(); Physics.SyncTransforms();
            foreach (var checkpoint in flow.checkpoints)
            {
                Vector3 wanted = checkpoint.position;
                bool Clear(Vector3 p) => !Physics.CheckCapsule(p - Vector3.up * 1.3f, p - Vector3.up * .2f, .24f, flow.config.worldMask, QueryTriggerInteraction.Ignore);
                if (!Clear(wanted))
                {
                    bool found = false;
                    foreach (float radius in new[] { .5f, 1f, 1.5f, 2f })
                    {
                        for (int i = 0; i < 8 && !found; i++)
                        {
                            var p = wanted + new Vector3(Mathf.Cos(i * Mathf.PI / 4), 0, Mathf.Sin(i * Mathf.PI / 4)) * radius;
                            if (!Clear(p)) continue;
                            if (p.y > 0 && !Physics.Raycast(p, Vector3.down, 2.1f, flow.config.worldMask, QueryTriggerInteraction.Ignore)) continue;
                            checkpoint.position = p; found = true;
                        }
                        if (found) break;
                    }
                }
                lines.Add((Clear(checkpoint.position) ? "PASS " : "FAIL ") + checkpoint.name + " head=" + checkpoint.position);
            }
            void Sign(string id, Transform anchor, string value)
            {
                var existing = interactions.Find(id);
                var go = existing != null ? existing.gameObject : new GameObject(id, typeof(RectTransform), typeof(Canvas));
                go.transform.SetParent(interactions, true); go.transform.position = anchor.position + new Vector3(0, 2.3f, -.75f);
                go.transform.rotation = Quaternion.Euler(0, 180, 0); go.transform.localScale = Vector3.one * .002f;
                go.layer = world; var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
                var rect = go.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(1400, 240);
                var text = Ensure<Text>(go); text.font = flow.config.chineseFont; text.text = value; text.fontSize = 34;
                text.alignment = TextAnchor.MiddleCenter; text.color = Color.white; text.raycastTarget = false;
            }
            Transform Room(string name) => rig.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
            Sign("Equipment Instructions", Room("Room_Equipment_Preparation"), "EQUIPMENT / START HERE\nGrip: pick up flashlight. Trigger while held: light on/off.\nRead the first log and check the alarm across the main deck. Equip suit, then BEGIN DIVE.");
            Sign("Log Instructions", Room("Room_Log_Monitor"), "INVESTIGATION LOGS\nPoint an EMPTY hand and press Trigger to read.\nRead ROUTINE VOYAGE LOG, then CHECK ALARM RECORD.");
            Sign("Analysis Instructions", Room("Room_BlackBox_Analysis"), "EVIDENCE ANALYSIS\nPhysically insert BLACK BOX into the left dock. Wait for playback.\nWATER SAMPLE is optional. Then choose PRESERVE EVIDENCE or UPLOAD TO COMPANY.");
            Ensure<DemoPauseCoordinator>(flow.gameObject);
            var input = flow.player.GetComponent<DemoInputRouter>();
            foreach (var hand in new[] { input.leftHand, input.rightHand })
                foreach (var b in hand.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (b == null) continue;
                    var so = new SerializedObject(b);
                    var range = so.FindProperty("m_CastDistance"); if (range != null) range.floatValue = 3;
                    var mask = so.FindProperty("m_RaycastMask"); if (mask != null) mask.intValue = flow.config.worldMask | flow.config.interactMask;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            EditorSceneManager.MarkSceneDirty(s); EditorSceneManager.SaveScene(s);
            File.WriteAllLines("Assets/DeepSeaDemo/Reports/1VR-CheckpointSafety.txt", lines);
        }
        public static void SaveAndTest()
        {
            var s = ConfiguredActiveCopy();
            // Save the current configured copy in place; never reload/discard any unsaved edits.
            EditorSceneManager.SaveScene(s); DemoPlaySmoke.Begin1VR();
        }
        static bool IsUserReference(SerializedProperty p) => p.propertyType == SerializedPropertyType.ObjectReference &&
            p.propertyPath != "m_GameObject" && p.propertyPath != "m_Script" && p.propertyPath != "m_CorrespondingSourceObject" &&
            p.propertyPath != "m_PrefabInstance" && p.propertyPath != "m_PrefabAsset";
        // Invoked only after the user explicitly confirms no manual edits since failed assembly.
        public static void RecoverAuthorizedAttempt()
        {
            throw new InvalidOperationException("Automatic reload disabled after native crash. Use the already-open clean saved copy and explicit assembly.");
        }
        static void BindOrigin(XROrigin origin)
        {
            if (origin.Camera == null) origin.Camera = origin.GetComponentsInChildren<Camera>(true).First(c => c.name == "Main Camera");
            if (origin.CameraFloorOffsetObject == null) origin.CameraFloorOffsetObject = origin.GetComponentsInChildren<Transform>(true).First(t => t.name == "Camera Offset").gameObject;
            origin.Camera.enabled = true; origin.Camera.tag = "MainCamera";
        }
        static Transform Hand(XROrigin o, bool right) => o.GetComponentsInChildren<Transform>(true).First(t => t.name.Equals(right ? "Right" : "left", StringComparison.OrdinalIgnoreCase));
        static void ConfigureGlove(Transform source, Transform target, bool right)
        {
            if (target.GetComponentInChildren<DemoHandAnimator>(true) != null) return;
            var glove = source.GetComponentInChildren<DemoHandAnimator>(true);
            if (glove == null) throw new InvalidOperationException("Expected leather glove pose rig missing.");
            var copy = Object.Instantiate(glove.gameObject, target, false); copy.name = "Visual Leather Glove " + (right ? "Right" : "Left");
            foreach (var cube in target.Cast<Transform>().Where(t => t.name == "Cube").ToArray()) cube.gameObject.SetActive(false);
        }
        static Object Isolate(Object original, Dictionary<Object, Object> cache)
        {
            if (original == null) return null;
            if (cache.TryGetValue(original, out var found)) return found;
            string src = AssetDatabase.GetAssetPath(original);
            if (src.StartsWith(Folder + "/")) return original;
            string dst = Folder + "/" + AssetDatabase.AssetPathToGUID(src) + Path.GetExtension(src);
            if (!File.Exists(dst) && !AssetDatabase.CopyAsset(src, dst)) throw new IOException("Cannot isolate asset " + src);
            return cache[original] = AssetDatabase.LoadAssetAtPath(dst, original.GetType());
        }
        static void IsolatePipeline(DemoPipeline p, Dictionary<Object, Object> cache)
        {
            p.pipeline = Isolate(p.pipeline, cache) as UniversalRenderPipelineAsset;
            var so = new SerializedObject(p.pipeline); var list = so.FindProperty("m_RendererDataList");
            for (int i = 0; i < list.arraySize; i++)
            {
                var item = list.GetArrayElementAtIndex(i); item.objectReferenceValue = Isolate(item.objectReferenceValue, cache);
            }
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(p.pipeline);
        }
        static void ConfigureUICamera(XROrigin origin, DemoConfig cfg)
        {
            var pipeline = All<DemoPipeline>(origin.gameObject.scene).Single(p => p.gameObject.activeInHierarchy || p.transform.root.name == Module);
            if (!AssetDatabase.GetAssetPath(pipeline.pipeline).StartsWith(Folder + "/"))
                throw new InvalidOperationException("UI renderer changes must target the isolated Demo pipeline.");
            var pipelineSO = new SerializedObject(pipeline.pipeline);
            var renderers = pipelineSO.FindProperty("m_RendererDataList");
            const string uiPath = Folder + "/UIRenderer.asset";
            var uiRenderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(uiPath);
            if (uiRenderer == null)
            {
                var source = renderers.GetArrayElementAtIndex(0).objectReferenceValue as UniversalRendererData;
                if (source == null) throw new InvalidOperationException("Demo requires Universal Renderer data.");
                uiRenderer = Object.Instantiate(source); uiRenderer.name = "1VR UI Renderer";
                uiRenderer.rendererFeatures.Clear();
                AssetDatabase.CreateAsset(uiRenderer, uiPath);
            }
            var uiSO = new SerializedObject(uiRenderer);
            uiSO.FindProperty("m_RenderingMode").intValue = 0;
            uiSO.FindProperty("m_OpaqueLayerMask").intValue = cfg.uiMask;
            uiSO.FindProperty("m_TransparentLayerMask").intValue = cfg.uiMask;
            uiSO.ApplyModifiedPropertiesWithoutUndo(); uiRenderer.SetDirty();
            renderers.arraySize = Mathf.Max(2, renderers.arraySize);
            renderers.GetArrayElementAtIndex(1).objectReferenceValue = uiRenderer;
            pipelineSO.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(pipeline.pipeline);
            var worldRenderer = renderers.GetArrayElementAtIndex(0).objectReferenceValue as UniversalRendererData;
            var worldSO = new SerializedObject(worldRenderer);
            worldSO.FindProperty("m_OpaqueLayerMask").intValue &= ~cfg.uiMask.value;
            worldSO.FindProperty("m_TransparentLayerMask").intValue &= ~cfg.uiMask.value;
            worldSO.ApplyModifiedPropertiesWithoutUndo();
            var uiPass = worldRenderer.rendererFeatures.OfType<RenderObjects>().FirstOrDefault(f => f.name == "Demo UI After Water");
            if (uiPass == null)
            {
                uiPass = ScriptableObject.CreateInstance<RenderObjects>(); uiPass.name = "Demo UI After Water";
                AssetDatabase.AddObjectToAsset(uiPass, worldRenderer); worldRenderer.rendererFeatures.Add(uiPass);
            }
            // Draw after water/outline/droplets, but BEFORE the final XR blit/viewport switch.
            uiPass.settings.Event = UnityEngine.Rendering.Universal.RenderPassEvent.AfterRenderingPostProcessing + 10;
            uiPass.settings.filterSettings.RenderQueueType = RenderQueueType.Transparent;
            uiPass.settings.filterSettings.LayerMask = cfg.uiMask;
            uiPass.settings.overrideMode = RenderObjects.RenderObjectsSettings.OverrideMaterialMode.None;
            uiPass.settings.overrideDepthState = true; uiPass.settings.enableWrite = false;
            uiPass.settings.depthCompareFunction = UnityEngine.Rendering.CompareFunction.Always;
            uiPass.SetActive(true); uiPass.Create(); EditorUtility.SetDirty(uiPass); worldRenderer.SetDirty(); EditorUtility.SetDirty(worldRenderer);
            var data = origin.Camera.GetUniversalAdditionalCameraData(); data.SetRenderer(0);
            // Draw the UI layer once, after fog/outlines/droplets, through URP's standard
            // Render Objects pass. The isolated overlay-camera path drops UGUI in this project.
            origin.Camera.cullingMask |= cfg.uiMask.value;
            var child = origin.Camera.transform.Find("DeepSeaDemo UI Camera");
            var camera = child != null ? child.GetComponent<Camera>() : new GameObject("DeepSeaDemo UI Camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(origin.Camera.transform, false); camera.transform.localPosition = Vector3.zero; camera.transform.localRotation = Quaternion.identity;
            camera.CopyFrom(origin.Camera); camera.cullingMask = cfg.uiMask; camera.tag = "Untagged";
            var ui = camera.GetUniversalAdditionalCameraData(); ui.renderType = CameraRenderType.Overlay; ui.SetRenderer(1);
            camera.enabled = false; Set(ui, "m_ClearDepth", true);
            data.cameraStack.Remove(camera);
            // Main Camera belongs to the inherited Player prefab: without explicit override
            // recording, the stack and culling mask disappear when the scene enters Play.
            EditorUtility.SetDirty(data); EditorUtility.SetDirty(origin.Camera);
            PrefabUtility.RecordPrefabInstancePropertyModifications(data);
            PrefabUtility.RecordPrefabInstancePropertyModifications(origin.Camera);
            AssetDatabase.SaveAssets();
        }
        static Bounds Bounds(GameObject g)
        { var rs = g.GetComponentsInChildren<Renderer>(true); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b; }
        static void Fit(GameObject g, Bounds target)
        {
            var b = Bounds(g); g.transform.localScale *= Mathf.Max(target.size.x, target.size.y, target.size.z) / Mathf.Max(b.size.x, b.size.y, b.size.z);
            g.transform.position += target.center - Bounds(g).center;
        }
        static void ConfigureExistingPlatform(GameObject rig, GameObject module, DemoFlow flow, float waterY)
        {
            Vector3 position = rig.transform.position, scale = rig.transform.localScale; Quaternion rotation = rig.transform.rotation;
            Transform Anchor(string name) => rig.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
            var prep = Anchor("Room_Equipment_Preparation"); var logs = Anchor("Room_Log_Monitor"); var analysis = Anchor("Room_BlackBox_Analysis");
            var platform = module.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Platform Investigation");
            var interactions = new GameObject("Investigation Interactions - Existing Platform").transform;
            interactions.SetParent(rig.transform, false);
            void Move(string name, Vector3 destination)
            {
                var t = platform.GetComponentsInChildren<Transform>(true).Single(x => x.name == name);
                t.SetParent(interactions, true); t.position = destination;
            }
            for (int i = 0; i < 4; i++) Move("Log " + (i + 1), logs.position + new Vector3(-1.1f + i * .7f, 1.02f, -.6f));
            Move("Equipment Table", logs.position + new Vector3(0, .86f, -.6f));
            Move("Alarm Terminal", logs.position + new Vector3(1.8f, 1.35f, -.6f));
            Move("Diving Suit Confirmation", prep.position + new Vector3(1.25f, 1.1f, 0));
            Move("Flashlight", prep.position + new Vector3(0, 1.05f, -.6f));
            Move("Flashlight Recovery Point", prep.position + new Vector3(0, 1.05f, -.6f));
            var table = GameObject.CreatePrimitive(PrimitiveType.Cube); table.name = "Equipment Pedestal";
            table.transform.SetParent(interactions, false); table.transform.position = prep.position + new Vector3(0, .85f, -.6f);
            table.transform.localScale = new Vector3(1.4f, .2f, .8f); table.layer = LayerMask.NameToLayer("DeepSeaWorld");
            table.GetComponent<Renderer>().sharedMaterial = flow.props.Single(p => p.id == "flashlight").GetComponentInChildren<Renderer>().sharedMaterial;
            Move("Analysis Workbench", analysis.position + new Vector3(0, .85f, -.6f));
            Move("Black Box Analysis Socket", analysis.position + new Vector3(-.55f, 1.18f, -.6f));
            Move("Dock Base", analysis.position + new Vector3(-.55f, 1.01f, -.6f));
            Move("Water Sample Receiver", analysis.position + new Vector3(.65f, 1.15f, -.6f));
            var boardPosition = rig.transform.TransformPoint(new Vector3(0, 0, 15)); boardPosition.y = waterY + .35f;
            Move("Safe Boarding", boardPosition);
            flow.checkpoints[0].position = prep.position + new Vector3(0, 1.75f, 1.5f);
            flow.checkpoints[3].position = analysis.position + new Vector3(0, 1.75f, 1.5f);
            flow.checkpoints[0].rotation = flow.checkpoints[3].rotation = Quaternion.Euler(0, 180, 0);
            var dive = GameObject.CreatePrimitive(PrimitiveType.Cube); dive.name = "Dive Entry Panel"; dive.layer = LayerMask.NameToLayer("DeepSeaProp");
            dive.transform.SetParent(interactions, false); dive.transform.position = prep.position + new Vector3(-1.4f, 1.2f, 0); dive.transform.localScale = new Vector3(.5f, .6f, .15f);
            var entry = dive.AddComponent<DemoWorldAction>(); entry.kind = DemoActionKind.Dive; entry.title = "Begin Dive";
            var waterEntry = new GameObject("Safe Water Entry").transform; waterEntry.SetParent(interactions, false);
            waterEntry.position = boardPosition + Vector3.forward * 1.8f; waterEntry.position = new Vector3(waterEntry.position.x, waterY + .16f, waterEntry.position.z); entry.destination = waterEntry;
            // Only the imported replacement platform is removed. The user's platform is unchanged.
            Object.DestroyImmediate(platform.gameObject);
            if (rig.transform.position != position || rig.transform.rotation != rotation || rig.transform.localScale != scale)
                throw new InvalidOperationException("Existing platform transform changed unexpectedly.");
            report.Add("PASS EXISTING PLATFORM retained at " + position + " scale=" + scale + "; no replacement deck, no resized rig, original colliders retained.");
            report.Add("ROOM anchors: equipment=" + prep.position + ", logs=" + logs.position + ", analysis=" + analysis.position);
        }
        static void ReplaceSub(GameObject[] inherited, GameObject module, DemoFlow flow)
        {
            var sub = inherited.Single(g => g.name == "SubMarine");
            if (PrefabUtility.IsPartOfPrefabInstance(sub)) PrefabUtility.UnpackPrefabInstance(sub, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var region = module.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Flooded Submarine");
            var proxy = region.Cast<Transform>().First(t => t.GetComponentsInChildren<MeshFilter>(true).Length > 10);
            var bounds = Bounds(proxy.gameObject);
            foreach (var r in flow.door.hinge.GetComponentsInChildren<Renderer>(true)) bounds.Encapsulate(r.bounds);
            sub.transform.SetParent(region, true); Fit(sub, bounds);
            var realDoor = sub.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Door");
            foreach (Transform oldDoor in flow.door.hinge.Cast<Transform>().ToArray()) Object.DestroyImmediate(oldDoor.gameObject);
            realDoor.SetParent(flow.door.hinge, true);
            foreach (var mf in sub.GetComponentsInChildren<MeshFilter>(true).Concat(realDoor.GetComponentsInChildren<MeshFilter>(true)))
                if (mf.sharedMesh != null && mf.GetComponent<Collider>() == null) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            SetLayers(sub, LayerMask.NameToLayer("DeepSeaWorld")); SetLayers(realDoor.gameObject, LayerMask.NameToLayer("DeepSeaWorld"));
            Object.DestroyImmediate(proxy.gameObject);
            report.Add("REUSED source SubMarine and actual Door group. Functional lock, hinge, blocker, shelf, recording and black box connected.");
        }
        static void SetLayers(GameObject go, int layer) { if (layer >= 0) foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer; }
        static void Set(Object o, string name, object value)
        {
            if (o == null) return;
            var so = new SerializedObject(o); var p = so.FindProperty(name); if (p == null) return;
            if (value is bool b) p.boolValue = b; else if (value is Vector3 v) p.vector3Value = v;
            else p.objectReferenceValue = value as Object; so.ApplyModifiedPropertiesWithoutUndo();
        }
        static DemoTextCatalog GetText()
        {
            var path = Folder + "/EnglishText.asset"; var text = AssetDatabase.LoadAssetAtPath<DemoTextCatalog>(path);
            if (text == null) { text = ScriptableObject.CreateInstance<DemoTextCatalog>(); AssetDatabase.CreateAsset(text, path); }
            return text;
        }
        static void English(GameObject root, DemoTextCatalog text)
        {
            foreach (var a in All<DemoWorldAction>(root.scene).Where(a => a.gameObject.activeInHierarchy))
            {
                a.title = a.name;
                if (a.fact != null && a.fact.StartsWith("log0")) { a.titleId = a.fact + ".title"; a.bodyId = a.fact + ".body"; a.title = text.Resolve(a.titleId); a.body = text.Resolve(a.bodyId); }
                else
                {
                    a.titleId = "world." + a.name.Replace(' ', '_') + ".title"; a.bodyId = "world." + a.name.Replace(' ', '_') + ".body";
                    a.body = a.kind == DemoActionKind.Alarm ? "Paper log: 23:17, sonar array lost and pump stopped. Digital export: 00:03, scheduled maintenance. Forty-six minutes are missing. No engineer signed the paper log. The discrepancy has been registered."
                        : a.fact == "sensor" ? "23:11: discharge valve opened. 23:14: turbidity reached 8.7 times baseline. 23:17: sonar array failed. The company export smooths this interval to normal values. Original sensor data registered. Return the nearby water sample for independent verification."
                        : a.fact == "testimony" ? "I could not see it. When the pump stopped, it stopped too. As we escaped, it turned toward the running array, not toward us. Do not erase the shutdown record. It explains why we survived."
                        : a.fact == "last_record" ? "FRAME 01: repeated sonar pulses. The creature turns toward the array. FRAME 02: the discharge pump ruptures. Divers escape while the animal circles the machinery. RECORDER: It follows the noise. This black box contains the original timestamps. Removing it will start emergency power. Follow the return beacons."
                        : "Use an empty-hand Trigger to interact. Grip picks up physical objects.";
                    text.entries = text.entries.Concat(new[] { new DemoTextCatalog.Entry { id = a.titleId, text = a.title }, new DemoTextCatalog.Entry { id = a.bodyId, text = a.body } }).GroupBy(e => e.id).Select(g => g.Last()).ToArray();
                }
            }
            foreach (var label in root.GetComponentsInChildren<Text>(true))
            {
                if (!label.text.Any(c => c >= '\u4e00' && c <= '\u9fff')) continue;
                string original = label.text;
                label.text = original.Contains("01 /") ? "01 / PLATFORM INVESTIGATION\nRead logs - Check alarms - Take flashlight - Equip suit"
                    : original.Contains("02 /") ? "02 / DIVE\nLeft stick: move. Right stick: turn / ascend / descend.\nAt the surface, push down to dive again."
                    : original.Contains("04 /") ? "04 / SUBMARINE HATCH\nHold tool near lock + Trigger to repair.\nUse OTHER empty-hand Grip for the skill check. Rest flashlight on shelf."
                    : original.Contains("05 /") ? "05 / EVIDENCE ANALYSIS\nLeft dock: BLACK BOX. Right dock: optional WATER SAMPLE.\nInsert the black box and wait for playback."
                    : original.Contains("维修笼") ? "MAINTENANCE CAGE / ELECTRONIC LOCK TOOL\nTake the orange tool to the submarine hatch."
                    : "RETURN TO PLATFORM\nPoint an empty hand at the orange boarding panel and press Trigger.";
                label.gameObject.name = "English Route Sign";
            }
        }
        static void Audit(Scene scene)
        {
            var flow = All<DemoFlow>(scene).Single(f => f.gameObject.activeInHierarchy);
            void Check(bool ok, string what) { report.Add((ok ? "PASS " : "FAIL ") + what); if (!ok) throw new InvalidOperationException(what); }
            Check(flow.player != null && flow.player.Camera != null && flow.player.GetComponent<DemoXRInput>() != null, "Inherited XR rig, camera and InputAction adapter bound");
            Check(flow.checkpoints.Length == 4 && flow.checkpoints.All(p => p != null), "All four checkpoint anchors");
            Check(flow.props.Select(p => p.id).Distinct().Count() == flow.props.Length, "Unique physical item IDs");
            foreach (var id in new[] { "flashlight", "locktool", "card", "blackbox", "sample" }) Check(flow.props.Any(p => p.id == id), "Physical prop " + id);
            var actions = All<DemoWorldAction>(scene).Where(a => a.gameObject.activeInHierarchy).ToArray();
            foreach (var id in new[] { "log01", "log02", "log03", "log04", "alarm", "sensor", "testimony", "last_record" }) Check(actions.Any(a => a.fact == id), "Readable evidence " + id);
            Check(actions.Any(a => a.kind == DemoActionKind.Equip) && actions.Any(a => a.kind == DemoActionKind.Board && a.destination != null), "Suit station and return boarding interaction");
            Check(flow.door != null && flow.door.repair != null && flow.door.hinge != null && flow.door.doorwayBlocker != null, "Door QTE, mesh hinge and blocking collider");
            Check(flow.props.Single(p => p.id == "locktool").GetComponent<RepairSkillCheckController>() != null, "Repair tool skill check");
            Check(flow.GetComponent<DemoSceneBindings>().dock != null && flow.enemy != null, "Physical black-box playback dock and enemy binding");
            Check(All<XROrigin>(scene).Count(o => o.gameObject.activeInHierarchy) == 1, "Exactly one active XR Origin");
            Check(All<WaterVolume>(scene).Count(w => w.gameObject.activeInHierarchy) == 1, "Exactly one active Water Volume");
            Check(!actions.Any(a => (a.title + a.body).Any(c => c >= '\u4e00' && c <= '\u9fff')), "All active world-action text English");
            foreach (var p in flow.props) report.Add("ITEM " + p.id + " at " + p.transform.position + " recovery=" + (p.recoveryPoint != null));
            foreach (var a in actions) report.Add("ACTION " + a.kind + " / " + a.fact + " / " + a.transform.position);
            report.Add("Reference audit only; does not claim input, rendering or end-to-end acceptance.");
        }
        [MenuItem("Tools/Deep Sea Demo/15 Audit Saved Complete Flow")]
        public static void AuditSaved()
        {
            Guard(); report.Clear(); var s = SceneManager.GetSceneByPath(DeepSeaDemoBuilder.Copy1VR); bool opened = !s.isLoaded;
            if (opened) s = EditorSceneManager.OpenScene(DeepSeaDemoBuilder.Copy1VR, OpenSceneMode.Additive);
            try { Audit(s); } finally { File.WriteAllLines("Assets/DeepSeaDemo/Reports/1VR-FlowAudit.txt", report); if (opened) EditorSceneManager.CloseScene(s, true); }
        }
    }
}
#endif
