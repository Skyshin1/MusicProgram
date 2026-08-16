using System;
using System.Collections.Generic;
using UnityEngine;

namespace MusicProgram.AbyssalWater
{
    public enum AbyssalWaterQuality
    {
        PcVrHigh,
        VrBalanced,
        QuestStandalone
    }

    [Serializable]
    public struct AbyssalManualWave
    {
        public bool enabled;
        [Range(0f, 360f)] public float direction;
        [Min(0.05f)] public float wavelength;
        [Min(0f)] public float amplitude;
        [Range(0f, 1f)] public float steepness;
        [Range(0.05f, 4f)] public float speedMultiplier;
        public float phase;
    }

    /// <summary>
    /// Compact authoring profile. Simple controls generate a deterministic wave
    /// spectrum; the Advanced foldout exposes every optical and simulation knob.
    /// </summary>
    [CreateAssetMenu(menuName = "Music Program/Abyssal Water Profile", fileName = "AbyssalWaterProfile")]
    public sealed class AbyssalWaterProfile : ScriptableObject
    {
        public const int MaximumWaves = 12;
        public const int MaximumMicroWaves = 8;

        [Header("Workflow")]
        public AbyssalWaterQuality quality = AbyssalWaterQuality.PcVrHigh;
        [Tooltip("The custom inspector keeps this foldout collapsed by default.")]
        public bool showAdvanced;

        [Header("Simple Wave Controls")]
        [Range(0f, 4f)] public float waveHeight = 0.9f;
        [Range(0.1f, 5f)] public float waveScale = 1f;
        [Range(0f, 3f)] public float waveSpeed = 1f;
        [Range(0f, 360f)] public float windDirection = 28f;
        [Range(0f, 35f)] public float windSpeed = 11f;
        [Range(0f, 1.4f)] public float choppiness = 0.62f;

        [Header("Wave Spectrum")]
        [Range(2, MaximumWaves)] public int spectrumBands = 8;
        [Min(0.1f)] public float minimumWavelength = 0.45f;
        [Min(0.2f)] public float maximumWavelength = 42f;
        [Range(0f, 90f)] public float directionSpread = 28f;
        public int spectrumSeed = 8128;
        public AnimationCurve amplitudeByWavelength = new AnimationCurve(
            new Keyframe(0f, 0.08f),
            new Keyframe(0.28f, 0.26f),
            new Keyframe(0.65f, 0.72f),
            new Keyframe(1f, 1f));
        public AnimationCurve directionSpreadByWavelength = AnimationCurve.Linear(0f, 1f, 1f, 0.2f);
        public AbyssalManualWave[] manualWaves = Array.Empty<AbyssalManualWave>();

        [Header("Algorithmic Anti-Tiling")]
        public bool enableAntiTiling = true;
        [Range(0f, 2f)] public float phaseWarpStrength = 0.65f;
        [Range(8f, 256f)] public float phaseWarpPatchSize = 55f;
        [Range(0f, 1f)] public float stochasticNormalBlend = 1f;
        public int antiTilingSeed = 19373;

        [Header("Optical Micro-Wave Spectrum")]
        public bool enableMicroSpectrum = true;
        [Range(0, MaximumMicroWaves)] public int microWaveCount = 8;
        [Range(0f, 0.3f)] public float microWaveAmplitude = 0.055f;
        [Range(0.08f, 2f)] public float microMinimumWavelength = 0.35f;
        [Range(0.2f, 8f)] public float microMaximumWavelength = 2.8f;
        [Range(0f, 180f)] public float microDirectionSpread = 150f;
        [Range(0f, 1f)] public float microChoppiness = 0.22f;
        [Range(0.05f, 4f)] public float microWaveSpeed = 1.15f;
        public int microSpectrumSeed = 27183;

        [Header("Water Absorption (Beer-Lambert)")]
        [ColorUsage(false, true)] public Color transmittanceColor = new Color(0.12f, 0.62f, 0.72f, 1f);
        [Min(0.01f)] public float transmittanceReferenceDistance = 8f;
        [ColorUsage(false, true)] public Color scatteringColor = new Color(0.025f, 0.32f, 0.42f, 1f);
        [Range(0f, 3f)] public float scatteringStrength = 0.72f;
        [Range(-0.9f, 0.9f)] public float scatteringAnisotropy = 0.18f;
        [Min(1f)] public float maximumOpticalDepth = 180f;

