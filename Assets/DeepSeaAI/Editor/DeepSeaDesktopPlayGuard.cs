using System.IO;
using Microsoft.Win32;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.XR.Management;

namespace DeepSeaAI.Editor
{
    [InitializeOnLoad]
    internal static class DeepSeaDesktopPlayGuard
    {
        private const string TargetScenePath = "Assets/Scenes/1-VR.unity";
        private const string RestoreKey = "DeepSeaAI.RestoreXRInit";
        private const string ToggleKey = "DeepSeaAI.DesktopPlayGuardEnabled";

        static DeepSeaDesktopPlayGuard()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Tools/Deep Sea AI/Desktop Test Without Headset")]
        private static void Toggle()
        {
            bool enabled = !EditorPrefs.GetBool(ToggleKey, true);
            EditorPrefs.SetBool(ToggleKey, enabled);
            Menu.SetChecked(
                "Tools/Deep Sea AI/Desktop Test Without Headset",
                enabled);
        }

        [MenuItem("Tools/Deep Sea AI/Desktop Test Without Headset", true)]
        private static bool ValidateToggle()
        {
            Menu.SetChecked(
                "Tools/Deep Sea AI/Desktop Test Without Headset",
                EditorPrefs.GetBool(ToggleKey, true));
            return true;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
                PrepareDesktopPlay();
            else if (change == PlayModeStateChange.EnteredEditMode)
                RestoreXRSetting();
        }

        private static void PrepareDesktopPlay()
        {
            if (!EditorPrefs.GetBool(ToggleKey, true) ||
                EditorSceneManager.GetActiveScene().path != TargetScenePath ||
                HasActiveOpenXRRuntime())
            {
                return;
            }

            XRGeneralSettings settings =
                XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(
                    BuildTargetGroup.Standalone);
            if (settings == null || !settings.InitManagerOnStart)
                return;

            SessionState.SetBool(RestoreKey, true);
            settings.InitManagerOnStart = false;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[DeepSeaAI] No active OpenXR runtime was found. XR startup is disabled " +
                "for this editor Play session only; desktop keyboard simulation remains active.");
        }

        private static void RestoreXRSetting()
        {
            if (!SessionState.GetBool(RestoreKey, false))
                return;

            XRGeneralSettings settings =
                XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(
                    BuildTargetGroup.Standalone);
            if (settings != null)
            {
                settings.InitManagerOnStart = true;
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
            SessionState.EraseBool(RestoreKey);
        }

        private static bool HasActiveOpenXRRuntime()
        {
#if UNITY_EDITOR_WIN
            const string keyPath = @"SOFTWARE\Khronos\OpenXR\1";
            string runtimePath = ReadRuntimePath(Registry.CurrentUser, keyPath);
            if (string.IsNullOrWhiteSpace(runtimePath))
                runtimePath = ReadRuntimePath(Registry.LocalMachine, keyPath);
            return !string.IsNullOrWhiteSpace(runtimePath) && File.Exists(runtimePath);
#else
            return true;
#endif
        }

        private static string ReadRuntimePath(RegistryKey root, string keyPath)
        {
            try
            {
                using RegistryKey key = root.OpenSubKey(keyPath);
                return key?.GetValue("ActiveRuntime") as string;
            }
            catch
            {
                return null;
            }
        }
    }
}
