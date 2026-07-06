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
        [SerializeField] Color outlineColor = new Color(0.16f, 0.27f, 0.30f, 1f);
        [SerializeField, Range(0.01f, 1f)] float edgeThreshold = 0.18f;
        [SerializeField, Range(0.25f, 8f)] float edgeStrength = 3.2f;
        [SerializeField, Range(0f, 1f)] float outlineOpacity = 0.86f;
        [SerializeField, Range(0.5f, 2.5f)] float edgeSampleDistance = 1f;
        [SerializeField] bool compositeBeforeTransparents;
        [SerializeField] Color letterboxColor = new Color(0.55f, 0.78f, 0.88f, 1f);
        [SerializeField] Vector2Int currentRenderTextureSize;

        public int LowResolutionScale => Mathf.Clamp(lowResolutionScale, 1, 12);
        public Shader EdgeCompositeShader => edgeCompositeShader;
        public Color OutlineColor => outlineColor;
        public float EdgeThreshold => edgeThreshold;
        public float EdgeStrength => edgeStrength;
        public float OutlineOpacity => outlineOpacity;
        public float EdgeSampleDistance => edgeSampleDistance;
        public bool CompositeBeforeTransparents => compositeBeforeTransparents;
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
            edgeThreshold = Mathf.Max(0.01f, edgeThreshold);
            edgeStrength = Mathf.Max(0.25f, edgeStrength);
            outlineOpacity = Mathf.Clamp01(outlineOpacity);
            edgeSampleDistance = Mathf.Clamp(edgeSampleDistance, 0.5f, 2.5f);
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
