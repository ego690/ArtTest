using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SisyphusPrototype
{
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class SisyphusSpiralRamp : MonoBehaviour
    {
        [Header("Spiral")]
        [Min(0.05f)] public float turns = 2.25f;
        [Min(0.25f)] public float radius = 8f;
        [Min(0.5f)] public float width = 4.5f;
        [Min(0.1f)] public float rise = 12f;
        [Min(8)] public int segments = 240;
        public float startAngleDegrees = -90f;
        public bool clockwise;

        [Header("U-shaped Cross Section")]
        [Min(0f)] public float wallHeight = 1.8f;
        [Range(0f, 0.9f)] public float flatBottomFraction = 0.38f;
        [Min(4)] public int crossSectionSegments = 12;
        [Min(0.05f)] public float thickness = 0.35f;
        [Min(0.1f)] public float uvMetersPerTile = 4f;

        [Header("Summit Exit")]
        [Tooltip("Flattens the raised radial-outside edge near the summit so the boulder can leave the ramp.")]
        public bool leaveOuterWallOpenAtTop = true;
        [Range(0f, 1f)] public float topExitFraction = 0.92f;

        [Header("Static Collision")]
        public bool generateCollider = true;
        [Tooltip("Uses a separately generated, lower resolution closed mesh for the static MeshCollider.")]
        public bool useSimplifiedCollider = true;
        [Min(8)] public int colliderSegments = 96;
        [Min(4)] public int colliderCrossSectionSegments = 6;

        [Header("Rendering")]
        [Tooltip("Optional. When empty, the MeshRenderer keeps its existing material.")]
        public Material rampMaterial;

        MeshFilter meshFilter;
        MeshRenderer meshRenderer;
        MeshCollider meshCollider;
        Mesh visualMesh;
        Mesh collisionMesh;

        public float ApproximatePathLength
        {
            get
            {
                float circumferenceDistance = Mathf.PI * 2f * radius * turns;
                return Mathf.Sqrt(circumferenceDistance * circumferenceDistance + rise * rise);
            }
        }

        void OnEnable()
        {
            Rebuild();
        }

        void OnValidate()
        {
            ClampParameters();
            if (isActiveAndEnabled)
                Rebuild();
        }

        void OnDestroy()
        {
            if (meshFilter != null && meshFilter.sharedMesh == visualMesh)
                meshFilter.sharedMesh = null;

            if (meshCollider != null &&
                (meshCollider.sharedMesh == visualMesh || meshCollider.sharedMesh == collisionMesh))
            {
                meshCollider.sharedMesh = null;
            }

            DestroyGeneratedMesh(ref visualMesh);
            DestroyGeneratedMesh(ref collisionMesh);
        }

        [ContextMenu("Rebuild Ramp")]
        public void Rebuild()
        {
            ClampParameters();
            CacheComponents();

            EnsureMesh(ref visualMesh, "Sisyphus Spiral Ramp (Visual)");
            BuildMesh(visualMesh, segments, crossSectionSegments);
            meshFilter.sharedMesh = visualMesh;

            if (rampMaterial != null)
                meshRenderer.sharedMaterial = rampMaterial;

            meshCollider.sharedMesh = null;
            meshCollider.convex = false;
            meshCollider.enabled = generateCollider;
            meshCollider.cookingOptions =
                MeshColliderCookingOptions.CookForFasterSimulation |
                MeshColliderCookingOptions.EnableMeshCleaning |
                MeshColliderCookingOptions.WeldColocatedVertices |
                MeshColliderCookingOptions.UseFastMidphase;

            if (!generateCollider)
                return;

            if (useSimplifiedCollider)
            {
                EnsureMesh(ref collisionMesh, "Sisyphus Spiral Ramp (Collision)");
                BuildMesh(collisionMesh, colliderSegments, colliderCrossSectionSegments);
                meshCollider.sharedMesh = collisionMesh;
            }
            else
            {
                meshCollider.sharedMesh = visualMesh;
            }
        }

        /// <summary>
        /// Returns the world-space center of the flat running surface, from foot (0) to summit (1).
        /// </summary>
        public Vector3 EvaluateCenter(float normalizedDistance)
        {
            EvaluateLocalFrame(
                Mathf.Clamp01(normalizedDistance),
                out Vector3 localCenter,
                out _,
                out _,
                out _,
                out _);
            return transform.TransformPoint(localCenter);
        }

        /// <summary>
        /// Returns a world-space route frame. Outward always points radially away from the spiral axis.
        /// </summary>
        public void EvaluateFrame(
            float normalizedDistance,
            out Vector3 center,
            out Vector3 tangent,
            out Vector3 normal,
            out Vector3 outward)
        {
            EvaluateLocalFrame(
                Mathf.Clamp01(normalizedDistance),
                out Vector3 localCenter,
                out Vector3 localTangent,
                out Vector3 localNormal,
                out _,
                out Vector3 localOutward);

            center = transform.TransformPoint(localCenter);
            tangent = transform.TransformVector(localTangent).normalized;
            normal = transform.worldToLocalMatrix.transpose.MultiplyVector(localNormal).normalized;
            outward = transform.TransformVector(localOutward).normalized;
        }

        /// <summary>
        /// Returns a point on the visible running surface. Lateral offset is in local-space metres.
        /// </summary>
        public Vector3 EvaluateSurface(float normalizedDistance, float lateralOffset)
        {
            float t = Mathf.Clamp01(normalizedDistance);
            EvaluateLocalFrame(
                t,
                out Vector3 center,
                out _,
                out Vector3 normal,
                out Vector3 right,
                out Vector3 outward);

            float clampedOffset = Mathf.Clamp(lateralOffset, -width * 0.5f, width * 0.5f);
            float height = EvaluateCrossSectionHeight(clampedOffset, t, right, outward);
            return transform.TransformPoint(center + right * clampedOffset + normal * height);
        }

        void BuildMesh(Mesh mesh, int longitudinalSegments, int transverseSegments)
        {
            longitudinalSegments = Mathf.Max(8, longitudinalSegments);
            transverseSegments = Mathf.Max(4, transverseSegments);

            int ringCount = longitudinalSegments + 1;
            int columnCount = transverseSegments + 1;
            var topPositions = new Vector3[ringCount * columnCount];
            var bottomPositions = new Vector3[ringCount * columnCount];

            for (int ring = 0; ring < ringCount; ring++)
            {
                float t = ring / (float)longitudinalSegments;
                EvaluateLocalFrame(
                    t,
                    out Vector3 center,
                    out Vector3 tangent,
                    out Vector3 normal,
                    out Vector3 right,
                    out Vector3 outward);

                for (int column = 0; column < columnCount; column++)
                {
                    float across = column / (float)transverseSegments;
                    float lateralOffset = Mathf.Lerp(-width * 0.5f, width * 0.5f, across);
                    float height = EvaluateCrossSectionHeight(lateralOffset, t, right, outward);
                    Vector3 top = center + right * lateralOffset + normal * height;
                    int index = ring * columnCount + column;
                    topPositions[index] = top;
                    bottomPositions[index] = top - normal * thickness;
                }
            }

            int gridVertexCount = ringCount * columnCount;
            int sideVertexCount = ringCount * 2 * 2;
            int capVertexCount = transverseSegments * 4 * 2;
            var vertices = new List<Vector3>(gridVertexCount * 2 + sideVertexCount + capVertexCount);
            var uvs = new List<Vector2>(vertices.Capacity);
            var triangles = new List<int>(
                longitudinalSegments * transverseSegments * 12 +
                longitudinalSegments * 12 +
                transverseSegments * 12);

            float uvLength = ApproximatePathLength / uvMetersPerTile;
            AddSurfaceGrid(
                vertices,
                uvs,
                triangles,
                topPositions,
                ringCount,
                columnCount,
                uvLength,
                reverseWinding: false);
            AddSurfaceGrid(
                vertices,
                uvs,
                triangles,
                bottomPositions,
                ringCount,
                columnCount,
                uvLength,
                reverseWinding: true);

            AddEdgeStrip(
                vertices,
                uvs,
                triangles,
                topPositions,
                bottomPositions,
                columnCount,
                longitudinalSegments,
                column: 0,
                uvLength,
                isRightEdge: false);
            AddEdgeStrip(
                vertices,
                uvs,
                triangles,
                topPositions,
                bottomPositions,
                columnCount,
                longitudinalSegments,
                column: columnCount - 1,
                uvLength,
                isRightEdge: true);

            AddEndCap(
                vertices,
                uvs,
                triangles,
                topPositions,
                bottomPositions,
                columnCount,
                ring: 0,
                reverseWinding: false);
            AddEndCap(
                vertices,
                uvs,
                triangles,
                topPositions,
                bottomPositions,
                columnCount,
                ring: ringCount - 1,
                reverseWinding: true);

            mesh.Clear();
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        float EvaluateCrossSectionHeight(
            float lateralOffset,
            float normalizedDistance,
            Vector3 right,
            Vector3 outward)
        {
            float halfWidth = width * 0.5f;
            float normalizedLateral = Mathf.Abs(lateralOffset) / halfWidth;
            if (normalizedLateral <= flatBottomFraction)
                return 0f;

            bool isOuterSide = lateralOffset * Vector3.Dot(right, outward) > 0f;
            if (leaveOuterWallOpenAtTop &&
                normalizedDistance >= topExitFraction &&
                isOuterSide)
            {
                return 0f;
            }

            float curvedFraction = Mathf.InverseLerp(flatBottomFraction, 1f, normalizedLateral);
            float semicircleProfile = 1f - Mathf.Sqrt(Mathf.Max(0f, 1f - curvedFraction * curvedFraction));
            return semicircleProfile * wallHeight;
        }

        void EvaluateLocalFrame(
            float normalizedDistance,
            out Vector3 center,
            out Vector3 tangent,
            out Vector3 normal,
            out Vector3 right,
            out Vector3 outward)
        {
            float direction = clockwise ? -1f : 1f;
            float angleTravel = Mathf.PI * 2f * turns * direction;
            float angle = startAngleDegrees * Mathf.Deg2Rad + angleTravel * normalizedDistance;
            float cosine = Mathf.Cos(angle);
            float sine = Mathf.Sin(angle);

            center = new Vector3(cosine * radius, rise * normalizedDistance, sine * radius);
            outward = new Vector3(cosine, 0f, sine);

            Vector3 derivative = new Vector3(
                -sine * radius * angleTravel,
                rise,
                cosine * radius * angleTravel);
            tangent = derivative.normalized;

            normal = Vector3.ProjectOnPlane(Vector3.up, tangent).normalized;
            if (normal.sqrMagnitude < 0.0001f)
                normal = Vector3.ProjectOnPlane(outward, tangent).normalized;

            right = Vector3.Cross(normal, tangent).normalized;
        }

        static void AddSurfaceGrid(
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<int> triangles,
            Vector3[] positions,
            int ringCount,
            int columnCount,
            float uvLength,
            bool reverseWinding)
        {
            int vertexStart = vertices.Count;
            for (int ring = 0; ring < ringCount; ring++)
            {
                float along = ring / (float)(ringCount - 1);
                for (int column = 0; column < columnCount; column++)
                {
                    vertices.Add(positions[ring * columnCount + column]);
                    uvs.Add(new Vector2(column / (float)(columnCount - 1), along * uvLength));
                }
            }

            for (int ring = 0; ring < ringCount - 1; ring++)
            {
                for (int column = 0; column < columnCount - 1; column++)
                {
                    int a = vertexStart + ring * columnCount + column;
                    int b = vertexStart + (ring + 1) * columnCount + column;
                    int c = a + 1;
                    int d = b + 1;
                    AddQuadTriangles(triangles, a, b, c, d, reverseWinding);
                }
            }
        }

        static void AddEdgeStrip(
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<int> triangles,
            Vector3[] topPositions,
            Vector3[] bottomPositions,
            int columnCount,
            int longitudinalSegments,
            int column,
            float uvLength,
            bool isRightEdge)
        {
            int vertexStart = vertices.Count;
            for (int ring = 0; ring <= longitudinalSegments; ring++)
            {
                int source = ring * columnCount + column;
                float along = ring / (float)longitudinalSegments;
                vertices.Add(topPositions[source]);
                vertices.Add(bottomPositions[source]);
                uvs.Add(new Vector2(0f, along * uvLength));
                uvs.Add(new Vector2(1f, along * uvLength));
            }

            for (int ring = 0; ring < longitudinalSegments; ring++)
            {
                int topCurrent = vertexStart + ring * 2;
                int bottomCurrent = topCurrent + 1;
                int topNext = topCurrent + 2;
                int bottomNext = topCurrent + 3;

                if (isRightEdge)
                {
                    triangles.Add(topCurrent);
                    triangles.Add(topNext);
                    triangles.Add(bottomCurrent);
                    triangles.Add(bottomCurrent);
                    triangles.Add(topNext);
                    triangles.Add(bottomNext);
                }
                else
                {
                    triangles.Add(topCurrent);
                    triangles.Add(bottomCurrent);
                    triangles.Add(topNext);
                    triangles.Add(bottomCurrent);
                    triangles.Add(bottomNext);
                    triangles.Add(topNext);
                }
            }
        }

        static void AddEndCap(
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<int> triangles,
            Vector3[] topPositions,
            Vector3[] bottomPositions,
            int columnCount,
            int ring,
            bool reverseWinding)
        {
            int rowStart = ring * columnCount;
            for (int column = 0; column < columnCount - 1; column++)
            {
                int vertexStart = vertices.Count;
                vertices.Add(topPositions[rowStart + column]);
                vertices.Add(topPositions[rowStart + column + 1]);
                vertices.Add(bottomPositions[rowStart + column]);
                vertices.Add(bottomPositions[rowStart + column + 1]);
                uvs.Add(new Vector2(0f, 1f));
                uvs.Add(new Vector2(1f, 1f));
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(1f, 0f));

                if (reverseWinding)
                {
                    triangles.Add(vertexStart);
                    triangles.Add(vertexStart + 2);
                    triangles.Add(vertexStart + 1);
                    triangles.Add(vertexStart + 2);
                    triangles.Add(vertexStart + 3);
                    triangles.Add(vertexStart + 1);
                }
                else
                {
                    triangles.Add(vertexStart);
                    triangles.Add(vertexStart + 1);
                    triangles.Add(vertexStart + 2);
                    triangles.Add(vertexStart + 2);
                    triangles.Add(vertexStart + 1);
                    triangles.Add(vertexStart + 3);
                }
            }
        }

        static void AddQuadTriangles(
            List<int> triangles,
            int a,
            int b,
            int c,
            int d,
            bool reverseWinding)
        {
            if (reverseWinding)
            {
                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(b);
                triangles.Add(c);
                triangles.Add(d);
                triangles.Add(b);
            }
            else
            {
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
                triangles.Add(c);
                triangles.Add(b);
                triangles.Add(d);
            }
        }

        void CacheComponents()
        {
            if (meshFilter == null)
                meshFilter = GetComponent<MeshFilter>();
            if (meshRenderer == null)
                meshRenderer = GetComponent<MeshRenderer>();
            if (meshCollider == null)
                meshCollider = GetComponent<MeshCollider>();
        }

        void ClampParameters()
        {
            turns = Mathf.Max(0.05f, turns);
            radius = Mathf.Max(0.25f, radius);
            width = Mathf.Max(0.5f, width);
            rise = Mathf.Max(0.1f, rise);
            segments = Mathf.Clamp(segments, 8, 4096);
            wallHeight = Mathf.Max(0f, wallHeight);
            flatBottomFraction = Mathf.Clamp(flatBottomFraction, 0f, 0.9f);
            crossSectionSegments = Mathf.Clamp(crossSectionSegments, 4, 64);
            thickness = Mathf.Max(0.05f, thickness);
            uvMetersPerTile = Mathf.Max(0.1f, uvMetersPerTile);
            topExitFraction = Mathf.Clamp01(topExitFraction);
            colliderSegments = Mathf.Clamp(colliderSegments, 8, 4096);
            colliderCrossSectionSegments = Mathf.Clamp(colliderCrossSectionSegments, 4, 64);
        }

        static void EnsureMesh(ref Mesh mesh, string meshName)
        {
            if (mesh != null)
                return;

            mesh = new Mesh
            {
                name = meshName,
                hideFlags = HideFlags.DontSave
            };
        }

        static void DestroyGeneratedMesh(ref Mesh mesh)
        {
            if (mesh == null)
                return;

            if (Application.isPlaying)
                Destroy(mesh);
            else
                DestroyImmediate(mesh);

            mesh = null;
        }

    }
}
