#if UNITY_EDITOR
using System;
using System.Linq;
using DeepSeaAI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using Object = UnityEngine.Object;

namespace DeepSeaDemo.Editor
{
    [InitializeOnLoad]
    public static class DemoFinalPolishSetup
    {
        const string ScenePath = "Assets/DeepSeaDemo/Scenes/DeepSeaInvestigation_1VR.unity";
        const string AutoKey = "DeepSeaDemo.FinalPolishApplied.20260911c";

        static DemoFinalPolishSetup()
        {
            if (!SessionState.GetBool(AutoKey, false)) EditorApplication.update += ApplyOnceWhenReady;
        }

        static void ApplyOnceWhenReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            // Unity temporarily restores play-mode scenes through a Temp backup.
            // Keep waiting until the authored 1VR scene is active again.
            if (SceneManager.GetActiveScene().path != ScenePath) return;
            EditorApplication.update -= ApplyOnceWhenReady;
            try
            {
                Apply();
                SessionState.SetBool(AutoKey, true);
            }
            catch (InvalidOperationException exception) when (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                // Play can be pressed between the update guard and the final
                // scene save. Retry after returning to Edit Mode; never mark a
                // play-mode-only mutation as applied.
                Debug.Log("[DeepSeaDemo] Final polish deferred until Edit Mode: " + exception.Message);
                EditorApplication.update += ApplyOnceWhenReady;
            }
        }

