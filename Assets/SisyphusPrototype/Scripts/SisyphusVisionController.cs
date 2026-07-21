using UnityEngine;
using UnityEngine.InputSystem;

namespace SisyphusPrototype
{
    [DisallowMultipleComponent]
    public sealed class SisyphusVisionController : MonoBehaviour
    {
        public enum VisionMode
        {
            Disabled = 0,
            DistanceFade = 1,
            LocalVisibility = 2,
            BoulderOnly = 3,
            FullBlack = 4
        }

        static readonly int VisionModeId = Shader.PropertyToID("_SisyphusVisionMode");
        static readonly int DarkColorId = Shader.PropertyToID("_SisyphusDarkColor");
        static readonly int DistanceStartId = Shader.PropertyToID("_SisyphusDistanceStart");
        static readonly int DistanceEndId = Shader.PropertyToID("_SisyphusDistanceEnd");
        static readonly int VisibilityRadiiId = Shader.PropertyToID("_SisyphusVisibilityRadii");
        static readonly int VisibilityFeatherId = Shader.PropertyToID("_SisyphusVisibilityFeather");
        static readonly int BoulderVisibilityRadiusId = Shader.PropertyToID("_SisyphusBoulderVisibilityRadius");
        static readonly int BoulderVisibilityFeatherId = Shader.PropertyToID("_SisyphusBoulderVisibilityFeather");
        static readonly int WorldToVisibilityId = Shader.PropertyToID("_SisyphusWorldToVisibility");

        [Header("Full Screen Pass")]
        [SerializeField] Material fullScreenMaterial;
        [SerializeField] bool writeShaderGlobals = true;

        [Header("State")]
        [SerializeField] VisionMode mode = VisionMode.Disabled;
        [SerializeField] bool enableNumberKeyShortcuts = true;
        [SerializeField] Color darkColor = Color.black;

        [Header("Round 2: Distance Fade")]
        [SerializeField, Min(0f)] float distanceFadeStart = 18f;
        [SerializeField, Min(0.01f)] float distanceFadeEnd = 42f;

        [Header("Round 3: Local Visibility")]
        [SerializeField, Tooltip("Position and rotation define the visibility volume. Scale is ignored.")]
        Transform visibilityAnchor;
        [SerializeField] Vector3 localVisibilityRadii = new Vector3(7f, 5f, 12f);
        [SerializeField, Range(0.001f, 1f)] float localVisibilityFeather = 0.2f;

        [Header("Round 4: Boulder Only")]
        [SerializeField, Min(0.01f)] float boulderVisibilityRadius = 3.02f;
        [SerializeField, Range(0.01f, 1f)] float boulderVisibilityFeather = 0.28f;

        public VisionMode Mode => mode;
        public Material FullScreenMaterial => fullScreenMaterial;

        public void Configure(Material material, Transform anchor, bool allowNumberKeyShortcuts = false)
        {
            fullScreenMaterial = material;
            visibilityAnchor = anchor;
            enableNumberKeyShortcuts = allowNumberKeyShortcuts;
            ApplyParameters();
        }

        void OnEnable()
        {
            ApplyParameters();
        }

        void Update()
        {
            if (enableNumberKeyShortcuts)
                ReadNumberKeys();

            // The anchor can move with the stone, so its matrix is refreshed every frame.
            ApplyParameters();
        }

        void OnValidate()
        {
            distanceFadeStart = Mathf.Max(0f, distanceFadeStart);
            distanceFadeEnd = Mathf.Max(distanceFadeStart + 0.01f, distanceFadeEnd);
            localVisibilityRadii = new Vector3(
                Mathf.Max(0.001f, Mathf.Abs(localVisibilityRadii.x)),
                Mathf.Max(0.001f, Mathf.Abs(localVisibilityRadii.y)),
                Mathf.Max(0.001f, Mathf.Abs(localVisibilityRadii.z)));
            localVisibilityFeather = Mathf.Max(0.001f, localVisibilityFeather);
            boulderVisibilityRadius = Mathf.Max(0.01f, boulderVisibilityRadius);
            boulderVisibilityFeather = Mathf.Max(0.01f, boulderVisibilityFeather);

            if (isActiveAndEnabled)
                ApplyParameters();
        }

        public void SetMode(VisionMode newMode)
        {
            mode = newMode;
            ApplyParameters();
        }

        public void SetStage(int stage)
        {
            mode = (VisionMode)Mathf.Clamp(stage - 1, 0, 4);
            ApplyParameters();
        }

        public void SetRound(int round) => SetStage(round);

        public void RevealWorld() => SetStage(1);

        public void ShowStage1() => SetStage(1);
        public void ShowStage2() => SetStage(2);
        public void ShowStage3() => SetStage(3);
        public void ShowStage4() => SetStage(4);
        public void ShowStage5() => SetStage(5);

        void ReadNumberKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
                SetStage(1);
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
                SetStage(2);
            else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
                SetStage(3);
            else if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame)
                SetStage(4);
            else if (keyboard.digit5Key.wasPressedThisFrame || keyboard.numpad5Key.wasPressedThisFrame)
                SetStage(5);
        }

        void ApplyParameters()
        {
            Transform anchor = visibilityAnchor != null ? visibilityAnchor : transform;
            Matrix4x4 worldToVisibility = Matrix4x4.TRS(
                anchor.position,
                anchor.rotation,
                Vector3.one).inverse;

            float safeDistanceEnd = Mathf.Max(distanceFadeEnd, distanceFadeStart + 0.01f);
            Vector4 radii = new Vector4(
                Mathf.Max(0.001f, Mathf.Abs(localVisibilityRadii.x)),
                Mathf.Max(0.001f, Mathf.Abs(localVisibilityRadii.y)),
                Mathf.Max(0.001f, Mathf.Abs(localVisibilityRadii.z)),
                0f);

            if (fullScreenMaterial != null)
            {
                fullScreenMaterial.SetFloat(VisionModeId, (float)mode);
                fullScreenMaterial.SetColor(DarkColorId, darkColor);
                fullScreenMaterial.SetFloat(DistanceStartId, distanceFadeStart);
                fullScreenMaterial.SetFloat(DistanceEndId, safeDistanceEnd);
                fullScreenMaterial.SetVector(VisibilityRadiiId, radii);
                fullScreenMaterial.SetFloat(VisibilityFeatherId, localVisibilityFeather);
                fullScreenMaterial.SetFloat(BoulderVisibilityRadiusId, boulderVisibilityRadius);
                fullScreenMaterial.SetFloat(BoulderVisibilityFeatherId, boulderVisibilityFeather);
                fullScreenMaterial.SetMatrix(WorldToVisibilityId, worldToVisibility);
            }

            if (!writeShaderGlobals)
                return;

            Shader.SetGlobalFloat(VisionModeId, (float)mode);
            Shader.SetGlobalColor(DarkColorId, darkColor);
            Shader.SetGlobalFloat(DistanceStartId, distanceFadeStart);
            Shader.SetGlobalFloat(DistanceEndId, safeDistanceEnd);
            Shader.SetGlobalVector(VisibilityRadiiId, radii);
            Shader.SetGlobalFloat(VisibilityFeatherId, localVisibilityFeather);
            Shader.SetGlobalFloat(BoulderVisibilityRadiusId, boulderVisibilityRadius);
            Shader.SetGlobalFloat(BoulderVisibilityFeatherId, boulderVisibilityFeather);
            Shader.SetGlobalMatrix(WorldToVisibilityId, worldToVisibility);
        }
    }
}
