Shader "ShortHikeStylePrototype/SineWaveWater"
{
    Properties
    {
        _TroughColor ("Trough Color", Color) = (0.02, 0.22, 0.58, 1)
        _CrestColor ("Crest Color", Color) = (0.08, 0.72, 0.92, 1)
        _FresnelColor ("Fresnel Color", Color) = (0.72, 0.96, 1.0, 1)
        _Alpha ("Water Alpha", Range(0, 1)) = 0.92

        _WaveAmplitude ("Wave Size", Range(0, 2)) = 0.35
        _WaveFrequency ("Wave Frequency (Cycles / Unit)", Range(0.01, 2)) = 0.08
        _WaveSpeed ("Wave Speed (Cycles / Second)", Range(-3, 3)) = 0.22
        _WaveDirection ("Wave Direction", Vector) = (1, 0.35, 0, 0)
        _FresnelStrength ("Fresnel Strength", Range(0, 1)) = 0.35
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
            Name "SineWaveWater"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float waveHeight : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _TroughColor;
                float4 _CrestColor;
                float4 _FresnelColor;
                float _Alpha;
                float _WaveAmplitude;
                float _WaveFrequency;
                float _WaveSpeed;
                float4 _WaveDirection;
                float _FresnelStrength;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float2 direction = _WaveDirection.xy;
                direction *= rsqrt(max(dot(direction, direction), 0.0001));

                const float twoPi = 6.28318530718;
                float phase = (dot(positionWS.xz, direction) * _WaveFrequency + _Time.y * _WaveSpeed) * twoPi;
                float waveHeight = sin(phase) * _WaveAmplitude;
                positionWS.y += waveHeight;

                float slope = cos(phase) * _WaveAmplitude * _WaveFrequency * twoPi;
                float3 normalWS = normalize(float3(-slope * direction.x, 1.0, -slope * direction.y));

                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = normalWS;
                output.waveHeight = waveHeight;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                float diffuse = 0.42 + saturate(dot(normalWS, mainLight.direction)) * 0.58;
                float height01 = _WaveAmplitude > 0.0001
                    ? saturate(input.waveHeight / (_WaveAmplitude * 2.0) + 0.5)
                    : 0.5;

                float3 color = lerp(_TroughColor.rgb, _CrestColor.rgb, height01);
                color *= lerp(1.0, mainLight.color, 0.65) * diffuse;

                float3 viewDirectionWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                float fresnel = pow(saturate(1.0 - dot(viewDirectionWS, normalWS)), 3.0) * _FresnelStrength;
                color = lerp(color, _FresnelColor.rgb, fresnel);
                return float4(color, _Alpha);
            }
            ENDHLSL
        }
    }
}
