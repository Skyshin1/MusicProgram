using System.Text;
using UnityEngine;

namespace DeepSeaAI
{
#if UNITY_EDITOR
    [DefaultExecutionOrder(32000)]
    internal sealed class DeepSeaRuntimeSanityProbe : MonoBehaviour
    {
        private int checksRemaining = 180;
        private bool reported;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var probe = new GameObject("Deep Sea Runtime Sanity Probe");
            probe.hideFlags = HideFlags.HideAndDontSave;
            probe.AddComponent<DeepSeaRuntimeSanityProbe>();
        }

        private void LateUpdate()
        {
            if (checksRemaining-- <= 0)
            {
                Destroy(gameObject);
                return;
            }

            foreach (Transform target in FindObjectsByType<Transform>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (IsFinite(target.position) &&
                    IsFinite(target.localScale) &&
                    IsFinite(target.rotation))
                {
                    continue;
                }

                if (!reported)
                {
                    reported = true;
                    Debug.LogError(
                        "[DeepSeaAI] Non-finite transform detected at " +
                        FullPath(target) + ". Desktop pose fallback repaired it.");
                }

                if (target.GetComponent<Camera>() != null)
                {
                    target.localPosition = Vector3.zero;
                    target.localRotation = Quaternion.identity;
                    target.localScale = Vector3.one;
                    continue;
                }

                Animator animator = target.GetComponentInParent<Animator>();
                if (animator != null)
                    animator.enabled = false;
                target.localPosition = Vector3.zero;
                target.localRotation = Quaternion.identity;
                target.localScale = Vector3.one;
            }

            foreach (Renderer renderer in FindObjectsByType<Renderer>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                Bounds bounds = renderer.bounds;
                if (IsFinite(bounds.center) &&
                    IsFinite(bounds.extents) &&
                    bounds.extents.sqrMagnitude < 100000000f)
                {
                    continue;
                }

                if (!reported)
                {
                    reported = true;
                    Debug.LogError(
                        "[DeepSeaAI] Invalid renderer bounds detected at " +
                        FullPath(renderer.transform) +
                        ". Its animator was disabled for this desktop test.");
                }

                Animator animator = renderer.GetComponentInParent<Animator>();
                if (animator != null)
                    animator.enabled = false;
                renderer.enabled = false;
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z) &&
                   float.IsFinite(value.w);
        }

        private static string FullPath(Transform target)
        {
            var builder = new StringBuilder(target.name);
            while (target.parent != null)
            {
                target = target.parent;
                builder.Insert(0, target.name + "/");
            }
            return builder.ToString();
        }
    }
#endif
}
