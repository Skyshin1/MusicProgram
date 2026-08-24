using System;
using System.Linq;
using System.Reflection;
using DeepSeaAI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepSeaAI.Editor
{
    [InitializeOnLoad]
    internal static class MissingStalkerSafeRepair
    {
        private const string TargetScenePath = "Assets/Scenes/1-VR.unity";
        private const string RootName = "Deep Sea Stalker AI";
        private const string ConfigPath = "Assets/DeepSeaAI/Generated/DeepSeaStalkerConfig.asset";
        private const string MaterialPath = "Assets/DeepSeaAI/Generated/ZombieStalker.mat";
        private const string ControllerPath = "Assets/DeepSeaAI/Generated/ZombieStalker.controller";

        static MissingStalkerSafeRepair()
        {
            EditorApplication.delayCall += TryRepair;
        }

        private static void TryRepair()
        {
            if (Application.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryRepair;
                return;
            }

            if (SceneManager.GetActiveScene().path != TargetScenePath)
                return;

            GameObject root = GameObject.Find(RootName);
            if (root == null || root.GetComponentInChildren<DeepSeaStalkerController>(true) != null)
                return;

            Transform routeRoot = root.transform.Cast<Transform>()
                .FirstOrDefault(child => child.name == "Patrol Route P0-P4");
            if (routeRoot == null)
            {
                Debug.LogWarning("[DeepSeaAI] Safe repair skipped: patrol route is missing.", root);
                return;
            }

            Transform[] route = routeRoot.Cast<Transform>()
                .OrderBy(point => point.name)
                .ToArray();
            if (route.Length == 0)
            {
                Debug.LogWarning("[DeepSeaAI] Safe repair skipped: patrol points are missing.", routeRoot);
                return;
            }

            DeepSeaStalkerConfig config =
                AssetDatabase.LoadAssetAtPath<DeepSeaStalkerConfig>(ConfigPath);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            RuntimeAnimatorController controller =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            PlayerRespawnController respawn =
                UnityEngine.Object.FindFirstObjectByType<PlayerRespawnController>(
                    FindObjectsInactive.Include);

            if (config == null || material == null || controller == null || respawn == null)
            {
                Debug.LogWarning("[DeepSeaAI] Safe repair skipped: a generated AI asset is missing.", root);
                return;
            }

            MethodInfo createStalker = typeof(DeepSeaStalkerSceneInstaller).GetMethod(
                "CreateStalker",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (createStalker == null)
            {
                Debug.LogError("[DeepSeaAI] Safe repair failed: CreateStalker was not found.", root);
                return;
            }

            try
            {
                createStalker.Invoke(
                    null,
                    new object[]
                    {
                        root.transform,
                        route,
                        config,
                        material,
                        controller,
                        respawn
                    });
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                Debug.Log("[DeepSeaAI] Missing stalker restored without rebuilding the AI root.", root);
            }
            catch (TargetInvocationException exception)
            {
                Debug.LogException(exception.InnerException ?? exception, root);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, root);
            }
        }
    }
}

