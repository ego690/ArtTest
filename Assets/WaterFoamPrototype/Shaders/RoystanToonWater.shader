Shader "WaterFoamPrototype/RoystanToonWater"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.36, 0.92, 0.86, 1)
        _DeepColor ("Deep Color", Color) = (0.05, 0.32, 0.60, 1)
        _DepthMaxDistance ("Depth Max Distance", Range(0.05, 8)) = 2.4
        _Alpha ("Water Alpha", Range(0, 1)) = 0.78

        _FoamColor ("Foam Color", Color) = (0.96, 1.0, 0.90, 1)
        _FoamTex ("Foam Noise", 2D) = "white" {}
        _FoamMinDistance ("Foam Min Distance", Range(0.01, 3)) = 0.12
        _FoamMaxDistance ("Foam Max Distance", Range(0.01, 3)) = 0.78
        _FoamCutoff ("Foam Cutoff", Range(0, 1)) = 0.58
        _FoamSoftness ("Foam Softness", Range(0.001, 0.4)) = 0.055
        _FoamNoiseScale ("Foam Noise Scale", Range(0.1, 40)) = 7.0
        _FoamSpeed ("Foam Speed", Vector) = (0.055, 0.025, -0.035, 0.04)

        _SurfaceFoamAmount ("Open Water Foam Amount", Range(0, 1)) = 0.16
        _SurfaceFoamCutoff ("Open Water Foam Cutoff", Range(0, 1)) = 0.78

        _DistortionTex ("Distortion Texture", 2D) = "gray" {}
        _DistortionStrength ("Distortion Strength", Range(0, 0.25)) = 0.055
        _DistortionScale ("Distortion Scale", Range(0.1, 40)) = 4.0
        _DistortionSpeed ("Distortion Speed", Vector) = (0.03, -0.02, 0, 0)

        _NormalFoamStrength ("Normal Foam Strength", Range(0, 1)) = 0.65
        _ReflectionColor ("Rim Reflection Color", Color) = (0.75, 0.96, 1.0, 1)
        _ReflectionStrength ("Rim Reflection Strength", Range(0, 1)) = 0.22
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        ZWrite Off
        ZTest LEqual
        Cull Back
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "RoystanToonWater"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

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
                float4 screenPosition : TEXCOORD3;
            };

            TEXTURE2D(_FoamTex);
            SAMPLER(sampler_FoamTex);
            TEXTURE2D(_DistortionTex);
            SAMPLER(sampler_DistortionTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float _DepthMaxDistance;
                float _Alpha;

                float4 _FoamColor;
                float _FoamMinDistance;
                float _FoamMaxDistance;
                float _FoamCutoff;
                float _FoamSoftness;
                float _FoamNoiseScale;
                float4 _FoamSpeed;

                float _SurfaceFoamAmount;
                float _SurfaceFoamCutoff;

                float _DistortionStrength;
                float _DistortionScale;
                float4 _DistortionSpeed;

                float _NormalFoamStrength;
                float4 _ReflectionColor;
                float _ReflectionStrength;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.uv = input.uv;
                output.screenPosition = ComputeScreenPos(output.positionCS);
                return output;
            }

            float EyeDepthFromRaw(float rawDepth)
            {
                return LinearEyeDepth(rawDepth, _ZBufferParams);
            }

            float FoamSample(float2 uv)
            {
                float timeValue = _Time.y;
                float2 distortionUv = uv * _DistortionScale + _DistortionSpeed.xy * timeValue;
                float2 distortion = SAMPLE_TEXTURE2D(_DistortionTex, sampler_DistortionTex, distortionUv).rg * 2.0 - 1.0;

                float2 foamUvA = uv * _FoamNoiseScale + _FoamSpeed.xy * timeValue + distortion * _DistortionStrength;
                float2 foamUvB = uv * (_FoamNoiseScale * 0.53) + _FoamSpeed.zw * timeValue - distortion.yx * _DistortionStrength * 0.7;

                float foamA = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, foamUvA).r;
                float foamB = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, foamUvB).g;
                return saturate(foamA * 0.72 + foamB * 0.46);
            }

            float4 AlphaBlend(float4 top, float4 bottom)
            {
                float3 color = top.rgb * top.a + bottom.rgb * (1.0 - top.a);
                float alpha = top.a + bottom.a * (1.0 - top.a);
                return float4(color, alpha);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 screenUv = input.screenPosition.xy / input.screenPosition.w;
                float rawSceneDepth = SampleSceneDepth(screenUv);
                float sceneEyeDepth = EyeDepthFromRaw(rawSceneDepth);
                float waterEyeDepth = EyeDepthFromRaw(input.positionCS.z);
                float depthDifference = max(sceneEyeDepth - waterEyeDepth, 0.0);

                float depth01 = saturate(depthDifference / max(_DepthMaxDistance, 0.001));
                float3 waterColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, depth01);

                float3 surfaceNormalWS = normalize(input.normalWS);
                float3 sceneNormalWS = normalize(SampleSceneNormals(screenUv));
                float normalDifference = 1.0 - saturate(dot(surfaceNormalWS, sceneNormalWS));
                float foamDistance = lerp(_FoamMinDistance, _FoamMaxDistance, saturate(normalDifference * _NormalFoamStrength));
                float foamDepthDifference01 = saturate(depthDifference / max(foamDistance, 0.001));

                float2 waterUv = input.positionWS.xz;
                float foamNoise = FoamSample(waterUv);
                float shoreCutoff = foamDepthDifference01 * _FoamCutoff;
                float shoreFoam = smoothstep(shoreCutoff - _FoamSoftness, shoreCutoff + _FoamSoftness, foamNoise);

                float openFoam = smoothstep(_SurfaceFoamCutoff - _FoamSoftness, _SurfaceFoamCutoff + _FoamSoftness, foamNoise);
                openFoam *= _SurfaceFoamAmount * smoothstep(0.18, 1.0, depth01);

                float foam = saturate(max(shoreFoam, openFoam));

                float3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                float fresnel = pow(saturate(1.0 - dot(viewDirWS, surfaceNormalWS)), 3.0) * _ReflectionStrength;
                waterColor = lerp(waterColor, _ReflectionColor.rgb, fresnel);

                float alpha = saturate(_Alpha + foam * 0.18);
                float4 water = float4(waterColor, alpha);
                float4 foamColor = float4(_FoamColor.rgb, foam * _FoamColor.a);
                return AlphaBlend(foamColor, water);
            }
            ENDHLSL
        }
    }
}
