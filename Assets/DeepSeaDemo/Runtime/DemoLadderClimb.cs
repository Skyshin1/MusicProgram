using UnityEngine;

namespace DeepSeaDemo
{
    [DisallowMultipleComponent]
    public sealed class DemoLadderClimb : MonoBehaviour
    {
        public Transform bottomAnchor;
        public Transform topAnchor;
        public Transform platformExit;
        [Tooltip("Maximum sideways distance from the climb path before normal movement resumes.")]
        [Min(.2f)] public float releaseDistance = 1.2f;
        [Min(.2f)] public float climbSpeed = 1.35f;
        [Min(.1f)] public float activationDistance = 3f;
        [Min(.05f)] public float alignmentSeconds = .35f;
        [Range(.05f, .8f)] public float stickDeadZone = .2f;

        public bool IsConfigured => bottomAnchor != null && topAnchor != null;

        public Vector3 ClosestPoint(Vector3 position)
        {
            Vector3 path = topAnchor.position - bottomAnchor.position;
            float t = path.sqrMagnitude > .0001f
                ? Mathf.Clamp01(Vector3.Dot(position - bottomAnchor.position, path) / path.sqrMagnitude) : 0f;
            return bottomAnchor.position + path * t;
        }
    }
}
