using UnityEngine;

namespace SonicWorld
{
    [DisallowMultipleComponent]
    public sealed class SonicMKToonTarget : MonoBehaviour
    {
        [SerializeField] private Renderer targetRenderer;
        [SerializeField, Range(0f, 2f)] private float emission = 1f;
        [SerializeField, Range(0f, 2f)] private float outline = 1f;
        [SerializeField, Range(0f, 2f)] private float rim = 1f;
        [SerializeField, Range(0f, 2f)] private float iridescence = 1f;
        [SerializeField, Range(0f, 2f)] private float vertexAnimation = 0.5f;

        public Renderer TargetRenderer =>
            targetRenderer != null ? targetRenderer : GetComponent<Renderer>();
        public float Emission => emission;
        public float Outline => outline;
        public float Rim => rim;
        public float Iridescence => iridescence;
        public float VertexAnimation => vertexAnimation;

        public void Configure(Renderer renderer, SonicSurfaceProfile profile)
        {
            targetRenderer = renderer;
            if (profile == null)
            {
                emission = 1f;
                outline = 1f;
                rim = 1f;
                iridescence = 1f;
                vertexAnimation = 0.5f;
                return;
            }

            emission = profile.emissionResponse;
            outline = profile.outlineResponse;
            rim = profile.rimResponse;
            iridescence = profile.iridescenceResponse;
            vertexAnimation = profile.vertexResponse;
        }
    }
}
