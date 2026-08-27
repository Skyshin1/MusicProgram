using System;
using AbstractOcclusion.WebGpuWater;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Samples the tracked headset against WebGPU Water and publishes stable
/// enter/exit events. The hysteresis prevents waves from toggling the state
/// every frame while the player is at the waterline.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(XROrigin))]
[DefaultExecutionOrder(-100)]
public sealed class WaterSurfaceStateTracker : MonoBehaviour
{
    [Serializable] public sealed class WaterEvent : UnityEvent { }

    [Header("Tracked Point")]
    [SerializeField] private Transform probe;
    [SerializeField, Min(0f)] private float enterDepth = 0.1f;
    [SerializeField, Min(0f)] private float exitHeight = 0.1f;

    [Header("Events")]
    [SerializeField] private WaterEvent onEnteredWater = new();
    [SerializeField] private WaterEvent onExitedWater = new();

    private XROrigin xrOrigin;
    private bool initialized;
    private float signedDepth;
    private Vector3 waterFlow;

    public bool IsUnderwater { get; private set; }
    public WaterVolume CurrentWater { get; private set; }
    public float SignedDepth => signedDepth;
    public Vector3 WaterFlow => waterFlow;
    public Transform Probe => ResolveProbe();

    public event Action<WaterVolume> EnteredWater;
    public event Action<WaterVolume> ExitedWater;

    private void Awake()
    {
        xrOrigin = GetComponent<XROrigin>();
    }

    private void Start()
    {
        Sample(false);
        initialized = true;
    }

    private void Update()
    {
        Sample(initialized);
        initialized = true;
    }

    public bool TryRefreshNow()
    {
        return Sample(initialized);
    }

    private bool Sample(bool publishChanges)
    {
        Transform activeProbe = ResolveProbe();
        if (activeProbe == null)
            return false;

        Vector3 point = activeProbe.position;
        WaterVolume body = WaterVolume.BodyContaining(point);
        if (body == null)
        {
            UpdateState(false, null, publishChanges);
            signedDepth = float.NegativeInfinity;
            waterFlow = Vector3.zero;
            return false;
        }

        if (!body.TrySampleSubmersion(point, out float depth, out _, out Vector3 flow))
        {
            if (!body.TryGetAnalyticWaterline(point.x, point.z, out float waterline))
                return false;
            depth = waterline - point.y;
            flow = Vector3.zero;
        }

        signedDepth = depth;
        waterFlow = flow;
        bool underwater = IsUnderwater
            ? depth > -exitHeight
            : depth > enterDepth;
        UpdateState(underwater, body, publishChanges);
        return true;
    }

    private void UpdateState(bool underwater, WaterVolume body, bool publishChanges)
    {
        bool changed = underwater != IsUnderwater;
        WaterVolume previous = CurrentWater;
        IsUnderwater = underwater;
        CurrentWater = body;
        if (!changed || !publishChanges)
            return;

        if (underwater)
        {
            EnteredWater?.Invoke(body);
            onEnteredWater?.Invoke();
        }
        else
        {
            ExitedWater?.Invoke(previous != null ? previous : body);
            onExitedWater?.Invoke();
        }
    }

    private Transform ResolveProbe()
    {
        if (probe != null)
            return probe;
        if (xrOrigin == null)
            xrOrigin = GetComponent<XROrigin>();
        return xrOrigin != null && xrOrigin.Camera != null
            ? xrOrigin.Camera.transform
            : transform;
    }

    private void OnValidate()
    {
        enterDepth = Mathf.Max(0f, enterDepth);
        exitHeight = Mathf.Max(0f, exitHeight);
    }
}
