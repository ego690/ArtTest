using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ShortHikeStylePrototype.WaterFoam.Editor
{
    public static class ShortHikeWaterFoamSetup
    {
        const string Root = "Assets/ShortHikeStylePrototype/WaterFoam";
        const string MaterialsRoot = Root + "/Materials";
        const string MeshesRoot = Root + "/Meshes";
        const string ScenesRoot = Root + "/Scenes";
        const string TexturesRoot = Root + "/Textures";
        const string ScenePath = ScenesRoot + "/WaterFoam2DDemo.unity";
        const string RebuildMarkerPath = "Temp/ShortHikeWaterFoamRebuild.flag";
        const string RoystanFoamNoisePath = TexturesRoot + "/T_Roystan_FoamNoise.png";
        const string RoystanDistortionPath = TexturesRoot + "/T_Roystan_DistortionRG.png";
        const string RendererPath = "Assets/Settings/PC_Renderer.asset";
        const string NormalsFeatureName = "Water Foam Normals Texture Request";

        static readonly Color SkyColor = new Color(0.56f, 0.78f, 0.88f, 1f);

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

        [MenuItem("Tools/Water Foam Prototype/Build Demo Scene")]
        public static void BuildDemoScene()
        {
            EnsureFolders();
            EnsureRoystanTextures();
            EnsureNormalsTextureFeature();

            Material sand = CreateLitMaterial("M_WF_Sand", new Color(0.78f, 0.64f, 0.38f), 0.4f);
            Material grass = CreateLitMaterial("M_WF_Grass", new Color(0.34f, 0.57f, 0.30f), 0.35f);
            Material rock = CreateLitMaterial("M_WF_Rock", new Color(0.45f, 0.45f, 0.40f), 0.48f);
            Material warmPost = CreateLitMaterial("M_WF_WarmPost", new Color(0.72f, 0.41f, 0.28f), 0.35f);
            Material water = CreateWaterMaterial();
            Mesh islandMesh = CreateIslandMesh();

            CreateScene(islandMesh, sand, grass, rock, warmPost, water);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Water foam prototype scene built at " + ScenePath);
        }

        [MenuItem("Tools/Water Foam Prototype/Create Self Contained Demo In Current Scene")]
        public static void CreateSelfContainedDemoInCurrentScene()
        {
            EnsureFolders();
            EnsureRoystanTextures();
            EnsureNormalsTextureFeature();
            Material sand = CreateLitMaterial("M_WF_Sand", new Color(0.78f, 0.64f, 0.38f), 0.4f);
            Material grass = CreateLitMaterial("M_WF_Grass", new Color(0.34f, 0.57f, 0.30f), 0.35f);
            Material rock = CreateLitMaterial("M_WF_Rock", new Color(0.45f, 0.45f, 0.40f), 0.48f);
            Material warmPost = CreateLitMaterial("M_WF_WarmPost", new Color(0.72f, 0.41f, 0.28f), 0.35f);
            Material water = CreateWaterMaterial();
            Mesh islandMesh = CreateIslandMesh();

            CreateDemoObjects(islandMesh, sand, grass, rock, warmPost, water, createCameraAndLight: false);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        [MenuItem("Tools/Water Foam Prototype/Prepare Current Scene For World Height Water")]
        public static void PrepareCurrentSceneForWorldHeightWater()
        {
            Debug.Log("This prototype now includes a Roystan-style screen-depth water demo. Use Build Demo Scene to create it.");
        }

        public static void EnsureRoystanRuntimeAssets()
        {
            EnsureFolders();
            EnsureRoystanTextures();
            EnsureNormalsTextureFeature();
            AssetDatabase.SaveAssets();
        }

        public static Material LoadCurrentRoystanWaterMaterial()
        {
            EnsureRoystanRuntimeAssets();

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialsRoot + "/M_WF_RoystanToonWater.mat");
            if (material != null)
                return material;

            return CreateWaterMaterial();
        }

        static void EnsureFolders()
        {
            foreach (string folder in new[] { Root, MaterialsRoot, MeshesRoot, ScenesRoot, TexturesRoot, Root + "/Shaders", Root + "/Scripts", Root + "/Editor" })
            {
                if (AssetDatabase.IsValidFolder(folder))
                    continue;

                string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
                string name = Path.GetFileName(folder);
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        static Material CreateLitMaterial(string name, Color color, float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
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

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            material.SetOverrideTag("RenderType", "Opaque");
            material.renderQueue = (int)RenderQueue.Geometry;
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material CreateWaterMaterial()
        {
            Shader shader = Shader.Find("ShortHikeStylePrototype/WaterFoam/RoystanToonWater");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");

            string path = MaterialsRoot + "/M_WF_RoystanToonWater.mat";
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

            Texture2D foam = AssetDatabase.LoadAssetAtPath<Texture2D>(RoystanFoamNoisePath);
            Texture2D distortion = AssetDatabase.LoadAssetAtPath<Texture2D>(RoystanDistortionPath);
            if (foam != null)
                material.SetTexture("_FoamTex", foam);
            if (distortion != null)
                material.SetTexture("_DistortionTex", distortion);

            material.SetColor("_ShallowColor", new Color(0.40f, 0.92f, 0.84f, 1f));
            material.SetColor("_DeepColor", new Color(0.06f, 0.31f, 0.62f, 1f));
            material.SetFloat("_DepthMaxDistance", 2.1f);
            material.SetFloat("_Alpha", 0.82f);
            material.SetColor("_FoamColor", new Color(0.95f, 1.0f, 0.88f, 1f));
            material.SetFloat("_FoamMinDistance", 0.10f);
            material.SetFloat("_FoamMaxDistance", 0.82f);
            material.SetFloat("_FoamCutoff", 0.58f);
            material.SetFloat("_FoamSoftness", 0.045f);
            material.SetFloat("_FoamDisplayThreshold", 0.5f);
            material.SetFloat("_FoamNoiseScale", 5.7f);
            material.SetFloat("_FoamVariationStrength", 0.65f);
            material.SetFloat("_FoamVariationScale", 1.73f);
            material.SetVector("_SurfaceNoiseScroll", new Vector4(0.03f, 0.03f, 0f, 0f));
            material.SetVector("_FoamSpeed", new Vector4(0.04f, 0.02f, -0.025f, 0.035f));
            material.SetFloat("_SurfaceFoamAmount", 0.0f);
            material.SetFloat("_SurfaceFoamCutoff", 0.80f);
            material.SetFloat("_SurfaceDistortionAmount", 0.27f);
            material.SetFloat("_DistortionStrength", 0.045f);
            material.SetFloat("_DistortionScale", 3.2f);
            material.SetVector("_DistortionSpeed", new Vector4(0.025f, -0.018f, 0f, 0f));
            material.SetFloat("_NormalFoamStrength", 0.72f);
            material.SetColor("_ReflectionColor", new Color(0.80f, 0.95f, 1.0f, 1f));
            material.SetFloat("_ReflectionStrength", 0.18f);
            material.renderQueue = (int)RenderQueue.Transparent;

            EditorUtility.SetDirty(material);
            return material;
        }

        static void EnsureNormalsTextureFeature()
        {
            UniversalRendererData renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
                return;

            WaterFoamNormalsTextureFeature feature = null;
            foreach (ScriptableRendererFeature existing in renderer.rendererFeatures)
            {
                if (existing is WaterFoamNormalsTextureFeature normalsFeature)
                {
                    feature = normalsFeature;
                    break;
                }
            }

            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<WaterFoamNormalsTextureFeature>();
                feature.name = NormalsFeatureName;
                AssetDatabase.AddObjectToAsset(feature, renderer);
                renderer.rendererFeatures.Add(feature);
            }

            feature.name = NormalsFeatureName;
            feature.SetActive(true);
            EditorUtility.SetDirty(feature);
            SyncRendererFeatureMap(renderer);
            EditorUtility.SetDirty(renderer);
        }

        static void SyncRendererFeatureMap(UniversalRendererData renderer)
        {
            var serializedRenderer = new SerializedObject(renderer);
            serializedRenderer.Update();
            SerializedProperty map = serializedRenderer.FindProperty("m_RendererFeatureMap");
            map.arraySize = renderer.rendererFeatures.Count;
            for (int i = 0; i < renderer.rendererFeatures.Count; i++)
            {
                long localId = 0;
                ScriptableRendererFeature feature = renderer.rendererFeatures[i];
                if (feature != null)
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out localId);

                map.GetArrayElementAtIndex(i).longValue = localId;
            }

            serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureRoystanTextures()
        {
            const int size = 256;
            WriteTexturePng(RoystanFoamNoisePath, CreateRoystanFoamNoise(size));
            WriteTexturePng(RoystanDistortionPath, CreateRoystanDistortion(size));

            AssetDatabase.ImportAsset(RoystanFoamNoisePath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(RoystanDistortionPath, ImportAssetOptions.ForceUpdate);
            ConfigureGeneratedTextureImporter(RoystanFoamNoisePath, mipmaps: false, filterMode: FilterMode.Bilinear);
            ConfigureGeneratedTextureImporter(RoystanDistortionPath, mipmaps: true, filterMode: FilterMode.Bilinear);
        }

        static Texture2D CreateRoystanFoamNoise(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "T_Roystan_FoamNoise",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    float v = y / (float)size;
                    float noise = FractalPerlin(u, v, 4.0f, 4, 0.52f, new Vector2(17.31f, 41.77f));
                    noise = Mathf.SmoothStep(0.0f, 1.0f, noise);
                    byte value = (byte)Mathf.RoundToInt(noise * 255f);
                    pixels[y * size + x] = new Color32(value, value, value, 255);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        static Texture2D CreateRoystanDistortion(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
            {
                name = "T_Roystan_DistortionRG",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    float v = y / (float)size;
                    float r = FractalPerlin(u, v, 3.0f, 3, 0.55f, new Vector2(73.2f, 11.6f));
                    float g = FractalPerlin(u, v, 3.0f, 3, 0.55f, new Vector2(19.4f, 89.8f));
                    pixels[y * size + x] = new Color32(
                        (byte)Mathf.RoundToInt(r * 255f),
                        (byte)Mathf.RoundToInt(g * 255f),
                        128,
                        255);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            return texture;
        }

        static float FractalPerlin(float u, float v, float baseFrequency, int octaves, float persistence, Vector2 offset)
        {
            float sum = 0f;
            float amplitude = 1f;
            float amplitudeSum = 0f;
            float frequency = baseFrequency;

            for (int i = 0; i < octaves; i++)
            {
                float sample = TileablePerlin(u, v, frequency, offset + new Vector2(i * 31.7f, i * 19.3f));
                sum += sample * amplitude;
                amplitudeSum += amplitude;
                amplitude *= persistence;
                frequency *= 2f;
            }

            return Mathf.Clamp01(sum / Mathf.Max(amplitudeSum, 0.0001f));
        }

        static float TileablePerlin(float u, float v, float frequency, Vector2 offset)
        {
            float x = u * frequency + offset.x;
            float y = v * frequency + offset.y;
            float width = frequency;
            float height = frequency;

            float a = Mathf.PerlinNoise(x, y);
            float b = Mathf.PerlinNoise(x - width, y);
            float c = Mathf.PerlinNoise(x, y - height);
            float d = Mathf.PerlinNoise(x - width, y - height);
            float blendX = Mathf.SmoothStep(0f, 1f, u);
            float blendY = Mathf.SmoothStep(0f, 1f, v);

            return Mathf.Lerp(Mathf.Lerp(a, b, blendX), Mathf.Lerp(c, d, blendX), blendY);
        }

        static void WriteTexturePng(string assetPath, Texture2D texture)
        {
            string fullPath = Path.GetFullPath(assetPath);
            File.WriteAllBytes(fullPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        static void ConfigureGeneratedTextureImporter(string assetPath, bool mipmaps, FilterMode filterMode)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return;

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.mipmapEnabled = mipmaps;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = filterMode;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        static Mesh CreateIslandMesh()
        {
            const int resolution = 48;
            const float size = 12f;
            const float half = size * 0.5f;
            var vertices = new List<Vector3>((resolution + 1) * (resolution + 1));
            var normals = new List<Vector3>((resolution + 1) * (resolution + 1));
            var uvs = new List<Vector2>((resolution + 1) * (resolution + 1));
            var triangles = new List<int>(resolution * resolution * 6);

            for (int z = 0; z <= resolution; z++)
            {
                for (int x = 0; x <= resolution; x++)
                {
                    Vector2 uv = new Vector2(x / (float)resolution, z / (float)resolution);
                    float px = Mathf.Lerp(-half, half, uv.x);
                    float pz = Mathf.Lerp(-half, half, uv.y);
                    vertices.Add(new Vector3(px, IslandHeight(px, pz), pz));
                    uvs.Add(uv);
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
                name = "MESH_WaterFoam_DepthIsland",
                indexFormat = IndexFormat.UInt32
            };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            string path = MeshesRoot + "/MESH_WaterFoam_DepthIsland.asset";
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

        static void CreateScene(Mesh islandMesh, Material sand, Material grass, Material rock, Material warmPost, Material water)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.path == ScenePath)
            {
                ClearScene(activeScene);
                CreateDemoObjects(islandMesh, sand, grass, rock, warmPost, water, createCameraAndLight: true);
                AddSceneToBuild(ScenePath);
                EditorSceneManager.SaveScene(activeScene, ScenePath);
                return;
            }

            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SetActiveScene(scene);
            scene.name = "WaterFoam2DDemo";

            CreateDemoObjects(islandMesh, sand, grass, rock, warmPost, water, createCameraAndLight: true);
            AddSceneToBuild(ScenePath);
            EditorSceneManager.SaveScene(scene, ScenePath);

            if (previousActiveScene.IsValid())
                EditorSceneManager.SetActiveScene(previousActiveScene);

            EditorSceneManager.CloseScene(scene, true);
        }

        static void ClearScene(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            foreach (GameObject root in roots)
                Object.DestroyImmediate(root);
        }

        static void CreateDemoObjects(Mesh islandMesh, Material sand, Material grass, Material rock, Material warmPost, Material water, bool createCameraAndLight)
        {
            GameObject root = new GameObject("Water Foam Prototype - Roystan Screen Depth Demo");
            GameObject motionRoot = new GameObject("Slow Orbit Root");
            motionRoot.transform.SetParent(root.transform, false);

            GameObject seaFloor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            seaFloor.name = "Visible Sloped Sea Floor";
            seaFloor.transform.SetParent(root.transform, false);
            seaFloor.transform.position = new Vector3(0f, -1.35f, 0f);
            seaFloor.transform.localScale = new Vector3(2.7f, 1f, 2.7f);
            seaFloor.GetComponent<MeshRenderer>().sharedMaterial = sand;
            Object.DestroyImmediate(seaFloor.GetComponent<Collider>());

            GameObject island = new GameObject("Depth Tested Island");
            island.transform.SetParent(root.transform, false);
            island.AddComponent<MeshFilter>().sharedMesh = islandMesh;
            island.AddComponent<MeshRenderer>().sharedMaterial = grass;

            GameObject waterPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            waterPlane.name = "Roystan Style Transparent Water";
            waterPlane.transform.SetParent(root.transform, false);
            waterPlane.transform.position = new Vector3(0f, 0f, 0f);
            waterPlane.transform.localScale = new Vector3(2.4f, 1f, 2.4f);
            waterPlane.GetComponent<MeshRenderer>().sharedMaterial = water;
            Object.DestroyImmediate(waterPlane.GetComponent<Collider>());

            CreateCylinder("Half Submerged Round Post", root.transform, new Vector3(-2.35f, 0.32f, -1.10f), new Vector3(0.48f, 1.28f, 0.48f), warmPost, 20);
            CreateCylinder("Near Shore Rock Stack", root.transform, new Vector3(2.00f, 0.13f, 1.45f), new Vector3(0.72f, 0.46f, 0.72f), rock, 12);
            CreateCylinder("Foam Width Test Pylon", root.transform, new Vector3(0.95f, 0.22f, -2.20f), new Vector3(0.28f, 1.0f, 0.28f), warmPost, 12);

            GameObject bobber = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bobber.name = "Bobbing Foam Contact Test";
            bobber.transform.SetParent(motionRoot.transform, false);
            bobber.transform.position = new Vector3(-0.35f, 0.22f, 2.25f);
            bobber.transform.localScale = new Vector3(0.46f, 0.46f, 0.46f);
            bobber.GetComponent<MeshRenderer>().sharedMaterial = warmPost;
            Object.DestroyImmediate(bobber.GetComponent<Collider>());

            for (int i = 0; i < 9; i++)
            {
                float angle = i / 9f * Mathf.PI * 2f;
                Vector3 position = new Vector3(Mathf.Cos(angle) * 4.0f, -0.03f, Mathf.Sin(angle) * 3.5f);
                CreateCylinder("Shoreline Pebble " + (i + 1), root.transform, position, new Vector3(0.28f, 0.18f, 0.34f), rock, 9);
            }

            var motion = root.AddComponent<WaterFoamDemoMotion>();
            var serializedMotion = new SerializedObject(motion);
            serializedMotion.FindProperty("orbitRoot").objectReferenceValue = motionRoot.transform;
            serializedMotion.FindProperty("bobbingObject").objectReferenceValue = bobber.transform;
            serializedMotion.ApplyModifiedPropertiesWithoutUndo();

            if (!createCameraAndLight)
                return;

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 38f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 80f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = SkyColor;
            camera.allowHDR = true;

            var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = false;
            cameraData.requiresDepthTexture = true;
            cameraData.requiresColorTexture = true;
            cameraData.antialiasing = AntialiasingMode.None;

            cameraObject.transform.position = new Vector3(-6.8f, 5.0f, -6.9f);
            cameraObject.transform.LookAt(new Vector3(0f, 0.25f, 0f));

            GameObject sunObject = new GameObject("Directional Sun");
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1.0f, 0.88f, 0.70f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.32f;
            sunObject.transform.rotation = Quaternion.Euler(48f, -35f, 8f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.52f, 0.62f, 0.68f);
            RenderSettings.fog = false;
        }

        static GameObject CreateCylinder(string name, Transform parent, Vector3 position, Vector3 scale, Material material, int sides)
        {
            Mesh mesh = CreateCylinderMesh(name + " Mesh", sides);
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.position = position;
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

        static float IslandHeight(float x, float z)
        {
            float radial = Mathf.Sqrt((x * x) / 16.5f + (z * z) / 12.5f);
            float dome = Mathf.Clamp01(1f - radial);
            float ripple = Mathf.PerlinNoise(x * 0.38f + 12.3f, z * 0.38f + 4.8f) * 0.24f;
            float shelf = Mathf.SmoothStep(-0.55f, 1.05f, dome + ripple);
            return shelf * 1.35f - 0.34f;
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
