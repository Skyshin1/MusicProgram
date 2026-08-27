using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepSeaAI
{
    public enum NoiseKind
    {
        Sonar,
        Impact,
        Interaction,
        Voice
    }

    public readonly struct NoiseStimulus
    {
        public readonly Vector3 Position;
        public readonly float Radius;
        public readonly NoiseKind Kind;
        public readonly Transform Source;
        public readonly float Timestamp;

        public NoiseStimulus(Vector3 position, float radius, NoiseKind kind, Transform source, float timestamp)
        {
            Position = position;
            Radius = Mathf.Max(0f, radius);
            Kind = kind;
            Source = source;
            Timestamp = timestamp;
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-120)]
    public sealed class NoiseSystem : MonoBehaviour
    {
        public static NoiseSystem Instance { get; private set; }
        public static event Action<NoiseStimulus> NoiseEmitted;

        [SerializeField, Min(0.1f)] private float sonarRadius = 18f;
        [SerializeField, Min(0.1f)] private float impactScanInterval = 5f;

        private float nextImpactScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstance()
        {
            if (FindFirstObjectByType<NoiseSystem>() != null)
                return;

            var root = new GameObject("Deep Sea Noise System");
            root.AddComponent<NoiseSystem>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnEnable()
        {
            // PulseStarted is the common path for keyboard sonar, controller sonar,
            // scripted sonar and collision / thrown-object sonar. Listening only to
            // PlayerSonarEmitted made the AI ignore non-keyboard pulses.
            VolumetricFogPulseEmitter.PulseStarted += OnSonarPulseStarted;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            VolumetricFogPulseEmitter.PulseStarted -= OnSonarPulseStarted;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
            AddImpactEmitters();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextImpactScan)
                return;

            nextImpactScan = Time.unscaledTime + impactScanInterval;
            AddImpactEmitters();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            nextImpactScan = 0f;
        }

        private void OnSonarPulseStarted(VolumetricFogPulseEmitter.PulseState pulse)
        {
            Emit(new NoiseStimulus(
                pulse.Origin,
                sonarRadius * Mathf.Clamp01(pulse.Strength),
                NoiseKind.Sonar,
                null,
                Time.time));
        }

        public static void Emit(NoiseStimulus stimulus)
        {
            if (stimulus.Radius <= 0.01f)
                return;
            NoiseEmitted?.Invoke(stimulus);
        }

        public static void EmitImpact(Vector3 position, float relativeSpeed, Transform source)
        {
            float radius;
            if (relativeSpeed < 0.6f)
                return;
            if (relativeSpeed < 2f)
                radius = Mathf.Lerp(3f, 6f, Mathf.InverseLerp(0.6f, 2f, relativeSpeed));
            else if (relativeSpeed < 5f)
                radius = Mathf.Lerp(6f, 12f, Mathf.InverseLerp(2f, 5f, relativeSpeed));
            else
                radius = 12f;

            Emit(new NoiseStimulus(position, radius, NoiseKind.Impact, source, Time.time));
        }

        private static void AddImpactEmitters()
        {
            Rigidbody[] bodies = FindObjectsByType<Rigidbody>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            foreach (Rigidbody body in bodies)
            {
                if (body == null || body.isKinematic ||
                    body.GetComponent<DeepSeaImpactNoiseEmitter>() != null)
                {
                    continue;
                }

                body.gameObject.AddComponent<DeepSeaImpactNoiseEmitter>();
            }
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class DeepSeaImpactNoiseEmitter : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float cooldown = 0.2f;
        private float lastEmission = -100f;

        private void OnCollisionEnter(Collision collision)
        {
            if (Time.time - lastEmission < cooldown)
                return;

            float speed = collision.relativeVelocity.magnitude;
            if (speed < 0.6f)
                return;

            Vector3 point = collision.contactCount > 0
                ? collision.GetContact(0).point
                : transform.position;
            lastEmission = Time.time;
            NoiseSystem.EmitImpact(point, speed, transform);
        }
    }
}
