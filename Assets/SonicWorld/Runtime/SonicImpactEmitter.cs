using UnityEngine;

namespace SonicWorld
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class SonicImpactEmitter : MonoBehaviour
    {
        [SerializeField] private SonicSurfaceProfile profile;
        [SerializeField, Min(0f)] private float minimumRelativeSpeed = 0.25f;
        [SerializeField, Min(0f)] private float retriggerDelay = 0.045f;

        private Rigidbody ownBody;
        private float lastImpactTime = -100f;

        public SonicSurfaceProfile Profile => profile;

        public void Configure(SonicSurfaceProfile newProfile)
        {
            profile = newProfile;
        }

        private void Awake()
        {
            ownBody = GetComponentInParent<Rigidbody>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            SonicImpactEmitter otherEmitter =
                collision.collider.GetComponentInParent<SonicImpactEmitter>();

            if (otherEmitter != null &&
                otherEmitter != this &&
                GetInstanceID() > otherEmitter.GetInstanceID())
            {
                return;
            }

            if (Time.time - lastImpactTime < retriggerDelay)
                return;

            ContactPoint contact = collision.contactCount > 0
                ? collision.GetContact(0)
                : default;
            Vector3 point = collision.contactCount > 0 ? contact.point : transform.position;
            Vector3 normal = collision.contactCount > 0 ? contact.normal.normalized : Vector3.up;
            Rigidbody firstBody = ownBody;
            Rigidbody secondBody = collision.rigidbody;
            Vector3 firstVelocity = firstBody != null ? firstBody.GetPointVelocity(point) : Vector3.zero;
            Vector3 secondVelocity = secondBody != null ? secondBody.GetPointVelocity(point) : Vector3.zero;
            Vector3 relative = firstVelocity - secondVelocity;
            float normalSpeed = Mathf.Abs(Vector3.Dot(relative, normal));
            Vector3 tangentVelocity = relative - Vector3.Dot(relative, normal) * normal;
            float evaluatedRelativeSpeed = Mathf.Max(normalSpeed, tangentVelocity.magnitude * 0.25f);

            if (evaluatedRelativeSpeed < minimumRelativeSpeed)
                return;

            lastImpactTime = Time.time;
            if (otherEmitter != null)
                otherEmitter.lastImpactTime = Time.time;

            SonicSwingTracker firstSwing = GetComponentInParent<SonicSwingTracker>();
            SonicSwingTracker secondSwing =
                otherEmitter != null ? otherEmitter.GetComponentInParent<SonicSwingTracker>() : null;
            firstSwing?.NotifyCollision();
            secondSwing?.NotifyCollision();

            if (profile == null || SonicCollisionAudio.Instance == null)
                return;

            SonicSurfaceProfile otherProfile = otherEmitter != null
                ? otherEmitter.profile
                : SonicCollisionAudio.Instance.GetProfile(SonicSurfaceType.Stone);
            if (otherProfile == null)
                otherProfile = profile;

            float impulse = collision.impulse.magnitude;
            SonicSoundResult firstResult =
                profile.Evaluate(firstVelocity.magnitude, evaluatedRelativeSpeed, impulse);
            SonicSoundResult secondResult =
                otherProfile.Evaluate(secondVelocity.magnitude, evaluatedRelativeSpeed, impulse);

            SonicCollisionAudio.Instance.PlayCollision(
                point,
                profile,
                firstResult,
                otherProfile,
                secondResult,
                transform,
                otherEmitter != null ? otherEmitter.transform : null);
        }
    }
}
