using System;
using System.Collections;
using UnityEngine;

namespace SonicWorld
{
    [DisallowMultipleComponent]
    public sealed class SonicAudioBus : MonoBehaviour
    {
        public readonly struct Frame
        {
            public readonly float Loudness;
            public readonly float Peak;
            public readonly float Bass;
            public readonly float Mid;
            public readonly float Treble;
            public readonly float Centroid;
            public readonly float DominantFrequency;
            public readonly float Pulse;
            public readonly Color ReactiveColor;

            public Frame(
                float loudness,
                float peak,
                float bass,
                float mid,
                float treble,
                float centroid,
                float dominantFrequency,
                float pulse,
                Color reactiveColor)
            {
                Loudness = loudness;
                Peak = peak;
                Bass = bass;
                Mid = mid;
                Treble = treble;
                Centroid = centroid;
                DominantFrequency = dominantFrequency;
                Pulse = pulse;
                ReactiveColor = reactiveColor;
            }
        }

        public static SonicAudioBus Instance { get; private set; }

        [SerializeField, Range(0.1f, 20f)] private float inputGain = 5f;
        [SerializeField, Range(1f, 30f)] private float attack = 18f;
        [SerializeField, Range(1f, 20f)] private float release = 5f;
        [SerializeField, Range(0f, 2f)] private float beatSensitivity = 0.42f;

        [Header("Microphone Voice Input")]
        [SerializeField] private bool enableMicrophone = true;
        [SerializeField]
        [Tooltip("Leave empty to use the operating system's default microphone.")]
        private string microphoneDevice;
        [SerializeField, Range(1f, 40f)] private float microphoneGain = 12f;
        [SerializeField, Range(0f, 0.1f)] private float microphoneNoiseGate = 0.012f;
        [SerializeField, Range(0.01f, 0.5f)] private float voiceEventThreshold = 0.08f;
        [SerializeField, Range(0.05f, 0.5f)] private float voiceEventInterval = 0.14f;
        [SerializeField]
        [Tooltip("Optional world-space origin for voice events. Defaults to Main Camera.")]
        private Transform voiceOrigin;

        private const int SampleCount = 512;
        private const int VisualSpectrumSize = 64;
        private readonly float[] spectrum = new float[SampleCount];
        private readonly float[] waveform = new float[SampleCount];
        private readonly float[] previousSpectrum = new float[SampleCount];
        private readonly float[] visualSpectrum = new float[VisualSpectrumSize];

        private float loudness;
        private float peak;
        private float bass;
        private float mid;
        private float treble;
        private float centroid;
        private float dominant;
        private float pulse;
        private float transientPulse;
        private float averageFlux = 0.001f;
        private Vector3 transientPosition;
        private SonicSurfaceType transientSurface;
        private uint soundEventSequence;
        private AudioClip microphoneClip;
        private float[] microphoneSamples;
        private string activeMicrophoneDevice;
        private int microphoneSampleRate;
        private int lastMicrophonePosition = -1;
        private float microphoneLevel;
        private float microphonePeak;
        private Vector3 microphoneBands = new Vector3(0.2f, 0.65f, 0.15f);
        private float nextVoiceEventTime;
        private bool microphoneStarting;
        private bool microphoneStartFailed;

        public Frame Current { get; private set; }
        public Vector3 LastTransientPosition => transientPosition;
        public SonicSurfaceType LastTransientSurface => transientSurface;
        public float InteractionPulse => transientPulse;
        public bool IsMicrophoneActive =>
            microphoneClip != null &&
            Microphone.IsRecording(activeMicrophoneDevice);
        public float MicrophoneLevel => microphoneLevel;
        public event Action<Frame> FrameUpdated;
        public event Action<SonicSoundEvent> SoundEventReported;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            if (GetComponent<SonicMasterLimiter>() == null)
                gameObject.AddComponent<SonicMasterLimiter>();
        }

        private IEnumerator Start()
        {
            if (enableMicrophone)
                yield return StartMicrophoneCapture();
        }

        private void OnDestroy()
        {
            StopMicrophoneCapture();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            UpdateMicrophoneCapture();

            AudioListener.GetOutputData(waveform, 0);
            AudioListener.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);

            float squareSum = 0f;
            float rawPeak = 0f;
            for (int i = 0; i < waveform.Length; i++)
            {
                float absolute = Mathf.Abs(waveform[i]);
                squareSum += waveform[i] * waveform[i];
                rawPeak = Mathf.Max(rawPeak, absolute);
            }

