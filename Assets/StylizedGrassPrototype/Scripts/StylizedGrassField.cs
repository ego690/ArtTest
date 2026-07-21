using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace StylizedGrassPrototype
{
    public enum GrassGradientRampMode
    {
        Smooth,
        Linear,
        Stepped
    }

    public enum GrassDistributionMode
    {
        ExistingMesh,
        GeneratedPlane
    }

    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class StylizedGrassField : MonoBehaviour
    {
        const int CurrentSamplingSettingsVersion = 1;
        static readonly HashSet<StylizedGrassField> ActiveFields = new HashSet<StylizedGrassField>();

        public static bool AnyInteractionEnabled
        {
            get
            {
                foreach (StylizedGrassField field in ActiveFields)
                {
                    if (field != null && field.isActiveAndEnabled && field.enableInteraction)
                        return true;
                }
                return false;
            }
        }

        [Header("Distribution Source")]
        [Tooltip("Existing Mesh keeps the MeshFilter geometry unchanged. Generated Plane creates a rectangular distribution mesh from Field Size and Subdivisions.")]
        public GrassDistributionMode distributionMode = GrassDistributionMode.ExistingMesh;

        [Header("Area Sampling")]
        [Tooltip("Average number of grass blades generated per square meter of source mesh surface.")]
        [Range(0f, 64f)] public float bladesPerSquareMeter = 20f;
        [Tooltip("Safety cap for one source triangle. Subdivide very large triangles when this cap limits density.")]
        [Range(1, 11)] public int maxBladesPerTriangle = 11;
        [Tooltip("When enabled, grass is omitted from surfaces steeper than Max Slope Angle.")]
        public bool enableSlopeFilter;
        [Range(0f, 90f)] public float maxSlopeAngle = 55f;

        [Header("Feature Toggles")]
        [Tooltip("Enables procedural wind motion and wind-driven color variation. Procedural leaf trees using this field as their wind source also stop moving when disabled.")]
        public bool enableWind = true;
        [Tooltip("Enables the drifting procedural color field used by the ground and blade gradients.")]
        public bool enableColorNoise = true;
        [Tooltip("Enables the URP main-light shadow mask on the grass field.")]
        public bool enableDynamicShadows = true;
        [Tooltip("Enables bending and flattening from Stylized Grass Interactor components.")]
        public bool enableInteraction = true;

        [Header("草地分布")]
        [Tooltip("草地平面的世界尺寸。数值越大，草地覆盖范围越大；不会自动增加单位面积内的草片密度。")]
        [Min(0.5f)] public float fieldSize = 10f;
        [Tooltip("平面的细分数量。每个三角形生成一组草片；数值越大草越密，但几何开销也越高。")]
        [Range(4, 180)] public int subdivisions = 80;
        [Tooltip("草片随机分布种子。修改它会改变草片的位置、缩放和朝向，但不会改变整体参数。")]
        public int distributionSeed = 17;

        [Header("草片遮罩与形状")]
        [Tooltip("草片黑白遮罩纹理。白色区域保留为草，黑色区域会被裁剪。")]
        public Texture2D bladeMask;
        [Tooltip("遮罩裁剪阈值。提高后只保留更亮的白色区域，草片轮廓会更细；过高可能丢失边缘。")]
        [Range(0f, 1f)] public float alphaCutoff = 0.5f;
        [Tooltip("单张草片卡片的基础宽度。最终宽度还会受到随机缩放影响。")]
        [Range(0.02f, 1f)] public float bladeWidth = 0.34f;
        [Tooltip("单张草片卡片的基础高度。最终高度还会受到随机缩放和风场拔高/压低影响。")]
        [Range(0.05f, 2f)] public float bladeHeight = 0.72f;
        [Tooltip("草片随机缩放范围：X 是最小倍率，Y 是最大倍率。范围越大，草片高矮差异越明显。")]
        public Vector2 randomScale = new Vector2(0.72f, 1.28f);
        [Tooltip("草片绕垂直轴的随机旋转强度。1 表示完整随机朝向，0 表示朝向基本一致。")]
        [Range(0f, 1f)] public float verticalAxisRotation = 1f;
        [Tooltip("草片绕另外两个轴的随机倾斜系数。数值越大，草片初始姿态越凌乱；它不等同于风弯曲。")]
        [Range(0f, 0.6f)] public float otherAxisCoefficient = 0.12f;
        [Tooltip("草片根部相对地面的垂直偏移。稍微提高可避免穿插或闪烁，过高会让草片悬空。")]
        [Range(-0.1f, 0.2f)] public float groundOffset = 0.015f;

        [Header("静态颜色噪声与渐变")]
        [HideInInspector]
        [Tooltip("旧版静态噪声纹理引用，仅为保留已有场景数据；当前 Shader 已改用程序噪声，不再读取此纹理。")]
        public Texture2D noiseTexture;
        [Tooltip("静态颜色噪声的世界缩放。数值越大，颜色斑块越小越密；数值越小，色块越宽广。")]
        [Range(0.01f, 2f)] public float noiseWorldScale = 0.18f;
        [Tooltip("云影颜色噪声的基础对比度。运行时会在此值上下约 22% 缓慢变化；提高后云影明暗分界更明显。")]
        [Range(0.1f, 4f)] public float noiseContrast = 1.25f;
        [Tooltip("云影程序噪声的初始偏移。用于更换起始分布；运行时还会沿风向自动漂移。")]
        public Vector2 noiseOffset;
        [Tooltip("云影漂移以及 Noise Contrast 时间变化的独立速度。0 会停止云影移动，但不会停止草片风浪。")]
        [Range(0f, 1f)] public float cloudShadowSpeed = 0.12f;
        [Tooltip("地面颜色渐变的低值颜色，对应静态噪声较暗的区域。")]
        public Color gradientLow = new Color(0.055f, 0.22f, 0.08f, 1f);
        [Tooltip("地面颜色渐变的中值颜色，对应静态噪声中间区域。")]
        public Color gradientMid = new Color(0.16f, 0.48f, 0.12f, 1f);
        [Tooltip("地面颜色渐变的高值颜色，对应静态噪声较亮的区域。")]
        public Color gradientHigh = new Color(0.56f, 0.82f, 0.20f, 1f);
        [Tooltip("草尖颜色渐变的低值颜色。它会从草根颜色逐渐过渡到草尖。")]
        public Color tipGradientLow = new Color(0.18f, 0.42f, 0.08f, 1f);
        [Tooltip("草尖颜色渐变的中值颜色。")]
        public Color tipGradientMid = new Color(0.48f, 0.76f, 0.14f, 1f);
        [Tooltip("草尖颜色渐变的高值颜色，通常用于最亮、最受风抬起的视觉区域。")]
        public Color tipGradientHigh = new Color(0.82f, 1f, 0.32f, 1f);
        [Tooltip("三色渐变中间颜色所在的位置。减小会扩大中高值颜色区域，提高会保留更多低值颜色。")]
        [Range(0.05f, 0.95f)] public float gradientMidpoint = 0.5f;
        [Tooltip("控制世界坐标噪声如何转换为草地颜色遮罩。Linear 使用卡通化的线性过渡；Smooth 保留原来的柔和过渡；Stepped 生成离散色带。")]
        public GrassGradientRampMode gradientRampMode = GrassGradientRampMode.Linear;
        [Tooltip("Stepped 模式使用的颜色分段数量，不影响 Smooth 和 Linear 模式。")]
        [Range(2, 12)] public int gradientRampSteps = 4;

        [Header("整体渲染风格")]
        [Tooltip("整体色相偏移。0 保持原色，正负方向会沿色环整体改变地面和草尖颜色。")]
        [Range(-1f, 1f)] public float hueShift;
        [Tooltip("整体饱和度倍率。0 为灰度，1 接近原始颜色，增大后颜色更鲜艳。")]
        [Range(0f, 2f)] public float saturation = 1.15f;
        [Tooltip("整体明度倍率。同时影响地面和草片；过高可能产生大面积纯亮颜色。")]
        [Range(0f, 2f)] public float brightness = 1.08f;
        [Tooltip("草片的 Unlit 提亮倍率，只影响竖起的草片，不影响地面。用于保持卡通草片明亮可读。")]
        [Range(0.5f, 3f)] public float unlitBladeBoost = 1.25f;
        [Tooltip("从草根到草尖的额外亮度倍率。1 表示不额外提亮，增大后草尖更亮。")]
        [Range(0.5f, 2f)] public float tipBrightness = 1.12f;

        [Header("动态柏林风场")]
        [HideInInspector]
        [Tooltip("旧版风场纹理引用，仅为保留已有场景数据；当前 Shader 已改用程序噪声，不再读取此纹理。")]
        public Texture2D windNoiseTexture;
        [Tooltip("大尺度风浪的采样缩放。数值越小，风浪区域越宽；数值越大，大片起伏更密集。")]
        [Range(0.005f, 0.2f)] public float windLargeScale = 0.03f;
        [Tooltip("小尺度扰动的采样缩放。数值越大，草地上的细碎风纹越密。")]
        [Range(0.02f, 0.5f)] public float windDetailScale = 0.15f;
        [Tooltip("小尺度扰动混入大风浪的权重。0 只有平缓大风浪，提高后会增加细碎变化。")]
        [Range(0f, 1f)] public float windDetailStrength = 0.25f;
        [Tooltip("噪声生成前对世界 X/Z 坐标分别乘的倍率。两个值不相等时会把噪声拉成长条波纹；(1,1) 为等比例噪声。")]
        public Vector2 windNoiseAxisScale = new Vector2(0.45f, 2.4f);
        [Tooltip("草片程序风场沿风向移动的速度。0 会停止草片风浪，但不会停止云影移动。")]
        [Range(0f, 1f)] public float windSpeed = 0.12f;
        [Tooltip("草浪与云影在世界 X/Z 平面中的共同移动方向。X 控制左右，Y 实际对应世界 Z 前后。")]
        public Vector2 windDirection = new Vector2(0.8f, 0.35f);
        [Tooltip("风场时间偏移。用于预览或切换不同风相位；通常保持 0，不用于控制正常播放速度。")]
        public float windTimeOffset;
        [Tooltip("风对草片横向弯曲的最大影响。增大后低风值区域更明显地顺风压伏。")]
        [Range(0f, 1.5f)] public float windBendStrength = 0.82f;
        [Tooltip("风值对草片高度的影响。增大后低值区域更矮、高值区域更高，草浪起伏更明显。")]
        [Range(0f, 0.6f)] public float windHeightInfluence = 0.32f;
        [Tooltip("风值附加到草片朝向旋转的强度。提高可增强草浪方向感，过高会显得扭转杂乱。")]
        [Range(0f, 1f)] public float windRotationStrength = 0.28f;
        [Tooltip("草片从根部到尖端的弯曲曲线指数。较大时根部更直、弯曲集中在草尖；较小时整片草更早弯曲。")]
        [Range(0.5f, 4f)] public float windBendCurve = 1.8f;
        [Tooltip("风浪对明暗的影响强度。0 关闭动态明暗波纹，提高后高风值更亮、低风值更暗。")]
        [Range(0f, 1f)] public float waveLightStrength = 0.28f;
        [Tooltip("风噪声低值区域的颜色乘色。用于定义草浪暗部色调，而不只是降低明度。")]
        public Color waveShadowTint = new Color(0.52f, 0.72f, 0.58f, 1f);
        [Tooltip("风噪声高值区域的颜色乘色。用于定义草浪高光色调和暖冷倾向。")]
        public Color waveHighlightTint = new Color(1.12f, 1.08f, 0.82f, 1f);

        [Header("动态物体阴影遮罩")]
        [Tooltip("其他物体投射到草地上的阴影颜色。草地自身不会投射阴影。")]
        public Color dynamicShadowColor = new Color(0.055f, 0.07f, 0.045f, 1f);
        [Tooltip("动态物体阴影的整体强度。0 不显示接收阴影，1 完全使用设定的阴影颜色。")]
        [Range(0f, 1f)] public float dynamicShadowStrength = 0.72f;
        [Tooltip("把主光阴影衰减转换成黑白遮罩时的中心阈值。调节它可改变阴影覆盖与显现范围。")]
        [Range(0f, 1f)] public float shadowThreshold = 0.55f;
        [Tooltip("动态阴影遮罩边缘的柔和范围。数值越小边缘越硬，数值越大过渡越宽。")]
        [Range(0.001f, 0.5f)] public float shadowSoftness = 0.18f;

        MeshFilter meshFilter;
        MeshRenderer meshRenderer;
        MaterialPropertyBlock propertyBlock;
        [SerializeField, HideInInspector] bool distributionModeInitialized;
        [SerializeField, HideInInspector] int samplingSettingsVersion;
        [SerializeField, HideInInspector] Mesh sourceMesh;
        Mesh generatedMesh;
        float cachedFieldSize = -1f;
        int cachedSubdivisions = -1;

        void Reset()
        {
            CacheComponents();
            enableSlopeFilter = false;
            samplingSettingsVersion = CurrentSamplingSettingsVersion;
            CaptureSourceMesh();
            distributionMode = sourceMesh != null
                ? GrassDistributionMode.ExistingMesh
                : GrassDistributionMode.GeneratedPlane;
            distributionModeInitialized = true;
            EnsureDistributionMesh();
            ApplyMaterialProperties();
        }

        void OnEnable()
        {
            ActiveFields.Add(this);
            CacheComponents();
            InitializeDistributionMode();
            InitializeSamplingSettings();
            EnsureDistributionMesh();
            ApplyMaterialProperties();
        }

        void OnValidate()
        {
            bladesPerSquareMeter = Mathf.Max(0f, bladesPerSquareMeter);
            maxBladesPerTriangle = Mathf.Clamp(maxBladesPerTriangle, 1, 11);
            maxSlopeAngle = Mathf.Clamp(maxSlopeAngle, 0f, 90f);
            randomScale.x = Mathf.Max(0.01f, randomScale.x);
            randomScale.y = Mathf.Max(randomScale.x, randomScale.y);
            gradientRampSteps = Mathf.Clamp(gradientRampSteps, 2, 12);
            windNoiseAxisScale.x = Mathf.Max(0.001f, Mathf.Abs(windNoiseAxisScale.x));
            windNoiseAxisScale.y = Mathf.Max(0.001f, Mathf.Abs(windNoiseAxisScale.y));
            CacheComponents();
            InitializeDistributionMode();
            InitializeSamplingSettings();
            EnsureDistributionMesh();
            ApplyMaterialProperties();
        }

        void Update()
        {
            if (!Application.isPlaying)
                ApplyMaterialProperties();
        }

        void OnDisable()
        {
            ActiveFields.Remove(this);
            RestoreSourceMesh();
            ReleaseGeneratedMesh();
        }

        void OnDestroy()
        {
            ActiveFields.Remove(this);
            RestoreSourceMesh();
            ReleaseGeneratedMesh();
        }

        [ContextMenu("Rebuild Distribution Mesh")]
        public void RebuildDistributionMesh()
        {
            CacheComponents();
            CaptureSourceMesh();
            distributionMode = GrassDistributionMode.GeneratedPlane;
            ReplaceGeneratedMesh(CreateDistributionMesh(fieldSize, subdivisions));
        }

        public void Refresh()
        {
            CacheComponents();
            EnsureDistributionMesh();
            ApplyMaterialProperties();
        }

        public static Mesh CreateDistributionMesh(float size, int divisions)
        {
            divisions = Mathf.Clamp(divisions, 1, 256);
            int side = divisions + 1;
            var vertices = new Vector3[side * side];
            var normals = new Vector3[vertices.Length];
            var tangents = new Vector4[vertices.Length];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[divisions * divisions * 6];

            int vertexIndex = 0;
            for (int z = 0; z <= divisions; z++)
            {
                float v = z / (float)divisions;
                for (int x = 0; x <= divisions; x++)
                {
                    float u = x / (float)divisions;
                    vertices[vertexIndex] = new Vector3((u - 0.5f) * size, 0f, (v - 0.5f) * size);
                    normals[vertexIndex] = Vector3.up;
                    tangents[vertexIndex] = new Vector4(1f, 0f, 0f, 1f);
                    uvs[vertexIndex] = new Vector2(u, v);
                    vertexIndex++;
                }
            }

            int triangleIndex = 0;
            for (int z = 0; z < divisions; z++)
            {
                for (int x = 0; x < divisions; x++)
                {
                    int bottomLeft = z * side + x;
                    int topLeft = (z + 1) * side + x;
                    triangles[triangleIndex++] = bottomLeft;
                    triangles[triangleIndex++] = topLeft;
                    triangles[triangleIndex++] = bottomLeft + 1;
                    triangles[triangleIndex++] = bottomLeft + 1;
                    triangles[triangleIndex++] = topLeft;
                    triangles[triangleIndex++] = topLeft + 1;
                }
            }

            var mesh = new Mesh
            {
                name = $"Stylized Grass Distribution {divisions}x{divisions}",
                indexFormat = vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.bounds = new Bounds(Vector3.up, new Vector3(size + 4f, 3f, size + 4f));
            return mesh;
        }

        void CacheComponents()
        {
            if (meshFilter == null)
                meshFilter = GetComponent<MeshFilter>();
            if (meshRenderer == null)
                meshRenderer = GetComponent<MeshRenderer>();
            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();
        }

        void InitializeDistributionMode()
        {
            if (distributionModeInitialized)
                return;

            distributionMode = meshFilter != null && meshFilter.sharedMesh != null
                ? GrassDistributionMode.ExistingMesh
                : GrassDistributionMode.GeneratedPlane;
            distributionModeInitialized = true;
            CaptureSourceMesh();
        }

        void InitializeSamplingSettings()
        {
            if (samplingSettingsVersion >= CurrentSamplingSettingsVersion)
                return;

            enableSlopeFilter = false;
            samplingSettingsVersion = CurrentSamplingSettingsVersion;
        }

        void EnsureDistributionMesh()
        {
            if (meshFilter == null)
                return;

            if (distributionMode == GrassDistributionMode.ExistingMesh)
            {
                if (generatedMesh != null)
                {
                    RestoreSourceMesh();
                    ReleaseGeneratedMesh();
                }
                else if (meshFilter.sharedMesh != null)
                {
                    sourceMesh = meshFilter.sharedMesh;
                }

                cachedFieldSize = fieldSize;
                cachedSubdivisions = subdivisions;
                return;
            }

            CaptureSourceMesh();
            bool dimensionsChanged = !Mathf.Approximately(cachedFieldSize, fieldSize)
                || cachedSubdivisions != subdivisions;
            bool hasCachedConfiguration = cachedFieldSize >= 0f && cachedSubdivisions >= 0;

            if (generatedMesh == null
                || meshFilter.sharedMesh != generatedMesh
                || (hasCachedConfiguration && dimensionsChanged))
                ReplaceGeneratedMesh(CreateDistributionMesh(fieldSize, subdivisions));

            cachedFieldSize = fieldSize;
            cachedSubdivisions = subdivisions;
        }

        void ReplaceGeneratedMesh(Mesh replacement)
        {
            ReleaseGeneratedMesh();

            generatedMesh = replacement;
            generatedMesh.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            meshFilter.sharedMesh = generatedMesh;
            cachedFieldSize = fieldSize;
            cachedSubdivisions = subdivisions;
        }

        void CaptureSourceMesh()
        {
            if (meshFilter == null || meshFilter.sharedMesh == null)
                return;

            if (meshFilter.sharedMesh != generatedMesh)
                sourceMesh = meshFilter.sharedMesh;
        }

        void RestoreSourceMesh()
        {
            if (meshFilter != null && meshFilter.sharedMesh == generatedMesh)
                meshFilter.sharedMesh = sourceMesh;
        }

        void ReleaseGeneratedMesh()
        {
            if (generatedMesh == null)
                return;

            if (Application.isPlaying)
                Destroy(generatedMesh);
            else
                DestroyImmediate(generatedMesh);
            generatedMesh = null;
        }

        void ApplyMaterialProperties()
        {
            if (meshRenderer == null)
                return;

            UpdateRendererBounds();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = enableDynamicShadows;
            propertyBlock.Clear();
            if (bladeMask != null)
                propertyBlock.SetTexture("_BladeMask", bladeMask);
            propertyBlock.SetFloat("_GrassDensity", bladesPerSquareMeter);
            propertyBlock.SetFloat("_MaxBladesPerTriangle", maxBladesPerTriangle);
            propertyBlock.SetFloat("_SlopeFilterEnabled", enableSlopeFilter ? 1f : 0f);
            propertyBlock.SetFloat("_MinGrassUpDot", Mathf.Cos(maxSlopeAngle * Mathf.Deg2Rad));
            propertyBlock.SetFloat("_WindEnabled", enableWind ? 1f : 0f);
            propertyBlock.SetFloat("_FieldNoiseEnabled", enableColorNoise ? 1f : 0f);
            propertyBlock.SetFloat("_DynamicShadowEnabled", enableDynamicShadows ? 1f : 0f);
            propertyBlock.SetFloat("_InteractionEnabled", enableInteraction ? 1f : 0f);
            propertyBlock.SetFloat("_AlphaCutoff", alphaCutoff);
            propertyBlock.SetFloat("_BladeWidth", bladeWidth);
            propertyBlock.SetFloat("_BladeHeight", bladeHeight);
            propertyBlock.SetVector("_ScaleMinMax", new Vector4(randomScale.x, randomScale.y, 0f, 0f));
            propertyBlock.SetFloat("_VerticalRotation", verticalAxisRotation);
            propertyBlock.SetFloat("_TiltCoefficient", otherAxisCoefficient);
            propertyBlock.SetFloat("_DistributionSeed", distributionSeed);
            propertyBlock.SetFloat("_GroundOffset", groundOffset);
            propertyBlock.SetColor("_GradientLow", gradientLow);
            propertyBlock.SetColor("_GradientMid", gradientMid);
            propertyBlock.SetColor("_GradientHigh", gradientHigh);
            propertyBlock.SetColor("_TipGradientLow", tipGradientLow);
            propertyBlock.SetColor("_TipGradientMid", tipGradientMid);
            propertyBlock.SetColor("_TipGradientHigh", tipGradientHigh);
            propertyBlock.SetFloat("_GradientMidpoint", gradientMidpoint);
            propertyBlock.SetFloat("_GradientRampMode", (float)gradientRampMode);
            propertyBlock.SetFloat("_GradientRampSteps", gradientRampSteps);
            propertyBlock.SetFloat("_NoiseScale", noiseWorldScale);
            propertyBlock.SetFloat("_NoiseContrast", noiseContrast);
            propertyBlock.SetVector("_NoiseOffset", new Vector4(noiseOffset.x, noiseOffset.y, 0f, 0f));
            propertyBlock.SetFloat("_CloudShadowSpeed", cloudShadowSpeed);
            propertyBlock.SetFloat("_HueShift", hueShift);
            propertyBlock.SetFloat("_Saturation", saturation);
            propertyBlock.SetFloat("_Brightness", brightness);
            propertyBlock.SetFloat("_UnlitBoost", unlitBladeBoost);
            propertyBlock.SetFloat("_TipBrightness", tipBrightness);
            propertyBlock.SetFloat("_WindLargeScale", windLargeScale);
            propertyBlock.SetFloat("_WindDetailScale", windDetailScale);
            propertyBlock.SetFloat("_WindDetailStrength", windDetailStrength);
            propertyBlock.SetVector("_WindNoiseAxisScale", new Vector4(
                windNoiseAxisScale.x,
                windNoiseAxisScale.y,
                0f,
                0f));
            propertyBlock.SetFloat("_WindSpeed", windSpeed);
            propertyBlock.SetVector("_WindDirection", new Vector4(windDirection.x, 0f, windDirection.y, 0f));
            propertyBlock.SetFloat("_WindTimeOffset", windTimeOffset);
            propertyBlock.SetFloat("_WindBendStrength", windBendStrength);
            propertyBlock.SetFloat("_WindHeightInfluence", windHeightInfluence);
            propertyBlock.SetFloat("_WindRotationStrength", windRotationStrength);
            propertyBlock.SetFloat("_WindBendCurve", windBendCurve);
            propertyBlock.SetFloat("_WaveLightStrength", waveLightStrength);
            propertyBlock.SetColor("_WaveShadowTint", waveShadowTint);
            propertyBlock.SetColor("_WaveHighlightTint", waveHighlightTint);
            propertyBlock.SetColor("_DynamicShadowColor", dynamicShadowColor);
            propertyBlock.SetFloat("_DynamicShadowStrength", dynamicShadowStrength);
            propertyBlock.SetFloat("_ShadowThreshold", shadowThreshold);
            propertyBlock.SetFloat("_ShadowSoftness", shadowSoftness);
            meshRenderer.SetPropertyBlock(propertyBlock);
        }

        void UpdateRendererBounds()
        {
            if (meshFilter == null || meshFilter.sharedMesh == null)
                return;

            Bounds bounds = meshFilter.sharedMesh.bounds;
            float maximumScale = Mathf.Max(0.01f, randomScale.y);
            float maximumHeight = bladeHeight * maximumScale
                * (1f + (enableWind ? Mathf.Abs(windHeightInfluence) : 0f));
            float maximumBend = enableWind
                ? windBendStrength * bladeHeight * maximumScale
                : 0f;
            float halfWidth = bladeWidth * maximumScale * 0.5f;
            float maximumInteractionBend = enableInteraction ? 3f : 0f;
            float padding = maximumHeight
                + maximumBend
                + maximumInteractionBend
                + halfWidth
                + Mathf.Abs(groundOffset);
            bounds.Expand(padding * 2f);
            meshRenderer.localBounds = bounds;
        }
    }
}
