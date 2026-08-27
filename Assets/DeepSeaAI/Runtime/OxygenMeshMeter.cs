using UnityEngine;

namespace DeepSeaAI
{
    /// <summary>
    /// A 3D oxygen bar that changes only the scale of a fill mesh. Parent the
    /// meter root under a glove later; this component keeps its source reference.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OxygenMeshMeter : MonoBehaviour
    {
        public enum ScaleAxis { X, Y, Z }

        [Header("References")]
        [SerializeField] private PlayerOxygen oxygenSource;
        [SerializeField] private Transform fillMesh;

        [Header("Mesh Scaling")]
        [SerializeField] private ScaleAxis scaleAxis = ScaleAxis.X;
        [SerializeField, Range(0f, 0.2f)] private float emptyScaleFraction = 0.015f;
        [SerializeField] private bool keepOneEdgeAnchored = true;
        [SerializeField] private bool growTowardPositiveAxis = true;
        [SerializeField, Min(0f)] private float smoothingSpeed = 10f;

        private Vector3 fullScale;
        private Vector3 fullPosition;
        private float displayedAmount = 1f;
        private bool initialized;

        public void Configure(PlayerOxygen source, Transform targetMesh)
        {
            oxygenSource = source;
            fillMesh = targetMesh;
            CaptureFullMeshTransform();
            RefreshImmediately();
        }

        private void Awake()
        {
            ResolveSource();
            CaptureFullMeshTransform();
        }

        private void OnEnable()
        {
            ResolveSource();
            if (oxygenSource != null)
                oxygenSource.OxygenChanged += OnOxygenChanged;
        }

        private void OnDisable()
        {
            if (oxygenSource != null)
                oxygenSource.OxygenChanged -= OnOxygenChanged;
        }

        private void Update()
        {
            if (!initialized || fillMesh == null)
                return;
            if (oxygenSource == null)
                ResolveSource();
            if (oxygenSource == null)
                return;

            float target = oxygenSource.NormalizedOxygen;
            displayedAmount = smoothingSpeed <= 0f
                ? target
                : Mathf.MoveTowards(displayedAmount, target, smoothingSpeed * Time.deltaTime);
            Apply(displayedAmount);
        }

        public void RefreshImmediately()
        {
            if (!initialized || fillMesh == null)
                return;
            ResolveSource();
            displayedAmount = oxygenSource != null ? oxygenSource.NormalizedOxygen : 1f;
            Apply(displayedAmount);
        }

        private void OnOxygenChanged(float normalized)
        {
            if (smoothingSpeed <= 0f)
            {
                displayedAmount = normalized;
                Apply(displayedAmount);
            }
        }

        private void ResolveSource()
        {
            if (oxygenSource != null)
                return;
            oxygenSource = GetComponentInParent<PlayerOxygen>();
            if (oxygenSource == null)
                oxygenSource = FindFirstObjectByType<PlayerOxygen>();
        }

        private void CaptureFullMeshTransform()
        {
            if (fillMesh == null)
                return;
            fullScale = fillMesh.localScale;
            fullPosition = fillMesh.localPosition;
            initialized = true;
        }

        private void Apply(float normalized)
        {
            if (fillMesh == null)
                return;

            float fraction = Mathf.Lerp(emptyScaleFraction, 1f, Mathf.Clamp01(normalized));
            Vector3 scale = fullScale;
            Vector3 position = fullPosition;
            float fullAxisScale = GetAxis(fullScale);
            SetAxis(ref scale, fullAxisScale * fraction);

            if (keepOneEdgeAnchored)
            {
                float offset = fullAxisScale * (1f - fraction) * 0.5f;
                SetAxis(ref position, GetAxis(fullPosition) + (growTowardPositiveAxis ? offset : -offset));
            }

            fillMesh.localScale = scale;
            fillMesh.localPosition = position;
        }

        private float GetAxis(Vector3 value)
        {
            return scaleAxis switch
            {
                ScaleAxis.Y => value.y,
                ScaleAxis.Z => value.z,
                _ => value.x
            };
        }

        private void SetAxis(ref Vector3 value, float axisValue)
        {
            switch (scaleAxis)
            {
                case ScaleAxis.Y: value.y = axisValue; break;
                case ScaleAxis.Z: value.z = axisValue; break;
                default: value.x = axisValue; break;
            }
        }

        private void OnValidate()
        {
            emptyScaleFraction = Mathf.Clamp(emptyScaleFraction, 0f, 0.2f);
            smoothingSpeed = Mathf.Max(0f, smoothingSpeed);
        }
    }
}
