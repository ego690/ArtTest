Shader "ObraDinnPrototype/ObraDinnWater"
{
    Properties
    {
        _BaseGray ("Base Gray", Range(0, 1)) = 0.2
        _MidGray ("Mid Gray", Range(0, 1)) = 0.46
        _HighlightGray ("Highlight Gray", Range(0, 1)) = 0.96
        _WaveScale ("Wave Scale", Range(0.1, 40)) = 5.5
        _WaveSpeed ("Wave Speed", Range(0, 4)) = 0.38
        _WaveStrength ("Wave Strength", Range(0, 1)) = 0.58
        _RippleScale ("Ripple Scale", Range(1, 120)) = 26
        _RippleStrength ("Ripple Strength", Range(0, 1)) = 0.26
        _LightStrength ("Light Strength", Range(0, 2)) = 1.15
        _SpecularStrength ("Specular Strength", Range(0, 4)) = 2.1
        _SpecularPower ("Specular Power", Range(1, 96)) = 22
        _GlintStrength ("Glint Strength", Range(0, 2)) = 0.85
        _FresnelStrength ("Fresnel Strength", Range(0, 2)) = 0.65
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 2.4
        _FoamScale ("Foam Scale", Range(0.1, 80)) = 7.5
        _FoamSpeed ("Foam Speed", Range(0, 4)) = 0.22
        _FoamThreshold ("Foam Threshold", Range(0, 1)) = 0.48
        _FoamSoftness ("Foam Softness", Range(0.001, 0.5)) = 0.22
        _FoamStrength ("Foam Strength", Range(0, 2)) = 1.35
        _DarkLineStrength ("Dark Line Strength", Range(0, 1)) = 0.08
        _LineStrength ("Line Strength", Range(0, 2)) = 0.9
        _LineFrequency ("Line Frequency", Range(0.1, 20)) = 4.2
        _DebugMode ("Debug Mode", Range(0, 3)) = 0
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
        ZTest LEqual
        Cull Back

        Pass
        {
            Name "ForwardGrayWater"
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
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float _BaseGray;
                float _MidGray;
                float _HighlightGray;
                float _WaveScale;
                float _WaveSpeed;
                float _WaveStrength;
                float _RippleScale;
                float _RippleStrength;
                float _LightStrength;
                float _SpecularStrength;
                float _SpecularPower;
                float _GlintStrength;
                float _FresnelStrength;
                float _FresnelPower;
                float _FoamScale;
                float _FoamSpeed;
                float _FoamThreshold;
                float _FoamSoftness;
                float _FoamStrength;
                float _DarkLineStrength;
                float _LineStrength;
                float _LineFrequency;
                float _DebugMode;
            CBUFFER_END

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
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float FractalNoise(float2 p)
            {
                float sum = 0.0;
                float amp = 0.5;
                float norm = 0.0;

                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    sum += ValueNoise(p) * amp;
                    norm += amp;
                    p = p * 2.07 + float2(17.7, 9.2);
                    amp *= 0.5;
                }

                return sum / max(norm, 0.0001);
            }

            float WaveHeight(float2 xz, float timeValue)
            {
                float2 p = xz * _WaveScale;
                float waveA = sin(p.x * 1.37 + p.y * 0.42 + timeValue * 1.31);
                float waveB = sin(p.x * -0.28 + p.y * 1.73 - timeValue * 1.07);
                float waveC = sin(dot(p, normalize(float2(0.72, -0.69))) * 2.12 + timeValue * 0.73);
                float ripple = FractalNoise(xz * _RippleScale + timeValue * float2(0.11, -0.07)) * 2.0 - 1.0;
                return (waveA * 0.48 + waveB * 0.34 + waveC * 0.18) * _WaveStrength + ripple * _RippleStrength;
            }

            float3 WaterNormal(float3 normalWS, float2 xz, float timeValue)
            {
                float stepSize = 0.08;
                float center = WaveHeight(xz, timeValue);
                float dx = WaveHeight(xz + float2(stepSize, 0.0), timeValue) - center;
                float dz = WaveHeight(xz + float2(0.0, stepSize), timeValue) - center;
                float3 waveNormal = normalize(float3(-dx, 1.0, -dz));
                return normalize(lerp(normalize(normalWS), waveNormal, saturate(_WaveStrength)));
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.uv = input.uv;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float timeValue = _Time.y * _WaveSpeed;
                float2 xz = input.positionWS.xz;
                float3 normalWS = WaterNormal(input.normalWS, xz, timeValue);

                float debugLine = 1.0 - smoothstep(0.0, 0.08, abs(frac((xz.x + xz.y * 0.62) * 2.0) - 0.5));
                if (_DebugMode > 2.5)
                    return float4(debugLine.xxx, 1.0);

                if (_DebugMode > 1.5)
                    return float4(0.85.xxx, 1.0);

                Light mainLight = GetMainLight();
                float3 lightDir = normalize(mainLight.direction);
                float lightBand = saturate(dot(normalWS, lightDir) * 0.5 + 0.5);
                lightBand = smoothstep(0.45, 0.92, lightBand) * _LightStrength;

                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                float3 halfDir = normalize(lightDir + viewDir);
                float specular = pow(saturate(dot(normalWS, halfDir)), _SpecularPower) * _SpecularStrength;
                float fresnel = pow(saturate(1.0 - dot(viewDir, normalWS)), _FresnelPower) * _FresnelStrength;

                float foamNoiseA = FractalNoise(xz * _FoamScale + _Time.y * _FoamSpeed * float2(0.13, 0.07));
                float foamNoiseB = FractalNoise(xz * (_FoamScale * 0.47) - _Time.y * _FoamSpeed * float2(0.09, 0.16) + 31.0);
                float foamField = lerp(foamNoiseA, foamNoiseB, 0.45);
                float foam = smoothstep(_FoamThreshold, saturate(_FoamThreshold + _FoamSoftness), foamField) * _FoamStrength;

                float2 moonDir = normalize(float2(0.35, 1.0));
                float alongMoon = dot(xz, moonDir);
                float acrossMoon = dot(xz, float2(-moonDir.y, moonDir.x));
                float streak = 1.0 - smoothstep(0.0, 2.6, abs(acrossMoon));
                float brokenStreak = smoothstep(0.42, 0.82, FractalNoise(float2(alongMoon * 0.55, acrossMoon * 3.6) + _Time.y * 0.08));
                float glint = streak * brokenStreak * _GlintStrength;

                float waveLineA = 1.0 - smoothstep(0.0, 0.17, abs(frac((xz.x + xz.y * 0.34) * _LineFrequency + foamNoiseA * 0.28) - 0.5));
                float waveLineB = 1.0 - smoothstep(0.0, 0.11, abs(frac((xz.x * -0.26 + xz.y) * (_LineFrequency * 1.43) + foamNoiseB * 0.37) - 0.5));
                float brightLines = saturate(max(waveLineA * 0.7, waveLineB * 0.45) * _LineStrength);

                float darkLines = 1.0 - smoothstep(0.02, 0.16, abs(frac((xz.x + xz.y * 0.37) * _WaveScale * 0.8 + foamNoiseB * 0.45) - 0.5));
                darkLines *= _DarkLineStrength;

                if (_DebugMode > 0.5)
                    return float4(saturate(max(brightLines, glint)).xxx, 1.0);

                float gray = lerp(_BaseGray, _MidGray, saturate(lightBand * 0.65 + fresnel * 0.25));
                gray = lerp(gray, _HighlightGray, saturate(foam + fresnel * 0.45 + specular + glint + brightLines));
                gray = saturate(gray - darkLines);
                return float4(gray.xxx, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
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

            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }

    }

    CustomEditor "ObraDinnPrototype.Editor.ObraDinnShaderGUI"
}
