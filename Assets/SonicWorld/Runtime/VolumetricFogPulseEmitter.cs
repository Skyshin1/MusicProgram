using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using Unity.XR.CoreUtils;

/// <summary>
/// Standalone gameplay pulse. Pressing its configured key emits a world-space
/// shell that clears WebGPU Water's underwater visibility only where it
/// intersects visible geometry. The legacy type name is intentionally kept so
/// existing player prefabs, collision emitters and gameplay code keep working.
/// </summary>
[DisallowMultipleComponent]
public sealed class VolumetricFogPulseEmitter : MonoBehaviour
{
    public enum PlayerPulseOrigin
    {
        RightHand,
        LeftHand,
        PlayerBody
    }

    public readonly struct PulseState
    {
        public readonly int Id;
        public readonly Vector3 Origin;
        public readonly float Radius;
        public readonly float Width;
        public readonly float Strength;
        public readonly float EndFade;

        public PulseState(int id, Vector3 origin, float radius, float width, float strength, float endFade)
        {
            Id = id;
            Origin = origin;
            Radius = radius;
            Width = width;
            Strength = strength;
            EndFade = endFade;
        }
    }

    private struct Pulse
    {
        public bool active;
        public int id;
        public Vector3 origin;
        public float age;
        public float speed;
        public float width;
        public float maximumRadius;
        public float endFadeDuration;
        public float strength;
    }

    private const int PulseCapacity = 12;

    // These names deliberately differ from the old Volumetric Fog package's globals.
    // Water sonar must not also clear a legacy fog renderer still installed in the project.
    private static readonly int PulseCountId =
        Shader.PropertyToID("_WaterSonarPulseCount");
    private static readonly int PulseOriginsId =
        Shader.PropertyToID("_WaterSonarPulseOrigins");
    private static readonly int PulseParamsId =
        Shader.PropertyToID("_WaterSonarPulseParams");
    private static readonly int LegacyPulseCountId =
        Shader.PropertyToID("_VolumetricFogPulseCount");

    public static VolumetricFogPulseEmitter Instance { get; private set; }
    public Transform OriginTransform => ResolveOriginTransform();
    public static event System.Action<PulseState> PulseStarted;
    public static event System.Action<PulseState> PulseUpdated;
    public static event System.Action<PulseState> PulseEnded;
    public static event System.Action AllPulsesEnded;
    public static event System.Action<Vector3, float, Transform> PlayerSonarEmitted;

    [Header("Input")]
    [SerializeField] private Key triggerKey = Key.F;
    [SerializeField, Range(0f, 1f)] private float triggerStrength = 1f;
    [SerializeField]
    [Tooltip("Optional explicit gameplay origin. When assigned, it overrides the selected player hand/body origin.")]
    private Transform origin;

    [SerializeField]
    [Tooltip("Default is Right Hand. The selected tracked hand is used at the instant F is pressed; if it is unavailable, the pulse falls back to Player Body.")]
    private PlayerPulseOrigin playerPulseOrigin = PlayerPulseOrigin.RightHand;

    [Header("XR Body-centred Origin")]
    [SerializeField, Range(-1f, 3f)]
    [Tooltip("Vertical position of a player-originated sonar above the XR Origin. Horizontal position always follows the tracked headset.")]
    private float playerCenterHeight = 0.9f;

    [Header("Water Sonar Shell")]
    [SerializeField, Range(1f, 40f)] private float propagationSpeed = 12f;
    [SerializeField, Range(0.05f, 3f)] private float ringWidth = 0.45f;
    [SerializeField, Range(1f, 150f)] private float maximumRadius = 45f;
    [SerializeField, Range(0f, 2f)] private float endFadeDuration = 0.25f;

    private readonly Pulse[] pulses = new Pulse[PulseCapacity];
    private readonly Vector4[] shaderOrigins = new Vector4[PulseCapacity];
    private readonly Vector4[] shaderParams = new Vector4[PulseCapacity];
    private int nextPulse;
    private int nextPulseId = 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (FindFirstObjectByType<VolumetricFogPulseEmitter>() != null)
            return;

