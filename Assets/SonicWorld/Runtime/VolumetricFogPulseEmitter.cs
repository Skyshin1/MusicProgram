using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Standalone gameplay pulse. Pressing its configured key emits a world-space
/// ring that clears volumetric fog only where the ring intersects visible
/// geometry. It does not read or subscribe to any SonicWorld audio system.
/// </summary>
[DisallowMultipleComponent]
public sealed class VolumetricFogPulseEmitter : MonoBehaviour
{
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

    private static readonly int PulseCountId =
        Shader.PropertyToID("_VolumetricFogPulseCount");
    private static readonly int PulseOriginsId =
        Shader.PropertyToID("_VolumetricFogPulseOrigins");
    private static readonly int PulseParamsId =
        Shader.PropertyToID("_VolumetricFogPulseParams");

    public static VolumetricFogPulseEmitter Instance { get; private set; }
    public Transform OriginTransform => origin != null ? origin : transform;
    public static event System.Action<PulseState> PulseStarted;
    public static event System.Action<PulseState> PulseUpdated;
    public static event System.Action<PulseState> PulseEnded;
    public static event System.Action AllPulsesEnded;

    [Header("Input")]
    [SerializeField] private Key triggerKey = Key.F;
    [SerializeField, Range(0f, 1f)] private float triggerStrength = 1f;
    [SerializeField]
    [Tooltip("Optional gameplay origin. If empty, the Main Camera emits the pulse.")]
    private Transform origin;

    [Header("Fog Clearing Ring")]
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
    }

    private void OnDisable()
    {
        if (Instance == this)
            Instance = null;
        Shader.SetGlobalInt(PulseCountId, 0);
    }

    private void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current[triggerKey].wasPressedThisFrame)
        {
            Emit(ResolveOrigin(), triggerStrength);
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

        Camera mainCamera = Camera.main;
        return mainCamera != null ? mainCamera.transform.position : transform.position;
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
