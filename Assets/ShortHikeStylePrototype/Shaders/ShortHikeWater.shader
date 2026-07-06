Shader "ShortHikeStylePrototype/Water"
{
    Properties
    {
        _DeepColor ("Deep Color", Color) = (0.12, 0.46, 0.67, 0.88)
        _ShallowColor ("Shallow Color", Color) = (0.38, 0.78, 0.78, 0.82)
        _FoamColor ("Foam Color", Color) = (0.84, 0.94, 0.82, 1)
        _WaveScale ("Wave Scale", Range(0.5, 12)) = 4
        _WaveSpeed ("Wave Speed", Range(0, 2)) = 0.34
        _WaveStrength ("Wave Strength", Range(0, 1)) = 0.18
        _FoamScale ("Foam Scale", Range(1, 32)) = 4.8
        _FoamAmount ("Foam Amount", Range(0, 1)) = 0.22
        _TimeOffset ("Time Offset", Float) = 0
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
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off

        Pass
        {
            Name "ShortHikeWater"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
                float2 uv : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _DeepColor;
                float4 _ShallowColor;
                float4 _FoamColor;
                float _WaveScale;
                float _WaveSpeed;
                float _WaveStrength;
                float _FoamScale;
                float _FoamAmount;
                float _TimeOffset;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionOS = input.positionOS.xyz;
                float2 p = positionOS.xz;
                float wave = sin((p.x + p.y * 0.6 + _TimeOffset * _WaveSpeed) * _WaveScale) * 0.025;
                wave += sin((p.x * -0.45 + p.y + _TimeOffset * _WaveSpeed * 1.4) * _WaveScale * 0.72) * 0.018;
                positionOS.y += wave * _WaveStrength;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(positionOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.uv = input.uv;
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.positionWS.xz;
                float low = ValueNoise(p * 0.22 + _TimeOffset * _WaveSpeed * 0.05);
                float bandA = sin((p.x * 0.44 + p.y * 0.18 + _TimeOffset * _WaveSpeed * 0.42) * _FoamScale) * 0.5 + 0.5;
                float bandB = sin((p.x * -0.16 + p.y * 0.52 + _TimeOffset * _WaveSpeed * 0.55) * _FoamScale * 0.72) * 0.5 + 0.5;
                float bands = max(smoothstep(0.78, 0.94, bandA), smoothstep(0.84, 0.97, bandB) * 0.58);
                bands *= lerp(0.62, 1.0, low);

                float shoreRadius = length(float2(p.x / 4.8, p.y / 4.25));
                float shoreMask = smoothstep(0.66, 0.94, shoreRadius) * (1.0 - smoothstep(1.22, 1.9, shoreRadius));
                float openWaterMask = (1.0 - smoothstep(2.0, 3.6, shoreRadius)) * 0.16;
                float foam = bands * saturate(shoreMask + openWaterMask);

                float depthTint = saturate(length(input.uv - 0.5) * 1.5);
                float3 color = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthTint);
                color = lerp(color, _FoamColor.rgb, foam * _FoamAmount);
                return float4(color, lerp(_ShallowColor.a, _DeepColor.a, depthTint));
            }
            ENDHLSL
        }
    }
}
