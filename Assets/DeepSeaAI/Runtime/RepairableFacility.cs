using UnityEngine;
using UnityEngine.Events;

namespace DeepSeaAI
{
    /// <summary>
    /// A reusable, tool-gated repair target. Attach it to any facility collider,
    /// then set the required tool id and the damaged/repaired mesh roots in the Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RepairableFacility : MonoBehaviour
    {
        [Header("Repair Requirements")]
        [SerializeField] private string requiredToolId = "StandardRepairTool";
        [SerializeField, Min(0.1f)] private float repairSeconds = 3f;
        [SerializeField] private bool startRepaired;

        [Header("Mesh Variants")]
        [Tooltip("Meshes or roots shown while this facility is damaged.")]
        [SerializeField] private GameObject[] damagedMeshes;
        [Tooltip("Meshes or roots shown after this facility is repaired.")]
        [SerializeField] private GameObject[] repairedMeshes;

        [Header("Events")]
        [SerializeField] private UnityEvent onRepaired;

        private float repairProgress;
        private bool repaired;

        public string RequiredToolId => requiredToolId;
        public float RepairProgress => repairProgress;
        public bool IsRepaired => repaired;
        public float RepairSeconds => repairSeconds;

        private void Awake()
        {
            repaired = startRepaired;
            repairProgress = repaired ? 1f : 0f;
            ApplyVisuals();
        }

        public void Configure(
            string toolId,
            float seconds,
            GameObject[] damagedVariantMeshes,
            GameObject[] repairedVariantMeshes)
        {
            requiredToolId = string.IsNullOrWhiteSpace(toolId)
                ? "StandardRepairTool"
                : toolId;
            repairSeconds = Mathf.Max(0.1f, seconds);
            damagedMeshes = damagedVariantMeshes;
            repairedMeshes = repairedVariantMeshes;
            ApplyVisuals();
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
            ApplyVisuals();
            if (repairProgress < 1f)
                return false;

            repaired = true;
            ApplyVisuals();
            onRepaired?.Invoke();
            return true;
        }

        public void ResetRepair()
        {
            repaired = false;
            repairProgress = 0f;
            ApplyVisuals();
        }

        private void ApplyVisuals()
        {
            SetActive(damagedMeshes, !repaired);
            SetActive(repairedMeshes, repaired);
        }

        private static void SetActive(GameObject[] meshes, bool active)
        {
            if (meshes == null)
                return;
            foreach (GameObject mesh in meshes)
            {
                if (mesh != null)
                    mesh.SetActive(active);
            }
        }

        private void OnValidate()
        {
            repairSeconds = Mathf.Max(0.1f, repairSeconds);
        }
    }
}
