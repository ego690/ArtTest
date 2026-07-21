using UnityEngine;
using UnityEngine.Rendering;

namespace ShortHikeStylePrototype
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    public sealed class ShortHikeWaterSurfaceGrid : MonoBehaviour
    {
        [SerializeField, Range(8, 128)] int resolution = 128;
        [SerializeField, Min(0.1f)] float size = 10f;
        [SerializeField, HideInInspector] Mesh sourceMesh;

        Mesh generatedMesh;

        void OnEnable()
        {
            Rebuild();
        }

        void OnValidate()
        {
            resolution = Mathf.Clamp(resolution, 8, 128);
            size = Mathf.Max(0.1f, size);
            Rebuild();
        }

        void OnDisable()
        {
            RestoreSourceMesh();
            DestroyGeneratedMesh();
        }

        void OnDestroy()
        {
            RestoreSourceMesh();
            DestroyGeneratedMesh();
        }

        void Rebuild()
        {
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null)
                return;

            if (sourceMesh == null && meshFilter.sharedMesh != generatedMesh)
                sourceMesh = meshFilter.sharedMesh;

            DestroyGeneratedMesh();
            generatedMesh = BuildGrid(resolution, size);
            meshFilter.sharedMesh = generatedMesh;
        }

        void RestoreSourceMesh()
        {
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh == generatedMesh)
                meshFilter.sharedMesh = sourceMesh;
        }

        void DestroyGeneratedMesh()
        {
            if (generatedMesh == null)
                return;

            if (Application.isPlaying)
                Destroy(generatedMesh);
            else
                DestroyImmediate(generatedMesh);

            generatedMesh = null;
        }

        static Mesh BuildGrid(int gridResolution, float gridSize)
        {
            int rowLength = gridResolution + 1;
            int vertexCount = rowLength * rowLength;
            var vertices = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var tangents = new Vector4[vertexCount];
            var uvs = new Vector2[vertexCount];
            var triangles = new int[gridResolution * gridResolution * 6];
            float halfSize = gridSize * 0.5f;

            for (int z = 0; z <= gridResolution; z++)
            {
                for (int x = 0; x <= gridResolution; x++)
                {
                    int index = z * rowLength + x;
                    Vector2 uv = new Vector2(x / (float)gridResolution, z / (float)gridResolution);
                    vertices[index] = new Vector3(
                        Mathf.Lerp(-halfSize, halfSize, uv.x),
                        0f,
                        Mathf.Lerp(-halfSize, halfSize, uv.y));
                    normals[index] = Vector3.up;
                    tangents[index] = new Vector4(1f, 0f, 0f, -1f);
                    uvs[index] = uv;
                }
            }

            int triangleIndex = 0;
            for (int z = 0; z < gridResolution; z++)
            {
                for (int x = 0; x < gridResolution; x++)
                {
                    int a = z * rowLength + x;
                    int b = a + 1;
                    int c = a + rowLength;
                    int d = c + 1;

                    triangles[triangleIndex++] = a;
                    triangles[triangleIndex++] = c;
                    triangles[triangleIndex++] = b;
                    triangles[triangleIndex++] = b;
                    triangles[triangleIndex++] = c;
                    triangles[triangleIndex++] = d;
                }
            }

            var mesh = new Mesh
            {
                name = "Short Hike Dynamic Water Grid",
                hideFlags = HideFlags.HideAndDontSave,
                indexFormat = vertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16,
                vertices = vertices,
                normals = normals,
                tangents = tangents,
                uv = uvs,
                triangles = triangles,
                bounds = new Bounds(Vector3.zero, new Vector3(gridSize, 2f, gridSize))
            };
            return mesh;
        }
    }
}
