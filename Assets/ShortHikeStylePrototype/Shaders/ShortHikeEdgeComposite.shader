Shader "Hidden/ShortHikeStylePrototype/LowResEdgeComposite"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.025, 0.022, 0.018, 0.85)
        _EdgeThreshold ("Edge Threshold", Range(0.001, 0.5)) = 0.08
        _EdgeStrength ("Edge Strength", Range(0, 4)) = 1.35
        _OutlineOpacity ("Outline Opacity", Range(0, 1)) = 0.8
        _EdgeSampleDistance ("Edge Sample Distance", Range(0.5, 4)) = 1
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

            TEXTURE2D_X(_RoystanOutlineMaskTexture);
            SAMPLER(sampler_RoystanOutlineMaskTexture);

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

            float SampleOutlineMask(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X_LOD(_RoystanOutlineMaskTexture, sampler_RoystanOutlineMaskTexture, uv, 0).r;
            }

            float ColorEdge(float3 center, float3 sampleColor)
            {
                float3 delta = abs(center - sampleColor);
                return max(delta.r, max(delta.g, delta.b));
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord.xy;
                float3 center = SampleScene(uv);
                float2 texel = _BlitTexture_TexelSize.xy * max(_EdgeSampleDistance, 0.5);

                float2 leftUv = uv + float2(-texel.x, 0.0);
                float2 rightUv = uv + float2(texel.x, 0.0);
                float2 downUv = uv + float2(0.0, -texel.y);
                float2 upUv = uv + float2(0.0, texel.y);

                float edge = 0.0;
                edge = max(edge, ColorEdge(center, SampleScene(leftUv)));
                edge = max(edge, ColorEdge(center, SampleScene(rightUv)));
                edge = max(edge, ColorEdge(center, SampleScene(downUv)));
                edge = max(edge, ColorEdge(center, SampleScene(upUv)));
                edge = smoothstep(_EdgeThreshold, _EdgeThreshold * 2.0, edge * max(_EdgeStrength, 0.0));

                float outlineMask = SampleOutlineMask(uv);
                outlineMask = max(outlineMask, SampleOutlineMask(leftUv));
                outlineMask = max(outlineMask, SampleOutlineMask(rightUv));
                outlineMask = max(outlineMask, SampleOutlineMask(downUv));
                outlineMask = max(outlineMask, SampleOutlineMask(upUv));

                float alpha = edge * step(0.5, outlineMask) * saturate(_OutlineOpacity) * saturate(_OutlineColor.a);
                float3 color = lerp(center, _OutlineColor.rgb, alpha);

                return float4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
