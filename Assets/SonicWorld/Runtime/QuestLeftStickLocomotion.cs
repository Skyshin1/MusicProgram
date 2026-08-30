using AbstractOcclusion.WebGpuWater;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.InputSystem;

/// <summary>
/// Quest/OpenXR locomotion for this project's XR Origin. The left stick moves
/// horizontally relative to the headset, right-stick X turns, and right-stick
/// Y swims up/down. A CharacterController supplies collision and gravity.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
[DefaultExecutionOrder(-50)]
public sealed class QuestLeftStickLocomotion : MonoBehaviour
{
    [Header("Horizontal Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 2f;
    [SerializeField, Range(0f, 0.95f)] private float deadZone = 0.15f;
    [SerializeField] private bool headRelative = true;
    [SerializeField]
    [Tooltip("When an XRI move provider is added later, this component stands down to avoid double movement.")]
    private bool disableWhenAnotherMoveProviderExists = true;

    [Header("Underwater Vertical Movement")]
    [SerializeField, Min(0f)] private float verticalSwimSpeed = 1.5f;
    [SerializeField, Min(0f)] private float verticalAcceleration = 4f;
    [SerializeField, Min(0f)] private float underwaterSinkAcceleration = 0.8f;
    [SerializeField, Min(0f)] private float maximumSinkSpeed = 0.6f;
    [SerializeField] private float airGravity = -9.81f;
    [SerializeField, Min(0f)] private float maximumAirFallSpeed = 25f;

    [Header("Water Surface Floating")]
    [SerializeField, Min(0f)] private float surfaceEyeHeight = 0.15f;
    [SerializeField, Min(0.1f)] private float surfaceSnapSpeed = 4f;
    [SerializeField, Min(0.1f)] private float surfaceCaptureDistance = 0.8f;
    [SerializeField, Range(0.1f, 0.95f)] private float diveInputThreshold = 0.45f;
    [SerializeField] private bool allowDesktopDiveKey = true;
    [SerializeField] private Key desktopDiveKey = Key.Q;

    [Header("Player Collision Capsule")]
    [SerializeField, Range(0.1f, 0.5f)] private float capsuleRadius = 0.25f;
    [SerializeField, Range(0.5f, 1.2f)] private float minimumCapsuleHeight = 0.8f;
    [SerializeField, Range(1.2f, 2.5f)] private float maximumCapsuleHeight = 2.2f;
    [SerializeField, Range(0f, 0.2f)] private float headClearance = 0.05f;

    [Header("Right Stick Turning")]
    [SerializeField] private bool useSnapTurn = true;
    [SerializeField, Range(15f, 90f)] private float snapTurnDegrees = 45f;
    [SerializeField, Range(0.1f, 0.95f)] private float turnDeadZone = 0.7f;
    [SerializeField, Min(0f)] private float snapTurnCooldown = 0.2f;
    [SerializeField, Range(10f, 360f)] private float smoothTurnDegreesPerSecond = 120f;

    private XROrigin xrOrigin;
    private CharacterController characterController;
    private WaterSurfaceStateTracker waterState;
    private UnityEngine.XR.InputDevice leftController;
    private UnityEngine.XR.InputDevice rightController;
    private float nextSnapTurnTime;
    private bool snapTurnReady = true;
    private float verticalVelocity;
    private bool movementEnabled = true;
    private bool hasConflictingMoveProvider;
    private bool surfaceFloating;

    public bool IsUnderwater => waterState != null && waterState.IsUnderwater;
    public WaterVolume CurrentWater => waterState != null ? waterState.CurrentWater : null;
    public float VerticalVelocity => verticalVelocity;
    public bool MovementEnabled => movementEnabled;
    public bool IsSurfaceFloating => surfaceFloating;

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
        characterController = GetComponent<CharacterController>();
        // RequireComponent is not applied retroactively to scene instances
        // that already had this script before the requirement was introduced.
        if (characterController == null)
            characterController = gameObject.AddComponent<CharacterController>();
        waterState = GetComponent<WaterSurfaceStateTracker>();
        if (waterState == null)
            waterState = gameObject.AddComponent<WaterSurfaceStateTracker>();

        characterController.radius = capsuleRadius;
        characterController.skinWidth = Mathf.Min(0.03f, capsuleRadius * 0.2f);
        characterController.stepOffset = Mathf.Min(0.25f, minimumCapsuleHeight * 0.3f);
        characterController.slopeLimit = 55f;
        hasConflictingMoveProvider = HasAnotherMoveProvider();
    }

    private void Update()
    {
        if (!movementEnabled || (disableWhenAnotherMoveProviderExists && hasConflictingMoveProvider))
            return;

        UpdateCollisionCapsule();
        ReadControllerAxes(out Vector2 leftStick, out Vector2 rightStick);
        UpdateMovement(leftStick, rightStick.y);
        UpdateTurning(rightStick.x);
    }

    public void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;
        if (!enabled)
            verticalVelocity = 0f;
    }

    private void ReadControllerAxes(out Vector2 leftStick, out Vector2 rightStick)
    {
        leftStick = Vector2.zero;
        rightStick = Vector2.zero;

        if (!leftController.isValid)
            leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (leftController.isValid)
            leftController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out leftStick);

