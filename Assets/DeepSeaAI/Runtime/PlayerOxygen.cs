using System;
using UnityEngine;
using UnityEngine.Events;

namespace DeepSeaAI
{
    [Serializable]
    public sealed class OxygenAmountEvent : UnityEvent<float> { }

    /// <summary>
    /// Owns the player's oxygen amount. It is deliberately independent from the
    /// meter mesh so the same oxygen source can drive a glove gauge, HUD, audio,
    /// objectives, and later underwater-only rules.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerOxygen : MonoBehaviour
    {
        [Header("Capacity")]
        [SerializeField, Min(1f)] private float maxOxygen = 100f;
        [SerializeField, Min(0f)] private float startingOxygen = 100f;

        [Header("Drain")]
        [SerializeField] private bool drainAutomatically = true;
        [Tooltip("Default 0.2 drains a full tank in about 8 minutes 20 seconds.")]
        [SerializeField, Min(0f)] private float drainPerSecond = 0.2f;
        [SerializeField, Range(0f, 1f)] private float lowOxygenThreshold = 0.25f;
        [Tooltip("Keep this off until the respawn / game-over flow is finalized.")]
        [SerializeField] private bool respawnWhenDepleted;

        [Header("Events")]
        [SerializeField] private OxygenAmountEvent onOxygenChanged;
        [SerializeField] private UnityEvent onLowOxygen;
        [SerializeField] private UnityEvent onDepleted;
        [SerializeField] private UnityEvent onRefilled;

        private float oxygen;
        private bool lowOxygenInvoked;
        private bool depletedInvoked;
        private PlayerRespawnController respawnController;

        public event Action<float> OxygenChanged;
        public float Oxygen => oxygen;
        public float MaxOxygen => maxOxygen;
        public float NormalizedOxygen => maxOxygen <= 0f ? 0f : oxygen / maxOxygen;
        public bool IsDepleted => oxygen <= 0.001f;

        private void Awake()
        {
            maxOxygen = Mathf.Max(1f, maxOxygen);
            oxygen = Mathf.Clamp(startingOxygen, 0f, maxOxygen);
            respawnController = GetComponent<PlayerRespawnController>();
        }

        private void OnEnable()
        {
            if (respawnController == null)
                respawnController = GetComponent<PlayerRespawnController>();
            if (respawnController != null)
                respawnController.Respawned += Refill;
        }

        private void OnDisable()
        {
            if (respawnController != null)
                respawnController.Respawned -= Refill;
        }

        private void Start()
        {
            NotifyChanged();
        }

        private void Update()
        {
            if (drainAutomatically && !IsDepleted)
                Consume(drainPerSecond * Time.deltaTime);
        }

        public void Consume(float amount)
        {
            if (amount <= 0f || IsDepleted)
                return;
            SetOxygen(oxygen - amount, false);
        }

        public void Refill(float amount)
        {
            if (amount <= 0f)
                return;
            SetOxygen(oxygen + amount, false);
        }

        public void Refill()
        {
            bool wasBelowMaximum = oxygen < maxOxygen - 0.001f;
            SetOxygen(maxOxygen, false);
            if (wasBelowMaximum)
                onRefilled?.Invoke();
        }

        public void SetOxygen(float value)
        {
            SetOxygen(value, false);
        }

        private void SetOxygen(float value, bool forceNotify)
        {
            float previous = oxygen;
            oxygen = Mathf.Clamp(value, 0f, maxOxygen);
            if (forceNotify || !Mathf.Approximately(previous, oxygen))
                NotifyChanged();

            if (!lowOxygenInvoked && oxygen > 0f && NormalizedOxygen <= lowOxygenThreshold)
            {
                lowOxygenInvoked = true;
                onLowOxygen?.Invoke();
            }
            else if (NormalizedOxygen > lowOxygenThreshold)
            {
                lowOxygenInvoked = false;
            }

            if (!depletedInvoked && IsDepleted)
            {
                depletedInvoked = true;
                onDepleted?.Invoke();
                if (respawnWhenDepleted && respawnController != null)
                    respawnController.Kill(transform);
            }
            else if (!IsDepleted)
            {
                depletedInvoked = false;
            }
        }

        private void NotifyChanged()
        {
            float normalized = NormalizedOxygen;
            OxygenChanged?.Invoke(normalized);
            onOxygenChanged?.Invoke(normalized);
        }

        private void OnValidate()
        {
            maxOxygen = Mathf.Max(1f, maxOxygen);
            startingOxygen = Mathf.Clamp(startingOxygen, 0f, maxOxygen);
            drainPerSecond = Mathf.Max(0f, drainPerSecond);
        }
    }
}
