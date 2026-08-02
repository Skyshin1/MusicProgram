using UnityEngine;

namespace SonicWorld
{
    public enum SonicSurfaceType
    {
        Wood,
        Metal,
        Glass,
        Stone,
        Soft
    }

    [CreateAssetMenu(menuName = "Sonic World/Surface Profile", fileName = "Sonic Surface")]
    public sealed class SonicSurfaceProfile : ScriptableObject
    {
        [Header("Identity")]
        public SonicSurfaceType surfaceType;
        public AudioClip impactClip;

        [Header("Speed response")]
        [Min(0.01f)] public float minimumSpeed = 0.25f;
        [Min(0.1f)] public float fullSpeed = 8f;
        [Range(0.25f, 2f)] public float slowPitch = 0.72f;
        [Range(0.25f, 2f)] public float fastPitch = 1.35f;
        [Range(100f, 22000f)] public float slowLowPass = 1100f;
        [Range(100f, 22000f)] public float fastLowPass = 19000f;
        [Range(0f, 1f)] public float minimumGain = 0.08f;
        [Range(0.05f, 2f)] public float tailScale = 1f;

        [Header("Acoustics")]
        [Range(40f, 8000f)] public float resonance = 330f;
        [Range(0.1f, 12f)] public float decay = 4f;
        [Range(0f, 1f)] public float brightness = 0.45f;
        [Range(0f, 1f)] public float noise = 0.25f;

        [Header("MK Toon response")]
        [Range(0f, 2f)] public float emissionResponse = 1f;
        [Range(0f, 2f)] public float outlineResponse = 1f;
        [Range(0f, 2f)] public float rimResponse = 1f;
        [Range(0f, 2f)] public float iridescenceResponse = 1f;
        [Range(0f, 2f)] public float vertexResponse = 0.6f;

        public SonicSoundResult Evaluate(float ownPointSpeed, float relativeSpeed, float impulse)
        {
            float driveSpeed = relativeSpeed * 0.72f + ownPointSpeed * 0.28f;
            float normalized = Mathf.Clamp01(Mathf.InverseLerp(minimumSpeed, fullSpeed, driveSpeed));
            float shaped = Mathf.Pow(normalized, 0.62f);
            float impulseGain = Mathf.Clamp01(0.72f + Mathf.Log10(1f + Mathf.Max(0f, impulse)) * 0.18f);

            return new SonicSoundResult(
                Mathf.Lerp(slowPitch, fastPitch, shaped),
                Mathf.Lerp(minimumGain, 1f, shaped) * impulseGain,
                Mathf.Lerp(slowLowPass, fastLowPass, shaped),
                Mathf.Lerp(0.28f, 1f, shaped) * tailScale,
                resonance * Mathf.Lerp(0.92f, 1.12f, shaped),
                Mathf.Lerp(brightness * 0.45f, brightness, shaped),
                normalized);
        }
    }

    public readonly struct SonicSoundResult
    {
        public readonly float Pitch;
        public readonly float Gain;
        public readonly float LowPass;
        public readonly float Tail;
        public readonly float Resonance;
        public readonly float Brightness;
        public readonly float Energy;

        public SonicSoundResult(
            float pitch,
            float gain,
            float lowPass,
            float tail,
            float resonance,
            float brightness,
            float energy)
        {
            Pitch = pitch;
            Gain = gain;
            LowPass = lowPass;
            Tail = tail;
            Resonance = resonance;
            Brightness = brightness;
            Energy = energy;
        }

        public static SonicSoundResult Fuse(SonicSoundResult first, SonicSoundResult second)
        {
            return new SonicSoundResult(
                Mathf.Sqrt(Mathf.Max(0.01f, first.Pitch * second.Pitch)),
                Mathf.Sqrt(Mathf.Max(0f, first.Gain * second.Gain)),
                Mathf.Sqrt(Mathf.Max(100f, first.LowPass * second.LowPass)),
                (first.Tail + second.Tail) * 0.5f,
                Mathf.Sqrt(Mathf.Max(40f, first.Resonance * second.Resonance)),
                (first.Brightness + second.Brightness) * 0.5f,
                Mathf.Sqrt(Mathf.Max(0f, first.Energy * second.Energy)));
        }
    }
}