        [Header("Surface Optics")]
        [Range(1f, 1.6f)] public float indexOfRefraction = 1.333f;
        [Range(0f, 0.12f)] public float refractionStrength = 0.025f;
        [Range(0f, 2f)] public float reflectionStrength = 0.82f;
        [Range(0f, 1f)] public float smoothness = 0.94f;
        [Range(0f, 8f)] public float normalStrength = 1f;
        [Range(0f, 4f)] public float crestTransmission = 1.2f;
        [ColorUsage(false, true)] public Color crestTransmissionColor = new Color(0.05f, 0.65f, 0.72f, 1f);
        [Range(0.1f, 12f)] public float crestTransmissionPower = 4f;

        [Header("Foam")]
        [ColorUsage(false, true)] public Color foamColor = new Color(0.86f, 0.96f, 1f, 1f);
        [Range(0f, 4f)] public float foamStrength = 0.35f;
        [Range(0f, 2f)] public float crestFoamThreshold = 0.9f;
        [Range(0.001f, 1f)] public float crestFoamFeather = 0.08f;
        [Range(0f, 8f)] public float shorelineFoamDistance = 0.55f;
        [Range(0f, 4f)] public float contactFoamStrength = 1.25f;
        [Range(0.01f, 3f)] public float meniscusWidth = 0.18f;

        [Header("Wave-driven Caustics")]
        [Range(0f, 8f)] public float causticIntensity = 0.5f;
        [ColorUsage(false, true)] public Color causticColor = new Color(0.24f, 0.72f, 0.78f, 1f);
        [Range(0.1f, 8f)] public float causticScale = 1f;
        [Range(0.1f, 8f)] public float causticFocus = 1.45f;
        [Range(0f, 4f)] public float causticChromaticAberration = 0.12f;
        [Range(0f, 200f)] public float causticMaximumDepth = 45f;

        [Header("Underwater / Waterline")]
        [Range(0f, 2f)] public float underwaterDistortion = 0.22f;
        [Range(0.001f, 1f)] public float waterlineThickness = 0.045f;
        [Range(0f, 4f)] public float waterlineMeniscus = 0.38f;
        [Range(0f, 2f)] public float underwaterFogMultiplier = 1f;
        public bool enableGodRays = true;
        [Range(0f, 2f)] public float godRayStrength = 0.32f;

        [Header("Near-field Dynamic Waves")]
        public bool enableDynamicWaves = true;
        [Range(32, 512)] public int dynamicResolution = 256;
        [Range(8f, 256f)] public float dynamicWorldSize = 96f;
        [Range(0.05f, 8f)] public float dynamicWaveSpeed = 2.5f;
        [Range(0f, 4f)] public float dynamicDamping = 0.34f;
        [Range(0f, 4f)] public float dynamicDisplacement = 1f;
        [Range(1, 4)] public int dynamicSubsteps = 2;
        [Range(1, 64)] public int maximumImpulsesPerStep = 32;

        [Header("Infinite Ocean LOD")]
        [Range(2, 8)] public int lodLevels = 6;
        [Range(16, 160)] public int verticesPerLevel = 64;
        [Range(8f, 256f)] public float baseLodSize = 48f;
        [Range(0f, 8f)] public float skirtDepth = 1.5f;

        readonly Vector4[] _waveDataA = new Vector4[MaximumWaves];
        readonly Vector4[] _waveDataB = new Vector4[MaximumWaves];
        readonly Vector4[] _microWaveDataA = new Vector4[MaximumMicroWaves];
        readonly Vector4[] _microWaveDataB = new Vector4[MaximumMicroWaves];

        public int EffectiveDynamicResolution
        {
            get
            {
                var cap = quality switch
                {
                    AbyssalWaterQuality.PcVrHigh => 512,
                    AbyssalWaterQuality.VrBalanced => 256,
                    _ => 128
                };
                return Mathf.Clamp(Mathf.NextPowerOfTwo(dynamicResolution), 32, cap);
            }
        }

        public int EffectiveWaveCount => quality == AbyssalWaterQuality.QuestStandalone
            ? Mathf.Min(6, spectrumBands)
            : Mathf.Min(MaximumWaves, spectrumBands + CountEnabledManualWaves());

