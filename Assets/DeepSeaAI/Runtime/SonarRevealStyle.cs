using UnityEngine;

namespace DeepSeaAI
{
    [DisallowMultipleComponent]
    public sealed class SonarRevealStyle : MonoBehaviour
    {
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static Material invisibleMaterial;

        [SerializeField] private Color outlineColor = new Color(1f, 0.035f, 0.02f, 1f);
        [SerializeField, Min(0.05f)] private float revealDuration = 1.5f;
        [SerializeField] private bool hideSurfaceOutsideSonar;

        private Renderer[] renderers;
        private Material[][] originalMaterials;
        private MaterialPropertyBlock propertyBlock;

        public Color OutlineColor => outlineColor;
        public float RevealDuration => revealDuration;

        public void Configure(Color color, float duration, bool hideSurface = false)
        {
            outlineColor = color;
            revealDuration = Mathf.Max(0.05f, duration);
            hideSurfaceOutsideSonar = hideSurface;
            if (Application.isPlaying)
                ApplyStyle();
        }

        private void Awake()
        {
            CacheRenderers();
            ApplyStyle();
        }

        private void OnEnable()
        {
            VolumetricFogPulseEmitter.PulseUpdated += OnPulseUpdated;
            if (Application.isPlaying)
                ApplyStyle();
        }

        private void OnDisable()
        {
            VolumetricFogPulseEmitter.PulseUpdated -= OnPulseUpdated;
            if (Application.isPlaying)
                RestoreOriginalMaterials();
        }

        private void CacheRenderers()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            originalMaterials = new Material[renderers.Length][];
            for (int i = 0; i < renderers.Length; i++)
                originalMaterials[i] = renderers[i] != null ? renderers[i].sharedMaterials : null;
            propertyBlock = new MaterialPropertyBlock();
        }

        private void ApplyStyle()
        {
            if (renderers == null || originalMaterials == null || propertyBlock == null)
                CacheRenderers();

            Material hidden = hideSurfaceOutsideSonar ? GetInvisibleMaterial() : null;
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer target = renderers[rendererIndex];
                if (target == null)
                    continue;

                target.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(OutlineColorId, outlineColor);
                target.SetPropertyBlock(propertyBlock);

                if (hidden == null)
                {
                    if (rendererIndex < originalMaterials.Length && originalMaterials[rendererIndex] != null)
                        target.sharedMaterials = originalMaterials[rendererIndex];
                    continue;
                }

                Material[] slots = target.sharedMaterials;
                if (slots == null || slots.Length == 0)
                    slots = new Material[1];
                for (int i = 0; i < slots.Length; i++)
                    slots[i] = hidden;
                target.sharedMaterials = slots;
            }
        }

        private void RestoreOriginalMaterials()
        {
            if (renderers == null || originalMaterials == null)
                return;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && i < originalMaterials.Length && originalMaterials[i] != null)
                    renderers[i].sharedMaterials = originalMaterials[i];
            }
        }

        private void OnPulseUpdated(VolumetricFogPulseEmitter.PulseState pulse)
        {
            if (pulse.Strength <= 0.001f)
                return;
            if (renderers == null || renderers.Length == 0)
                CacheRenderers();

            Collider bodyCollider = GetComponent<Collider>();
            bool hasBounds = bodyCollider != null && bodyCollider.enabled;
            Bounds combinedBounds = hasBounds ? bodyCollider.bounds : default;
            foreach (Renderer target in renderers)
            {
                if (target == null || !target.gameObject.activeInHierarchy)
                    continue;
                if (!hasBounds)
                {
                    combinedBounds = target.bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(target.bounds);
                }
            }

            if (!hasBounds)
                return;

            float shellTolerance = combinedBounds.extents.magnitude + pulse.Width * 0.5f + 0.12f;
            float centerDistance = Vector3.Distance(combinedBounds.center, pulse.Origin);
            if (Mathf.Abs(centerDistance - pulse.Radius) > shellTolerance)
                return;

            foreach (Renderer target in renderers)
                SonarRevealManager.RevealRenderer(target, revealDuration);

        }

        private static Material GetInvisibleMaterial()
        {
            if (invisibleMaterial != null)
                return invisibleMaterial;

            Shader shader = Shader.Find("Hidden/DeepSeaAI/Invisible Surface");
            if (shader == null)
                return null;

            invisibleMaterial = new Material(shader)
            {
                name = "Deep Sea NPC Invisible Surface",
                hideFlags = HideFlags.HideAndDontSave
            };
            return invisibleMaterial;
        }

        private void OnValidate()
        {
            revealDuration = Mathf.Max(0.05f, revealDuration);
        }
    }
}

