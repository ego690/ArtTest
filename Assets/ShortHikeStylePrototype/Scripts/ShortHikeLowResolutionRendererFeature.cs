using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace ShortHikeStylePrototype
{
    public sealed class ShortHikeLowResolutionRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] Shader edgeCompositeShader;
        [SerializeField, Range(1, 12)] int fallbackLowResolutionScale = 3;
        [SerializeField] bool affectSceneView;

        LowResolutionPass pass;

        public override void Create()
        {
            pass = new LowResolutionPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            if (camera == null || renderingData.cameraData.isPreviewCamera)
                return;

            if (!affectSceneView && renderingData.cameraData.isSceneViewCamera)
                return;

            ShortHikePixelCamera controller = camera.GetComponent<ShortHikePixelCamera>();
            if (controller == null || !controller.isActiveAndEnabled)
                return;

            Shader shader = controller.EdgeCompositeShader != null ? controller.EdgeCompositeShader : edgeCompositeShader;
            if (shader == null)
                shader = Shader.Find("Hidden/ShortHikeStylePrototype/LowResEdgeComposite");

            int scale = Mathf.Max(1, controller.LowResolutionScale > 0 ? controller.LowResolutionScale : fallbackLowResolutionScale);
            pass.Setup(controller, shader, scale);
            pass.renderPassEvent = controller.CompositeBeforeTransparents
                ? RenderPassEvent.BeforeRenderingTransparents
                : RenderPassEvent.AfterRenderingPostProcessing;
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            pass?.Dispose();
        }

        sealed class LowResolutionPass : ScriptableRenderPass
        {
            static readonly Vector2 FullScale = Vector2.one;
            static readonly Vector2 NoOffset = Vector2.zero;

            ShortHikePixelCamera controller;
            Material edgeMaterial;
            Shader edgeShader;
            int scale = 3;

            public void Setup(ShortHikePixelCamera pixelCamera, Shader shader, int lowResolutionScale)
            {
                controller = pixelCamera;
                edgeShader = shader;
                scale = Mathf.Clamp(lowResolutionScale, 1, 12);
                requiresIntermediateTexture = true;
            }

            public void Dispose()
            {
                CoreUtils.Destroy(edgeMaterial);
                edgeMaterial = null;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer || controller == null)
                    return;

                TextureHandle source = resourceData.activeColorTexture;
                TextureDesc sourceDesc = renderGraph.GetTextureDesc(source);
                int lowWidth = Mathf.Max(16, Mathf.CeilToInt(sourceDesc.width / (float)scale));
                int lowHeight = Mathf.Max(16, Mathf.CeilToInt(sourceDesc.height / (float)scale));

                controller.CurrentRenderTextureSize = new Vector2Int(lowWidth, lowHeight);

                TextureDesc lowDesc = new TextureDesc(sourceDesc)
                {
                    name = "Short Hike Low Resolution Color",
                    width = lowWidth,
                    height = lowHeight,
                    depthBufferBits = DepthBits.None,
                    msaaSamples = MSAASamples.None,
                    bindTextureMS = false,
                    useDynamicScale = false,
                    useDynamicScaleExplicit = false,
                    useMipMap = false,
                    autoGenerateMips = false,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    clearBuffer = false
                };

                TextureDesc compositeDesc = new TextureDesc(lowDesc)
                {
                    name = "Short Hike Low Resolution Composite"
                };

                TextureDesc outputDesc = new TextureDesc(sourceDesc)
                {
                    name = "Short Hike Low Resolution Upscaled Color",
                    depthBufferBits = DepthBits.None,
                    msaaSamples = MSAASamples.None,
                    bindTextureMS = false,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    clearBuffer = false
                };

                TextureHandle lowColor = renderGraph.CreateTexture(lowDesc);
                TextureHandle composite = renderGraph.CreateTexture(compositeDesc);
                TextureHandle output = renderGraph.CreateTexture(outputDesc);

                renderGraph.AddBlitPass(
                    source,
                    lowColor,
                    FullScale,
                    NoOffset,
                    filterMode: RenderGraphUtils.BlitFilterMode.ClampNearest,
                    passName: "Short Hike Downsample To Low Resolution");

                Material material = EnsureEdgeMaterial();
                if (material != null)
                {
                    var edgeParameters = new RenderGraphUtils.BlitMaterialParameters(lowColor, composite, material, 0);
                    renderGraph.AddBlitPass(edgeParameters, "Short Hike Low Resolution Composite");
                }
                else
                {
                    renderGraph.AddBlitPass(
                        lowColor,
                        composite,
                        FullScale,
                        NoOffset,
                        filterMode: RenderGraphUtils.BlitFilterMode.ClampNearest,
                        passName: "Short Hike Low Resolution Copy");
                }

                renderGraph.AddBlitPass(
                    composite,
                    output,
                    FullScale,
                    NoOffset,
                    filterMode: RenderGraphUtils.BlitFilterMode.ClampNearest,
                    passName: "Short Hike Upscale Low Resolution");

                resourceData.cameraColor = output;
            }

            Material EnsureEdgeMaterial()
            {
                if (edgeMaterial != null && edgeMaterial.shader == edgeShader)
                    return edgeMaterial;

                CoreUtils.Destroy(edgeMaterial);
                edgeMaterial = null;

                if (edgeShader == null)
                    return null;

                edgeMaterial = CoreUtils.CreateEngineMaterial(edgeShader);
                return edgeMaterial;
            }
        }
    }
}
