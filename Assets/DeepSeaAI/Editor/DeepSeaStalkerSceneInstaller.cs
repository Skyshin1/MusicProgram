using System;
using System.IO;
using System.Linq;
using DeepSeaAI;
using Unity.AI.Navigation;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;

namespace DeepSeaAI.Editor
{
    [InitializeOnLoad]
    public static class DeepSeaStalkerSceneInstaller
    {
        private const string TargetScenePath = "Assets/Scenes/1-VR.unity";
        private const string RootName = "Deep Sea Stalker AI";
        private const string GeneratedFolder = "Assets/DeepSeaAI/Generated";
        private const string ConfigPath = GeneratedFolder + "/DeepSeaStalkerConfig.asset";
        private const string MaterialPath = GeneratedFolder + "/ZombieStalker.mat";
        private const string ControllerPath = GeneratedFolder + "/ZombieStalker.controller";
        private const string IdleClipPath = GeneratedFolder + "/ZombieIdleLoop.anim";
        private const string WalkClipPath = GeneratedFolder + "/ZombieWalkLoop.anim";
        private const string AttackClipPath = GeneratedFolder + "/ZombieAttack.anim";
        private const string NavMeshPath = GeneratedFolder + "/DeepSeaFloorNavMesh.asset";
        private const string BehaviorGraphPath = GeneratedFolder + "/DeepSeaStalkerBehaviorGraph.asset";
        private const string ModelPath =
            "Assets/ThirdParty/Quaternius/ZombieApocalypse/Model/Zombie_Basic.fbx";
        private const string TexturePath =
            "Assets/ThirdParty/Quaternius/ZombieApocalypse/Model/Zombie_Atlas.png";
        private const string InstallSessionKey = "DeepSeaAI.SceneInstalled.v8";

        static DeepSeaStalkerSceneInstaller()
        {
            EditorApplication.delayCall += TryAutomaticInstall;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Tools/Deep Sea AI/Install or Repair 1-VR Scene")]
        public static void InstallFromMenu()
        {
            InstallOrRepair(true);
        }

        [MenuItem("Tools/Deep Sea AI/Select Stalker Config")]
        private static void SelectConfig()
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<DeepSeaStalkerConfig>(ConfigPath);
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += TryAutomaticInstall;
        }

        private static void TryAutomaticInstall()
        {
            if (Application.isPlaying)
                return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryAutomaticInstall;
                return;
            }
            if (SceneManager.GetActiveScene().path != TargetScenePath)
                return;
            if (SessionState.GetBool(InstallSessionKey, false) &&
                GameObject.Find(RootName) != null)
                return;

            InstallOrRepair(false);
        }

        private static void InstallOrRepair(bool explicitRequest)
        {
            if (Application.isPlaying)
            {
                if (explicitRequest)
                    EditorUtility.DisplayDialog(
                        "Deep Sea AI",
                        "Exit Play Mode, then run the installer again.",
                        "OK");
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.path != TargetScenePath)
            {
                if (!explicitRequest)
                    return;
                activeScene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
            }

            EnsureFolder("Assets/DeepSeaAI");
            EnsureFolder(GeneratedFolder);
            ConfigureModelImporter();

            DeepSeaStalkerConfig config = EnsureConfig();
            Material monsterMaterial = EnsureMonsterMaterial();
            RuntimeAnimatorController runtimeController = EnsureAnimatorController();

            GameObject existing = GameObject.Find(RootName);
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing);

            Bounds floorBounds = FindFloorBounds();
            float floorY = floorBounds.max.y;

            GameObject root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Install Deep Sea Stalker AI");

            Transform[] patrolPoints = CreatePatrolRoute(root.transform, floorBounds, floorY);
            NavMeshSurface surface = CreateAndBakeNavMesh(root.transform, floorBounds, floorY);
            PlayerRespawnController respawn = ConfigurePlayerRespawn(root.transform, floorY);
            GameObject stalker = CreateStalker(
                root.transform,
                patrolPoints,
                config,
                monsterMaterial,
                runtimeController,
                respawn);

