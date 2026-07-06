using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ObraDinnPrototype.Editor
{
    public sealed class ObraDinnShaderGUI : ShaderGUI
    {
        static readonly Dictionary<string, string> Tooltips = new Dictionary<string, string>
        {
            { "_DarkColor", "最终二值画面里的暗色。调它会改变黑色墨线、暗部和轮廓的颜色。" },
            { "_LightColor", "最终二值画面里的亮色。调它会改变纸张、亮部和背景的颜色。" },
            { "_DarkEdgeColor", "暗色描边模式使用的边缘颜色。关闭 Use Light Edge Color 时，深度/法线检测到的轮廓会被推向这个颜色。" },
            { "_LightEdgeColor", "亮色描边模式使用的边缘颜色。开启 Use Light Edge Color 时，深度/法线检测到的轮廓会被推向这个颜色。" },
            { "_BlueNoiseTex", "蓝噪声纹理，用于让亮暗临界区域的抖动更细碎、更少规则网格感。" },
            { "_BayerTex", "Bayer 阈值纹理。全屏 Bayer pass 会按屏幕像素位置读取对应纹理格子，再和画面亮度比较决定输出暗色或亮色。" },
            { "_HalftoneTex", "半色调色阶纹理条。全屏半色调 pass 会按区块平均亮度选择其中一格，再用格子内对应位置的黑白点阵替换画面。" },
            { "_AtlasTileSize", "半色调色阶纹理里每一格的像素尺寸。当前生成图是每格 32 像素，通常不用手动改。" },
            { "_AtlasLevels", "半色调色阶纹理的横向格数。当前生成图是 17 格，从黑底白点到中心半覆盖，再到白底黑点。" },
            { "_PixelScale", "像素化采样尺度。数值越大，抖动和边缘越粗，更接近低分辨率屏幕。" },
            { "_BlockSize", "半色调区块大小，单位近似为屏幕像素。数值越大，每个点阵单元越大，画面越粗。" },
            { "_UseBlockCount", "半色调分块模式开关。关闭时使用 Block Size 控制每块像素大小；开启时使用 Block Columns 和 Block Rows 指定画面横纵分成多少块。" },
            { "_BlockColumns", "开启 Use Block Count 后的横向区块数量。数值越大，单个区块越小，点阵越细。" },
            { "_BlockRows", "开启 Use Block Count 后的纵向区块数量。数值越大，单个区块越小，点阵越细。" },
            { "_AverageRadius", "区块平均采样半径。数值越大，平均颜色越接近模糊后的画面；数值越小，越接近区块中心颜色。" },
            { "_Contrast", "进入二值化前的灰度对比度。越大，亮暗分界越硬；越小，中间灰更宽。" },
            { "_Brightness", "进入二值化前的整体亮度偏移。提高会让更多区域进入亮色，降低会让画面更暗。" },
            { "_Gamma", "灰度曲线。小于 1 会提亮中间调，大于 1 会压暗中间调。" },
            { "_ThresholdScale", "半色调 atlas 选格前的亮度系数。数值越大，同样灰度会选择更亮的格子，亮部更多、暗部更少。" },
            { "_BlueNoiseWeight", "主 Obra Dinn pass 的蓝噪声阈值权重。0 接近固定 0.5 阈值，1 完全使用蓝噪声阈值；Bayer 已独立到全屏 pass。" },
            { "_WorldDitherWeight", "物体表面世界空间抖动参与程度。越高，抖动越像贴在物体上，而不是贴在屏幕上。" },
            { "_WorldDitherScale", "世界空间抖动尺寸。数值越大，世界空间图案越密；数值越小，块更大。" },
            { "_WorldTriplanarSharpness", "世界空间三向投影的轴向锐度。越高，图案更贴合主要朝向，但轴切换可能更明显。" },
            { "_ToneFieldStrength", "大面积明暗场扰动强度。提高后平坦灰度也会出现轻微起伏，减少死板大片纯色。" },
            { "_ToneFieldScale", "明暗场扰动尺寸。数值越大，起伏越密；数值越小，起伏更大块。" },
            { "_ToneFieldHatchStrength", "斜向排线参与强度。提高会让非脸部区域出现更明显的版画式线性纹理。" },
            { "_ToneFieldHatchAngle", "斜向排线角度，单位是弧度。改变它可以旋转排线方向。" },
            { "_FaceMatrixScale", "脸部 stencil 区域的矩阵抖动缩放。数值越大，脸部矩阵图案越密。" },
            { "_FaceMatrixOffset", "脸部矩阵抖动的 X/Y 偏移。用于让脸部图案和身体/背景错开，避免连成一片。" },
            { "_FaceBlueNoiseWeight", "脸部区域蓝噪声混入比例。越高脸部越碎，越低越保持规则矩阵感。" },
            { "_ThresholdBias", "二值化阈值整体偏移。提高会让像素更容易变暗，降低会让画面更容易变亮。" },
            { "_EdgeStrength", "深度/法线边缘描线强度。越高，模型轮廓和折角越明显。" },
            { "_UseLightEdgeColor", "边缘线颜色开关。关闭时使用 Dark Edge Color；开启时使用 Light Edge Color，更适合暗底上的 Obra Dinn 式浅色轮廓。" },
            { "_DepthEdgeScale", "深度差异对描边的贡献。主要控制物体前后遮挡、轮廓边缘的线条。" },
            { "_NormalEdgeScale", "法线差异对描边的贡献。主要控制模型表面折角、硬边和形体转折。" },
            { "_RotationOffset", "由相机旋转补偿脚本写入的采样偏移。一般不要手动改，用于减轻抖动糊在屏幕上的感觉。" },
            { "_OffsetStrength", "相机旋转补偿写入 shader 后的最终强度。0 关闭补偿，1 使用脚本计算值。" },
            { "_Strength", "全屏 Bayer pass 的混合强度。0 保留原画面，1 完全使用 Bayer 阈值化后的暗色/亮色画面。" },

            { "_BaseGray", "水体暗部基础灰度。降低会让水更沉、更容易被 Obra Dinn 后处理压成暗色。" },
            { "_MidGray", "水体中间灰度。控制大部分水面的基础可见度。" },
            { "_HighlightGray", "水面高光灰度。提高会让浪尖、闪光线更容易在二值化后留下亮线。" },
            { "_WaveScale", "大波浪噪声尺度。数值越大，波形越密；数值越小，波形更宽。" },
            { "_WaveSpeed", "水面动画速度。越高，波浪和线索移动越快。" },
            { "_WaveStrength", "大波浪强度。提高会让水面明暗起伏更明显，也更容易产生可见线条。" },
            { "_RippleScale", "细波纹尺度。数值越大，细节越密集。" },
            { "_RippleStrength", "细波纹强度。提高会增加水面碎线和细小亮暗变化。" },
            { "_LightStrength", "主光对水面的影响。越高，受光方向的水面越亮。" },
            { "_SpecularStrength", "镜面高光强度。提高会让水面更容易出现强亮线。" },
            { "_SpecularPower", "镜面高光锐度。越高，高光越窄越尖；越低，高光更宽。" },
            { "_GlintStrength", "闪光线强度。提高会让水面局部亮线更明显。" },
            { "_FresnelStrength", "掠射角亮边强度。提高会让视线擦过水面处更亮。" },
            { "_FresnelPower", "掠射角亮边曲线。越高，亮边越集中在极端角度。" },
            { "_FoamScale", "泡沫/碎线噪声尺度。控制水面白色碎线的大小。" },
            { "_FoamSpeed", "泡沫/碎线移动速度。越高，水面线条漂移越快。" },
            { "_FoamThreshold", "泡沫出现阈值。降低会出现更多白线，提高会只保留最强的线索。" },
            { "_FoamSoftness", "泡沫边缘软硬。越小越硬、越像清晰线条；越大越柔和。" },
            { "_FoamStrength", "泡沫整体强度。提高会让白色水纹更容易穿过后处理保留下来。" },
            { "_DarkLineStrength", "水面暗线强度。提高会在亮线之外增加一些暗色波纹线。" },
            { "_LineStrength", "程序化水纹线强度。越高，水面方向性线条越明显。" },
            { "_LineFrequency", "程序化水纹线频率。越高，线条越密。" },
            { "_DebugMode", "水材质调试显示。0 为正常；较高数值会显示中间结果，方便判断线条/高光是否生成。" },

            { "_BaseColor", "脸部 stencil 材质的基础颜色。它也负责把该区域写入 stencil，让脸部使用单独的矩阵抖动 pass。" },
            { "_Smoothness", "脸部材质的高光平滑度。越高，高光越集中，也更容易影响后处理明暗分界。" },
        };

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            foreach (MaterialProperty property in properties)
            {
                Tooltips.TryGetValue(property.name, out string tooltip);
                var label = string.IsNullOrEmpty(tooltip)
                    ? new GUIContent(property.displayName)
                    : new GUIContent(property.displayName, tooltip);

                materialEditor.ShaderProperty(property, label);
            }
        }
    }
}
