using System.Collections.Generic;
using System.IO;
using ObraDinnPrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ObraDinnPrototype.Editor
{
    [InitializeOnLoad]
    public static class ObraDinnPrototypeSetup
    {
        const string Root = "Assets/ObraDinnPrototype";
        const string MaterialsRoot = Root + "/Materials";
        const string TexturesRoot = Root + "/Textures";
        const string ScenesRoot = Root + "/Scenes";
        const string PipelineAssetPath = "Assets/Settings/PC_RPAsset.asset";
        const string RendererPath = "Assets/Settings/PC_Renderer.asset";
        const string ObraDinnRendererPath = "Assets/Settings/PC_ObraDinn_Renderer.asset";
        const string ScenePath = ScenesRoot + "/ObraDinnDitherDemo.unity";
        const string PostMaterialPath = MaterialsRoot + "/M_ObraDinnPost.mat";
        const string BayerMaterialPath = MaterialsRoot + "/M_ObraDinnBayer.mat";
        const string HalftoneMaterialPath = MaterialsRoot + "/M_ObraDinnHalftone.mat";
        const string WaterMaterialPath = MaterialsRoot + "/M_ObraDinnWater.mat";
        const string BlueNoisePath = TexturesRoot + "/T_BlueNoise128.png";
        const string BayerTexturePath = TexturesRoot + "/T_Bayer8.png";
        const string HalftoneTexturePath = TexturesRoot + "/T_HalftoneRampAtlas.png";
        const string InvertMaterialGuid = "78a08787ca30045abb69f6f7dd878e09";
        const string OtherPassName = "Obra Dinn Other Dither Pass";
        const string FacePassName = "Obra Dinn Face Matrix Pass";
        const string BayerPassName = "Obra Dinn Bayer Dither Pass";
        const string HalftonePassName = "Obra Dinn Halftone Dither Pass";
        const string PreInvertPassName = "Obra Dinn Pre Invert Pass";
        const string PostInvertPassName = "Obra Dinn Post Invert Pass";
        const string LegacyPassName = "Obra Dinn Dither Pass";
        const string AutoSetupSessionKey = "ObraDinnPrototype.AutoSetupComplete";

        static ObraDinnPrototypeSetup()
        {
            if (SessionState.GetBool(AutoSetupSessionKey, false))
                return;

            EditorApplication.delayCall += RunAutoSetupOnce;
        }

        static void RunAutoSetupOnce()
        {
            if (SessionState.GetBool(AutoSetupSessionKey, false))
                return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RunAutoSetupOnce;
                return;
            }

            SessionState.SetBool(AutoSetupSessionKey, true);
            BuildDemoScene();
        }

        [MenuItem("Tools/Obra Dinn Prototype/Build Demo Scene")]
        public static void BuildDemoScene()
        {
            EnsureFolders();
            Texture2D blueNoise = CreateBlueNoiseTexture();
            Texture2D bayerTexture = CreateBayerTexture();
            Texture2D halftoneTexture = CreateHalftoneTexture();
            Material postMaterial = CreatePostMaterial(blueNoise);
            Material bayerMaterial = CreateBayerMaterial(bayerTexture);
            Material halftoneMaterial = CreateHalftoneMaterial(halftoneTexture);
            UniversalRendererData obraDinnRenderer = EnsureObraDinnRenderer(postMaterial, bayerMaterial, halftoneMaterial);
            int obraDinnRendererIndex = EnsureRendererRegistered(obraDinnRenderer);
            CreateDemoScene(postMaterial, halftoneMaterial, obraDinnRenderer, obraDinnRendererIndex);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Obra Dinn prototype scene built at " + ScenePath);
        }

        [MenuItem("Tools/Obra Dinn Prototype/Apply Bayer Pass Assets")]
        public static void ApplyBayerPassAssets()
        {
            EnsureFolders();
            Texture2D bayerTexture = CreateBayerTexture();
            Texture2D halftoneTexture = CreateHalftoneTexture();
            Material bayerMaterial = CreateBayerMaterial(bayerTexture);
            Material halftoneMaterial = CreateHalftoneMaterial(halftoneTexture);
            Material postMaterial = AssetDatabase.LoadAssetAtPath<Material>(PostMaterialPath);
            if (postMaterial == null)
                postMaterial = CreatePostMaterial(CreateBlueNoiseTexture());

            UniversalRendererData obraDinnRenderer = EnsureObraDinnRenderer(postMaterial, bayerMaterial, halftoneMaterial);
            EnsureRendererRegistered(obraDinnRenderer);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Obra Dinn Bayer pass assets applied.");
        }

        static void EnsureFolders()
        {
            foreach (string folder in new[] { Root, MaterialsRoot, TexturesRoot, ScenesRoot })
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
                    string name = Path.GetFileName(folder);
                    AssetDatabase.CreateFolder(parent, name);
                }
            }
        }

        static Texture2D CreateBlueNoiseTexture()
        {
            const int size = 128;
            var values = new List<float>(size * size);
            for (int i = 0; i < size * size; i++)
                values.Add((i + 0.5f) / (size * size));

            var rng = new System.Random(3909);
            for (int i = values.Count - 1; i > 0; i--)
            {
                int swap = rng.Next(i + 1);
                (values[i], values[swap]) = (values[swap], values[i]);
            }

            // A tiny relaxation pass pushes the random threshold field away from white-noise clumps.
            for (int pass = 0; pass < 6; pass++)
            {
                for (int i = 0; i < values.Count; i++)
                {
                    int x = i % size;
                    int y = i / size;
                    float local = 0f;
                    for (int oy = -1; oy <= 1; oy++)
                    {
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            if (ox == 0 && oy == 0)
                                continue;

                            int sx = (x + ox + size) % size;
                            int sy = (y + oy + size) % size;
                            local += values[sy * size + sx];
                        }
                    }

                    local /= 8f;
                    values[i] = Mathf.Lerp(values[i], 1f - local, 0.025f);
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.R8, false, true)
            {
                name = "T_BlueNoise128",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };

            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                byte v = (byte)Mathf.Clamp(Mathf.RoundToInt(values[i] * 255f), 0, 255);
                pixels[i] = new Color32(v, v, v, 255);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(BlueNoisePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(BlueNoisePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(BlueNoisePath);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(BlueNoisePath);
        }

        static Texture2D CreateBayerTexture()
        {
            int[,] bayer =
            {
                { 0, 48, 12, 60, 3, 51, 15, 63 },
                { 32, 16, 44, 28, 35, 19, 47, 31 },
                { 8, 56, 4, 52, 11, 59, 7, 55 },
                { 40, 24, 36, 20, 43, 27, 39, 23 },
                { 2, 50, 14, 62, 1, 49, 13, 61 },
                { 34, 18, 46, 30, 33, 17, 45, 29 },
                { 10, 58, 6, 54, 9, 57, 5, 53 },
                { 42, 26, 38, 22, 41, 25, 37, 21 },
            };

            const int size = 8;
            var texture = new Texture2D(size, size, TextureFormat.R8, false, true)
            {
                name = "T_Bayer8",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    byte v = (byte)Mathf.Clamp(Mathf.RoundToInt(((bayer[y, x] + 0.5f) / 64f) * 255f), 0, 255);
                    pixels[y * size + x] = new Color32(v, v, v, 255);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(BayerTexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(BayerTexturePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(BayerTexturePath);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(BayerTexturePath);
        }

        static Texture2D CreateHalftoneTexture()
        {
            const int tileSize = 32;
            const int levels = 17;
            int width = tileSize * levels;
            const int height = tileSize;
            var texture = new Texture2D(width, height, TextureFormat.R8, false, true)
            {
                name = "T_HalftoneRampAtlas",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[width * height];
            const int dotCellSize = 8;
            const int centerLevel = levels / 2;
            float cellArea = dotCellSize * dotCellSize;
            Vector2 cellCenter = new Vector2((dotCellSize - 1) * 0.5f, (dotCellSize - 1) * 0.5f);
            for (int level = 0; level < levels; level++)
            {
                for (int y = 0; y < tileSize; y++)
                {
                    for (int x = 0; x < tileSize; x++)
                    {
                        bool lightPixel;
                        if (level == centerLevel)
                        {
                            lightPixel = ((x / 4) + (y / 4)) % 2 == 0;
                        }
                        else
                        {
                            int sourceLevel = level < centerLevel ? level : levels - 1 - level;
                            float coverage = Mathf.Clamp01(sourceLevel / (float)(levels - 1));
                            float dotRadius = Mathf.Sqrt(coverage * cellArea / Mathf.PI);
                            float cellX = x % dotCellSize;
                            float cellY = y % dotCellSize;
                            float distance = Vector2.Distance(new Vector2(cellX, cellY), cellCenter);
                            bool insideDot = distance <= dotRadius;
                            lightPixel = level < centerLevel ? insideDot : !insideDot;
                        }

                        byte v = lightPixel ? (byte)255 : (byte)0;
                        int atlasX = level * tileSize + x;
                        pixels[y * width + atlasX] = new Color32(v, v, v, 255);
                    }
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(HalftoneTexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(HalftoneTexturePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(HalftoneTexturePath);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(HalftoneTexturePath);
        }

        static Material CreatePostMaterial(Texture2D blueNoise)
        {
            Shader shader = Shader.Find("Hidden/ObraDinnPrototype/ObraDinnStyle");
            if (shader == null)
                throw new System.InvalidOperationException("Missing ObraDinnStyle shader.");

            var material = AssetDatabase.LoadAssetAtPath<Material>(PostMaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, PostMaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.name = "M_ObraDinnPost";
            material.SetTexture("_BlueNoiseTex", blueNoise);
            material.SetColor("_DarkColor", new Color(0.035f, 0.032f, 0.026f, 1f));
            material.SetColor("_LightColor", new Color(0.91f, 0.84f, 0.66f, 1f));
            material.SetColor("_DarkEdgeColor", new Color(0.035f, 0.032f, 0.026f, 1f));
            material.SetColor("_LightEdgeColor", new Color(0.91f, 0.84f, 0.66f, 1f));
            material.SetFloat("_PixelScale", 1f);
            material.SetFloat("_Contrast", 1.45f);
            material.SetFloat("_Brightness", 0.02f);
            material.SetFloat("_Gamma", 0.82f);
            material.SetFloat("_BlueNoiseWeight", 0.82f);
            material.SetFloat("_WorldDitherWeight", 0.68f);
            material.SetFloat("_WorldDitherScale", 36f);
            material.SetFloat("_WorldTriplanarSharpness", 4f);
            material.SetFloat("_ToneFieldStrength", 0.08f);
            material.SetFloat("_ToneFieldScale", 42f);
            material.SetFloat("_ToneFieldHatchStrength", 0.05f);
            material.SetFloat("_ToneFieldHatchAngle", 0.62f);
            material.SetFloat("_FaceMatrixScale", 1.4f);
            material.SetVector("_FaceMatrixOffset", new Vector4(3f, 5f, 0f, 0f));
            material.SetFloat("_FaceBlueNoiseWeight", 0.14f);
            material.SetFloat("_ThresholdBias", 0f);
            material.SetFloat("_EdgeStrength", 0.72f);
            material.SetFloat("_UseLightEdgeColor", 1f);
            material.SetFloat("_DepthEdgeScale", 42f);
            material.SetFloat("_NormalEdgeScale", 4f);
            material.SetFloat("_OffsetStrength", 1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material CreateBayerMaterial(Texture2D bayerTexture)
        {
            Shader shader = Shader.Find("Hidden/ObraDinnPrototype/BayerDitherFullscreen");
            if (shader == null)
                throw new System.InvalidOperationException("Missing BayerDitherFullscreen shader.");

            var material = AssetDatabase.LoadAssetAtPath<Material>(BayerMaterialPath);
            bool created = material == null;
            if (created)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, BayerMaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.name = "M_ObraDinnBayer";
            material.SetTexture("_BayerTex", bayerTexture);
            if (created)
            {
                material.SetColor("_DarkColor", new Color(0.035f, 0.032f, 0.026f, 1f));
                material.SetColor("_LightColor", new Color(0.91f, 0.84f, 0.66f, 1f));
                material.SetFloat("_PixelScale", 1f);
                material.SetFloat("_Contrast", 1f);
                material.SetFloat("_Brightness", 0f);
                material.SetFloat("_Gamma", 1f);
                material.SetFloat("_ThresholdBias", 0f);
                material.SetFloat("_Strength", 1f);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        static Material CreateHalftoneMaterial(Texture2D halftoneTexture)
        {
            Shader shader = Shader.Find("Hidden/ObraDinnPrototype/HalftoneDitherFullscreen");
            if (shader == null)
                throw new System.InvalidOperationException("Missing HalftoneDitherFullscreen shader.");

            var material = AssetDatabase.LoadAssetAtPath<Material>(HalftoneMaterialPath);
            bool created = material == null;
            if (created)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, HalftoneMaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.name = "M_ObraDinnHalftone";
            material.SetTexture("_HalftoneTex", halftoneTexture);
            material.SetFloat("_AtlasTileSize", 32f);
            material.SetFloat("_AtlasLevels", 17f);
            if (created)
            {
                material.SetColor("_DarkColor", new Color(0.035f, 0.032f, 0.026f, 1f));
                material.SetColor("_LightColor", new Color(0.91f, 0.84f, 0.66f, 1f));
                material.SetFloat("_BlockSize", 12f);
                material.SetFloat("_UseBlockCount", 0f);
                material.SetFloat("_BlockColumns", 96f);
                material.SetFloat("_BlockRows", 54f);
                material.SetFloat("_AverageRadius", 1f);
                material.SetFloat("_Contrast", 1.2f);
                material.SetFloat("_Brightness", 0f);
                material.SetFloat("_Gamma", 1f);
                material.SetFloat("_ThresholdScale", 1.35f);
                material.SetFloat("_ThresholdBias", 0f);
                material.SetFloat("_Strength", 1f);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        static UniversalRendererData EnsureObraDinnRenderer(Material postMaterial, Material bayerMaterial, Material halftoneMaterial)
        {
            var defaultRenderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (defaultRenderer == null)
                throw new System.InvalidOperationException("Could not find renderer at " + RendererPath);

            DisableObraDinnFeatures(defaultRenderer);

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(ObraDinnRendererPath);
            if (renderer == null)
            {
                if (!AssetDatabase.CopyAsset(RendererPath, ObraDinnRendererPath))
                    throw new System.InvalidOperationException("Could not create renderer at " + ObraDinnRendererPath);

                AssetDatabase.ImportAsset(ObraDinnRendererPath);
                renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(ObraDinnRendererPath);
            }

            if (renderer == null)
                throw new System.InvalidOperationException("Could not find renderer at " + ObraDinnRendererPath);

            renderer.name = "PC_ObraDinn_Renderer";
            ConfigureObraDinnFeatures(renderer, postMaterial, bayerMaterial, halftoneMaterial, true);
            EditorUtility.SetDirty(renderer);
            return renderer;
        }

        static void DisableObraDinnFeatures(UniversalRendererData renderer)
        {
            foreach (ScriptableRendererFeature existing in renderer.rendererFeatures)
            {
                if (existing == null)
                    continue;

                if (existing.name == OtherPassName || existing.name == LegacyPassName || existing.name == FacePassName || existing.name == BayerPassName ||
                    existing.name == HalftonePassName ||
                    existing.name == PreInvertPassName || existing.name == PostInvertPassName)
                {
                    existing.SetActive(false);
                    EditorUtility.SetDirty(existing);
                }
            }

            EditorUtility.SetDirty(renderer);
        }

        static void ConfigureObraDinnFeatures(UniversalRendererData renderer, Material postMaterial, Material bayerMaterial, Material halftoneMaterial, bool active)
        {
            Material invertMaterial = LoadInvertMaterial();
            FullScreenPassRendererFeature otherFeature = null;
            FullScreenPassRendererFeature faceFeature = null;
            FullScreenPassRendererFeature bayerFeature = null;
            FullScreenPassRendererFeature halftoneFeature = null;
            FullScreenPassRendererFeature preInvertFeature = null;
            FullScreenPassRendererFeature postInvertFeature = null;
            foreach (ScriptableRendererFeature existing in renderer.rendererFeatures)
            {
                if (existing is not FullScreenPassRendererFeature fullScreen)
                    continue;

                if (existing.name == OtherPassName || existing.name == LegacyPassName)
                {
                    existing.name = OtherPassName;
                    otherFeature = fullScreen;
                }
                else if (existing.name == FacePassName)
                {
                    faceFeature = fullScreen;
                }
                else if (existing.name == BayerPassName)
                {
                    bayerFeature = fullScreen;
                }
                else if (existing.name == HalftonePassName)
                {
                    halftoneFeature = fullScreen;
                }
                else if (existing.name == PreInvertPassName)
                {
                    preInvertFeature = fullScreen;
                }
                else if (existing.name == PostInvertPassName)
                {
                    postInvertFeature = fullScreen;
                }
                else if (fullScreen.passMaterial == invertMaterial)
                {
                    if (fullScreen.injectionPoint == FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing && preInvertFeature == null)
                        preInvertFeature = fullScreen;
                    else if (postInvertFeature == null)
                        postInvertFeature = fullScreen;
                }
                else if (fullScreen.passMaterial == bayerMaterial && bayerFeature == null)
                {
                    bayerFeature = fullScreen;
                }
                else if (fullScreen.passMaterial == halftoneMaterial && halftoneFeature == null)
                {
                    halftoneFeature = fullScreen;
                }
            }

            otherFeature ??= AddRendererFeature(renderer, OtherPassName);
            faceFeature ??= AddRendererFeature(renderer, FacePassName);
            bayerFeature ??= AddRendererFeature(renderer, BayerPassName);
            halftoneFeature ??= AddRendererFeature(renderer, HalftonePassName);
            preInvertFeature ??= AddRendererFeature(renderer, PreInvertPassName);
            postInvertFeature ??= AddRendererFeature(renderer, PostInvertPassName);

            ConfigureInvertPass(preInvertFeature, invertMaterial, FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing, active);
            ConfigureFullScreenPass(otherFeature, postMaterial, 0, active);
            ConfigureFullScreenPass(faceFeature, postMaterial, 1, active);
            ConfigureBayerPass(bayerFeature, bayerMaterial, false);
            ConfigureHalftonePass(halftoneFeature, halftoneMaterial, false);
            ConfigureInvertPass(postInvertFeature, invertMaterial, FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing, active);
            OrderRendererFeatures(renderer, preInvertFeature, otherFeature, faceFeature, bayerFeature, halftoneFeature, postInvertFeature);

            EditorUtility.SetDirty(renderer);
        }

        static Material LoadInvertMaterial()
        {
            string path = AssetDatabase.GUIDToAssetPath(InvertMaterialGuid);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
                throw new System.InvalidOperationException("Could not find FullscreenInvertColors material.");

            return material;
        }

        static int EnsureRendererRegistered(UniversalRendererData renderer)
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
            if (pipeline == null)
                throw new System.InvalidOperationException("Could not find render pipeline asset at " + PipelineAssetPath);

            var serializedPipeline = new SerializedObject(pipeline);
            serializedPipeline.Update();
            SerializedProperty renderers = serializedPipeline.FindProperty("m_RendererDataList");

            for (int i = 0; i < renderers.arraySize; i++)
            {
                if (renderers.GetArrayElementAtIndex(i).objectReferenceValue == renderer)
                    return i;
            }

            int index = renderers.arraySize;
            renderers.arraySize++;
            renderers.GetArrayElementAtIndex(index).objectReferenceValue = renderer;
            serializedPipeline.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            return index;
        }

        static FullScreenPassRendererFeature AddRendererFeature(UniversalRendererData renderer, string featureName)
        {
            var feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
            feature.name = featureName;
            AssetDatabase.AddObjectToAsset(feature, renderer);
            renderer.rendererFeatures.Add(feature);
            SyncRendererFeatureMap(renderer);

            return feature;
        }

        static void OrderRendererFeatures(UniversalRendererData renderer, params ScriptableRendererFeature[] orderedFeatures)
        {
            var orderedSet = new HashSet<ScriptableRendererFeature>();
            foreach (ScriptableRendererFeature feature in orderedFeatures)
            {
                if (feature != null)
                    orderedSet.Add(feature);
            }

            renderer.rendererFeatures.RemoveAll(feature => orderedSet.Contains(feature));
            foreach (ScriptableRendererFeature feature in orderedFeatures)
            {
                if (feature != null)
                    renderer.rendererFeatures.Add(feature);
            }

            SyncRendererFeatureMap(renderer);
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

        static void ConfigureFullScreenPass(FullScreenPassRendererFeature feature, Material postMaterial, int passIndex, bool active)
        {
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
            feature.fetchColorBuffer = true;
            feature.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal;
            feature.passMaterial = postMaterial;
            feature.passIndex = passIndex;
            feature.bindDepthStencilAttachment = true;
            feature.SetActive(active);
            EditorUtility.SetDirty(feature);
        }

        static void ConfigureBayerPass(FullScreenPassRendererFeature feature, Material bayerMaterial, bool active)
        {
            feature.name = BayerPassName;
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
            feature.fetchColorBuffer = true;
            feature.requirements = ScriptableRenderPassInput.None;
            feature.passMaterial = bayerMaterial;
            feature.passIndex = 0;
            feature.bindDepthStencilAttachment = false;
            feature.SetActive(active);
            EditorUtility.SetDirty(feature);
        }

        static void ConfigureHalftonePass(FullScreenPassRendererFeature feature, Material halftoneMaterial, bool active)
        {
            feature.name = HalftonePassName;
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
            feature.fetchColorBuffer = true;
            feature.requirements = ScriptableRenderPassInput.None;
            feature.passMaterial = halftoneMaterial;
            feature.passIndex = 0;
            feature.bindDepthStencilAttachment = false;
            feature.SetActive(active);
            EditorUtility.SetDirty(feature);
        }

        static void ConfigureInvertPass(FullScreenPassRendererFeature feature, Material invertMaterial, FullScreenPassRendererFeature.InjectionPoint injectionPoint, bool active)
        {
            feature.name = injectionPoint == FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing
                ? PreInvertPassName
                : PostInvertPassName;
            feature.injectionPoint = injectionPoint;
            feature.fetchColorBuffer = true;
            feature.requirements = ScriptableRenderPassInput.None;
            feature.passMaterial = invertMaterial;
            feature.passIndex = 0;
            feature.bindDepthStencilAttachment = false;
            feature.SetActive(active);
            EditorUtility.SetDirty(feature);
        }

        static void CreateDemoScene(Material postMaterial, Material halftoneMaterial, UniversalRendererData obraDinnRenderer, int obraDinnRendererIndex)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "ObraDinnDitherDemo";

            Material hull = CreateMaterial("M_DemoHullWarmGray", new Color(0.55f, 0.52f, 0.45f), 0.38f);
            Material deck = CreateMaterial("M_DemoDeckPale", new Color(0.78f, 0.73f, 0.62f), 0.32f);
            Material dark = CreateMaterial("M_DemoDarkWood", new Color(0.28f, 0.25f, 0.20f), 0.42f);
            Material face = CreateFaceStencilMaterial();
            Material coat = CreateMaterial("M_DemoCoat", new Color(0.18f, 0.18f, 0.17f), 0.55f);
            Material water = CreateWaterMaterial();

            GameObject cameraObject = new GameObject("Main Camera");
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, 2.1f, -7.2f);
            camera.transform.rotation = Quaternion.Euler(11f, 0f, 0f);
            camera.fieldOfView = 54f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.70f, 0.66f, 0.56f);
            camera.allowHDR = false;
            var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            cameraData.SetRenderer(obraDinnRendererIndex);
            var rendererToggle = cameraObject.AddComponent<ObraDinnSceneRendererToggle>();
            var toggleSerialized = new SerializedObject(rendererToggle);
            toggleSerialized.FindProperty("effectEnabled").boolValue = true;
            toggleSerialized.FindProperty("invertPassesEnabled").boolValue = true;
            toggleSerialized.FindProperty("bayerPassEnabled").boolValue = false;
            toggleSerialized.FindProperty("mainDitherPassesEnabled").boolValue = true;
            toggleSerialized.FindProperty("halftonePassEnabled").boolValue = false;
            toggleSerialized.FindProperty("halftoneMaterial").objectReferenceValue = halftoneMaterial;
            toggleSerialized.FindProperty("halftoneUseBlockCount").boolValue = false;
            toggleSerialized.FindProperty("halftoneBlockSize").floatValue = 12f;
            toggleSerialized.FindProperty("halftoneBlockColumns").floatValue = 96f;
            toggleSerialized.FindProperty("halftoneBlockRows").floatValue = 54f;
            toggleSerialized.FindProperty("normalRendererIndex").intValue = -1;
            toggleSerialized.FindProperty("obraDinnRendererIndex").intValue = obraDinnRendererIndex;
            toggleSerialized.FindProperty("obraDinnRendererData").objectReferenceValue = obraDinnRenderer;
            toggleSerialized.FindProperty("otherFeatureName").stringValue = OtherPassName;
            toggleSerialized.FindProperty("faceFeatureName").stringValue = FacePassName;
            toggleSerialized.FindProperty("preInvertFeatureName").stringValue = PreInvertPassName;
            toggleSerialized.FindProperty("postInvertFeatureName").stringValue = PostInvertPassName;
            toggleSerialized.FindProperty("bayerFeatureName").stringValue = BayerPassName;
            toggleSerialized.FindProperty("halftoneFeatureName").stringValue = HalftonePassName;
            toggleSerialized.FindProperty("applyInEditMode").boolValue = true;
            toggleSerialized.ApplyModifiedPropertiesWithoutUndo();
            var offset = cameraObject.AddComponent<ObraDinnDitherOffset>();
            var offsetSerialized = new SerializedObject(offset);
            offsetSerialized.FindProperty("targetMaterial").objectReferenceValue = postMaterial;
            offsetSerialized.FindProperty("strength").floatValue = 1f;
            offsetSerialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject lightObject = new GameObject("Moon Key Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.45f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(52f, -34f, 12f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.21f, 0.22f, 0.24f);

            CreateCube("Deck", new Vector3(0f, -0.08f, 0f), new Vector3(9f, 0.16f, 10f), deck);
            CreateCube("Back Wall", new Vector3(0f, 2.0f, 4.2f), new Vector3(9f, 4f, 0.22f), hull);
            CreateCube("Left Hull Wall", new Vector3(-4.4f, 1.4f, 0f), new Vector3(0.24f, 2.8f, 10f), hull);
            CreateCube("Right Hull Wall", new Vector3(4.4f, 1.4f, 0f), new Vector3(0.24f, 2.8f, 10f), hull);
            CreateWaterPlane("Port Water", new Vector3(-6.9f, 0.03f, -0.2f), new Vector3(6.0f, 1f, 12.5f), water);
            CreateWaterPlane("Starboard Water", new Vector3(6.9f, 0.03f, -0.2f), new Vector3(6.0f, 1f, 12.5f), water);

            for (int i = 0; i < 7; i++)
            {
                float x = -3.6f + i * 1.2f;
                CreateCube("Deck Plank " + i, new Vector3(x, 0.025f, -0.25f), new Vector3(0.045f, 0.05f, 9.5f), dark);
            }

            for (int i = 0; i < 6; i++)
            {
                float x = -3.5f + i * 1.4f;
                CreateCube("Wall Rib " + i, new Vector3(x, 2.0f, 4.05f), new Vector3(0.11f, 3.5f, 0.32f), dark);
            }

            CreateBarrelCluster(dark, deck);
            CreateCharacter("Sailor A", new Vector3(-1.5f, 0.95f, 0.6f), coat, face);
            CreateCharacter("Sailor B", new Vector3(1.35f, 0.95f, 1.1f), coat, face);
            CreateSphere("Bayer/Blue Noise Test Sphere", new Vector3(0f, 1.35f, -1.15f), new Vector3(1.35f, 1.35f, 1.35f), deck);

            var volumeObject = new GameObject("Volume");
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuild(ScenePath);
        }

        static Material CreateMaterial(string name, Color color, float smoothness)
        {
            string path = MaterialsRoot + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
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
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material CreateFaceStencilMaterial()
        {
            string path = MaterialsRoot + "/M_DemoFace.mat";
            Shader shader = Shader.Find("ObraDinnPrototype/FaceStencilLit");
            if (shader == null)
                throw new System.InvalidOperationException("Missing FaceStencilLit shader.");

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.SetColor("_BaseColor", new Color(0.72f, 0.62f, 0.48f, 1f));
            material.SetFloat("_Smoothness", 0.18f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material CreateWaterMaterial()
        {
            Shader shader = Shader.Find("ObraDinnPrototype/ObraDinnWater");
            if (shader == null)
                throw new System.InvalidOperationException("Missing ObraDinnWater shader.");

            var material = AssetDatabase.LoadAssetAtPath<Material>(WaterMaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, WaterMaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.SetFloat("_BaseGray", 0.2f);
            material.SetFloat("_MidGray", 0.46f);
            material.SetFloat("_HighlightGray", 0.96f);
            material.SetFloat("_WaveScale", 5.5f);
            material.SetFloat("_WaveSpeed", 0.38f);
            material.SetFloat("_WaveStrength", 0.58f);
            material.SetFloat("_RippleScale", 26f);
            material.SetFloat("_RippleStrength", 0.26f);
            material.SetFloat("_LightStrength", 1.15f);
            material.SetFloat("_SpecularStrength", 2.1f);
            material.SetFloat("_SpecularPower", 22f);
            material.SetFloat("_GlintStrength", 0.85f);
            material.SetFloat("_FresnelStrength", 0.65f);
            material.SetFloat("_FresnelPower", 2.4f);
            material.SetFloat("_FoamScale", 7.5f);
            material.SetFloat("_FoamSpeed", 0.22f);
            material.SetFloat("_FoamThreshold", 0.48f);
            material.SetFloat("_FoamSoftness", 0.22f);
            material.SetFloat("_FoamStrength", 1.35f);
            material.SetFloat("_DarkLineStrength", 0.08f);
            material.SetFloat("_LineStrength", 0.9f);
            material.SetFloat("_LineFrequency", 4.2f);
            material.SetFloat("_DebugMode", 1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static GameObject CreateCube(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.position = position;
            obj.transform.localScale = scale;
            obj.GetComponent<MeshRenderer>().sharedMaterial = material;
            return obj;
        }

        static GameObject CreateSphere(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            obj.name = name;
            obj.transform.position = position;
            obj.transform.localScale = scale;
            obj.GetComponent<MeshRenderer>().sharedMaterial = material;
            return obj;
        }

        static GameObject CreateWaterPlane(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Plane);
            obj.name = name;
            obj.transform.position = position;
            obj.transform.localScale = scale;
            obj.GetComponent<MeshRenderer>().sharedMaterial = material;
            return obj;
        }

        static void CreateBarrelCluster(Material dark, Material deck)
        {
            for (int i = 0; i < 4; i++)
            {
                float x = -3.0f + (i % 2) * 0.72f;
                float z = -2.4f + (i / 2) * 0.72f;
                GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                barrel.name = "Barrel " + i;
                barrel.transform.position = new Vector3(x, 0.55f, z);
                barrel.transform.localScale = new Vector3(0.45f, 0.55f, 0.45f);
                barrel.GetComponent<MeshRenderer>().sharedMaterial = i % 2 == 0 ? dark : deck;
            }
        }

        static void CreateCharacter(string name, Vector3 position, Material coat, Material face)
        {
            GameObject root = new GameObject(name);
            root.transform.position = position;

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0f, 0f);
            body.transform.localScale = new Vector3(0.42f, 0.72f, 0.42f);
            body.GetComponent<MeshRenderer>().sharedMaterial = coat;

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 0.83f, 0f);
            head.transform.localScale = new Vector3(0.34f, 0.38f, 0.32f);
            head.GetComponent<MeshRenderer>().sharedMaterial = face;

            CreateCube("Nose", root.transform.position + new Vector3(0f, 0.85f, -0.22f), new Vector3(0.08f, 0.08f, 0.16f), face).transform.SetParent(root.transform, true);
            CreateCube("Hat Brim", root.transform.position + new Vector3(0f, 1.18f, 0f), new Vector3(0.62f, 0.07f, 0.56f), coat).transform.SetParent(root.transform, true);
            CreateCube("Hat Top", root.transform.position + new Vector3(0f, 1.34f, 0f), new Vector3(0.38f, 0.24f, 0.38f), coat).transform.SetParent(root.transform, true);
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
