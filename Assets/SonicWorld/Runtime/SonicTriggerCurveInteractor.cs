using UnityEngine;
using UnityEngine.InputSystem;

namespace SonicWorld
{
    [DisallowMultipleComponent]
    public sealed class SonicTriggerCurveInteractor : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string triggerActionName;
        [SerializeField, Range(0.05f, 0.4f)] private float reach = 0.16f;
        [SerializeField, Range(0.2f, 0.8f)] private float visualRange = 0.35f;
        [SerializeField] private LayerMask controlLayer = ~0;

        private readonly Collider[] overlapBuffer = new Collider[16];
        private InputAction triggerAction;
        private SonicCurveControlPoint grabbedPoint;
        private Vector3 grabOffset;

        public void Configure(
            InputActionAsset actions,
            string actionName,
            LayerMask layer)
        {
            inputActions = actions;
            triggerActionName = actionName;
            controlLayer = layer;
        }

        private void OnEnable()
        {
            triggerAction = inputActions != null
                ? inputActions.FindAction(triggerActionName, false)
                : null;
            triggerAction?.Enable();
        }

        private void OnDisable()
        {
            Release();
        }

        private void Update()
        {
            if (grabbedPoint != null && !grabbedPoint.RuntimeEditingAllowed)
                Release();

            SonicCurveControlPoint nearest = FindNearest(out float nearestDistance);
            if (nearest != null)
            {
                float proximity = 1f - Mathf.InverseLerp(reach, visualRange, nearestDistance);
                nearest.ReportProximity(proximity);
            }

            if (grabbedPoint != null)
            {
                grabbedPoint.ReportProximity(1f);
                grabbedPoint.transform.position = transform.position + grabOffset;
                if (triggerAction == null || !triggerAction.IsPressed())
                    Release();
                return;
            }

            if (triggerAction != null &&
                triggerAction.WasPressedThisFrame() &&
                nearest != null &&
                nearestDistance <= reach &&
                nearest.TryBeginGrab(this))
            {
                grabbedPoint = nearest;
                grabOffset = nearest.transform.position - transform.position;
            }
        }

        private SonicCurveControlPoint FindNearest(out float nearestDistance)
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position,
                visualRange,
                overlapBuffer,
                controlLayer,
                QueryTriggerInteraction.Collide);
            SonicCurveControlPoint nearest = null;
            nearestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Collider candidateCollider = overlapBuffer[i];
                if (candidateCollider == null)
                    continue;
                SonicCurveControlPoint candidate =
                    candidateCollider.GetComponentInParent<SonicCurveControlPoint>();
                if (candidate == null || !candidate.RuntimeEditingAllowed)
                    continue;

                float distance = Vector3.Distance(
                    transform.position,
                    candidate.transform.position);
                candidate.ReportProximity(
                    1f - Mathf.InverseLerp(reach, visualRange, distance));
                if (distance < nearestDistance)
                {
                    nearest = candidate;
                    nearestDistance = distance;
                }
            }
            return nearest;
        }

        private void Release()
        {
            if (grabbedPoint == null)
                return;
            grabbedPoint.EndGrab(this);
            grabbedPoint = null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.7f, 0.1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, reach);
        }
    }
}
