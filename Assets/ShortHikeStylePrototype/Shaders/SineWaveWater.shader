Shader "ShortHikeStylePrototype/Water/SineWave"
{
    Properties
    {
        _ShallowColor ("Wave Crest Color", Color) = (0.18, 0.72, 0.92, 1)
        _DeepColor ("Wave Trough Color", Color) = (0.02, 0.22, 0.52, 1)
        _FresnelColor ("Fresnel Color", Color) = (0.72, 0.96, 1.0, 1)
        _Alpha ("Water Alpha", Range(0, 1)) = 0.9

        _WaveAmplitude ("Wave Amplitude", Range(0, 2)) = 0.28
        _WaveFrequency ("Wave Frequency (Cycles Per Unit)", Range(0.001, 1)) = 0.08
        _WaveSpeed ("Wave Speed (Cycles Per Second)", Range(-2, 2)) = 0.12
        _WaveDirection ("Wave Direction XZ", Vector) = (1, 0.35, 0, 0)

        _FresnelStrength ("Fresnel Strength", Range(0, 1)) = 0.3
        _HighlightStrength ("Sun Highlight Strength", Range(0, 1)) = 0.24
        _HighlightPower ("Sun Highlight Sharpness", Range(2, 128)) = 36
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
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            static const float TwoPi = 6.28318530718;

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
                float4 _ShallowColor;
                float4 _DeepColor;
                float4 _FresnelColor;
                float _Alpha;
                float _WaveAmplitude;
                float _WaveFrequency;
                float _WaveSpeed;
                float4 _WaveDirection;
                float _FresnelStrength;
                float _HighlightStrength;
                float _HighlightPower;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float2 direction = normalize(_WaveDirection.xy + float2(0.0001, 0.0001));
                float phase = (dot(positionWS.xz, direction) * _WaveFrequency + _Time.y * _WaveSpeed) * TwoPi;
                float waveHeight = sin(phase) * _WaveAmplitude;
                positionWS.y += waveHeight;

                float slope = cos(phase) * _WaveAmplitude * _WaveFrequency * TwoPi;
                float3 normalWS = normalize(float3(-direction.x * slope, 1.0, -direction.y * slope));

                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = normalWS;
                output.waveHeight = waveHeight;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float amplitude = max(_WaveAmplitude, 0.0001);
                float height01 = saturate(input.waveHeight / amplitude * 0.5 + 0.5);
                float3 waterColor = lerp(_DeepColor.rgb, _ShallowColor.rgb, height01);

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirectionWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                Light mainLight = GetMainLight();
                float lightAmount = saturate(dot(normalWS, mainLight.direction));
                float toonLight = lerp(0.62, 1.05, smoothstep(0.25, 0.65, lightAmount));
                waterColor *= toonLight * mainLight.color;

                float3 halfDirection = normalize(mainLight.direction + viewDirectionWS);
                float highlight = pow(saturate(dot(normalWS, halfDirection)), _HighlightPower) * _HighlightStrength;
                float fresnel = pow(saturate(1.0 - dot(normalWS, viewDirectionWS)), 3.0) * _FresnelStrength;
                waterColor += mainLight.color * highlight;
                waterColor = lerp(waterColor, _FresnelColor.rgb, fresnel);

                return float4(waterColor, _Alpha);
            }
            ENDHLSL
        }
    }
}
