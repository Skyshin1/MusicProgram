using UnityEngine;

namespace DeepSeaAI
{
    [CreateAssetMenu(menuName = "Deep Sea AI/Stalker Config", fileName = "DeepSeaStalkerConfig")]
    public sealed class DeepSeaStalkerConfig : ScriptableObject
    {
        [Header("Movement")]
        [Min(0f)] public float patrolSpeed = 1.2f;
        [Min(0f)] public float investigateSpeed = 1.6f;
        [Min(0f)] public float chaseSpeed = 2.6f;
        [Min(0f)] public float patrolWaitSeconds = 1f;
        [Min(0.05f)] public float stoppingDistance = 0.18f;

        [Header("Sight")]
        [Min(0.1f)] public float sightRange = 3.5f;
        [Range(1f, 180f)] public float sightAngle = 60f;
        [Min(0.02f)] public float sightInterval = 0.2f;
        [Min(0f)] public float eyeHeight = 1.55f;
        [Min(0f)] public float playerBodyOffset = 0.75f;
        public LayerMask sightBlockers = ~0;

        [Header("Hearing")]
        [Min(0.1f)] public float sonarHearingRadius = 18f;
        [Range(0.05f, 1f)] public float occludedRadiusMultiplier = 0.55f;
        [Range(0f, 1f)] public float noiseRetargetAdvantage = 0.2f;
        [Min(0f)] public float noiseMemorySeconds = 8f;

        [Header("Search")]
        [Min(0.1f)] public float searchRadius = 2f;
        [Min(0.1f)] public float searchDuration = 6f;
        [Range(1, 12)] public int searchPointCount = 4;
        [Min(0f)] public float searchPointWait = 0.6f;
        [Min(0f)] public float searchTurnSpeed = 150f;

        [Header("Attack")]
        [Min(0.1f)] public float killDistance = 0.85f;
        [Min(0f)] public float attackWindup = 0.35f;

        [Header("Sonar Reveal")]
        public Color monsterOutlineColor = new Color(1f, 0.035f, 0.02f, 1f);
        [Min(0.05f)] public float revealDuration = 1.5f;

        public static DeepSeaStalkerConfig CreateRuntimeDefaults()
        {
            var config = CreateInstance<DeepSeaStalkerConfig>();
            config.name = "Deep Sea Stalker Runtime Defaults";
            return config;
        }

        private void OnValidate()
        {
            patrolSpeed = Mathf.Max(0f, patrolSpeed);
            investigateSpeed = Mathf.Max(0f, investigateSpeed);
            chaseSpeed = Mathf.Max(0f, chaseSpeed);
            sightRange = Mathf.Max(0.1f, sightRange);
            sightInterval = Mathf.Max(0.02f, sightInterval);
            sonarHearingRadius = Mathf.Max(0.1f, sonarHearingRadius);
            searchRadius = Mathf.Max(0.1f, searchRadius);
            searchDuration = Mathf.Max(0.1f, searchDuration);
            killDistance = Mathf.Max(0.1f, killDistance);
            revealDuration = Mathf.Max(0.05f, revealDuration);
        }
    }
}
