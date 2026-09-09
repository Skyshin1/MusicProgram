using DeepSeaAI;
using UnityEngine;

namespace DeepSeaDemo
{
    public sealed class DemoAcoustics : MonoBehaviour
    {
        public DemoConfig config;
        public DeepSeaStalkerConfig enemyConfig;
        public float Exposure { get; private set; }
        float lastPulse = -100f;
        float baseSearch;
        DeepSeaStalkerConfig runtimeConfig;
        void Awake()
        {
            // Never mutate the shared authored config when pressure changes.
            if (enemyConfig != null) { runtimeConfig = Instantiate(enemyConfig); baseSearch = runtimeConfig.searchDuration; }
        }
        public DeepSeaStalkerConfig RuntimeEnemyConfig => runtimeConfig != null ? runtimeConfig : enemyConfig;
        void OnEnable()
        {
            VolumetricFogPulseEmitter.PlayerSonarEmitted += OnPlayer;
            VolumetricFogCollisionPulse.CollisionSonarEmitted += OnImpact;
        }
        void OnDisable()
        {
            VolumetricFogPulseEmitter.PlayerSonarEmitted -= OnPlayer;
            VolumetricFogCollisionPulse.CollisionSonarEmitted -= OnImpact;
        }
        void OnDestroy() { if (runtimeConfig != null) Destroy(runtimeConfig); }
        void Update()
        {
            if (Time.time - lastPulse > config.exposureDecayDelay)
                Exposure = Mathf.MoveTowards(Exposure, 0, config.exposureDecaySpeed * Time.deltaTime);
            if (runtimeConfig != null) runtimeConfig.searchDuration = baseSearch + Exposure * .06f;
        }
        void OnPlayer(Vector3 p, float strength, Transform source)
        {
            Exposure = Mathf.Clamp(Exposure + config.exposurePerPulse, 0, 100); lastPulse = Time.time;
            float radius = Mathf.Lerp(config.baseHearingRadius, config.maximumHearingRadius, Exposure / 100f);
            NoiseSystem.Emit(new NoiseStimulus(p, radius * strength, NoiseKind.Sonar, source, Time.time));
        }
        void OnImpact(Vector3 p, float strength, Transform source)
        {
            NoiseSystem.Emit(new NoiseStimulus(p, config.impactRadius * Mathf.Lerp(.5f, 1f, strength), NoiseKind.Impact, source, Time.time));
        }
        public void ResetExposure() { Exposure = 0; lastPulse = -100; }
    }
}
