Shader "StylizedGrassPrototype/Procedural Leaf Clusters"
{
    Properties
    {
        [NoScaleOffset] _LeafMask ("Leaf Mask (Black = Leaf)", 2D) = "white" {}
        _AlphaCutoff ("White Background Cutoff", Range(0, 1)) = 0.45

        [HDR] _ShadowColor ("Dark Band Color", Color) = (0.025, 0.11, 0.035, 1)
        [HDR] _BaseColor ("Middle Band Color", Color) = (0.11, 0.34, 0.075, 1)
        [HDR] _HighlightColor ("Light Band Color", Color) = (0.45, 0.76, 0.16, 1)
        [HDR] _NoiseColor ("Noise Mix Color", Color) = (0.20, 0.46, 0.07, 1)
        [HDR] _BottomColor ("Crown Bottom Color", Color) = (0.018, 0.07, 0.025, 1)

        _BrightMultiplier ("Diffuse Light Multiplier", Range(1, 3)) = 1.7
        _DarkMultiplier ("Diffuse Dark Multiplier", Range(0, 2)) = 1.0
        _DarkThreshold ("Dark Constant Ramp", Range(0, 1)) = 0.48
        _LightThreshold ("Light Constant Ramp", Range(0, 1.7)) = 1.04
        _ShadowReceiveStrength ("Main Light Shadow Strength", Range(0, 1)) = 0.45

        _NoiseScale ("Object Noise Scale", Range(0.05, 8)) = 1.35
        _NoiseLow ("Noise Linear Ramp Low", Range(0, 1)) = 0.30
        _NoiseHigh ("Noise Linear Ramp High", Range(0, 1)) = 0.72
        _NoiseBlend ("Noise Color Amount", Range(0, 1)) = 0.34
        _NoiseOffset ("Noise Offset", Vector) = (0, 0, 0, 0)

        _CrownBottom ("Crown Gradient Bottom", Float) = 1.4
        _CrownTop ("Crown Gradient Top", Float) = 4.8
        _BottomRampStart ("Bottom Linear Ramp Start", Range(0, 1)) = 0.08
        _BottomRampEnd ("Bottom Linear Ramp End", Range(0, 1)) = 0.62
        _FinalHueShift ("Final Hue Shift", Range(-1, 1)) = 0
        _FinalSaturation ("Final Saturation", Range(0, 2)) = 1
        _FinalValue ("Final Value", Range(0, 2)) = 1
        [HideInInspector] _TreeHueShift ("Per-Tree Hue Shift", Range(-1, 1)) = 0
        [HideInInspector] _TreeSaturation ("Per-Tree Saturation", Range(0, 2)) = 1
        [HideInInspector] _TreeValue ("Per-Tree Value", Range(0, 2)) = 1
        [HideInInspector] _DistributionSeed ("Shared Wind Seed", Float) = 17
        [HideInInspector] _WindLargeScale ("Shared Wind Large Scale", Float) = 0.03
        [HideInInspector] _WindDetailScale ("Shared Wind Detail Scale", Float) = 0.15
        [HideInInspector] _WindDetailStrength ("Shared Wind Detail Strength", Float) = 0.25
        [HideInInspector] _WindNoiseAxisScale ("Shared Wind Axis Scale", Vector) = (0.45, 2.4, 0, 0)
        [HideInInspector] _WindSpeed ("Shared Wind Speed", Float) = 0.12
        [HideInInspector] _WindDirection ("Shared Wind Direction", Vector) = (0.8, 0, 0.35, 0)
        [HideInInspector] _WindTimeOffset ("Shared Wind Time Offset", Float) = 0
        [HideInInspector] _WindBendStrength ("Shared Wind Bend Strength", Float) = 0.82
        [HideInInspector] _WindHeightInfluence ("Shared Wind Height Influence", Float) = 0.32
        [HideInInspector] _WindRotationStrength ("Shared Wind Rotation Strength", Float) = 0.28
        [HideInInspector] _WindBendCurve ("Shared Wind Bend Curve", Float) = 1.8
        [HideInInspector] _LeafWindFrequencyMultiplier ("Leaf Wind Frequency", Float) = 1.5
        [HideInInspector] _LeafWindAmplitudeMultiplier ("Leaf Wind Amplitude", Float) = 1
        [HideInInspector] _LeafWindDisplacementMultiplier ("Leaf Wind Displacement", Float) = 0.35
        [HideInInspector] _LeafWindRotationMultiplier ("Leaf Wind Rotation", Float) = 0.5
        [HideInInspector] _WindEnabled ("Shared Wind Enabled", Float) = 1
        _EmissionStrength ("Emission Output Strength", Range(0, 3)) = 1.08
        _FixedNormalOS ("Fixed Object Normal", Vector) = (0, 0, 1, 0)
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

        HLSLINCLUDE
        #pragma target 3.5

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        TEXTURE2D(_LeafMask);
        SAMPLER(sampler_LeafMask);

        CBUFFER_START(UnityPerMaterial)
            float4 _ShadowColor;
            float4 _BaseColor;
            float4 _HighlightColor;
            float4 _NoiseColor;
            float4 _BottomColor;
            float4 _NoiseOffset;
            float4 _FixedNormalOS;
            float4 _WindDirection;
            float4 _WindNoiseAxisScale;
            float _AlphaCutoff;
            float _BrightMultiplier;
            float _DarkMultiplier;
            float _DarkThreshold;
            float _LightThreshold;
            float _ShadowReceiveStrength;
            float _NoiseScale;
            float _NoiseLow;
            float _NoiseHigh;
            float _NoiseBlend;
            float _CrownBottom;
            float _CrownTop;
            float _BottomRampStart;
            float _BottomRampEnd;
            float _FinalHueShift;
            float _FinalSaturation;
            float _FinalValue;
            float _TreeHueShift;
            float _TreeSaturation;
            float _TreeValue;
            float _DistributionSeed;
            float _WindLargeScale;
            float _WindDetailScale;
            float _WindDetailStrength;
            float _WindSpeed;
            float _WindTimeOffset;
            float _WindBendStrength;
            float _WindHeightInfluence;
            float _WindRotationStrength;
            float _WindBendCurve;
            float _LeafWindFrequencyMultiplier;
            float _LeafWindAmplitudeMultiplier;
            float _LeafWindDisplacementMultiplier;
            float _LeafWindRotationMultiplier;
            float _WindEnabled;
            float _EmissionStrength;
        CBUFFER_END

        float SampleInverseLeafMask(float2 uv)
        {
            float3 sampleColor = SAMPLE_TEXTURE2D(_LeafMask, sampler_LeafMask, uv).rgb;
            float luminance = dot(sampleColor, float3(0.299, 0.587, 0.114));
            return 1.0 - luminance;
        }

        float Hash31(float3 position)
        {
            position = frac(position * 0.1031);
            position += dot(position, position.yzx + 33.33);
            return frac((position.x + position.y) * position.z);
        }

        float ObjectValueNoise(float3 position)
        {
            float3 cell = floor(position);
            float3 local = frac(position);
            float3 blend = local * local * (3.0 - 2.0 * local);

            float x00 = lerp(Hash31(cell), Hash31(cell + float3(1, 0, 0)), blend.x);
            float x10 = lerp(Hash31(cell + float3(0, 1, 0)), Hash31(cell + float3(1, 1, 0)), blend.x);
            float x01 = lerp(Hash31(cell + float3(0, 0, 1)), Hash31(cell + float3(1, 0, 1)), blend.x);
            float x11 = lerp(Hash31(cell + float3(0, 1, 1)), Hash31(cell + float3(1, 1, 1)), blend.x);
            return lerp(lerp(x00, x10, blend.y), lerp(x01, x11, blend.y), blend.z);
        }

        float LeafWindHash21(float2 cell)
        {
            float3 p3 = frac(float3(cell.xyx) * float3(0.1031, 0.1030, 0.0973));
            p3 += dot(p3, p3.yzx + 33.33);
            return frac((p3.x + p3.y) * p3.z);
        }

        float LeafWindValueNoise(float2 position)
        {
            float2 cell = floor(position);
            float2 local = frac(position);
            float2 blend = local * local * local * (local * (local * 6.0 - 15.0) + 10.0);

            float bottomLeft = LeafWindHash21(cell);
            float bottomRight = LeafWindHash21(cell + float2(1.0, 0.0));
            float topLeft = LeafWindHash21(cell + float2(0.0, 1.0));
            float topRight = LeafWindHash21(cell + float2(1.0, 1.0));
            float bottom = lerp(bottomLeft, bottomRight, blend.x);
            float top = lerp(topLeft, topRight, blend.x);
            return lerp(bottom, top, blend.y);
        }

        float LeafProceduralWindNoise(float2 position)
        {
            float2 seed = float2(_DistributionSeed * 0.193, _DistributionSeed * 0.347);
            float large = LeafWindValueNoise(position * 2.1 + seed);
            float medium = LeafWindValueNoise(position * 5.3 + seed.yx + float2(13.1, 29.4));
            float detail = LeafWindValueNoise(position * 11.7 - seed + float2(47.2, 5.9));
            return saturate(large * 0.62 + medium * 0.28 + detail * 0.1);
        }

        float SampleSharedWindNoise(float3 positionWS)
        {
            float2 windDirection = normalize(_WindDirection.xz + float2(0.0001, 0.0001));
            float windTime = (_Time.y * _LeafWindFrequencyMultiplier + _WindTimeOffset)
                * _WindSpeed;
            float2 advection = windDirection * windTime;
            float2 axisScale = max(abs(_WindNoiseAxisScale.xy), float2(0.001, 0.001));
            float2 stretchedPosition = positionWS.xz * axisScale;
            float largeWave = LeafProceduralWindNoise(
                stretchedPosition * _WindLargeScale + advection);
            float smallDisturbance = LeafProceduralWindNoise(
                stretchedPosition * _WindDetailScale
                + advection * 1.37
                + float2(0.37, 0.71));
            return saturate(
                (largeWave + smallDisturbance * _WindDetailStrength)
                / max(1.0 + _WindDetailStrength, 0.001));
        }

        float3 RotateLeafVector(float3 inputVector, float3 axis, float angle)
        {
            float sine;
            float cosine;
            sincos(angle, sine, cosine);
            return inputVector * cosine
                + cross(axis, inputVector) * sine
                + axis * dot(axis, inputVector) * (1.0 - cosine);
        }

        float3 ApplyLeafWind(float3 positionOS, float3 pivotOS, float2 uv)
        {
            float3 positionWS = TransformObjectToWorld(positionOS);
            [branch]
            if (_WindEnabled < 0.5)
                return positionWS;

            float3 pivotWS = TransformObjectToWorld(pivotOS);
            float wind = SampleSharedWindNoise(pivotWS);
            float wave = (wind - 0.5) * 2.0;

            float2 directionXZ = normalize(_WindDirection.xz + float2(0.0001, 0.0001));
            float3 directionWS = float3(directionXZ.x, 0.0, directionXZ.y);
            float3 worldUp = float3(0.0, 1.0, 0.0);
            float3 swayAxis = normalize(cross(worldUp, directionWS));

            float3 offsetWS = positionWS - pivotWS;
            float swayAngle = wave
                * _WindRotationStrength
                * _LeafWindRotationMultiplier
                * _LeafWindAmplitudeMultiplier;
            offsetWS = RotateLeafVector(offsetWS, swayAxis, swayAngle);
            offsetWS = RotateLeafVector(
                offsetWS,
                worldUp,
                swayAngle * 0.32);

            float curve = pow(saturate(uv.y), max(_WindBendCurve, 0.01));
            float vertexInfluence = lerp(0.45, 1.0, curve);
            float flattenAmount = saturate(1.0 - wind);
            float bendDistance = _WindBendStrength
                * _LeafWindDisplacementMultiplier
                * _LeafWindAmplitudeMultiplier
                * lerp(0.22, 1.0, flattenAmount);
            float verticalDisplacement = wave
                * _WindHeightInfluence
                * _LeafWindDisplacementMultiplier
                * _LeafWindAmplitudeMultiplier
                * 0.35;

            return pivotWS
                + offsetWS
                + directionWS * (bendDistance * vertexInfluence)
                + worldUp * (verticalDisplacement * vertexInfluence);
        }

        float3 LeafRgbToHsv(float3 color)
        {
            float4 k = float4(0.0, -0.3333333, 0.6666667, -1.0);
            float4 p = lerp(float4(color.bg, k.wz), float4(color.gb, k.xy), step(color.b, color.g));
            float4 q = lerp(float4(p.xyw, color.r), float4(color.r, p.yzx), step(p.x, color.r));
            float difference = q.x - min(q.w, q.y);
            float epsilon = 1e-10;
            return float3(
                abs(q.z + (q.w - q.y) / (6.0 * difference + epsilon)),
                difference / (q.x + epsilon),
                q.x);
        }

        float3 LeafHsvToRgb(float3 hsv)
        {
            float3 p = abs(frac(hsv.xxx + float3(0.0, 0.6666667, 0.3333333)) * 6.0 - 3.0);
            return hsv.z * lerp(1.0.xxx, saturate(p - 1.0), hsv.y);
        }

        float3 ApplyFinalHsv(float3 color)
        {
            float3 hsv = LeafRgbToHsv(max(color, 0.0));
            hsv.x = frac(hsv.x + _FinalHueShift + _TreeHueShift);
            hsv.y = saturate(hsv.y * _FinalSaturation * _TreeSaturation);
            hsv.z = max(0.0, hsv.z * _FinalValue * _TreeValue);
            return LeafHsvToRgb(hsv);
        }
        ENDHLSL

        Pass
        {
            Name "LeafForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex LeafVert
            #pragma fragment LeafFrag
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 pivotOS : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings LeafVert(Attributes input)
            {
                Varyings output;
                float3 positionWS = ApplyLeafWind(input.positionOS.xyz, input.pivotOS, input.uv);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.positionOS = input.positionOS.xyz;
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 LeafFrag(Varyings input) : SV_Target
            {
                float leafMask = SampleInverseLeafMask(input.uv);
                clip(leafMask - _AlphaCutoff);

                float3 fixedNormalOS = normalize(_FixedNormalOS.xyz + float3(0.0, 0.0, 0.0001));
                float3 normalWS = normalize(TransformObjectToWorldNormal(fixedNormalOS));
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float diffuse = saturate(dot(normalWS, mainLight.direction) * 0.5 + 0.5);
                float shadowAttenuation = lerp(1.0, mainLight.shadowAttenuation, _ShadowReceiveStrength);
                diffuse *= shadowAttenuation;

                float darkOutput = saturate(diffuse * _DarkMultiplier);
                float brightOutput = diffuse * _BrightMultiplier;
                float darkRamp = step(_DarkThreshold, darkOutput);
                float lightRamp = step(_LightThreshold, brightOutput);

                float3 color = lerp(_ShadowColor.rgb, _BaseColor.rgb, darkRamp);
                color = lerp(color, _HighlightColor.rgb * mainLight.color, lightRamp);

                float noise = ObjectValueNoise(input.positionOS * _NoiseScale + _NoiseOffset.xyz);
                float noiseRamp = saturate((noise - _NoiseLow) / max(_NoiseHigh - _NoiseLow, 0.0001));
                color = lerp(color, _NoiseColor.rgb, noiseRamp * _NoiseBlend);

                float crownHeight = saturate((input.positionOS.y - _CrownBottom) / max(_CrownTop - _CrownBottom, 0.0001));
                float bottomRamp = saturate(
                    (crownHeight - _BottomRampStart)
                    / max(_BottomRampEnd - _BottomRampStart, 0.0001));
                color = lerp(_BottomColor.rgb, color, bottomRamp);

                color = ApplyFinalHsv(color);
                color *= _EmissionStrength;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
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

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 pivotOS : TEXCOORD1;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output;
                float3 positionWS = ApplyLeafWind(input.positionOS.xyz, input.pivotOS, input.uv);
                float3 normalWS = normalize(TransformObjectToWorldNormal(_FixedNormalOS.xyz));

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                output.uv = input.uv;
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                clip(SampleInverseLeafMask(input.uv) - _AlphaCutoff);
                return 0;
            }
            ENDHLSL
        }
    }
}
