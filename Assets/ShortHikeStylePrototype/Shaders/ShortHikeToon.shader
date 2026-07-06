Shader "ShortHikeStylePrototype/Toon"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _ShadowColor ("Shadow Color", Color) = (0.48, 0.55, 0.46, 1)
        _HighlightColor ("Highlight Color", Color) = (1.12, 1.08, 0.92, 1)
        _BandSoftness ("Band Softness", Range(0.01, 0.45)) = 0.12
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 color : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _ShadowColor;
                float4 _HighlightColor;
                float4 _FogColor;
                float _BandSoftness;
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
                output.color = input.color;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                float ndl = dot(normalWS, normalize(mainLight.direction)) * 0.5 + 0.5;

                float low = smoothstep(0.24 - _BandSoftness, 0.24 + _BandSoftness, ndl);
                float high = smoothstep(0.78 - _BandSoftness, 0.78 + _BandSoftness, ndl);
                float3 ramp = lerp(_ShadowColor.rgb, _BaseColor.rgb, low);
                ramp = lerp(ramp, _HighlightColor.rgb, high * 0.42);

                float3 vertexColor = lerp(float3(1, 1, 1), input.color.rgb, saturate(_VertexColorStrength));
                float3 color = ramp * vertexColor * mainLight.color.rgb;
                color += _BaseColor.rgb * SampleSH(normalWS) * 0.35;

                float distanceToCamera = distance(GetCameraPositionWS(), input.positionWS);
                float fog = saturate((distanceToCamera - _FogStart) / max(_FogEnd - _FogStart, 0.001));
                color = lerp(color, _FogColor.rgb, fog * fog);

                return float4(saturate(color), 1.0);
            }
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
