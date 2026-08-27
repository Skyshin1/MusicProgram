using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Permanent VR sonar input. A Trigger press from an empty hand emits from that
/// hand. A hand that currently selects an interactable leaves Trigger entirely
/// to that object (flashlight, repair tool, and so on).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(XROrigin))]
public sealed class XRHandSonarInput : MonoBehaviour
{
    [Header("Sonar")]
    [SerializeField, Range(0f, 1f)] private float strength = 1f;
    [SerializeField, Min(0f)] private float globalCooldown = 0.4f;
    [SerializeField, Range(0.1f, 0.95f)] private float analogPressThreshold = 0.75f;

    [Header("Optional Explicit Hand Poses")]
    [SerializeField] private Transform leftHand;
    [SerializeField] private Transform rightHand;

    private XROrigin xrOrigin;
    private InputDevice leftDevice;
    private InputDevice rightDevice;
    private bool leftWasPressed;
    private bool rightWasPressed;
    private float nextAllowedTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureOnLoadedOrigins()
    {
        XROrigin[] origins = FindObjectsByType<XROrigin>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (XROrigin origin in origins)
        {
            if (origin != null && origin.GetComponent<XRHandSonarInput>() == null)
                origin.gameObject.AddComponent<XRHandSonarInput>();
        }
    }

    private void Awake()
    {
        xrOrigin = GetComponent<XROrigin>();
        ResolveHands();
    }

    private void Update()
    {
        if (!leftDevice.isValid)
            leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (!rightDevice.isValid)
            rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        bool leftPressed = ReadTrigger(leftDevice);
        bool rightPressed = ReadTrigger(rightDevice);

        if (leftPressed && !leftWasPressed)
            TryEmit(leftHand, false);
        if (rightPressed && !rightWasPressed)
            TryEmit(rightHand, true);

        leftWasPressed = leftPressed;
        rightWasPressed = rightPressed;
    }

    private bool ReadTrigger(InputDevice device)
    {
        if (!device.isValid)
            return false;
        if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool button))
            return button;
        return device.TryGetFeatureValue(CommonUsages.trigger, out float value) &&
               value >= analogPressThreshold;
    }

    private void TryEmit(Transform hand, bool right)
    {
        if (Time.unscaledTime < nextAllowedTime)
            return;

        if (hand == null)
        {
            hand = VolumetricFogPulseEmitter.FindPlayerHandTransform(right);
            if (right)
                rightHand = hand;
            else
                leftHand = hand;
        }

        if (HandIsHoldingSomething(hand))
            return;

        Transform fallback = xrOrigin != null && xrOrigin.Camera != null
            ? xrOrigin.Camera.transform
            : VolumetricFogPulseEmitter.FindPlayerViewTransform();
        Transform source = hand != null ? hand : fallback;
        Vector3 position = source != null ? source.position : transform.position;
        VolumetricFogPulseEmitter.EmitPlayerAt(position, strength, source);
        nextAllowedTime = Time.unscaledTime + globalCooldown;
    }

    private static bool HandIsHoldingSomething(Transform hand)
    {
        if (hand == null)
            return false;

        XRBaseInteractor[] interactors = hand.GetComponentsInChildren<XRBaseInteractor>(true);
        for (int i = 0; i < interactors.Length; i++)
        {
            XRBaseInteractor interactor = interactors[i];
            if (interactor != null && interactor.interactablesSelected.Count > 0)
                return true;
        }
        return false;
    }

    private void ResolveHands()
    {
        if (leftHand == null)
            leftHand = VolumetricFogPulseEmitter.FindPlayerHandTransform(false);
        if (rightHand == null)
            rightHand = VolumetricFogPulseEmitter.FindPlayerHandTransform(true);
    }

    private void OnValidate()
    {
        strength = Mathf.Clamp01(strength);
        globalCooldown = Mathf.Max(0f, globalCooldown);
        analogPressThreshold = Mathf.Clamp(analogPressThreshold, 0.1f, 0.95f);
    }
}
