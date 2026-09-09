#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeepSeaAI;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeepSeaDemo.Editor
{
    public static partial class DeepSeaDemoBuilder
    {
        static void SaveReusable(GameObject root, DemoFlow flow)
        {
            foreach (var prop in props)
            {
                var recovery = prop.recoveryPoint; prop.recoveryPoint = null;
                PrefabUtility.SaveAsPrefabAsset(prop.gameObject, Root + "/Prefabs/" + prop.id + ".prefab");
                prop.recoveryPoint = recovery;
            }
            var playerRoot = flow.player.transform;
            while (playerRoot.parent != null && playerRoot.name != "VR Player") playerRoot = playerRoot.parent;
            PrefabUtility.SaveAsPrefabAsset(playerRoot.gameObject, Root + "/Prefabs/VRPlayer.prefab");
            string savedEnemy = EditorJsonUtility.ToJson(flow.enemy);
            flow.enemy.Configure(flow.enemy.GetComponentInParent<DemoFlow>()?.GetComponent<DemoAcoustics>()?.enemyConfig ??
                AssetDatabase.LoadAssetAtPath<DeepSeaStalkerConfig>(Root + "/Settings/EnemyConfig.asset"),
                Array.Empty<Transform>(), null, null, flow.enemy.GetComponentInChildren<Animator>());
            PrefabUtility.SaveAsPrefabAsset(flow.enemy.gameObject, Root + "/Prefabs/EnemyFish.prefab");
            EditorJsonUtility.FromJsonOverwrite(savedEnemy, flow.enemy);
            foreach (var fish in root.GetComponentsInChildren<DeepSeaFishAI>().Take(1))
                PrefabUtility.SaveAsPrefabAsset(fish.transform.parent.gameObject, Root + "/Prefabs/FishSchool1.prefab");
            var secondSchool = root.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Fish School 2");
            if (secondSchool != null) PrefabUtility.SaveAsPrefabAsset(secondSchool.gameObject, Root + "/Prefabs/FishSchool2.prefab");
            // Complete assembly retains all scene service bindings internally. Independent props
            // deliberately omit scene recovery references; explicit scene assembly assigns them.
            PrefabUtility.SaveAsPrefabAsset(root, Root + "/Prefabs/CompleteDemoAssembly.prefab");
            notes.Add("Saved complete bound assembly plus independent player, exact gloves, fish schools, enemy and props.");
        }
        static void ValidateScene(Scene target, List<string> report)
        {
            report.Add("VALIDATION UTC " + DateTime.UtcNow.ToString("O"));
            var roots = target.GetRootGameObjects();
            var transforms = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            var flow = roots.SelectMany(r => r.GetComponentsInChildren<DemoFlow>(true)).Single();
            report.Add("XR Origin count: " + roots.Sum(r => r.GetComponentsInChildren<XROrigin>(true).Length));
            report.Add("Prop IDs unique: " + (flow.props.Select(p => p.id).Distinct().Count() == flow.props.Length));
            report.Add("Documents: " + transforms.Select(t => t.GetComponent<DemoWorldAction>()).Count(a => a != null && a.fact != null && a.fact.StartsWith("log0")));
            int missing = transforms.Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            report.Add("Missing scripts: " + missing);
            foreach (var r in transforms.Select(t => t.GetComponent<Renderer>()).Where(r => r != null))
                foreach (var m in r.sharedMaterials)
                    if (m == null || m.shader == null || !m.shader.isSupported) report.Add("MATERIAL CHECK: " + r.name + " / " + (m == null ? "null" : m.name));
            Physics.SyncTransforms();
            foreach (var cp in flow.checkpoints)
            {
                var found = Physics.OverlapCapsule(cp.position - Vector3.up * 1.3f, cp.position - Vector3.up * .2f, .24f, flow.config.worldMask, QueryTriggerInteraction.Ignore);
                report.Add("Checkpoint " + cp.name + " head=" + cp.position + " obstructed=" + string.Join(",", found.Select(c => c.name)));
            }
            report.Add("Door real model bound: " + (flow.door != null && flow.door.hinge != null && flow.door.repair != null));
            report.Add("NavMesh triangles: " + NavMesh.CalculateTriangulation().indices.Length / 3);
            report.Add("WEATHER: " + flow.config.weatherStatus);
            report.Add("NOT VERIFIED: Quest 3 stereo, 72 Hz CPU/GPU, hand pose alignment, full controller-only playthrough, submarine corridor clearances, both endings/checkpoint runtime regression.");
        }
        [MenuItem("Tools/Deep Sea Demo/02 Validate Saved Demo")]
        public static void ValidateSaved()
        {
            var old = SceneManager.GetActiveScene(); var loaded = SceneManager.GetSceneByPath(ScenePath); bool opened = !loaded.isLoaded;
            try
            {
                if (opened) loaded = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                var report = new List<string>(); ValidateScene(loaded, report);
                Directory.CreateDirectory(Root + "/Reports"); File.WriteAllLines(Root + "/Reports/Validation.txt", report);
            }
            finally { if (opened && loaded.isLoaded) EditorSceneManager.CloseScene(loaded, true); if (old.isLoaded) SceneManager.SetActiveScene(old); }
        }
        [MenuItem("Tools/Deep Sea Demo/03 Build Windows PCVR")]
        public static void BuildPlayer()
        {
            if (!File.Exists(ScenePath)) throw new FileNotFoundException("Build Demo scene first", ScenePath);
            Directory.CreateDirectory("Builds/DeepSeaDemoPCVR");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, locationPathName = "Builds/DeepSeaDemoPCVR/DeepSeaInvestigation.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            Directory.CreateDirectory(Root + "/Reports");
            File.WriteAllText(Root + "/Reports/PlayerBuild.txt", "Result: " + result.summary.result + "\nErrors: " + result.summary.totalErrors + "\nWarnings: " + result.summary.totalWarnings + "\nBytes: " + result.summary.totalSize + "\nDuration: " + result.summary.totalTime + "\nWeather and Quest headset acceptance remain separate from build success.");
        }
    }
}
#endif
