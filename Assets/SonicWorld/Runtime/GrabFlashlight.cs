using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// XR-grabbable flashlight. Activate/Trigger toggles the light while selected.
/// Active lights are also uploaded to WebGPU Water so their cones locally
/// reduce underwater fog and depth darkening.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(XRGrabInteractable))]
public sealed class GrabFlashlight : MonoBehaviour
{
    private const int MaximumWaterLights = 4;
    private static readonly int CountId = Shader.PropertyToID("_WaterFlashlightCount");
    private static readonly int OriginsId = Shader.PropertyToID("_WaterFlashlightOrigins");
    private static readonly int DirectionsId = Shader.PropertyToID("_WaterFlashlightDirections");
    private static readonly int ParametersId = Shader.PropertyToID("_WaterFlashlightParameters");
    private static readonly List<GrabFlashlight> Registered = new();
    private static readonly Vector4[] Origins = new Vector4[MaximumWaterLights];
    private static readonly Vector4[] Directions = new Vector4[MaximumWaterLights];
    private static readonly Vector4[] Parameters = new Vector4[MaximumWaterLights];

    [Header("Light")]
    [SerializeField] private Transform beamOrigin;
    [SerializeField] private Light spotLight;
    [SerializeField] private GameObject beamVisual;
    [SerializeField] private bool startsOn;
    [SerializeField] private bool turnOffOnDrop;
    [SerializeField] private Color lightColor = new(0.78f, 0.92f, 1f, 1f);
    [SerializeField, Min(0.1f)] private float range = 8f;
    [SerializeField, Range(5f, 80f)] private float spotAngle = 34f;
    [SerializeField, Min(0f)] private float intensity = 5f;
    [SerializeField] private bool castShadows;

    [Header("Visible Beam")]
    [SerializeField] private Shader beamShader;
    [SerializeField, Range(0f, 1f)] private float beamOpacity = 0.18f;

    [Header("Water Visibility")]
    [SerializeField, Range(0f, 1f)] private float waterFogClearStrength = 0.9f;
    [SerializeField, Range(0.5f, 15f)] private float coneEdgeSoftnessDegrees = 4f;

    [Header("Events")]
    [SerializeField] private UnityEvent<bool> onLightChanged = new();

    private XRGrabInteractable grab;
    private bool isOn;
    private Material runtimeBeamMaterial;
    private Mesh runtimeBeamMesh;
    private MaterialPropertyBlock beamProperties;

