using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Quest/OpenXR locomotion for scenes that use this project's XR Origin.
/// The left thumbstick moves horizontally relative to the tracked headset;
/// the right thumbstick rotates the XR Origin around the headset position.
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

    [Header("Right Stick Turning")]
    [SerializeField]
    [Tooltip("Recommended for Quest comfort. Turn the stick left/right once to rotate by Snap Turn Degrees.")]
    private bool useSnapTurn = true;
    [SerializeField, Range(15f, 90f)] private float snapTurnDegrees = 45f;
    [SerializeField, Range(0.1f, 0.95f)] private float turnDeadZone = 0.7f;
    [SerializeField, Min(0f)] private float snapTurnCooldown = 0.2f;
    [SerializeField, Range(10f, 360f)] private float smoothTurnDegreesPerSecond = 120f;

    private XROrigin xrOrigin;
    private CharacterController characterController;
    private InputDevice leftController;
    private InputDevice rightController;
    private float nextSnapTurnTime;
    private bool snapTurnReady = true;

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
        if (disableWhenAnotherMoveProviderExists && HasAnotherMoveProvider())
            return;

        UpdateMovement();
        UpdateTurning();
    }

    private void UpdateMovement()
    {
        if (moveSpeed <= 0f)
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

    private void UpdateTurning()
    {
        if (!rightController.isValid)
            rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (!rightController.isValid ||
            !rightController.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick))
        {
            return;
        }

        float horizontal = stick.x;
        if (useSnapTurn)
        {
            if (Mathf.Abs(horizontal) < turnDeadZone)
            {
                snapTurnReady = true;
                return;
            }

            if (!snapTurnReady || Time.unscaledTime < nextSnapTurnTime)
                return;

            RotateOriginAroundHead(Mathf.Sign(horizontal) * snapTurnDegrees);
            nextSnapTurnTime = Time.unscaledTime + snapTurnCooldown;
            snapTurnReady = false;
            return;
        }

        if (Mathf.Abs(horizontal) < deadZone)
            return;

        RotateOriginAroundHead(horizontal * smoothTurnDegreesPerSecond * Time.deltaTime);
    }

    private void RotateOriginAroundHead(float degrees)
    {
        if (Mathf.Approximately(degrees, 0f))
            return;

        Transform head = xrOrigin != null ? xrOrigin.Camera?.transform : null;
        Vector3 headPosition = head != null ? head.position : transform.position;
        Vector3 offsetFromHead = transform.position - headPosition;
        Quaternion rotation = Quaternion.AngleAxis(degrees, Vector3.up);

        transform.rotation = rotation * transform.rotation;
        // Room-scale users may stand away from the XR Origin. Keep the actual
        // headset world position stable while rotating, rather than orbiting it.
        transform.position = headPosition + rotation * offsetFromHead;
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
        turnDeadZone = Mathf.Clamp(turnDeadZone, 0.1f, 0.95f);
        snapTurnDegrees = Mathf.Clamp(snapTurnDegrees, 15f, 90f);
        snapTurnCooldown = Mathf.Max(0f, snapTurnCooldown);
        smoothTurnDegreesPerSecond = Mathf.Clamp(smoothTurnDegreesPerSecond, 10f, 360f);
    }
}
