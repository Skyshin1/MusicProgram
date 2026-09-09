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
        public event Action StateChanged;
        DemoSave checkpoint, initial;
        QuestLeftStickLocomotion movement;
        WaterSurfaceStateTracker water;
        PlayerOxygen oxygen;
        PlayerRespawnController respawn;
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
            movement = player.GetComponent<QuestLeftStickLocomotion>();
            water = player.GetComponent<WaterSurfaceStateTracker>();
            oxygen = player.GetComponent<PlayerOxygen>();
            respawn = player.GetComponent<PlayerRespawnController>();
            if (respawn != null) respawn.Respawned += OnRespawned;
        }
        void Start()
        {
            props ??= FindObjectsByType<DemoProp>(FindObjectsSortMode.None);
            State.spawn = checkpoints[0].position;
            State.yaw = checkpoints[0].eulerAngles.y;
            CaptureProps(State);
            initial = State.Copy(); checkpoint = initial.Copy();
            SetPaused(true); ui.ShowMain();
        }
        void OnDestroy()
        {
            if (respawn != null) respawn.Respawned -= OnRespawned;
            if (Instance == this) { Instance = null; Time.timeScale = 1f; }
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
        void Restore(DemoSave source)
        {
            State = source.Copy();
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
            player.MatchOriginUpCameraForward(Vector3.up, Quaternion.Euler(0, yaw, 0) * Vector3.forward);
            player.MoveCameraToWorldLocation(headPosition); cc.enabled = was;
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
