Shader "Hidden/ObraDinnPrototype/HalftoneDitherFullscreen"
{
    Properties
    {
        _DarkColor ("Dark Color", Color) = (0.035, 0.032, 0.026, 1)
        _LightColor ("Light Color", Color) = (0.91, 0.84, 0.66, 1)
        _HalftoneTex ("Halftone Atlas Texture", 2D) = "gray" {}
        _AtlasTileSize ("Atlas Tile Size", Range(8, 128)) = 32
        _AtlasLevels ("Atlas Levels", Range(2, 64)) = 17
        _BlockSize ("Block Size", Range(2, 96)) = 12
        [Toggle] _UseBlockCount ("Use Block Count", Float) = 0
        _BlockColumns ("Block Columns", Range(4, 320)) = 96
        _BlockRows ("Block Rows", Range(4, 180)) = 54
        _AverageRadius ("Average Radius", Range(0, 1)) = 1
        _Contrast ("Contrast", Range(0.25, 4)) = 1
        _Brightness ("Brightness", Range(-1, 1)) = 0
        _Gamma ("Gamma", Range(0.25, 4)) = 1
        _ThresholdScale ("Threshold Scale", Range(0.25, 4)) = 1.35
        _ThresholdBias ("Threshold Bias", Range(-0.5, 0.5)) = 0
        _Strength ("Strength", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "HalftoneAtlas"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_HalftoneTex);
            SAMPLER(sampler_HalftoneTex);

            float4 _DarkColor;
            float4 _LightColor;
            float4 _HalftoneTex_TexelSize;
            float _AtlasTileSize;
            float _AtlasLevels;
            float _BlockSize;
            float _UseBlockCount;
            float _BlockColumns;
            float _BlockRows;
            float _AverageRadius;
            float _Contrast;
            float _Brightness;
            float _Gamma;
            float _ThresholdScale;
            float _ThresholdBias;
            float _Strength;

            float Luma(float3 color)
            {
                return dot(color, float3(0.2126, 0.7152, 0.0722));
            }

            float ToneMap(float value)
            {
                value = (value - 0.5) * _Contrast + 0.5 + _Brightness;
                value = saturate(value);
                value = pow(value, max(_Gamma, 0.001));
                return value;
            }

            float AverageBlockLuma(float2 blockCenterUv, float2 blockUvSize)
            {
                float radius = saturate(_AverageRadius) * 0.45;
                float2 dx = float2(blockUvSize.x * radius, 0.0);
                float2 dy = float2(0.0, blockUvSize.y * radius);

                float3 sum = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, blockCenterUv).rgb;
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, blockCenterUv + dx).rgb;
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, blockCenterUv - dx).rgb;
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, blockCenterUv + dy).rgb;
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, blockCenterUv - dy).rgb;
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, blockCenterUv + dx + dy).rgb;
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, blockCenterUv + dx - dy).rgb;
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, blockCenterUv - dx + dy).rgb;
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, blockCenterUv - dx - dy).rgb;

                return ToneMap(Luma(sum / 9.0));
            }

            float SampleHalftoneAtlas(float tone, float2 localBlockUv)
            {
                float atlasTileSize = max(_AtlasTileSize, 1.0);
                float atlasLevels = max(_AtlasLevels, 2.0);
                float adjustedTone = saturate(tone * max(_ThresholdScale, 0.001) - _ThresholdBias);
                float atlasLevel = floor(adjustedTone * (atlasLevels - 1.0) + 0.5);
                atlasLevel = clamp(atlasLevel, 0.0, atlasLevels - 1.0);

                float2 patternPixel = floor(saturate(localBlockUv) * atlasTileSize);
                patternPixel = min(patternPixel, atlasTileSize - 1.0);
                float2 atlasPixel = float2(atlasLevel * atlasTileSize + patternPixel.x, patternPixel.y);
                float2 atlasSize = max(_HalftoneTex_TexelSize.zw, float2(1.0, 1.0));
                float2 atlasUv = (atlasPixel + 0.5) / atlasSize;
                return SAMPLE_TEXTURE2D(_HalftoneTex, sampler_HalftoneTex, atlasUv).r;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord.xy;
                float2 screenSize = max(_BlitTexture_TexelSize.zw, float2(1.0, 1.0));
                float2 blockPixelSize = max(_BlockSize.xx, float2(1.0, 1.0));
                if (_UseBlockCount > 0.5)
                {
                    float2 blockCount = max(float2(_BlockColumns, _BlockRows), float2(1.0, 1.0));
                    blockPixelSize = max(screenSize / blockCount, float2(1.0, 1.0));
                }

                float2 pixel = uv * screenSize;
                float2 blockPixel = floor(pixel / blockPixelSize);
                float2 localBlockPixel = pixel - blockPixel * blockPixelSize;
                float2 localBlockUv = localBlockPixel / blockPixelSize;
                float2 blockCenterPixel = (blockPixel + 0.5) * blockPixelSize;
                float2 blockCenterUv = saturate(blockCenterPixel / screenSize);
                float2 blockUvSize = blockPixelSize * _BlitTexture_TexelSize.xy;

                float blockLuma = AverageBlockLuma(blockCenterUv, blockUvSize);
                float bit = SampleHalftoneAtlas(blockLuma, localBlockUv);
                float3 halftoneColor = lerp(_DarkColor.rgb, _LightColor.rgb, bit);
                float3 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).rgb;
                float3 color = lerp(source, halftoneColor, saturate(_Strength));
                return float4(color, 1.0);
            }
            ENDHLSL
        }
    }

    CustomEditor "ObraDinnPrototype.Editor.ObraDinnShaderGUI"
}
