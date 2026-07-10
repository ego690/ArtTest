Shader "ShortHikeStylePrototype/Toon"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [HDR] _AmbientColor ("Ambient Color", Color) = (0.18, 0.22, 0.26, 1)
        _AmbientStrength ("Ambient Strength", Range(0, 1)) = 0.35
        _ShadowColor ("Shadow Color", Color) = (0.48, 0.55, 0.46, 1)
        _ShadowStrength ("Realtime Shadow Strength", Range(0, 1)) = 0.75
        _HighlightColor ("Highlight Color", Color) = (1.12, 1.08, 0.92, 1)
        _HighlightStrength ("Highlight Strength", Range(0, 1)) = 0.42
        _BandSoftness ("Band Softness", Range(0.01, 0.45)) = 0.12
        [HDR] _SpecularColor ("Specular Color", Color) = (1.05, 0.96, 0.70, 1)
        _SpecularStrength ("Specular Strength", Range(0, 1)) = 0.18
        _SpecularSize ("Specular Size", Range(0.001, 1)) = 0.16
        _Glossiness ("Glossiness", Range(1, 128)) = 48
        [HDR] _RimColor ("Rim Color", Color) = (0.55, 0.78, 1.12, 1)
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.18
        _RimAmount ("Rim Amount", Range(0, 1)) = 0.58
        _RimThreshold ("Rim Lit-Side Bias", Range(0.01, 2)) = 0.38
        _VertexColorStrength ("Vertex Color Strength", Range(0, 1)) = 1
        _FogColor ("Fog Color", Color) = (0.61, 0.80, 0.88, 1)
        _FogStart ("Fog Start", Float) = 11
        _FogEnd ("Fog End", Float) = 31
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        ZWrite On
        Cull Back

        Pass
        {
            Name "ShortHikeToon"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 color : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _AmbientColor;
                float4 _ShadowColor;
                float4 _HighlightColor;
                float4 _FogColor;
                float _AmbientStrength;
                float _ShadowStrength;
                float _HighlightStrength;
                float _BandSoftness;
                float4 _SpecularColor;
                float _SpecularStrength;
                float _SpecularSize;
                float _Glossiness;
                float4 _RimColor;
                float _RimStrength;
                float _RimAmount;
                float _RimThreshold;
                float _VertexColorStrength;
                float _FogStart;
                float _FogEnd;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float3 lightDirWS = normalize(mainLight.direction);
                float ndlSigned = dot(normalWS, lightDirWS);
                float shadow = lerp(1.0, mainLight.shadowAttenuation, saturate(_ShadowStrength));
                float ndl = (ndlSigned * 0.5 + 0.5) * shadow;

                float low = smoothstep(0.24 - _BandSoftness, 0.24 + _BandSoftness, ndl);
                float high = smoothstep(0.78 - _BandSoftness, 0.78 + _BandSoftness, ndl);
                float3 ramp = lerp(_ShadowColor.rgb, _BaseColor.rgb, low);
                ramp = lerp(ramp, _HighlightColor.rgb, high * saturate(_HighlightStrength));

                float3 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                float3 vertexColor = lerp(float3(1, 1, 1), input.color.rgb, saturate(_VertexColorStrength));
                float3 surfaceColor = baseMap * vertexColor;

                float3 halfDirWS = normalize(lightDirWS + viewDirWS);
                float ndh = saturate(dot(normalWS, halfDirWS));
                float specularPower = pow(ndh * low, _Glossiness);
                float specularBand = smoothstep(_SpecularSize, _SpecularSize + 0.02, specularPower);

                float rimDot = 1.0 - saturate(dot(viewDirWS, normalWS));
                float litSide = pow(saturate(ndlSigned), _RimThreshold);
                float rimBand = smoothstep(_RimAmount - 0.02, _RimAmount + 0.02, rimDot * litSide);

                float3 color = ramp * surfaceColor * mainLight.color.rgb;
                color += _BaseColor.rgb * surfaceColor * SampleSH(normalWS) * saturate(_AmbientStrength);
                color += _BaseColor.rgb * surfaceColor * _AmbientColor.rgb * saturate(_AmbientStrength);
                color += _SpecularColor.rgb * specularBand * saturate(_SpecularStrength) * shadow;
                color += _RimColor.rgb * rimBand * saturate(_RimStrength);

                float distanceToCamera = distance(GetCameraPositionWS(), input.positionWS);
                float fog = saturate((distanceToCamera - _FogStart) / max(_FogEnd - _FogStart, 0.001));
                color = lerp(color, _FogColor.rgb, fog * fog);

                return float4(saturate(color), 1.0);
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
            #include "Packages/com.unity.render-pipelines.universal/Shaders/UnlitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }
}
