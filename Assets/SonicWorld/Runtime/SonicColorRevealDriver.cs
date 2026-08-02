using UnityEngine;
using UnityEngine.InputSystem;

namespace SonicWorld
{
    /// <summary>
    /// Converts player-generated spatial sound events into world-space reveal
    /// shells. The full-screen shader remains monochrome everywhere except where
    /// one of these deterministic shells intersects visible scene geometry.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SonicColorRevealDriver : MonoBehaviour
    {
        private struct RevealWave
        {
            public bool Active;
            public Vector3 Position;
            public float Age;
            public float Strength;
        }

        private const int WaveCapacity = 12;

        private static readonly int WaveCountId =
            Shader.PropertyToID("_SonicRevealWaveCount");
        private static readonly int WaveOriginsId =
            Shader.PropertyToID("_SonicRevealWaveOrigins");
        private static readonly int WaveParamsId =
            Shader.PropertyToID("_SonicRevealWaveParams");
        private static readonly int GlobalColorRestoreId =
            Shader.PropertyToID("_SonicGlobalColorRestore");
        private static readonly int GlobalColorWaveId =
            Shader.PropertyToID("_SonicGlobalColorWave");
        private static readonly int GlobalColorWaveParamsId =
            Shader.PropertyToID("_SonicGlobalColorWaveParams");

        [SerializeField, Range(1f, 12f)] private float waveSpeed = 5f;
        [SerializeField, Range(0.1f, 2f)] private float waveWidth = 0.6f;
        [SerializeField, Range(0.1f, 3f)] private float trailLength = 0.8f;
        [SerializeField, Range(2f, 30f)] private float maximumRadius = 12f;
        [SerializeField, Range(0f, 2f)] private float temporalDecay = 0.12f;
        [SerializeField, Range(0.1f, 6f)] private float fadeOutDuration = 1.5f;
        [SerializeField, Range(0f, 1f)] private float minimumReveal = 0.18f;

        [Header("Full Color Override")]
        [SerializeField] private bool fullColorOverride;
        [SerializeField] private bool enablePKeyToggle = true;
        [SerializeField]
        [Tooltip("Optional origin of the global color wave. Defaults to Main Camera.")]
        private Transform fullColorWaveOrigin;
        [SerializeField, Range(1f, 80f)] private float colorExpansionSpeed = 18f;
        [SerializeField, Range(5f, 300f)] private float colorExpansionDistance = 120f;
        [SerializeField, Range(0.1f, 15f)] private float colorExpansionFeather = 3f;

        private readonly RevealWave[] waves = new RevealWave[WaveCapacity];
        private readonly Vector4[] shaderOrigins = new Vector4[WaveCapacity];
        private readonly Vector4[] shaderParams = new Vector4[WaveCapacity];
        private int nextWave;
        private bool renderedFullColor;
        private bool transitionTarget;
        private bool globalTransitionActive;
        private float globalTransitionRadius;
        private Vector3 globalTransitionOrigin;
        private SonicAudioBus subscribedBus;

        public bool FullColorOverride => fullColorOverride;

        private void OnEnable()
        {
            EnsureBusSubscription();
            Shader.SetGlobalInt(WaveCountId, 0);
            renderedFullColor = fullColorOverride;
            transitionTarget = fullColorOverride;
            globalTransitionActive = false;
            Shader.SetGlobalFloat(
                GlobalColorRestoreId,
                renderedFullColor ? 1f : 0f);
            Shader.SetGlobalVector(GlobalColorWaveParamsId, Vector4.zero);
        }

        private void OnDisable()
        {
            if (subscribedBus != null)
                subscribedBus.SoundEventReported -= OnSoundEvent;
            subscribedBus = null;
            Shader.SetGlobalInt(WaveCountId, 0);
            Shader.SetGlobalFloat(GlobalColorRestoreId, 0f);
            Shader.SetGlobalVector(GlobalColorWaveParamsId, Vector4.zero);
        }

        private void Update()
        {
            EnsureBusSubscription();
            if (enablePKeyToggle &&
                Keyboard.current != null &&
                Keyboard.current.pKey.wasPressedThisFrame)
            {
                ToggleFullColor();
            }

            if (fullColorOverride != transitionTarget)
                BeginGlobalColorTransition(fullColorOverride);
            UpdateGlobalColorTransition();

            int activeCount = 0;
            float deltaTime = Time.unscaledDeltaTime;
            for (int i = 0; i < waves.Length; i++)
            {
                if (!waves[i].Active)
                    continue;

                RevealWave wave = waves[i];
                wave.Age += deltaTime;
                float travelDuration =
                    maximumRadius / Mathf.Max(0.01f, waveSpeed);
                float fadeAge = Mathf.Max(0f, wave.Age - travelDuration);
                if (fadeAge >= fadeOutDuration)
                {
                    wave.Active = false;
                    waves[i] = wave;
                    continue;
                }

                float radius = Mathf.Min(wave.Age * waveSpeed, maximumRadius);
                float endFade = 1f - Mathf.SmoothStep(
                    0f,
                    1f,
                    fadeAge / Mathf.Max(0.01f, fadeOutDuration));
                float decay =
                    Mathf.Exp(-wave.Age * temporalDecay) * endFade;
                shaderOrigins[activeCount] =
                    new Vector4(wave.Position.x, wave.Position.y, wave.Position.z, radius);
                shaderParams[activeCount] =
                    new Vector4(waveWidth, wave.Strength, trailLength, decay);
                activeCount++;
                waves[i] = wave;
            }

            Shader.SetGlobalInt(WaveCountId, activeCount);
            if (activeCount == 0)
                return;

            Shader.SetGlobalVectorArray(WaveOriginsId, shaderOrigins);
            Shader.SetGlobalVectorArray(WaveParamsId, shaderParams);
        }

        public void RestoreFullColor()
        {
            SetFullColor(true);
        }

        public void RestoreMonochrome()
        {
            SetFullColor(false);
        }

        public void ToggleFullColor()
        {
            SetFullColor(!fullColorOverride);
        }

        public void SetFullColor(bool restoreColor)
        {
            if (fullColorOverride == restoreColor &&
                transitionTarget == restoreColor &&
                (globalTransitionActive || renderedFullColor == restoreColor))
            {
                return;
            }

            fullColorOverride = restoreColor;
            BeginGlobalColorTransition(restoreColor);
        }

        private void BeginGlobalColorTransition(bool restoreColor)
        {
            transitionTarget = restoreColor;
            globalTransitionOrigin = ResolveFullColorOrigin();
            globalTransitionRadius = 0f;
            globalTransitionActive = true;
            Shader.SetGlobalFloat(
                GlobalColorRestoreId,
                renderedFullColor ? 1f : 0f);
        }

        private void UpdateGlobalColorTransition()
        {
            if (!globalTransitionActive)
            {
                Shader.SetGlobalFloat(
                    GlobalColorRestoreId,
                    renderedFullColor ? 1f : 0f);
                Shader.SetGlobalVector(
                    GlobalColorWaveParamsId,
                    Vector4.zero);
                return;
            }

            globalTransitionRadius +=
                Mathf.Max(0.01f, colorExpansionSpeed) *
                Time.unscaledDeltaTime;
            float completionRadius =
                Mathf.Max(0.01f, colorExpansionDistance) +
                Mathf.Max(0.001f, colorExpansionFeather);
            float progress = Mathf.Clamp01(
                globalTransitionRadius /
                completionRadius);
            Shader.SetGlobalVector(
                GlobalColorWaveId,
                new Vector4(
                    globalTransitionOrigin.x,
                    globalTransitionOrigin.y,
                    globalTransitionOrigin.z,
                    globalTransitionRadius));
            Shader.SetGlobalVector(
                GlobalColorWaveParamsId,
                new Vector4(
                    colorExpansionFeather,
                    transitionTarget ? 1f : -1f,
                    progress,
                    1f));

            if (progress < 1f)
                return;

            globalTransitionActive = false;
            renderedFullColor = transitionTarget;
            Shader.SetGlobalFloat(
                GlobalColorRestoreId,
                renderedFullColor ? 1f : 0f);
            Shader.SetGlobalVector(
                GlobalColorWaveParamsId,
                Vector4.zero);
        }

        private Vector3 ResolveFullColorOrigin()
        {
            if (fullColorWaveOrigin != null)
                return fullColorWaveOrigin.position;

            Camera mainCamera = Camera.main;
            return mainCamera != null
                ? mainCamera.transform.position
                : transform.position;
        }

        private void EnsureBusSubscription()
        {
            SonicAudioBus current = SonicAudioBus.Instance;
            if (current == subscribedBus)
                return;

            if (subscribedBus != null)
                subscribedBus.SoundEventReported -= OnSoundEvent;
            subscribedBus = current;
            if (subscribedBus != null)
                subscribedBus.SoundEventReported += OnSoundEvent;
        }

        private void OnSoundEvent(SonicSoundEvent soundEvent)
        {
            if (soundEvent.Kind != SonicSoundEventKind.Collision &&
                soundEvent.Kind != SonicSoundEventKind.Swing &&
                soundEvent.Kind != SonicSoundEventKind.Voice)
            {
                return;
            }
            if (soundEvent.Strength <= 0.001f)
                return;

            waves[nextWave] = new RevealWave
            {
                Active = true,
                Position = soundEvent.Position,
                Age = 0f,
                Strength = Mathf.Lerp(
                    minimumReveal,
                    1f,
                    Mathf.Sqrt(Mathf.Clamp01(soundEvent.Strength)))
            };
            nextWave = (nextWave + 1) % WaveCapacity;
        }
    }
}
