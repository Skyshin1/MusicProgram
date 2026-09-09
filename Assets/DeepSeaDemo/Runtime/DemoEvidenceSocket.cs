using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace DeepSeaDemo
{
    public sealed class DemoEvidenceSocket : XRSocketInteractor
    {
        public string requiredId = "blackbox";
        public override bool CanSelect(IXRSelectInteractable interactable)
        {
            var prop = interactable.transform.GetComponent<DemoProp>();
            return prop != null && prop.id == requiredId && base.CanSelect(interactable);
        }
        public override bool CanHover(IXRHoverInteractable interactable)
        {
            var prop = interactable.transform.GetComponent<DemoProp>();
            return prop != null && prop.id == requiredId && base.CanHover(interactable);
        }
    }
}
