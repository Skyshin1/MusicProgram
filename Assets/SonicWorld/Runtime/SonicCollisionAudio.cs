using System.Collections.Generic;
using UnityEngine;

namespace SonicWorld
{
    [DisallowMultipleComponent]
    public sealed class SonicCollisionAudio : MonoBehaviour
    {
        private sealed class Voice
        {
            public readonly GameObject Root;
            public readonly AudioSource First;
            public readonly AudioSource Second;
            public readonly AudioSource Fusion;
            public readonly AudioLowPassFilter FirstFilter;
            public readonly AudioLowPassFilter SecondFilter;
            public readonly AudioLowPassFilter FusionFilter;
            public AudioClip GeneratedFusionClip;

            public Voice(Transform parent, int index)
            {
                Root = new GameObject($"Impact Voice {index + 1}");
                Root.transform.SetParent(parent, false);
                First = CreateSource(Root.transform, "A", out FirstFilter);
                Second = CreateSource(Root.transform, "B", out SecondFilter);
                Fusion = CreateSource(Root.transform, "Fusion", out FusionFilter);
            }

            public void Prepare(Vector3 position)
            {
                First.Stop();
                Second.Stop();
                Fusion.Stop();
                Root.transform.position = position;
                if (GeneratedFusionClip != null)
                {
                    Object.Destroy(GeneratedFusionClip);
                    GeneratedFusionClip = null;
                }
            }

            private static AudioSource CreateSource(
                Transform parent,
                string name,
                out AudioLowPassFilter lowPass)
            {
                GameObject child = new GameObject(name);
                child.transform.SetParent(parent, false);
                AudioSource source = child.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0.92f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.minDistance = 0.35f;
                source.maxDistance = 18f;
                source.dopplerLevel = 0.2f;
                lowPass = child.AddComponent<AudioLowPassFilter>();
                return source;
            }
        }

        public static SonicCollisionAudio Instance { get; private set; }

        [SerializeField] private SonicSurfaceProfile[] profiles;
        [SerializeField, Range(4, 24)] private int voiceCount = 12;
        [SerializeField, Range(0f, 1f)] private float impactVolume = 0.82f;
        [SerializeField, Range(0f, 1f)] private float fusionVolume = 0.38f;

        private readonly Dictionary<SonicSurfaceType, SonicSurfaceProfile> profileMap =
            new Dictionary<SonicSurfaceType, SonicSurfaceProfile>();
        private readonly List<Voice> voices = new List<Voice>();
        private int nextVoice;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            RebuildProfileMap();
            for (int i = 0; i < voiceCount; i++)
                voices.Add(new Voice(transform, i));
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public SonicSurfaceProfile GetProfile(SonicSurfaceType surface)
        {
            profileMap.TryGetValue(surface, out SonicSurfaceProfile profile);
            return profile;
        }

        public void PlayCollision(
            Vector3 position,
            SonicSurfaceProfile firstProfile,
            SonicSoundResult firstResult,
            SonicSurfaceProfile secondProfile,
            SonicSoundResult secondResult,
            Transform firstSource,
            Transform secondSource)
        {
            if (firstProfile == null || firstProfile.impactClip == null)
                return;

            Voice voice = AcquireVoice(position);
            if (secondProfile == null ||
                secondProfile.impactClip == null ||
                firstProfile.surfaceType == secondProfile.surfaceType)
            {
                SonicSoundResult combined = secondProfile != null
                    ? CombineSameSurface(firstResult, secondResult)
                    : firstResult;
                PlayLayer(voice.First, voice.FirstFilter, firstProfile.impactClip, combined, impactVolume);
                SonicAudioBus.Instance?.ReportTransient(
                    position,
                    combined.Energy,
                    firstProfile.surfaceType,
                    SonicSoundEventKind.Collision,
                    firstSource,
                    secondSource,
                    GetBands(combined));
                return;
            }

            PlayLayer(voice.First, voice.FirstFilter, firstProfile.impactClip, firstResult, impactVolume);
            PlayLayer(voice.Second, voice.SecondFilter, secondProfile.impactClip, secondResult, impactVolume);

            SonicSoundResult fused = SonicSoundResult.Fuse(firstResult, secondResult);
            voice.GeneratedFusionClip = CreateFusionClip(fused, firstProfile.noise, secondProfile.noise);
            PlayLayer(voice.Fusion, voice.FusionFilter, voice.GeneratedFusionClip, fused, fusionVolume);
            SonicAudioBus.Instance?.ReportTransient(
                position,
                Mathf.Max(firstResult.Energy, secondResult.Energy),
                firstProfile.surfaceType,
                SonicSoundEventKind.Collision,
                firstSource,
                secondSource,
                GetBands(fused));
        }

