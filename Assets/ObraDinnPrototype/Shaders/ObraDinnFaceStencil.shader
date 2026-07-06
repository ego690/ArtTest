Shader "ObraDinnPrototype/FaceStencilLit"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.72, 0.62, 0.48, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.18
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

            Stencil
            {
                Ref 9
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Smoothness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float ndl = saturate(dot(normal, mainLight.direction));
                float wrapped = saturate(ndl * 0.82 + 0.18);
                float3 diffuse = _BaseColor.rgb * mainLight.color * wrapped * mainLight.shadowAttenuation;
                float3 ambient = _BaseColor.rgb * SampleSH(normal) * 0.75;

                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                float3 halfDir = normalize(mainLight.direction + viewDir);
                float specular = pow(saturate(dot(normal, halfDir)), lerp(8.0, 64.0, _Smoothness)) * _Smoothness;

                return float4(diffuse + ambient + specular.xxx * 0.18, _BaseColor.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            Stencil
            {
                Ref 9
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
    CustomEditor "ObraDinnPrototype.Editor.ObraDinnShaderGUI"
}
