using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Attachment;

namespace DeepSeaDemo
{
    public enum DemoPropGrabMode { StableAtHand, ThrowableAtDistance }

    [RequireComponent(typeof(XRGrabInteractable), typeof(Rigidbody))]
    public sealed class DemoProp : MonoBehaviour
    {
        public string id;
        public string factOnPickup;
        public Transform recoveryPoint;
        public bool critical = true;
        public Transform leftAttach;
        public Transform rightAttach;
        public float fingerCurl = .8f;
        [Header("Grab Behaviour")]
        public DemoPropGrabMode grabMode = DemoPropGrabMode.StableAtHand;
        [Min(0f)] public float throwableVelocityScale = 1.45f;
        [Min(0f)] public float throwableAngularVelocityScale = .9f;
        public bool Submitted { get; private set; }
        public XRGrabInteractable Grab { get; private set; }
        Rigidbody body;
        Renderer[] sonarRenderers;
        float nextCheck;
        void Awake()
        {
            Grab = GetComponent<XRGrabInteractable>(); body = GetComponent<Rigidbody>();
            ApplyGrabMode();
            sonarRenderers = GetComponentsInChildren<Renderer>(true);
            Grab.selectEntered.AddListener(PickedUp);
            Grab.selectExited.AddListener(Released);
        }
        public void ApplyGrabMode()
        {
            if (Grab == null) Grab = GetComponent<XRGrabInteractable>();
            bool throwable = grabMode == DemoPropGrabMode.ThrowableAtDistance;
            // Evidence and tools always snap to their authored hand pose. Stones deliberately
            // retain the ray distance so a short controller swing can create a useful impact.
            Grab.farAttachMode = throwable ? InteractableFarAttachMode.Far : InteractableFarAttachMode.Near;
            Grab.useDynamicAttach = throwable;
            Grab.throwOnDetach = true;
            Grab.movementType = throwable
                ? XRBaseInteractable.MovementType.VelocityTracking
                : XRBaseInteractable.MovementType.Instantaneous;
            Grab.smoothPosition = false;
            Grab.smoothRotation = false;
            Grab.attachEaseInTime = throwable ? .05f : 0f;
            if (throwable)
            {
                Grab.throwVelocityScale = throwableVelocityScale;
                Grab.throwAngularVelocityScale = throwableAngularVelocityScale;
            }
        }
        void OnDestroy()
        {
            SetSonarSuppressed(false);
            if (Grab == null) return;
            Grab.selectEntered.RemoveListener(PickedUp);
            Grab.selectExited.RemoveListener(Released);
        }
        void OnDisable() => SetSonarSuppressed(false);
        void OnEnable() { if (Grab != null) RefreshSonarSuppression(); }
        public void RefreshVisualRenderers()
        {
            SetSonarSuppressed(false);
            sonarRenderers = GetComponentsInChildren<Renderer>(true);
            RefreshSonarSuppression();
        }
        void Released(SelectExitEventArgs e) => RefreshSonarSuppression();
        void RefreshSonarSuppression()
        {
            bool held = false;
            foreach (var selector in Grab.interactorsSelecting)
                if (selector is not UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor) { held = true; break; }
            SetSonarSuppressed(held);
        }
        void SetSonarSuppressed(bool suppressed)
        {
            if (sonarRenderers == null) return;
            foreach (var renderer in sonarRenderers) SonarRevealManager.SetSuppressed(renderer, suppressed);
        }
        public Transform AttachFor(Transform interactor)
        {
            var input = DemoInputRouter.Instance;
            bool right = input == null || input.IsRight(interactor);
            return right ? rightAttach : leftAttach;
        }
        void PickedUp(SelectEnterEventArgs e)
        {
            RefreshSonarSuppression();
            if (e.interactorObject is UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor) return;
            DemoFlow.Instance?.Record(factOnPickup);
        }
        void Update()
        {
            if (!critical || Submitted || recoveryPoint == null || Grab.isSelected || Time.time < nextCheck) return;
            nextCheck = Time.time + 2f;
            Vector3 p = transform.position;
            if (p.y < -40f || p.y > 50f || Mathf.Abs(p.x) > 100f || Mathf.Abs(p.z) > 110f)
                Recover();
        }
        public void Recover()
        {
            if (Submitted || Grab.isSelected || recoveryPoint == null) return;
            body.position = recoveryPoint.position; body.rotation = recoveryPoint.rotation;
            body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
            DemoFlow.Instance?.ui.Toast(name + DemoTextCatalog.Get("runtime.024"));
        }
        public PropSnapshot Capture()
        {
            bool held = Grab != null && Grab.isSelected;
            Transform pose = held && recoveryPoint != null ? recoveryPoint : transform;
            return new PropSnapshot { id = id, position = pose.position, rotation = pose.rotation,
                submitted = Submitted, lightOn = GetComponent<GrabFlashlight>()?.IsOn ?? false };
        }
        public void Restore(PropSnapshot snapshot)
        {
            gameObject.SetActive(true);
            Release(); Submitted = snapshot.submitted;
            body.isKinematic = false;
            body.position = snapshot.position; body.rotation = snapshot.rotation;
            transform.SetPositionAndRotation(snapshot.position, snapshot.rotation);
            body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
            GetComponent<GrabFlashlight>()?.SetLight(snapshot.lightOn);
            if (Submitted) gameObject.SetActive(false);
        }
        public void Submit()
        {
            Release(); Submitted = true; gameObject.SetActive(false);
        }
        public void MoveWithPlayer(Pose before, Pose after)
        {
            Quaternion turn = after.rotation * Quaternion.Inverse(before.rotation);
            Vector3 position = after.position + turn * (transform.position - before.position);
            Quaternion rotation = turn * transform.rotation;
            body.position = position; body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
            Grab.SetTargetPose(new Pose(position, rotation));
            if (Grab is DemoGrabInteractable demoGrab) demoGrab.SuppressTeleportThrow();
        }
        public void Release()
        {
            if (Grab == null || Grab.interactionManager == null) return;
            var selectors = new List<UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor>(Grab.interactorsSelecting);
            foreach (var selector in selectors) Grab.interactionManager.SelectExit(selector, Grab);
        }
    }
}
