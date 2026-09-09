using UnityEngine;

namespace DeepSeaDemo
{
    [CreateAssetMenu(menuName = "Deep Sea Demo/Configuration")]
    public sealed class DemoConfig : ScriptableObject
    {
        public DemoTextCatalog text;
        [Tooltip("A filename only, isolated from other demo scenes.")]
        public string saveFileName = "DeepSeaInvestigation.checkpoint.json";
        [Header("Player / metres and seconds")]
        public float moveSpeed = 2f;
        public float swimSpeed = 1.5f;
        public float oxygenSeconds = 1200f;
        public float refillSeconds = 5f;
        public float grabRange = 3f;
        [Header("Sonar")]
        public float sonarRadius = 25f;
        public float sonarSpeed = 12f;
        public float sonarWidth = .5f;
        public float sonarCooldown = 1.5f;
        public float exposurePerPulse = 20f;
        public float exposureDecayDelay = 8f;
        public float exposureDecaySpeed = 3f;
        public float baseHearingRadius = 24f;
        public float maximumHearingRadius = 36f;
        [Header("Collision sonar")]
        public float impactSpeed = 1.5f;
        public float impactCooldown = .6f;
        public float impactRadius = 12f;
        [Header("Layers - assigned by builder, never overwrite occupied user layers")]
        public LayerMask worldMask;
        public LayerMask interactMask;
        public LayerMask uiMask;
        [Header("XR / presentation")]
        public Font chineseFont;
        [Tooltip("Explicit build dependencies for shaders created by runtime effects.")]
        public Shader[] runtimeShaders;
        public bool weatherReady;
        [TextArea] public string weatherStatus;
    }
}
