Shader "RoystanToonDemo/Toon URP"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [HDR] _AmbientColor ("Ambient Color", Color) = (0.32, 0.36, 0.42, 1)
        _ShadowColor ("Shadow Color", Color) = (0.48, 0.55, 0.68, 1)
        _ShadowThreshold ("Shadow Threshold", Range(-0.2, 0.8)) = 0.02
        _ShadowSoftness ("Shadow Softness", Range(0.001, 0.25)) = 0.025
        [HDR] _SpecularColor ("Specular Color", Color) = (1.0, 0.94, 0.76, 1)
        _SpecularSize ("Specular Size", Range(0.001, 1)) = 0.18
        _Glossiness ("Glossiness", Range(1, 128)) = 48
        [HDR] _RimColor ("Rim Color", Color) = (0.72, 0.92, 1.35, 1)
        _RimAmount ("Rim Amount", Range(0, 1)) = 0.58
        _RimThreshold ("Rim Lit-Side Bias", Range(0.01, 2)) = 0.38
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _AmbientColor;
                float4 _ShadowColor;
                float _ShadowThreshold;
                float _ShadowSoftness;
                float4 _SpecularColor;
                float _SpecularSize;
                float _Glossiness;
                float4 _RimColor;
                float _RimAmount;
                float _RimThreshold;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float3 lightDirWS = normalize(mainLight.direction);

                float ndl = dot(normalWS, lightDirWS);
                float shadowAttenuation = mainLight.shadowAttenuation;
                float litBand = smoothstep(
                    _ShadowThreshold - _ShadowSoftness,
                    _ShadowThreshold + _ShadowSoftness,
                    ndl * shadowAttenuation
                );

                float3 halfDirWS = normalize(lightDirWS + viewDirWS);
                float ndh = saturate(dot(normalWS, halfDirWS));
                float specularPower = pow(ndh * litBand, _Glossiness);
                float specularBand = smoothstep(_SpecularSize, _SpecularSize + 0.02, specularPower);

                float rimDot = 1.0 - saturate(dot(viewDirWS, normalWS));
                float litSide = pow(saturate(ndl), _RimThreshold);
                float rimBand = smoothstep(_RimAmount - 0.02, _RimAmount + 0.02, rimDot * litSide);

                float3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                float3 toonDiffuse = lerp(_ShadowColor.rgb, mainLight.color.rgb, litBand);
                float3 lighting = _AmbientColor.rgb + toonDiffuse;
                lighting += specularBand * _SpecularColor.rgb;
                lighting += rimBand * _RimColor.rgb;

                return float4(saturate(albedo * lighting), _BaseColor.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }
}
