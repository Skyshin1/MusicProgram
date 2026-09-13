using DeepSeaAI;
using System;
using System.Linq;
using AbstractOcclusion.WebGpuWater;
using UnityEngine;

namespace DeepSeaDemo
{
    [DefaultExecutionOrder(-40)]
    public sealed class DemoSceneBindings : MonoBehaviour
    {
        public DemoFlow flow;
        public DemoAcoustics acoustics;
        public Transform[] route;
        public Animator enemyAnimator;
        public BlackBoxPlaybackDock dock;
        AudioClip generatedEngine;
        void Start()
        {
            ApplyRuntimePolish();
            // The user moved both analysis receivers to the accessible first
            // floor. Keep the return checkpoint on that floor too, using the
            // existing validated platform spawn instead of inventing a point
            // inside the relocated workbench or its surrounding furniture.
            if (dock != null && flow.checkpoints != null && flow.checkpoints.Length > 3 &&
                flow.checkpoints[0] != null && flow.checkpoints[3] != null &&
                Mathf.Abs(flow.checkpoints[3].position.y - dock.transform.position.y) > 2f &&
                Mathf.Abs(flow.checkpoints[0].position.y - dock.transform.position.y) < 2f)
                flow.checkpoints[3].SetPositionAndRotation(flow.checkpoints[0].position, flow.checkpoints[0].rotation);
            if (flow.engineAudio != null && flow.engineAudio.clip == null)
            {
                const int rate = 16000; var data = new float[rate * 4];
                for (int i = 0; i < data.Length; i++)
                {
                    float t = (float)i / rate;
                    data[i] = (.16f * Mathf.Sin(t * 2 * Mathf.PI * 53) + .07f * Mathf.Sin(t * 2 * Mathf.PI * 107)) * (.7f + .3f * Mathf.Sin(t * 2 * Mathf.PI * 7));
                }
                generatedEngine = AudioClip.Create("Temporary engine loop", data.Length, 1, rate, false); generatedEngine.SetData(data, 0);
                flow.engineAudio.clip = generatedEngine; flow.engineAudio.loop = true;
            }
            flow.enemy.Configure(acoustics.RuntimeEnemyConfig, route, flow.player.transform,
                flow.player.GetComponent<PlayerRespawnController>(), enemyAnimator);
            if (dock != null) dock.OnPlaybackFinished.AddListener(flow.ParsedBlackBox);
            // Held props never push the tracked player's collision capsule.
            var playerColliders = flow.player.GetComponentsInChildren<Collider>(true);
            foreach (var prop in flow.props)
            {
                foreach (var collider in prop.GetComponentsInChildren<Collider>(true))
                    foreach (var playerCollider in playerColliders)
                        if (collider != null && playerCollider != null) Physics.IgnoreCollision(collider, playerCollider, true);
            }
        }
        void ApplyRuntimePolish()
        {
            if (flow.initialSpawn == null && flow.checkpoints != null && flow.checkpoints.Length > 0)
                flow.initialSpawn = flow.checkpoints[0];

            ReplaceProp("flashlight", "DeepSeaDemo/FinalModels/FlashLight");
            ReplaceProp("blackbox", "DeepSeaDemo/FinalModels/BlackBox");
            ReplaceProp("locktool", "DeepSeaDemo/FinalModels/RepairTool");
            foreach (var log in FindObjectsByType<DemoWorldAction>(FindObjectsSortMode.None)
                .Where(a => new[] { "log01", "log02", "log03", "log04" }.Contains(a.fact)))
                ReplaceVisual(log.gameObject, Resources.Load<GameObject>("DeepSeaDemo/FinalModels/File"), true);

            foreach (var stone in flow.props.Where(p => p.id != null && p.id.StartsWith("stone", StringComparison.Ordinal)))
            {
                stone.grabMode = DemoPropGrabMode.ThrowableAtDistance;
                stone.throwableVelocityScale = 1.45f;
                stone.throwableAngularVelocityScale = .9f;
                stone.ApplyGrabMode();
                var body = stone.GetComponent<Rigidbody>();
                if (body != null) { body.mass = .42f; body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; }
            }

            var toolProp = flow.props.FirstOrDefault(p => p.id == "locktool");
            toolProp?.GetComponent<RepairSkillCheckController>()?.ApplyAccessibleDefaults();
            ConfigureLadder();
            HidePersistentWorldGuides();

            // Water appearance is authored on the scene's WaterVolume. Do not
            // overwrite its colour/depth settings when entering Play Mode.
        }

