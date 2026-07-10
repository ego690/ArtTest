using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RoystanOutlineDemo
{
    [ExecuteAlways]
    [RequireComponent(typeof(UniversalAdditionalCameraData))]
    public sealed class RoystanSmaaToggle : MonoBehaviour
    {
        [SerializeField] bool smaaEnabled = true;

        UniversalAdditionalCameraData cameraData;

        public bool SmaaEnabled
        {
            get => smaaEnabled;
            set
            {
                if (smaaEnabled == value && cameraData != null)
                    return;

                smaaEnabled = value;
                Apply();
            }
        }

        void OnEnable()
        {
            cameraData = GetComponent<UniversalAdditionalCameraData>();
            Apply();
        }

        void OnValidate()
        {
            cameraData = GetComponent<UniversalAdditionalCameraData>();
            Apply();
        }

        void Apply()
        {
            if (cameraData == null)
                return;

            cameraData.antialiasing = smaaEnabled
                ? AntialiasingMode.SubpixelMorphologicalAntiAliasing
                : AntialiasingMode.None;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
        }
    }
}
