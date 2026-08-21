using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

/// <summary>Draws a material-independent white inverted-hull outline for renderers registered by SonarRevealManager.</summary>
[DisallowMultipleRendererFeature("Sonar White Outline")]
public sealed class SonarWhiteOutlineRendererFeature : ScriptableRendererFeature
{
    [SerializeField, Range(0.001f, 0.15f)] private float outlineWidth = 0.025f;
    [SerializeField] private RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;

    private Material material;
    private OutlinePass pass;

    public override void Create()
    {
        Shader shader = Shader.Find("Hidden/Sonar/White Outline");
        CoreUtils.Destroy(material);
        material = shader != null ? CoreUtils.CreateEngineMaterial(shader) : null;
        pass = new OutlinePass(material) { renderPassEvent = injectionPoint };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!isActive || material == null || SonarRevealManager.OutlineStrength <= 0f)
            return;
        CameraType type = renderingData.cameraData.cameraType;
        if (type == CameraType.Preview || type == CameraType.Reflection)
            return;

        pass.renderPassEvent = injectionPoint;
        pass.SetWidth(outlineWidth);
        pass.ConfigureInput(ScriptableRenderPassInput.Depth);
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(material);
        material = null;
    }

    private sealed class OutlinePass : ScriptableRenderPass
    {
        private static readonly int WidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int StrengthId = Shader.PropertyToID("_OutlineStrength");
        private readonly Material material;
        private readonly List<Renderer> renderers = new List<Renderer>();

        public OutlinePass(Material material)
        {
            this.material = material;
            profilingSampler = new ProfilingSampler("Sonar White Outlines");
        }

        public void SetWidth(float width) => material.SetFloat(WidthId, width);

        private bool Gather()
        {
            renderers.Clear();
            foreach (Renderer renderer in SonarRevealManager.ActiveRenderers)
            {
                if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                    renderers.Add(renderer);
            }
            material.SetFloat(StrengthId, SonarRevealManager.OutlineStrength);
            return renderers.Count > 0 && SonarRevealManager.OutlineStrength > 0f;
        }

#if UNITY_6000_0_OR_NEWER
        private sealed class PassData
        {
            public Material Material;
            public List<Renderer> Renderers;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null || !Gather())
                return;

            UniversalResourceData resources = frameData.Get<UniversalResourceData>();
            using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(
                       "Sonar White Outlines", out PassData data, profilingSampler))
            {
                data.Material = material;
                data.Renderers = new List<Renderer>(renderers);
                builder.SetRenderAttachment(resources.cameraColor, 0, AccessFlags.ReadWrite);
                if (resources.cameraDepthTexture.IsValid())
                    builder.SetRenderAttachmentDepth(resources.cameraDepthTexture, AccessFlags.Read);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((PassData passData, RasterGraphContext context) =>
                {
                    Draw(context.cmd, passData.Material, passData.Renderers);
                });
            }
        }
#endif

#pragma warning disable CS0618
        [System.Obsolete("Compatibility path used when the URP RenderGraph is disabled.")]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null || !Gather())
                return;
            CommandBuffer cmd = CommandBufferPool.Get("Sonar White Outlines");
            using (new ProfilingScope(cmd, profilingSampler))
                Draw(cmd, material, renderers);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
#pragma warning restore CS0618

        private static void Draw(CommandBuffer cmd, Material outlineMaterial, List<Renderer> targets)
        {
            foreach (Renderer renderer in targets)
            {
                if (renderer == null)
                    continue;
                int subMeshCount = Mathf.Max(1, renderer.sharedMaterials.Length);
                for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
                    cmd.DrawRenderer(renderer, outlineMaterial, subMesh, 0);
            }
        }

#if UNITY_6000_0_OR_NEWER
        private static void Draw(RasterCommandBuffer cmd, Material outlineMaterial, List<Renderer> targets)
        {
            foreach (Renderer renderer in targets)
            {
                if (renderer == null)
                    continue;
                int subMeshCount = Mathf.Max(1, renderer.sharedMaterials.Length);
                for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
                    cmd.DrawRenderer(renderer, outlineMaterial, subMesh, 0);
            }
        }
#endif
    }
}
