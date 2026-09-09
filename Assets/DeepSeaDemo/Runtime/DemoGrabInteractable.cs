using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace DeepSeaDemo
{
    public sealed class DemoGrabInteractable : XRGrabInteractable
    {
        public override Transform GetAttachTransform(IXRInteractor interactor)
        {
            if (interactor is XRSocketInteractor) return base.GetAttachTransform(interactor);
            var prop = GetComponent<DemoProp>();
            var attach = prop != null && interactor != null ? prop.AttachFor(interactor.transform) : null;
            return attach != null ? attach : base.GetAttachTransform(interactor);
        }
    }
}
