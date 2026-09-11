#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DeepSeaAI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeepSeaDemo.Editor
{
    [InitializeOnLoad]
    internal static partial class DemoGameplayRegression
    {
        const string PlayKey = "DeepSeaDemo.GameplayRegression";
        static int step;
        static double next;
        static StringBuilder playReport;
        static DemoFlow playFlow;
        static DemoGameplayRegression()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(PlayKey, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    step = 0; next = EditorApplication.timeSinceStartup + 4;
                    playReport = new StringBuilder();
                }
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    RestoreWaterlineOptions();
                    EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(PlayKey + "Previous", ""));
                    SessionState.SetBool(PlayKey, false);
                    SessionState.SetBool(PlayKey + "Environment", false);
                    SessionState.SetBool(PlayKey + "Qte", false);
                    SessionState.SetBool(PlayKey + "Surface", false);
                    SessionState.SetBool(PlayKey + "Waterline", false);
                    SessionState.SetBool(PlayKey + "Interaction", false);
                    SessionState.SetBool(PlayKey + "Boarding", false);
                    SessionState.SetBool(PlayKey + "Repair", false);
                }
            };
            EditorApplication.update += TickPlay;
        }

        public static void BeginEnvironmentPlay()
        {
            SessionState.SetBool(PlayKey + "Environment", true);
            BeginPlay();
        }
        public static void BeginQtePlay()
        {
            SessionState.SetBool(PlayKey + "Qte", true);
            BeginPlay();
        }
        public static void BeginSurfacePlay()
        {
            SessionState.SetBool(PlayKey + "Surface", true); BeginPlay();
        }
        static void TickSurface()
        {
            if (step == 0)
            {
                playFlow = Object.FindFirstObjectByType<DemoFlow>(); playFlow.suppressSaveForTests = true;
                playFlow.NewGame(); playFlow.SetPaused(true); playFlow.ui.ShowHUD();
                float surface = Shader.GetGlobalFloat("_UnderwaterSurfaceY");
                var camera = playFlow.player.Camera.transform;
                playFlow.player.transform.position += new Vector3(0,surface+5,12)-camera.position;
                camera.rotation = Quaternion.LookRotation(Vector3.down,Vector3.forward);
                depthProbe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                depthProbe.GetComponent<Collider>().enabled = false;
                depthProbe.GetComponent<Renderer>().material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) {color=Color.white};
                depthProbe.transform.position = new Vector3(0,surface-1,12); depthProbe.transform.localScale = new Vector3(3,.1f,3);
            }
            if (step == 1) ScreenCapture.CaptureScreenshot("Logs/DeepSeaDemo/Surface-Shallow.png");
            if (step == 2)
            {
                float surface = Shader.GetGlobalFloat("_UnderwaterSurfaceY");
                depthProbe.transform.position = new Vector3(0,surface-12,12); depthProbe.transform.localScale = new Vector3(8,.1f,8);
            }
            if (step == 3)
            {
                ScreenCapture.CaptureScreenshot("Logs/DeepSeaDemo/Surface-Deep.png");
                foreach (var water in Object.FindObjectsByType<AbstractOcclusion.WebGpuWater.WaterVolume>(FindObjectsSortMode.None))
                {
                    playReport.AppendLine("WATER " + EditorJsonUtility.ToJson(water));
                    foreach (var renderer in water.GetComponentsInChildren<Renderer>())
                    {
                        var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
                        playReport.AppendLine("SURFACE " + renderer.name+ " material="+renderer.sharedMaterial+" on="+renderer.enabled+" off="+renderer.forceRenderingOff+" layer="+renderer.gameObject.layer+" fog="+block.GetFloat("_WaterFogEnabled")+" extinction="+block.GetVector("_WaterExtinction"));
                    }
                }
            }
            if (step == 4)
            {
                float shallow = 0, deep = 0;
                foreach (string label in new[] {"Shallow","Deep"})
                {
                    var tex = new Texture2D(2,2); tex.LoadImage(File.ReadAllBytes("Logs/DeepSeaDemo/Surface-"+label+".png"));
                    float value = tex.GetPixel(tex.width/2,(int)(tex.height*.55f)).grayscale;
                    if (label == "Shallow") shallow = value; else deep = value;
                    playReport.AppendLine("PIXEL "+label+"="+value);
                    Object.DestroyImmediate(tex);
                }
                if (deep > .12f || deep >= shallow * .5f) throw new Exception("Deep object remains too visible through the surface");
                playReport.AppendLine("PASS deep target is obscured from above while shallow target remains visible.");
                VerifyAudioBank(); FinishPlay(); return;
            }
            step++;
        }
        static void VerifyAudioBank()
        {
            var bank = DemoAudioBank.Default;
            if (bank == null) throw new Exception("Missing audio bank");
            foreach (DemoSound sound in Enum.GetValues(typeof(DemoSound)))
            {
                var cue = bank.Get(sound); if (cue?.clip == null) throw new Exception("Missing audio slot "+sound);
                var samples = new float[cue.clip.samples]; cue.clip.GetData(samples,0);
                float peak = samples.Max(v=>Mathf.Abs(v));
                if (peak < .01f || peak >= .99f || Mathf.Abs(samples[0]) > .005f || Mathf.Abs(samples[samples.Length-1]) > .005f) throw new Exception("Invalid placeholder waveform "+sound);
                playReport.AppendLine("AUDIO "+sound+" seconds="+cue.clip.length+" peak="+peak);
            }
            playFlow.SetPaused(false);
            var emitter = playFlow.door.gameObject.GetComponent<DemoAudioEmitter>() ?? playFlow.door.gameObject.AddComponent<DemoAudioEmitter>();
            emitter.Play(DemoSound.HatchOpen);
            if (emitter.PlayedCount != 1 || !emitter.GetComponents<AudioSource>().Any(a=>a.isPlaying)) throw new Exception("Audio cue did not play");
            playReport.AppendLine("PASS all audio slots have nonclipping, non-silent PCM and a live spatial AudioSource plays the selected cue.");
            var check = Object.FindFirstObjectByType<RepairSkillCheckController>();
            check.Tick(playFlow.door.repair,true,true);
            typeof(RepairSkillCheckController).GetMethod("BeginCheck",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(check,new object[]{true});
            var toolAudio = check.GetComponent<DemoAudioEmitter>();
            if (toolAudio.LastPlayed != DemoSound.QteStart) throw new Exception("QTE start sound not connected");
            typeof(RepairSkillCheckController).GetField("perfectCenterDegrees",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(check,0f);
            typeof(RepairSkillCheckController).GetField("checkStartedAt",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(check,Time.time);
            typeof(RepairSkillCheckController).GetMethod("ResolveCheck",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(check,null);
            if (toolAudio.LastPlayed != DemoSound.RepairSuccess) throw new Exception("Repair success sound not connected");
            typeof(RepairSkillCheckController).GetMethod("Fail",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(check,new object[]{true});
            if (toolAudio.LastPlayed != DemoSound.RepairFailure) throw new Exception("Repair timeout sound not connected");
            int count = emitter.PlayedCount;
            playFlow.door.repair.Repair("StandardRepairTool",playFlow.door.repair.RepairSeconds);
            if (emitter.PlayedCount != count+1 || emitter.LastPlayed != DemoSound.RepairComplete) throw new Exception("Completed repair sound not connected");
            typeof(DemoDoor).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(playFlow.door,null);
            if (emitter.LastPlayed != DemoSound.HatchOpen) throw new Exception("Door opening sound not connected");
            count = emitter.PlayedCount;
            typeof(DemoDoor).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(playFlow.door,null);
            if (emitter.PlayedCount != count) throw new Exception("Door opening sound repeated per frame");
            playFlow.door.Restore(true);
            if (emitter.PlayedCount != count) throw new Exception("Loading open-door state played repair audio");
            var fish = Object.FindFirstObjectByType<DeepSeaFishAI>();
            typeof(DeepSeaFishAI).GetMethod("BeginFlee",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(fish,new object[]{fish.transform.position+Vector3.forward});
            if (!fish.IsFleeing || !fish.GetComponent<AudioSource>().isPlaying) throw new Exception("Fish flee sound not connected");
            playReport.AppendLine("PASS QTE start/success/timeout, repair completion, door opening once and fish flee all trigger playback; state restore is silent.");
        }
        static RepairSkillCheckController qteCheck;
        static float qteStarted;
        static Transform qteRing;
        static void TickQte()
        {
            next = EditorApplication.timeSinceStartup + .05;
            if (step == 0)
            {
                playFlow = Object.FindFirstObjectByType<DemoFlow>();
                playFlow.suppressSaveForTests = true;
                playFlow.NewGame(); playFlow.SetPaused(true); playFlow.ui.ShowHUD();
                qteCheck = Object.FindFirstObjectByType<RepairSkillCheckController>();
                var camera = playFlow.player.Camera.transform;
                Vector3 center = playFlow.door.repair.transform.position + Vector3.up * 1.15f;
                playFlow.player.transform.position += center + Vector3.back * 2 - camera.position;
                camera.rotation = Quaternion.LookRotation(center - camera.position);
                qteCheck.Tick(null,false,true);
                if (qteCheck.IsCheckActive) throw new Exception("Proximity alone started QTE");
                qteStarted = Time.time;
                qteCheck.Tick(playFlow.door.repair,true,true);
                step++; return;
            }
            if (step == 1)
            {
                qteCheck.Tick(playFlow.door.repair,true,true);
                if (!qteCheck.IsCheckActive)
                {
                    if (Time.time - qteStarted > 5) throw new Exception("Held repair never started QTE");
                    return;
                }
                float delay = Time.time - qteStarted;
                if (delay < 2.45f) throw new Exception("QTE appeared before repair delay");
                qteRing = GameObject.Find("Repair Skill Check Ring").transform;
                foreach (var line in qteRing.GetComponentsInChildren<LineRenderer>())
                    if ((playFlow.config.uiMask.value & (1 << line.gameObject.layer)) == 0 || line.sharedMaterial.renderQueue != 3000)
                        throw new Exception("QTE renderer not in after-water UI queue");
                playReport.AppendLine("PASS held repair starts QTE after " + delay + " seconds; approaching alone does not.");
                playReport.AppendLine("Ring=" + qteRing.position + " camera=" + playFlow.player.Camera.transform.position + " underwater=" + Shader.GetGlobalFloat("_CameraUnderwater"));
                step++; next = EditorApplication.timeSinceStartup + .3; return;
            }
            if (step == 2)
            {
                ScreenCapture.CaptureScreenshot("Logs/DeepSeaDemo/Hatch-QTE.png");
                step++; next = EditorApplication.timeSinceStartup + .4; return;
            }
            var tex = new Texture2D(2,2); tex.LoadImage(File.ReadAllBytes("Logs/DeepSeaDemo/Hatch-QTE.png"));
            int green = 0, gold = 0;
            for (int y = tex.height / 4; y < tex.height * 3 / 4; y++)
                for (int x = tex.width / 4; x < tex.width * 3 / 4; x++)
                {
                    Color c = tex.GetPixel(x,y);
                    if (c.g > .45f && c.g > c.r * 1.7f && c.g > c.b * 1.7f) green++;
                    if (c.r > .5f && c.g > .35f && c.b < .3f) gold++;
                }
            Object.DestroyImmediate(tex);
            if (green < 15 || gold < 5) throw new Exception("QTE is not visibly rendered underwater: green=" + green + " gold=" + gold);
            playReport.AppendLine("PASS underwater QTE screenshot contains visible success arc and needle: green=" + green + " gold=" + gold);
            qteCheck.Tick(null,false,true);
            if (qteCheck.IsCheckActive || qteRing.gameObject.activeSelf) throw new Exception("Released repair did not hide QTE");
            playReport.AppendLine("PASS releasing repair hides the QTE.");
            FinishPlay();
        }
        static GameObject depthProbe;
        static readonly float[] testDepths = { -2, 1, 5, 12, 20 };
        static void TickEnvironment()
        {
            if (step == 0)
            {
                playFlow = Object.FindFirstObjectByType<DemoFlow>();
                playFlow.suppressSaveForTests = true;
                playFlow.NewGame(); playFlow.SetPaused(true); playFlow.ui.ShowHUD();
                depthProbe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                depthProbe.GetComponent<Collider>().enabled = false;
                depthProbe.GetComponent<Renderer>().material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = Color.white };
                depthProbe.transform.localScale = Vector3.one * .8f;
            }
            if (step < 10)
            {
                int sample = step / 2;
                var cam = playFlow.player.Camera.transform;
                if (step % 2 == 0)
                {
                    float surface = Shader.GetGlobalFloat("_UnderwaterSurfaceY");
                    playFlow.player.transform.position += new Vector3(0, surface - testDepths[sample], 6) - cam.position;
                    cam.rotation = Quaternion.identity;
                    depthProbe.transform.position = cam.position + Vector3.forward;
                }
                else
                {
                    playReport.AppendLine("DEPTH " + testDepths[sample] + " camera=" + cam.position + " fog=" + Shader.GetGlobalFloat("_UnderwaterFogArmed") + " depthEnabled=" + Shader.GetGlobalFloat("_DepthDarkenEnabled") + " lantern=" + Shader.GetGlobalFloat("_WaterSonarLanternEnabled"));
                    ScreenCapture.CaptureScreenshot("Logs/DeepSeaDemo/Depth-" + sample + ".png");
                }
                step++; return;
            }
            if (step == 10)
            {
                float previous = 2;
                for (int i = 0; i < testDepths.Length; i++)
                {
                    var tex = new Texture2D(2,2); tex.LoadImage(File.ReadAllBytes("Logs/DeepSeaDemo/Depth-" + i + ".png"));
                    float sum = 0; int n = 0;
                    for (int y = (int)(tex.height * .58f); y < tex.height * .62f; y++)
                        for (int x = (int)(tex.width * .48f); x < tex.width * .52f; x++) { sum += tex.GetPixel(x,y).grayscale; n++; }
                    float luminance = sum / n;
                    Object.DestroyImmediate(tex);
                    playReport.AppendLine("PIXEL LUMINANCE depth=" + testDepths[i] + " value=" + luminance);
                    if (luminance >= previous - .005f) throw new Exception("Depth darkness did not increase at " + testDepths[i]);
                    previous = luminance;
                }
                if (previous < .02f || previous > .18f) throw new Exception("Deep-water visibility floor is outside the faint-but-readable range");
                playReport.AppendLine("PASS actual rendered proximity surface darkens progressively and retains a faint deep-water silhouette.");
                foreach (var indicator in Object.FindObjectsByType<DeepSeaStalkerAlertIndicator>(FindObjectsSortMode.None))
                    if (indicator.enabled || indicator.transform.Find("AI Alert Indicator") != null) throw new Exception("Enemy alert visual still present");
                playReport.AppendLine("PASS enemy alert symbols and lights are absent.");
                var tool = Object.FindFirstObjectByType<RepairTool>();
                var trigger = playFlow.door.repair.GetComponent<BoxCollider>();
                Vector3 targetPoint = trigger.bounds.center;
                var find = typeof(RepairTool).GetMethod("FindNearestTarget", BindingFlags.Instance | BindingFlags.NonPublic);
                foreach (var offset in new[] { Vector3.zero, Vector3.up * .25f, Vector3.right * .2f, Vector3.forward * .2f })
                {
                    tool.transform.position = targetPoint + offset; Physics.SyncTransforms();
                    if (find.Invoke(tool,null) as RepairableFacility != playFlow.door.repair) throw new Exception("Visible hatch repair point missed " + offset);
                }
                // Dense colliders used to truncate the 16-entry repair query.
                var clutter = new GameObject("Repair query regression colliders");
                for (int i = 0; i < 40; i++)
                {
                    var go = new GameObject("Collider " + i); go.transform.SetParent(clutter.transform);
                    go.transform.position = targetPoint; go.AddComponent<SphereCollider>().radius = .04f;
                }
                tool.transform.position = targetPoint; Physics.SyncTransforms();
                if (find.Invoke(tool,null) as RepairableFacility != playFlow.door.repair) throw new Exception("Dense hatch query lost repair target");
                Object.DestroyImmediate(clutter);
                playReport.AppendLine("PASS hatch top and surrounding contact points; target survives more than 40 overlapping colliders.");
                // Exercise XRI selection and Activate through the real repair update.
                playFlow.SetPaused(false);
                var grab = tool.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                var interactor = playFlow.player.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor>().First();
                var manager = interactor.interactionManager;
                // Inject the selection lifecycle; no tracked hand/raycast exists in
                // this editor run. Real controller input still needs headset QA.
                manager.SelectEnterUnconditionally((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)interactor, grab);
                grab.activated.Invoke(new UnityEngine.XR.Interaction.Toolkit.ActivateEventArgs { interactorObject = interactor, interactableObject = grab });
                tool.transform.position = targetPoint; Physics.SyncTransforms();
                typeof(RepairTool).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tool,null);
                if (tool.CurrentTarget != playFlow.door.repair || playFlow.door.repair.RepairProgress <= 0) throw new Exception("Held Activate did not start hatch repair");
                grab.deactivated.Invoke(new UnityEngine.XR.Interaction.Toolkit.DeactivateEventArgs { interactorObject = interactor, interactableObject = grab });
                manager.SelectExit((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)interactor,grab);
                playReport.AppendLine("PASS XRI grab + Activate starts repair progress on the hatch.");
                var check = tool.GetComponent<RepairSkillCheckController>();
                check.Tick(playFlow.door.repair,true,true);
                typeof(RepairSkillCheckController).GetMethod("BeginCheck", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(check,new object[]{true});
                float center = (float)typeof(RepairSkillCheckController).GetField("perfectCenterDegrees", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(check);
                typeof(RepairSkillCheckController).GetField("checkStartedAt", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(check,Time.time-center/270f);
                typeof(RepairSkillCheckController).GetMethod("ResolveCheck", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(check,null);
                if (check.IsCheckActive || check.RepairSpeedMultiplier <= 1) throw new Exception("Repair QTE success did not resume repair");
                playReport.AppendLine("PASS repair QTE success resumes progress.");
                playFlow.SetPaused(true);
                tool.transform.position = new Vector3(0,0,0);
                playFlow.door.repair.Repair(tool.ToolId,playFlow.door.repair.RepairSeconds);
                playFlow.door.enabled = true;
                step++; next = EditorApplication.timeSinceStartup + playFlow.door.seconds + 1; return;
            }
            if (!playFlow.State.doorOpen || playFlow.door.doorwayBlocker.enabled) throw new Exception("Repaired hatch did not open and clear its blocker");
            playReport.AppendLine("PASS completed repair opens hatch and removes the doorway blocker.");
            FinishPlay();
        }
        [MenuItem("Tools/Deep Sea Demo/Validation/Test New Game and Four Bites (Current Scene)")]
        public static void BeginPlay()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != DeepSeaDemoBuilder.Copy1VR) throw new InvalidOperationException("Open the 1VR copy in Edit Mode first.");
            SessionState.SetString(PlayKey + "Previous", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            EditorSceneManager.playModeStartScene = null;
            SessionState.SetBool(PlayKey, true);
            EditorApplication.isPlaying = true;
        }

        static void TickPlay()
        {
            if (!SessionState.GetBool(PlayKey, false) || !EditorApplication.isPlaying || playReport == null || EditorApplication.timeSinceStartup < next) return;
            try
            {
                next = EditorApplication.timeSinceStartup + 1.2;
                if (SessionState.GetBool(PlayKey + "Waterline", false)) { TickWaterline(); return; }
                if (SessionState.GetBool(PlayKey + "Repair", false)) { TickRepair(); return; }
                if (SessionState.GetBool(PlayKey + "Boarding", false)) { TickBoarding(); return; }
                if (SessionState.GetBool(PlayKey + "Interaction", false)) { TickInteraction(); return; }
                if (SessionState.GetBool(PlayKey + "Surface", false)) { TickSurface(); return; }
                if (SessionState.GetBool(PlayKey + "Qte", false)) { TickQte(); return; }
                if (SessionState.GetBool(PlayKey + "Environment", false)) { TickEnvironment(); return; }
                if (step == 0)
                {
                    playFlow = Object.FindFirstObjectByType<DemoFlow>();
                    playFlow.suppressSaveForTests = true;
                    Snapshot("Before New Game");
                    ScreenCapture.CaptureScreenshot("Logs/DeepSeaDemo/NewGame-Before.png");
                }
                else if (step == 1) { playFlow.NewGame(); Snapshot("Immediately after New Game"); }
                else if (step == 2)
                {
                    Snapshot("After New Game 1.2s");
                    var orbit = playFlow.player.Camera.GetComponent<AbstractOcclusion.WebGpuWater.OrbitCamera>();
                    if (orbit != null && orbit.enabled) throw new Exception("Water OrbitCamera still owns XR camera");
                    var fade = Object.FindObjectsByType<CanvasGroup>(FindObjectsSortMode.None).First(g=>g.name=="Safety Fade");
                    if (fade.alpha > .001f) throw new Exception("New Game caused safety blackout");
                    if (Vector3.Distance(playFlow.player.Camera.transform.position,playFlow.checkpoints[0].position)>1f) throw new Exception("New Game head escaped spawn");
                    playReport.AppendLine("PASS XR spawn remains stable and New Game safety fade is zero.");
                    ScreenCapture.CaptureScreenshot("Logs/DeepSeaDemo/NewGame-After.png");
                }
                else if (step == 3) { Snapshot("After New Game 2.4s"); playFlow.MainMenu(); }
                else if (step == 4)
                {
                    Snapshot("Main Menu at same location");
                    playFlow.NewGame(); playFlow.SetPaused(true);
                }
                else if (step >= 5 && step <= 8)
                {
                    var player = playFlow.player.GetComponent<PlayerRespawnController>();
                    bool hit = player.ReceiveBite(playFlow.enemy.transform);
                    playReport.AppendLine("BITE " + (step - 4) + " accepted=" + hit + " remaining=" + player.BitesRemaining + " respawning=" + player.IsProtected);
                    if (!hit || player.BitesRemaining != 8 - step || (step < 8 && player.IsProtected)) throw new Exception("Four-bite runtime check failed");
                }
                else if (step == 9)
                {
                    var player = playFlow.player.GetComponent<PlayerRespawnController>();
                    if (player.BitesRemaining != 4) throw new Exception("Respawn did not restore health");
                    playReport.AppendLine("PASS fourth bite triggers respawn; respawn restores 4/4 health.");
                    FinishPlay(); return;
                }
                step++;
            }
            catch (Exception ex) { playReport.AppendLine("FAIL " + ex); FinishPlay(); }
        }

        static void Snapshot(string label)
        {
            playReport.AppendLine("--- " + label);
            playReport.AppendLine("Head=" + playFlow.player.Camera.transform.position + " paused=" + playFlow.Paused + " ambient=" + RenderSettings.ambientIntensity + " ambientColor=" + RenderSettings.ambientLight + " fog=" + RenderSettings.fog);
            for(var t = playFlow.player.Camera.transform; t != null; t = t.parent)
                playReport.AppendLine("POSE " + t.name + " world=" + t.position + " local=" + t.localPosition + " components=" + string.Join(",",t.GetComponents<Component>().Select(c=>c.GetType().Name)));
            playReport.AppendLine("Origin target="+playFlow.player.Origin.name);
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.enabled) playReport.AppendLine("Light=" + light.name + " intensity=" + light.intensity);
            foreach (var group in Object.FindObjectsByType<CanvasGroup>(FindObjectsSortMode.None))
                if (group.name.Contains("Fade")) playReport.AppendLine("Fade=" + group.name + " alpha=" + group.alpha);
            var stack = VolumeManager.instance.stack;
            playReport.AppendLine("Post exposure=" + stack.GetComponent<UnityEngine.Rendering.Universal.ColorAdjustments>()?.postExposure.value + " Vignette=" + stack.GetComponent<UnityEngine.Rendering.Universal.Vignette>()?.intensity.value);
            var safety = playFlow.player.GetComponent<DemoPlayerSafety>();
            foreach (var c in Physics.OverlapSphere(playFlow.player.Camera.transform.position, safety.headRadius, playFlow.config.worldMask, QueryTriggerInteraction.Ignore))
                playReport.AppendLine("Head overlaps " + c.name + " bounds=" + c.bounds);
        }

        static void FinishPlay()
        {
            if (SessionState.GetBool(PlayKey + "Repair", false)) File.WriteAllText("Logs/DeepSeaDemo/Repair-Reach-Play.txt", playReport.ToString());
            if (SessionState.GetBool(PlayKey + "Boarding", false)) File.WriteAllText("Logs/DeepSeaDemo/Boarding-Play.txt", playReport.ToString());
            if (SessionState.GetBool(PlayKey + "Interaction", false)) File.WriteAllText("Logs/DeepSeaDemo/Interaction-Play.txt", playReport.ToString());
            if (SessionState.GetBool(PlayKey + "Surface", false)) File.WriteAllText("Logs/DeepSeaDemo/Surface-Audio-Play.txt", playReport.ToString());
            if (SessionState.GetBool(PlayKey + "Qte", false)) File.WriteAllText("Logs/DeepSeaDemo/QTE-Play.txt", playReport.ToString());
            if (SessionState.GetBool(PlayKey + "Environment", false)) File.WriteAllText("Logs/DeepSeaDemo/Environment-Play.txt", playReport.ToString());
            File.WriteAllText("Logs/DeepSeaDemo/Gameplay-Play.txt", playReport.ToString());
            playReport = null;
            EditorApplication.isPlaying = false;
        }

        [MenuItem("Tools/Deep Sea Demo/Validation/Test Held Sonar and Bite Rules (Isolated)")]
        public static void Verify()
        {
            var scene = EditorSceneManager.NewPreviewScene(); var sb = new StringBuilder();
            var oldManager = SonarRevealManager.Instance;
            var root = new GameObject("Isolated gameplay tests"); root.SetActive(false); SceneManager.MoveGameObjectToScene(root, scene);
            SonarRevealManager manager = null; Renderer renderer = null;
            try
            {
                var player = root.AddComponent<PlayerRespawnController>();
                var register = typeof(PlayerRespawnController).GetMethod("RegisterBite", BindingFlags.NonPublic | BindingFlags.Instance);
                player.ResetBiteHealth();
                for (int i = 0; i < 4; i++)
                {
                    float now = 10 + i * 1.2f;
                    if (!(bool)register.Invoke(player, new object[] { now }) || player.BitesRemaining != 3 - i) throw new Exception("Bite count failed");
                    if ((bool)register.Invoke(player, new object[] { now + .01f })) throw new Exception("Duplicate bite accepted during cooldown");
                }
                if ((bool)register.Invoke(player, new object[] { 30f })) throw new Exception("Damage accepted after lethal bite");
                player.ResetBiteHealth(); if (player.BitesRemaining != 4) throw new Exception("Health reset failed");
                sb.AppendLine("PASS four accepted bites, duplicate-hit cooldown, lethal lockout and reset.");
                manager = root.AddComponent<SonarRevealManager>();
                typeof(SonarRevealManager).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);
                var prop = GameObject.CreatePrimitive(PrimitiveType.Cube); SceneManager.MoveGameObjectToScene(prop, scene);
                renderer = prop.GetComponent<Renderer>();
                SonarRevealManager.RevealRenderer(renderer, 3);
                if (!SonarRevealManager.ActiveRenderers.Contains(renderer)) throw new Exception("Loose prop not revealed");
                SonarRevealManager.SetSuppressed(renderer, true);
                if (SonarRevealManager.ActiveRenderers.Contains(renderer)) throw new Exception("Grab did not clear existing outline");
                SonarRevealManager.RevealRenderer(renderer, 3);
                if (SonarRevealManager.ActiveRenderers.Contains(renderer)) throw new Exception("Held prop revealed");
                typeof(SonarRevealManager).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);
                if (Shader.GetGlobalInt("_WaterSonarExcludedCount") != 1) throw new Exception("Water sonar exclusion not uploaded");
                SonarRevealManager.SetSuppressed(renderer, false);
                SonarRevealManager.RevealRenderer(renderer, 3);
                if (!SonarRevealManager.ActiveRenderers.Contains(renderer)) throw new Exception("Released prop not revealed");
                typeof(SonarRevealManager).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);
                if (Shader.GetGlobalInt("_WaterSonarExcludedCount") != 0) throw new Exception("Released prop keeps water exclusion");
                sb.AppendLine("PASS loose / held / released sonar registration, immediate removal, water-exclusion upload and cleanup.");
                var style = prop.AddComponent<SonarRevealStyle>(); style.Configure(new Color(1, .035f, .02f, 1), 1.5f);
                renderer.SetPropertyBlock(null);
                var pass = typeof(SonarWhiteOutlineRendererFeature).GetNestedType("OutlinePass", BindingFlags.NonPublic);
                var color = (Color)pass.GetMethod("ResolveOutlineColor", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { renderer });
                if (color.r < .99f || color.g > .04f || color.b > .03f) throw new Exception("Enemy outline lost red style");
                sb.AppendLine("PASS enemy outline remains red after property-block replacement.");
                var shader = AssetDatabase.LoadAssetAtPath<Shader>("Packages/com.abstractocclusion.webgpuwater/Runtime/Shaders/WaterUnderwaterFog.shader");
                var material = new Material(shader);
                try
                {
                    ShaderUtil.CompilePass(material, 0, true);
                    var errors = ShaderUtil.GetShaderMessages(shader).Where(m => m.severity.ToString() == "Error").ToArray();
                    if (errors.Length > 0) throw new Exception(string.Join("\n", errors.Select(e => e.message)));
                    sb.AppendLine("PASS underwater sonar shader compilation.");
                }
                finally { Object.DestroyImmediate(material); }
            }
            catch (Exception ex) { sb.AppendLine("FAIL " + ex); throw; }
            finally
            {
                if (renderer != null) SonarRevealManager.SetSuppressed(renderer, false);
                if (manager != null) typeof(SonarRevealManager).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);
                typeof(SonarRevealManager).GetProperty("Instance").SetValue(null, oldManager);
                EditorSceneManager.ClosePreviewScene(scene); File.WriteAllText("Logs/DeepSeaDemo/Gameplay-Regression.txt", sb.ToString());
            }
        }
        public static void Inspect()
        {
            var sb = new StringBuilder();
            var flow = Object.FindFirstObjectByType<DemoFlow>();
            sb.AppendLine("Scene=" + SceneManager.GetActiveScene().path + " playing=" + EditorApplication.isPlaying);
            if (flow == null) return;
            sb.AppendLine("Head=" + flow.player.Camera.transform.position + " new-game spawn=" + flow.checkpoints[0].position);
            for(var t = flow.player.Camera.transform; t != null; t=t.parent)
                foreach(var c in t.GetComponents<Component>())
                    sb.AppendLine("COMPONENT "+t.name+" "+c.GetType().Name+" "+(c is MonoBehaviour ? EditorJsonUtility.ToJson(c) : ""));
            sb.AppendLine("Weather ready=" + flow.config.weatherReady + " weather=" + flow.weather?.system + " storm=" + flow.weather?.Storm);
            sb.AppendLine("Ambient=" + RenderSettings.ambientIntensity + " fog=" + RenderSettings.fog);
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                sb.AppendLine("Light=" + light.name + " on=" + light.isActiveAndEnabled + " intensity=" + light.intensity + " pos=" + light.transform.position + " emergency=" + flow.emergencyLights.Contains(light));
            foreach (var volume in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                sb.AppendLine("Volume=" + volume.name + " active=" + volume.isActiveAndEnabled + " global=" + volume.isGlobal + " weight=" + volume.weight + " profile=" + AssetDatabase.GetAssetPath(volume.sharedProfile));
            foreach (var p in new[] {flow.player.Camera.transform.position, flow.checkpoints[0].position})
                foreach (var collider in Physics.OverlapSphere(p, .12f, flow.config.worldMask, QueryTriggerInteraction.Ignore))
                    sb.AppendLine("Head obstruction at " + p + ": " + collider.name + " layer=" + collider.gameObject.layer + " bounds=" + collider.bounds);
            foreach (var w in Object.FindObjectsByType<AbstractOcclusion.WebGpuWater.WaterVolume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                sb.AppendLine("WATER " + w.name + " active=" + w.isActiveAndEnabled + " " + EditorJsonUtility.ToJson(w));
            var cameraData = flow.player.Camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            sb.AppendLine("RENDERER " + cameraData.scriptableRenderer.GetType().Name);
            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableRendererData"))
            {
                var data = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.ScriptableRendererData>(AssetDatabase.GUIDToAssetPath(guid));
                sb.AppendLine("RENDERER ASSET " + data.name + " " + string.Join(",", data.rendererFeatures.Select(f => f == null ? "NULL" : f.GetType().Name + "=" + f.isActive)));
            }
            sb.AppendLine("DOOR " + EditorJsonUtility.ToJson(flow.door) + " position=" + flow.door.transform.position);
            var hatchRenderers = flow.door.hinge.GetComponentsInChildren<MeshRenderer>();
            if (hatchRenderers.Length > 0)
            {
                var bounds = hatchRenderers[0].bounds;
                foreach (var renderer in hatchRenderers) bounds.Encapsulate(renderer.bounds);
                sb.AppendLine("HATCH BOUNDS center=" + bounds.center + " size=" + bounds.size + " min=" + bounds.min + " max=" + bounds.max);
                var userHand = new Vector3(20.24f, -19.07f, 18.42f);
                sb.AppendLine("HATCH closest visual point to reported hand=" + bounds.ClosestPoint(userHand) + " distance=" + Vector3.Distance(userHand, bounds.ClosestPoint(userHand)));
            }
            foreach (var c in flow.door.hinge.GetComponentsInChildren<Collider>()) sb.AppendLine("DOOR COLLIDER " + c.name + " " + c.bounds);
            foreach (var p in flow.props) sb.AppendLine("PROP " + p.id + " " + p.name + " at=" + p.transform.position + " repair=" + (p.GetComponent<RepairTool>() != null));
            foreach (var r in Object.FindObjectsByType<RepairTool>(FindObjectsSortMode.None)) sb.AppendLine("TOOL " + EditorJsonUtility.ToJson(r));
            File.WriteAllText("Logs/DeepSeaDemo/Gameplay-Inspection.txt", sb.ToString());
        }
    }
}
#endif
