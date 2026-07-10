using System.Collections.Generic;
using System.IO;
using RoystanOutlineDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace RoystanOutlineDemo.Editor
{
    public static class RoystanOutlineDemoSetup
    {
        const string Root = "Assets/RoystanOutlineDemo";
        const string MaterialsRoot = Root + "/Materials";
        const string ScenesRoot = Root + "/Scenes";
        const string PipelineAssetPath = "Assets/Settings/PC_RPAsset.asset";
        const string BaseRendererPath = "Assets/Settings/PC_Renderer.asset";
        const string OutlineRendererPath = "Assets/Settings/PC_RoystanOutline_Renderer.asset";
        const string ShaderPath = Root + "/Shaders/RoystanScreenSpaceOutline.shader";
        const string MaskShaderPath = Root + "/Shaders/RoystanOutlineMask.shader";
        const string OutlineMaterialPath = MaterialsRoot + "/M_RoystanScreenSpaceOutline.mat";
        const string ScenePath = ScenesRoot + "/RoystanOutlineDemo.unity";
        const string OutlineLayerName = "Outline";
        const int OutlineLayerIndex = 8;
        const int UserLayerMaskBits = unchecked((int)0xFFFFFF00);
        const string MaskPassName = "Roystan Outline Layer Mask";
        const string OutlinePassName = "Roystan Screen Space Outline";

        [MenuItem("Tools/Roystan Outline Demo/Install Outline Renderer")]
        public static void InstallOutlineRenderer()
        {
            EnsureFolders();
            EnsureOutlineLayer();
            Material outlineMaterial = CreateOutlineMaterial();
            UniversalRendererData outlineRenderer = EnsureOutlineRenderer(outlineMaterial);
            EnsureRendererRegistered(outlineRenderer);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Roystan outline renderer installed at " + OutlineRendererPath);
        }

        [MenuItem("Tools/Roystan Outline Demo/Build Demo Scene")]
        public static void BuildDemoScene()
        {
            EnsureFolders();
            EnsureOutlineLayer();
            Material outlineMaterial = CreateOutlineMaterial();
            UniversalRendererData outlineRenderer = EnsureOutlineRenderer(outlineMaterial);
            int rendererIndex = EnsureRendererRegistered(outlineRenderer);
            CreateDemoScene(rendererIndex);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Roystan outline demo scene built at " + ScenePath);
        }

        static void EnsureFolders()
        {
            foreach (string folder in new[] { Root, MaterialsRoot, ScenesRoot })
            {
                if (AssetDatabase.IsValidFolder(folder))
                    continue;

                string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
                string name = Path.GetFileName(folder);
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        static void EnsureOutlineLayer()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            SerializedProperty layer = layers.GetArrayElementAtIndex(OutlineLayerIndex);
            if (layer.stringValue != OutlineLayerName)
            {
                layer.stringValue = OutlineLayerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static Material CreateOutlineMaterial()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
                shader = Shader.Find("Hidden/RoystanOutlineDemo/ScreenSpaceOutline");

            if (shader == null)
                throw new System.InvalidOperationException("Missing Roystan outline shader.");

            var material = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, OutlineMaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.name = "M_RoystanScreenSpaceOutline";
            material.SetColor("_OutlineColor", new Color(0.025f, 0.022f, 0.018f, 0.92f));
            material.SetFloat("_Scale", 1f);
            material.SetFloat("_DepthThreshold", 0.012f);
            material.SetFloat("_NormalThreshold", 0.36f);
            material.SetFloat("_DepthEdgeOpacity", 0.9f);
            material.SetFloat("_NormalEdgeOpacity", 0.28f);
            material.SetFloat("_DepthNormalThreshold", 0.48f);
            material.SetFloat("_DepthNormalThresholdScale", 5.5f);
            material.SetFloat("_DebugMode", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static UniversalRendererData EnsureOutlineRenderer(Material outlineMaterial)
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(OutlineRendererPath);
            if (renderer == null)
            {
                if (!AssetDatabase.CopyAsset(BaseRendererPath, OutlineRendererPath))
                    throw new System.InvalidOperationException("Could not create renderer at " + OutlineRendererPath);

                AssetDatabase.ImportAsset(OutlineRendererPath);
                renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(OutlineRendererPath);
            }

            if (renderer == null)
                throw new System.InvalidOperationException("Could not find renderer at " + OutlineRendererPath);

            renderer.name = "PC_RoystanOutline_Renderer";

            RoystanOutlineMaskRendererFeature maskFeature = null;
            FullScreenPassRendererFeature outlineFeature = null;
            foreach (ScriptableRendererFeature feature in renderer.rendererFeatures)
            {
                if (feature is RoystanOutlineMaskRendererFeature existingMask)
                    maskFeature = existingMask;

                if (feature is FullScreenPassRendererFeature fullScreen && feature.name == OutlinePassName)
                {
                    outlineFeature = fullScreen;
                }
            }

            maskFeature ??= AddMaskRendererFeature(renderer);
            ConfigureMaskPass(maskFeature);
            outlineFeature ??= AddRendererFeature(renderer, OutlinePassName);
            ConfigureOutlinePass(outlineFeature, outlineMaterial);
            MoveFeatureBefore(renderer, maskFeature, outlineFeature);
            MoveFeatureToEnd(renderer, outlineFeature);
            SyncRendererFeatureMap(renderer);

            EditorUtility.SetDirty(renderer);
            return renderer;
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

        static RoystanOutlineMaskRendererFeature AddMaskRendererFeature(UniversalRendererData renderer)
        {
            var feature = ScriptableObject.CreateInstance<RoystanOutlineMaskRendererFeature>();
            feature.name = MaskPassName;
            AssetDatabase.AddObjectToAsset(feature, renderer);
            renderer.rendererFeatures.Add(feature);
            SyncRendererFeatureMap(renderer);
            return feature;
        }

        static void ConfigureMaskPass(RoystanOutlineMaskRendererFeature feature)
        {
            feature.name = MaskPassName;
            var serializedFeature = new SerializedObject(feature);
            serializedFeature.Update();

            SerializedProperty maskShader = serializedFeature.FindProperty("maskShader");
            if (maskShader != null)
                maskShader.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>(MaskShaderPath);

            SerializedProperty outlineLayerMaskBits = serializedFeature.FindProperty("outlineLayerMaskBits");
            if (outlineLayerMaskBits != null)
                outlineLayerMaskBits.intValue = UserLayerMaskBits;

            serializedFeature.ApplyModifiedPropertiesWithoutUndo();
            feature.SetActive(true);
            EditorUtility.SetDirty(feature);
        }

        static void ConfigureOutlinePass(FullScreenPassRendererFeature feature, Material outlineMaterial)
        {
            feature.name = OutlinePassName;
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
            feature.fetchColorBuffer = true;
            feature.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal;
            feature.passMaterial = outlineMaterial;
            feature.passIndex = 0;
            feature.bindDepthStencilAttachment = false;
            feature.SetActive(true);
            EditorUtility.SetDirty(feature);
        }

        static void MoveFeatureToEnd(UniversalRendererData renderer, ScriptableRendererFeature feature)
        {
            renderer.rendererFeatures.Remove(feature);
            renderer.rendererFeatures.Add(feature);
        }

        static void MoveFeatureBefore(UniversalRendererData renderer, ScriptableRendererFeature feature, ScriptableRendererFeature before)
        {
            renderer.rendererFeatures.Remove(feature);
            int beforeIndex = renderer.rendererFeatures.IndexOf(before);
            if (beforeIndex < 0)
            {
                renderer.rendererFeatures.Add(feature);
                return;
            }

            renderer.rendererFeatures.Insert(beforeIndex, feature);
        }

        static void SyncRendererFeatureMap(UniversalRendererData renderer)
        {
            var serializedRenderer = new SerializedObject(renderer);
            serializedRenderer.Update();
            SerializedProperty map = serializedRenderer.FindProperty("m_RendererFeatureMap");
            if (map == null)
                return;

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

        static void CreateDemoScene(int rendererIndex)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "RoystanOutlineDemo";

            Material clay = CreateMaterial("M_DemoClay", new Color(0.78f, 0.55f, 0.42f), 0.28f);
            Material moss = CreateMaterial("M_DemoMoss", new Color(0.36f, 0.58f, 0.42f), 0.22f);
            Material slate = CreateMaterial("M_DemoSlate", new Color(0.43f, 0.50f, 0.58f), 0.34f);
            Material cream = CreateMaterial("M_DemoCream", new Color(0.86f, 0.78f, 0.58f), 0.18f);
            Material dark = CreateMaterial("M_DemoDark", new Color(0.18f, 0.17f, 0.15f), 0.45f);

            GameObject cameraObject = new GameObject("Main Camera");
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, 3.1f, -8.2f);
            camera.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
            camera.fieldOfView = 48f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.68f, 0.80f, 0.86f);
            camera.allowHDR = false;

            var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            cameraData.requiresDepthTexture = true;
            cameraData.requiresColorTexture = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
            cameraData.SetRenderer(rendererIndex);

            var smaaToggle = cameraObject.AddComponent<RoystanSmaaToggle>();
            var smaaToggleSerialized = new SerializedObject(smaaToggle);
            smaaToggleSerialized.FindProperty("smaaEnabled").boolValue = true;
            smaaToggleSerialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject lightObject = new GameObject("Key Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(52f, -32f, 18f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.38f, 0.42f, 0.45f);

            CreateCube("Ground", new Vector3(0f, -0.08f, 0f), new Vector3(10f, 0.16f, 8f), cream);
            CreateCube("Back Plate", new Vector3(0f, 1.6f, 2.9f), new Vector3(8.2f, 3.2f, 0.18f), slate);
            CreateCube("Left Block", new Vector3(-2.6f, 0.55f, -0.6f), new Vector3(1.1f, 1.1f, 1.1f), clay);
            CreateSphere("Sphere Depth Silhouette", new Vector3(-0.7f, 0.7f, -0.8f), new Vector3(1.35f, 1.35f, 1.35f), moss);
            CreateCylinder("Cylinder Normal Edge", new Vector3(1.35f, 0.8f, -0.7f), new Vector3(0.72f, 0.8f, 0.72f), slate);

            CreateStairStack(cream, dark);
            CreateSlantedPlane(slate);
            CreateCharacterLikeStack(clay, moss, dark);

            var labelRoot = new GameObject("Outline Test Geometry");
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                if (rootObject == cameraObject || rootObject == lightObject || rootObject == labelRoot)
                    continue;

                rootObject.transform.SetParent(labelRoot.transform, true);
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuild(ScenePath);
        }

        static Material CreateMaterial(string name, Color color, float smoothness)
        {
            string path = MaterialsRoot + "/" + name + ".mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new System.InvalidOperationException("Missing URP Lit shader.");

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

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
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

        static GameObject CreateCylinder(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            obj.name = name;
            obj.transform.position = position;
            obj.transform.localScale = scale;
            obj.GetComponent<MeshRenderer>().sharedMaterial = material;
            return obj;
        }

        static void CreateStairStack(Material treadMaterial, Material riserMaterial)
        {
            for (int i = 0; i < 5; i++)
            {
                float height = 0.16f + i * 0.22f;
                CreateCube("Depth Step " + i, new Vector3(-3.1f + i * 0.58f, height * 0.5f, 1.0f), new Vector3(0.58f, height, 1.2f), i % 2 == 0 ? treadMaterial : riserMaterial);
            }
        }

        static void CreateSlantedPlane(Material material)
        {
            GameObject ramp = CreateCube("Grazing Angle Ramp", new Vector3(2.65f, 0.42f, 0.75f), new Vector3(2.1f, 0.12f, 1.5f), material);
            ramp.transform.rotation = Quaternion.Euler(0f, 0f, -24f);
        }

        static void CreateCharacterLikeStack(Material bodyMaterial, Material headMaterial, Material accentMaterial)
        {
            GameObject root = new GameObject("Normal Detail Stack");
            root.transform.position = new Vector3(0.4f, 0f, 1.15f);

            GameObject body = CreateCylinder("Body", root.transform.position + new Vector3(0f, 0.64f, 0f), new Vector3(0.42f, 0.64f, 0.42f), bodyMaterial);
            body.transform.SetParent(root.transform, true);

            GameObject head = CreateSphere("Head", root.transform.position + new Vector3(0f, 1.45f, 0f), new Vector3(0.5f, 0.46f, 0.5f), headMaterial);
            head.transform.SetParent(root.transform, true);

            GameObject brim = CreateCube("Hat Brim", root.transform.position + new Vector3(0f, 1.77f, 0f), new Vector3(0.9f, 0.08f, 0.74f), accentMaterial);
            brim.transform.SetParent(root.transform, true);

            GameObject top = CreateCube("Hat Top", root.transform.position + new Vector3(0f, 1.98f, 0f), new Vector3(0.48f, 0.28f, 0.48f), accentMaterial);
            top.transform.SetParent(root.transform, true);
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
