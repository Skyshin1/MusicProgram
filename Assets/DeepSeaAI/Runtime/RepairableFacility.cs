using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;

namespace DeepSeaAI
{
    /// <summary>
    /// A reusable, tool-gated repair target. Repairing fades only the assigned
    /// URP damage Decal Projectors; it never swaps, enables, disables, or moves
    /// the facility's mesh objects.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RepairableFacility : MonoBehaviour
    {
        [Header("Repair Requirements")]
        [SerializeField] private string requiredToolId = "StandardRepairTool";
        [SerializeField, Min(0.1f)] private float repairSeconds = 3f;
        [SerializeField] private bool startRepaired;

        [Header("Damage Decals")]
        [Tooltip("Only these URP Decal Projectors fade during repair. Leave empty and enable Auto Find to use all child decals.")]
        [SerializeField] private DecalProjector[] damageDecals;
        [SerializeField]
        [Tooltip("Convenient for a single prop: all Decal Projectors below this object are treated as repairable damage decals.")]
        private bool autoFindChildDecals = true;
        [SerializeField]
        [Tooltip("Disables each damage decal after it has faded completely. Turn this off only when another system needs the projector to stay active.")]
        private bool disableDecalsWhenRepaired = true;

        [Header("Events")]
        [SerializeField] private UnityEvent onRepaired;

        private float repairProgress;
        private bool repaired;
        private float[] originalFadeFactors;

        public string RequiredToolId => requiredToolId;
        public float RepairProgress => repairProgress;
        public bool IsRepaired => repaired;
        public float RepairSeconds => repairSeconds;

        private void Awake()
        {
            CacheDamageDecals();
            repaired = startRepaired;
            repairProgress = repaired ? 1f : 0f;
            ApplyDecalFade();
        }

        /// <summary>Configures this repair target with explicit damage decals.</summary>
        public void Configure(string toolId, float seconds, DecalProjector[] decals)
        {
            requiredToolId = string.IsNullOrWhiteSpace(toolId)
                ? "StandardRepairTool"
                : toolId;
            repairSeconds = Mathf.Max(0.1f, seconds);
            damageDecals = decals;
            autoFindChildDecals = false;
            originalFadeFactors = null;
            CacheDamageDecals();
            ApplyDecalFade();
        }

        /// <summary>
        /// Compatibility overload for older setup scripts. Mesh arguments are
        /// intentionally ignored: repair visuals are decal-only from now on.
        /// </summary>
        [Obsolete("Repair visuals are decal-only. Use Configure(toolId, seconds, DecalProjector[]) instead.")]
        public void Configure(
            string toolId,
            float seconds,
            GameObject[] unusedDamagedMeshes,
            GameObject[] unusedRepairedMeshes)
        {
            Configure(toolId, seconds, GetComponentsInChildren<DecalProjector>(true));
        }

        /// <returns>True when this tool is allowed to repair this facility.</returns>
        public bool AcceptsTool(string toolId)
        {
            return !repaired && !string.IsNullOrEmpty(toolId) && toolId == requiredToolId;
        }

        /// <returns>True only on the frame the facility becomes fully repaired.</returns>
        public bool Repair(string toolId, float deltaTime)
        {
            if (!AcceptsTool(toolId) || deltaTime <= 0f)
                return false;

            repairProgress = Mathf.Clamp01(repairProgress + deltaTime / repairSeconds);
            ApplyDecalFade();
            if (repairProgress < 1f)
                return false;

            repaired = true;
            ApplyDecalFade();
            onRepaired?.Invoke();
            return true;
        }

        public void ResetRepair()
        {
            repaired = false;
            repairProgress = 0f;
            ApplyDecalFade();
        }

        private void CacheDamageDecals()
        {
            if ((damageDecals == null || damageDecals.Length == 0) && autoFindChildDecals)
                damageDecals = GetComponentsInChildren<DecalProjector>(true);

            int count = damageDecals != null ? damageDecals.Length : 0;
            if (originalFadeFactors != null && originalFadeFactors.Length == count)
                return;

            originalFadeFactors = new float[count];
            for (int i = 0; i < count; i++)
            {
                DecalProjector decal = damageDecals[i];
                originalFadeFactors[i] = decal != null ? Mathf.Clamp01(decal.fadeFactor) : 0f;
            }
        }

        private void ApplyDecalFade()
        {
            CacheDamageDecals();
            if (damageDecals == null)
                return;

            float multiplier = repaired ? 0f : 1f - repairProgress;
            for (int i = 0; i < damageDecals.Length; i++)
            {
                DecalProjector decal = damageDecals[i];
                if (decal == null)
                    continue;

                if (repaired && disableDecalsWhenRepaired)
                {
                    decal.gameObject.SetActive(false);
                    continue;
                }

                if (!decal.gameObject.activeSelf)
                    decal.gameObject.SetActive(true);

                float original = i < originalFadeFactors.Length ? originalFadeFactors[i] : 1f;
                decal.fadeFactor = original * multiplier;
            }
        }

        private void OnValidate()
        {
            repairSeconds = Mathf.Max(0.1f, repairSeconds);
        }
    }
}