        if (!rightController.isValid)
            rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (rightController.isValid)
            rightController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out rightStick);

        if (leftStick.sqrMagnitude < deadZone * deadZone)
            leftStick = Vector2.zero;
        if (Mathf.Abs(rightStick.y) < deadZone)
            rightStick.y = 0f;
    }

    private void UpdateMovement(Vector2 leftStick, float verticalInput)
    {
        Transform reference = headRelative && xrOrigin != null && xrOrigin.Camera != null
            ? xrOrigin.Camera.transform
            : transform;
        Vector3 forward = Vector3.ProjectOnPlane(reference.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(reference.right, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;
        if (right.sqrMagnitude < 0.001f)
            right = Vector3.right;

        Vector3 horizontal = forward * leftStick.y + right * leftStick.x;
        if (horizontal.sqrMagnitude > 1f)
            horizontal.Normalize();
        horizontal *= moveSpeed;

        float dt = Time.deltaTime;
        bool diveRequested = verticalInput <= -diveInputThreshold ||
            (allowDesktopDiveKey && !Application.isMobilePlatform && Keyboard.current != null &&
             Keyboard.current[desktopDiveKey].isPressed);
        float surfaceY = 0f;
        bool hasSurface = waterState != null && waterState.TryGetSurfaceHeight(out surfaceY);
        float headY = xrOrigin != null && xrOrigin.Camera != null
            ? xrOrigin.Camera.transform.position.y
            : transform.position.y;

        if (surfaceFloating)
        {
            if (diveRequested)
            {
                surfaceFloating = false;
                verticalVelocity = -verticalSwimSpeed;
            }
            else if (hasSurface)
            {
                float targetHeadY = surfaceY + surfaceEyeHeight;
                verticalVelocity = Mathf.Clamp((targetHeadY - headY) * surfaceSnapSpeed,
                    -verticalSwimSpeed, verticalSwimSpeed);
                CollisionFlags surfaceFlags = characterController.Move(
                    (horizontal + Vector3.up * verticalVelocity) * dt);
                if ((surfaceFlags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
                    verticalVelocity = 0f;
                return;
            }
            else
            {
                surfaceFloating = false;
            }
        }

        if (!IsUnderwater && hasSurface && !diveRequested &&
            Mathf.Abs(headY - (surfaceY + surfaceEyeHeight)) <= surfaceCaptureDistance)
        {
            surfaceFloating = true;
            verticalVelocity = 0f;
            return;
        }

        if (IsUnderwater)
        {
            float targetVertical = Mathf.Abs(verticalInput) > 0.001f
                ? verticalInput * verticalSwimSpeed
                : -maximumSinkSpeed;
            float acceleration = Mathf.Abs(verticalInput) > 0.001f
                ? verticalAcceleration
                : underwaterSinkAcceleration;
            verticalVelocity = Mathf.MoveTowards(verticalVelocity, targetVertical, acceleration * dt);
        }
        else
        {
            if (characterController.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -0.5f;
            else
                verticalVelocity = Mathf.Max(-maximumAirFallSpeed, verticalVelocity + airGravity * dt);
        }

        CollisionFlags flags = characterController.Move(
            (horizontal + Vector3.up * verticalVelocity) * dt);
        if ((flags & CollisionFlags.Below) != 0 && verticalVelocity < 0f)
            verticalVelocity = IsUnderwater ? 0f : -0.5f;
        if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            verticalVelocity = 0f;
    }

    private void UpdateCollisionCapsule()
    {
        if (xrOrigin == null || xrOrigin.Camera == null || characterController == null)
            return;

        Vector3 headLocal = transform.InverseTransformPoint(xrOrigin.Camera.transform.position);
        float height = Mathf.Clamp(headLocal.y + headClearance,
            minimumCapsuleHeight, maximumCapsuleHeight);
        characterController.height = height;
        characterController.radius = Mathf.Min(capsuleRadius, height * 0.45f);
        characterController.center = new Vector3(headLocal.x, height * 0.5f, headLocal.z);
    }

    private void UpdateTurning(float horizontal)
    {
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

        if (Mathf.Abs(horizontal) >= deadZone)
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
        verticalSwimSpeed = Mathf.Max(0f, verticalSwimSpeed);
        verticalAcceleration = Mathf.Max(0f, verticalAcceleration);
        underwaterSinkAcceleration = Mathf.Max(0f, underwaterSinkAcceleration);
        maximumSinkSpeed = Mathf.Max(0f, maximumSinkSpeed);
        maximumAirFallSpeed = Mathf.Max(0f, maximumAirFallSpeed);
        surfaceEyeHeight = Mathf.Max(0f, surfaceEyeHeight);
        surfaceSnapSpeed = Mathf.Max(0.1f, surfaceSnapSpeed);
        surfaceCaptureDistance = Mathf.Max(0.1f, surfaceCaptureDistance);
        diveInputThreshold = Mathf.Clamp(diveInputThreshold, 0.1f, 0.95f);
        deadZone = Mathf.Clamp(deadZone, 0f, 0.95f);
        turnDeadZone = Mathf.Clamp(turnDeadZone, 0.1f, 0.95f);
        snapTurnDegrees = Mathf.Clamp(snapTurnDegrees, 15f, 90f);
        snapTurnCooldown = Mathf.Max(0f, snapTurnCooldown);
        smoothTurnDegreesPerSecond = Mathf.Clamp(smoothTurnDegreesPerSecond, 10f, 360f);
        minimumCapsuleHeight = Mathf.Max(0.5f, minimumCapsuleHeight);
        maximumCapsuleHeight = Mathf.Max(minimumCapsuleHeight, maximumCapsuleHeight);
    }
}