        static void HidePersistentWorldGuides()
        {
            foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string n = canvas.name;
                if (n == "Equipment Instructions" || n == "Log Instructions" ||
                    n == "Analysis Instructions" || n.StartsWith("Sign ", StringComparison.Ordinal))
                    canvas.gameObject.SetActive(false);
            }
        }

        void ReplaceProp(string id, string resource)
        {
            var prop = flow.props.FirstOrDefault(p => p.id == id);
            if (prop == null) return;
            ReplaceVisual(prop.gameObject, Resources.Load<GameObject>(resource), false);
            prop.RefreshVisualRenderers();
        }

        static void ReplaceVisual(GameObject root, GameObject prefab, bool removeRootRenderer)
        {
            if (prefab == null || root.transform.Find("Final Visual") != null) return;
            Collider rootCollider = root.GetComponent<Collider>();
            Bounds target = rootCollider != null ? rootCollider.bounds : RendererBounds(root.GetComponentsInChildren<Renderer>(true));
            Transform old = root.transform.Find("Visual");
            if (old != null) Destroy(old.gameObject);
            if (removeRootRenderer)
            {
                foreach (var renderer in root.GetComponents<Renderer>()) renderer.enabled = false;
            }
            GameObject visual = Instantiate(prefab, root.transform);
            visual.name = "Final Visual";
            visual.transform.localPosition = Vector3.zero;
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true)) { collider.enabled = false; Destroy(collider); }
            foreach (Transform child in visual.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = root.layer;
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            Bounds source = RendererBounds(renderers);
            float factor = Mathf.Max(target.size.x, target.size.y, target.size.z) * .92f /
                Mathf.Max(.0001f, Mathf.Max(source.size.x, source.size.y, source.size.z));
            visual.transform.localScale *= factor;
            source = RendererBounds(renderers);
            visual.transform.position += target.center - source.center;
        }

        static Bounds RendererBounds(Renderer[] renderers)
        {
            if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one * .3f);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        void ConfigureLadder()
        {
            var action = FindObjectsByType<DemoWorldAction>(FindObjectsSortMode.None).FirstOrDefault(a => a.kind == DemoActionKind.Board);
            if (action == null || action.destination == null) return;
            var ladder = action.GetComponent<DemoLadderClimb>() ?? action.gameObject.AddComponent<DemoLadderClimb>();
            if (ladder.IsConfigured) return;
            Transform bottom = RuntimeAnchor(action.transform.parent, "Ladder Bottom", action.transform.position);
            Transform top = RuntimeAnchor(action.transform.parent, "Ladder Top", new Vector3(bottom.position.x, action.destination.position.y, bottom.position.z));
            ladder.bottomAnchor = bottom; ladder.topAnchor = top; ladder.platformExit = action.destination;
            ladder.climbSpeed = 1.35f; ladder.activationDistance = 3f; ladder.alignmentSeconds = .35f; ladder.stickDeadZone = .2f;
        }

        static Transform RuntimeAnchor(Transform parent, string name, Vector3 position)
        {
            Transform anchor = parent.Find(name);
            if (anchor == null) { anchor = new GameObject(name).transform; anchor.SetParent(parent, true); anchor.position = position; anchor.rotation = Quaternion.identity; }
            return anchor;
        }
        void OnDestroy() { if (dock != null && flow != null) dock.OnPlaybackFinished.RemoveListener(flow.ParsedBlackBox); if (generatedEngine != null) Destroy(generatedEngine); }
    }
}