        public int EffectiveMicroWaveCount
        {
            get
            {
                if (!enableMicroSpectrum || microWaveAmplitude <= 0f) return 0;
                var qualityCap = quality switch
                {
                    AbyssalWaterQuality.PcVrHigh => MaximumMicroWaves,
                    AbyssalWaterQuality.VrBalanced => 5,
                    _ => 3
                };
                return Mathf.Clamp(microWaveCount, 0, qualityCap);
            }
        }

        public Vector3 AbsorptionCoefficient
        {
            get
            {
                var linear = transmittanceColor.linear;
                var distance = Mathf.Max(0.01f, transmittanceReferenceDistance);
                return new Vector3(
                    -Mathf.Log(Mathf.Max(0.0001f, linear.r)) / distance,
                    -Mathf.Log(Mathf.Max(0.0001f, linear.g)) / distance,
                    -Mathf.Log(Mathf.Max(0.0001f, linear.b)) / distance);
            }
        }

        public int BuildWaveData(float time, out Vector4[] dataA, out Vector4[] dataB)
        {
            Array.Clear(_waveDataA, 0, _waveDataA.Length);
            Array.Clear(_waveDataB, 0, _waveDataB.Length);

            var generatedCount = quality == AbyssalWaterQuality.QuestStandalone
                ? Mathf.Min(6, spectrumBands)
                : Mathf.Clamp(spectrumBands, 2, MaximumWaves);
            var rng = new System.Random(spectrumSeed);
            var amplitudeWeightSum = 0f;
            for (var band = 0; band < generatedCount; band++)
            {
                var bandT = generatedCount <= 1 ? 0f : band / (generatedCount - 1f);
                amplitudeWeightSum += Mathf.Max(0f, amplitudeByWavelength.Evaluate(bandT));
            }
            amplitudeWeightSum = Mathf.Max(0.0001f, amplitudeWeightSum);
            var index = 0;
            for (; index < generatedCount && index < MaximumWaves; index++)
            {
                var t = generatedCount <= 1 ? 0f : index / (generatedCount - 1f);
                var wavelength = Mathf.Lerp(minimumWavelength, maximumWavelength, t * t) * waveScale;
                var spread = directionSpread * Mathf.Max(0f, directionSpreadByWavelength.Evaluate(t));
                var jitter = ((float)rng.NextDouble() * 2f - 1f) * spread;
                var direction = windDirection + jitter;
                // The curve distributes one total amplitude budget instead of
                // granting every band the full Wave Height value.
                var amplitude = Mathf.Max(0f, amplitudeByWavelength.Evaluate(t)) /
                                amplitudeWeightSum * waveHeight;
                var steepness = Mathf.Lerp(0.9f, 0.45f, t);
                WriteWave(index, direction, wavelength, amplitude, steepness,
                    Mathf.Max(0.05f, waveSpeed * Mathf.Lerp(0.78f, 1.18f, windSpeed / 35f)),
                    (float)rng.NextDouble() * Mathf.PI * 2f);
            }

            if (quality != AbyssalWaterQuality.QuestStandalone && manualWaves != null)
            {
                foreach (var wave in manualWaves)
                {
                    if (!wave.enabled || index >= MaximumWaves) continue;
                    WriteWave(index++, wave.direction, wave.wavelength * waveScale,
                        wave.amplitude * waveHeight, wave.steepness,
                        wave.speedMultiplier * waveSpeed, wave.phase);
                }
            }

            dataA = _waveDataA;
            dataB = _waveDataB;
            return index;
        }

        public int BuildMicroWaveData(out Vector4[] dataA, out Vector4[] dataB)
        {
            Array.Clear(_microWaveDataA, 0, _microWaveDataA.Length);
            Array.Clear(_microWaveDataB, 0, _microWaveDataB.Length);
            var count = EffectiveMicroWaveCount;
            if (count == 0)
            {
                dataA = _microWaveDataA;
                dataB = _microWaveDataB;
                return 0;
            }

            var rng = new System.Random(microSpectrumSeed);
            var weightSum = 0f;
            for (var i = 0; i < count; i++)
            {
                var t = count <= 1 ? 0.5f : i / (count - 1f);
                weightSum += 0.72f + Mathf.Sin(t * Mathf.PI) * 0.28f;
            }

            var logMinimum = Mathf.Log(Mathf.Max(0.08f, microMinimumWavelength));
            var logMaximum = Mathf.Log(Mathf.Max(microMinimumWavelength, microMaximumWavelength));
            const float goldenRatioConjugate = 0.61803398875f;
            for (var i = 0; i < count; i++)
            {
                var t = count <= 1 ? 0.5f : i / (count - 1f);
                var wavelengthJitter = Mathf.Lerp(0.86f, 1.16f, (float)rng.NextDouble());
                var wavelength = Mathf.Exp(Mathf.Lerp(logMinimum, logMaximum, t)) * wavelengthJitter;
                var distributed = Mathf.Repeat(i * goldenRatioConjugate + (float)rng.NextDouble() * 0.17f, 1f);
                var direction = windDirection + (distributed * 2f - 1f) * microDirectionSpread;
                var weight = 0.72f + Mathf.Sin(t * Mathf.PI) * 0.28f;
                var amplitude = microWaveAmplitude * weight / Mathf.Max(0.0001f, weightSum);
                WriteMicroWave(i, direction, wavelength, amplitude, microChoppiness,
                    microWaveSpeed * Mathf.Lerp(0.92f, 1.12f, (float)rng.NextDouble()),
                    (float)rng.NextDouble() * Mathf.PI * 2f);
            }

            dataA = _microWaveDataA;
            dataB = _microWaveDataB;
            return count;
        }

