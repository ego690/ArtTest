using UnityEngine;
using UnityEngine.Rendering;

namespace SisyphusPrototype
{
    [DisallowMultipleComponent]
    public sealed class SisyphusPipelineScope : MonoBehaviour
    {
        [SerializeField] RenderPipelineAsset scenePipeline;

        RenderPipelineAsset previousPipeline;
        bool changedPipeline;

        public void Configure(RenderPipelineAsset pipeline)
        {
            scenePipeline = pipeline;
        }

        void Awake()
        {
            if (scenePipeline == null)
                return;

            previousPipeline = QualitySettings.renderPipeline;
            if (previousPipeline == scenePipeline)
                return;

            QualitySettings.renderPipeline = scenePipeline;
            changedPipeline = true;
        }

        void OnDestroy()
        {
            if (changedPipeline && QualitySettings.renderPipeline == scenePipeline)
                QualitySettings.renderPipeline = previousPipeline;
        }
    }
}
