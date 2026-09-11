using System;
using System.Collections;
using System.IO;
using System.Linq;
using DeepSeaAI;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace DeepSeaDemo
{
    [DefaultExecutionOrder(-80)]
    public sealed class DemoFlow : MonoBehaviour
    {
        public DemoConfig config;
        public XROrigin player;
        [Tooltip("New Game head position and facing. Falls back to checkpoints[0] when unassigned.")]
        public Transform initialSpawn;
        public Transform[] checkpoints;
        public DemoProp[] props;
        public DemoDoor door;
        public DeepSeaStalkerController enemy;
        public Transform engineInvestigation;
        public AudioSource engineAudio;
        public Light[] emergencyLights;
        public DemoWeather weather;
        public DemoUI ui;
        public static DemoFlow Instance { get; private set; }
        public DemoSave State { get; private set; } = new();
        public bool Running { get; private set; }
        public bool Paused { get; private set; }
        public bool Busy { get; private set; }
        public bool Equipped => State.Has("suit");
        [NonSerialized] public bool suppressSaveForTests;
        [NonSerialized] public float ladderTestInput = float.NaN;
        public event Action StateChanged;
        DemoSave checkpoint, initial;
        QuestLeftStickLocomotion movement;
        WaterSurfaceStateTracker water;
        PlayerOxygen oxygen;
        PlayerRespawnController respawn;
        Coroutine ladderRoutine;
        string SavePath => Path.Combine(Application.persistentDataPath,
            string.IsNullOrWhiteSpace(config.saveFileName) ? "DeepSeaInvestigation.checkpoint.json" : Path.GetFileName(config.saveFileName));
        public bool HasSave => File.Exists(SavePath);
        public string Objective => State.stage switch
        {
            DemoStage.Platform => !State.Has("log01") ? DemoTextCatalog.Get("runtime.000") :
                !State.Has("alarm") ? DemoTextCatalog.Get("runtime.001") :
                !State.Has("flashlight") ? DemoTextCatalog.Get("runtime.002") : DemoTextCatalog.Get("runtime.003"),
            DemoStage.Dive => DemoTextCatalog.Get("runtime.004"),
            DemoStage.Seabed => DemoTextCatalog.Get("runtime.005"),
            DemoStage.Submarine => State.doorOpen ? DemoTextCatalog.Get("runtime.006") : DemoTextCatalog.Get("runtime.007"),
            DemoStage.Return => DemoTextCatalog.Get("runtime.008"),
            DemoStage.Analysis => DemoTextCatalog.Get("runtime.009"),
            _ => DemoTextCatalog.Get("runtime.010")
        };
        void Awake()
        {
            Instance = this;
            foreach (var body in FindObjectsByType<AbstractOcclusion.WebGpuWater.WaterVolume>(FindObjectsSortMode.None))
                if (body.IsPrimary)
                {
                    body.SurfaceAbsorptionScale = config.waterSurfaceAbsorptionScale;
                    body.UnderwaterSurfaceOpacity = config.waterUnderSurfaceOpacity;
                }
            // The water sample's orbit controller writes camera WORLD position
            // every LateUpdate, undoing XR tracking/teleports and putting the head
            // inside the platform floor. Only the XR rig owns this camera.
            var orbit = player.Camera.GetComponent<AbstractOcclusion.WebGpuWater.OrbitCamera>();
            if (orbit != null) orbit.enabled = false;
            movement = player.GetComponent<QuestLeftStickLocomotion>();
            water = player.GetComponent<WaterSurfaceStateTracker>();
            oxygen = player.GetComponent<PlayerOxygen>();
            respawn = player.GetComponent<PlayerRespawnController>();
            if (respawn != null) respawn.Respawned += OnRespawned;
            if (respawn != null) respawn.BiteReceived += OnBiteReceived;
        }
        IEnumerator Start()
        {
            props ??= FindObjectsByType<DemoProp>(FindObjectsSortMode.None);
            Transform start = initialSpawn != null ? initialSpawn : checkpoints[0];
            State.spawn = start.position;
            State.yaw = start.eulerAngles.y;
            CaptureProps(State);
            initial = State.Copy(); checkpoint = initial.Copy();
            SetPaused(true);
            // Let tracked poses initialize, then show the menu at the same
            // position and heading New Game will restore. Previously the click
            // itself moved/turned the view, abruptly changing visible lighting.
            yield return null;
            if (!Running) { SafeTeleport(State.spawn, State.yaw); ui.ShowMain(); }
        }
        void OnDestroy()
        {
            if (respawn != null) respawn.Respawned -= OnRespawned;
            if (respawn != null) respawn.BiteReceived -= OnBiteReceived;
            if (Instance == this) { Instance = null; Time.timeScale = 1f; }
        }
        void OnDisable() => CancelLadder();
        void CancelLadder()
        {
            if (ladderRoutine == null) return;
            StopCoroutine(ladderRoutine);
            ladderRoutine = null;
        }
        void Update()
        {
            if (!Running || Paused || Busy) return;
            if (oxygen != null)
            {
                if (Equipped && water.IsUnderwater)
                    oxygen.Consume(100f / Mathf.Max(60f, config.oxygenSeconds) * Time.deltaTime);
                else oxygen.Refill(100f / Mathf.Max(1f, config.refillSeconds) * Time.deltaTime);
                if (oxygen.IsDepleted) { Retry(); return; }
            }
            if (State.stage == DemoStage.Dive && water.IsUnderwater && water.SignedDepth > 12f)
            {
                State.stage = DemoStage.Seabed; Changed();
                ui.Toast(DemoTextCatalog.Get("runtime.011"));
            }
            if (State.stage == DemoStage.Return && !water.IsUnderwater &&
                Vector3.Distance(player.Camera.transform.position, checkpoints[3].position) < 12f)
            {
                State.stage = DemoStage.Analysis; SaveCheckpoint(3); Changed();
            }
        }
        public void NewGame()
        {
            checkpoint = initial.Copy();
            Running = true; Restore(checkpoint); WriteSave(); SetPaused(false); ui.ShowHUD();
        }
        public void ContinueGame()
        {
            try
            {
                var loaded = JsonUtility.FromJson<DemoSave>(File.ReadAllText(SavePath));
                if (loaded == null || loaded.version != 1 || loaded.facts == null || loaded.props == null ||
                    !Enum.IsDefined(typeof(DemoStage), loaded.stage) || !Finite(loaded.spawn))
                    throw new InvalidDataException("Invalid checkpoint");
                checkpoint = loaded; Running = true; Restore(checkpoint); SetPaused(false); ui.ShowHUD();
            }
            catch (Exception ex) { Debug.LogWarning("[DeepSeaDemo] Cannot load checkpoint: " + ex.Message); ui.Toast(DemoTextCatalog.Get("runtime.012")); }
        }
        static bool Finite(Vector3 p) => !float.IsNaN(p.sqrMagnitude) && !float.IsInfinity(p.sqrMagnitude);
        public void Record(string fact)
        {
            if (!Running || Busy || !State.Add(fact)) return;
            if (State.stage == DemoStage.Platform && new[] { "log01", "alarm", "flashlight", "suit" }.All(State.Has))
            {
                State.stage = DemoStage.Dive; SaveCheckpoint(0); weather?.SetStorm(true);
                ui.Toast(DemoTextCatalog.Get("runtime.013"));
            }
            if (State.Has("locktool") && State.stage <= DemoStage.Seabed && Equipped)
            { State.stage = DemoStage.Submarine; SaveCheckpoint(1); }
            if (State.Has("blackbox") && State.doorOpen && State.stage < DemoStage.Return)
            { State.stage = DemoStage.Return; StartEngine(); }
            Changed();
        }
        public void DoorOpened()
        {
            if (Busy || State.doorOpen) return;
            State.doorOpen = true; State.Add("door"); SaveCheckpoint(2); Changed();
            if (State.Has("blackbox")) { State.stage = DemoStage.Return; StartEngine(); Changed(); }
            ui.Toast(DemoTextCatalog.Get("runtime.014"));
        }
        public void ParsedBlackBox()
        {
            if (!Running || Busy || !State.Has("blackbox") || !State.doorOpen) return;
            var bindings = GetComponent<DemoSceneBindings>();
            if (bindings == null || bindings.dock == null || bindings.dock.CurrentItem == null) return;
            State.Add("parsed"); State.stage = DemoStage.Analysis; Changed();
            ui.ShowAnalysis();
        }
        public void ChooseEnding(bool upload)
        {
            if (!State.Has("parsed") || Busy) return;
            StartCoroutine(EndRoutine(upload));
        }
        IEnumerator EndRoutine(bool upload)
        {
            Busy = true; SetPaused(true);
            if (upload)
            {
                ui.ShowMessage(DemoTextCatalog.Get("runtime.015"), DemoTextCatalog.Get("runtime.016"), false);
                yield return new WaitForSecondsRealtime(2f);
                ui.ShowMessage(DemoTextCatalog.Get("runtime.017"), DemoTextCatalog.Get("runtime.018"), false);
                yield return new WaitForSecondsRealtime(3f);
            }
            State.ending = upload ? "uploaded" : "preserved"; State.stage = DemoStage.Ending;
            Busy = false; ui.ShowEnding(upload); Changed();
        }
        void StartEngine()
        {
            if (State.engineStarted) return;
            State.engineStarted = true;
            if (engineAudio != null) engineAudio.Play();
            foreach (var light in emergencyLights) if (light != null) light.enabled = true;
            NoiseSystem.Emit(new NoiseStimulus(engineInvestigation.position, 45f, NoiseKind.Interaction, engineInvestigation, Time.time));
            if (ui.SubtitlesEnabled)
                ui.ShowMessage(DemoTextCatalog.Get("runtime.019"), DemoTextCatalog.Get("runtime.020"), true);
            else ui.Toast(DemoTextCatalog.Get("runtime.021"));
        }
        void SaveCheckpoint(int index)
        {
            State.spawn = checkpoints[index].position; State.yaw = checkpoints[index].eulerAngles.y;
            CaptureProps(State); checkpoint = State.Copy(); WriteSave();
            respawn?.Configure(checkpoints[index]);
        }
        void CaptureProps(DemoSave target)
        {
            target.props.Clear();
            foreach (var prop in props) if (prop != null) target.props.Add(prop.Capture());
        }
        void WriteSave()
        {
            if (suppressSaveForTests) return;
            try
            {
                // Only this game's explicitly named checkpoint is replaced; never arbitrary paths.
                string temp = SavePath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(checkpoint, true));
                if (File.Exists(SavePath)) File.Replace(temp, SavePath, SavePath + ".bak");
                else File.Move(temp, SavePath);
            }
            catch (Exception ex) { Debug.LogWarning("[DeepSeaDemo] Checkpoint write failed: " + ex.Message); ui.Toast(DemoTextCatalog.Get("runtime.022")); }
        }
        public void Retry() { if (!Busy && Running) StartCoroutine(RetryRoutine()); }
        IEnumerator RetryRoutine()
        {
            Busy = true; SetPaused(true); ui.SetFade(1f);
            yield return new WaitForSecondsRealtime(.35f);
            Restore(checkpoint); ui.SetFade(0f); Busy = false; SetPaused(false); ui.ShowHUD();
        }
        void OnRespawned() { if (Running) Restore(checkpoint); }
        void OnBiteReceived(int remaining)
        {
            if (remaining > 0) ui.Toast("BITTEN - Health " + remaining + "/4. Move away from the fish!");
        }
        void Restore(DemoSave source)
        {
            CancelLadder();
            State = source.Copy();
            respawn?.ResetBiteHealth();
            foreach (var prop in props)
            {
                var saved = State.props.Find(p => p.id == prop.id);
                if (saved != null) prop.Restore(saved);
            }
            door?.Restore(State.doorOpen);
            VolumetricFogPulseEmitter.Instance?.ClearPulses();
            FindFirstObjectByType<SonarRevealManager>()?.ClearReveals();
            foreach (var check in FindObjectsByType<RepairSkillCheckController>(FindObjectsSortMode.None)) check.CancelActiveCheck();
            FindFirstObjectByType<DemoAcoustics>()?.ResetExposure();
            if (engineAudio != null) { engineAudio.Stop(); if (State.engineStarted) engineAudio.Play(); }
            foreach (var light in emergencyLights) if (light != null) light.enabled = State.engineStarted;
            weather?.SetStorm(State.stage != DemoStage.Platform);
            oxygen.Refill(); SafeTeleport(State.spawn, State.yaw);
            enemy?.ResetToPatrol(true); Changed();
        }
        public bool SafeTeleport(Vector3 headPosition, float yaw)
        {
            if (!Finite(headPosition)) return false;
            var safety = player.GetComponent<DemoPlayerSafety>();
            if (safety != null && !safety.CanPlaceHead(headPosition)) { ui.Toast(DemoTextCatalog.Get("runtime.023")); return false; }
            var cc = player.GetComponent<CharacterController>(); bool was = cc.enabled; cc.enabled = false;
            Pose before = new Pose(player.transform.position, player.transform.rotation);
            var input = player.GetComponent<DemoInputRouter>();
            var left = input != null ? input.HeldProp(false) : null;
            var right = input != null ? input.HeldProp(true) : null;
            player.MatchOriginUpCameraForward(Vector3.up, Quaternion.Euler(0, yaw, 0) * Vector3.forward);
            player.MoveCameraToWorldLocation(headPosition); cc.enabled = was;
            Pose after = new Pose(player.transform.position, player.transform.rotation);
            if (left != null) left.MoveWithPlayer(before, after);
            if (right != null && right != left) right.MoveWithPlayer(before, after);
            movement.ResetVerticalMotion(); water.TryRefreshNow(); return true;
        }
        public void ReturnToSafePosition() { if (!Busy) SafeTeleport(checkpoint.spawn, checkpoint.yaw); }
        public void Board(Transform destination) { if (!Busy && destination != null) StartCoroutine(BoardRoutine(destination)); }
        IEnumerator BoardRoutine(Transform destination)
        {
            Busy = true; SetPaused(true); ui.SetFade(1);
            yield return new WaitForSecondsRealtime(.25f);
            SafeTeleport(destination.position, destination.eulerAngles.y);
            yield return new WaitForSecondsRealtime(.15f);
            ui.SetFade(0); Busy = false; SetPaused(false);
        }
        public void ClimbLadder(DemoLadderClimb ladder)
        {
            if (Busy || ladder == null || !ladder.IsConfigured) return;
            if (Vector3.Distance(player.Camera.transform.position, ladder.ClosestPoint(player.Camera.transform.position)) > ladder.activationDistance)
            { ui.Toast("Move closer to the ladder."); return; }
            ladderRoutine = StartCoroutine(LadderRoutine(ladder));
        }
        IEnumerator LadderRoutine(DemoLadderClimb ladder)
        {
            Busy = true;
            movement.SetMovementEnabled(false);
            var cc = player.GetComponent<CharacterController>();
            bool ccWasEnabled = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;
            try
            {
            Vector3 start = player.Camera.transform.position;
            Vector3 bottom = ladder.bottomAnchor.position;
            Vector3 top = ladder.topAnchor.position;
            Vector3 entry = ladder.ClosestPoint(start);
            float align = Mathf.Max(.05f, ladder.alignmentSeconds);
            for (float t = 0; t < align; t += Time.unscaledDeltaTime)
            {
                float eased = Mathf.SmoothStep(0, 1, t / align);
                if (ladder == null || !ladder.isActiveAndEnabled) yield break;
                player.MoveCameraToWorldLocation(Vector3.Lerp(start, entry, eased));
                yield return null;
            }
            player.MoveCameraToWorldLocation(entry);
            float totalLength = Mathf.Max(.1f, Vector3.Distance(bottom, top));
            float travelled = Vector3.Distance(bottom, entry);
            while (travelled < totalLength)
            {
                if (ladder == null || !ladder.isActiveAndEnabled || !ladder.IsConfigured) yield break;
                Vector3 head = player.Camera.transform.position;
                if (Vector3.Distance(head, ladder.ClosestPoint(head)) > ladder.releaseDistance) yield break;
                // A walk input releases the ladder instead of trapping the player
                // on the path while ordinary locomotion is disabled.
                var controls = player.GetComponent<DemoXRInput>();
                if (controls != null && controls.left.Stick.sqrMagnitude > .25f) yield break;
                float input = LadderVerticalInput();
                if (Mathf.Abs(input) < ladder.stickDeadZone) input = 0f;
                travelled = Mathf.Clamp(travelled + input * ladder.climbSpeed * Time.unscaledDeltaTime, 0f, totalLength);
                Vector3 position = Vector3.Lerp(bottom, top, travelled / totalLength);
                player.MoveCameraToWorldLocation(position);
                if (travelled <= 0f && input < 0f) break;
                if (travelled >= totalLength) break;
                yield return null;
            }
            }
            finally
            {
            if (cc != null) cc.enabled = ccWasEnabled;
            if (movement != null) { movement.ResetVerticalMotion(); movement.SetMovementEnabled(!Paused); }
            if (water != null) water.TryRefreshNow();
            Busy = false;
            ladderRoutine = null;
            }
        }
        float LadderVerticalInput()
        {
            if (!float.IsNaN(ladderTestInput)) return Mathf.Clamp(ladderTestInput, -1f, 1f);
            var input = player.GetComponent<DemoXRInput>();
            if (input != null && input.isActiveAndEnabled) return input.right.Stick.y;
            var device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
            return device.isValid && device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 stick) ? stick.y : 0f;
        }
        public void SetPaused(bool paused)
        {
            Paused = paused;
            // The simulator integrates tracked device motion with deltaTime. Global zero time
            // also freezes its initial FPS mode and makes the main menu impossible to aim at.
            var coordinator = GetComponent<DemoPauseCoordinator>();
            if (coordinator != null) { Time.timeScale = 1f; coordinator.SetPaused(paused); }
            else Time.timeScale = paused ? 0f : 1f;
            if (movement != null) movement.SetMovementEnabled(!paused);
        }
        public void Pause() { if (Running && !Busy) { SetPaused(true); ui.ShowPause(); } }
        public void Resume() { if (Running && !Busy) { SetPaused(false); ui.ShowHUD(); } }
        public void MainMenu() { SetPaused(true); Running = false; ui.ShowMain(); }
        void Changed() { StateChanged?.Invoke(); }
    }
}
