using System.Collections.Generic;
using UnityEngine;
using AbstractOcclusion.WebGpuWater;

/// <summary>
/// Tracks renderers swept by standalone Water Volume sonar shells. Renderers
/// keep their original materials; the renderer feature reads this registry to
/// draw only a temporary white outline.
/// </summary>
[DisallowMultipleComponent]
public sealed class SonarRevealManager : MonoBehaviour
{
    private const int ColliderCapacity = 256;

    public static SonarRevealManager Instance { get; private set; }
    public static IReadOnlyCollection<Renderer> ActiveRenderers =>
        Instance != null ? Instance.activeRenderers.Keys : EmptyRenderers;
    public static float OutlineStrength => Instance != null ? Instance.outlineStrength : 0f;

    private static readonly Renderer[] EmptyRenderers = new Renderer[0];
    private static readonly HashSet<Renderer> SuppressedRenderers = new();
    private const int ExclusionCapacity = 16;
    private readonly Vector4[] excludedMin = new Vector4[ExclusionCapacity];
    private readonly Vector4[] excludedMax = new Vector4[ExclusionCapacity];
    private static readonly int ExcludedCountId = Shader.PropertyToID("_WaterSonarExcludedCount");
    private static readonly int ExcludedMinId = Shader.PropertyToID("_WaterSonarExcludedMin");
    private static readonly int ExcludedMaxId = Shader.PropertyToID("_WaterSonarExcludedMax");

    public static bool IsSuppressed(Renderer renderer) => renderer != null && SuppressedRenderers.Contains(renderer);

    public static void SetSuppressed(Renderer renderer, bool suppressed)
    {
        if (renderer == null) return;
        if (suppressed)
        {
            SuppressedRenderers.Add(renderer);
            // Picking up an already revealed item removes the outline immediately.
            if (Instance != null) { Instance.activeRenderers.Remove(renderer); Instance.surfaceLimits.Remove(renderer); }
        }
        else SuppressedRenderers.Remove(renderer);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetExclusions()
    {
        SuppressedRenderers.Clear(); Shader.SetGlobalInt(ExcludedCountId, 0);
    }

    private void LateUpdate()
    {
        int count = 0;
        foreach (var renderer in SuppressedRenderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy || count >= ExclusionCapacity) continue;
            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
            var bounds = renderer.bounds; bounds.Expand(.006f);
            excludedMin[count] = bounds.min; excludedMax[count] = bounds.max; count++;
        }
        Shader.SetGlobalInt(ExcludedCountId, count);
        if (count == 0) return;
        Shader.SetGlobalVectorArray(ExcludedMinId, excludedMin);
        Shader.SetGlobalVectorArray(ExcludedMaxId, excludedMax);
    }

    [Header("Reveal Target Filter")]
    [SerializeField] private LayerMask targetLayers = ~0;
    [SerializeField] private LayerMask ignoredLayers;
    [SerializeField] private bool ignoreGroundTag = true;
    [SerializeField] private string groundTag = "Ground";
    [SerializeField, Min(0.01f)] private float shellPadding = 0.08f;

    [Header("Unified Outline Fade")]
    [SerializeField, Min(0f)] private float outlineHoldDelay = 1.25f;
    [SerializeField, Min(0.01f)] private float outlineFadeDuration = 1.0f;

    private readonly Collider[] colliderBuffer = new Collider[ColliderCapacity];
    private readonly Dictionary<Renderer, float> activeRenderers = new Dictionary<Renderer, float>();
    private readonly Dictionary<Collider, Transform> colliderRoots = new();
    private readonly Dictionary<Transform, Renderer[]> rendererCache = new();
    private readonly HashSet<Transform> scannedRoots = new();
    private readonly Dictionary<int, WaterVolume> pulseWater = new();
    private readonly Dictionary<Renderer, float> surfaceLimits = new();
    private readonly List<Material> targetMaterials = new();
    public static float OutlineMaximumY(Renderer renderer) => Instance != null && Instance.surfaceLimits.TryGetValue(renderer, out float y) ? y : 1e20f;
    private float allPulsesEndedAt = float.PositiveInfinity;
    private float outlineStrength = 0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (FindFirstObjectByType<SonarRevealManager>() != null)
            return;