            float rawLoudness = Mathf.Clamp01(Mathf.Sqrt(squareSum / waveform.Length) * inputGain);
            rawPeak = Mathf.Clamp01(rawPeak * inputGain);
            float rawBass = 0f;
            float rawMid = 0f;
            float rawTreble = 0f;
            float weightedFrequency = 0f;
            float magnitudeSum = 0f;
            float dominantMagnitude = 0f;
            float dominantFrequency = 0f;
            float flux = 0f;
            float nyquist = AudioSettings.outputSampleRate * 0.5f;

            for (int i = 1; i < spectrum.Length; i++)
            {
                float frequency = i * nyquist / spectrum.Length;
                float magnitude = spectrum[i];
                float perceptual = Mathf.Sqrt(Mathf.Max(0f, magnitude));

                if (frequency < 250f)
                    rawBass += perceptual;
                else if (frequency < 2500f)
                    rawMid += perceptual;
                else if (frequency < 12000f)
                    rawTreble += perceptual;

                if (frequency <= 12000f)
                {
                    weightedFrequency += frequency * perceptual;
                    magnitudeSum += perceptual;
                }

                if (frequency >= 40f && frequency <= 12000f && magnitude > dominantMagnitude)
                {
                    dominantMagnitude = magnitude;
                    dominantFrequency = frequency;
                }

                flux += Mathf.Max(0f, magnitude - previousSpectrum[i]);
                previousSpectrum[i] = magnitude;
            }

            rawBass = Mathf.Clamp01(rawBass * 0.13f * inputGain);
            rawMid = Mathf.Clamp01(rawMid * 0.045f * inputGain);
            rawTreble = Mathf.Clamp01(rawTreble * 0.08f * inputGain);
            float rawCentroid = magnitudeSum > 0.0001f
                ? Mathf.Clamp01((weightedFrequency / magnitudeSum) / 12000f)
                : 0f;

            if (microphoneLevel > 0.0001f)
            {
                rawLoudness = Mathf.Max(rawLoudness, microphoneLevel);
                rawPeak = Mathf.Max(rawPeak, microphonePeak);
                rawBass = Mathf.Clamp01(
                    rawBass + microphoneBands.x * microphoneLevel);
                rawMid = Mathf.Clamp01(
                    rawMid + microphoneBands.y * microphoneLevel);
                rawTreble = Mathf.Clamp01(
                    rawTreble + microphoneBands.z * microphoneLevel);

                float microphoneCentroid =
                    microphoneBands.x * 0.015f +
                    microphoneBands.y * 0.16f +
                    microphoneBands.z * 0.58f;
                rawCentroid = Mathf.Lerp(
                    rawCentroid,
                    microphoneCentroid,
                    microphoneLevel);
                float microphoneDominant = microphoneBands.x > microphoneBands.y
                    ? (microphoneBands.x > microphoneBands.z ? 180f : 5200f)
                    : (microphoneBands.y > microphoneBands.z ? 1200f : 5200f);
                dominantFrequency = Mathf.Lerp(
                    dominantFrequency,
                    microphoneDominant,
                    microphoneLevel);
            }

