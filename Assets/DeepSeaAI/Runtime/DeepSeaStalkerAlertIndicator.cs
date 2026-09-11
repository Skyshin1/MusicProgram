using UnityEngine;

namespace DeepSeaAI
{
    // Keep the serialized type so existing scenes do not acquire missing scripts.
    // The enemy no longer displays an alert symbol or its accompanying light.
    [DisallowMultipleComponent]
    public sealed class DeepSeaStalkerAlertIndicator : MonoBehaviour
    {
        void Awake()
        {
            var legacyVisual = transform.Find("AI Alert Indicator");
            if (legacyVisual != null)
            {
                legacyVisual.gameObject.SetActive(false);
                Destroy(legacyVisual.gameObject);
            }
            enabled = false;
        }
    }
}
