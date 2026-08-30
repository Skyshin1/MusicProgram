using Crest;
using UnityEngine;

namespace OilRigAssembly.Runtime
{
    /// <summary>
    /// Stable multi-point wave follower for a large offshore platform.
    /// It intentionally moves one kinematic root instead of applying thousands of
    /// individual buoyancy forces to the modular children.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CrestFloatingPlatform : MonoBehaviour
    {
        [Header("Crest Surface Sampling")]
        public Transform[] samplePoints;
        [Min(0.1f)] public float minimumWaveLength = 14f;
        public float waterlineOffset;

        [Header("Motion")]
        public bool followHeave = true;
        public bool followPitchAndRoll = true;
        [Min(0.01f)] public float heaveSmoothTime = 0.8f;
        [Min(0.01f)] public float rotationResponse = 1.6f;
        [UnityEngine.Range(0f, 12f)] public float maximumTilt = 4.5f;

        [Header("Safety / Preview")]
        public bool motionEnabled = true;
        public bool drawSampleGizmos = true;

        SampleHeightHelper[] _samples;
        Rigidbody _body;
        float _verticalVelocity;
        float _neutralY;
        Quaternion _neutralRotation;
        Vector3 _neutralForward;

        void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _body.isKinematic = true;
            _body.useGravity = false;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _neutralY = transform.position.y;
            _neutralRotation = transform.rotation;
            _neutralForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (_neutralForward.sqrMagnitude < 0.01f) _neutralForward = Vector3.forward;
            RebuildSamplers();
        }

        void OnEnable()
        {
            if (_samples == null || _samples.Length != (samplePoints?.Length ?? 0))
            {
                RebuildSamplers();
            }
        }

        void RebuildSamplers()
        {
            int count = samplePoints?.Length ?? 0;
            _samples = new SampleHeightHelper[count];
            for (int i = 0; i < count; i++) _samples[i] = new SampleHeightHelper();
        }

        void FixedUpdate()
        {
            if (!motionEnabled || OceanRenderer.Instance == null || _body == null ||
                samplePoints == null || samplePoints.Length < 4)
            {
                return;
            }

            if (_samples == null || _samples.Length != samplePoints.Length) RebuildSamplers();

            float[] heights = new float[samplePoints.Length];
            Vector3 normalSum = Vector3.zero;
            int valid = 0;
            for (int i = 0; i < samplePoints.Length; i++)
            {
                Transform point = samplePoints[i];
                if (point == null) continue;
                _samples[i].Init(point.position, minimumWaveLength, true, this);
                if (_samples[i].Sample(out float height, out Vector3 normal))
                {
                    heights[i] = height;
                    normalSum += normal;
                    valid++;
                }
            }

            if (valid < 4) return;

            float averageHeight = 0f;
            for (int i = 0; i < heights.Length; i++) averageHeight += heights[i];
            averageHeight /= heights.Length;

            Vector3 targetPosition = _body.position;
            if (followHeave)
            {
                float targetY = averageHeight + waterlineOffset;
                targetPosition.y = Mathf.SmoothDamp(_body.position.y, targetY, ref _verticalVelocity,
                    heaveSmoothTime, Mathf.Infinity, Time.fixedDeltaTime);
            }
            else
            {
                targetPosition.y = _neutralY;
            }

            Quaternion targetRotation = _neutralRotation;
            if (followPitchAndRoll)
            {
                // Samples are generated in SW, SE, NW, NE order. Heights form a
                // stable plane across the entire hull; averaged Crest normals are
                // used as a fallback when the points temporarily converge.
                Vector3 sw = samplePoints[0].position; sw.y = heights[0];
                Vector3 se = samplePoints[1].position; se.y = heights[1];
                Vector3 nw = samplePoints[2].position; nw.y = heights[2];
                Vector3 ne = samplePoints[3].position; ne.y = heights[3];
                Vector3 across = ((se + ne) - (sw + nw)).normalized;
                Vector3 forward = ((nw + ne) - (sw + se)).normalized;
                Vector3 surfaceNormal = Vector3.Cross(forward, across).normalized;
                if (surfaceNormal.y < 0f) surfaceNormal = -surfaceNormal;
                if (surfaceNormal.sqrMagnitude < 0.5f) surfaceNormal = normalSum.normalized;

                float tilt = Vector3.Angle(Vector3.up, surfaceNormal);
                if (tilt > maximumTilt && tilt > 0.001f)
                {
                    surfaceNormal = Vector3.Slerp(Vector3.up, surfaceNormal, maximumTilt / tilt);
                }

                Vector3 heading = Vector3.ProjectOnPlane(_neutralForward, surfaceNormal).normalized;
                if (heading.sqrMagnitude < 0.01f) heading = Vector3.ProjectOnPlane(Vector3.forward, surfaceNormal).normalized;
                targetRotation = Quaternion.LookRotation(heading, surfaceNormal);
            }

            float rotationT = 1f - Mathf.Exp(-rotationResponse * Time.fixedDeltaTime);
            _body.MovePosition(targetPosition);
            _body.MoveRotation(Quaternion.Slerp(_body.rotation, targetRotation, rotationT));
        }

        void OnDrawGizmosSelected()
        {
            if (!drawSampleGizmos || samplePoints == null) return;
            Gizmos.color = new Color(0.1f, 0.85f, 1f, 0.9f);
            foreach (Transform point in samplePoints)
            {
                if (point == null) continue;
                Gizmos.DrawWireSphere(point.position, 0.45f);
                Gizmos.DrawLine(point.position - Vector3.up, point.position + Vector3.up);
            }
        }
    }
}
