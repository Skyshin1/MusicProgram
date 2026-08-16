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
    private struct Pulse
    {
        public bool active;
        public Vector3 origin;
        public float age;
        public float speed;
        public float width;
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

    public void Emit(Vector3 worldOrigin, float strength = 1f)
    {
        if (strength <= 0.001f)
            return;

        pulses[nextPulse] = new Pulse
        {
            active = true,
            origin = worldOrigin,
            age = 0f,
            speed = propagationSpeed,
            width = ringWidth,
            strength = Mathf.Clamp01(strength)
        };
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
        float deltaTime = Time.unscaledDeltaTime;

        for (int i = 0; i < pulses.Length; i++)
        {
            Pulse pulse = pulses[i];
            if (!pulse.active)
                continue;

            pulse.age += deltaTime;
            float radius = pulse.age * Mathf.Max(0.01f, pulse.speed);
            float fadeAge = Mathf.Max(0f, radius - maximumRadius) /
                Mathf.Max(0.01f, pulse.speed);
            if (fadeAge > endFadeDuration)
            {
                pulse.active = false;
                pulses[i] = pulse;
                continue;
            }

            float endFade = endFadeDuration <= 0f
                ? 1f
                : 1f - Mathf.SmoothStep(0f, 1f, fadeAge / endFadeDuration);
            shaderOrigins[activeCount] = new Vector4(
                pulse.origin.x,
                pulse.origin.y,
                pulse.origin.z,
                Mathf.Min(radius, maximumRadius));
            shaderParams[activeCount] = new Vector4(
                Mathf.Max(0.01f, pulse.width),
                pulse.strength,
                endFade,
                0f);
            activeCount++;
            pulses[i] = pulse;
        }

        Shader.SetGlobalInt(PulseCountId, activeCount);
        if (activeCount > 0)
        {
            Shader.SetGlobalVectorArray(PulseOriginsId, shaderOrigins);
            Shader.SetGlobalVectorArray(PulseParamsId, shaderParams);
        }
    }
}
