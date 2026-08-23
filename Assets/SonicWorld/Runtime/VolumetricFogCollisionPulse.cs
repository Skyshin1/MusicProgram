using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Emits a standalone Water Volume sonar pulse from a physics impact point.
/// Attach this to an object that is allowed to create sonar when it collides.
/// This component deliberately has no dependency on the SonicWorld audio system.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class VolumetricFogCollisionPulse : MonoBehaviour
{
    [Header("Collision Filter")]
    [SerializeField]
    [Tooltip("Only collisions with these layers may create a pulse.")]
    private LayerMask collisionLayers = ~0;

    [SerializeField]
    [Tooltip("Collisions with these layers never create a pulse. Put your floor/terrain on a Ground layer and add it here.")]
    private LayerMask ignoredLayers;

    [SerializeField] private bool ignoreGroundTag = true;
    [SerializeField]
    [Tooltip("Optional extra ground filter. This is compared as text, so the tag does not need to exist in every project.")]
    private string groundTag = "Ground";

    [SerializeField]
    [Tooltip("When enabled, the other object must also have a Rigidbody. Disable this to allow impacts against static walls/props.")]
    private bool requireOtherRigidbody;

    [Header("Sonar Collision Groups")]
    [SerializeField]
    [Tooltip("Only objects carrying Sonar Collision Group can trigger this emitter. Keep this enabled to prevent hands/controllers from repeatedly emitting pulses.")]
    private bool requireConfiguredTarget = true;

    [SerializeField]
    [Tooltip("Allowed Sonar Collision Group IDs on the object that was hit. An empty list accepts every configured group.")]
    private List<int> acceptedTargetGroups = new List<int>();

    [Header("Impact Threshold")]
    [SerializeField, Min(0f)] private float minimumRelativeSpeed = 1.2f;
    [SerializeField, Min(0f)] private float retriggerDelay = 0.45f;
    [SerializeField]
    [Tooltip("Off by default: a continuous touch creates one pulse on impact, not a pulse every physics frame.")]
    private bool pulseWhileSliding;

    [Header("Pulse Strength")]
    [SerializeField, Range(0f, 1f)] private float minimumStrength = 0.35f;
    [SerializeField, Min(0.01f)] private float speedForMaximumStrength = 8f;
    [SerializeField, Range(0f, 1f)] private float maximumStrength = 1f;

    [Header("Optional Ring Shape Override")]
    [SerializeField]
    [Tooltip("Enable to give this object its own speed, width and radius. Disabled uses the player's sonar settings.")]
    private bool overrideRingShape;
    [SerializeField, Min(0.01f)] private float propagationSpeed = 15f;
    [SerializeField, Min(0.01f)] private float ringWidth = 0.55f;
    [SerializeField, Min(0.01f)] private float maximumRadius = 32f;
    [SerializeField, Min(0f)] private float endFadeDuration = 0.25f;

    private float lastPulseTime = float.NegativeInfinity;

    private void OnCollisionEnter(Collision collision)
    {
        TryEmit(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (pulseWhileSliding)
            TryEmit(collision);
    }

    private void TryEmit(Collision collision)
    {
        if (Time.time - lastPulseTime < retriggerDelay || collision.collider == null)
            return;

        Collider otherCollider = collision.collider;
        if (otherCollider.transform.IsChildOf(transform) || transform.IsChildOf(otherCollider.transform))
            return;

        int otherLayerMask = 1 << otherCollider.gameObject.layer;
        if ((collisionLayers.value & otherLayerMask) == 0 ||
            (ignoredLayers.value & otherLayerMask) != 0 ||
            (ignoreGroundTag && !string.IsNullOrEmpty(groundTag) &&
             otherCollider.tag == groundTag) ||
            (requireOtherRigidbody && collision.rigidbody == null))
        {
            return;
        }

        SonarCollisionGroup targetGroup = otherCollider.GetComponentInParent<SonarCollisionGroup>();
        if (requireConfiguredTarget && targetGroup == null)
            return;
        if (targetGroup != null &&
            (!targetGroup.AcceptsCollisionSonar ||
             (acceptedTargetGroups.Count > 0 && !acceptedTargetGroups.Contains(targetGroup.GroupId))))
        {
            return;
        }

        // If both colliding objects are configured as emitters, only one owns
        // this physical event. This prevents duplicate rings at the same point.
        VolumetricFogCollisionPulse otherEmitter =
            otherCollider.GetComponentInParent<VolumetricFogCollisionPulse>();
        if (otherEmitter != null && otherEmitter != this &&
            GetInstanceID() > otherEmitter.GetInstanceID())
        {
            return;
        }

        float relativeSpeed = collision.relativeVelocity.magnitude;
        if (relativeSpeed < minimumRelativeSpeed)
            return;

        Vector3 point = collision.contactCount > 0
            ? collision.GetContact(0).point
            : transform.position;
        float normalizedSpeed = Mathf.InverseLerp(
            minimumRelativeSpeed,
            Mathf.Max(minimumRelativeSpeed + 0.01f, speedForMaximumStrength),
            relativeSpeed);
        float strength = Mathf.Lerp(minimumStrength, maximumStrength, normalizedSpeed);

        lastPulseTime = Time.time;
        if (otherEmitter != null)
            otherEmitter.lastPulseTime = Time.time;

        if (overrideRingShape)
        {
            VolumetricFogPulseEmitter.EmitAt(
                point,
                strength,
                propagationSpeed,
                ringWidth,
                maximumRadius,
                endFadeDuration);
        }
        else
        {
            VolumetricFogPulseEmitter.EmitAt(point, strength);
        }
    }

    private void OnValidate()
    {
        minimumRelativeSpeed = Mathf.Max(0f, minimumRelativeSpeed);
        retriggerDelay = Mathf.Max(0f, retriggerDelay);
        minimumStrength = Mathf.Clamp01(minimumStrength);
        maximumStrength = Mathf.Clamp(maximumStrength, minimumStrength, 1f);
        speedForMaximumStrength = Mathf.Max(0.01f, speedForMaximumStrength);
        propagationSpeed = Mathf.Max(0.01f, propagationSpeed);
        ringWidth = Mathf.Max(0.01f, ringWidth);
        maximumRadius = Mathf.Max(0.01f, maximumRadius);
        endFadeDuration = Mathf.Max(0f, endFadeDuration);
    }
}
