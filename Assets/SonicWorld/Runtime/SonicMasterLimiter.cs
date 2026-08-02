using UnityEngine;

namespace SonicWorld
{
    /// <summary>
    /// Allocation-free soft limiter on the final listener mix. It prevents several
    /// simultaneous material layers from clipping while retaining their transients.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SonicMasterLimiter : MonoBehaviour
    {
        [SerializeField, Range(0.25f, 1f)] private float threshold = 0.88f;
        [SerializeField, Range(0.5f, 2f)] private float outputGain = 1f;

        private void OnAudioFilterRead(float[] data, int channels)
        {
            float safeThreshold = Mathf.Max(0.01f, threshold);
            for (int i = 0; i < data.Length; i++)
            {
                float normalized = data[i] / safeThreshold;
                float limited = normalized / (1f + Mathf.Abs(normalized));
                data[i] = limited * safeThreshold * outputGain;
            }
        }
    }
}