        GameObject system = new GameObject("Volumetric Fog Pulse Emitter");
        system.AddComponent<VolumetricFogPulseEmitter>();
        DontDestroyOnLoad(system);
    }

    private void OnEnable()
    {
        Instance = this;
        Shader.SetGlobalInt(PulseCountId, 0);
        // Fast Enter Play Mode keeps shader globals alive. Explicitly neutralize
        // the retired route so a previous session cannot keep legacy fog clearing.
        Shader.SetGlobalInt(LegacyPulseCountId, 0);
    }

    private void OnDisable()
    {
        if (Instance == this)
            Instance = null;
        Shader.SetGlobalInt(PulseCountId, 0);
        Shader.SetGlobalInt(LegacyPulseCountId, 0);
    }

    private void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current[triggerKey].wasPressedThisFrame)
        {
            Vector3 playerOrigin = ResolveOrigin();
            Emit(playerOrigin, triggerStrength);
            Transform playerSource = OriginTransform;
            PlayerSonarEmitted?.Invoke(playerOrigin, triggerStrength, playerSource);
        }

        UploadActivePulses();
    }

    /// <summary>
    /// Emits from any gameplay position. Use this from skills, interaction
    /// events, or UI without using the keyboard input path.
    /// </summary>
    public static void EmitAt(Vector3 worldOrigin, float strength = 1f)
    {
        if (Instance == null)
            EnsureInstance();
        Instance?.Emit(worldOrigin, strength);
    }

    /// <summary>
    /// Emits a pulse with its own ring shape. This is intended for gameplay
    /// sources such as collisions, while the keyboard pulse keeps its defaults.
    /// </summary>
    public static void EmitAt(
        Vector3 worldOrigin,
        float strength,
        float speed,
        float width,
        float radius,
        float fadeDuration)
    {
        if (Instance == null)
            EnsureInstance();
        Instance?.Emit(worldOrigin, strength, speed, width, radius, fadeDuration);
    }

    public void Emit(Vector3 worldOrigin, float strength = 1f)
    {
        Emit(worldOrigin, strength, propagationSpeed, ringWidth, maximumRadius, endFadeDuration);
    }

    public void Emit(
        Vector3 worldOrigin,
        float strength,
        float speed,
        float width,
        float radius,
        float fadeDuration)
    {
        if (strength <= 0.001f)
            return;

        if (pulses[nextPulse].active)
            PulseEnded?.Invoke(ToState(pulses[nextPulse], pulses[nextPulse].maximumRadius, 0f));

        pulses[nextPulse] = new Pulse
        {
            active = true,
            id = nextPulseId++,
            origin = worldOrigin,
            age = 0f,
            speed = Mathf.Max(0.01f, speed),
            width = Mathf.Max(0.01f, width),
            maximumRadius = Mathf.Max(0.01f, radius),
            endFadeDuration = Mathf.Max(0f, fadeDuration),
            strength = Mathf.Clamp01(strength)
        };
        PulseStarted?.Invoke(ToState(pulses[nextPulse], 0f, 1f));
        nextPulse = (nextPulse + 1) % PulseCapacity;
    }

    private Vector3 ResolveOrigin()
    {
        if (origin != null)
            return origin.position;

        if (playerPulseOrigin != PlayerPulseOrigin.PlayerBody)
        {
            Transform hand = FindPlayerHandTransform(playerPulseOrigin == PlayerPulseOrigin.RightHand);
            if (hand != null)
                return hand.position;
        }

        Transform playerBody = FindPlayerOriginTransform();
        Transform playerView = FindPlayerViewTransform();
        if (playerBody != null && playerView != null)
        {
            // Room-scale movement updates the headset but not necessarily the
            // XR Origin. Conversely, this project's headset Y can be offset by
            // underwater tracking. Combine the trustworthy parts of both.
            return new Vector3(
                playerView.position.x,
                playerBody.position.y + playerCenterHeight,
                playerView.position.z);
        }

        return OriginTransform.position;
    }

    private Transform ResolveOriginTransform()
    {
        if (origin != null)
            return origin;

        if (playerPulseOrigin != PlayerPulseOrigin.PlayerBody)
        {
            Transform hand = FindPlayerHandTransform(playerPulseOrigin == PlayerPulseOrigin.RightHand);
            if (hand != null)
                return hand;
        }

        return FindPlayerOriginTransform() ?? FindPlayerViewTransform() ?? transform;
    }

    /// <summary>
    /// Resolves the camera driven by the active XR Origin before falling back to
    /// a tagged Main Camera. This avoids selecting a legacy scene camera when a
    /// VR scene contains both a desktop camera and a tracked headset camera.
    /// </summary>
    public static Transform FindPlayerViewTransform()
    {
        // The tracked pose driver is the most precise discriminator in scenes
        // containing an XR Interaction Simulator, a legacy camera, or multiple
        // XR Origins. Only the headset camera has both this driver and Camera.
        TrackedPoseDriver[] trackedDrivers = FindObjectsByType<TrackedPoseDriver>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        foreach (TrackedPoseDriver trackedDriver in trackedDrivers)
        {
            if (trackedDriver != null &&
                trackedDriver.TryGetComponent(out Camera trackedCamera) &&
                trackedCamera.isActiveAndEnabled)
            {
                return trackedCamera.transform;
            }
        }

        XROrigin[] origins = FindObjectsByType<XROrigin>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        foreach (XROrigin xrOrigin in origins)
        {
            Camera xrCamera = xrOrigin != null ? xrOrigin.Camera : null;
            if (xrCamera != null && xrCamera.isActiveAndEnabled)
                return xrCamera.transform;
        }

        Camera mainCamera = Camera.main;
        return mainCamera != null ? mainCamera.transform : null;
    }

    /// <summary>
    /// Finds the body/root transform that owns the actively tracked headset.
    /// This keeps a spherical gameplay pulse centred on the player even when
    /// headset tracking changes the camera's local Y position.
    /// </summary>
    public static Transform FindPlayerOriginTransform()
    {
        XROrigin[] origins = FindObjectsByType<XROrigin>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        foreach (XROrigin xrOrigin in origins)
        {
            Camera xrCamera = xrOrigin != null ? xrOrigin.Camera : null;
            if (xrCamera != null &&
                xrCamera.isActiveAndEnabled &&
                xrCamera.TryGetComponent<TrackedPoseDriver>(out _))
            {
                return xrOrigin.transform;
            }
        }

        // A project without the Input System pose driver can still use an
        // ordinary XR Origin as the player body.
        foreach (XROrigin xrOrigin in origins)
        {
            Camera xrCamera = xrOrigin != null ? xrOrigin.Camera : null;
            if (xrCamera != null && xrCamera.isActiveAndEnabled)
                return xrOrigin.transform;
        }

        return null;
    }

    /// <summary>
    /// Finds the controller transform driven by the Input System pose driver.
    /// The project uses children named Left/Right, while the name matching also
    /// supports the standard XRI controller prefab naming convention.
    /// </summary>
    public static Transform FindPlayerHandTransform(bool rightHand)
    {
        Transform playerRoot = FindPlayerOriginTransform();
        if (playerRoot == null)
            return null;

        string side = rightHand ? "right" : "left";
        TrackedPoseDriver[] drivers = playerRoot.GetComponentsInChildren<TrackedPoseDriver>(true);
        foreach (TrackedPoseDriver driver in drivers)
        {
            if (driver == null || !driver.isActiveAndEnabled)
                continue;

            if (driver.name.IndexOf(side, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return driver.transform;
        }

        return null;
    }

    private void UploadActivePulses()
    {
        int activeCount = 0;
        bool endedAnyPulse = false;
        float deltaTime = Time.unscaledDeltaTime;

        for (int i = 0; i < pulses.Length; i++)
        {
            Pulse pulse = pulses[i];
            if (!pulse.active)
                continue;

            pulse.age += deltaTime;
            float radius = pulse.age * Mathf.Max(0.01f, pulse.speed);
            float fadeAge = Mathf.Max(0f, radius - pulse.maximumRadius) /
                Mathf.Max(0.01f, pulse.speed);
            if (fadeAge > pulse.endFadeDuration)
            {
                pulse.active = false;
                pulses[i] = pulse;
                PulseEnded?.Invoke(ToState(pulse, pulse.maximumRadius, 0f));
                endedAnyPulse = true;
                continue;
            }

            float endFade = pulse.endFadeDuration <= 0f
                ? 1f
                : 1f - Mathf.SmoothStep(0f, 1f, fadeAge / pulse.endFadeDuration);
            shaderOrigins[activeCount] = new Vector4(
                pulse.origin.x,
                pulse.origin.y,
                pulse.origin.z,
                Mathf.Min(radius, pulse.maximumRadius));
            shaderParams[activeCount] = new Vector4(
                Mathf.Max(0.01f, pulse.width),
                pulse.strength,
                endFade,
                0f);
            activeCount++;
            PulseUpdated?.Invoke(ToState(pulse, Mathf.Min(radius, pulse.maximumRadius), endFade));
            pulses[i] = pulse;
        }

        Shader.SetGlobalInt(PulseCountId, activeCount);
        if (activeCount > 0)
        {
            Shader.SetGlobalVectorArray(PulseOriginsId, shaderOrigins);
            Shader.SetGlobalVectorArray(PulseParamsId, shaderParams);
        }

        if (endedAnyPulse && activeCount == 0)
            AllPulsesEnded?.Invoke();
    }

    private static PulseState ToState(Pulse pulse, float radius, float endFade)
    {
        return new PulseState(pulse.id, pulse.origin, radius, pulse.width, pulse.strength, endFade);
    }
}
