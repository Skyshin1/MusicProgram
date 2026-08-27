using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Quest/XR-safe fullscreen water-on-lens pass. It records only while a
/// WaterExitLensEffect is active, so it has zero render cost during normal play.
/// </summary>
[DisallowMultipleRendererFeature("Water Exit Lens Droplets")]
public sealed class WaterExitDropletsRendererFeature : ScriptableRendererFeature
{
    [SerializeField] private Shader shader;
    [SerializeField] private RenderPassEvent injectionPoint =
        RenderPassEvent.AfterRenderingPostProcessing;

    private Material material;
    private WaterExitDropletsPass pass;

    public override void Create()
    {
        CoreUtils.Destroy(material);
        if (shader == null)
            shader = Shader.Find("Hidden/Sonar/Water Exit Lens Droplets");
        material = shader != null ? CoreUtils.CreateEngineMaterial(shader) : null;
        pass = material != null ? new WaterExitDropletsPass(material) : null;
        if (pass != null)
            pass.renderPassEvent = injectionPoint;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!isActive || pass == null ||
            renderingData.cameraData.cameraType != CameraType.Game)
            return;

        WaterExitLensEffect controller = WaterExitLensEffect.ActiveInstance;
        if (controller == null)
            return;
        WaterExitLensEffect.RenderState state = controller.GetRenderState();
        if (state.Weight <= 0.001f)
            return;

        pass.renderPassEvent = injectionPoint;
        pass.SetState(state);
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(material);
        material = null;
        pass = null;
    }

    private sealed class WaterExitDropletsPass : ScriptableRenderPass
    {
        private static readonly int SceneTextureId = Shader.PropertyToID("_WaterExitSceneTex");
        private static readonly int EffectWeightId = Shader.PropertyToID("_EffectWeight");
        private static readonly int EffectTimeId = Shader.PropertyToID("_EffectTime");
        private static readonly int EdgeWidthId = Shader.PropertyToID("_EdgeWidth");
        private static readonly int DensityId = Shader.PropertyToID("_DropletDensity");
        private static readonly int FallSpeedId = Shader.PropertyToID("_FallSpeed");
        private static readonly int DistortionId = Shader.PropertyToID("_Distortion");

        private readonly Material material;
        private readonly ProfilingSampler sampler = new("Water Exit Lens Droplets");
        private WaterExitLensEffect.RenderState state;

        public WaterExitDropletsPass(Material material)
        {
            this.material = material;
        }

        public void SetState(WaterExitLensEffect.RenderState value)
        {
            state = value;
        }

        private sealed class PassData
        {
            public Material Material;
            public TextureHandle SceneCopy;
            public WaterExitLensEffect.RenderState State;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resources = frameData.Get<UniversalResourceData>();
            TextureHandle cameraColor = resources.activeColorTexture;
            if (!cameraColor.IsValid())
                return;

            TextureDesc copyDesc = renderGraph.GetTextureDesc(cameraColor);
            copyDesc.name = "_WaterExitSceneTex";
            copyDesc.clearBuffer = false;
            TextureHandle sceneCopy = renderGraph.CreateTexture(copyDesc);
            renderGraph.AddCopyPass(cameraColor, sceneCopy, "WaterExitDroplets.Copy");

            using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(
                "Water Exit Lens Droplets", out PassData data, sampler);
            data.Material = material;
            data.SceneCopy = sceneCopy;
            data.State = state;
            builder.SetRenderAttachment(cameraColor, 0, AccessFlags.Write);
            builder.UseTexture(sceneCopy, AccessFlags.Read);
            builder.AllowPassCulling(false);
            builder.SetRenderFunc((PassData passData, RasterGraphContext context) =>
            {
                Material mat = passData.Material;
                mat.SetTexture(SceneTextureId, passData.SceneCopy);
                mat.SetFloat(EffectWeightId, passData.State.Weight);
                mat.SetFloat(EffectTimeId, passData.State.Elapsed);
                mat.SetFloat(EdgeWidthId, passData.State.EdgeWidth);
                mat.SetFloat(DensityId, passData.State.Density);
                mat.SetFloat(FallSpeedId, passData.State.FallSpeed);
                mat.SetFloat(DistortionId, passData.State.Distortion);
                CoreUtils.DrawFullScreen(context.cmd, mat);
            });
        }
    }
}
