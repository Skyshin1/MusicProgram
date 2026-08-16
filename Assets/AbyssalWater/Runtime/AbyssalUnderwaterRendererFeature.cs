using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace MusicProgram.AbyssalWater
{
    [Tooltip("Beer-Lambert underwater absorption, wave waterline and physical caustics.")]
    [DisallowMultipleRendererFeature("Abyssal Underwater")]
    public sealed class AbyssalUnderwaterRendererFeature : ScriptableRendererFeature
    {
        [SerializeField, HideInInspector] Shader underwaterShader;
        [SerializeField] RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingTransparents;

        Material _material;
        UnderwaterPass _pass;

        public override void Create()
        {
            underwaterShader = underwaterShader != null
                ? underwaterShader
                : Shader.Find("Hidden/MusicProgram/Abyssal Water/Underwater");
            CoreUtils.Destroy(_material);
            if (underwaterShader != null) _material = CoreUtils.CreateEngineMaterial(underwaterShader);
            _pass = new UnderwaterPass(_material)
            {
                renderPassEvent = injectionPoint
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var water = AbyssalWaterSystem.Active;
            if (!isActive || _material == null || water == null || water.profile == null) return;
            var cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Reflection || cameraType == CameraType.Preview) return;
            if (cameraType == CameraType.SceneView && !water.renderInSceneView) return;
            _pass.renderPassEvent = injectionPoint;
            _pass.ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
            CoreUtils.Destroy(_material);
            _material = null;
        }

        sealed class UnderwaterPass : ScriptableRenderPass
        {
            sealed class PassData
            {
                public TextureHandle source;
                public Material material;
            }

            readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Abyssal Underwater");
            readonly Material _material;
            RTHandle _temporary;

            public UnderwaterPass(Material material) => _material = material;

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_material == null) return;
                var resourceData = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                var descriptor = cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0;
                descriptor.msaaSamples = 1;
                var target = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor,
                    "Abyssal Underwater Composite", false);

                using (var builder = renderGraph.AddRasterRenderPass("Abyssal Underwater Composite",
                           out PassData passData, _profilingSampler))
                {
                    passData.source = resourceData.cameraColor;
                    passData.material = _material;
                    builder.UseTexture(resourceData.cameraColor, AccessFlags.Read);
                    if (resourceData.cameraDepthTexture.IsValid())
                        builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(target, 0, AccessFlags.WriteAll);
                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        Blitter.BlitTexture(context.cmd, data.source, Vector2.one, data.material, 0);
                    });
                }

                resourceData.cameraColor = target;
            }

#pragma warning disable CS0618
            [System.Obsolete("Compatibility path used when the URP RenderGraph is disabled.")]
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var descriptor = renderingData.cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0;
                descriptor.msaaSamples = 1;
                RenderingUtils.ReAllocateHandleIfNeeded(ref _temporary, descriptor,
                    FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_AbyssalUnderwaterTemporary");
            }

            [System.Obsolete("Compatibility path used when the URP RenderGraph is disabled.")]
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_material == null || _temporary == null) return;
                var source = renderingData.cameraData.renderer.cameraColorTargetHandle;
                var cmd = CommandBufferPool.Get("Abyssal Underwater");
                using (new ProfilingScope(cmd, _profilingSampler))
                {
                    Blitter.BlitCameraTexture(cmd, source, _temporary, _material, 0);
                    Blitter.BlitCameraTexture(cmd, _temporary, source);
                }
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
#pragma warning restore CS0618

            public void Dispose()
            {
                _temporary?.Release();
                _temporary = null;
            }
        }
    }
}
