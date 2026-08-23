using System.Collections.Generic;
using UnityEngine;

/// <summary>Draws pooled, depth-tested white wire spheres for every sonar pulse.</summary>
[DisallowMultipleComponent]
public sealed class SonarWaveVisualSystem : MonoBehaviour
{
    private sealed class WaveVisual
    {
        public GameObject Root;
        public LineRenderer[] Lines;
    }

    [Header("Visibility")]
    [Tooltip("Only controls the white wire-sphere drawing. Turning this off does not disable sonar pulses, fog interaction, hit detection, or white outlines.")]
    [SerializeField] private bool showWireSphere = false;

    [Header("Wire Sphere")]
    [SerializeField, Range(3, 16)] private int latitudeLines = 7;
    [SerializeField, Range(4, 20)] private int longitudeLines = 10;
    [SerializeField, Range(16, 96)] private int segmentsPerLine = 48;
    [SerializeField, Range(0.002f, 0.08f)] private float lineWidth = 0.014f;
    [SerializeField] private Color waveColor = Color.white;

    private readonly Dictionary<int, WaveVisual> active = new Dictionary<int, WaveVisual>();
    private readonly Stack<WaveVisual> pool = new Stack<WaveVisual>();
    private Material lineMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (FindFirstObjectByType<SonarWaveVisualSystem>() != null)
            return;

        GameObject system = new GameObject("Sonar Wave Visual System");
        system.AddComponent<SonarWaveVisualSystem>();
        DontDestroyOnLoad(system);
    }

    private void OnEnable()
    {
        VolumetricFogPulseEmitter.PulseStarted += OnPulseStarted;
        VolumetricFogPulseEmitter.PulseUpdated += OnPulseUpdated;
        VolumetricFogPulseEmitter.PulseEnded += OnPulseEnded;
    }

    private void OnDisable()
    {
        VolumetricFogPulseEmitter.PulseStarted -= OnPulseStarted;
        VolumetricFogPulseEmitter.PulseUpdated -= OnPulseUpdated;
        VolumetricFogPulseEmitter.PulseEnded -= OnPulseEnded;
    }

    private void OnPulseStarted(VolumetricFogPulseEmitter.PulseState pulse)
    {
        if (!showWireSphere)
            return;

        WaveVisual visual = pool.Count > 0 ? pool.Pop() : CreateVisual();
        visual.Root.SetActive(true);
        active[pulse.Id] = visual;
        Apply(visual, pulse);
    }

    private void OnPulseUpdated(VolumetricFogPulseEmitter.PulseState pulse)
    {
        if (active.TryGetValue(pulse.Id, out WaveVisual visual))
            Apply(visual, pulse);
    }

    private void OnPulseEnded(VolumetricFogPulseEmitter.PulseState pulse)
    {
        if (!active.Remove(pulse.Id, out WaveVisual visual))
            return;
        visual.Root.SetActive(false);
        pool.Push(visual);
    }

    private void OnValidate()
    {
        if (!showWireSphere)
            HideAllVisuals();
    }

    /// <summary>Shows or hides only the wire-sphere rendering; sonar gameplay remains active.</summary>
    public void SetWireSphereVisible(bool visible)
    {
        showWireSphere = visible;
        if (!visible)
            HideAllVisuals();
    }

    private void HideAllVisuals()
    {
        foreach (WaveVisual visual in active.Values)
        {
            if (visual.Root != null)
                visual.Root.SetActive(false);
            pool.Push(visual);
        }

        active.Clear();
    }

    private WaveVisual CreateVisual()
    {
        if (lineMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            lineMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
        }

        int lineCount = latitudeLines + longitudeLines;
        GameObject root = new GameObject("Sonar Wire Sphere") { hideFlags = HideFlags.DontSave };
        LineRenderer[] lines = new LineRenderer[lineCount];
        for (int i = 0; i < lineCount; i++)
        {
            GameObject lineObject = new GameObject("Line") { hideFlags = HideFlags.DontSave };
            lineObject.transform.SetParent(root.transform, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.material = lineMaterial;
            line.widthMultiplier = lineWidth;
            line.positionCount = segmentsPerLine;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.startColor = waveColor;
            line.endColor = waveColor;
            lines[i] = line;
        }

        BuildLines(lines);
        root.SetActive(false);
        return new WaveVisual { Root = root, Lines = lines };
    }

    private void Apply(WaveVisual visual, VolumetricFogPulseEmitter.PulseState pulse)
    {
        visual.Root.transform.position = pulse.Origin;
        visual.Root.transform.localScale = Vector3.one * Mathf.Max(0.001f, pulse.Radius);
        Color color = waveColor;
        color.a *= pulse.Strength * pulse.EndFade;
        foreach (LineRenderer line in visual.Lines)
        {
            line.widthMultiplier = lineWidth;
            line.startColor = color;
            line.endColor = color;
        }
    }

    private void BuildLines(LineRenderer[] lines)
    {
        int line = 0;
        for (int latitude = 1; latitude <= latitudeLines; latitude++)
        {
            float polar = Mathf.PI * latitude / (latitudeLines + 1f);
            SetCircle(lines[line++], Vector3.up * Mathf.Cos(polar), Mathf.Sin(polar), false);
        }

        for (int longitude = 0; longitude < longitudeLines; longitude++)
        {
            float angle = Mathf.PI * 2f * longitude / longitudeLines;
            SetCircle(lines[line++], Vector3.zero, 1f, true, angle);
        }
    }

    private void SetCircle(LineRenderer line, Vector3 center, float radius, bool meridian, float angle = 0f)
    {
        for (int i = 0; i < segmentsPerLine; i++)
        {
            float theta = Mathf.PI * 2f * i / segmentsPerLine;
            Vector3 position = meridian
                ? new Vector3(Mathf.Sin(theta) * Mathf.Cos(angle), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(angle))
                : center + new Vector3(Mathf.Cos(theta) * radius, 0f, Mathf.Sin(theta) * radius);
            line.SetPosition(i, position);
        }
    }
}
