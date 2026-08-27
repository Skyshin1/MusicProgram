using UnityEngine;

/// <summary>
/// Clears WebGPU Water's underwater fog and depth darkening inside a
/// configurable, forward-facing horizontal cylinder. It intentionally does
/// not alter sonar hits or white outlines. The legacy component name remains
/// so configured player prefabs stay valid.
/// </summary>
[DisallowMultipleComponent]
public sealed class SonarFogLantern : MonoBehaviour
{
    private static readonly int EnabledId = Shader.PropertyToID("_WaterSonarLanternEnabled");
    private static readonly int PositionId = Shader.PropertyToID("_WaterSonarLanternPosition");
    private static readonly int ForwardId = Shader.PropertyToID("_WaterSonarLanternForward");
    private static readonly int ShapeId = Shader.PropertyToID("_WaterSonarLanternShape");
    private static readonly int HeightId = Shader.PropertyToID("_WaterSonarLanternHeight");
    private static readonly int LegacyEnabledId = Shader.PropertyToID("_SonarFogLanternEnabled");

    [Header("Follow Target")]
    [SerializeField]
    [Tooltip("In VR, use the XR Origin's tracked camera pose so the light follows the actual player/headset position and facing direction.")]
    private bool followMainCamera = true;
    [SerializeField]
    [Tooltip("Optional explicit source. When set, it takes priority over Follow Main Camera.")]
    private Transform origin;

    [Header("Water Visibility Cylinder")]
    [SerializeField] private bool activeLantern = true;
    [SerializeField, Min(0f)] private float forwardOffset = 1f;
    [SerializeField, Min(0.01f)] private float radius = 1f;
    [SerializeField, Min(0.001f)] private float edgeFadeWidth = 0.45f;
    [SerializeField] private float bottomOffset = -0.9f;
    [SerializeField, Min(0.01f)] private float height = 2.4f;
    [SerializeField, Range(0f, 1f)] private float visibilityStrength = 1f;

    private void OnEnable()
    {
        // Do not let a retained global from an earlier Fast Enter Play session
        // keep the old volumetric-fog lantern alive.
        Shader.SetGlobalFloat(LegacyEnabledId, 0f);
        Upload();
    }
    private void LateUpdate() => Upload();

    private void OnDisable()
    {
        Shader.SetGlobalFloat(EnabledId, 0f);
        Shader.SetGlobalFloat(LegacyEnabledId, 0f);
    }

    private void Upload()
    {
        Transform source = ResolveSource();
        Vector3 forward = Vector3.ProjectOnPlane(source.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;

        Shader.SetGlobalFloat(EnabledId, activeLantern ? 1f : 0f);
        Shader.SetGlobalVector(PositionId, source.position);
        Shader.SetGlobalVector(ForwardId, forward);
        Shader.SetGlobalVector(ShapeId, new Vector4(
            forwardOffset, radius, edgeFadeWidth, visibilityStrength));
        Shader.SetGlobalVector(HeightId, new Vector4(bottomOffset, height, 0f, 0f));
    }

    private Transform ResolveSource()
    {
        if (origin != null)
            return origin;

        if (followMainCamera)
        {
            Transform playerView = VolumetricFogPulseEmitter.FindPlayerViewTransform();
            if (playerView != null)
                return playerView;
        }

        return transform;
    }

    private void OnValidate()
    {
        radius = Mathf.Max(0.01f, radius);
        edgeFadeWidth = Mathf.Max(0.001f, edgeFadeWidth);
        height = Mathf.Max(0.01f, height);
        visibilityStrength = Mathf.Clamp01(visibilityStrength);
    }
}
