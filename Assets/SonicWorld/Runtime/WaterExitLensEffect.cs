using Unity.XR.CoreUtils;
using UnityEngine;

/// <summary>
/// Drives the short screen-space lens-water effect when the tracked headset
/// crosses from underwater to air. Rendering is performed by the matching URP
/// renderer feature.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(WaterSurfaceStateTracker))]
public sealed class WaterExitLensEffect : MonoBehaviour
{
    public readonly struct RenderState
    {
        public readonly float Weight;
        public readonly float Elapsed;
        public readonly float EdgeWidth;
        public readonly float Density;
        public readonly float FallSpeed;
        public readonly float Distortion;

        public RenderState(float weight, float elapsed, float edgeWidth,
            float density, float fallSpeed, float distortion)
        {
            Weight = weight;
            Elapsed = elapsed;
            EdgeWidth = edgeWidth;
            Density = density;
            FallSpeed = fallSpeed;
            Distortion = distortion;
        }
    }

    [Header("Exit Effect")]
    [SerializeField, Min(0.1f)] private float duration = 2.5f;
    [SerializeField, Range(0.05f, 0.5f)] private float edgeWidth = 0.24f;
    [SerializeField, Range(4f, 40f)] private float dropletDensity = 17f;
    [SerializeField, Range(0.05f, 3f)] private float fallSpeed = 0.75f;
    [SerializeField, Range(0f, 0.05f)] private float distortion = 0.012f;

    private WaterSurfaceStateTracker waterState;
    private float startedAt = float.NegativeInfinity;

    public static WaterExitLensEffect ActiveInstance { get; private set; }
    public bool IsActive => Time.unscaledTime - startedAt < duration;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        ActiveInstance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureOnLoadedOrigins()
    {
        XROrigin[] origins = FindObjectsByType<XROrigin>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (XROrigin origin in origins)
        {
            if (origin == null)
                continue;
            if (origin.GetComponent<WaterSurfaceStateTracker>() == null)
                origin.gameObject.AddComponent<WaterSurfaceStateTracker>();
            if (origin.GetComponent<WaterExitLensEffect>() == null)
                origin.gameObject.AddComponent<WaterExitLensEffect>();
        }
    }

    private void Awake()
    {
        waterState = GetComponent<WaterSurfaceStateTracker>();
    }

    private void OnEnable()
    {
        ActiveInstance = this;
        if (waterState == null)
            waterState = GetComponent<WaterSurfaceStateTracker>();
        waterState.ExitedWater += OnExitedWater;
    }

    private void OnDisable()
    {
        if (waterState != null)
            waterState.ExitedWater -= OnExitedWater;
        if (ActiveInstance == this)
            ActiveInstance = null;
    }

    public void Play()
    {
        startedAt = Time.unscaledTime;
    }

    public RenderState GetRenderState()
    {
        float elapsed = Mathf.Max(0f, Time.unscaledTime - startedAt);
        float normalized = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, duration));
        float weight = IsActive
            ? 1f - normalized * normalized * (3f - 2f * normalized)
            : 0f;
        return new RenderState(
            weight, elapsed, edgeWidth, dropletDensity, fallSpeed, distortion);
    }

    private void OnExitedWater(AbstractOcclusion.WebGpuWater.WaterVolume water)
    {
        Play();
    }

    private void OnValidate()
    {
        duration = Mathf.Max(0.1f, duration);
        edgeWidth = Mathf.Clamp(edgeWidth, 0.05f, 0.5f);
        dropletDensity = Mathf.Clamp(dropletDensity, 4f, 40f);
        fallSpeed = Mathf.Clamp(fallSpeed, 0.05f, 3f);
        distortion = Mathf.Clamp(distortion, 0f, 0.05f);
    }
}
