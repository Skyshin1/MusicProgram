using UnityEngine;

namespace SonicWorld
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider), typeof(Renderer))]
    public sealed class SonicCurveControlPoint : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int AlbedoColorId = Shader.PropertyToID("_AlbedoColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private Color idleColor = new Color(0.05f, 0.95f, 1f, 0.82f);
        [SerializeField] private Color hoverColor = new Color(1f, 0.82f, 0.08f, 1f);
        [SerializeField] private Color grabbedColor = new Color(1f, 0.05f, 0.72f, 1f);
        [SerializeField, Range(1f, 2f)] private float hoverScale = 1.35f;
        [SerializeField, Range(1f, 2.5f)] private float grabbedScale = 1.65f;

        private Renderer targetRenderer;
        private Collider targetCollider;
        private MaterialPropertyBlock propertyBlock;
        private SonicPointWave ownerWave;
        private Vector3 baseScale;
        private bool rendererInitiallyEnabled;
        private bool colliderInitiallyEnabled;
        private bool lastRuntimeVisibility = true;
        private object grabOwner;
        private float proximity;
        private float reportedProximity;

        public bool IsGrabbed => grabOwner != null;
        public bool RuntimeEditingAllowed =>
            ownerWave == null || ownerWave.AllowRuntimePointEditing;

        private void Awake()
        {
            targetRenderer = GetComponent<Renderer>();
            targetCollider = GetComponent<Collider>();
            propertyBlock = new MaterialPropertyBlock();
            baseScale = transform.localScale;
            ownerWave = GetComponentInParent<SonicPointWave>();
            rendererInitiallyEnabled = targetRenderer.enabled;
            colliderInitiallyEnabled = targetCollider.enabled;
            ApplyRuntimeVisibility(RuntimeEditingAllowed);
        }

        private void OnTransformParentChanged()
        {
            ownerWave = GetComponentInParent<SonicPointWave>();
        }

        private void LateUpdate()
        {
            bool runtimeVisible = RuntimeEditingAllowed;
            ApplyRuntimeVisibility(runtimeVisible);
            if (!runtimeVisible)
            {
                grabOwner = null;
                proximity = 0f;
                reportedProximity = 0f;
                transform.localScale = baseScale;
                return;
            }

            proximity = Mathf.MoveTowards(
                proximity,
                reportedProximity,
                Time.unscaledDeltaTime * 7f);
            reportedProximity = 0f;

            float pulse = IsGrabbed
                ? 1f + Mathf.Sin(Time.unscaledTime * 7f) * 0.08f
                : 1f;
            float scale = IsGrabbed
                ? grabbedScale * pulse
                : Mathf.Lerp(1f, hoverScale, proximity);
            transform.localScale = baseScale * scale;

            Color color = IsGrabbed
                ? grabbedColor * Mathf.Lerp(0.82f, 1.22f, (pulse - 0.92f) / 0.16f)
                : Color.Lerp(idleColor, hoverColor, proximity);
            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            propertyBlock.SetColor(AlbedoColorId, color);
            propertyBlock.SetColor(EmissionColorId, color * 2.2f);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }

        private void ApplyRuntimeVisibility(bool visible)
        {
            if (targetRenderer == null || targetCollider == null)
                return;
            if (visible == lastRuntimeVisibility &&
                targetRenderer.enabled ==
                (visible && rendererInitiallyEnabled) &&
                targetCollider.enabled ==
                (visible && colliderInitiallyEnabled))
            {
                return;
            }

            targetRenderer.enabled = visible && rendererInitiallyEnabled;
            targetCollider.enabled = visible && colliderInitiallyEnabled;
            lastRuntimeVisibility = visible;
        }

        public void ReportProximity(float amount)
        {
            if (!RuntimeEditingAllowed)
                return;
            reportedProximity = Mathf.Max(reportedProximity, Mathf.Clamp01(amount));
        }

        public bool TryBeginGrab(object owner)
        {
            if (!RuntimeEditingAllowed ||
                owner == null ||
                (grabOwner != null && grabOwner != owner))
                return false;
            grabOwner = owner;
            return true;
        }

        public void EndGrab(object owner)
        {
            if (grabOwner == owner)
                grabOwner = null;
        }
    }
}
