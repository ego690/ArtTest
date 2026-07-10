using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace RoystanOutlineDemo
{
    public sealed class RoystanOutlineMaskRendererFeature : ScriptableRendererFeature
    {
        const string MaskTextureName = "_RoystanOutlineMaskTexture";
        const int UserLayerMaskBits = unchecked((int)0xFFFFFF00);

        [SerializeField] Shader maskShader;
        [SerializeField] int outlineLayerMaskBits = UserLayerMaskBits;

        OutlineMaskPass pass;
        Material maskMaterial;

        public override void Create()
        {
            pass = new OutlineMaskPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.isPreviewCamera)
                return;

            Material material = EnsureMaskMaterial();
            if (material == null)
                return;

            pass.Setup(material, outlineLayerMaskBits);
            pass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(maskMaterial);
            maskMaterial = null;
            pass?.Dispose();
        }

        Material EnsureMaskMaterial()
        {
            if (maskShader == null)
                maskShader = Shader.Find("Hidden/RoystanOutlineDemo/OutlineMask");

            if (maskShader == null)
                return null;

            if (maskMaterial != null && maskMaterial.shader == maskShader)
                return maskMaterial;

            CoreUtils.Destroy(maskMaterial);
            maskMaterial = CoreUtils.CreateEngineMaterial(maskShader);
            return maskMaterial;
        }

        sealed class OutlineMaskPass : ScriptableRenderPass
        {
            static readonly int MaskTextureId = Shader.PropertyToID(MaskTextureName);
            static readonly List<ShaderTagId> ShaderTagIds = new List<ShaderTagId>
            {
                new ShaderTagId("UniversalForward"),
                new ShaderTagId("UniversalForwardOnly"),
                new ShaderTagId("SRPDefaultUnlit"),
                new ShaderTagId("DepthNormals"),
                new ShaderTagId("DepthOnly")
            };

            readonly ProfilingSampler profilingSampler = new ProfilingSampler("Roystan Outline Mask");
            RTHandle maskTexture;
            Material maskMaterial;
            FilteringSettings filteringSettings;
            RenderTextureDescriptor descriptor;

            class PassData
            {
                public RendererListHandle rendererList;
            }

            public void Setup(Material material, int layerMaskBits)
            {
                maskMaterial = material;
                filteringSettings = new FilteringSettings(RenderQueueRange.all, layerMaskBits);
            }

#pragma warning disable 618, 672
            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                descriptor = cameraTextureDescriptor;
                descriptor.colorFormat = RenderTextureFormat.R8;
                descriptor.depthBufferBits = 0;
                descriptor.msaaSamples = 1;

                RenderingUtils.ReAllocateHandleIfNeeded(
                    ref maskTexture,
                    descriptor,
                    FilterMode.Point,
                    TextureWrapMode.Clamp,
                    name: MaskTextureName);

                ConfigureTarget(maskTexture);
                ConfigureClear(ClearFlag.Color, Color.black);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (maskMaterial == null)
                    return;

                Camera camera = renderingData.cameraData.camera;
                if (camera == null || renderingData.cameraData.isPreviewCamera)
                    return;

                SortingCriteria sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
                DrawingSettings drawSettings = RenderingUtils.CreateDrawingSettings(ShaderTagIds, ref renderingData, sortFlags);
                drawSettings.overrideMaterial = maskMaterial;
                drawSettings.overrideMaterialPassIndex = 0;
                drawSettings.perObjectData = PerObjectData.None;

                RendererListParams rendererListParams = new RendererListParams(renderingData.cullResults, drawSettings, filteringSettings);
                rendererListParams.filteringSettings.batchLayerMask = uint.MaxValue;
                RendererList rendererList = context.CreateRendererList(ref rendererListParams);

                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    cmd.ClearRenderTarget(false, true, Color.black);
                    cmd.DrawRendererList(rendererList);
                    cmd.SetGlobalTexture(MaskTextureId, maskTexture);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }
#pragma warning restore 618, 672

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();

                if (maskMaterial == null || cameraData.isPreviewCamera)
                    return;

                descriptor = cameraData.cameraTargetDescriptor;
                descriptor.colorFormat = RenderTextureFormat.R8;
                descriptor.depthBufferBits = 0;
                descriptor.msaaSamples = 1;

                RenderingUtils.ReAllocateHandleIfNeeded(
                    ref maskTexture,
                    descriptor,
                    FilterMode.Point,
                    TextureWrapMode.Clamp,
                    name: MaskTextureName);

                TextureHandle destination = renderGraph.ImportTexture(maskTexture);
                if (!destination.IsValid())
                    return;

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Roystan Outline Mask", out PassData passData, profilingSampler))
                {
                    SortingCriteria sortFlags = cameraData.defaultOpaqueSortFlags;
                    DrawingSettings drawSettings = RenderingUtils.CreateDrawingSettings(ShaderTagIds, renderingData, cameraData, lightData, sortFlags);
                    drawSettings.overrideMaterial = maskMaterial;
                    drawSettings.overrideMaterialPassIndex = 0;
                    drawSettings.perObjectData = PerObjectData.None;

                    RendererListParams rendererListParams = new RendererListParams(renderingData.cullResults, drawSettings, filteringSettings);
                    rendererListParams.filteringSettings.batchLayerMask = uint.MaxValue;
                    passData.rendererList = renderGraph.CreateRendererList(rendererListParams);

                    builder.UseRendererList(passData.rendererList);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                    builder.AllowPassCulling(false);
                    builder.SetGlobalTextureAfterPass(destination, MaskTextureId);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        context.cmd.ClearRenderTarget(false, true, Color.black);
                        context.cmd.DrawRendererList(data.rendererList);
                    });
                }
            }

            public void Dispose()
            {
                maskTexture?.Release();
            }
        }
    }
}
