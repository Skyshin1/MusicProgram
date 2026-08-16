using UnityEngine;

namespace MusicProgram.CrestURP
{
    /// <summary>Simple deterministic movement used by the showcase submarine and fish school.</summary>
    public sealed class CrestURPDynamicWaveDemoActor : MonoBehaviour
    {
        public Vector2 orbitRadius = new(13f, 7f);
        [UnityEngine.Range(-90f, 90f)] public float angularSpeed = 16f;
        [UnityEngine.Range(0f, 2f)] public float bobAmplitude = 0.28f;
        [UnityEngine.Range(0f, 5f)] public float bobFrequency = 0.72f;
        public float phase;
        public bool faceDirection = true;

        Vector3 _origin;
        Vector3 _previousPosition;

        void Awake()
        {
            _origin = transform.position;
            _previousPosition = EvaluatePosition(Time.time);
            transform.position = _previousPosition;
        }

        void Update()
        {
            var position = EvaluatePosition(Time.time);
            transform.position = position;

            var velocity = position - _previousPosition;
            if (faceDirection && velocity.sqrMagnitude > 0.00001f)
            {
                var flatVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
                if (flatVelocity.sqrMagnitude > 0.00001f)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation,
                        Quaternion.LookRotation(flatVelocity.normalized, Vector3.up),
                        1f - Mathf.Exp(-Time.deltaTime * 7f));
                }
            }
            _previousPosition = position;
        }

        Vector3 EvaluatePosition(float time)
        {
            var angle = (time * angularSpeed + phase) * Mathf.Deg2Rad;
            return _origin + new Vector3(
                Mathf.Cos(angle) * orbitRadius.x,
                Mathf.Sin(time * bobFrequency + phase * Mathf.Deg2Rad) * bobAmplitude,
                Mathf.Sin(angle) * orbitRadius.y);
        }
    }
}
