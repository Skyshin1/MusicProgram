using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace SonicWorld
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
    public sealed class SonicCollisionAwareGrabTransformer : XRGeneralGrabTransformer
    {
        private const int OverlapCapacity = 32;

        [SerializeField, Min(0.001f)] private float collisionSkin = 0.012f;
        [SerializeField, Range(1, 6)] private int penetrationIterations = 3;

        private readonly Collider[] overlapResults = new Collider[OverlapCapacity];
        private Rigidbody body;
        private Collider ownCollider;

        public override void OnLink(XRGrabInteractable grabInteractable)
        {
            base.OnLink(grabInteractable);
            body = grabInteractable.GetComponent<Rigidbody>();
            ownCollider = grabInteractable.GetComponent<Collider>();
        }

        public override void Process(
            XRGrabInteractable grabInteractable,
            XRInteractionUpdateOrder.UpdatePhase updatePhase,
            ref Pose targetPose,
            ref Vector3 localScale)
        {
            base.Process(grabInteractable, updatePhase, ref targetPose, ref localScale);

            if (!grabInteractable.isSelected ||
                body == null ||
                ownCollider == null ||
                (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic &&
                 updatePhase != XRInteractionUpdateOrder.UpdatePhase.OnBeforeRender))
            {
                return;
            }

            Vector3 currentPosition = grabInteractable.transform.position;
            Vector3 displacement = targetPose.position - currentPosition;
            float distance = displacement.magnitude;
            if (distance > collisionSkin)
            {
                Vector3 direction = displacement / distance;
                if (body.SweepTest(
                        direction,
                        out RaycastHit hit,
                        distance + collisionSkin,
                        QueryTriggerInteraction.Ignore) &&
                    Vector3.Dot(direction, hit.normal) < -0.01f)
                {
                    float permittedDistance = Mathf.Max(0f, hit.distance - collisionSkin);
                    targetPose.position = currentPosition + direction * permittedDistance;
                }
            }

            ResolveTargetPenetration(ref targetPose);
        }

        private void ResolveTargetPenetration(ref Pose targetPose)
        {
            float radius = Mathf.Max(0.05f, ownCollider.bounds.extents.magnitude);
            for (int iteration = 0; iteration < penetrationIterations; iteration++)
            {
                int count = Physics.OverlapSphereNonAlloc(
                    targetPose.position,
                    radius + collisionSkin,
                    overlapResults,
                    Physics.AllLayers,
                    QueryTriggerInteraction.Ignore);

                bool adjusted = false;
                for (int i = 0; i < count; i++)
                {
                    Collider other = overlapResults[i];
                    if (other == null ||
                        other == ownCollider ||
                        other.attachedRigidbody == body ||
                        other.transform.IsChildOf(transform))
                    {
                        continue;
                    }

                    if (!Physics.ComputePenetration(
                            ownCollider,
                            targetPose.position,
                            targetPose.rotation,
                            other,
                            other.transform.position,
                            other.transform.rotation,
                            out Vector3 separationDirection,
                            out float separationDistance))
                    {
                        continue;
                    }

                    targetPose.position +=
                        separationDirection * (separationDistance + collisionSkin);
                    adjusted = true;
                }

                if (!adjusted)
                    break;
            }
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
    public sealed class SonicSwingTracker : MonoBehaviour
    {
        [SerializeField, Range(0.1f, 0.6f)] private float averagingWindow = 0.25f;
        [SerializeField, Range(0.5f, 5f)] private float armSpeed = 1.4f;
        [SerializeField, Range(0.05f, 1f)] private float stopSpeed = 0.35f;
        [SerializeField, Range(0.02f, 0.3f)] private float stopHoldTime = 0.08f;
        [SerializeField, Range(0.02f, 0.3f)] private float releaseGrace = 0.12f;
        [SerializeField, Range(0.1f, 1f)] private float triggerCooldown = 0.3f;

        private const int SampleCapacity = 32;
        private readonly float[] speeds = new float[SampleCapacity];
        private readonly float[] times = new float[SampleCapacity];

        private XRGrabInteractable grabInteractable;
        private Rigidbody body;
        private SonicImpactEmitter emitter;
        private int writeIndex;
        private int sampleCount;
        private int motionSequence;
        private int triggeredSequence = -1;
        private int collisionVersion;
        private bool selected;
        private bool armed;
        private float stopTimer;
        private float lastTriggerTime = -100f;

        private void Awake()
        {
            grabInteractable = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();
            emitter = GetComponent<SonicImpactEmitter>();

            if (!TryGetComponent<SonicCollisionAwareGrabTransformer>(out _))
                gameObject.AddComponent<SonicCollisionAwareGrabTransformer>();
        }

        private void OnEnable()
        {
            grabInteractable.selectEntered.AddListener(OnSelectEntered);
            grabInteractable.selectExited.AddListener(OnSelectExited);
        }

        private void OnDisable()
        {
            grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
            grabInteractable.selectExited.RemoveListener(OnSelectExited);
        }

        private void FixedUpdate()
        {
            if (!selected)
                return;

            float representativeRadius = 0.2f;
            Collider ownCollider = GetComponent<Collider>();
            if (ownCollider != null)
                representativeRadius = Mathf.Min(0.75f, ownCollider.bounds.extents.magnitude);

            Vector3 representativePoint =
                body.worldCenterOfMass + transform.forward * representativeRadius;
            float speed = body.GetPointVelocity(representativePoint).magnitude;
            RecordSpeed(speed);
            float average = GetAverageSpeed();

            if (!armed &&
                average >= armSpeed &&
                Time.time - lastTriggerTime >= triggerCooldown)
            {
                armed = true;
                stopTimer = 0f;
                motionSequence++;
            }

            if (!armed)
                return;

            if (speed <= stopSpeed && average <= armSpeed * 0.65f)
            {
                stopTimer += Time.fixedDeltaTime;
                if (stopTimer >= stopHoldTime)
                    TriggerSolo(motionSequence, average);
            }
            else
            {
                stopTimer = 0f;
            }
        }

        public void NotifyCollision()
        {
            collisionVersion++;
            armed = false;
            stopTimer = 0f;
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            selected = true;
            armed = false;
            stopTimer = 0f;
            writeIndex = 0;
            sampleCount = 0;
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            selected = false;
            stopTimer = 0f;
            if (!armed)
                return;

            int sequence = motionSequence;
            int collisionSnapshot = collisionVersion;
            float average = GetAverageSpeed();
            StartCoroutine(TriggerAfterReleaseGrace(sequence, collisionSnapshot, average));
        }

        private IEnumerator TriggerAfterReleaseGrace(
            int sequence,
            int collisionSnapshot,
            float average)
        {
            yield return new WaitForSeconds(releaseGrace);
            if (collisionVersion == collisionSnapshot)
                TriggerSolo(sequence, average);
        }

        private void TriggerSolo(int sequence, float averageSpeed)
        {
            if (!armed || sequence == triggeredSequence)
                return;

            armed = false;
            triggeredSequence = sequence;
            lastTriggerTime = Time.time;
            if (SonicCollisionAudio.Instance != null && emitter != null)
            {
                SonicCollisionAudio.Instance.PlaySolo(
                    transform.position,
                    emitter.Profile,
                    Mathf.Max(averageSpeed, stopSpeed),
                    transform);
            }
        }

        private void RecordSpeed(float speed)
        {
            speeds[writeIndex] = speed;
            times[writeIndex] = Time.time;
            writeIndex = (writeIndex + 1) % SampleCapacity;
            sampleCount = Mathf.Min(sampleCount + 1, SampleCapacity);
        }

        private float GetAverageSpeed()
        {
            if (sampleCount == 0)
                return 0f;

            float now = Time.time;
            float weightedSum = 0f;
            float weightSum = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                int index = (writeIndex - 1 - i + SampleCapacity) % SampleCapacity;
                float age = now - times[index];
                if (age > averagingWindow)
                    break;

                float weight = Mathf.Lerp(1f, 0.4f, age / averagingWindow);
                weightedSum += speeds[index] * weight;
                weightSum += weight;
            }

            return weightSum > 0f ? weightedSum / weightSum : 0f;
        }
    }
}
