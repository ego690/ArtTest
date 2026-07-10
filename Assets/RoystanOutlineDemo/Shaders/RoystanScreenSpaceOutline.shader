Shader "Hidden/RoystanOutlineDemo/ScreenSpaceOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.03, 0.025, 0.02, 1)
        _Scale ("Scale", Range(1, 6)) = 2
        _DepthThreshold ("Depth Threshold", Range(0.001, 0.1)) = 0.012
        _NormalThreshold ("Normal Threshold", Range(0.001, 1)) = 0.18
        _DepthEdgeOpacity ("Depth Edge Opacity", Range(0, 1)) = 1
        _NormalEdgeOpacity ("Normal Edge Opacity", Range(0, 1)) = 0.35
        _DepthNormalThreshold ("Depth Normal Threshold", Range(0, 1)) = 0.55
        _DepthNormalThresholdScale ("Depth Normal Threshold Scale", Range(1, 12)) = 6
        _DebugMode ("Debug Mode", Range(0, 3)) = 0
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
            Name "RoystanScreenSpaceOutline"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            TEXTURE2D_X(_RoystanOutlineMaskTexture);
            SAMPLER(sampler_RoystanOutlineMaskTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _Scale;
                float _DepthThreshold;
                float _NormalThreshold;
                float _DepthEdgeOpacity;
                float _NormalEdgeOpacity;
                float _DepthNormalThreshold;
                float _DepthNormalThresholdScale;
                float _DebugMode;
            CBUFFER_END

            float SceneDepth01(float2 uv)
            {
                return Linear01Depth(SampleSceneDepth(uv), _ZBufferParams);
            }

            float3 SceneNormal(float2 uv)
            {
                return normalize(SampleSceneNormals(uv));
            }

            float SceneMask(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_RoystanOutlineMaskTexture, sampler_RoystanOutlineMaskTexture, uv).r;
            }

            float DepthSurfaceMultiplier(float2 uv, float3 viewNormalWS)
            {
                float rawDepth = SampleSceneDepth(uv);

                #if UNITY_REVERSED_Z
                    float deviceDepth = rawDepth;
                #else
                    float deviceDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif

                float3 positionWS = ComputeWorldSpacePosition(uv, deviceDepth, UNITY_MATRIX_I_VP);
                float3 viewDirectionWS = normalize(GetCameraPositionWS() - positionWS);
                float grazing = 1.0 - saturate(dot(normalize(viewNormalWS), viewDirectionWS));
                float normalThreshold01 = saturate((grazing - _DepthNormalThreshold) / max(1.0 - _DepthNormalThreshold, 0.0001));
                return 1.0 + normalThreshold01 * max(_DepthNormalThresholdScale - 1.0, 0.0);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord.xy;
                float2 texel = _BlitTexture_TexelSize.xy;
                float halfFloor = floor(_Scale * 0.5);
                float halfCeil = ceil(_Scale * 0.5);

                float2 bottomLeft = uv - texel * halfFloor;
                float2 topRight = uv + texel * halfCeil;
                float2 bottomRight = uv + float2(texel.x * halfCeil, -texel.y * halfFloor);
                float2 topLeft = uv + float2(-texel.x * halfFloor, texel.y * halfCeil);
                float2 left = uv + float2(-texel.x * halfCeil, 0.0);
                float2 right = uv + float2(texel.x * halfCeil, 0.0);
                float2 down = uv + float2(0.0, -texel.y * halfFloor);
                float2 up = uv + float2(0.0, texel.y * halfCeil);

                float outlineMask = SceneMask(uv);
                outlineMask = max(outlineMask, SceneMask(bottomLeft));
                outlineMask = max(outlineMask, SceneMask(topRight));
                outlineMask = max(outlineMask, SceneMask(bottomRight));
                outlineMask = max(outlineMask, SceneMask(topLeft));
                outlineMask = max(outlineMask, SceneMask(left));
                outlineMask = max(outlineMask, SceneMask(right));
                outlineMask = max(outlineMask, SceneMask(down));
                outlineMask = max(outlineMask, SceneMask(up));

                float depth0 = SceneDepth01(bottomLeft);
                float depth1 = SceneDepth01(topRight);
                float depth2 = SceneDepth01(bottomRight);
                float depth3 = SceneDepth01(topLeft);

                float depthFiniteDifference0 = depth1 - depth0;
                float depthFiniteDifference1 = depth3 - depth2;
                float edgeDepth = sqrt(depthFiniteDifference0 * depthFiniteDifference0 + depthFiniteDifference1 * depthFiniteDifference1);

                float3 normal0 = SceneNormal(bottomLeft);
                float3 normal1 = SceneNormal(topRight);
                float3 normal2 = SceneNormal(bottomRight);
                float3 normal3 = SceneNormal(topLeft);

                float3 normalFiniteDifference0 = normal1 - normal0;
                float3 normalFiniteDifference1 = normal3 - normal2;
                float edgeNormal = sqrt(dot(normalFiniteDifference0, normalFiniteDifference0) + dot(normalFiniteDifference1, normalFiniteDifference1));

                float depthMultiplier = DepthSurfaceMultiplier(uv, SceneNormal(uv));
                float depthThreshold = _DepthThreshold * max(depth0, 0.0001) * depthMultiplier;
                float depthEdgeMask = edgeDepth > depthThreshold ? 1.0 : 0.0;
                float normalEdgeMask = edgeNormal > _NormalThreshold ? 1.0 : 0.0;
                float edge = max(depthEdgeMask * _DepthEdgeOpacity, normalEdgeMask * _NormalEdgeOpacity) * step(0.5, outlineMask);

                float3 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).rgb;

                if (_DebugMode > 2.5)
                    return float4(edge.xxx, 1.0);

                if (_DebugMode > 1.5)
                    return float4(normalEdgeMask.xxx, 1.0);

                if (_DebugMode > 0.5)
                    return float4(depthEdgeMask.xxx, 1.0);

                float alpha = saturate(_OutlineColor.a) * edge;
                float3 color = lerp(source, _OutlineColor.rgb, alpha);
                return float4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
