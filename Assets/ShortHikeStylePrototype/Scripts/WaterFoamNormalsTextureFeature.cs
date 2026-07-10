using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace ShortHikeStylePrototype
{
    public sealed class WaterFoamNormalsTextureFeature : ScriptableRendererFeature
    {
        NormalsRequestPass pass;

        public override void Create()
        {
            pass = new NormalsRequestPass
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPrePasses
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.isPreviewCamera)
                return;

            renderer.EnqueuePass(pass);
        }

        sealed class NormalsRequestPass : ScriptableRenderPass
        {
            public NormalsRequestPass()
            {
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
            }
        }
    }
}
