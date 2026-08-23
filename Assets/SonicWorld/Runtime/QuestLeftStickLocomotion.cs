using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Quest/OpenXR locomotion for scenes that use this project's XR Origin.
/// The left thumbstick moves horizontally relative to the tracked headset.
/// A parent CharacterController is used when present, preserving collision.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public sealed class QuestLeftStickLocomotion : MonoBehaviour
{
    [Header("Left Stick Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 2.2f;
    [SerializeField, Range(0f, 0.95f)] private float deadZone = 0.15f;
    [SerializeField] private bool headRelative = true;
    [SerializeField]
    [Tooltip("When an XRI move provider is added later, this component automatically stands down to avoid double movement.")]
    private bool disableWhenAnotherMoveProviderExists = true;

    private XROrigin xrOrigin;
    private CharacterController characterController;
    private InputDevice leftController;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureOnLoadedOrigins()
    {
        XROrigin[] origins = FindObjectsByType<XROrigin>(FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        foreach (XROrigin origin in origins)
        {
            if (origin != null && origin.GetComponent<QuestLeftStickLocomotion>() == null)
                origin.gameObject.AddComponent<QuestLeftStickLocomotion>();
        }
    }

    private void Awake()
    {
        xrOrigin = GetComponent<XROrigin>();
        characterController = GetComponentInParent<CharacterController>();
    }

    private void Update()
    {
        if (moveSpeed <= 0f || (disableWhenAnotherMoveProviderExists && HasAnotherMoveProvider()))
            return;

        if (!leftController.isValid)
            leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (!leftController.isValid ||
            !leftController.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick))
            return;

        if (stick.sqrMagnitude < deadZone * deadZone)
            return;

        Transform reference = headRelative && xrOrigin != null && xrOrigin.Camera != null
            ? xrOrigin.Camera.transform
            : transform;
        Vector3 forward = Vector3.ProjectOnPlane(reference.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(reference.right, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;
        if (right.sqrMagnitude < 0.001f)
            right = Vector3.right;

        Vector3 movement = (forward * stick.y + right * stick.x);
        if (movement.sqrMagnitude > 1f)
            movement.Normalize();
        movement *= moveSpeed * Time.deltaTime;

        if (characterController != null && characterController.enabled)
            characterController.Move(movement);
        else
            transform.position += movement;
    }

    private bool HasAnotherMoveProvider()
    {
        Behaviour[] behaviours = GetComponents<Behaviour>();
        foreach (Behaviour behaviour in behaviours)
        {
            if (behaviour != null && behaviour != this && behaviour.enabled &&
                behaviour.GetType().Name.Contains("MoveProvider"))
                return true;
        }
        return false;
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        deadZone = Mathf.Clamp(deadZone, 0f, 0.95f);
    }
}