        public void ApplyGlobals(float time)
        {
            var count = BuildWaveData(time, out var dataA, out var dataB);
            Shader.SetGlobalInt(AbyssalWaterShaderIds.WaveCount, count);
            Shader.SetGlobalVectorArray(AbyssalWaterShaderIds.WaveDataA, dataA);
            Shader.SetGlobalVectorArray(AbyssalWaterShaderIds.WaveDataB, dataB);
            Shader.SetGlobalFloat(AbyssalWaterShaderIds.Time, time);
            var microCount = BuildMicroWaveData(out var microDataA, out var microDataB);
            Shader.SetGlobalInt(AbyssalWaterShaderIds.MicroWaveCount, microCount);
            Shader.SetGlobalVectorArray(AbyssalWaterShaderIds.MicroWaveDataA, microDataA);
            Shader.SetGlobalVectorArray(AbyssalWaterShaderIds.MicroWaveDataB, microDataB);
            Shader.SetGlobalVector(AbyssalWaterShaderIds.AntiTiling,
                new Vector4(enableAntiTiling ? 1f : 0f, phaseWarpStrength,
                    1f / Mathf.Max(8f, phaseWarpPatchSize), stochasticNormalBlend));
            Shader.SetGlobalFloat(AbyssalWaterShaderIds.AntiTilingSeed, antiTilingSeed);

            var absorption = AbsorptionCoefficient;
            Shader.SetGlobalVector(AbyssalWaterShaderIds.Absorption,
                new Vector4(absorption.x, absorption.y, absorption.z, maximumOpticalDepth));
            var scatter = scatteringColor.linear;
            Shader.SetGlobalVector(AbyssalWaterShaderIds.Scattering,
                new Vector4(scatter.r, scatter.g, scatter.b, scatteringStrength));
            Shader.SetGlobalFloat(AbyssalWaterShaderIds.Anisotropy, scatteringAnisotropy);
            Shader.SetGlobalVector(AbyssalWaterShaderIds.Optics,
                new Vector4(indexOfRefraction, refractionStrength, reflectionStrength, smoothness));
            Shader.SetGlobalVector(AbyssalWaterShaderIds.Surface,
                new Vector4(normalStrength, crestTransmission, crestTransmissionPower, choppiness));
            Shader.SetGlobalColor(AbyssalWaterShaderIds.CrestColor, crestTransmissionColor.linear);
            Shader.SetGlobalColor(AbyssalWaterShaderIds.FoamColor, foamColor.linear);
            Shader.SetGlobalVector(AbyssalWaterShaderIds.Foam,
                new Vector4(foamStrength, crestFoamThreshold, crestFoamFeather, shorelineFoamDistance));
            Shader.SetGlobalVector(AbyssalWaterShaderIds.Contact,
                new Vector4(contactFoamStrength, meniscusWidth, 0f, 0f));
            Shader.SetGlobalVector(AbyssalWaterShaderIds.Caustics,
                new Vector4(causticIntensity, causticScale, causticFocus, causticChromaticAberration));
            Shader.SetGlobalColor(AbyssalWaterShaderIds.CausticColor, causticColor.linear);
            Shader.SetGlobalFloat(AbyssalWaterShaderIds.CausticMaximumDepth, causticMaximumDepth);
            Shader.SetGlobalVector(AbyssalWaterShaderIds.Underwater,
                new Vector4(underwaterDistortion, waterlineThickness, waterlineMeniscus, underwaterFogMultiplier));
            Shader.SetGlobalVector(AbyssalWaterShaderIds.GodRays,
                new Vector4(enableGodRays ? godRayStrength : 0f, 0f, 0f, 0f));
        }

