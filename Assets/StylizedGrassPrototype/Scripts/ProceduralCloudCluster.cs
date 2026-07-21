using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace StylizedGrassPrototype
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class ProceduralCloudCluster : MonoBehaviour
    {
        const int MaximumSphereCount = 128;
        const int MaximumCloudletCount = 4096;

        [Header("Sphere Growth")]
        [Tooltip("程序化云在对象局部坐标中的核心位置。修改它会移动整团云的生成中心。")]
        public Vector3 cloudCenter;
        [Tooltip("第一颗核心球的半径，也是后续球体尺寸的起点。")]
        [Min(0.1f)] public float coreRadius = 1.8f;
        [Tooltip("球体向外生长的迭代次数。0 只保留核心球；每轮都会让上一轮球体继续生成子球，最多保留 128 个球。")]
        [Range(0, 4)] public int growthIterations = 2;
        [Tooltip("每一轮中，每个父球生成的子球数量。提高后云形更复杂，但球面实例数量和网格开销也会快速增加。")]
        [Range(1, 4)] public int childSpheresPerParent = 2;
        [Tooltip("子球半径相对父球半径的随机范围：X 为最小倍率，Y 为最大倍率。")]
        public Vector2 childRadiusRatio = new Vector2(0.48f, 0.72f);
        [Tooltip("子球中心相对父球半径的表面距离倍率。1 表示子球中心位于父球表面；降低会让球体重叠得更多。")]
        [Range(0.35f, 1.25f)] public float childSurfaceDistance = 0.82f;
        [Tooltip("球体生长方向在局部 X、Y、Z 轴上的权重。减小 Y 可生成更扁平、横向延伸的云。")]
        public Vector3 growthAxisScale = new Vector3(1f, 0.48f, 0.78f);
        [Tooltip("球体生长的垂直偏向。正值更容易向上生长，负值更容易向下生长，0 为上下均匀。")]
        [Range(-1f, 1f)] public float verticalBias = 0.18f;
        [Tooltip("控制球体生长、球面采样、缩放和旋转的随机种子。相同参数和种子会得到相同的云形。")]
        public int randomSeed = 301;

        [Header("Surface Cloudlets")]
        [Tooltip("每单位球面面积散布的小云团数量。提高会使云更密实，同时增加顶点和透明裁剪开销。")]
        [Range(0.05f, 3f)] public float cloudletDensity = 0.72f;
        [Tooltip("单张小云团卡片的局部尺寸：X 控制宽度，Y 控制高度。每个小云团由 XY、XZ、YZ 三张互相垂直的卡片组成。")]
        public Vector2 cloudletCardSize = new Vector2(1.35f, 1.05f);
        [Tooltip("每个小云团的随机统一缩放范围：X 为最小倍率，Y 为最大倍率。")]
        public Vector2 randomScale = new Vector2(0.72f, 1.28f);
        [Tooltip("小云团相对所属球面的法线偏移。正值向球外移动，负值会嵌入球体内部。")]
        [Range(-0.5f, 0.8f)] public float surfaceOffset = 0.04f;
        [Tooltip("小云团相对球面法线的随机倾斜角度，单位为度。提高会让三交叉卡片方向更凌乱。")]
        [Range(0f, 45f)] public float randomTilt = 14f;

        [Header("Growth Sphere Display")]
        [Tooltip("每个核心球和迭代子球中心都会显示一组三垂直云面片。面片保留小云团卡片的宽高比，最大边等于当前球直径乘以此缩放值；因此父球与子球会按各自半径同比缩放。")]
        [Range(0.1f, 3f)] public float growthSphereCardScale = 1f;

        [Header("Cloud Appearance")]
        [Tooltip("该程序化云使用的材质，通常指定 Stylized Procedural Clouds 材质。")]
        public Material cloudMaterial;
        [Tooltip("云体背光和下部区域使用的暗部颜色。")]
        public Color shadowColor = new Color(0.48f, 0.60f, 0.70f, 1f);
        [Tooltip("云体主要区域使用的基础颜色。")]
        public Color baseColor = new Color(0.78f, 0.86f, 0.92f, 1f);
        [Tooltip("云体受光区域使用的高光颜色。")]
        public Color highlightColor = new Color(1f, 0.98f, 0.92f, 1f);

        [Header("View Cleanup")]
        [Tooltip("云片接近侧视时开始隐藏的正面朝向阈值。提高后侧视面片会更早消失。")]
        [Range(0f, 1f)] public float cardEdgeFadeStart = 0.08f;
        [Tooltip("云片完全恢复显示的正面朝向阈值。必须高于 Card Edge Fade Start；降低可缩短侧视清理过渡。")]
        [Range(0f, 1f)] public float cardEdgeFadeEnd = 0.35f;

        [Header("Blue Noise Mask")]
        [Tooltip("蓝噪声在世界空间中的程序化采样缩放。数值越大，噪声颗粒越小、越密，并且不会随云片尺寸同比放大。")]
        [Range(0.25f, 32f)] public float blueNoiseScale = 4f;
        [Tooltip("蓝噪声与云纹理遮罩的混合强度。0 只使用原云纹理，1 使用云纹理乘以完整蓝噪声。")]
        [Range(0f, 1f)] public float blueNoiseBlend = 1f;
        [Tooltip("云纹理与蓝噪声相乘后的裁剪阈值。提高会减少绘制区域，降低会保留更多噪声细节。")]
        [Range(0f, 1f)] public float alphaCutoff = 0.24f;
        [Tooltip("不同小云团之间的随机明暗变化强度。0 关闭变化，提高后云表面会出现更明显的色块差异。")]
        [Range(0f, 0.5f)] public float colorVariation = 0.10f;
        [Tooltip("整团云的最终亮度倍率。1 保持设定颜色，大于 1 会整体提亮。")]
        [Range(0f, 3f)] public float brightness = 1.05f;

        MeshFilter meshFilter;
        MeshRenderer meshRenderer;
        MaterialPropertyBlock propertyBlock;
        Mesh generatedMesh;

        readonly struct CloudSphere
        {
            public readonly Vector3 center;
            public readonly float radius;

            public CloudSphere(Vector3 center, float radius)
            {
                this.center = center;
                this.radius = radius;
            }
        }

        void OnEnable()
        {
            RebuildCloud();
        }

        void OnValidate()
        {
            coreRadius = Mathf.Max(0.1f, coreRadius);
            childRadiusRatio.x = Mathf.Clamp(childRadiusRatio.x, 0.1f, 1f);
            childRadiusRatio.y = Mathf.Clamp(childRadiusRatio.y, childRadiusRatio.x, 1f);
            growthAxisScale.x = Mathf.Max(0.05f, Mathf.Abs(growthAxisScale.x));
            growthAxisScale.y = Mathf.Max(0.05f, Mathf.Abs(growthAxisScale.y));
            growthAxisScale.z = Mathf.Max(0.05f, Mathf.Abs(growthAxisScale.z));
            cloudletDensity = Mathf.Max(0.01f, cloudletDensity);
            cloudletCardSize.x = Mathf.Max(0.05f, cloudletCardSize.x);
            cloudletCardSize.y = Mathf.Max(0.05f, cloudletCardSize.y);
            randomScale.x = Mathf.Max(0.01f, randomScale.x);
            randomScale.y = Mathf.Max(randomScale.x, randomScale.y);
            growthSphereCardScale = Mathf.Max(0.1f, growthSphereCardScale);
            cardEdgeFadeStart = Mathf.Clamp01(cardEdgeFadeStart);
            cardEdgeFadeEnd = Mathf.Clamp(cardEdgeFadeEnd, cardEdgeFadeStart + 0.001f, 1f);
            blueNoiseScale = Mathf.Max(0.25f, blueNoiseScale);
            blueNoiseBlend = Mathf.Clamp01(blueNoiseBlend);

            if (isActiveAndEnabled)
                RebuildCloud();
        }

        void OnDestroy()
        {
            ReleaseGeneratedMesh();
        }

        [ContextMenu("Rebuild Procedural Cloud")]
        public void RebuildCloud()
        {
            CacheComponents();
            ReleaseGeneratedMesh();

            generatedMesh = CreateCloudMesh();
            generatedMesh.name = name + " Procedural Cloud";
            generatedMesh.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            meshFilter.sharedMesh = generatedMesh;

            meshRenderer.sharedMaterial = cloudMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            ApplyMaterialProperties();
        }

        Mesh CreateCloudMesh()
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var tangents = new List<Vector4>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();
            var triangles = new List<int>();

            Random.State previousRandomState = Random.state;
            Random.InitState(randomSeed);
            try
            {
                List<CloudSphere> spheres = CreateSphereGrowth();
                int cloudletCount = 0;

                for (int sphereIndex = 0; sphereIndex < spheres.Count; sphereIndex++)
                {
                    CloudSphere sphere = spheres[sphereIndex];
                    float surfaceArea = 4f * Mathf.PI * sphere.radius * sphere.radius;
                    int pointCount = Mathf.Max(4, Mathf.RoundToInt(surfaceArea * cloudletDensity));
                    pointCount = Mathf.Min(pointCount, MaximumCloudletCount - cloudletCount);
                    if (pointCount <= 0)
                        break;

                    for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
                    {
                        Vector3 surfaceNormal = Random.onUnitSphere;
                        Vector3 point = sphere.center
                            + surfaceNormal * (sphere.radius + surfaceOffset);
                        float scale = Random.Range(randomScale.x, randomScale.y);
                        float cloudletRandom = Random.value;

                        Quaternion surfaceRotation = Quaternion.FromToRotation(Vector3.up, surfaceNormal);
                        Quaternion spin = Quaternion.AngleAxis(Random.Range(0f, 360f), surfaceNormal);
                        Quaternion tilt = Quaternion.Euler(
                            Random.Range(-randomTilt, randomTilt),
                            Random.Range(-randomTilt, randomTilt),
                            Random.Range(-randomTilt, randomTilt));
                        Quaternion clusterRotation = spin * surfaceRotation * tilt;

                        AddOrthogonalCardCluster(
                            vertices,
                            normals,
                            tangents,
                            uvs,
                            colors,
                            triangles,
                            point,
                            surfaceNormal,
                            clusterRotation,
                            cloudletCardSize * scale,
                            cloudletRandom);
                    }

                    cloudletCount += pointCount;
                }

                // Append growth-sphere cards without consuming more random values, so
                // the existing surface-cloudlet layout stays stable for the same seed.
                for (int sphereIndex = 0; sphereIndex < spheres.Count; sphereIndex++)
                {
                    CloudSphere sphere = spheres[sphereIndex];
                    float cardReferenceSize = Mathf.Max(
                        cloudletCardSize.x,
                        cloudletCardSize.y);
                    Vector2 sphereCardAspect = cloudletCardSize / cardReferenceSize;
                    Vector2 sphereCardSize = sphereCardAspect
                        * (sphere.radius * 2f * growthSphereCardScale);

                    AddOrthogonalCardCluster(
                        vertices,
                        normals,
                        tangents,
                        uvs,
                        colors,
                        triangles,
                        sphere.center,
                        Vector3.up,
                        Quaternion.identity,
                        sphereCardSize,
                        0.5f);
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
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        List<CloudSphere> CreateSphereGrowth()
        {
            var spheres = new List<CloudSphere>(MaximumSphereCount)
            {
                new CloudSphere(cloudCenter, coreRadius)
            };
            var frontier = new List<int> { 0 };

            for (int iteration = 0;
                 iteration < growthIterations && spheres.Count < MaximumSphereCount;
                 iteration++)
            {
                var nextFrontier = new List<int>();
                for (int parentIndex = 0;
                     parentIndex < frontier.Count && spheres.Count < MaximumSphereCount;
                     parentIndex++)
                {
                    CloudSphere parent = spheres[frontier[parentIndex]];
                    for (int childIndex = 0;
                         childIndex < childSpheresPerParent && spheres.Count < MaximumSphereCount;
                         childIndex++)
                    {
                        Vector3 direction = Random.onUnitSphere + Vector3.up * verticalBias;
                        direction = Vector3.Scale(direction, growthAxisScale).normalized;
                        float childRadius = parent.radius
                            * Random.Range(childRadiusRatio.x, childRadiusRatio.y);
                        Vector3 childCenter = parent.center
                            + direction * (parent.radius * childSurfaceDistance);
                        spheres.Add(new CloudSphere(childCenter, childRadius));
                        nextFrontier.Add(spheres.Count - 1);
                    }
                }

                frontier = nextFrontier;
                if (frontier.Count == 0)
                    break;
            }

            return spheres;
        }

        static void AddOrthogonalCardCluster(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector4> tangents,
            List<Vector2> uvs,
            List<Color> colors,
            List<int> triangles,
            Vector3 center,
            Vector3 surfaceNormal,
            Quaternion clusterRotation,
            Vector2 size,
            float randomValue)
        {
            for (int cardIndex = 0; cardIndex < 3; cardIndex++)
            {
                Quaternion orthogonalCardRotation;
                if (cardIndex == 1)
                    orthogonalCardRotation = Quaternion.AngleAxis(90f, Vector3.right);
                else if (cardIndex == 2)
                    orthogonalCardRotation = Quaternion.AngleAxis(90f, Vector3.up);
                else
                    orthogonalCardRotation = Quaternion.identity;

                AddCenteredCard(
                    vertices,
                    normals,
                    tangents,
                    uvs,
                    colors,
                    triangles,
                    center,
                    surfaceNormal,
                    clusterRotation * orthogonalCardRotation,
                    size,
                    randomValue);
            }
        }

        static void AddCenteredCard(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector4> tangents,
            List<Vector2> uvs,
            List<Color> colors,
            List<int> triangles,
            Vector3 center,
            Vector3 surfaceNormal,
            Quaternion rotation,
            Vector2 size,
            float randomValue)
        {
            int firstVertex = vertices.Count;
            Vector2 halfSize = size * 0.5f;
            Vector3[] corners =
            {
                new Vector3(-halfSize.x, -halfSize.y, 0f),
                new Vector3(halfSize.x, -halfSize.y, 0f),
                new Vector3(halfSize.x, halfSize.y, 0f),
                new Vector3(-halfSize.x, halfSize.y, 0f)
            };
            Vector2[] cardUvs =
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };
            Vector3 cardNormal = rotation * Vector3.forward;

            for (int cornerIndex = 0; cornerIndex < 4; cornerIndex++)
            {
                vertices.Add(center + rotation * corners[cornerIndex]);
                normals.Add(surfaceNormal);
                tangents.Add(new Vector4(cardNormal.x, cardNormal.y, cardNormal.z, 1f));
                uvs.Add(cardUvs[cornerIndex]);
                colors.Add(new Color(randomValue, randomValue, randomValue, 1f));
            }

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
            if (meshRenderer == null)
                return;

            propertyBlock.Clear();
            propertyBlock.SetColor("_CloudShadowColor", shadowColor);
            propertyBlock.SetColor("_CloudBaseColor", baseColor);
            propertyBlock.SetColor("_CloudHighlightColor", highlightColor);
            propertyBlock.SetFloat("_AlphaCutoff", alphaCutoff);
            propertyBlock.SetFloat("_BlueNoiseScale", blueNoiseScale);
            propertyBlock.SetFloat("_BlueNoiseBlend", blueNoiseBlend);
            propertyBlock.SetFloat("_CardEdgeFadeStart", cardEdgeFadeStart);
            propertyBlock.SetFloat("_CardEdgeFadeEnd", cardEdgeFadeEnd);
            propertyBlock.SetFloat("_ColorVariation", colorVariation);
            propertyBlock.SetFloat("_Brightness", brightness);
            meshRenderer.SetPropertyBlock(propertyBlock);
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
