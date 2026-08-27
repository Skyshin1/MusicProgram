using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace DeepSeaAI
{
    /// <summary>
    /// Attach alongside XRGrabInteractable. While the tool is held, holding the
    /// XR Activate input repairs the nearest compatible RepairableFacility.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class RepairTool : MonoBehaviour
    {
        private const int HitCapacity = 16;

        [Header("Identity")]
        [SerializeField] private string toolId = "StandardRepairTool";

        [Header("Repair Reach")]
        [SerializeField] private Transform repairTip;
        [SerializeField, Min(0.02f)] private float repairRadius = 0.32f;
        [SerializeField] private LayerMask repairLayers = ~0;

        [Header("Input")]
        [Tooltip("Allows R to repair during desktop Play Mode without grabbing the tool. It is ignored in a device build.")]
        [SerializeField] private bool allowDesktopKeyboardTest = true;
        [SerializeField] private Key desktopRepairKey = Key.R;

        [Header("Feedback")]
        [SerializeField] private LineRenderer repairBeam;
        [SerializeField] private Color beamColor = new(0.4f, 1f, 0.95f, 1f);

        private readonly Collider[] nearby = new Collider[HitCapacity];
        private XRGrabInteractable grabInteractable;
        private bool activateHeld;
        private RepairableFacility currentTarget;

        public string ToolId => toolId;
        public RepairableFacility CurrentTarget => currentTarget;

        public void Configure(string id, Transform tip, LineRenderer beam)
        {
            toolId = string.IsNullOrWhiteSpace(id) ? "StandardRepairTool" : id;
            repairTip = tip;
            repairBeam = beam;
            ConfigureBeam();
        }

        private void Awake()
        {
            grabInteractable = GetComponent<XRGrabInteractable>();
            ConfigureBeam();
        }

        private void OnEnable()
        {
            if (grabInteractable == null)
                grabInteractable = GetComponent<XRGrabInteractable>();
            grabInteractable.activated.AddListener(OnActivated);
            grabInteractable.deactivated.AddListener(OnDeactivated);
        }

        private void OnDisable()
        {
            if (grabInteractable == null)
                return;
            grabInteractable.activated.RemoveListener(OnActivated);
            grabInteractable.deactivated.RemoveListener(OnDeactivated);
            activateHeld = false;
            SetBeam(false, null);
        }

        private void Update()
        {
            bool grabbed = grabInteractable != null && grabInteractable.isSelected;
            bool desktopInput = allowDesktopKeyboardTest &&
                !Application.isMobilePlatform &&
                Keyboard.current != null &&
                Keyboard.current[desktopRepairKey].isPressed;
            bool repairing = (grabbed && activateHeld) || desktopInput;

            currentTarget = repairing ? FindNearestTarget() : null;
            if (currentTarget == null)
            {
                SetBeam(false, null);
                return;
            }

            currentTarget.Repair(toolId, Time.deltaTime);
            SetBeam(true, currentTarget);
        }

        private void OnActivated(ActivateEventArgs args)
        {
            activateHeld = true;
        }

        private void OnDeactivated(DeactivateEventArgs args)
        {
            activateHeld = false;
        }

        private RepairableFacility FindNearestTarget()
        {
            Vector3 origin = repairTip != null ? repairTip.position : transform.position;
            int count = Physics.OverlapSphereNonAlloc(
                origin,
                repairRadius,
                nearby,
                repairLayers,
                QueryTriggerInteraction.Collide);

            RepairableFacility best = null;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider hit = nearby[i];
                if (hit == null)
                    continue;

                RepairableFacility candidate = hit.GetComponentInParent<RepairableFacility>();
                if (candidate == null || !candidate.AcceptsTool(toolId))
                    continue;

                float distance = (hit.ClosestPoint(origin) - origin).sqrMagnitude;
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                best = candidate;
            }
            return best;
        }

        private void ConfigureBeam()
        {
            if (repairBeam == null)
                return;
            repairBeam.useWorldSpace = true;
            repairBeam.positionCount = 2;
            repairBeam.startWidth = 0.009f;
            repairBeam.endWidth = 0.018f;
            repairBeam.startColor = beamColor;
            repairBeam.endColor = beamColor;
            repairBeam.enabled = false;
        }

        private void SetBeam(bool visible, RepairableFacility target)
        {
            if (repairBeam == null)
                return;
            repairBeam.enabled = visible;
            if (!visible || target == null)
                return;

            Vector3 origin = repairTip != null ? repairTip.position : transform.position;
            repairBeam.SetPosition(0, origin);
            repairBeam.SetPosition(1, target.transform.position + Vector3.up * 0.55f);
        }

        private void OnDrawGizmosSelected()
        {
            Transform point = repairTip != null ? repairTip : transform;
            Gizmos.color = new Color(0.25f, 1f, 0.9f, 0.6f);
            Gizmos.DrawWireSphere(point.position, repairRadius);
        }

        private void OnValidate()
        {
            repairRadius = Mathf.Max(0.02f, repairRadius);
        }
    }
}
