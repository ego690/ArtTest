Shader "Hidden/SisyphusPrototype/VisionFullscreen"
{
    Properties
    {
        [Enum(Disabled,0,DistanceFade,1,LocalVisibility,2,BoulderOnly,3,FullBlack,4)]
        _SisyphusVisionMode ("Vision Mode", Float) = 0
        _SisyphusDarkColor ("Dark Color", Color) = (0, 0, 0, 1)
        _SisyphusDistanceStart ("Distance Fade Start", Float) = 18
        _SisyphusDistanceEnd ("Distance Fade End", Float) = 42
        _SisyphusVisibilityRadii ("Local Visibility Radii", Vector) = (7, 5, 12, 0)
        _SisyphusVisibilityFeather ("Local Visibility Feather", Range(0.001, 1)) = 0.2
        _SisyphusBoulderVisibilityRadius ("Boulder Visibility Radius", Float) = 3.02
        _SisyphusBoulderVisibilityFeather ("Boulder Visibility Feather", Range(0.01, 1)) = 0.28
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
            Name "SisyphusVision"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float _SisyphusVisionMode;
            float4 _SisyphusDarkColor;
            float _SisyphusDistanceStart;
            float _SisyphusDistanceEnd;
            float4 _SisyphusVisibilityRadii;
            float _SisyphusVisibilityFeather;
            float _SisyphusBoulderVisibilityRadius;
            float _SisyphusBoulderVisibilityFeather;
            float4x4 _SisyphusWorldToVisibility;

            float3 ReconstructPositionWS(float2 uv, float rawDepth)
            {
                #if UNITY_REVERSED_Z
                    float deviceDepth = rawDepth;
                #else
                    float deviceDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif

                return ComputeWorldSpacePosition(uv, deviceDepth, UNITY_MATRIX_I_VP);
            }

            float DistanceVisibility(float3 positionWS)
            {
                float fadeStart = max(_SisyphusDistanceStart, 0.0);
                float fadeEnd = max(_SisyphusDistanceEnd, fadeStart + 0.001);
                float cameraDistance = distance(GetCameraPositionWS(), positionWS);
                return 1.0 - smoothstep(fadeStart, fadeEnd, cameraDistance);
            }

            float LocalVisibility(float3 positionWS)
            {
                float3 localPosition = mul(_SisyphusWorldToVisibility, float4(positionWS, 1.0)).xyz;
                float3 radii = max(abs(_SisyphusVisibilityRadii.xyz), float3(0.001, 0.001, 0.001));
                float normalizedDistance = length(localPosition / radii);
                float feather = max(_SisyphusVisibilityFeather, 0.001);
                return 1.0 - smoothstep(1.0, 1.0 + feather, normalizedDistance);
            }

            float BoulderVisibility(float3 positionWS)
            {
                float3 localPosition = mul(_SisyphusWorldToVisibility, float4(positionWS, 1.0)).xyz;
                float radius = max(_SisyphusBoulderVisibilityRadius, 0.01);
                float feather = max(_SisyphusBoulderVisibilityFeather, 0.01);
                return 1.0 - smoothstep(radius, radius + feather, length(localPosition));
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord.xy;
                float4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);
                int mode = (int)round(_SisyphusVisionMode);

                if (mode <= 0)
                    return source;

                if (mode >= 4)
                    return float4(_SisyphusDarkColor.rgb, source.a);

                float rawDepth = SampleSceneDepth(uv);
                float3 positionWS = ReconstructPositionWS(uv, rawDepth);
                float visibility = mode == 1
                    ? DistanceVisibility(positionWS)
                    : mode == 2
                        ? LocalVisibility(positionWS)
                        : BoulderVisibility(positionWS);

                float3 color = lerp(_SisyphusDarkColor.rgb, source.rgb, saturate(visibility));
                return float4(color, source.a);
            }
            ENDHLSL
        }
    }
}
