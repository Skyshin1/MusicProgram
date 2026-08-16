using Crest;
using UnityEngine;
using Range = UnityEngine.RangeAttribute;

namespace MusicProgram.CrestURP
{
    /// <summary>
    /// Central authoring and runtime API for FFT sea state, spectrum, dynamic
    /// interactions, foam and simulation time. Fine 14-band spectrum controls
    /// are exposed by its custom inspector.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class CrestURPWaveController : MonoBehaviour
    {
        public enum SeaPreset
        {
            Custom,
            Glassy,
            TropicalLagoon,
            OpenOcean,
            Storm,
        }

        public enum FFTResolution
        {
            Low64 = 64,
            High128 = 128,
            Ultra256 = 256,
        }

        [Header("References")]
        public OceanRenderer ocean;
        public ShapeFFT fft;
        public OceanWaveSpectrum spectrum;
        public SimSettingsWave dynamicWaves;
        public SimSettingsFoam foam;
        public CrestURPScaledTimeProvider timeProvider;

        [Header("Sea state")]
        public SeaPreset preset = SeaPreset.Custom;
        [Range(-20f, 20f)] public float seaLevel;
        [Range(0f, 150f)] public float windSpeedKph = 18f;
        [Range(-180f, 180f)] public float windDirectionDegrees = 24f;
        [Range(0f, 1f)] public float windTurbulence = 0.18f;
        [Range(0f, 1f)] public float fftWeight = 1f;
        [Range(0f, 10f)] public float waveAmplitude = 1f;
        [Range(0f, 2f)] public float horizontalChop = 1.45f;
        [Range(0f, 180f)] public float directionalSpread = 78f;
        [Range(0f, 4f)] public float spectralTimeScale = 1f;
        [Range(0f, 2f)] public float smallWavelengthMultiplier = 1f;
        public FFTResolution fftResolution = FFTResolution.High128;

        [Header("Simulation time")]
        [Range(0f, 4f)] public float simulationTimeScale = 1f;
        public bool pauseSimulation;
        public bool manualSimulationTime;
        public float manualTimeSeconds;

        [Header("Dynamic interaction waves")]
        [Range(15f, 200f)] public float dynamicSimulationFrequency = 45f;
        [Range(0f, 1f)] public float dynamicDamping = 0.075f;
        [Range(0.1f, 1f)] public float dynamicCourantNumber = 0.6f;
        [Range(0f, 1f)] public float dynamicShallowAttenuation = 0.72f;
        [Range(0f, 20f)] public float dynamicHorizontalDisplacement = 2.5f;
        [Range(0f, 1f)] public float dynamicDisplacementClamp = 0.35f;
        [Range(0f, 8f)] public float dynamicGravityMultiplier = 1f;

        [Header("Foam simulation")]
        [Range(0f, 20f)] public float foamFadeRate = 0.8f;
        [Range(0f, 5f)] public float whitecapStrength = 1.15f;
        [Range(0f, 1f)] public float whitecapCoverage = 0.56f;
        [Range(0.01f, 3f)] public float shorelineFoamDepth = 0.65f;
        [Range(0f, 5f)] public float shorelineFoamStrength = 2f;
        [Range(15f, 200f)] public float foamSimulationFrequency = 30f;

        [Header("Runtime")]
        [Tooltip("Continuously applies controls, allowing scripts/UI to change the sea state at runtime.")]
        public bool applyContinuously = true;

        int _lastSpectrumHash;
        bool _hasSpectrumHash;

        void Reset()
        {
            FindReferences();
            CaptureCurrentSettings();
            ApplySettings(true);
        }

        void OnEnable()
        {
            FindReferences();
            ApplySettings(true);
        }

        void OnValidate() => ApplySettings(false);

        void Update()
        {
            if (applyContinuously) ApplySettings(false);
        }

        public void FindReferences()
        {
            if (ocean == null) ocean = OceanRenderer.Instance != null
                ? OceanRenderer.Instance
                : FindFirstObjectByType<OceanRenderer>();
            if (fft == null && ocean != null) fft = ocean.GetComponent<ShapeFFT>();
            if (spectrum == null && fft != null) spectrum = fft._spectrum;
            if (dynamicWaves == null && ocean != null) dynamicWaves = ocean.SimSettingsDynamicWaves;
            if (foam == null && ocean != null) foam = ocean._simSettingsFoam;
            if (timeProvider == null && ocean != null) timeProvider = ocean.GetComponent<CrestURPScaledTimeProvider>();
        }

        public void ApplySettings(bool forceSpectrumRefresh)
        {
            FindReferences();

            if (ocean != null)
            {
                var position = ocean.transform.position;
                position.y = seaLevel;
                ocean.transform.position = position;
                ocean._globalWindZone = null;
                ocean._globalWindSpeed = windSpeedKph;
                ocean._globalWindDirectionAngle = windDirectionDegrees;
                ocean._globalWindTurbulence = windTurbulence;
                if (dynamicWaves != null) ocean.SimSettingsDynamicWaves = dynamicWaves;
                if (foam != null) ocean._simSettingsFoam = foam;
            }

            if (fft != null)
            {
                fft._overrideGlobalWindSpeed = false;
                fft._overrideGlobalWindDirection = false;
                fft._overrideGlobalWindTurbulence = false;
                fft._weight = fftWeight;
                fft._resolution = (int)fftResolution;
                if (spectrum != null) fft._spectrum = spectrum;
            }

            if (spectrum != null)
            {
                spectrum._multiplier = waveAmplitude;
                spectrum._chop = horizontalChop;
                spectrum._waveDirectionVariance = directionalSpread;
                spectrum._gravityScale = spectralTimeScale;
                spectrum._smallWavelengthMultiplier = smallWavelengthMultiplier;

                var hash = ComputeSpectrumHash(spectrum);
                if (forceSpectrumRefresh || !_hasSpectrumHash || hash != _lastSpectrumHash)
                {
                    FFTCompute.InvalidateSpectrum(spectrum);
                    _lastSpectrumHash = hash;
                    _hasSpectrumHash = true;
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        UnityEditor.EditorUtility.SetDirty(spectrum);
                    }
#endif
                }
            }

            if (dynamicWaves != null)
            {
                dynamicWaves._simulationFrequency = dynamicSimulationFrequency;
                dynamicWaves._damping = dynamicDamping;
                dynamicWaves._courantNumber = dynamicCourantNumber;
                dynamicWaves._attenuationInShallows = dynamicShallowAttenuation;
                dynamicWaves._horizDisplace = dynamicHorizontalDisplacement;
                dynamicWaves._displaceClamp = dynamicDisplacementClamp;
                dynamicWaves._gravityMultiplier = dynamicGravityMultiplier;
            }

            if (foam != null)
            {
                foam._foamFadeRate = foamFadeRate;
                foam._waveFoamStrength = whitecapStrength;
                foam._waveFoamCoverage = whitecapCoverage;
                foam._shorelineFoamMaxDepth = shorelineFoamDepth;
                foam._shorelineFoamStrength = shorelineFoamStrength;
                foam._simulationFrequency = foamSimulationFrequency;
            }

            if (timeProvider != null)
            {
                timeProvider.timeScale = simulationTimeScale;
                timeProvider.paused = pauseSimulation;
                timeProvider.manualTime = manualSimulationTime;
                timeProvider.manualTimeSeconds = manualTimeSeconds;
            }
        }