        [MenuItem("Tools/Deep Sea Demo/16 Apply Final Interaction and Art Polish")]
        public static void Apply()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath)
                throw new InvalidOperationException("Open DeepSeaInvestigation_1VR in Edit Mode. No other scene is modified.");

            DemoFlow flow = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DemoFlow>(true)).Single();
            Undo.RecordObject(flow, "Configure final demo polish");
            flow.initialSpawn = ConfigureSpawnMarker(flow);

            GameObject flashlightModel = Load("Assets/Prefab/FlashLight.prefab");
            GameObject fileModel = Load("Assets/Prefab/File.prefab");
            GameObject blackBoxModel = Load("Assets/Prefab/黑匣子.prefab");
            GameObject repairModel = Load("Assets/Prefab/RepairTool.prefab");

            ReplacePropVisual(flow.props.Single(p => p.id == "flashlight"), flashlightModel);
            ReplacePropVisual(flow.props.Single(p => p.id == "blackbox"), blackBoxModel);
            ReplacePropVisual(flow.props.Single(p => p.id == "locktool"), repairModel);

            foreach (DemoWorldAction log in scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<DemoWorldAction>(true))
                .Where(a => new[] { "log01", "log02", "log03", "log04" }.Contains(a.fact)))
                ReplaceActionVisual(log, fileModel);

            ConfigureToolOrigins(flow);
            ConfigureStones(flow);
            ConfigureQte(flow);
            ConfigureLadder(scene, flow);
            HidePersistentWorldGuides(scene);
            Demo1VRPresentationSetup.Apply();

            EditorUtility.SetDirty(flow);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[DeepSeaDemo] Final models, stable held props, camera QTE, spawn marker, manual ladder and hidden persistent guides applied.");
        }

        static Transform ConfigureSpawnMarker(DemoFlow flow)
        {
            Transform marker = flow.transform.Find("Player Spawn (Move Me)");
            if (marker == null)
            {
                marker = new GameObject("Player Spawn (Move Me)").transform;
                Undo.RegisterCreatedObjectUndo(marker.gameObject, "Create configurable player spawn");
                marker.SetParent(flow.transform, true);
                Transform source = flow.initialSpawn != null ? flow.initialSpawn : flow.checkpoints[0];
                marker.SetPositionAndRotation(source.position, source.rotation);
            }
            return marker;
        }

        static void HidePersistentWorldGuides(Scene scene)
        {
            foreach (Canvas canvas in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Canvas>(true)))
            {
                string n = canvas.name;
                if (n != "Equipment Instructions" && n != "Log Instructions" &&
                    n != "Analysis Instructions" && !n.StartsWith("Sign ", StringComparison.Ordinal)) continue;
                Undo.RecordObject(canvas.gameObject, "Hide persistent world guide");
                canvas.gameObject.SetActive(false);
                EditorUtility.SetDirty(canvas.gameObject);
            }
        }

        static GameObject Load(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("Missing replacement prefab: " + path);
            return prefab;
        }

        static void ReplacePropVisual(DemoProp prop, GameObject model)
        {
            Undo.RecordObject(prop.gameObject, "Replace prop model");
            Bounds target = TargetBounds(prop.gameObject);
            Transform old = prop.transform.Find("Visual");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            Transform previous = prop.transform.Find("Final Visual");
            if (previous != null) Undo.DestroyObjectImmediate(previous.gameObject);
            AddFittedVisual(prop.gameObject, model, target);
            prop.ApplyGrabMode();
            prop.RefreshVisualRenderers();
            EditorUtility.SetDirty(prop);
        }

        static void ReplaceActionVisual(DemoWorldAction action, GameObject model)
        {
            Bounds target = TargetBounds(action.gameObject);
            foreach (Renderer renderer in action.GetComponents<Renderer>()) Undo.DestroyObjectImmediate(renderer);
            foreach (MeshFilter filter in action.GetComponents<MeshFilter>()) Undo.DestroyObjectImmediate(filter);
            Transform previous = action.transform.Find("Final Visual");
            if (previous != null) Undo.DestroyObjectImmediate(previous.gameObject);
            AddFittedVisual(action.gameObject, model, target);
        }

        static void AddFittedVisual(GameObject root, GameObject prefab, Bounds target)
        {
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            Undo.RegisterCreatedObjectUndo(visual, "Add final model");
            visual.name = "Final Visual";
            visual.transform.localPosition = Vector3.zero;
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true)) Undo.DestroyObjectImmediate(collider);
            SetLayer(visual, root.layer);
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException(prefab.name + " has no renderer");
            Bounds source = RendererBounds(renderers);
            float sourceMax = Mathf.Max(source.size.x, source.size.y, source.size.z);
            float targetMax = Mathf.Max(target.size.x, target.size.y, target.size.z) * .92f;
            if (sourceMax > .0001f) visual.transform.localScale *= targetMax / sourceMax;
            source = RendererBounds(renderers);
            visual.transform.position += target.center - source.center;
        }

        static Bounds TargetBounds(GameObject root)
        {
            Collider collider = root.GetComponent<Collider>();
            if (collider != null) return collider.bounds;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.one * .3f);
            return RendererBounds(renderers);
        }

        static Bounds RendererBounds(Renderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        static void SetLayer(GameObject root, int layer)
        { foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer; }

        static void ConfigureToolOrigins(DemoFlow flow)
        {
            DemoProp flashlight = flow.props.Single(p => p.id == "flashlight");
            Transform beam = Point(flashlight.transform, "Beam Origin", flashlight.GetComponent<Collider>());
            SerializedObject light = new SerializedObject(flashlight.GetComponent<GrabFlashlight>());
            light.FindProperty("beamOrigin").objectReferenceValue = beam; light.ApplyModifiedPropertiesWithoutUndo();

            DemoProp repair = flow.props.Single(p => p.id == "locktool");
            Transform tip = Point(repair.transform, "Repair Tip", repair.GetComponent<Collider>());
            SerializedObject tool = new SerializedObject(repair.GetComponent<RepairTool>());
            tool.FindProperty("repairTip").objectReferenceValue = tip;
            tool.FindProperty("repairRadius").floatValue = .42f;
            tool.ApplyModifiedPropertiesWithoutUndo();
        }

        static Transform Point(Transform root, string name, Collider collider)
        {
            Transform point = root.Find(name);
            if (point == null) { point = new GameObject(name).transform; Undo.RegisterCreatedObjectUndo(point.gameObject, "Add tool origin"); point.SetParent(root, false); }
            Bounds bounds = collider != null ? collider.bounds : new Bounds(root.position, Vector3.one * .3f);
            point.position = bounds.center + root.forward * Mathf.Max(.08f, bounds.extents.magnitude * .55f);
            point.rotation = root.rotation;
            return point;
        }

        static void ConfigureStones(DemoFlow flow)
        {
            foreach (DemoProp stone in flow.props.Where(p => p.id != null && p.id.StartsWith("stone", StringComparison.Ordinal)))
            {
                Undo.RecordObject(stone, "Configure throwing stone");
                stone.grabMode = DemoPropGrabMode.ThrowableAtDistance;
                stone.throwableVelocityScale = 1.45f;
                stone.throwableAngularVelocityScale = .9f;
                stone.ApplyGrabMode();
                Rigidbody body = stone.GetComponent<Rigidbody>();
                Undo.RecordObject(body, "Configure throwing stone body");
                body.mass = .42f; body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                Component pulse = stone.GetComponent<VolumetricFogCollisionPulse>();
                if (pulse != null)
                {
                    var so = new SerializedObject(pulse);
                    var speed = so.FindProperty("minimumRelativeSpeed"); if (speed != null) speed.floatValue = .35f;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorUtility.SetDirty(stone);
            }
        }

        static void ConfigureQte(DemoFlow flow)
        {
            RepairSkillCheckController qte = flow.props.Single(p => p.id == "locktool").GetComponent<RepairSkillCheckController>();
            var so = new SerializedObject(qte);
            Float(so, "firstCheckDelay", 1.25f); Float(so, "minimumInterval", 6f); Float(so, "maximumInterval", 8f);
            Float(so, "checkDuration", 2.2f); Float(so, "needleDegreesPerSecond", 150f);
            Float(so, "successArcDegrees", 110f); Float(so, "perfectArcDegrees", 28f); Float(so, "failureProgressRegression", .03f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigureLadder(Scene scene, DemoFlow flow)
        {
            DemoWorldAction action = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DemoWorldAction>(true))
                .Single(a => a.kind == DemoActionKind.Board);
            DemoLadderClimb ladder = action.GetComponent<DemoLadderClimb>() ?? Undo.AddComponent<DemoLadderClimb>(action.gameObject);
            if (ladder.IsConfigured) return;
            Transform parent = action.transform.parent;
            Transform bottom = Anchor(parent, "Ladder Bottom", action.transform.position);
            Vector3 topPosition = new Vector3(bottom.position.x, action.destination.position.y, bottom.position.z);
            Transform top = Anchor(parent, "Ladder Top", topPosition);
            ladder.bottomAnchor = bottom; ladder.topAnchor = top; ladder.platformExit = action.destination;
            ladder.climbSpeed = 1.35f; ladder.activationDistance = 3f; ladder.alignmentSeconds = .35f; ladder.stickDeadZone = .2f;
            EditorUtility.SetDirty(ladder);
        }

        static Transform Anchor(Transform parent, string name, Vector3 position)
        {
            Transform result = parent.Find(name);
            if (result == null) { result = new GameObject(name).transform; Undo.RegisterCreatedObjectUndo(result.gameObject, "Add ladder anchor"); result.SetParent(parent, true); }
            result.position = position; result.rotation = Quaternion.identity; return result;
        }

        static void Float(SerializedObject so, string name, float value)
        { SerializedProperty property = so.FindProperty(name); if (property == null) throw new InvalidOperationException("Missing field " + name); property.floatValue = value; }
    }
}
#endif
