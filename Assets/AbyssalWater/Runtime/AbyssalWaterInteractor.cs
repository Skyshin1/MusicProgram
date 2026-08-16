using UnityEngine;

namespace MusicProgram.AbyssalWater
{
    /// <summary>
    /// Emits real height-field impulses for players, fish, submarines and props.
    /// Add one component per representative contact point or wake source.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AbyssalWaterInteractor : MonoBehaviour
    {
        [Min(0.02f)] public float radius = 0.8f;
        [Range(-4f, 4f)] public float strength = 0.65f;
        [Min(0.01f)] public float emissionInterval = 0.08f;
        [Min(0f)] public float minimumSpeed = 0.18f;
        [Min(0f)] public float activationDepth = 2.5f;
        [Range(0f, 3f)] public float speedToStrength = 0.22f;
        public bool emitContinuously = true;
        public bool emitOnSurfaceCrossing = true;

        Vector3 _lastPosition;
        float _lastEmissionTime;
        float _lastSignedDistance;

        void OnEnable()
        {
            _lastPosition = transform.position;
            var water = AbyssalWaterSystem.Active;
            _lastSignedDistance = water != null
                ? transform.position.y - water.GetWaterHeight(transform.position)
                : 0f;
        }

        void LateUpdate()
        {
            var water = AbyssalWaterSystem.Active;
            if (water == null) return;

            var dt = Mathf.Max(Time.deltaTime, 0.0001f);
            var velocity = (transform.position - _lastPosition) / dt;
            var signedDistance = transform.position.y - water.GetWaterHeight(transform.position);
            var crossed = Mathf.Sign(signedDistance) != Mathf.Sign(_lastSignedDistance);
            var nearSurface = Mathf.Abs(signedDistance) <= activationDepth;

            if (emitOnSurfaceCrossing && crossed)
                Emit(Mathf.Max(Mathf.Abs(strength), velocity.magnitude * speedToStrength));

            if (emitContinuously && nearSurface && velocity.magnitude >= minimumSpeed &&
                Time.time - _lastEmissionTime >= emissionInterval)
            {
                var directionSign = signedDistance > 0f ? 1f : -1f;
                Emit(strength * directionSign + velocity.magnitude * speedToStrength * directionSign);
            }

            _lastSignedDistance = signedDistance;
            _lastPosition = transform.position;
        }

        public void EmitNow() => Emit(strength);

        public void Emit(float impulseStrength)
        {
            var water = AbyssalWaterSystem.Active;
            if (water == null) return;
            var position = transform.position;
            position.y = water.GetWaterHeight(position);
            water.EnqueueImpulse(position, radius, impulseStrength);
            _lastEmissionTime = Time.time;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.15f, 0.85f, 1f, 0.65f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