        public void PlaySolo(
            Vector3 position,
            SonicSurfaceProfile profile,
            float averageSpeed,
            Transform sourceTransform)
        {
            if (profile == null || profile.impactClip == null)
                return;

            SonicSoundResult result = profile.Evaluate(averageSpeed, averageSpeed, 1f);
            Voice voice = AcquireVoice(position);
            PlayLayer(voice.First, voice.FirstFilter, profile.impactClip, result, impactVolume * 0.72f);
            SonicAudioBus.Instance?.ReportTransient(
                position,
                result.Energy,
                profile.surfaceType,
                SonicSoundEventKind.Swing,
                sourceTransform,
                null,
                GetBands(result));
        }

        private static Vector3 GetBands(SonicSoundResult result)
        {
            float normalizedPitch = Mathf.Clamp01(
                Mathf.Log10(Mathf.Max(40f, result.Resonance) / 40f) /
                Mathf.Log10(12000f / 40f));
            float low = Mathf.Clamp01(1f - normalizedPitch * 1.45f);
            float high = Mathf.Clamp01((normalizedPitch - 0.38f) * 1.7f) *
                Mathf.Lerp(0.55f, 1.2f, result.Brightness);
            float mid = Mathf.Clamp01(1f - Mathf.Abs(normalizedPitch - 0.46f) * 2f);
            return new Vector3(low + 0.08f, mid + 0.08f, high + 0.08f);
        }

        private Voice AcquireVoice(Vector3 position)
        {
            if (voices.Count == 0)
                voices.Add(new Voice(transform, 0));

            Voice voice = voices[nextVoice++ % voices.Count];
            voice.Prepare(position);
            return voice;
        }

        private static void PlayLayer(
            AudioSource source,
            AudioLowPassFilter filter,
            AudioClip clip,
            SonicSoundResult result,
            float masterGain)
        {
            source.clip = clip;
            source.pitch = result.Pitch;
            source.volume = Mathf.Clamp01(result.Gain * masterGain);
            filter.cutoffFrequency = Mathf.Clamp(result.LowPass, 100f, 22000f);
            source.Play();
        }

        private static SonicSoundResult CombineSameSurface(
            SonicSoundResult first,
            SonicSoundResult second)
        {
            return new SonicSoundResult(
                (first.Pitch + second.Pitch) * 0.5f,
                Mathf.Clamp01(Mathf.Max(first.Gain, second.Gain)),
                Mathf.Max(first.LowPass, second.LowPass),
                Mathf.Max(first.Tail, second.Tail),
                (first.Resonance + second.Resonance) * 0.5f,
                Mathf.Max(first.Brightness, second.Brightness),
                Mathf.Max(first.Energy, second.Energy));
        }

        private static AudioClip CreateFusionClip(
            SonicSoundResult result,
            float firstNoise,
            float secondNoise)
        {
            int sampleRate = AudioSettings.outputSampleRate;
            float duration = Mathf.Lerp(0.12f, 0.62f, Mathf.Clamp01(result.Tail));
            int sampleCount = Mathf.Max(64, Mathf.CeilToInt(duration * sampleRate));
            float[] samples = new float[sampleCount];
            float phase = 0f;
            float overtonePhase = 0f;
            float noiseAmount = (firstNoise + secondNoise) * 0.12f;
            uint randomState = (uint)(Time.frameCount * 747796405 + sampleCount);

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float attack = 1f - Mathf.Exp(-time * 420f);
                float envelope = attack * Mathf.Exp(-time * Mathf.Lerp(4f, 1.3f, result.Tail));
                phase += Mathf.PI * 2f * result.Resonance / sampleRate;
                overtonePhase += Mathf.PI * 2f * result.Resonance * (1.7f + result.Brightness) / sampleRate;
                randomState = randomState * 1664525u + 1013904223u;
                float noise = ((randomState >> 9) / 8388607f) * 2f - 1f;
                samples[i] =
                    (Mathf.Sin(phase) * 0.64f +
                     Mathf.Sin(overtonePhase) * result.Brightness * 0.22f +
                     noise * noiseAmount * Mathf.Exp(-time * 18f)) *
                    envelope * result.Gain;
            }

            AudioClip clip = AudioClip.Create(
                $"Fusion {result.Resonance:0}Hz",
                sampleCount,
                1,
                sampleRate,
                false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void RebuildProfileMap()
        {
            profileMap.Clear();
            if (profiles == null)
                return;

            foreach (SonicSurfaceProfile profile in profiles)
            {
                if (profile != null)
                    profileMap[profile.surfaceType] = profile;
            }
        }
    }
}