        public void CaptureCurrentSettings()
        {
            FindReferences();
            if (ocean != null)
            {
                seaLevel = ocean.transform.position.y;
                windSpeedKph = ocean._globalWindSpeed;
                windDirectionDegrees = ocean._globalWindDirectionAngle;
                windTurbulence = ocean._globalWindTurbulence;
            }
            if (fft != null)
            {
                fftWeight = fft._weight;
                fftResolution = fft._resolution >= 256 ? FFTResolution.Ultra256
                    : fft._resolution >= 128 ? FFTResolution.High128 : FFTResolution.Low64;
            }
            if (spectrum != null)
            {
                waveAmplitude = spectrum._multiplier;
                horizontalChop = spectrum._chop;
                directionalSpread = spectrum._waveDirectionVariance;
                spectralTimeScale = spectrum._gravityScale;
                smallWavelengthMultiplier = spectrum._smallWavelengthMultiplier;
            }
            if (dynamicWaves != null)
            {
                dynamicSimulationFrequency = dynamicWaves._simulationFrequency;
                dynamicDamping = dynamicWaves._damping;
                dynamicCourantNumber = dynamicWaves._courantNumber;
                dynamicShallowAttenuation = dynamicWaves._attenuationInShallows;
                dynamicHorizontalDisplacement = dynamicWaves._horizDisplace;
                dynamicDisplacementClamp = dynamicWaves._displaceClamp;
                dynamicGravityMultiplier = dynamicWaves._gravityMultiplier;
            }
            if (foam != null)
            {
                foamFadeRate = foam._foamFadeRate;
                whitecapStrength = foam._waveFoamStrength;
                whitecapCoverage = foam._waveFoamCoverage;
                shorelineFoamDepth = foam._shorelineFoamMaxDepth;
                shorelineFoamStrength = foam._shorelineFoamStrength;
                foamSimulationFrequency = foam._simulationFrequency;
            }
            if (timeProvider != null)
            {
                simulationTimeScale = timeProvider.timeScale;
                pauseSimulation = timeProvider.paused;
                manualSimulationTime = timeProvider.manualTime;
                manualTimeSeconds = timeProvider.manualTimeSeconds;
            }
        }

        public void ApplySelectedPreset()
        {
            switch (preset)
            {
                case SeaPreset.Glassy:
                    SetSeaState(4f, 0.08f, 0.28f, 0.55f, 125f, 0.72f);
                    whitecapStrength = 0.2f;
                    whitecapCoverage = 0.2f;
                    break;
                case SeaPreset.TropicalLagoon:
                    SetSeaState(11f, 0.14f, 0.58f, 0.95f, 96f, 0.88f);
                    whitecapStrength = 0.62f;
                    whitecapCoverage = 0.38f;
                    break;
                case SeaPreset.OpenOcean:
                    SetSeaState(28f, 0.28f, 1.18f, 1.5f, 68f, 1f);
                    whitecapStrength = 1.2f;
                    whitecapCoverage = 0.56f;
                    break;
                case SeaPreset.Storm:
                    SetSeaState(68f, 0.62f, 2.15f, 1.95f, 42f, 1.18f);
                    whitecapStrength = 2.4f;
                    whitecapCoverage = 0.8f;
                    break;
            }
            ApplySettings(true);
        }

        void SetSeaState(float wind, float turbulence, float amplitude, float chop, float spread, float speed)
        {
            windSpeedKph = wind;
            windTurbulence = turbulence;
            waveAmplitude = amplitude;
            horizontalChop = chop;
            directionalSpread = spread;
            spectralTimeScale = speed;
        }

        static int ComputeSpectrumHash(OceanWaveSpectrum value)
        {
            return value.ComputeSettingsHash();
        }
    }
}
