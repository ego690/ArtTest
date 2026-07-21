using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace StylizedGrassPrototype
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class ProceduralLeafTree : MonoBehaviour
    {
        [Header("Crown Source Geometry")]
        [Tooltip("树冠源椭球在树对象局部坐标中的中心位置。修改它会整体移动程序化叶片分布区域。")]
        public Vector3 crownCenter = new Vector3(0f, 3.2f, 0f);
        [Tooltip("树冠源椭球在局部 X、Y、Z 三个方向的半径，分别控制树冠宽度、高度和深度。")]
        public Vector3 crownRadii = new Vector3(1.55f, 1.4f, 1.45f);
        [Tooltip("树冠源椭球从底部到顶部的环数。数值越高，采样表面更细致，但重建开销也会增加。")]
        [Range(3, 12)] public int sourceRings = 6;
        [Tooltip("树冠源椭球水平方向的分段数。数值越高，树冠轮廓和叶片分布更均匀，但重建开销也会增加。")]
        [Range(4, 20)] public int sourceSegments = 10;
        [Tooltip("Leaf Density 为 1 时的基础叶片簇采样点数量。每个采样点会生成三张相交叶片卡片。")]
        [Min(1)] public int leafPointCount = 72;
        [Tooltip("叶片密度倍率，不修改基础 Leaf Point Count。1 保持基础数量，提高会增加叶片数量和渲染开销。")]
        [Range(0.1f, 4f)] public float leafDensity = 1f;
        [Tooltip("沿树冠源表面法线推动叶片簇的距离。正值向树冠外侧移动，负值向内部收缩。")]
        [Range(-0.2f, 0.3f)] public float surfaceOffset = 0.02f;

        [Header("Three-Cross Leaf Instances")]
        [Tooltip("单张叶片卡片的局部尺寸：X 控制宽度，Y 控制高度。每个叶片簇由三张该尺寸的卡片交叉组成。")]
        public Vector2 leafCardSize = new Vector2(1.05f, 1.05f);
        [Tooltip("每个叶片簇的随机统一缩放范围：X 为最小倍率，Y 为最大倍率。")]
        public Vector2 randomScale = new Vector2(0.72f, 1.24f);
        [Tooltip("叶片簇绕局部 Y 轴随机旋转的角度范围，单位为度；用于改变水平方向朝向。")]
        public Vector2 randomYaw = new Vector2(0f, 360f);
        [Tooltip("叶片簇绕局部 X 轴随机旋转的角度范围，单位为度；用于产生前后倾斜。")]
        public Vector2 randomPitch = new Vector2(-22f, 22f);
        [Tooltip("叶片簇绕局部 Z 轴随机旋转的角度范围，单位为度；用于产生左右倾斜。")]
        public Vector2 randomRoll = new Vector2(-18f, 18f);
        [Tooltip("程序化分布随机种子。修改后会重新排列叶片位置、缩放和旋转，但不会改变叶片数量。")]
        public int distributionSeed = 41;
        [Tooltip("该树使用的树叶材质，通常指定 Stylized Procedural Leaves 材质。")]
        public Material leafMaterial;

        [Header("Per-Tree Final Color")]
        [Tooltip("该树最终输出颜色的色相偏移。0 保持材质原色，正负值会沿色环旋转。")]
        [Range(-1f, 1f)] public float hueShift;
        [Tooltip("该树最终输出颜色的饱和度倍率。0 为灰度，1 保持材质饱和度，大于 1 会更鲜艳。")]
        [Range(0f, 2f)] public float saturation = 1f;
        [Tooltip("该树最终输出颜色的 HSV 明度倍率。0 为黑色，1 保持材质明度，大于 1 会整体提亮。")]
        [Range(0f, 2f)] public float value = 1f;

        [Header("Shared Grass Wind")]
        [Tooltip("可选的草地风场来源，用于共享世界坐标噪声、风向和基础强度。留空时自动使用场景中第一个启用的 Stylized Grass Field。")]
        public StylizedGrassField windSource;
        [Tooltip("仅控制该树的风动画频率。0 冻结树叶风相位，1 与草地速度一致，大于 1 会让树叶晃动得更快，不影响草地。")]
        [Range(0f, 4f)] public float windFrequencyMultiplier = 1.5f;
        [Tooltip("该树风力幅度总倍率，同时缩放位移和旋转。0 关闭树叶晃动，1 保持下方两个细分倍率的原始强度。")]
        [Range(0f, 3f)] public float windAmplitudeMultiplier = 1f;
        [Tooltip("树叶顶点在世界空间中的方向位移和垂直位移倍率。只影响该树，不修改草地风参数。")]
        [Range(0f, 2f)] public float windDisplacementMultiplier = 0.35f;
        [Tooltip("叶片卡片围绕各自簇中心旋转的风力倍率。提高会增强摆动和扭转，只影响该树。")]
        [Range(0f, 2f)] public float windRotationMultiplier = 0.5f;

        MeshFilter meshFilter;
        MeshRenderer meshRenderer;
        MaterialPropertyBlock propertyBlock;
        Mesh generatedMesh;
        StylizedGrassField cachedAutomaticWindSource;

        readonly struct CrownTriangle
        {
            public readonly Vector3 a;
            public readonly Vector3 b;
            public readonly Vector3 c;
            public readonly Vector3 normal;
            public readonly float area;

            public CrownTriangle(Vector3 a, Vector3 b, Vector3 c)
            {
                this.a = a;
                this.b = b;
                this.c = c;
                Vector3 cross = Vector3.Cross(b - a, c - a);
                area = cross.magnitude * 0.5f;
                normal = cross.sqrMagnitude > 0.000001f ? cross.normalized : Vector3.up;
            }
        }

        void OnEnable()
        {
            RebuildLeafInstances();
        }

        void OnValidate()
        {
            crownRadii.x = Mathf.Max(0.1f, Mathf.Abs(crownRadii.x));
            crownRadii.y = Mathf.Max(0.1f, Mathf.Abs(crownRadii.y));
            crownRadii.z = Mathf.Max(0.1f, Mathf.Abs(crownRadii.z));
            leafCardSize.x = Mathf.Max(0.05f, leafCardSize.x);
            leafCardSize.y = Mathf.Max(0.05f, leafCardSize.y);
            randomScale.x = Mathf.Max(0.01f, randomScale.x);
            randomScale.y = Mathf.Max(randomScale.x, randomScale.y);
            leafDensity = Mathf.Max(0.1f, leafDensity);
            saturation = Mathf.Max(0f, saturation);
            value = Mathf.Max(0f, value);
            windFrequencyMultiplier = Mathf.Max(0f, windFrequencyMultiplier);
            windAmplitudeMultiplier = Mathf.Max(0f, windAmplitudeMultiplier);
            windDisplacementMultiplier = Mathf.Max(0f, windDisplacementMultiplier);
            windRotationMultiplier = Mathf.Max(0f, windRotationMultiplier);

            if (isActiveAndEnabled)
                RebuildLeafInstances();
        }

        void Update()
        {
            CacheComponents();
            ApplyMaterialProperties();
            UpdateRendererBounds();
        }

        void OnDestroy()
        {
            ReleaseGeneratedMesh();
        }

        [ContextMenu("Rebuild Leaf Instances")]
        public void RebuildLeafInstances()
        {
            CacheComponents();
            ReleaseGeneratedMesh();

            generatedMesh = CreateLeafMesh();
            generatedMesh.name = name + " Procedural Leaf Instances";
            generatedMesh.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            meshFilter.sharedMesh = generatedMesh;

            meshRenderer.sharedMaterial = leafMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.On;
            meshRenderer.receiveShadows = true;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            ApplyMaterialProperties();
            UpdateRendererBounds();
        }

        Mesh CreateLeafMesh()
        {
            List<CrownTriangle> crownTriangles = CreateCrownTriangles();
            float totalArea = 0f;
            var cumulativeAreas = new float[crownTriangles.Count];
            for (int i = 0; i < crownTriangles.Count; i++)
            {
                totalArea += crownTriangles[i].area;
                cumulativeAreas[i] = totalArea;
            }

            int pointCount = Mathf.Max(1, Mathf.RoundToInt(leafPointCount * leafDensity));
            var vertices = new List<Vector3>(pointCount * 12);
            var normals = new List<Vector3>(pointCount * 12);
            var tangents = new List<Vector4>(pointCount * 12);
            var uvs = new List<Vector2>(pointCount * 12);
            var pivots = new List<Vector3>(pointCount * 12);
            var triangles = new List<int>(pointCount * 18);

            Random.State previousRandomState = Random.state;
            Random.InitState(distributionSeed);
            try
            {
                for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
                {
                    CrownTriangle source = SelectTriangle(crownTriangles, cumulativeAreas, totalArea);
                    Vector3 point = SampleTriangle(source) + source.normal * surfaceOffset;
                    float scale = Random.Range(randomScale.x, randomScale.y);
                    Quaternion clusterRotation = Quaternion.Euler(
                        Random.Range(randomPitch.x, randomPitch.y),
                        Random.Range(randomYaw.x, randomYaw.y),
                        Random.Range(randomRoll.x, randomRoll.y));

                    for (int cardIndex = 0; cardIndex < 3; cardIndex++)
                    {
                        Quaternion cardRotation = clusterRotation
                            * Quaternion.AngleAxis(cardIndex * 60f, Vector3.up);
                        AddCard(
                            vertices,
                            normals,
                            tangents,
                            uvs,
                            pivots,
                            triangles,
                            point,
                            cardRotation,
                            leafCardSize * scale);
                    }
                }
            }
            finally
            {
                Random.state = previousRandomState;
            }

            var mesh = new Mesh
            {
                indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(1, pivots);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        List<CrownTriangle> CreateCrownTriangles()
        {
            int rings = Mathf.Max(3, sourceRings);
            int segments = Mathf.Max(4, sourceSegments);
            var ringPoints = new Vector3[rings + 1, segments];

            for (int ring = 0; ring <= rings; ring++)
            {
                float latitude = Mathf.PI * ring / rings;
                float horizontal = Mathf.Sin(latitude);
                float vertical = Mathf.Cos(latitude);
                for (int segment = 0; segment < segments; segment++)
                {
                    float longitude = Mathf.PI * 2f * segment / segments;
                    ringPoints[ring, segment] = crownCenter + new Vector3(
                        Mathf.Cos(longitude) * horizontal * crownRadii.x,
                        vertical * crownRadii.y,
                        Mathf.Sin(longitude) * horizontal * crownRadii.z);
                }
            }

            var result = new List<CrownTriangle>(segments * rings * 2);
            for (int ring = 0; ring < rings; ring++)
            {
                for (int segment = 0; segment < segments; segment++)
                {
                    int nextSegment = (segment + 1) % segments;
                    Vector3 bottomLeft = ringPoints[ring, segment];
                    Vector3 bottomRight = ringPoints[ring, nextSegment];
                    Vector3 topLeft = ringPoints[ring + 1, segment];
                    Vector3 topRight = ringPoints[ring + 1, nextSegment];

                    if (ring > 0)
                        result.Add(new CrownTriangle(bottomLeft, topLeft, bottomRight));
                    if (ring < rings - 1)
                        result.Add(new CrownTriangle(bottomRight, topLeft, topRight));
                }
            }

            return result;
        }

        static CrownTriangle SelectTriangle(
            IReadOnlyList<CrownTriangle> triangles,
            IReadOnlyList<float> cumulativeAreas,
            float totalArea)
        {
            float selection = Random.value * totalArea;
            for (int i = 0; i < cumulativeAreas.Count; i++)
            {
                if (selection <= cumulativeAreas[i])
                    return triangles[i];
            }

            return triangles[triangles.Count - 1];
        }

        static Vector3 SampleTriangle(CrownTriangle triangle)
        {
            float root = Mathf.Sqrt(Random.value);
            float second = Random.value;
            float weightA = 1f - root;
            float weightB = root * (1f - second);
            float weightC = root * second;
            return triangle.a * weightA + triangle.b * weightB + triangle.c * weightC;
        }

        static void AddCard(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector4> tangents,
            List<Vector2> uvs,
            List<Vector3> pivots,
            List<int> triangles,
            Vector3 center,
            Quaternion rotation,
            Vector2 size)
        {
            int firstVertex = vertices.Count;
            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;
            Vector3 cardNormal = rotation * Vector3.forward;
            Vector3 cardTangent = rotation * Vector3.right;

            vertices.Add(center + rotation * new Vector3(-halfWidth, -halfHeight, 0f));
            vertices.Add(center + rotation * new Vector3(halfWidth, -halfHeight, 0f));
            vertices.Add(center + rotation * new Vector3(halfWidth, halfHeight, 0f));
            vertices.Add(center + rotation * new Vector3(-halfWidth, halfHeight, 0f));

            for (int i = 0; i < 4; i++)
            {
                normals.Add(cardNormal);
                tangents.Add(new Vector4(cardTangent.x, cardTangent.y, cardTangent.z, 1f));
                pivots.Add(center);
            }

            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(0f, 1f));

            triangles.Add(firstVertex);
            triangles.Add(firstVertex + 1);
            triangles.Add(firstVertex + 2);
            triangles.Add(firstVertex);
            triangles.Add(firstVertex + 2);
            triangles.Add(firstVertex + 3);
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

        void ApplyMaterialProperties()
        {
            propertyBlock.Clear();
            propertyBlock.SetFloat("_CrownBottom", crownCenter.y - crownRadii.y);
            propertyBlock.SetFloat("_CrownTop", crownCenter.y + crownRadii.y);
            propertyBlock.SetVector("_NoiseOffset", new Vector4(
                distributionSeed * 0.137f,
                distributionSeed * 0.271f,
                distributionSeed * 0.419f,
                0f));
            propertyBlock.SetFloat("_TreeHueShift", hueShift);
            propertyBlock.SetFloat("_TreeSaturation", saturation);
            propertyBlock.SetFloat("_TreeValue", value);
            ApplyWindMaterialProperties();
            meshRenderer.SetPropertyBlock(propertyBlock);
        }

        void ApplyWindMaterialProperties()
        {
            StylizedGrassField source = ResolveWindSource();
            propertyBlock.SetFloat("_WindEnabled", source == null || source.enableWind ? 1f : 0f);
            if (source != null)
            {
                propertyBlock.SetFloat("_DistributionSeed", source.distributionSeed);
                propertyBlock.SetFloat("_WindLargeScale", source.windLargeScale);
                propertyBlock.SetFloat("_WindDetailScale", source.windDetailScale);
                propertyBlock.SetFloat("_WindDetailStrength", source.windDetailStrength);
                propertyBlock.SetVector("_WindNoiseAxisScale", new Vector4(
                    source.windNoiseAxisScale.x,
                    source.windNoiseAxisScale.y,
                    0f,
                    0f));
                propertyBlock.SetFloat("_WindSpeed", source.windSpeed);
                propertyBlock.SetVector("_WindDirection", new Vector4(
                    source.windDirection.x,
                    0f,
                    source.windDirection.y,
                    0f));
                propertyBlock.SetFloat("_WindTimeOffset", source.windTimeOffset);
                propertyBlock.SetFloat("_WindBendStrength", source.windBendStrength);
                propertyBlock.SetFloat("_WindHeightInfluence", source.windHeightInfluence);
                propertyBlock.SetFloat("_WindRotationStrength", source.windRotationStrength);
                propertyBlock.SetFloat("_WindBendCurve", source.windBendCurve);
            }

            propertyBlock.SetFloat("_LeafWindFrequencyMultiplier", windFrequencyMultiplier);
            propertyBlock.SetFloat("_LeafWindAmplitudeMultiplier", windAmplitudeMultiplier);
            propertyBlock.SetFloat("_LeafWindDisplacementMultiplier", windDisplacementMultiplier);
            propertyBlock.SetFloat("_LeafWindRotationMultiplier", windRotationMultiplier);
        }

        StylizedGrassField ResolveWindSource()
        {
            if (windSource != null)
                return windSource;
            if (cachedAutomaticWindSource == null)
                cachedAutomaticWindSource = FindFirstObjectByType<StylizedGrassField>();
            return cachedAutomaticWindSource;
        }

        void UpdateRendererBounds()
        {
            if (meshRenderer == null || meshFilter == null || meshFilter.sharedMesh == null)
                return;

            StylizedGrassField source = ResolveWindSource();
            bool windEnabled = source == null || source.enableWind;
            float sourceBend = windEnabled
                ? (source != null ? source.windBendStrength : 0.82f)
                : 0f;
            float sourceHeight = windEnabled
                ? (source != null ? source.windHeightInfluence : 0.32f)
                : 0f;
            float cardRadius = leafCardSize.magnitude * randomScale.y * 0.5f;
            float movement = (sourceBend * windDisplacementMultiplier
                + sourceHeight * windDisplacementMultiplier
                + cardRadius * windRotationMultiplier)
                * windAmplitudeMultiplier;
            Bounds bounds = meshFilter.sharedMesh.bounds;
            bounds.Expand(Mathf.Max(0.1f, movement) * 2f);
            meshRenderer.localBounds = bounds;
        }

        void ReleaseGeneratedMesh()
        {
            if (generatedMesh == null)
                return;

            if (meshFilter != null && meshFilter.sharedMesh == generatedMesh)
                meshFilter.sharedMesh = null;

            if (Application.isPlaying)
                Destroy(generatedMesh);
            else
                DestroyImmediate(generatedMesh);
            generatedMesh = null;
        }
    }
}