        GameObject system = new GameObject("Sonar Reveal Manager");
        system.AddComponent<SonarRevealManager>();
        DontDestroyOnLoad(system);
    }

    public static void RevealRenderer(Renderer renderer, float duration)
    {
        if (renderer == null || IsSuppressed(renderer))
            return;
        if (Instance == null)
            EnsureInstance();
        if (Instance == null)
            return;

        Instance.activeRenderers[renderer] = Time.unscaledTime + Mathf.Max(0.05f, duration);
        Instance.surfaceLimits[renderer] = 1e20f;
        Instance.outlineStrength = 1f;
        Instance.allPulsesEndedAt = float.PositiveInfinity;
    }

    public static void RevealRendererForPulse(Renderer renderer, float duration, VolumetricFogPulseEmitter.PulseState pulse)
    {
        if (renderer == null) return;
        if (Instance == null) EnsureInstance();
        if (Instance == null || !Instance.EligibleSurface(renderer, pulse, out float maxY)) return;
        Instance.activeRenderers[renderer] = Time.unscaledTime + Mathf.Max(.05f, duration);
        Instance.surfaceLimits[renderer] = maxY;
        Instance.outlineStrength = 1;
        Instance.allPulsesEndedAt = float.PositiveInfinity;
    }

    public void ClearReveals()
    {
        activeRenderers.Clear();
        colliderRoots.Clear(); rendererCache.Clear(); scannedRoots.Clear();
        pulseWater.Clear(); surfaceLimits.Clear();
        outlineStrength = 0;
        allPulsesEndedAt = float.PositiveInfinity;
    }

    private void OnEnable()
    {
        Instance = this;
        VolumetricFogPulseEmitter.PulseStarted += OnPulseStarted;
        VolumetricFogPulseEmitter.PulseUpdated += OnPulseUpdated;
        VolumetricFogPulseEmitter.PulseEnded += OnPulseEnded;
        VolumetricFogPulseEmitter.AllPulsesEnded += OnAllPulsesEnded;
    }

    private void OnDisable()
    {
        VolumetricFogPulseEmitter.PulseStarted -= OnPulseStarted;
        VolumetricFogPulseEmitter.PulseUpdated -= OnPulseUpdated;
        VolumetricFogPulseEmitter.PulseEnded -= OnPulseEnded;
        VolumetricFogPulseEmitter.AllPulsesEnded -= OnAllPulsesEnded;
        if (Instance == this)
        {
            Shader.SetGlobalInt(ExcludedCountId, 0);
            Instance = null;
        }
    }

    private void Update()
    {
        RemoveDestroyedOrExpiredRenderers();
        if (activeRenderers.Count == 0 || float.IsPositiveInfinity(allPulsesEndedAt))
            return;

        float elapsed = Time.unscaledTime - allPulsesEndedAt;
        if (elapsed < outlineHoldDelay)
            return;

        outlineStrength = 1f - Mathf.SmoothStep(0f, 1f,
            (elapsed - outlineHoldDelay) / outlineFadeDuration);
        if (outlineStrength > 0f)
            return;

        activeRenderers.Clear();
        allPulsesEndedAt = float.PositiveInfinity;
        surfaceLimits.Clear(); rendererCache.Clear(); colliderRoots.Clear();
    }

    private void OnPulseStarted(VolumetricFogPulseEmitter.PulseState pulse)
    {
        // Models can be replaced between pulses, but not rediscovered for every
        // collider on every frame of the same pulse.
        colliderRoots.Clear(); rendererCache.Clear();
        allPulsesEndedAt = float.PositiveInfinity;
        pulseWater[pulse.Id] = WaterVolume.BodyContaining(pulse.Origin);
        outlineStrength = 1f;
    }

    private void OnPulseUpdated(VolumetricFogPulseEmitter.PulseState pulse)
    {
        if (pulse.Radius <= 0f)
            return;

        float queryRadius = pulse.Radius + pulse.Width + shellPadding;
        scannedRoots.Clear();
        int count = Physics.OverlapSphereNonAlloc(
            pulse.Origin,
            queryRadius,
            colliderBuffer,
            targetLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider collider = colliderBuffer[i];
            colliderBuffer[i] = null;
            if (!IsEligible(collider) || !TouchesShell(collider, pulse))
                continue;

            // A static level commonly has one very large "Environment" root.
            // Using Transform.root here would outline the whole level when a
            // single rock is swept. Prefer the Rigidbody object for dynamic
            // props; otherwise use the closest renderer-bearing hierarchy.
            if (!colliderRoots.TryGetValue(collider, out Transform root) || root == null)
            { root = ResolveRendererRoot(collider); colliderRoots[collider] = root; }
            if (!scannedRoots.Add(root)) continue;
            if (!rendererCache.TryGetValue(root, out Renderer[] targets))
            { targets = root.GetComponentsInChildren<Renderer>(true); rendererCache[root] = targets; }
            foreach (Renderer renderer in targets)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;
                // A swept group collider is not proof that every child surface
                // was swept. Never enroll UI/particles/water helper lines either.
                if (!EligibleSurface(renderer, pulse, out float maxY)) continue;
                surfaceLimits[renderer] = maxY;
                if (activeRenderers.TryGetValue(renderer, out float expiry) && float.IsPositiveInfinity(expiry)) continue;

                DeepSeaAI.SonarRevealStyle style =
                    renderer.GetComponentInParent<DeepSeaAI.SonarRevealStyle>();
                activeRenderers[renderer] = style != null
                    ? Time.unscaledTime + style.RevealDuration
                    : float.PositiveInfinity;
            }
        }
    }

    private void OnPulseEnded(VolumetricFogPulseEmitter.PulseState pulse) => pulseWater.Remove(pulse.Id);

    private bool EligibleSurface(Renderer renderer, VolumetricFogPulseEmitter.PulseState pulse, out float maxY)
    {
        maxY = 1e20f;
        if (renderer == null || IsSuppressed(renderer) || !renderer.enabled || !renderer.gameObject.activeInHierarchy) return false;
        if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) return false;
        int layer = 1 << renderer.gameObject.layer;
        if ((targetLayers.value & layer) == 0 || (ignoredLayers.value & layer) != 0) return false;
        if (ignoreGroundTag && !string.IsNullOrEmpty(groundTag) && renderer.tag == groundTag) return false;
        if (!BoundsTouchShell(renderer.bounds, pulse.Origin, pulse.Radius, pulse.Width * .5f + shellPadding)) return false;
        renderer.GetSharedMaterials(targetMaterials);
        foreach (var material in targetMaterials)
            if (material != null && material.shader != null && material.shader.name.StartsWith("AbstractOcclusion/WebGpuWater/"))
                return false;
        if (!pulseWater.TryGetValue(pulse.Id, out var water))
        { water = WaterVolume.BodyContaining(pulse.Origin); pulseWater[pulse.Id] = water; }
        if (water != null && water.TryGetAnalyticWaterline(pulse.Origin.x, pulse.Origin.z, out float originSurface) && pulse.Origin.y < originSurface)
        {
            var center = renderer.bounds.center;
            if (!water.TryGetAnalyticWaterline(center.x, center.z, out maxY)) maxY = originSurface;
            maxY -= .03f;
            if (renderer.bounds.min.y >= maxY) return false;
            // Clip straddling meshes (platform supports) per fragment as well.
        }
        return true;
    }

    public static bool BoundsTouchShell(Bounds bounds, Vector3 origin, float radius, float padding)
    {
        float min = Vector3.Distance(bounds.ClosestPoint(origin), origin);
        Vector3 far = new Vector3(Mathf.Abs(bounds.center.x - origin.x), Mathf.Abs(bounds.center.y - origin.y), Mathf.Abs(bounds.center.z - origin.z)) + bounds.extents;
        return min <= radius + padding && far.magnitude >= radius - padding;
    }

    private static Transform ResolveRendererRoot(Collider collider)
    {
        if (collider.attachedRigidbody != null)
            return collider.attachedRigidbody.transform;

        Transform current = collider.transform;
        while (current.parent != null)
        {
            if (current.GetComponent<Renderer>() != null ||
                current.GetComponentInChildren<Renderer>(true) != null)
                return current;
            current = current.parent;
        }

        return collider.transform;
    }

    private void OnAllPulsesEnded()
    {
        if (activeRenderers.Count > 0)
            allPulsesEndedAt = Time.unscaledTime;
    }

    private bool IsEligible(Collider collider)
    {
        if (collider == null)
            return false;

        int layerMask = 1 << collider.gameObject.layer;
        if ((ignoredLayers.value & layerMask) != 0)
            return false;
        return !ignoreGroundTag || string.IsNullOrEmpty(groundTag) || collider.tag != groundTag;
    }

    private bool TouchesShell(Collider collider, VolumetricFogPulseEmitter.PulseState pulse)
    {
        float shellHalfWidth = pulse.Width * 0.5f + shellPadding;
        MeshCollider meshCollider = collider as MeshCollider;
        bool supportsClosestPoint =
            collider is BoxCollider ||
            collider is SphereCollider ||
            collider is CapsuleCollider ||
            (meshCollider != null && meshCollider.convex);

        if (supportsClosestPoint)
        {
            Vector3 closest = collider.ClosestPoint(pulse.Origin);
            float distanceToShell = Mathf.Abs(Vector3.Distance(closest, pulse.Origin) - pulse.Radius);
            if (distanceToShell <= shellHalfWidth)
                return true;
        }

        Bounds bounds = collider.bounds;
        float centerDistance = Vector3.Distance(bounds.center, pulse.Origin);
        return Mathf.Abs(centerDistance - pulse.Radius) <=
            bounds.extents.magnitude + shellHalfWidth;
    }

    private void RemoveDestroyedOrExpiredRenderers()
    {
        if (activeRenderers.Count == 0)
            return;

        List<Renderer> removed = null;
        float now = Time.unscaledTime;
        foreach (KeyValuePair<Renderer, float> pair in activeRenderers)
        {
            Renderer renderer = pair.Key;
            bool expired = !float.IsPositiveInfinity(pair.Value) && now >= pair.Value;
            if (renderer == null || IsSuppressed(renderer) || expired)
            {
                removed ??= new List<Renderer>();
                removed.Add(renderer);
            }
        }

        if (removed == null)
            return;
        foreach (Renderer renderer in removed)
        {
            activeRenderers.Remove(renderer);
            surfaceLimits.Remove(renderer);
        }
    }
}
