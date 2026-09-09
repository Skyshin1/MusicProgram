using UnityEngine;
using UnityEngine.Rendering;

namespace DeepSeaDemo
{
    [DefaultExecutionOrder(-20000)]
    public sealed class DemoPipeline : MonoBehaviour
    {
        public RenderPipelineAsset pipeline;
        RenderPipelineAsset previousDefault, previousQuality;
        void Awake()
        {
            previousDefault = GraphicsSettings.defaultRenderPipeline;
            previousQuality = QualitySettings.renderPipeline;
            if (pipeline != null) { GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline; }
        }
        void OnDestroy()
        {
            if (GraphicsSettings.defaultRenderPipeline == pipeline) GraphicsSettings.defaultRenderPipeline = previousDefault;
            if (QualitySettings.renderPipeline == pipeline) QualitySettings.renderPipeline = previousQuality;
        }
    }
}
