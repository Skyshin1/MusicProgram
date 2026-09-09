using DeepSeaAI;
using UnityEngine;

namespace DeepSeaDemo
{
    public sealed class DemoOxygenIndicator : MonoBehaviour
    {
        public PlayerOxygen oxygen;
        public Transform fill;
        Vector3 full;
        void Awake() { full = fill.localScale; }
        void LateUpdate()
        {
            if (oxygen != null) fill.localScale = new Vector3(full.x * Mathf.Max(.001f, oxygen.NormalizedOxygen), full.y, full.z);
        }
    }
}
