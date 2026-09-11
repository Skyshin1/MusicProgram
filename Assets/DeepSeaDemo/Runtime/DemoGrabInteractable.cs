using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace DeepSeaDemo
{
    public sealed class DemoGrabInteractable : XRGrabInteractable
    {
        float teleportThrowUntil;
        public void SuppressTeleportThrow() => teleportThrowUntil = Time.unscaledTime + Mathf.Max(.3f, throwSmoothingDuration + .1f);
        protected override void Detach()
        {
            base.Detach();
            // Custom platform boarding is not an XRI TeleportationProvider.
            // Do not turn its position jump into a throw if released immediately.
            if (Time.unscaledTime >= teleportThrowUntil) return;
            var body = GetComponent<Rigidbody>();
            if (body != null && !body.isKinematic)
            { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
        }
        public override Transform GetAttachTransform(IXRInteractor interactor)
        {
            if (interactor is XRSocketInteractor) return base.GetAttachTransform(interactor);
            var prop = GetComponent<DemoProp>();
            var attach = prop != null && interactor != null ? prop.AttachFor(interactor.transform) : null;
            return attach != null ? attach : base.GetAttachTransform(interactor);
        }
    }
}
