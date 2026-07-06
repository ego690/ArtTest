using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ObraDinnPrototype
{
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    [RequireComponent(typeof(UniversalAdditionalCameraData))]
    public sealed class ObraDinnSceneRendererToggle : MonoBehaviour
    {
        [SerializeField, Tooltip("是否启用整套 Obra Dinn 专用渲染器。关闭后相机会切回普通 Renderer，所有 Obra Dinn 后处理都不再作用于画面。")] bool effectEnabled = true;
        [SerializeField, Tooltip("是否启用前后两层反相 Full Screen Pass。关闭后仍保留 Obra Dinn 抖动/描边，只是不再做先反相、处理后再反相的颜色流程。")] bool invertPassesEnabled = true;
        [SerializeField, Tooltip("是否启用独立的全屏 Bayer 抖动 pass。关闭后仍保留 Obra Dinn 主后处理，但不会再额外用 Bayer 阈值纹理重新筛选画面。")] bool bayerPassEnabled = false;
        [SerializeField, Tooltip("是否启用 Obra Dinn 主抖动 pass，也就是前面的蓝噪声/脸部矩阵抖动。关闭后会跳过 Other 和 Face Matrix 两个 pass，只保留后面的独立 Bayer pass。")] bool mainDitherPassesEnabled = true;
        [SerializeField, Tooltip("是否启用独立的全屏半色调抖动 pass。开启后会按屏幕区块求平均亮度，再用半色调纹理阈值替换成点阵式明暗效果。")] bool halftonePassEnabled = false;
        [SerializeField, Tooltip("半色调全屏 pass 使用的材质。脚本会把下面的区块分辨率参数实时写入这个材质。")] Material halftoneMaterial;
        [SerializeField, Tooltip("半色调分辨率模式。关闭时用 Block Size 指定每个小区块的像素大小；开启时用 Block Columns 指定横向区块数，纵向数量按 96:54 比例自动锁定。")] bool halftoneUseBlockCount = false;
        [SerializeField, Min(2f), Tooltip("半色调小区块大小，单位是屏幕像素。数值越小，取样区块越密，半色调纹理越细；数值越大，区块越粗。")] float halftoneBlockSize = 12f;
        [SerializeField, Min(1f), Tooltip("开启 Use Block Count 后，屏幕横向分成多少个半色调小区块。纵向数量会按 96:54 比例自动锁定，避免 atlas 格子被拉伸。")] float halftoneBlockColumns = 96f;
        [SerializeField, HideInInspector] float halftoneBlockRows = 54f;
        [SerializeField, Tooltip("关闭 Obra Dinn 效果时使用的 Renderer 索引。-1 表示使用当前 URP Asset 的默认 Renderer。")] int normalRendererIndex = -1;
        [SerializeField, Tooltip("开启 Obra Dinn 效果时使用的专用 Renderer 索引。当前项目里通常是 1，对应 PC_ObraDinn_Renderer。")] int obraDinnRendererIndex = 1;
        [SerializeField, Tooltip("包含 Obra Dinn pass 的专用 RendererData。脚本会在这里查找并开关主抖动、Pre/Post Invert 和 Bayer Renderer Feature。")] UniversalRendererData obraDinnRendererData;
        [SerializeField, Tooltip("非脸部/普通区域主抖动 pass 的 Renderer Feature 名称。只有你重命名了 Renderer Feature 时才需要改。")] string otherFeatureName = "Obra Dinn Other Dither Pass";
        [SerializeField, Tooltip("脸部矩阵抖动 pass 的 Renderer Feature 名称。只有你重命名了 Renderer Feature 时才需要改。")] string faceFeatureName = "Obra Dinn Face Matrix Pass";
        [SerializeField, Tooltip("渲染前反相 pass 的 Renderer Feature 名称。只有你重命名了 Renderer Feature 时才需要改。")] string preInvertFeatureName = "Obra Dinn Pre Invert Pass";
        [SerializeField, Tooltip("渲染后反相 pass 的 Renderer Feature 名称。只有你重命名了 Renderer Feature 时才需要改。")] string postInvertFeatureName = "Obra Dinn Post Invert Pass";
        [SerializeField, Tooltip("独立 Bayer 抖动 pass 的 Renderer Feature 名称。只有你重命名了 Renderer Feature 时才需要改。")] string bayerFeatureName = "Obra Dinn Bayer Dither Pass";
        [SerializeField, Tooltip("独立半色调抖动 pass 的 Renderer Feature 名称。只有你重命名了 Renderer Feature 时才需要改。")] string halftoneFeatureName = "Obra Dinn Halftone Dither Pass";
        [SerializeField, Tooltip("是否在编辑模式下也立刻应用开关。开启后不用进入 Play Mode，勾选字段就会更新相机 Renderer 和相关 pass 状态。")] bool applyInEditMode = true;

        UniversalAdditionalCameraData cameraData;
        const float HalftoneRowsPerColumn = 54f / 96f;

        public bool EffectEnabled
        {
            get => effectEnabled;
            set
            {
                if (effectEnabled == value)
                    return;

                effectEnabled = value;
                Apply();
            }
        }

        public bool InvertPassesEnabled
        {
            get => invertPassesEnabled;
            set
            {
                if (invertPassesEnabled == value)
                    return;

                invertPassesEnabled = value;
                Apply();
            }
        }

        public bool BayerPassEnabled
        {
            get => bayerPassEnabled;
            set
            {
                if (bayerPassEnabled == value)
                    return;

                bayerPassEnabled = value;
                Apply();
            }
        }

        public bool MainDitherPassesEnabled
        {
            get => mainDitherPassesEnabled;
            set
            {
                if (mainDitherPassesEnabled == value)
                    return;

                mainDitherPassesEnabled = value;
                Apply();
            }
        }

        public bool HalftonePassEnabled
        {
            get => halftonePassEnabled;
            set
            {
                if (halftonePassEnabled == value)
                    return;

                halftonePassEnabled = value;
                Apply();
            }
        }

        void OnEnable()
        {
            Apply();
        }

        void OnValidate()
        {
            normalRendererIndex = Mathf.Max(-1, normalRendererIndex);
            obraDinnRendererIndex = Mathf.Max(0, obraDinnRendererIndex);
            halftoneBlockSize = Mathf.Max(2f, halftoneBlockSize);
            halftoneBlockColumns = Mathf.Max(1f, halftoneBlockColumns);
            halftoneBlockRows = CalculateLockedHalftoneRows();

            if (isActiveAndEnabled)
                Apply();
        }

        [ContextMenu("Enable Obra Dinn Renderer")]
        public void EnableEffect()
        {
            EffectEnabled = true;
        }

        [ContextMenu("Disable Obra Dinn Renderer")]
        public void DisableEffect()
        {
            EffectEnabled = false;
        }

        [ContextMenu("Enable Invert Passes")]
        public void EnableInvertPasses()
        {
            InvertPassesEnabled = true;
        }

        [ContextMenu("Disable Invert Passes")]
        public void DisableInvertPasses()
        {
            InvertPassesEnabled = false;
        }

        [ContextMenu("Enable Bayer Pass")]
        public void EnableBayerPass()
        {
            BayerPassEnabled = true;
        }

        [ContextMenu("Disable Bayer Pass")]
        public void DisableBayerPass()
        {
            BayerPassEnabled = false;
        }

        [ContextMenu("Enable Main Dither Passes")]
        public void EnableMainDitherPasses()
        {
            MainDitherPassesEnabled = true;
        }

        [ContextMenu("Disable Main Dither Passes")]
        public void DisableMainDitherPasses()
        {
            MainDitherPassesEnabled = false;
        }

        [ContextMenu("Enable Halftone Pass")]
        public void EnableHalftonePass()
        {
            HalftonePassEnabled = true;
        }

        [ContextMenu("Disable Halftone Pass")]
        public void DisableHalftonePass()
        {
            HalftonePassEnabled = false;
        }

        public void Apply()
        {
            if (!Application.isPlaying && !applyInEditMode)
                return;

            if (cameraData == null && !TryGetComponent(out cameraData))
                return;

            int rendererIndex = effectEnabled ? obraDinnRendererIndex : normalRendererIndex;
            cameraData.SetRenderer(rendererIndex);
            ApplyHalftoneMaterialParameters();
            ApplyRendererFeatureToggles();

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(cameraData);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
        }

        void ApplyRendererFeatureToggles()
        {
            if (obraDinnRendererData == null)
                return;

            bool changed = false;
            bool mainDitherActive = effectEnabled && mainDitherPassesEnabled;
            bool invertActive = effectEnabled && invertPassesEnabled;
            bool bayerActive = effectEnabled && bayerPassEnabled;
            bool halftoneActive = effectEnabled && halftonePassEnabled;
            changed |= SetFeatureActive(otherFeatureName, mainDitherActive);
            changed |= SetFeatureActive(faceFeatureName, mainDitherActive);
            changed |= SetFeatureActive(preInvertFeatureName, invertActive);
            changed |= SetFeatureActive(postInvertFeatureName, invertActive);
            changed |= SetFeatureActive(bayerFeatureName, bayerActive);
            changed |= SetFeatureActive(halftoneFeatureName, halftoneActive);

#if UNITY_EDITOR
            if (changed && !Application.isPlaying)
                UnityEditor.EditorUtility.SetDirty(obraDinnRendererData);
#endif
        }

        void ApplyHalftoneMaterialParameters()
        {
            if (halftoneMaterial == null)
                return;

            halftoneMaterial.SetFloat("_UseBlockCount", halftoneUseBlockCount ? 1f : 0f);
            halftoneMaterial.SetFloat("_BlockSize", halftoneBlockSize);
            halftoneMaterial.SetFloat("_BlockColumns", halftoneBlockColumns);
            halftoneBlockRows = CalculateLockedHalftoneRows();
            halftoneMaterial.SetFloat("_BlockRows", halftoneBlockRows);

#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorUtility.SetDirty(halftoneMaterial);
#endif
        }

        float CalculateLockedHalftoneRows()
        {
            return Mathf.Max(1f, Mathf.Round(halftoneBlockColumns * HalftoneRowsPerColumn));
        }

        bool SetFeatureActive(string featureName, bool active)
        {
            if (string.IsNullOrWhiteSpace(featureName))
                return false;

            bool changed = false;
            foreach (ScriptableRendererFeature feature in obraDinnRendererData.rendererFeatures)
            {
                if (feature == null || feature.name != featureName || feature.isActive == active)
                    continue;

                feature.SetActive(active);
                changed = true;

#if UNITY_EDITOR
                if (!Application.isPlaying)
                    UnityEditor.EditorUtility.SetDirty(feature);
#endif
            }

            return changed;
        }
    }
}
