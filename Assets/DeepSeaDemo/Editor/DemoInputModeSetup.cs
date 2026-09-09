#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace DeepSeaDemo.Editor
{
    public static class DemoInputModeSetup
    {
        [MenuItem("Tools/Deep Sea Demo/Input/Real VR - Quest Link")]
        public static void RealVR() => Configure(DemoXRInput.TestMode.RealVR);
        [MenuItem("Tools/Deep Sea Demo/Input/XR Simulator - Keyboard")]
        public static void Simulator() => Configure(DemoXRInput.TestMode.XRSimulator);
        [MenuItem("Tools/Deep Sea Demo/Input/Desktop UI - Mouse")]
        public static void DesktopUI() => Configure(DemoXRInput.TestMode.DesktopUI);
        static void Configure(DemoXRInput.TestMode mode)
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != DeepSeaDemoBuilder.Copy1VR)
                throw new InvalidOperationException("Exit Play and keep DeepSeaInvestigation_1VR open. No scene is reloaded or discarded.");
            var behaviours = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).Where(b => b != null).ToArray();
            var input = behaviours.OfType<DemoXRInput>().Single(i => i.gameObject.activeInHierarchy);
            var roots = behaviours.Where(b => b.GetType().Name == "XRInteractionSimulator" || b.GetType().Name == "XRDeviceSimulator")
                .Select(b => b.gameObject).Distinct().ToArray();
            Undo.RecordObject(input, "Change Demo input mode");
            input.editorMode = mode; input.simulatorRoots = roots;
            foreach (var root in roots)
            {
                Undo.RecordObject(root, "Change simulator activation");
                root.SetActive(mode == DemoXRInput.TestMode.XRSimulator);
                PrefabUtility.RecordPrefabInstancePropertyModifications(root);
            }
            if (mode == DemoXRInput.TestMode.RealVR)
                foreach (var b in behaviours.Where(b => b.GetType().Name == "SimulatedDeviceLifecycleManager"))
                {
                    // Defense in depth if someone later enables the simulator by hand.
                    var so = new SerializedObject(b); var remove = so.FindProperty("m_RemoveOtherHMDDevices");
                    if (remove != null) { remove.boolValue = false; so.ApplyModifiedProperties(); PrefabUtility.RecordPrefabInstancePropertyModifications(b); }
                }
            foreach (var module in behaviours.OfType<XRUIInputModule>())
            {
                Undo.RecordObject(module, "Set mouse UI mode"); module.enableMouseInput = mode == DemoXRInput.TestMode.DesktopUI;
                PrefabUtility.RecordPrefabInstancePropertyModifications(module);
            }
            EditorUtility.SetDirty(input); PrefabUtility.RecordPrefabInstancePropertyModifications(input);
            EditorSceneManager.MarkSceneDirty(scene);
            Directory.CreateDirectory("Assets/DeepSeaDemo/Reports");
            File.WriteAllText("Assets/DeepSeaDemo/Reports/1VR-InputMode.txt", "Scene: " + scene.path + "\nMode: " + mode +
                "\nSimulator roots: " + roots.Length + "\nActive simulators: " + roots.Count(r => r.activeInHierarchy) +
                "\nConfigured in open scene. NOT auto-saved: preserve user's existing unsaved changes. Save with Ctrl+S.\nHeadset tracking and controller play not verified by this configuration check.");
            Debug.Log("[DeepSeaDemo] Input mode: " + mode + ". Save the scene before Play. Existing unsaved changes preserved.", input);
        }
    }
}
#endif
