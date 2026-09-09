using Unity.XR.CoreUtils;
using UnityEngine;

namespace DeepSeaDemo
{
    public sealed class DemoPlayerSafety : MonoBehaviour
    {
        public DemoConfig config;
        public float headRadius = .12f;
        readonly Collider[] hits = new Collider[32];
        XROrigin origin;
        float dark;
        void Awake() { origin = GetComponent<XROrigin>(); }
        public bool CanPlaceHead(Vector3 head)
        {
            Vector3 bottom = head - Vector3.up * 1.3f;
            Vector3 top = head - Vector3.up * .2f;
            return !Blocked(bottom, top, .24f);
        }
        public bool CanResize(Vector3 localCenter, float height, float radius)
        {
            Vector3 center = transform.TransformPoint(localCenter);
            float half = Mathf.Max(0, height * .5f - radius);
            return !Blocked(center - Vector3.up * half, center + Vector3.up * half, radius * .92f);
        }
        bool Blocked(Vector3 a, Vector3 b, float r)
        {
            int count = Physics.OverlapCapsuleNonAlloc(a, b, r, hits, config.worldMask, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return true;
            for (int i = 0; i < count; i++)
                if (hits[i] != null && !hits[i].transform.IsChildOf(transform)) return true;
            return false;
        }
        void LateUpdate()
        {
            var flow = DemoFlow.Instance; if (flow == null || flow.Busy || !flow.Running) return;
            bool blocked = Blocked(origin.Camera.transform.position, origin.Camera.transform.position, headRadius);
            dark = Mathf.MoveTowards(dark, blocked ? .97f : 0, Time.unscaledDeltaTime * 4f);
            flow.ui.SetFade(dark);
        }
    }
}
