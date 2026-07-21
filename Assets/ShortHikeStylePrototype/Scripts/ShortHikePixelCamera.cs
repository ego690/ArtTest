using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ShortHikeStylePrototype
{
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public sealed class ShortHikePixelCamera : MonoBehaviour
    {
        const string PresentCameraName = "Short Hike Pixel Present Camera";
        const string PresentQuadName = "Short Hike Pixel Present Quad";
        const string OldPresentCanvasName = "Short Hike Pixel Present Canvas";

        [SerializeField, FormerlySerializedAs("pixelScale"), Range(1, 12)] int lowResolutionScale = 3;
        [SerializeField] Shader edgeCompositeShader;
        [SerializeField] bool compositeBeforeTransparents;
        [SerializeField] Color outlineColor = new Color(0.025f, 0.022f, 0.018f, 0.85f);
        [SerializeField, Range(0.001f, 0.5f)] float edgeThreshold = 0.08f;
        [SerializeField, Range(0f, 4f)] float edgeStrength = 1.35f;
        [SerializeField, Range(0f, 1f)] float outlineOpacity = 0.8f;
        [SerializeField, Range(0.5f, 4f)] float edgeSampleDistance = 1f;
        [SerializeField] Color letterboxColor = new Color(0.55f, 0.78f, 0.88f, 1f);
        [SerializeField] Vector2Int currentRenderTextureSize;

        public int LowResolutionScale => Mathf.Clamp(lowResolutionScale, 1, 12);
        public Shader EdgeCompositeShader => edgeCompositeShader;
        public bool CompositeBeforeTransparents => compositeBeforeTransparents;
        public Color OutlineColor => outlineColor;
        public float EdgeThreshold => Mathf.Max(0.001f, edgeThreshold);
        public float EdgeStrength => Mathf.Max(0f, edgeStrength);
        public float OutlineOpacity => Mathf.Clamp01(outlineOpacity);
        public float EdgeSampleDistance => Mathf.Max(0.5f, edgeSampleDistance);
        public Color LetterboxColor => letterboxColor;

        public Vector2Int CurrentRenderTextureSize
        {
            get => currentRenderTextureSize;
            set => currentRenderTextureSize = value;
        }

        void OnEnable()
        {
            ResetCameraOutput();
            QueueCleanupStalePresentationObjects();
        }

        void OnDisable()
        {
            ResetCameraOutput();
        }

        void OnValidate()
        {
            lowResolutionScale = Mathf.Clamp(lowResolutionScale, 1, 12);
            edgeThreshold = Mathf.Max(0.001f, edgeThreshold);
            edgeStrength = Mathf.Max(0f, edgeStrength);
            outlineOpacity = Mathf.Clamp01(outlineOpacity);
            edgeSampleDistance = Mathf.Max(0.5f, edgeSampleDistance);
        }

        void ResetCameraOutput()
        {
            Camera sourceCamera = GetComponent<Camera>();
            sourceCamera.targetTexture = null;
            sourceCamera.allowHDR = false;
            sourceCamera.allowMSAA = false;
        }

        static void QueueCleanupStalePresentationObjects()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorApplication.delayCall -= CleanupStalePresentationObjects;
                EditorApplication.delayCall += CleanupStalePresentationObjects;
                return;
            }
#endif

            CleanupStalePresentationObjects();
        }

        static void CleanupStalePresentationObjects()
        {
            DestroyNamedObject(PresentCameraName);
            DestroyNamedObject(PresentQuadName);
            DestroyNamedObject(OldPresentCanvasName);
        }

        static void DestroyNamedObject(string objectName)
        {
            GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < objects.Length; i++)
            {
                GameObject existing = objects[i];
                if (existing == null || existing.name != objectName)
                    continue;

                if (!Application.isPlaying && !existing.scene.IsValid())
                    continue;

                if (Application.isPlaying)
                    Destroy(existing);
                else
                    DestroyImmediate(existing);
            }
        }
    }
}