            averageFlux = Mathf.Lerp(averageFlux, flux, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 2.5f));
            if (flux > averageFlux * (1.5f + beatSensitivity) && rawLoudness > 0.015f)
                pulse = Mathf.Max(pulse, Mathf.Clamp01(flux / Mathf.Max(averageFlux * 4f, 0.0001f)));

            for (int i = 0; i < visualSpectrum.Length; i++)
            {
                float normalized = (i + 1f) / visualSpectrum.Length;
                int bin = Mathf.Clamp(
                    Mathf.RoundToInt(normalized * normalized * (spectrum.Length - 1)),
                    1,
                    spectrum.Length - 1);
                float target = Mathf.Clamp01(Mathf.Sqrt(Mathf.Max(0f, spectrum[bin])) * inputGain * 2.5f);
                float speed = target > visualSpectrum[i] ? 24f : 8f;
                visualSpectrum[i] = Mathf.Lerp(
                    visualSpectrum[i],
                    target,
                    1f - Mathf.Exp(-Time.unscaledDeltaTime * speed));
            }

            pulse = Mathf.Max(pulse, transientPulse);
            transientPulse = Mathf.MoveTowards(transientPulse, 0f, Time.unscaledDeltaTime * 3.5f);
            pulse = Mathf.MoveTowards(pulse, 0f, Time.unscaledDeltaTime * 2.2f);

            loudness = Smooth(loudness, rawLoudness);
            peak = Smooth(peak, rawPeak);
            bass = Smooth(bass, rawBass);
            mid = Smooth(mid, rawMid);
            treble = Smooth(treble, rawTreble);
            centroid = Smooth(centroid, rawCentroid);
            dominant = Smooth(dominant, Mathf.Clamp01(dominantFrequency / 12000f));

            float hue = Mathf.Repeat(0.52f + centroid * 0.42f + dominant * 0.12f, 1f);
            Color color = Color.HSVToRGB(hue, 0.82f, 1f);
            Current = new Frame(
                loudness,
                peak,
                bass,
                mid,
                treble,
                centroid,
                dominant * 12000f,
                pulse,
                color);
            FrameUpdated?.Invoke(Current);
        }

        private IEnumerator StartMicrophoneCapture()
        {
            if (microphoneStarting || microphoneClip != null)
                yield break;

            microphoneStarting = true;
            microphoneStartFailed = false;
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                yield return Application.RequestUserAuthorization(
                    UserAuthorization.Microphone);
            }

            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                Debug.LogWarning(
                    "[SonicWorld] Microphone permission was not granted. " +
                    "Voice visualization is disabled.",
                    this);
                microphoneStartFailed = true;
                microphoneStarting = false;
                yield break;
            }

            string[] devices = Microphone.devices;
            if (devices == null || devices.Length == 0)
            {
                Debug.LogWarning(
                    "[SonicWorld] No microphone device was found.",
                    this);
                microphoneStartFailed = true;
                microphoneStarting = false;
                yield break;
            }

            activeMicrophoneDevice = null;
            if (!string.IsNullOrWhiteSpace(microphoneDevice))
            {
                for (int i = 0; i < devices.Length; i++)
                {
                    if (string.Equals(
                            devices[i],
                            microphoneDevice,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        activeMicrophoneDevice = devices[i];
                        break;
                    }
                }
            }

            int requestedRate = Mathf.Max(8000, AudioSettings.outputSampleRate);
            Microphone.GetDeviceCaps(
                activeMicrophoneDevice,
                out int minimumRate,
                out int maximumRate);
            if (minimumRate > 0 && maximumRate > 0)
            {
                requestedRate = Mathf.Clamp(
                    requestedRate,
                    minimumRate,
                    maximumRate);
            }

            microphoneClip = Microphone.Start(
                activeMicrophoneDevice,
                true,
                1,
                requestedRate);
            if (microphoneClip == null)
            {
                Debug.LogWarning(
                    "[SonicWorld] The microphone could not be started.",
                    this);
                microphoneStartFailed = true;
                microphoneStarting = false;
                yield break;
            }

            microphoneSampleRate = requestedRate;
            float timeout = Time.realtimeSinceStartup + 3f;
            while (Microphone.GetPosition(activeMicrophoneDevice) <= 0 &&
                   Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            if (Microphone.GetPosition(activeMicrophoneDevice) <= 0)
            {
                Debug.LogWarning(
                    "[SonicWorld] The microphone did not begin delivering samples.",
                    this);
                StopMicrophoneCapture();
                microphoneStartFailed = true;
                microphoneStarting = false;
                yield break;
            }

            microphoneSamples =
                new float[SampleCount * Mathf.Max(1, microphoneClip.channels)];
            lastMicrophonePosition = -1;
            microphoneStarting = false;
            Debug.Log(
                $"[SonicWorld] Voice input active: " +
                $"{(activeMicrophoneDevice ?? devices[0])}",
                this);
        }

        private void StopMicrophoneCapture()
        {
            if (microphoneClip != null)
            {
                Microphone.End(activeMicrophoneDevice);
            }

            microphoneClip = null;
            microphoneSamples = null;
            microphoneLevel = 0f;
            microphonePeak = 0f;
            lastMicrophonePosition = -1;
            microphoneStarting = false;
        }

        private void UpdateMicrophoneCapture()
        {
            if (!enableMicrophone)
            {
                if (microphoneClip != null)
                    StopMicrophoneCapture();
                DecayMicrophoneAnalysis();
                return;
            }

            if (microphoneClip == null)
            {
                if (!microphoneStarting && !microphoneStartFailed)
                    StartCoroutine(StartMicrophoneCapture());
                DecayMicrophoneAnalysis();
                return;
            }

            int position = Microphone.GetPosition(activeMicrophoneDevice);
            if (position <= SampleCount ||
                position == lastMicrophonePosition ||
                microphoneSamples == null)
            {
                DecayMicrophoneAnalysis();
                return;
            }

            lastMicrophonePosition = position;
            int offset = position - SampleCount;
            if (!microphoneClip.GetData(microphoneSamples, offset))
            {
                DecayMicrophoneAnalysis();
                return;
            }

            int channels = Mathf.Max(1, microphoneClip.channels);
            int frameCount = microphoneSamples.Length / channels;
            float lowFilter = 0f;
            float middleFilter = 0f;
            float lowSquareSum = 0f;
            float middleSquareSum = 0f;
            float highSquareSum = 0f;
            float squareSum = 0f;
            float rawPeak = 0f;
            float lowAlpha = 1f - Mathf.Exp(
                -2f * Mathf.PI * 250f / Mathf.Max(8000, microphoneSampleRate));
            float middleAlpha = 1f - Mathf.Exp(
                -2f * Mathf.PI * 2500f / Mathf.Max(8000, microphoneSampleRate));

            for (int frame = 0; frame < frameCount; frame++)
            {
                int sampleIndex = frame * channels;
                float sample = 0f;
                for (int channel = 0; channel < channels; channel++)
                    sample += microphoneSamples[sampleIndex + channel];
                sample /= channels;

                lowFilter += (sample - lowFilter) * lowAlpha;
                middleFilter += (sample - middleFilter) * middleAlpha;
                float lowSample = lowFilter;
                float middleSample = middleFilter - lowFilter;
                float highSample = sample - middleFilter;
                squareSum += sample * sample;
                lowSquareSum += lowSample * lowSample;
                middleSquareSum += middleSample * middleSample;
                highSquareSum += highSample * highSample;
                rawPeak = Mathf.Max(rawPeak, Mathf.Abs(sample));
            }

            float inverseCount = 1f / Mathf.Max(1, frameCount);
            float rms = Mathf.Sqrt(squareSum * inverseCount);
            float targetLevel = Mathf.Clamp01(
                Mathf.Max(0f, rms - microphoneNoiseGate) * microphoneGain);
            float targetPeak = Mathf.Clamp01(
                Mathf.Max(0f, rawPeak - microphoneNoiseGate) * microphoneGain);
            Vector3 rawBands = new Vector3(
                Mathf.Sqrt(lowSquareSum * inverseCount),
                Mathf.Sqrt(middleSquareSum * inverseCount),
                Mathf.Sqrt(highSquareSum * inverseCount));
            float bandTotal = rawBands.x + rawBands.y + rawBands.z;
            if (bandTotal > 0.000001f)
                rawBands /= bandTotal;

            float levelSpeed = targetLevel > microphoneLevel ? 22f : 7f;
            float levelBlend = 1f - Mathf.Exp(
                -Time.unscaledDeltaTime * levelSpeed);
            microphoneLevel = Mathf.Lerp(
                microphoneLevel,
                targetLevel,
                levelBlend);
            microphonePeak = Mathf.Lerp(
                microphonePeak,
                targetPeak,
                levelBlend);
            microphoneBands = Vector3.Lerp(
                microphoneBands,
                rawBands,
                1f - Mathf.Exp(-Time.unscaledDeltaTime * 12f));

            if (microphoneLevel < voiceEventThreshold ||
                Time.unscaledTime < nextVoiceEventTime)
            {
                return;
            }

            Transform origin = voiceOrigin;
            if (origin == null && Camera.main != null)
                origin = Camera.main.transform;
            Vector3 positionWorld =
                origin != null ? origin.position : transform.position;
            ReportTransient(
                positionWorld,
                microphoneLevel,
                SonicSurfaceType.Soft,
                SonicSoundEventKind.Voice,
                origin,
                null,
                microphoneBands);
            nextVoiceEventTime = Time.unscaledTime + voiceEventInterval;
        }

        private void DecayMicrophoneAnalysis()
        {
            float blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 8f);
            microphoneLevel = Mathf.Lerp(microphoneLevel, 0f, blend);
            microphonePeak = Mathf.Lerp(microphonePeak, 0f, blend);
        }

        public void ReportTransient(
            Vector3 position,
            float strength,
            SonicSurfaceType surface,
            SonicSoundEventKind kind,
            Transform sourceA,
            Transform sourceB,
            Vector3 bands)
        {
            transientPosition = position;
            transientSurface = surface;
            transientPulse = Mathf.Max(transientPulse, Mathf.Clamp01(strength));
            pulse = Mathf.Max(pulse, transientPulse);
            SoundEventReported?.Invoke(new SonicSoundEvent(
                ++soundEventSequence,
                Time.time,
                position,
                strength,
                surface,
                kind,
                sourceA,
                sourceB,
                bands));
        }

        public float GetVisualSpectrum(float normalizedPosition)
        {
            float scaled = Mathf.Clamp01(normalizedPosition) * (visualSpectrum.Length - 1);
            int lower = Mathf.FloorToInt(scaled);
            int upper = Mathf.Min(lower + 1, visualSpectrum.Length - 1);
            return Mathf.Lerp(visualSpectrum[lower], visualSpectrum[upper], scaled - lower);
        }

        private float Smooth(float current, float target)
        {
            float speed = target > current ? attack : release;
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * speed));
        }
    }
}
