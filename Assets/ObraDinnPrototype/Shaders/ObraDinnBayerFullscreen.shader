Shader "Hidden/ObraDinnPrototype/BayerDitherFullscreen"
{
    Properties
    {
        _DarkColor ("Dark Color", Color) = (0.035, 0.032, 0.026, 1)
        _LightColor ("Light Color", Color) = (0.91, 0.84, 0.66, 1)
        _BayerTex ("Bayer Texture", 2D) = "gray" {}
        _PixelScale ("Pixel Scale", Range(1, 12)) = 1
        _Contrast ("Contrast", Range(0.25, 4)) = 1
        _Brightness ("Brightness", Range(-1, 1)) = 0
        _Gamma ("Gamma", Range(0.25, 4)) = 1
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
            Name "BayerThreshold"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_BayerTex);
            SAMPLER(sampler_BayerTex);

            float4 _DarkColor;
            float4 _LightColor;
            float4 _BayerTex_TexelSize;
            float _PixelScale;
            float _Contrast;
            float _Brightness;
            float _Gamma;
            float _ThresholdBias;
            float _Strength;

            float PosterizedLuma(float3 color)
            {
                float luma = dot(color, float3(0.2126, 0.7152, 0.0722));
                luma = (luma - 0.5) * _Contrast + 0.5 + _Brightness;
                luma = saturate(luma);
                luma = pow(luma, max(_Gamma, 0.001));
                return luma;
            }

            float BayerThreshold(float2 uv)
            {
                float2 screenSize = max(_BlitTexture_TexelSize.zw, float2(1.0, 1.0));
                float2 pixel = floor(uv * screenSize / max(_PixelScale, 1.0));
                float2 bayerSize = max(_BayerTex_TexelSize.zw, float2(1.0, 1.0));
                float2 bayerPixel = fmod(pixel, bayerSize);
                float2 bayerUv = (bayerPixel + 0.5) * _BayerTex_TexelSize.xy;
                return SAMPLE_TEXTURE2D(_BayerTex, sampler_BayerTex, bayerUv).r;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord.xy;
                float3 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).rgb;
                float luma = PosterizedLuma(source);
                float threshold = saturate(BayerThreshold(uv) + _ThresholdBias);
                float bit = step(threshold, luma);
                float3 bayerColor = lerp(_DarkColor.rgb, _LightColor.rgb, bit);
                float3 color = lerp(source, bayerColor, saturate(_Strength));
                return float4(color, 1);
            }
            ENDHLSL
        }
    }

    CustomEditor "ObraDinnPrototype.Editor.ObraDinnShaderGUI"
}
