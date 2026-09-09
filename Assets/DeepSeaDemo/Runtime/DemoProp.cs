using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace DeepSeaDemo
{
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
        public bool Submitted { get; private set; }
        public XRGrabInteractable Grab { get; private set; }
        Rigidbody body;
        float nextCheck;
        void Awake()
        {
            Grab = GetComponent<XRGrabInteractable>(); body = GetComponent<Rigidbody>();
            Grab.selectEntered.AddListener(PickedUp);
        }
        void OnDestroy()
        {
            if (Grab == null) return;
            Grab.selectEntered.RemoveListener(PickedUp);
        }
        public Transform AttachFor(Transform interactor)
        {
            var input = DemoInputRouter.Instance;
            bool right = input == null || input.IsRight(interactor);
            return right ? rightAttach : leftAttach;
        }
        void PickedUp(SelectEnterEventArgs e)
        {
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
        public void Release()
        {
            if (Grab == null || Grab.interactionManager == null) return;
            var selectors = new List<UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor>(Grab.interactorsSelecting);
            foreach (var selector in selectors) Grab.interactionManager.SelectExit(selector, Grab);
        }
    }
}