            TryAttachBehaviorAgent(stalker);
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            SessionState.SetBool(InstallSessionKey, true);
            Selection.activeGameObject = stalker;

            Debug.Log(
                "[DeepSeaAI] Installed patrol stalker, five patrol points, dedicated NavMesh, " +
                "sonar reveal, impact hearing and XR-safe respawn into Assets/Scenes/1-VR.unity.",
                stalker);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            string name = Path.GetFileName(folder);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static void ConfigureModelImporter()
        {
            ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
                return;

            bool changed = false;
            if (importer.animationType != ModelImporterAnimationType.Generic)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                changed = true;
            }
            if (!importer.importAnimation)
            {
                importer.importAnimation = true;
                changed = true;
            }
            if (importer.avatarSetup != ModelImporterAvatarSetup.NoAvatar)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
                changed = true;
            }

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            bool clipSettingsChanged = clips == null || clips.Length == 0;
            if (clipSettingsChanged)
                clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                string clipName = clip.name.ToLowerInvariant();
                bool shouldLoop =
                    clipName.Contains("idle") ||
                    clipName.Contains("walk") ||
                    clipName.Contains("run") ||
                    clipName.Contains("move") ||
                    clipName.Contains("search");

                if (clip.loopTime != shouldLoop)
                {
                    clip.loopTime = shouldLoop;
                    clipSettingsChanged = true;
                }
                if (shouldLoop && !clip.loopPose)
                {
                    clip.loopPose = true;
                    clipSettingsChanged = true;
                }
            }

            if (clipSettingsChanged)
            {
                importer.clipAnimations = clips;
                changed = true;
            }

            if (changed)
                importer.SaveAndReimport();
        }

        private static DeepSeaStalkerConfig EnsureConfig()
        {
            DeepSeaStalkerConfig config =
                AssetDatabase.LoadAssetAtPath<DeepSeaStalkerConfig>(ConfigPath);
            if (config != null)
                return config;

            config = ScriptableObject.CreateInstance<DeepSeaStalkerConfig>();
            config.name = "Deep Sea Stalker Config";
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        private static Material EnsureMonsterMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader)
                {
                    name = "Zombie Stalker"
                };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (texture != null)
            {
                material.SetTexture("_BaseMap", texture);
                material.SetTexture("_MainTex", texture);
            }
            material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static RuntimeAnimatorController EnsureAnimatorController()
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) is { } existing)
                AssetDatabase.DeleteAsset(ControllerPath);

            AnimatorController controller =
                AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsSearching", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            AnimationClip idle = FindClip(clips, "idle") ?? clips.FirstOrDefault();
            AnimationClip walk =
                FindClip(clips, "walk", "run", "move") ?? idle;
            AnimationClip attack =
                FindClip(clips, "attack", "hit", "bite") ?? idle;

            idle = EnsureGeneratedAnimationClip(idle, IdleClipPath, "Idle Loop", true);
            walk = EnsureGeneratedAnimationClip(walk, WalkClipPath, "Walk Loop", true);
            attack = EnsureGeneratedAnimationClip(attack, AttackClipPath, "Attack", false);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState idleState = machine.AddState("Idle");
            idleState.motion = idle;
            machine.defaultState = idleState;

            AnimatorState walkState = machine.AddState("Walk");
            walkState.motion = walk;
            AnimatorStateTransition toWalk = idleState.AddTransition(walkState);
            toWalk.hasExitTime = false;
            toWalk.duration = 0.12f;
            toWalk.AddCondition(AnimatorConditionMode.Greater, 0.05f, "Speed");

            AnimatorStateTransition toIdle = walkState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.15f;
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.05f, "Speed");

            AnimatorState attackState = machine.AddState("Attack");
            attackState.motion = attack;
            AnimatorStateTransition toAttack = machine.AddAnyStateTransition(attackState);
            toAttack.hasExitTime = false;
            toAttack.duration = 0.05f;
            toAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");

            AnimatorStateTransition attackToIdle = attackState.AddTransition(idleState);
            attackToIdle.hasExitTime = true;
            attackToIdle.exitTime = 0.9f;
            attackToIdle.duration = 0.12f;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimationClip EnsureGeneratedAnimationClip(
            AnimationClip source,
            string path,
            string assetName,
            bool loop)
        {
            if (source == null)
                return null;

            AnimationClip generated = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (generated == null)
            {
                generated = UnityEngine.Object.Instantiate(source);
                generated.hideFlags = HideFlags.None;
                AssetDatabase.CreateAsset(generated, path);
            }
            else
            {
                EditorUtility.CopySerialized(source, generated);
            }

            generated.name = assetName;
            generated.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(generated);
            settings.loopTime = loop;
            settings.loopBlend = loop;
            AnimationUtility.SetAnimationClipSettings(generated, settings);
            EditorUtility.SetDirty(generated);
            return generated;
        }

        private static AnimationClip FindClip(AnimationClip[] clips, params string[] terms)
        {
            return clips.FirstOrDefault(
                clip => terms.Any(
                    term => clip.name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        private static Bounds FindFloorBounds()
        {
            GameObject floor = GameObject.Find("Plane");
            if (floor == null)
            {
                floor = UnityEngine.Object.FindObjectsByType<GameObject>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .FirstOrDefault(candidate =>
                        candidate.name.IndexOf("floor", StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (floor != null)
            {
                Collider collider = floor.GetComponent<Collider>();
                if (collider != null)
                    return collider.bounds;
                Renderer renderer = floor.GetComponent<Renderer>();
                if (renderer != null)
                    return renderer.bounds;
            }

            return new Bounds(new Vector3(0f, -7.5f, 0f), new Vector3(50f, 0.1f, 50f));
        }

        private static Transform[] CreatePatrolRoute(
            Transform parent,
            Bounds floorBounds,
            float floorY)
        {
            GameObject routeObject = new GameObject("Patrol Route P0-P4");
            routeObject.transform.SetParent(parent, false);

            float radiusX = Mathf.Clamp(floorBounds.extents.x * 0.36f, 6f, 16f);
            float radiusZ = Mathf.Clamp(floorBounds.extents.z * 0.36f, 6f, 16f);
            Vector3 center = new Vector3(floorBounds.center.x, floorY, floorBounds.center.z);
            Vector2[] offsets =
            {
                new Vector2(-0.9f, -0.55f),
                new Vector2(0.05f, -0.95f),
                new Vector2(0.9f, -0.45f),
                new Vector2(0.75f, 0.75f),
                new Vector2(-0.8f, 0.7f)
            };

            Transform[] result = new Transform[offsets.Length];
            for (int i = 0; i < offsets.Length; i++)
            {
                GameObject point = new GameObject("P" + i);
                point.transform.SetParent(routeObject.transform, false);
                point.transform.position =
                    center + new Vector3(offsets[i].x * radiusX, 0.03f, offsets[i].y * radiusZ);
                result[i] = point.transform;
            }
            return result;
        }

        private static NavMeshSurface CreateAndBakeNavMesh(
            Transform parent,
            Bounds floorBounds,
            float floorY)
        {
            GameObject navObject = new GameObject("Deep Sea Floor Navigation");
            navObject.transform.SetParent(parent, false);
            NavMeshSurface surface = navObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            surface.ignoreNavMeshAgent = true;
            surface.ignoreNavMeshObstacle = false;
            surface.center = new Vector3(
                floorBounds.center.x,
                floorY + 0.6f,
                floorBounds.center.z);
            surface.size = new Vector3(
                Mathf.Clamp(floorBounds.size.x, 24f, 70f),
                2.8f,
                Mathf.Clamp(floorBounds.size.z, 24f, 70f));
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.12f;
            surface.overrideTileSize = true;
            surface.tileSize = 128;

            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshPath) != null)
                AssetDatabase.DeleteAsset(NavMeshPath);

            surface.BuildNavMesh();
            if (surface.navMeshData != null && !AssetDatabase.Contains(surface.navMeshData))
                AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
            return surface;
        }

        private static PlayerRespawnController ConfigurePlayerRespawn(
            Transform parent,
            float floorY)
        {
            XROrigin xrOrigin =
                UnityEngine.Object.FindFirstObjectByType<XROrigin>(FindObjectsInactive.Include);
            Transform playerRoot = xrOrigin != null
                ? xrOrigin.transform
                : Camera.main != null ? Camera.main.transform.root : null;
            if (playerRoot == null)
                return null;

            PlayerRespawnController respawn =
                playerRoot.GetComponent<PlayerRespawnController>();
            if (respawn == null)
                respawn = playerRoot.gameObject.AddComponent<PlayerRespawnController>();

            GameObject point = new GameObject("Player Respawn Point");
            point.transform.SetParent(parent, false);
            Camera playerCamera = xrOrigin != null ? xrOrigin.Camera : Camera.main;
            if (playerCamera != null)
            {
                Vector3 forward =
                    Vector3.ProjectOnPlane(playerCamera.transform.forward, Vector3.up).normalized;
                if (forward.sqrMagnitude < 0.001f)
                    forward = Vector3.forward;
                point.transform.SetPositionAndRotation(
                    new Vector3(
                        playerCamera.transform.position.x,
                        Mathf.Max(playerCamera.transform.position.y, floorY + 1.6f),
                        playerCamera.transform.position.z),
                    Quaternion.LookRotation(forward, Vector3.up));
            }
            else
            {
                point.transform.position = new Vector3(0f, floorY + 1.6f, 0f);
            }

            respawn.Configure(point.transform);
            EditorUtility.SetDirty(respawn);
            return respawn;
        }

        private static GameObject CreateStalker(
            Transform parent,
            Transform[] route,
            DeepSeaStalkerConfig config,
            Material material,
            RuntimeAnimatorController runtimeController,
            PlayerRespawnController respawn)
        {
            GameObject npc = new GameObject("Deep Sea Stalker - Sonar Red");
            npc.transform.SetParent(parent, false);
            npc.transform.position = route[0].position;

            CapsuleCollider capsule = npc.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.9f, 0f);
            capsule.height = 1.8f;
            capsule.radius = 0.32f;

            NavMeshAgent agent = npc.AddComponent<NavMeshAgent>();
            agent.radius = 0.3f;
            agent.height = 1.8f;
            agent.baseOffset = 0f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            agent.avoidancePriority = 40;

            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            GameObject model;
            if (modelAsset != null)
            {
                model = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
                model.name = "Quaternius Zombie Basic (CC0)";
                model.transform.SetParent(npc.transform, false);
            }
            else
            {
                model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                model.name = "Fallback Humanoid";
                model.transform.SetParent(npc.transform, false);
                UnityEngine.Object.DestroyImmediate(model.GetComponent<Collider>());
            }

            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] slots = renderer.sharedMaterials;
                if (slots == null || slots.Length == 0)
                    slots = new Material[1];
                for (int i = 0; i < slots.Length; i++)
                    slots[i] = material;
                renderer.sharedMaterials = slots;
            }

            FitModelToHeight(model.transform, 1.8f);

            Animator animator = model.GetComponentInChildren<Animator>(true);
            if (animator == null)
                animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = runtimeController;
            animator.applyRootMotion = false;

            SonarRevealStyle reveal = npc.AddComponent<SonarRevealStyle>();
            reveal.Configure(config.monsterOutlineColor, config.revealDuration, false);

            DeepSeaStalkerController controller =
                npc.AddComponent<DeepSeaStalkerController>();
            XROrigin xrOrigin =
                UnityEngine.Object.FindFirstObjectByType<XROrigin>(FindObjectsInactive.Include);
            Transform player = xrOrigin != null
                ? xrOrigin.transform
                : Camera.main != null ? Camera.main.transform.root : null;
            controller.Configure(config, route, player, respawn, animator);

            return npc;
        }

        private static void FitModelToHeight(Transform model, float targetHeight)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            if (bounds.size.y < 0.001f)
                return;

            float scale = targetHeight / bounds.size.y;
            model.localScale *= scale;

            renderers = model.GetComponentsInChildren<Renderer>(true);
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            model.position += Vector3.up * (model.parent.position.y - bounds.min.y);
        }

        private static BehaviorGraph EnsureBehaviorGraph()
        {
            BehaviorGraph existing =
                AssetDatabase.LoadAssetAtPath<BehaviorGraph>(BehaviorGraphPath);
            if (existing != null)
                AssetDatabase.DeleteAsset(BehaviorGraphPath);

            BehaviorGraph graph = ScriptableObject.CreateInstance<BehaviorGraph>();
            graph.name = "Deep Sea Stalker Priority Graph";

            var module = new BehaviorGraphModule
            {
                AuthoringAssetID = SerializableGUID.Generate()
            };

            var self = new BlackboardVariable<GameObject>(null)
            {
                Name = "Self",
                GUID = SerializableGUID.Generate()
            };
            module.BlackboardReference.Blackboard.Variables.Add(self);
            module.BlackboardReference.AddVariable("Player", (GameObject)null);
            module.BlackboardReference.AddVariable("PatrolPoints", (GameObject)null);
            module.BlackboardReference.AddVariable("CanSeePlayer", false);
            module.BlackboardReference.AddVariable("LastSeenPosition", Vector3.zero);
            module.BlackboardReference.AddVariable("HasNoise", false);
            module.BlackboardReference.AddVariable("LastNoisePosition", Vector3.zero);
            module.BlackboardReference.AddVariable("NoiseScore", 0f);
            module.BlackboardReference.AddVariable("ActiveState", "Patrol");

            var selector = new SelectorComposite();
            selector.Add(new DeepSeaChasePriorityAction { Agent = self });
            selector.Add(new DeepSeaInvestigatePriorityAction { Agent = self });
            selector.Add(new DeepSeaPatrolPriorityAction { Agent = self });

            var root = new Unity.Behavior.Start { Repeat = true };
            root.Add(selector);
            module.Root = root;
            graph.Graphs.Add(module);

            AssetDatabase.CreateAsset(graph, BehaviorGraphPath);
            EditorUtility.SetDirty(graph);
            return graph;
        }

        private static void TryAttachBehaviorAgent(GameObject npc)
        {
            BehaviorGraph graph = EnsureBehaviorGraph();
            BehaviorGraphAgent behaviorAgent = npc.GetComponent<BehaviorGraphAgent>();
            if (behaviorAgent == null)
                behaviorAgent = npc.AddComponent<BehaviorGraphAgent>();
            behaviorAgent.Graph = graph;

            DeepSeaStalkerController controller =
                npc.GetComponent<DeepSeaStalkerController>();
            XROrigin xrOrigin =
                UnityEngine.Object.FindFirstObjectByType<XROrigin>(FindObjectsInactive.Include);
            GameObject player = xrOrigin != null
                ? xrOrigin.gameObject
                : Camera.main != null ? Camera.main.transform.root.gameObject : null;
            Transform route = npc.transform.parent != null
                ? npc.transform.parent.Find("Patrol Route P0-P4")
                : null;

            DeepSeaStalkerBehaviorBridge bridge =
                npc.GetComponent<DeepSeaStalkerBehaviorBridge>();
            if (bridge == null)
                bridge = npc.AddComponent<DeepSeaStalkerBehaviorBridge>();
            bridge.Configure(
                behaviorAgent,
                controller,
                player,
                route != null ? route.gameObject : null);

            EditorUtility.SetDirty(behaviorAgent);
            EditorUtility.SetDirty(bridge);
        }    }
}
