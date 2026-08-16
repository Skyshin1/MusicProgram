using Crest;
using UnityEngine;
using Range = UnityEngine.RangeAttribute;

namespace MusicProgram.CrestURP
{
    /// <summary>Scalable, pausable and scrubbable clock for all Crest simulations.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class CrestURPScaledTimeProvider : TimeProviderBase
    {
        [Range(0f, 4f)] public float timeScale = 1f;
        public bool paused;
        public bool manualTime;
        public float manualTimeSeconds;

        readonly TimeProviderDefault _defaultClock = new();
        OceanRenderer _registeredOcean;
        float _scaledTime;

        void OnEnable()
        {
            _scaledTime = _defaultClock.CurrentTime;
            TryRegister();
        }

        void Update()
        {
            TryRegister();
            if (Application.isPlaying && !paused && !manualTime)
            {
                _scaledTime += _defaultClock.DeltaTime * Mathf.Max(0f, timeScale);
            }
        }

        void OnDisable()
        {
            if (_registeredOcean != null)
            {
                _registeredOcean.PopTimeProvider(this);
                _registeredOcean = null;
            }
        }

        void TryRegister()
        {
            var activeOcean = OceanRenderer.Instance;
            if (activeOcean == null || activeOcean == _registeredOcean) return;
            if (_registeredOcean != null) _registeredOcean.PopTimeProvider(this);
            activeOcean.PushTimeProvider(this);
            _registeredOcean = activeOcean;
        }

        public override float CurrentTime
        {
            get
            {
                if (manualTime) return manualTimeSeconds;
#if UNITY_EDITOR
                if (!Application.isPlaying) return _defaultClock.CurrentTime * Mathf.Max(0f, timeScale);
#endif
                return _scaledTime;
            }
        }

        public override float DeltaTime => paused || manualTime
            ? 0f
            : _defaultClock.DeltaTime * Mathf.Max(0f, timeScale);

        public override float DeltaTimeDynamics => DeltaTime;
    }
}
