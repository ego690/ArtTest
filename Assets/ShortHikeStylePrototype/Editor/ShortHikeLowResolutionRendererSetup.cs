using ShortHikeStylePrototype;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ShortHikeStylePrototype.Editor
{
    public static class ShortHikeLowResolutionRendererSetup
    {
        const string RendererPath = "Assets/Settings/PC_Renderer.asset";
        const string EdgeShaderPath = "Assets/ShortHikeStylePrototype/Shaders/ShortHikeEdgeComposite.shader";
        const string FeatureName = "Short Hike Low Resolution Present";

        [InitializeOnLoadMethod]
        static void InstallAfterReload()
        {
            EditorApplication.delayCall += EnsureInstalled;
        }

        [MenuItem("Tools/Short Hike Style Prototype/Install Low Resolution Renderer Feature")]
        public static void EnsureInstalled()
        {
            UniversalRendererData renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
                return;

            ShortHikeLowResolutionRendererFeature feature = null;
            foreach (ScriptableRendererFeature existing in renderer.rendererFeatures)
            {
                if (existing is ShortHikeLowResolutionRendererFeature lowResolutionFeature)
                {
                    feature = lowResolutionFeature;
                    break;
                }
            }

            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<ShortHikeLowResolutionRendererFeature>();
                feature.name = FeatureName;
                AssetDatabase.AddObjectToAsset(feature, renderer);
                renderer.rendererFeatures.Add(feature);
            }

            SerializedObject serializedFeature = new SerializedObject(feature);
            serializedFeature.Update();
            SerializedProperty shaderProperty = serializedFeature.FindProperty("edgeCompositeShader");
            if (shaderProperty != null && shaderProperty.objectReferenceValue == null)
                shaderProperty.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>(EdgeShaderPath);

            SerializedProperty scaleProperty = serializedFeature.FindProperty("fallbackLowResolutionScale");
            if (scaleProperty != null && scaleProperty.intValue <= 0)
                scaleProperty.intValue = 3;

            serializedFeature.ApplyModifiedPropertiesWithoutUndo();

            feature.name = FeatureName;
            feature.SetActive(true);
            SyncRendererFeatureMap(renderer);

            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssets();
        }

        static void SyncRendererFeatureMap(UniversalRendererData renderer)
        {
            SerializedObject serializedRenderer = new SerializedObject(renderer);
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
    }
}
