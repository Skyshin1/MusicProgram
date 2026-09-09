#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.XR.Management;

namespace DeepSeaDemo.Editor
{
    // Read-only, project-owned evidence: the shared Editor.log may belong to another project.
    [InitializeOnLoad]
    internal static class DemoXRLiveDiagnostics
    {
        const string PathName = "Logs/DeepSeaDemo/XR-Live.txt";
        static double next;
        static int samples;
        static DemoXRLiveDiagnostics()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode) { samples = 0; next = 0; }
                if (TargetScene()) Append("Play mode: " + state);
            };
            EditorApplication.update += Tick;
        }
        static bool TargetScene() => SceneManager.GetActiveScene().path == DeepSeaDemoBuilder.Copy1VR;
        static void Tick()
        {
            if (!EditorApplication.isPlaying || !TargetScene() || samples >= 6 || EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + 3; samples++;
            Capture();
        }
        [MenuItem("Tools/Deep Sea Demo/Input/Inspect Current VR State (Read Only)")]
        public static void Capture()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Project=" + Application.dataPath + " Scene=" + SceneManager.GetActiveScene().path + " Playing=" + EditorApplication.isPlaying);
            sb.AppendLine("BuildTarget=" + EditorUserBuildSettings.activeBuildTarget + " SelectedGroup=" + EditorUserBuildSettings.selectedBuildTargetGroup + " TouchPlusLayout=" + InputSystem.ListLayouts().Contains("QuestTouchPlusController"));
            var settings = XRGeneralSettings.Instance;
            var manager = settings != null ? settings.Manager : null;
            var display = manager?.activeLoader?.GetLoadedSubsystem<XRDisplaySubsystem>();
            sb.AppendLine("AutoXR=" + (settings != null && settings.InitManagerOnStart) + " Loader=" + (manager?.activeLoader != null ? manager.activeLoader.name : "NONE") + " DisplayRunning=" + (display != null && display.running));
            sb.AppendLine("Pipeline=" + (GraphicsSettings.currentRenderPipeline != null ? AssetDatabase.GetAssetPath(GraphicsSettings.currentRenderPipeline) : "Built-in"));
            foreach (var camera in Resources.FindObjectsOfTypeAll<Camera>().Where(c => c.gameObject.scene.IsValid() && c.gameObject.scene.isLoaded))
            {
                var data = camera.GetComponent<UniversalAdditionalCameraData>();
                sb.AppendLine("Camera=" + Hierarchy(camera.transform) + " Active=" + camera.isActiveAndEnabled + " Stereo=" + camera.stereoEnabled + " Eyes=" + camera.stereoTargetEye + " AllowXR=" + (data == null || data.allowXRRendering) + " Type=" + (data != null ? data.renderType.ToString() : "N/A") + " Target=" + (camera.targetTexture != null ? camera.targetTexture.name : "Screen") + " Mask=" + camera.cullingMask + " Position=" + camera.transform.position);
            }
            foreach (var input in Resources.FindObjectsOfTypeAll<DemoXRInput>().Where(i => i.gameObject.scene.IsValid() && i.gameObject.scene.isLoaded))
            {
                sb.AppendLine("DemoInput=" + Hierarchy(input.transform) + " Active=" + input.isActiveAndEnabled + " Mode=" + input.editorMode + " Values: see XR-PlayerInput.txt (player Update, not editor input state)");
                foreach (var hand in new[] { input.left, input.right })
                    foreach (var reference in new[] { hand.tracked, hand.stick, hand.trigger })
                    {
                        var action = reference != null ? reference.action : null;
                        sb.AppendLine("Action=" + (action != null ? action.actionMap.name + "/" + action.name : "MISSING") + " Enabled=" + (action != null && action.enabled) + " Controls=" + (action != null ? string.Join(",", action.controls.Select(c => c.path)) : ""));
                    }
            }
            foreach (var behaviour in Resources.FindObjectsOfTypeAll<MonoBehaviour>().Where(b => b != null && b.gameObject.scene.IsValid() && b.gameObject.scene.isLoaded && (b.GetType().Name == "XRInteractionSimulator" || b.GetType().Name == "XRDeviceSimulator")))
                sb.AppendLine("Simulator=" + Hierarchy(behaviour.transform) + " Active=" + behaviour.isActiveAndEnabled);
            foreach (var device in InputSystem.devices)
                if (device is UnityEngine.InputSystem.TrackedDevice)
                    sb.AppendLine("InputDevice=" + device.name + " Layout=" + device.layout + " Enabled=" + device.enabled + " Class=" + device.GetType().Name + " Product=" + device.description.product + " Interface=" + device.description.interfaceName + " Usages=" + string.Join(",", device.usages) + " Controls=" + string.Join(",", device.children.Select(c => c.name)));
            Append(sb.ToString());
        }
        static string Hierarchy(Transform t) => t.parent == null ? t.name : Hierarchy(t.parent) + "/" + t.name;
        static void Append(string text)
        {
            Directory.CreateDirectory("Logs/DeepSeaDemo");
            File.AppendAllText(PathName, DateTime.Now.ToString("s") + " " + text + "\n");
        }
    }
}
#endif
