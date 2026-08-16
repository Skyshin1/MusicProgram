using UnityEngine;

namespace MusicProgram.AbyssalWater
{
    /// <summary>
    /// Lightweight multi-point buoyancy using the same analytic spectrum as the
    /// shader. Dynamic ripples remain visual so no GPU readback stalls VR frames.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class AbyssalBuoyancy : MonoBehaviour
    {
        public Transform[] samplePoints;
        [Min(0f)] public float buoyancy = 18f;
        [Min(0f)] public float waterDrag = 2.5f;
        [Min(0f)] public float angularWaterDrag = 1.2f;
        [Range(0f, 2f)] public float waveVelocityInfluence = 0.55f;
        [Min(0.01f)] public float maximumSubmersion = 1.5f;

        Rigidbody _body;

        void Awake() => _body = GetComponent<Rigidbody>();

        void FixedUpdate()
        {
            var water = AbyssalWaterSystem.Active;
            if (water == null || _body == null) return;

            var points = samplePoints != null && samplePoints.Length > 0 ? samplePoints : null;
            var pointCount = points?.Length ?? 1;
            for (var i = 0; i < pointCount; i++)
            {
                var point = points != null && points[i] != null ? points[i].position : _body.worldCenterOfMass;
                water.SampleSurface(point, out var surface, out var normal, out var waveVelocity);
                var depth = surface.y - point.y;
                if (depth <= 0f) continue;

                var submersion = Mathf.Clamp01(depth / maximumSubmersion);
                var force = normal * (buoyancy * submersion / pointCount);
                var pointVelocity = _body.GetPointVelocity(point);
                force += (waveVelocity * waveVelocityInfluence - pointVelocity) *
                         (waterDrag * submersion / pointCount);
                _body.AddForceAtPosition(force, point, ForceMode.Acceleration);
                _body.AddTorque(-_body.angularVelocity * angularWaterDrag * submersion / pointCount,
                    ForceMode.Acceleration);
            }
        }
    }
}
