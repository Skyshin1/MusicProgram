#if UNITY_EDITOR
using System.Linq;
using DeepSeaAI;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace DeepSeaDemo.Editor
{
    public static partial class DeepSeaDemoBuilder
    {
        static void CreateFish(Transform parent, DemoFlow flow, DemoSceneBindings bindings)
        {
            var navigation = Make("Seabed Navigation", parent);
            var surface = navigation.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume; surface.center = new Vector3(0, -22, 14);
            surface.size = new Vector3(94, 8, 92); surface.layerMask = config.worldMask;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.overrideVoxelSize = true; surface.voxelSize = .15f;
            surface.BuildNavMesh();
            if (surface.navMeshData != null)
            {
                string navPath = Root + "/Generated/DemoSeabedNavigation.asset";
                var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
                if (existing == null) AssetDatabase.CreateAsset(surface.navMeshData, navPath);
                else { EditorUtility.CopySerialized(surface.navMeshData, existing); surface.RemoveData(); surface.navMeshData = existing; surface.AddData(); EditorUtility.SetDirty(existing); }
            }
            var cfg = Asset<DeepSeaStalkerConfig>(Root + "/Settings/EnemyConfig.asset", () => ScriptableObject.CreateInstance<DeepSeaStalkerConfig>());
            cfg.patrolSpeed = 1.2f; cfg.investigateSpeedMultiplier = 1.5f; cfg.chaseSpeed = 2.6f;
            cfg.sightBlockers = config.worldMask; cfg.requireLineOfSight = true; cfg.eyeHeight = .7f;
            cfg.sonarHearingRadius = config.baseHearingRadius; EditorUtility.SetDirty(cfg);
            bindings.acoustics.enemyConfig = cfg;
            var route = Make("Enemy Random Patrol Route", parent);
            var routePositions = new[] { new Vector3(-8, -23.5f, 6), new Vector3(7, -23.5f, 6), new Vector3(9, -23.5f, 25), new Vector3(-8, -23.5f, 29), new Vector3(-22, -23.5f, 19) };
            bindings.route = routePositions.Select((p, i) => Point("Patrol " + i, route.transform, p)).ToArray();
            foreach (var point in bindings.route) if (NavMesh.SamplePosition(point.position, out var hit, 2, NavMesh.AllAreas)) point.position = hit.position;
            var enemy = Make("Enemy Fish", parent); enemy.transform.position = bindings.route[0].position;
            var agent = enemy.AddComponent<NavMeshAgent>(); agent.radius = .36f; agent.height = 1.1f; agent.baseOffset = .05f;
            var visual = Load("Assets/Fish/Enemy.fbx", enemy.transform); visual.name = "Visual Enemy Fish";
            Fit(visual, enemy.transform.position + Vector3.up * .8f, 1.8f); UpgradeMaterials(visual); SetLayer(visual, worldLayer);
            bindings.enemyAnimator = FishAnimator(visual, "EnemyFish");
            var enemyCollider = enemy.AddComponent<CapsuleCollider>(); enemyCollider.radius = .4f; enemyCollider.height = 1.2f; enemyCollider.center = Vector3.up * .7f;
            enemy.layer = propLayer;
            flow.enemy = enemy.AddComponent<DeepSeaStalkerController>();
            Set(flow.enemy, "listenToRawPulses", false); Set(flow.enemy, "useStimulusSonarRadius", true); Set(flow.enemy, "validateAttackReach", true);
            flow.enemy.Configure(cfg, bindings.route, flow.player.transform, flow.player.GetComponent<PlayerRespawnController>(), bindings.enemyAnimator);
            for (int species = 0; species < 2; species++)
            {
                var school = Make("Fish School " + (species + 1), parent); school.transform.position = new Vector3(species == 0 ? -9 : 6, -19, species == 0 ? 1 : 27);
                for (int i = 0; i < 5; i++)
                {
                    var fish = Make("Fish " + species + "-" + i, school.transform); fish.transform.localPosition = new Vector3((i - 2) * .8f, (i % 3) * .4f, i % 2);
                    var model = Load(species == 0 ? "Assets/Fish/Fish-1.fbx" : "Assets/Fish/Fish2.fbx", fish.transform);
                    model.name = "Visual"; Fit(model, fish.transform.position, species == 0 ? .7f : .9f); UpgradeMaterials(model);
                    FishAnimator(model, "Fish" + species);
                    var audio = fish.AddComponent<AudioSource>(); var ai = fish.AddComponent<DeepSeaFishAI>();
                    ai.ConfigureDemo(school.transform, null, model.transform, audio, 6);
                    Set(ai, "obstacleLayers", config.worldMask); Set(ai, "avoidObstacles", true);
                    SetLayer(fish, propLayer);
                }
            }
            notes.Add("NavMesh baked for seabed volume; 1 enemy + 10 fish; source models preserved.");
        }
        static Animator FishAnimator(GameObject visual, string name)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(visual);
            string path = source != null ? AssetDatabase.GetAssetPath(source) : "";
            var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
            var controllerPath = Root + "/Generated/" + name + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                controller.AddParameter("Speed", AnimatorControllerParameterType.Float); controller.AddParameter("Flee", AnimatorControllerParameterType.Bool);
                controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger); controller.AddParameter("Walking", AnimatorControllerParameterType.Bool);
                var state = controller.layers[0].stateMachine.AddState("Swim");
                state.motion = clips.FirstOrDefault(c => c.name.ToLowerInvariant().Contains("swim")) ?? clips.FirstOrDefault();
                controller.layers[0].stateMachine.defaultState = state;
            }
            var animator = visual.GetComponent<Animator>();
            if (animator == null) animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
            notes.Add(name + " imported animations: " + string.Join(", ", clips.Select(c => c.name)));
            return animator;
        }
    }
}
#endif
