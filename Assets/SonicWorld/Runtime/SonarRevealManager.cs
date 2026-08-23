using System.Collections.Generic;
using UnityEngine;

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
    private readonly Dictionary<Renderer, byte> activeRenderers = new Dictionary<Renderer, byte>();
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

    private void OnEnable()
    {
        Instance = this;
        VolumetricFogPulseEmitter.PulseStarted += OnPulseStarted;
        VolumetricFogPulseEmitter.PulseUpdated += OnPulseUpdated;
        VolumetricFogPulseEmitter.AllPulsesEnded += OnAllPulsesEnded;
    }

    private void OnDisable()
    {
        VolumetricFogPulseEmitter.PulseStarted -= OnPulseStarted;
        VolumetricFogPulseEmitter.PulseUpdated -= OnPulseUpdated;
        VolumetricFogPulseEmitter.AllPulsesEnded -= OnAllPulsesEnded;
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        RemoveDestroyedRenderers();
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
    }

    private void OnPulseStarted(VolumetricFogPulseEmitter.PulseState pulse)
    {
        allPulsesEndedAt = float.PositiveInfinity;
        outlineStrength = 1f;
    }

    private void OnPulseUpdated(VolumetricFogPulseEmitter.PulseState pulse)
    {
        if (pulse.Radius <= 0f)
            return;

        float queryRadius = pulse.Radius + pulse.Width + shellPadding;
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
            Transform root = ResolveRendererRoot(collider);
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                    activeRenderers[renderer] = 0;
            }
        }
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
        Vector3 closest = collider.ClosestPoint(pulse.Origin);
        float shellHalfWidth = pulse.Width * 0.5f + shellPadding;
        float distanceToShell = Mathf.Abs(Vector3.Distance(closest, pulse.Origin) - pulse.Radius);
        if (distanceToShell <= shellHalfWidth)
            return true;

        Bounds bounds = collider.bounds;
        float centerDistance = Vector3.Distance(bounds.center, pulse.Origin);
        return Mathf.Abs(centerDistance - pulse.Radius) <= bounds.extents.magnitude + shellHalfWidth;
    }

    private void RemoveDestroyedRenderers()
    {
        if (activeRenderers.Count == 0)
            return;

        List<Renderer> removed = null;
        foreach (Renderer renderer in activeRenderers.Keys)
        {
            if (renderer == null)
            {
                removed ??= new List<Renderer>();
                removed.Add(renderer);
            }
        }

        if (removed == null)
            return;
        foreach (Renderer renderer in removed)
            activeRenderers.Remove(renderer);
    }
}
