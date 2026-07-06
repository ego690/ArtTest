using System.Collections.Generic;
using System.IO;
using ShortHikeStylePrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ShortHikeStylePrototype.Editor
{
    public static class ShortHikeStylePrototypeSetup
    {
        const string Root = "Assets/ShortHikeStylePrototype";
        const string MaterialsRoot = Root + "/Materials";
        const string MeshesRoot = Root + "/Meshes";
        const string ScenesRoot = Root + "/Scenes";
        const string ScenePath = ScenesRoot + "/ShortHikeStyleDemo.unity";
        const string RebuildMarkerPath = "Temp/ShortHikeStylePrototypeRebuild.flag";

        static readonly Color SkyColor = new Color(0.58f, 0.78f, 0.88f, 1f);

        [InitializeOnLoadMethod]
        static void RebuildQueuedDemoScene()
        {
            EditorApplication.delayCall += TryRunQueuedRebuild;
        }

        static void TryRunQueuedRebuild()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryRunQueuedRebuild;
                return;
            }

            if (!File.Exists(RebuildMarkerPath))
                return;

            File.Delete(RebuildMarkerPath);
            BuildDemoScene();
        }

        [MenuItem("Tools/Short Hike Style Prototype/Build Demo Scene")]
        public static void BuildDemoScene()
        {
            EnsureFolders();

            Material grass = CreateToonMaterial("M_SH_Grass", new Color(0.48f, 0.68f, 0.30f), new Color(0.24f, 0.42f, 0.22f), new Color(0.92f, 0.86f, 0.56f));
            Material sand = CreateToonMaterial("M_SH_Sand", new Color(0.83f, 0.72f, 0.43f), new Color(0.54f, 0.45f, 0.28f), new Color(1.0f, 0.92f, 0.62f));
            Material rock = CreateToonMaterial("M_SH_Rock", new Color(0.56f, 0.56f, 0.46f), new Color(0.34f, 0.38f, 0.34f), new Color(0.86f, 0.82f, 0.66f));
            Material leaves = CreateToonMaterial("M_SH_Leaves", new Color(0.24f, 0.58f, 0.35f), new Color(0.12f, 0.35f, 0.28f), new Color(0.76f, 0.78f, 0.35f));
            Material trunk = CreateToonMaterial("M_SH_Trunk", new Color(0.55f, 0.34f, 0.22f), new Color(0.30f, 0.22f, 0.16f), new Color(0.86f, 0.62f, 0.40f));
            Material cloth = CreateToonMaterial("M_SH_CharacterCoat", new Color(0.88f, 0.34f, 0.22f), new Color(0.52f, 0.19f, 0.16f), new Color(1.0f, 0.72f, 0.44f));
            Material body = CreateToonMaterial("M_SH_CharacterBody", new Color(0.97f, 0.78f, 0.46f), new Color(0.56f, 0.38f, 0.24f), new Color(1.0f, 0.94f, 0.65f));
            Material water = CreateWaterMaterial();
            Mesh islandMesh = CreateIslandMesh();

            CreateScene(islandMesh, grass, sand, rock, leaves, trunk, cloth, body, water);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Short Hike style prototype scene built at " + ScenePath);
        }

        static void EnsureFolders()
        {
            foreach (string folder in new[] { Root, MaterialsRoot, MeshesRoot, ScenesRoot })
            {
                if (AssetDatabase.IsValidFolder(folder))
                    continue;

                string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
                string name = Path.GetFileName(folder);
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        static Material CreateToonMaterial(string name, Color baseColor, Color shadowColor, Color highlightColor)
        {
            Shader shader = Shader.Find("ShortHikeStylePrototype/Toon");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Lit");

            string path = MaterialsRoot + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.SetColor("_BaseColor", baseColor);
            material.SetColor("_ShadowColor", shadowColor);
            material.SetColor("_HighlightColor", highlightColor);
            material.SetFloat("_BandSoftness", 0.10f);
            material.SetFloat("_VertexColorStrength", 1f);
            material.SetColor("_FogColor", SkyColor);
            material.SetFloat("_FogStart", 12f);
            material.SetFloat("_FogEnd", 34f);
            material.SetFloat("_Smoothness", 0.05f);
            material.SetFloat("_Metallic", 0f);
            material.SetOverrideTag("RenderType", "Opaque");
            material.renderQueue = (int)RenderQueue.Geometry;
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material CreateWaterMaterial()
        {
            Shader shader = Shader.Find("ShortHikeStylePrototype/Water");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");

            string path = MaterialsRoot + "/M_SH_Water.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.SetColor("_DeepColor", new Color(0.10f, 0.45f, 0.68f, 0.88f));
            material.SetColor("_ShallowColor", new Color(0.36f, 0.82f, 0.78f, 0.80f));
            material.SetColor("_FoamColor", new Color(0.84f, 0.94f, 0.82f, 1f));
            material.SetFloat("_WaveScale", 4.3f);
            material.SetFloat("_WaveSpeed", 0.34f);
            material.SetFloat("_WaveStrength", 0.58f);
            material.SetFloat("_FoamScale", 4.8f);
            material.SetFloat("_FoamAmount", 0.22f);
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        static Mesh CreateIslandMesh()
        {
            const int resolution = 30;
            const float size = 14f;
            const float half = size * 0.5f;
            var vertices = new List<Vector3>((resolution + 1) * (resolution + 1));
            var normals = new List<Vector3>((resolution + 1) * (resolution + 1));
            var colors = new List<Color>((resolution + 1) * (resolution + 1));
            var uvs = new List<Vector2>((resolution + 1) * (resolution + 1));
            var triangles = new List<int>(resolution * resolution * 6);

            for (int z = 0; z <= resolution; z++)
            {
                for (int x = 0; x <= resolution; x++)
                {
                    Vector2 uv = new Vector2(x / (float)resolution, z / (float)resolution);
                    float px = Mathf.Lerp(-half, half, uv.x);
                    float pz = Mathf.Lerp(-half, half, uv.y);
                    float height = IslandHeight(px, pz);
                    vertices.Add(new Vector3(px, height, pz));
                    uvs.Add(uv);
                    colors.Add(VertexTint(height, px, pz));
                    normals.Add(Vector3.up);
                }
            }

            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int i = z * (resolution + 1) + x;
                    triangles.Add(i);
                    triangles.Add(i + resolution + 1);
                    triangles.Add(i + 1);
                    triangles.Add(i + 1);
                    triangles.Add(i + resolution + 1);
                    triangles.Add(i + resolution + 2);
                }
            }

            var mesh = new Mesh
            {
                name = "MESH_ShortHike_LowPolyIsland",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
            };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            string path = MeshesRoot + "/MESH_ShortHike_LowPolyIsland.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            EditorUtility.CopySerialized(mesh, existing);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        static void CreateScene(
            Mesh islandMesh,
            Material grass,
            Material sand,
            Material rock,
            Material leaves,
            Material trunk,
            Material cloth,
            Material body,
            Material water)
        {
            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SetActiveScene(scene);
            scene.name = "ShortHikeStyleDemo";

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 38f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 80f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = SkyColor;
            camera.allowHDR = false;
            camera.allowMSAA = false;

            var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = false;
            cameraData.antialiasing = AntialiasingMode.None;
            var pixelCamera = cameraObject.AddComponent<ShortHikePixelCamera>();
            Shader edgeComposite = Shader.Find("Hidden/ShortHikeStylePrototype/LowResEdgeComposite");
            if (edgeComposite != null)
            {
                var pixelCameraSerialized = new SerializedObject(pixelCamera);
                pixelCameraSerialized.FindProperty("edgeCompositeShader").objectReferenceValue = edgeComposite;
                pixelCameraSerialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var orbit = cameraObject.AddComponent<ShortHikeOrbitCamera>();

            GameObject worldRoot = new GameObject("Short Hike Style Demo");
            GameObject island = new GameObject("Low Resolution Friendly Island");
            island.transform.SetParent(worldRoot.transform, false);
            island.AddComponent<MeshFilter>().sharedMesh = islandMesh;
            island.AddComponent<MeshRenderer>().sharedMaterial = grass;

            GameObject sandRing = CreateCylinder("Soft Sand Rim", new Vector3(0f, -0.08f, 0f), new Vector3(7.1f, 0.08f, 7.1f), sand, 32);
            sandRing.transform.SetParent(worldRoot.transform, true);

            GameObject waterPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            waterPlane.name = "Flat Graphic Ocean";
            waterPlane.transform.SetParent(worldRoot.transform, false);
            waterPlane.transform.position = new Vector3(0f, -0.16f, 0f);
            waterPlane.transform.localScale = new Vector3(5.2f, 1f, 5.2f);
            waterPlane.GetComponent<MeshRenderer>().sharedMaterial = water;
            Object.DestroyImmediate(waterPlane.GetComponent<Collider>());

            CreateTreeCluster(worldRoot.transform, leaves, trunk);
            CreateRockCluster(worldRoot.transform, rock);
            CreateCharacter(worldRoot.transform, body, cloth, out Transform character);
            orbit.SetTarget(character);

            var motion = worldRoot.AddComponent<ShortHikeDemoMotion>();
            var motionSerialized = new SerializedObject(motion);
            motionSerialized.FindProperty("character").objectReferenceValue = character;
            motionSerialized.FindProperty("waterMaterial").objectReferenceValue = water;
            motionSerialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject sunObject = new GameObject("Warm Directional Sun");
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1.0f, 0.88f, 0.68f);
            sun.intensity = 1.45f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.38f;
            sunObject.transform.rotation = Quaternion.Euler(48f, -38f, 12f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.64f, 0.74f, 0.72f);
            RenderSettings.fog = false;

            cameraObject.transform.position = new Vector3(-8.8f, 7.6f, -9.6f);
            cameraObject.transform.LookAt(new Vector3(0f, 0.65f, 0f));

            AddSceneToBuild(ScenePath);
            EditorSceneManager.SaveScene(scene, ScenePath);

            if (previousActiveScene.IsValid())
                EditorSceneManager.SetActiveScene(previousActiveScene);

            EditorSceneManager.CloseScene(scene, true);
        }

        static void CreateTreeCluster(Transform parent, Material leaves, Material trunk)
        {
            Vector3[] positions =
            {
                new Vector3(-3.4f, 0f, 1.0f),
                new Vector3(-2.4f, 0f, 2.5f),
                new Vector3(2.4f, 0f, -1.7f),
                new Vector3(3.1f, 0f, 0.8f),
                new Vector3(0.6f, 0f, 3.1f),
                new Vector3(-0.7f, 0f, -3.0f)
            };

            for (int i = 0; i < positions.Length; i++)
                CreateTree("Chunky Pine " + (i + 1), parent, positions[i], 0.82f + i * 0.035f, leaves, trunk);
        }

        static void CreateTree(string name, Transform parent, Vector3 position, float scale, Material leaves, Material trunk)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(position.x, IslandHeight(position.x, position.z), position.z);
            root.transform.localScale = Vector3.one * scale;

            GameObject trunkObject = CreateCylinder("Trunk", new Vector3(0f, 0.48f, 0f), new Vector3(0.32f, 0.96f, 0.32f), trunk, 7);
            trunkObject.transform.SetParent(root.transform, false);

            GameObject lower = CreateCone("Lower Leaves", new Vector3(0f, 1.12f, 0f), new Vector3(1.15f, 1.15f, 1.15f), leaves, 8);
            lower.transform.SetParent(root.transform, false);

            GameObject upper = CreateCone("Upper Leaves", new Vector3(0f, 1.82f, 0f), new Vector3(0.82f, 0.92f, 0.82f), leaves, 8);
            upper.transform.SetParent(root.transform, false);
        }

        static void CreateRockCluster(Transform parent, Material rock)
        {
            Vector3[] positions =
            {
                new Vector3(-4.2f, 0f, -1.4f),
                new Vector3(-3.7f, 0f, -2.0f),
                new Vector3(1.4f, 0f, 2.4f),
                new Vector3(2.0f, 0f, 2.7f),
                new Vector3(3.6f, 0f, -3.0f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject rockObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                rockObject.name = "Soft Low Poly Rock " + (i + 1);
                rockObject.transform.SetParent(parent, false);
                float y = IslandHeight(positions[i].x, positions[i].z) + 0.18f;
                rockObject.transform.position = new Vector3(positions[i].x, y, positions[i].z);
                rockObject.transform.localScale = new Vector3(0.7f + 0.13f * i, 0.36f + 0.04f * i, 0.48f + 0.08f * i);
                rockObject.transform.rotation = Quaternion.Euler(0f, i * 27f, 0f);
                rockObject.GetComponent<MeshRenderer>().sharedMaterial = rock;
                Object.DestroyImmediate(rockObject.GetComponent<Collider>());
            }
        }

        static void CreateCharacter(Transform parent, Material body, Material cloth, out Transform character)
        {
            GameObject root = new GameObject("Tiny Hiker Readability Test");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(0.15f, IslandHeight(0.15f, -0.45f) + 0.62f, -0.45f);
            character = root.transform;

            GameObject torso = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            torso.name = "Warm Coat Body";
            torso.transform.SetParent(root.transform, false);
            torso.transform.localPosition = Vector3.zero;
            torso.transform.localScale = new Vector3(0.42f, 0.56f, 0.42f);
            torso.GetComponent<MeshRenderer>().sharedMaterial = cloth;
            Object.DestroyImmediate(torso.GetComponent<Collider>());

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Round Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 0.73f, 0f);
            head.transform.localScale = new Vector3(0.36f, 0.34f, 0.36f);
            head.GetComponent<MeshRenderer>().sharedMaterial = body;
            Object.DestroyImmediate(head.GetComponent<Collider>());

            GameObject beak = CreateCone("Pointed Beak", new Vector3(0f, 0.73f, -0.33f), new Vector3(0.18f, 0.28f, 0.18f), body, 6);
            beak.transform.SetParent(root.transform, false);
            beak.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            GameObject hat = CreateCylinder("Little Hat", new Vector3(0f, 1.02f, 0f), new Vector3(0.44f, 0.16f, 0.44f), cloth, 12);
            hat.transform.SetParent(root.transform, false);
        }

        static GameObject CreateCylinder(string name, Vector3 position, Vector3 scale, Material material, int sides)
        {
            Mesh mesh = CreateCylinderMesh(name + " Mesh", sides);
            GameObject obj = new GameObject(name);
            obj.transform.localPosition = position;
            obj.transform.localScale = scale;
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            obj.AddComponent<MeshRenderer>().sharedMaterial = material;
            return obj;
        }

        static GameObject CreateCone(string name, Vector3 position, Vector3 scale, Material material, int sides)
        {
            Mesh mesh = CreateConeMesh(name + " Mesh", sides);
            GameObject obj = new GameObject(name);
            obj.transform.localPosition = position;
            obj.transform.localScale = scale;
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            obj.AddComponent<MeshRenderer>().sharedMaterial = material;
            return obj;
        }

        static Mesh CreateCylinderMesh(string name, int sides)
        {
            var mesh = new Mesh { name = name };
            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            for (int i = 0; i < sides; i++)
            {
                float angle = i / (float)sides * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * 0.5f;
                float z = Mathf.Sin(angle) * 0.5f;
                vertices.Add(new Vector3(x, -0.5f, z));
                vertices.Add(new Vector3(x, 0.5f, z));
            }

            int bottomCenter = vertices.Count;
            vertices.Add(new Vector3(0f, -0.5f, 0f));
            int topCenter = vertices.Count;
            vertices.Add(new Vector3(0f, 0.5f, 0f));

            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                int a = i * 2;
                int b = next * 2;
                int c = i * 2 + 1;
                int d = next * 2 + 1;

                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(b);
                triangles.Add(b);
                triangles.Add(c);
                triangles.Add(d);

                triangles.Add(bottomCenter);
                triangles.Add(b);
                triangles.Add(a);

                triangles.Add(topCenter);
                triangles.Add(c);
                triangles.Add(d);
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh CreateConeMesh(string name, int sides)
        {
            var mesh = new Mesh { name = name };
            var vertices = new List<Vector3> { new Vector3(0f, 0.58f, 0f), new Vector3(0f, -0.5f, 0f) };
            var triangles = new List<int>();

            for (int i = 0; i < sides; i++)
            {
                float angle = i / (float)sides * Mathf.PI * 2f;
                vertices.Add(new Vector3(Mathf.Cos(angle) * 0.5f, -0.5f, Mathf.Sin(angle) * 0.5f));
            }

            for (int i = 0; i < sides; i++)
            {
                int current = 2 + i;
                int next = 2 + ((i + 1) % sides);
                triangles.Add(0);
                triangles.Add(next);
                triangles.Add(current);
                triangles.Add(1);
                triangles.Add(current);
                triangles.Add(next);
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static float IslandHeight(float x, float z)
        {
            float radial = Mathf.Sqrt((x * x) / 21.5f + (z * z) / 17.0f);
            float dome = Mathf.Clamp01(1f - radial);
            float ridge = Mathf.PerlinNoise(x * 0.22f + 2.6f, z * 0.22f + 7.4f) * 0.55f;
            float terrace = Mathf.Floor((dome * 2.8f + ridge * 0.45f) * 4f) / 4f;
            return Mathf.Max(-0.10f, terrace * 1.05f - 0.16f);
        }

        static Color VertexTint(float height, float x, float z)
        {
            float n = Mathf.PerlinNoise(x * 0.45f + 11.1f, z * 0.45f + 3.2f);
            if (height < 0.02f)
                return Color.Lerp(new Color(1.0f, 0.88f, 0.55f), new Color(0.76f, 0.64f, 0.38f), n * 0.45f);

            if (height > 1.2f)
                return Color.Lerp(new Color(0.84f, 0.82f, 0.66f), new Color(0.62f, 0.62f, 0.52f), n * 0.6f);

            return Color.Lerp(new Color(0.78f, 0.86f, 0.42f), new Color(0.42f, 0.64f, 0.30f), n * 0.65f);
        }

        static void AddSceneToBuild(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == scenePath))
                return;

            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
