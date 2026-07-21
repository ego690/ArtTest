Shader "StylizedGrassPrototype/Procedural Cloud Clusters"
{
    Properties
    {
        [NoScaleOffset] _CloudMask ("Cloud Mask (Black = Cloud)", 2D) = "white" {}
        [NoScaleOffset] _BlueNoiseTex ("Blue Noise Mask", 2D) = "white" {}
        _AlphaCutoff ("Mask Cutoff", Range(0, 1)) = 0.24
        _BlueNoiseScale ("Blue Noise Scale", Range(0.25, 32)) = 4
        _BlueNoiseBlend ("Blue Noise Blend", Range(0, 1)) = 1
        [HDR] _CloudShadowColor ("Cloud Shadow Color", Color) = (0.48, 0.60, 0.70, 1)
        [HDR] _CloudBaseColor ("Cloud Base Color", Color) = (0.78, 0.86, 0.92, 1)
        [HDR] _CloudHighlightColor ("Cloud Highlight Color", Color) = (1, 0.98, 0.92, 1)
        _ShadowThreshold ("Shadow Band Threshold", Range(0, 1)) = 0.42
        _HighlightThreshold ("Highlight Band Threshold", Range(0, 1)) = 0.73
        _CardEdgeFadeStart ("Card Edge Fade Start", Range(0, 1)) = 0.08
        _CardEdgeFadeEnd ("Card Edge Fade End", Range(0, 1)) = 0.35
        _ColorVariation ("Cloudlet Color Variation", Range(0, 0.5)) = 0.10
        _Brightness ("Cloud Brightness", Range(0, 3)) = 1.05
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
        }

        Cull Off
        ZWrite On
        AlphaToMask On

        Pass
        {
            Name "CloudForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CloudVert
            #pragma fragment CloudFrag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_CloudMask);
            SAMPLER(sampler_CloudMask);
            TEXTURE2D(_BlueNoiseTex);
            SAMPLER(sampler_BlueNoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _CloudShadowColor;
                float4 _CloudBaseColor;
                float4 _CloudHighlightColor;
                float _AlphaCutoff;
                float _BlueNoiseScale;
                float _BlueNoiseBlend;
                float _ShadowThreshold;
                float _HighlightThreshold;
                float _CardEdgeFadeStart;
                float _CardEdgeFadeEnd;
                float _ColorVariation;
                float _Brightness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 cardNormalOS : TANGENT;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float cloudletRandom : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                float3 positionWS : TEXCOORD4;
                float3 cardNormalWS : TEXCOORD5;
            };

            Varyings CloudVert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                output.positionWS = positionWS;
                output.cardNormalWS = normalize(
                    TransformObjectToWorldNormal(input.cardNormalOS.xyz));
                output.uv = input.uv;
                output.cloudletRandom = input.color.r;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 CloudFrag(Varyings input) : SV_Target
            {
                float4 maskSample = SAMPLE_TEXTURE2D(_CloudMask, sampler_CloudMask, input.uv);
                float luminance = dot(maskSample.rgb, float3(0.299, 0.587, 0.114));
                float cloudMask = maskSample.a * (1.0 - luminance);
                float3 blueNoisePosition = input.positionWS * _BlueNoiseScale
                    + input.cloudletRandom * float3(17.0, 31.0, 47.0);
                float3 blueNoiseWeights = pow(
                    abs(normalize(input.cardNormalWS)),
                    float3(8.0, 8.0, 8.0));
                blueNoiseWeights /= max(
                    blueNoiseWeights.x + blueNoiseWeights.y + blueNoiseWeights.z,
                    0.0001);
                float blueNoiseX = SAMPLE_TEXTURE2D(
                    _BlueNoiseTex,
                    sampler_BlueNoiseTex,
                    blueNoisePosition.yz).r;
                float blueNoiseY = SAMPLE_TEXTURE2D(
                    _BlueNoiseTex,
                    sampler_BlueNoiseTex,
                    blueNoisePosition.xz).r;
                float blueNoiseZ = SAMPLE_TEXTURE2D(
                    _BlueNoiseTex,
                    sampler_BlueNoiseTex,
                    blueNoisePosition.xy).r;
                float blueNoise = dot(
                    float3(blueNoiseX, blueNoiseY, blueNoiseZ),
                    blueNoiseWeights);
                float blueNoiseMask = lerp(1.0, blueNoise, _BlueNoiseBlend);
                float combinedMask = cloudMask * blueNoiseMask;
                float3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float viewFacing = abs(dot(
                    normalize(input.cardNormalWS),
                    viewDirectionWS));
                float edgeVisibility = smoothstep(
                    _CardEdgeFadeStart,
                    _CardEdgeFadeEnd,
                    viewFacing);
                float viewAdjustedCutoff = lerp(
                    1.001,
                    _AlphaCutoff,
                    edgeVisibility);
                clip(combinedMask - viewAdjustedCutoff);

                Light mainLight = GetMainLight();
                float wrappedLight = saturate(dot(input.normalWS, mainLight.direction) * 0.5 + 0.5);
                float baseBand = step(_ShadowThreshold, wrappedLight);
                float highlightBand = step(_HighlightThreshold, wrappedLight);

                float3 color = lerp(_CloudShadowColor.rgb, _CloudBaseColor.rgb, baseBand);
                color = lerp(color, _CloudHighlightColor.rgb, highlightBand);
                float variation = 1.0
                    + (input.cloudletRandom * 2.0 - 1.0) * _ColorVariation;
                color *= variation * _Brightness;
                color *= lerp(1.0.xxx, mainLight.color, 0.35);
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
