#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace DeepSeaDemo.Editor
{
    public sealed class Demo1VRBuild : IProcessSceneWithReport
    {
        public int callbackOrder => 0;
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null || scene.path != DeepSeaDemoBuilder.Copy1VR) return;
            foreach (var input in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<DemoXRInput>(true)))
                foreach (var simulator in input.simulatorRoots)
                    if (simulator != null) simulator.SetActive(false);
        }
        [MenuItem("Tools/Deep Sea Demo/17 Build Configured 1-VR Windows PCVR")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != DeepSeaDemoBuilder.Copy1VR || SceneManager.sceneCount != 1) throw new InvalidOperationException("Open only the configured 1-VR copy.");
            EditorSceneManager.SaveScene(scene);
            Directory.CreateDirectory("Builds/DeepSea1VRPCVR");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { DeepSeaDemoBuilder.Copy1VR },
                locationPathName = "Builds/DeepSea1VRPCVR/DeepSeaInvestigation.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            File.WriteAllText("Assets/DeepSeaDemo/Reports/1VR-WindowsBuild.txt",
                "Scene: " + DeepSeaDemoBuilder.Copy1VR + "\nResult: " + result.summary.result +
                "\nErrors: " + result.summary.totalErrors + "\nWarnings: " + result.summary.totalWarnings +
                "\nBytes: " + result.summary.totalSize + "\nDuration: " + result.summary.totalTime +
                "\nSimulator disabled in build scene. Windows PCVR only, not a standalone Quest APK.\nHeadset stereo and performance NOT verified by build success.");
            WriteDetails(result);
        }
        [MenuItem("Tools/Deep Sea Demo/18 Export Latest Build Diagnostics")]
        public static void ExportLatest()
        {
            var report = BuildReport.GetLatestReport();
            if (report == null || !report.summary.outputPath.Replace('\\', '/').Contains("DeepSea1VRPCVR"))
                throw new InvalidOperationException("The latest build report is not the 1-VR Demo build.");
            WriteDetails(report);
        }
        static void WriteDetails(BuildReport report)
        {
            var lines = new System.Collections.Generic.List<string> { "Output: " + report.summary.outputPath, "Result: " + report.summary.result };
            foreach (var step in report.steps)
                foreach (var message in step.messages)
                    if (message.type == UnityEngine.LogType.Error || message.type == UnityEngine.LogType.Exception || message.type == UnityEngine.LogType.Warning)
                        lines.Add(message.type + " [" + step.name + "] " + message.content);
            File.WriteAllLines("Assets/DeepSeaDemo/Reports/1VR-WindowsBuild-Diagnostics.txt", lines);
        }
    }
}
#endif