        public void SampleSurface(Vector3 worldPosition, float time, float waterLevel,
            out Vector3 displacedPosition, out Vector3 normal, out Vector3 velocity)
        {
            var count = BuildWaveData(time, out var dataA, out var dataB);
            var position = new Vector3(worldPosition.x, waterLevel, worldPosition.z);
            var tangentX = Vector3.right;
            var tangentZ = Vector3.forward;
            velocity = Vector3.zero;

            for (var i = 0; i < count; i++)
            {
                var a = dataA[i];
                var b = dataB[i];
                var direction = new Vector2(a.x, a.y);
                var amplitude = a.z;
                var k = a.w;
                var omega = b.x;
                var q = Mathf.Clamp01(b.y * choppiness);
                var worldXZ = new Vector2(worldPosition.x, worldPosition.z);
                var phaseWarp = SamplePhaseWarp(worldXZ, i, out var phaseGradient);
                var thetaGradient = direction * k + phaseGradient;
                var theta = Vector2.Dot(direction, worldXZ) * k + phaseWarp + omega * time + b.z;
                var sine = Mathf.Sin(theta);
                var cosine = Mathf.Cos(theta);
                var qa = q * amplitude;
                position.x += direction.x * qa * cosine;
                position.y += amplitude * sine;
                position.z += direction.y * qa * cosine;

                tangentX += new Vector3(-direction.x * qa * sine * thetaGradient.x,
                    amplitude * cosine * thetaGradient.x,
                    -direction.y * qa * sine * thetaGradient.x);
                tangentZ += new Vector3(-direction.x * qa * sine * thetaGradient.y,
                    amplitude * cosine * thetaGradient.y,
                    -direction.y * qa * sine * thetaGradient.y);
                velocity += new Vector3(-direction.x * qa * omega * sine,
                    amplitude * omega * cosine,
                    -direction.y * qa * omega * sine);
            }

            displacedPosition = position;
            normal = Vector3.Cross(tangentZ, tangentX).normalized;
            if (normal.y < 0f) normal = -normal;
        }

        void WriteWave(int index, float degrees, float wavelength, float amplitude,
            float steepness, float speedMultiplier, float phase)
        {
            var radians = degrees * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;
            var safeWavelength = Mathf.Max(0.05f, wavelength);
            var k = Mathf.PI * 2f / safeWavelength;
            var omega = Mathf.Sqrt(9.81f * k) * Mathf.Max(0.05f, speedMultiplier);
            _waveDataA[index] = new Vector4(direction.x, direction.y, Mathf.Max(0f, amplitude), k);
            _waveDataB[index] = new Vector4(omega, Mathf.Clamp01(steepness), phase, 0f);
        }

        void WriteMicroWave(int index, float degrees, float wavelength, float amplitude,
            float steepness, float speedMultiplier, float phase)
        {
            var radians = degrees * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;
            var safeWavelength = Mathf.Max(0.05f, wavelength);
            var k = Mathf.PI * 2f / safeWavelength;
            var omega = Mathf.Sqrt(9.81f * k) * Mathf.Max(0.05f, speedMultiplier);
            _microWaveDataA[index] = new Vector4(direction.x, direction.y, Mathf.Max(0f, amplitude), k);
            _microWaveDataB[index] = new Vector4(omega, Mathf.Clamp01(steepness), phase, 0f);
        }

        float SamplePhaseWarp(Vector2 worldXZ, int waveIndex, out Vector2 phaseGradient)
        {
            phaseGradient = Vector2.zero;
            if (!enableAntiTiling || phaseWarpStrength <= 0f) return 0f;
            var frequency = 1f / Mathf.Max(8f, phaseWarpPatchSize);
            var directionA = new Vector2(0.7548777f, 0.6558659f).normalized;
            var directionB = new Vector2(-0.5698403f, 0.8217559f).normalized;
            var seedPhase = antiTilingSeed * 0.0137f + waveIndex * 2.3999632f;
            var angularA = Mathf.PI * 2f * frequency;
            var angularB = angularA * 1.618034f;
            var argumentA = Vector2.Dot(worldXZ, directionA) * angularA + seedPhase;
            var argumentB = Vector2.Dot(worldXZ, directionB) * angularB - seedPhase * 1.37f;
            phaseGradient = (Mathf.Cos(argumentA) * directionA * angularA * 0.62f +
                             Mathf.Cos(argumentB) * directionB * angularB * 0.38f) * phaseWarpStrength;
            return (Mathf.Sin(argumentA) * 0.62f + Mathf.Sin(argumentB) * 0.38f) * phaseWarpStrength;
        }