    public bool IsOn => isOn;
    public float Range => range;
    public UnityEvent<bool> OnLightChanged => onLightChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Registered.Clear();
        Shader.SetGlobalInt(CountId, 0);
    }

    private void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        EnsureVisuals();
        SetLight(startsOn, false);
    }

    private void OnEnable()
    {
        if (grab == null)
            grab = GetComponent<XRGrabInteractable>();
        grab.activated.AddListener(OnActivated);
        grab.selectExited.AddListener(OnSelectExited);
        if (!Registered.Contains(this))
            Registered.Add(this);
        UploadWaterLights();
    }

    private void OnDisable()
    {
        if (grab != null)
        {
            grab.activated.RemoveListener(OnActivated);
            grab.selectExited.RemoveListener(OnSelectExited);
        }
        Registered.Remove(this);
        if (spotLight != null)
            spotLight.enabled = false;
        if (beamVisual != null)
            beamVisual.SetActive(false);
        UploadWaterLights();
    }

    private void LateUpdate()
    {
        if (Registered.Count > 0 && Registered[0] == this)
            UploadWaterLights();
    }

    private void OnDestroy()
    {
        if (runtimeBeamMaterial != null)
            Destroy(runtimeBeamMaterial);
        if (runtimeBeamMesh != null)
            Destroy(runtimeBeamMesh);
    }

    public void ToggleLight()
    {
        SetLight(!isOn);
    }

    public void SetLight(bool enabled)
    {
        SetLight(enabled, true);
    }

    private void SetLight(bool enabled, bool notify)
    {
        isOn = enabled;
        ApplyVisualSettings();
        UploadWaterLights();
        if (notify)
            onLightChanged?.Invoke(isOn);
    }

    private void OnActivated(ActivateEventArgs args)
    {
        if (grab != null && grab.isSelected)
            ToggleLight();
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        if (turnOffOnDrop)
            SetLight(false);
    }

    private void EnsureVisuals()
    {
        if (beamOrigin == null)
            beamOrigin = transform;

        if (spotLight == null)
        {
            GameObject lightObject = new("Spot Light");
            lightObject.transform.SetParent(beamOrigin, false);
            spotLight = lightObject.AddComponent<Light>();
        }
        spotLight.type = LightType.Spot;

        if (beamVisual == null)
        {
            GameObject beam = new("Visible Water Beam");
            beam.transform.SetParent(beamOrigin, false);
            MeshFilter filter = beam.AddComponent<MeshFilter>();
            MeshRenderer renderer = beam.AddComponent<MeshRenderer>();
            runtimeBeamMesh = CreateConeMesh(24);
            filter.sharedMesh = runtimeBeamMesh;
            Shader shader = beamShader != null
                ? beamShader
                : Shader.Find("Hidden/Sonar/Quest Flashlight Beam");
            if (shader != null)
            {
                runtimeBeamMaterial = new Material(shader)
                {
                    name = "Runtime Quest Flashlight Beam",
                    hideFlags = HideFlags.DontSave
                };
                renderer.sharedMaterial = runtimeBeamMaterial;
            }
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            beamVisual = beam;
        }
    }

    private void ApplyVisualSettings()
    {
        if (spotLight != null)
        {
            spotLight.enabled = isOn;
            spotLight.color = lightColor;
            spotLight.range = range;
            spotLight.spotAngle = spotAngle;
            spotLight.intensity = intensity;
            spotLight.shadows = castShadows ? LightShadows.Soft : LightShadows.None;
        }

        if (beamVisual != null)
        {
            beamVisual.SetActive(isOn && beamOpacity > 0.001f);
            float radius = Mathf.Tan(spotAngle * 0.5f * Mathf.Deg2Rad) * range;
            beamVisual.transform.localScale = new Vector3(radius, radius, range);
            Renderer renderer = beamVisual.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null)
            {
                Color beamColor = lightColor;
                beamColor.a = beamOpacity;
                beamProperties ??= new MaterialPropertyBlock();
                renderer.GetPropertyBlock(beamProperties);
                beamProperties.SetColor("_BeamColor", beamColor);
                renderer.SetPropertyBlock(beamProperties);
            }
        }
    }

    private static void UploadWaterLights()
    {
        for (int i = Registered.Count - 1; i >= 0; i--)
        {
            if (Registered[i] == null)
                Registered.RemoveAt(i);
        }

        int count = 0;
        for (int i = 0; i < Registered.Count && count < MaximumWaterLights; i++)
        {
            GrabFlashlight light = Registered[i];
            if (!light.isActiveAndEnabled || !light.isOn)
                continue;

            Transform source = light.beamOrigin != null ? light.beamOrigin : light.transform;
            Vector3 direction = source.forward.normalized;
            float outerAngle = Mathf.Clamp(light.spotAngle * 0.5f, 1f, 89f);
            float innerAngle = Mathf.Max(0.1f, outerAngle - light.coneEdgeSoftnessDegrees);
            Origins[count] = new Vector4(source.position.x, source.position.y, source.position.z, light.range);
            Directions[count] = new Vector4(direction.x, direction.y, direction.z, light.waterFogClearStrength);
            Parameters[count] = new Vector4(
                Mathf.Cos(innerAngle * Mathf.Deg2Rad),
                Mathf.Cos(outerAngle * Mathf.Deg2Rad),
                0.82f,
                0f);
            count++;
        }

        Shader.SetGlobalInt(CountId, count);
        if (count > 0)
        {
            Shader.SetGlobalVectorArray(OriginsId, Origins);
            Shader.SetGlobalVectorArray(DirectionsId, Directions);
            Shader.SetGlobalVectorArray(ParametersId, Parameters);
        }
    }

    private static Mesh CreateConeMesh(int segments)
    {
        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 6];
        vertices[0] = Vector3.zero;
        for (int i = 0; i < segments; i++)
        {
            float angle = Mathf.PI * 2f * i / segments;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 1f);
        }
        vertices[segments + 1] = Vector3.forward;

        int cursor = 0;
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            triangles[cursor++] = 0;
            triangles[cursor++] = i + 1;
            triangles[cursor++] = next + 1;
            triangles[cursor++] = segments + 1;
            triangles[cursor++] = next + 1;
            triangles[cursor++] = i + 1;
        }

        Mesh mesh = new() { name = "Quest Flashlight Cone" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void OnValidate()
    {
        range = Mathf.Max(0.1f, range);
        spotAngle = Mathf.Clamp(spotAngle, 5f, 80f);
        intensity = Mathf.Max(0f, intensity);
        beamOpacity = Mathf.Clamp01(beamOpacity);
        waterFogClearStrength = Mathf.Clamp01(waterFogClearStrength);
        coneEdgeSoftnessDegrees = Mathf.Clamp(coneEdgeSoftnessDegrees, 0.5f, 15f);
        if (Application.isPlaying)
            ApplyVisualSettings();
    }
}
