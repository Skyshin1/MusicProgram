#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeepSeaAI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Object = UnityEngine.Object;

namespace DeepSeaDemo.Editor
{
    [InitializeOnLoad]
    internal static class DemoPlaySmoke
    {
        const string Key = "DeepSeaDemo.ExplicitSmoke";
        static List<string> report;
        static int step;
        static double next, deadline;
        static DemoFlow flow;
        static DemoEvidenceSocket socket;
        static DemoProp box;
        static DemoPlaySmoke()
        {
            EditorApplication.playModeStateChanged += Changed;
            EditorApplication.update += Tick;
            if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying)
                EditorApplication.delayCall += () => {
                    report = new List<string> { "INTERRUPTED: scripts reloaded during explicit smoke test. No acceptance result. Re-run with stable compiled scripts." };
                    Finish();
                };
        }
        [MenuItem("Tools/Deep Sea Demo/04 Run API Play Smoke (Not VR Acceptance)")]
        public static void Begin()
        { BeginScene(DeepSeaDemoBuilder.ScenePath); }
        public static void Begin1VR()
        { BeginScene(DeepSeaDemoBuilder.Copy1VR); }
        static void BeginScene(string path)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save or back up open scenes before testing.");
            if (!File.Exists(path)) throw new FileNotFoundException("Build scene first");
            SessionState.SetString(Key + ".Scene", path);
            SessionState.SetString(Key + ".PreviousStart", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
        }
        static void Changed(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                report = new List<string> { "API PLAY SMOKE " + DateTime.UtcNow.ToString("O"), "This is scripted API integration testing, NOT controller interaction, Quest stereo or performance acceptance." };
                step = 0; next = EditorApplication.timeSinceStartup + 3; deadline = EditorApplication.timeSinceStartup + 65;
                Application.logMessageReceived += Log;
            }
            if (change == PlayModeStateChange.EnteredEditMode)
            {
                Application.logMessageReceived -= Log;
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key + ".PreviousStart", ""));
                SessionState.SetBool(Key, false);
            }
        }
        static void Log(string condition, string trace, LogType type)
        { if (type == LogType.Exception || type == LogType.Error) report?.Add("RUNTIME ERROR: " + condition + "\n" + trace); }
        static void Check(bool condition, string name)
        { report.Add((condition ? "PASS: " : "FAIL: ") + name); }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || report == null || EditorApplication.timeSinceStartup < next) return;
            if (EditorApplication.timeSinceStartup > deadline) { report.Add("FAIL: timeout"); Finish(); return; }
            try
            {
                next = EditorApplication.timeSinceStartup + .3;
                switch (step)
                {
                    case 0:
                        flow = Object.FindFirstObjectByType<DemoFlow>();
                        Check(flow != null && flow.ui != null, "Scene bindings and main menu created");
                        if (flow == null) { Finish(); return; }
                        flow.suppressSaveForTests = true;
                        var panel = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Single(c => c.name == "Investigation Panel");
                        float menuDistance = Vector3.Distance(panel.transform.position, flow.player.Camera.transform.position);
                        Check(Mathf.Abs(menuDistance - 1.25f) < .1f, "Menu positioned 1.25m from headset in world space (actual " + menuDistance.ToString("F3") + "m)");
                        Check(Vector3.Dot((panel.transform.position - flow.player.Camera.transform.position).normalized, flow.player.Camera.transform.forward) > .99f, "Menu remains in front of tracked headset");
                        var data = flow.player.Camera.GetUniversalAdditionalCameraData();
                        Check((flow.player.Camera.cullingMask & flow.config.uiMask) != 0, "Main camera includes UI for the post-water rendering pass");
                        report.Add("DIAGNOSTIC panel=" + panel.transform.position + " scale=" + panel.transform.lossyScale + " forward=" + panel.transform.forward + " head=" + flow.player.Camera.transform.position + " forward=" + flow.player.Camera.transform.forward + " layer=" + panel.gameObject.layer);
                        foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                            report.Add("DIAGNOSTIC camera=" + camera.name + " enabled=" + camera.enabled + " mask=" + camera.cullingMask + " type=" + camera.GetUniversalAdditionalCameraData().renderType + " renderer=" + camera.GetUniversalAdditionalCameraData().scriptableRenderer?.GetType().Name + " panelViewport=" + camera.WorldToViewportPoint(panel.transform.position).ToString("F4") + " position=" + camera.transform.position.ToString("F4"));
                        Canvas.ForceUpdateCanvases();
                        foreach (var graphic in panel.GetComponentsInChildren<UnityEngine.UI.Graphic>().Take(4))
                            report.Add("DIAGNOSTIC graphic=" + graphic.name + " scale=" + graphic.transform.lossyScale.ToString("F6") + " rect=" + graphic.rectTransform.rect + " culled=" + graphic.canvasRenderer.cull + " material=" + graphic.materialForRendering.shader.name + " depth=" + graphic.depth);
                        Capture("MainMenu", flow.player.Camera.transform.position, flow.player.Camera.transform.forward);
                        Click("new"); Check(flow.Running && !flow.Paused && flow.State.stage == DemoStage.Platform, "Standard UI button starts platform");
                        flow.Pause(); Click("settings"); Click("back"); Click("resume");
                        Check(!flow.Paused, "Pause/settings/back/resume standard Button callback chain");
                        Check(flow.props.All(p => p.Grab != null), "All item XR Grab components awakened");
                        flow.enemy.enabled = false;
                        break;
                    case 1:
                        Read("log01"); Read("alarm"); flow.Record("flashlight");
                        Object.FindObjectsByType<DemoWorldAction>(FindObjectsSortMode.None).First(a => a.kind == DemoActionKind.Equip).Interact(false);
                        Check(flow.State.stage == DemoStage.Dive && flow.Equipped, "Equipment prerequisites advance to diving");
                        flow.Record("locktool"); Check(flow.State.stage == DemoStage.Submarine, "Tool advances submarine objective");
                        flow.door.repair.AdjustRepairProgress(.5f); flow.door.repair.AdjustRepairProgress(-.1f);
                        Check(Mathf.Abs(flow.door.repair.RepairProgress - .4f) < .001f, "QTE failure progress regression API");
                        flow.door.repair.AdjustRepairProgress(1); next = EditorApplication.timeSinceStartup + 4;
                        break;
                    case 2:
                        Check(flow.State.doorOpen, "Repair opens actual Door and saves chapter snapshot");
                        flow.Record("blackbox"); Check(flow.State.stage == DemoStage.Return && flow.State.engineStarted, "Black box triggers return and engine once");
                        flow.ui.Command("close");
                        box = flow.props.First(p => p.id == "blackbox"); socket = Object.FindFirstObjectByType<DemoEvidenceSocket>();
                        var manager = Object.FindFirstObjectByType<XRInteractionManager>();
                        manager.SelectEnter(socket, (IXRSelectInteractable)box.Grab);
                        Check(socket.hasSelection, "Black-box socket selects correct evidence");
                        next = EditorApplication.timeSinceStartup + 8;
                        break;
                    case 3:
                        Check(flow.State.Has("parsed"), "Playback completion opens analysis without optional evidence");
                        flow.ChooseEnding(false); next = EditorApplication.timeSinceStartup + .5;
                        break;
                    case 4:
                        Check(flow.State.ending == "preserved", "Preserve ending");
                        Capture("PreserveEnding", flow.player.Camera.transform.position, flow.player.Camera.transform.forward);
                        flow.NewGame(); flow.State.Add("blackbox"); flow.State.Add("parsed"); flow.ChooseEnding(true);
                        next = EditorApplication.timeSinceStartup + 6;
                        break;
                    case 5:
                        Check(flow.State.ending == "uploaded", "Upload ending uses game-only state");
                        flow.NewGame(); flow.Record("log01"); flow.Record("alarm"); flow.Record("flashlight"); flow.Record("suit"); flow.Record("locktool");
                        flow.Retry(); next = EditorApplication.timeSinceStartup + 1;
                        break;
                    case 6:
                        Check(flow.State.stage == DemoStage.Submarine && !flow.Busy && !flow.Paused, "Retry restores acquired-tool checkpoint");
                        Check(flow.player.GetComponent<PlayerOxygen>().NormalizedOxygen > .99f, "Oxygen refilled on retry");
                        Check(!flow.door.repair.IsRepaired, "Door resets to checkpoint state");
                        Capture("SeabedOverview", new Vector3(15, -12, -5), new Vector3(0, -22, 15) - new Vector3(15, -12, -5));
                        Finish(); return;
                }
                step++;
            }
            catch (Exception ex) { report.Add("FAIL: " + ex); Finish(); }
        }
        static void Read(string fact)
        {
            Object.FindObjectsByType<DemoWorldAction>(FindObjectsSortMode.None).First(a => a.fact == fact).Interact(false);
            Check(flow.State.Has(fact), "World interaction registered " + fact);
            Click("close");
        }
        static void Click(string command)
        {
            var button = Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).Single(b => b.name == "Button " + command);
            var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { button = UnityEngine.EventSystems.PointerEventData.InputButton.Left };
            UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerEnterHandler);
            UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
        }
        static void Capture(string name, Vector3 position, Vector3 forward)
        {
            if (name != "SeabedOverview" && SessionState.GetString(Key + ".Scene", "") == DeepSeaDemoBuilder.Copy1VR)
            {
                // A cloned camera omits the UI overlay stack, so it cannot validate menus.
                var destination = new RenderTexture(1280, 800, 24);
                var oldActive = RenderTexture.active;
                var pixels = new Texture2D(1280, 800, TextureFormat.RGB24, false);
                try
                {
                    Canvas.ForceUpdateCanvases();
                    void CameraRendered(UnityEngine.Rendering.ScriptableRenderContext context, Camera c) => report?.Add("RENDER " + name + ": " + c.name);
                    UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += CameraRendered;
                    try {
                    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(flow.player.Camera,
                        new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = destination });
                    } finally { UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= CameraRendered; }
                    RenderTexture.active = destination; pixels.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); pixels.Apply();
                    Directory.CreateDirectory("Logs/DeepSeaDemo/Previews"); File.WriteAllBytes("Logs/DeepSeaDemo/Previews/" + name + ".png", pixels.EncodeToPNG());
                }
                finally { RenderTexture.active = oldActive; destination.Release(); Object.DestroyImmediate(destination); Object.DestroyImmediate(pixels); }
                return;
            }
            var go = new GameObject("Smoke Capture Camera"); var camera = go.AddComponent<Camera>(); camera.CopyFrom(flow.player.Camera);
            camera.stereoTargetEye = StereoTargetEyeMask.None; camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward));
            camera.cullingMask = -1;
            var rt = new RenderTexture(1280, 800, 24); var old = RenderTexture.active;
            var texture = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt; texture.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); texture.Apply();
                Directory.CreateDirectory("Logs/DeepSeaDemo/Previews"); File.WriteAllBytes("Logs/DeepSeaDemo/Previews/" + name + ".png", texture.EncodeToPNG());
            }
            finally { RenderTexture.active = old; camera.targetTexture = null; rt.Release(); Object.DestroyImmediate(texture); Object.DestroyImmediate(rt); Object.DestroyImmediate(go); }
        }
        static void Finish()
        {
            Application.logMessageReceived -= Log;
            Directory.CreateDirectory("Assets/DeepSeaDemo/Reports");
            string suffix = SessionState.GetString(Key + ".Scene", "") == DeepSeaDemoBuilder.Copy1VR ? "1VR-PlaySmoke.txt" : "PlaySmoke.txt";
            File.WriteAllLines("Assets/DeepSeaDemo/Reports/" + suffix, report);
            report = null; EditorApplication.isPlaying = false;
        }
    }
}
#endif