        int CountEnabledManualWaves()
        {
            if (manualWaves == null) return 0;
            var count = 0;
            foreach (var wave in manualWaves)
                if (wave.enabled) count++;
            return count;
        }

        void OnValidate()
        {
            maximumWavelength = Mathf.Max(minimumWavelength, maximumWavelength);
            microMaximumWavelength = Mathf.Max(microMinimumWavelength, microMaximumWavelength);
            phaseWarpPatchSize = Mathf.Max(8f, phaseWarpPatchSize);
            transmittanceReferenceDistance = Mathf.Max(0.01f, transmittanceReferenceDistance);
            dynamicWorldSize = Mathf.Max(8f, dynamicWorldSize);
            baseLodSize = Mathf.Max(8f, baseLodSize);
        }
    }

    internal static class AbyssalWaterShaderIds
    {
        public static readonly int WaveCount = Shader.PropertyToID("_AbyssalWaveCount");
        public static readonly int WaveDataA = Shader.PropertyToID("_AbyssalWaveDataA");
        public static readonly int WaveDataB = Shader.PropertyToID("_AbyssalWaveDataB");
        public static readonly int MicroWaveCount = Shader.PropertyToID("_AbyssalMicroWaveCount");
        public static readonly int MicroWaveDataA = Shader.PropertyToID("_AbyssalMicroWaveDataA");
        public static readonly int MicroWaveDataB = Shader.PropertyToID("_AbyssalMicroWaveDataB");
        public static readonly int AntiTiling = Shader.PropertyToID("_AbyssalAntiTiling");
        public static readonly int AntiTilingSeed = Shader.PropertyToID("_AbyssalAntiTilingSeed");
        public static readonly int Time = Shader.PropertyToID("_AbyssalTime");
        public static readonly int WaterLevel = Shader.PropertyToID("_AbyssalWaterLevel");
        public static readonly int Absorption = Shader.PropertyToID("_AbyssalAbsorption");
        public static readonly int Scattering = Shader.PropertyToID("_AbyssalScattering");
        public static readonly int Anisotropy = Shader.PropertyToID("_AbyssalAnisotropy");
        public static readonly int Optics = Shader.PropertyToID("_AbyssalOptics");
        public static readonly int Surface = Shader.PropertyToID("_AbyssalSurface");
        public static readonly int CrestColor = Shader.PropertyToID("_AbyssalCrestColor");
        public static readonly int FoamColor = Shader.PropertyToID("_AbyssalFoamColor");
        public static readonly int Foam = Shader.PropertyToID("_AbyssalFoam");
        public static readonly int Contact = Shader.PropertyToID("_AbyssalContact");
        public static readonly int Caustics = Shader.PropertyToID("_AbyssalCaustics");
        public static readonly int CausticColor = Shader.PropertyToID("_AbyssalCausticColor");
        public static readonly int CausticMaximumDepth = Shader.PropertyToID("_AbyssalCausticMaximumDepth");
        public static readonly int Underwater = Shader.PropertyToID("_AbyssalUnderwater");
        public static readonly int GodRays = Shader.PropertyToID("_AbyssalGodRays");
        public static readonly int DynamicCurrent = Shader.PropertyToID("_AbyssalDynamicCurrent");
        public static readonly int DynamicPrevious = Shader.PropertyToID("_AbyssalDynamicPrevious");
        public static readonly int DynamicCenterSize = Shader.PropertyToID("_AbyssalDynamicCenterSize");
        public static readonly int DynamicParameters = Shader.PropertyToID("_AbyssalDynamicParameters");
        public static readonly int PlanarReflection = Shader.PropertyToID("_AbyssalPlanarReflectionTexture");
        public static readonly int PlanarReflectionVp = Shader.PropertyToID("_AbyssalPlanarReflectionVP");
        public static readonly int PlanarReflectionEnabled = Shader.PropertyToID("_AbyssalPlanarReflectionEnabled");
    }
}
