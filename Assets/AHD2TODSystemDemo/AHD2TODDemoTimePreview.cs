using AHD2TimeOfDay;
using UnityEngine;

namespace AHD2TODSystemDemo
{
    [ExecuteAlways]
    public sealed class AHD2TODDemoTimePreview : MonoBehaviour
    {
        [SerializeField] TODController controller;
        [SerializeField, Range(0f, 24f)] float previewTime = 12f;
        [SerializeField] bool flowInPlayMode = true;

        void OnValidate()
        {
            ApplyPreviewTime();
        }

        void Update()
        {
            if (controller == null)
                controller = FindFirstObjectByType<TODController>();

            if (controller == null || controller.todGlobalParameters == null)
                return;

            controller.todGlobalParameters.isTimeFlow = Application.isPlaying && flowInPlayMode;
            controller.isTimeFlow = Application.isPlaying && flowInPlayMode;

            if (!Application.isPlaying)
                ApplyPreviewTime();
        }

        void ApplyPreviewTime()
        {
            if (controller == null)
                controller = FindFirstObjectByType<TODController>();

            if (controller == null || controller.todGlobalParameters == null)
                return;

            controller.todGlobalParameters.CurrentTime = previewTime;
            controller.todGlobalParameters.isTimeFlow = false;
            controller.isTimeFlow = false;
        }
    }
}
