using System;
using Unity.XR.CoreUtils;
using UnityEngine;

/// <summary>
/// Cross-fades a camera-relative underwater current loop from the shared water
/// state. If no production clip is assigned, a deterministic low-frequency
/// placeholder is generated once so the feature can be tested immediately.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(WaterSurfaceStateTracker))]
[RequireComponent(typeof(AudioSource))]
public sealed class UnderwaterAmbienceController : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioClip underwaterLoop;
    [SerializeField, Range(0f, 1f)] private float surfaceVolume = 0.55f;
    [SerializeField, Range(0f, 1f)] private float deepWaterMinimumVolume = 0.14f;
    [SerializeField, Min(0.1f)] private float depthForMinimumVolume = 20f;
    [SerializeField, Min(0.01f)] private float fadeInSeconds = 1.2f;
    [SerializeField, Min(0.01f)] private float fadeOutSeconds = 0.8f;
    [SerializeField] private bool generatePlaceholderWhenEmpty = true;

    [Header("Water Flow Modulation")]
    [SerializeField, Range(0f, 0.5f)] private float flowVolumeInfluence = 0.12f;
    [SerializeField] private Vector2 pitchRange = new(0.92f, 1.08f);
    [SerializeField, Min(0.01f)] private float flowSpeedForMaximumPitch = 3f;

    private WaterSurfaceStateTracker waterState;
    private AudioSource source;
    private AudioClip generatedClip;

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
            if (origin.GetComponent<UnderwaterAmbienceController>() == null)
                origin.gameObject.AddComponent<UnderwaterAmbienceController>();
        }
    }

    private void Awake()
    {
        waterState = GetComponent<WaterSurfaceStateTracker>();
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.dopplerLevel = 0f;

        AudioClip clip = underwaterLoop;
        if (clip == null && generatePlaceholderWhenEmpty)
        {
            generatedClip = CreatePlaceholderCurrentLoop();
            clip = generatedClip;
        }
        source.clip = clip;
        if (clip != null)
            source.Play();
    }

    private void Update()
    {
        if (source == null)
            return;

        float flow01 = waterState != null
            ? Mathf.Clamp01(waterState.WaterFlow.magnitude / flowSpeedForMaximumPitch)
            : 0f;
        float depth01 = waterState != null && waterState.IsUnderwater
            ? Mathf.Clamp01(waterState.SignedDepth / depthForMinimumVolume)
            : 0f;
        float baseVolume = waterState != null && waterState.IsUnderwater
            ? Mathf.Lerp(surfaceVolume, deepWaterMinimumVolume, depth01)
            : surfaceVolume;
        float target = Mathf.Clamp01(baseVolume + flow01 * flowVolumeInfluence);
        float fade = target > source.volume ? fadeInSeconds : fadeOutSeconds;
        source.volume = Mathf.MoveTowards(source.volume, target, Time.unscaledDeltaTime / fade);
        source.pitch = Mathf.Lerp(pitchRange.x, pitchRange.y, flow01);
    }

    private void OnDestroy()
    {
        if (generatedClip != null)
            Destroy(generatedClip);
    }

    private static AudioClip CreatePlaceholderCurrentLoop()
    {
        const int sampleRate = 22050;
        const int seconds = 8;
        int count = sampleRate * seconds;
        float[] samples = new float[count];
        System.Random random = new(17191);
        float filtered = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float noise = (float)(random.NextDouble() * 2.0 - 1.0);
            filtered = Mathf.Lerp(filtered, noise, 0.012f);
            float slow = Mathf.Sin(t * Mathf.PI * 2f * 0.17f) * 0.12f;
            float swell = 0.72f + 0.28f * Mathf.Sin(t * Mathf.PI * 2f / seconds);
            samples[i] = Mathf.Clamp((filtered * 0.55f + slow) * swell, -0.8f, 0.8f);
        }

        AudioClip clip = AudioClip.Create(
            "Temporary Underwater Current", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void OnValidate()
    {
        surfaceVolume = Mathf.Clamp01(surfaceVolume);
        deepWaterMinimumVolume = Mathf.Clamp(deepWaterMinimumVolume, 0f, surfaceVolume);
        depthForMinimumVolume = Mathf.Max(0.1f, depthForMinimumVolume);
        fadeInSeconds = Mathf.Max(0.01f, fadeInSeconds);
        fadeOutSeconds = Mathf.Max(0.01f, fadeOutSeconds);
        flowSpeedForMaximumPitch = Mathf.Max(0.01f, flowSpeedForMaximumPitch);
        if (pitchRange.y < pitchRange.x)
            pitchRange.y = pitchRange.x;
    }
}
