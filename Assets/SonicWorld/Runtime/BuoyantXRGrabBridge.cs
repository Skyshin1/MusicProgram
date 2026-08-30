using AbstractOcclusion.WebGpuWater;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Prevents WaterBuoyancy from fighting XRI's velocity tracking while a prop is
/// held, then restores the authored buoyancy when it is released.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(WaterBuoyancy))]
public sealed class BuoyantXRGrabBridge : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)]
    [Tooltip("0 completely suspends water forces while held; a small value keeps subtle drag.")]
    private float heldForceScale;
    [SerializeField, Range(0f, 1f)]
    [Tooltip("Water-force multiplier restored after release. Set this to 0 for objects that should sink under gravity.")]
    private float releasedForceScale = 1f;
    [SerializeField] private bool clearVelocitiesOnGrab;

    private XRGrabInteractable grab;
    private WaterBuoyancy buoyancy;

    public float HeldForceScale
    {
        get => heldForceScale;
        set => heldForceScale = Mathf.Clamp01(value);
    }

    public float ReleasedForceScale
    {
        get => releasedForceScale;
        set => releasedForceScale = Mathf.Clamp01(value);
    }

    private void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        buoyancy = GetComponent<WaterBuoyancy>();
    }

    private void OnEnable()
    {
        grab.selectEntered.AddListener(OnGrabbed);
        grab.selectExited.AddListener(OnReleased);
        buoyancy.ExternalForceScale = grab.isSelected ? heldForceScale : releasedForceScale;
    }

    private void OnDisable()
    {
        if (grab != null)
        {
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnReleased);
        }
        if (buoyancy != null)
            buoyancy.ExternalForceScale = releasedForceScale;
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        buoyancy.ExternalForceScale = heldForceScale;
        if (!clearVelocitiesOnGrab || !TryGetComponent(out Rigidbody body))
            return;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        buoyancy.ExternalForceScale = releasedForceScale;
    }
}
