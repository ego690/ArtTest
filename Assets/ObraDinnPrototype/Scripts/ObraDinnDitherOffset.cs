using UnityEngine;

namespace ObraDinnPrototype
{
    [ExecuteAlways]
    public sealed class ObraDinnDitherOffset : MonoBehaviour
    {
        [SerializeField, Tooltip("要写入相机旋转偏移的 Obra Dinn 后处理材质，通常是 M_ObraDinnPost。")] Material targetMaterial;
        [SerializeField, Tooltip("旋转补偿强度。0 表示不补偿；1 表示按相机旋转量移动抖动采样，减轻图案糊在屏幕上的感觉；更高会更明显。")] float strength = 1f;

        Camera targetCamera;
        Quaternion referenceRotation;
        bool hasReference;

        static readonly int RotationOffsetId = Shader.PropertyToID("_RotationOffset");
        static readonly int OffsetStrengthId = Shader.PropertyToID("_OffsetStrength");

        void OnEnable()
        {
            targetCamera = GetComponent<Camera>();
            ResetReference();
        }

        void LateUpdate()
        {
            if (targetMaterial == null)
                return;

            if (targetCamera == null)
                targetCamera = GetComponent<Camera>();

            if (!hasReference || targetCamera == null)
                ResetReference();

            Vector2 offset = CalculateOffsetPixels();
            targetMaterial.SetVector(RotationOffsetId, new Vector4(offset.x, offset.y, 0f, 0f));
            targetMaterial.SetFloat(OffsetStrengthId, strength);
        }

        [ContextMenu("Reset Dither Reference")]
        public void ResetReference()
        {
            if (targetCamera == null)
                targetCamera = GetComponent<Camera>();

            referenceRotation = targetCamera != null ? targetCamera.transform.rotation : transform.rotation;
            hasReference = true;
        }

        Vector2 CalculateOffsetPixels()
        {
            if (targetCamera == null || targetMaterial == null)
                return Vector2.zero;

            Quaternion delta = Quaternion.Inverse(referenceRotation) * targetCamera.transform.rotation;
            Vector3 euler = delta.eulerAngles;
            float yaw = Mathf.DeltaAngle(0f, euler.y);
            float pitch = Mathf.DeltaAngle(0f, euler.x);

            float height = Mathf.Max(1, targetCamera.pixelHeight);
            float width = Mathf.Max(1, targetCamera.pixelWidth);
            float verticalFov = Mathf.Max(1f, targetCamera.fieldOfView);
            float horizontalFov = Camera.VerticalToHorizontalFieldOfView(verticalFov, targetCamera.aspect);

            return new Vector2(width * yaw / horizontalFov, -height * pitch / verticalFov);
        }
    }
}
