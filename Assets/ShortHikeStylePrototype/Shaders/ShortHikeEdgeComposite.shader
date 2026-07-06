Shader "Hidden/ShortHikeStylePrototype/LowResEdgeComposite"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.16, 0.27, 0.30, 1)
        _EdgeThreshold ("Edge Threshold", Range(0.01, 1)) = 0.18
        _EdgeStrength ("Edge Strength", Range(0.25, 8)) = 3.2
        _OutlineOpacity ("Outline Opacity", Range(0, 1)) = 0.86
        _EdgeSampleDistance ("Edge Sample Distance", Range(0.5, 2.5)) = 1
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
            Name "ShortHikeLowResEdgeComposite"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _EdgeThreshold;
                float _EdgeStrength;
                float _OutlineOpacity;
                float _EdgeSampleDistance;
            CBUFFER_END

            float3 SampleScene(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, _BlitMipLevel).rgb;
            }

            float Luma(float3 color)
            {
                return dot(color, float3(0.299, 0.587, 0.114));
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 texel = _BlitTexture_TexelSize.xy * _EdgeSampleDistance;
                float2 uv = input.texcoord.xy;
                float3 center = SampleScene(uv);

                float3 left = SampleScene(uv + float2(-texel.x, 0.0));
                float3 right = SampleScene(uv + float2(texel.x, 0.0));
                float3 down = SampleScene(uv + float2(0.0, -texel.y));
                float3 up = SampleScene(uv + float2(0.0, texel.y));
                float3 downLeft = SampleScene(uv + float2(-texel.x, -texel.y));
                float3 upRight = SampleScene(uv + float2(texel.x, texel.y));
                float3 upLeft = SampleScene(uv + float2(-texel.x, texel.y));
                float3 downRight = SampleScene(uv + float2(texel.x, -texel.y));

                float colorEdge = 0.0;
                colorEdge = max(colorEdge, length(center - left));
                colorEdge = max(colorEdge, length(center - right));
                colorEdge = max(colorEdge, length(center - down));
                colorEdge = max(colorEdge, length(center - up));

                float sobelX = Luma(upRight + 2.0 * right + downRight - upLeft - 2.0 * left - downLeft);
                float sobelY = Luma(upLeft + 2.0 * up + upRight - downLeft - 2.0 * down - downRight);
                float lumaEdge = sqrt(sobelX * sobelX + sobelY * sobelY);

                float edge = max(colorEdge, lumaEdge);
                float threshold = max(_EdgeThreshold, 0.001);
                float edgeMask = saturate((edge - threshold) * _EdgeStrength);
                edgeMask = smoothstep(0.08, 0.92, edgeMask) * _OutlineOpacity;

                float3 color = lerp(center, _OutlineColor.rgb, edgeMask);
                return float4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
