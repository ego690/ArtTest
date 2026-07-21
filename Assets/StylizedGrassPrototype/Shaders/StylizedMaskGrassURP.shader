Shader "StylizedGrassPrototype/Mask Grass Field"
{
    Properties
    {
        [NoScaleOffset] _BladeMask ("Blade Mask (White = Grass)", 2D) = "white" {}
        [HideInInspector] [NoScaleOffset] _NoiseTex ("Legacy Field Noise", 2D) = "gray" {}
        [HideInInspector] [NoScaleOffset] _WindNoiseTex ("Legacy Wind Noise", 2D) = "gray" {}
        _AlphaCutoff ("Mask Cutoff", Range(0, 1)) = 0.5

        _GradientLow ("Gradient Low", Color) = (0.055, 0.22, 0.08, 1)
        _GradientMid ("Gradient Mid", Color) = (0.16, 0.48, 0.12, 1)
        _GradientHigh ("Gradient High", Color) = (0.56, 0.82, 0.20, 1)
        _TipGradientLow ("Tip Gradient Low", Color) = (0.18, 0.42, 0.08, 1)
        _TipGradientMid ("Tip Gradient Mid", Color) = (0.48, 0.76, 0.14, 1)
        _TipGradientHigh ("Tip Gradient High", Color) = (0.82, 1.0, 0.32, 1)
        _GradientMidpoint ("Gradient Midpoint", Range(0.05, 0.95)) = 0.5
        [HideInInspector] _GradientRampMode ("Gradient Ramp Mode", Float) = 1
        [HideInInspector] _GradientRampSteps ("Gradient Ramp Steps", Float) = 4
        _NoiseScale ("Noise World Scale", Range(0.01, 2)) = 0.18
        _NoiseContrast ("Noise Contrast", Range(0.1, 4)) = 1.25
        _NoiseOffset ("Noise Offset", Vector) = (0, 0, 0, 0)
        _CloudShadowSpeed ("Cloud Shadow Speed", Range(0, 1)) = 0.12

        _HueShift ("Hue Shift", Range(-1, 1)) = 0
        _Saturation ("Saturation", Range(0, 2)) = 1.15
        _Brightness ("Brightness", Range(0, 2)) = 1.08
        _UnlitBoost ("Unlit Blade Boost", Range(0.5, 3)) = 1.25
        _TipBrightness ("Blade Tip Brightness", Range(0.5, 2)) = 1.12

        _BladeWidth ("Blade Card Width", Range(0.02, 1)) = 0.34
        _BladeHeight ("Blade Card Height", Range(0.05, 2)) = 0.72
        _ScaleMinMax ("Random Scale Min Max", Vector) = (0.72, 1.28, 0, 0)
        _VerticalRotation ("Vertical Axis Rotation", Range(0, 1)) = 1
        _TiltCoefficient ("Other Axis Coefficient", Range(0, 0.6)) = 0.12
        _DistributionSeed ("Distribution Seed", Float) = 17
        _GroundOffset ("Blade Ground Offset", Range(-0.1, 0.2)) = 0.015
        [HideInInspector] _GrassDensity ("Blades Per Square Meter", Float) = 20
        [HideInInspector] _MaxBladesPerTriangle ("Max Blades Per Triangle", Float) = 11
        [HideInInspector] _SlopeFilterEnabled ("Slope Filter Enabled", Float) = 0
        [HideInInspector] _MinGrassUpDot ("Minimum Grass Up Dot", Float) = 0.573576
        [HideInInspector] _WindEnabled ("Wind Enabled", Float) = 1
        [HideInInspector] _FieldNoiseEnabled ("Field Noise Enabled", Float) = 1
        [HideInInspector] _DynamicShadowEnabled ("Dynamic Shadow Enabled", Float) = 1
        [HideInInspector] _InteractionEnabled ("Interaction Enabled", Float) = 1

        _WindLargeScale ("Large Wave Scale", Range(0.005, 0.2)) = 0.03
        _WindDetailScale ("Small Disturbance Scale", Range(0.02, 0.5)) = 0.15
        _WindDetailStrength ("Small Disturbance Strength", Range(0, 1)) = 0.25
        _WindNoiseAxisScale ("Wind Noise Axis Scale X/Z", Vector) = (0.45, 2.4, 0, 0)
        _WindSpeed ("Wind Advection Speed", Range(0, 1)) = 0.12
        _WindDirection ("Wind Direction", Vector) = (0.8, 0, 0.35, 0)
        _WindTimeOffset ("Wind Time Offset", Float) = 0
        _WindBendStrength ("Wind Bend Strength", Range(0, 1.5)) = 0.82
        _WindHeightInfluence ("Wind Height Influence", Range(0, 0.6)) = 0.32
        _WindRotationStrength ("Wind Rotation Strength", Range(0, 1)) = 0.28
        _WindBendCurve ("Wind Bend Curve", Range(0.5, 4)) = 1.8
        _WaveLightStrength ("Wave Light Strength", Range(0, 1)) = 0.28
        [HDR] _WaveShadowTint ("Wave Shadow Tint", Color) = (0.52, 0.72, 0.58, 1)
        [HDR] _WaveHighlightTint ("Wave Highlight Tint", Color) = (1.12, 1.08, 0.82, 1)

        _DynamicShadowColor ("Dynamic Shadow Color", Color) = (0.055, 0.07, 0.045, 1)
        _DynamicShadowStrength ("Dynamic Shadow Strength", Range(0, 1)) = 0.72
        _ShadowThreshold ("Shadow Mask Threshold", Range(0, 1)) = 0.55
        _ShadowSoftness ("Shadow Mask Softness", Range(0.001, 0.5)) = 0.18
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

        HLSLINCLUDE
        #pragma target 4.5
        #pragma exclude_renderers gles gles3 glcore

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        TEXTURE2D(_BladeMask);
        SAMPLER(sampler_BladeMask);

        #define BLADE_SEGMENTS 4
        #define MAX_BLADES_PER_TRIANGLE 11
        #define MAX_GRASS_INTERACTION_POINTS 64

        CBUFFER_START(UnityPerMaterial)
            float4 _GradientLow;
            float4 _GradientMid;
            float4 _GradientHigh;
            float4 _TipGradientLow;
            float4 _TipGradientMid;
            float4 _TipGradientHigh;
            float4 _NoiseOffset;
            float4 _ScaleMinMax;
            float4 _WindDirection;
            float4 _WindNoiseAxisScale;
            float4 _WaveShadowTint;
            float4 _WaveHighlightTint;
            float4 _DynamicShadowColor;
            float _AlphaCutoff;
            float _GradientMidpoint;
            float _GradientRampMode;
            float _GradientRampSteps;
            float _NoiseScale;
            float _NoiseContrast;
            float _CloudShadowSpeed;
            float _HueShift;
            float _Saturation;
            float _Brightness;
            float _UnlitBoost;
            float _TipBrightness;
            float _BladeWidth;
            float _BladeHeight;
            float _VerticalRotation;
            float _TiltCoefficient;
            float _DistributionSeed;
            float _GroundOffset;
            float _GrassDensity;
            float _MaxBladesPerTriangle;
            float _SlopeFilterEnabled;
            float _MinGrassUpDot;
            float _WindEnabled;
            float _FieldNoiseEnabled;
            float _DynamicShadowEnabled;
            float _InteractionEnabled;
            float _WindLargeScale;
            float _WindDetailScale;
            float _WindDetailStrength;
            float _WindSpeed;
            float _WindTimeOffset;
            float _WindBendStrength;
            float _WindHeightInfluence;
            float _WindRotationStrength;
            float _WindBendCurve;
            float _WaveLightStrength;
            float _DynamicShadowStrength;
            float _ShadowThreshold;
            float _ShadowSoftness;
        CBUFFER_END

        int _GrassSamplePointCount;
        float4 _GrassSamplePointPositions[MAX_GRASS_INTERACTION_POINTS];
        float4 _GrassSamplePointDirections[MAX_GRASS_INTERACTION_POINTS];
        float4 _GrassSamplePointSettings[MAX_GRASS_INTERACTION_POINTS];

        float Random01(float3 seed)
        {
            return frac(sin(dot(seed, float3(12.9898, 78.233, 37.719))) * 43758.5453);
        }

        float SignedRandom(float3 seed)
        {
            return Random01(seed) * 2.0 - 1.0;
        }

        void SampleGrassInteraction(
            float3 baseWS,
            float3 surfaceUpWS,
            out float3 interactionBendWS,
            out float interactionFlatten)
        {
            interactionBendWS = 0.0;
            interactionFlatten = 0.0;
            [branch]
            if (_InteractionEnabled < 0.5)
                return;

            float interactionWeight = 0.0;
            int interactorCount = clamp(_GrassSamplePointCount, 0, MAX_GRASS_INTERACTION_POINTS);

            [loop]
            for (int index = 0; index < MAX_GRASS_INTERACTION_POINTS; index++)
            {
                if (index >= interactorCount)
                    break;

                float4 interactor = _GrassSamplePointPositions[index];
                float3 deltaWS = baseWS - interactor.xyz;
                float distanceToInteractor = length(deltaWS);
                float distanceFade = saturate(1.0 - distanceToInteractor / max(interactor.w, 0.001));
                float4 settings = _GrassSamplePointSettings[index];
                float influence = pow(distanceFade, max(settings.z, 0.25)) * settings.w;
                if (influence <= 0.0001)
                    continue;

                float3 tangentDeltaWS = deltaWS - surfaceUpWS * dot(deltaWS, surfaceUpWS);
                float3 fallbackAxisWS = abs(surfaceUpWS.y) < 0.999
                    ? float3(0.0, 1.0, 0.0)
                    : float3(1.0, 0.0, 0.0);
                float3 fallbackDirectionWS = normalize(cross(fallbackAxisWS, surfaceUpWS));
                float3 radialDirectionWS = dot(tangentDeltaWS, tangentDeltaWS) > 0.000001
                    ? normalize(tangentDeltaWS)
                    : fallbackDirectionWS;

                float4 directionAndStrength = _GrassSamplePointDirections[index];
                float3 motionDirectionWS = directionAndStrength.xyz
                    - surfaceUpWS * dot(directionAndStrength.xyz, surfaceUpWS);
                float hasMotion = step(0.000001, dot(motionDirectionWS, motionDirectionWS));
                motionDirectionWS = hasMotion > 0.5
                    ? normalize(motionDirectionWS)
                    : radialDirectionWS;

                float directionBlend = settings.y * hasMotion;
                float3 blendedDirectionWS = lerp(radialDirectionWS, motionDirectionWS, directionBlend);
                float3 bendDirectionWS = dot(blendedDirectionWS, blendedDirectionWS) > 0.000001
                    ? normalize(blendedDirectionWS)
                    : radialDirectionWS;
                interactionBendWS += bendDirectionWS * directionAndStrength.w * influence;
                interactionFlatten += settings.x * influence;
                interactionWeight += influence;
            }

            float normalization = max(1.0, interactionWeight);
            interactionBendWS /= normalization;
            interactionFlatten = saturate(interactionFlatten / normalization);
        }

        float ProceduralHash21(float2 cell)
        {
            float3 p3 = frac(float3(cell.xyx) * float3(0.1031, 0.1030, 0.0973));
            p3 += dot(p3, p3.yzx + 33.33);
            return frac((p3.x + p3.y) * p3.z);
        }

        float ProceduralValueNoise(float2 position)
        {
            float2 cell = floor(position);
            float2 local = frac(position);
            float2 blend = local * local * local * (local * (local * 6.0 - 15.0) + 10.0);

            float bottomLeft = ProceduralHash21(cell);
            float bottomRight = ProceduralHash21(cell + float2(1.0, 0.0));
            float topLeft = ProceduralHash21(cell + float2(0.0, 1.0));
            float topRight = ProceduralHash21(cell + float2(1.0, 1.0));
            float bottom = lerp(bottomLeft, bottomRight, blend.x);
            float top = lerp(topLeft, topRight, blend.x);
            return lerp(bottom, top, blend.y);
        }

        float ProceduralFieldNoise(float2 position)
        {
            float2 seed = float2(_DistributionSeed * 0.137, _DistributionSeed * 0.271);
            float broad = ProceduralValueNoise(position * 3.2 + seed);
            float detail = ProceduralValueNoise(position * 9.7 + seed.yx + float2(19.7, 7.3));
            return saturate(broad * 0.76 + detail * 0.24);
        }

        float ProceduralWindNoise(float2 position)
        {
            float2 seed = float2(_DistributionSeed * 0.193, _DistributionSeed * 0.347);
            float large = ProceduralValueNoise(position * 2.1 + seed);
            float medium = ProceduralValueNoise(position * 5.3 + seed.yx + float2(13.1, 29.4));
            float detail = ProceduralValueNoise(position * 11.7 - seed + float2(47.2, 5.9));
            return saturate(large * 0.62 + medium * 0.28 + detail * 0.1);
        }

        float3 GrassRgbToHsv(float3 c)
        {
            float4 k = float4(0.0, -0.3333333, 0.6666667, -1.0);
            float4 p = lerp(float4(c.bg, k.wz), float4(c.gb, k.xy), step(c.b, c.g));
            float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
            float d = q.x - min(q.w, q.y);
            float e = 1e-10;
            return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
        }

        float3 GrassHsvToRgb(float3 c)
        {
            float3 p = abs(frac(c.xxx + float3(0.0, 0.6666667, 0.3333333)) * 6.0 - 3.0);
            return c.z * lerp(1.0.xxx, saturate(p - 1.0), c.y);
        }

        float3 ApplyGlobalHsv(float3 color)
        {
            float3 hsv = GrassRgbToHsv(max(color, 0.0));
            hsv.x = frac(hsv.x + _HueShift);
            hsv.y = saturate(hsv.y * _Saturation);
            hsv.z = max(0.0, hsv.z * _Brightness);
            return GrassHsvToRgb(hsv);
        }

        float SampleFieldNoise(float3 positionWS)
        {
            float result = saturate(_GradientMidpoint);
            [branch]
            if (_FieldNoiseEnabled >= 0.5)
            {
                float2 cloudDirection = normalize(_WindDirection.xz + float2(0.0001, 0.0001));
                float cloudTime = (_Time.y + _WindTimeOffset) * _CloudShadowSpeed;
                float2 cloudDrift = cloudDirection * cloudTime * 0.35;
                float2 uv = positionWS.xz * _NoiseScale + _NoiseOffset.xy + cloudDrift;
                float noise = ProceduralFieldNoise(uv);
                float contrastWave = sin(cloudTime * 0.73 + _DistributionSeed * 0.19);
                float animatedContrast = _NoiseContrast * (1.0 + contrastWave * 0.22);
                result = saturate((noise - 0.5) * animatedContrast + 0.5);
            }
            return result;
        }

        float3 SampleGradient(float noise, float3 lowColor, float3 midColor, float3 highColor)
        {
            float midpoint = clamp(_GradientMidpoint, 0.001, 0.999);
            float rampNoise = saturate(noise);

            if (_GradientRampMode > 1.5)
            {
                float steps = max(2.0, round(_GradientRampSteps));
                rampNoise = round(rampNoise * (steps - 1.0)) / (steps - 1.0);
            }

            float lowToMidMask = saturate(rampNoise / midpoint);
            float midToHighMask = saturate((rampNoise - midpoint) / (1.0 - midpoint));

            if (_GradientRampMode < 0.5)
            {
                lowToMidMask = lowToMidMask * lowToMidMask * (3.0 - 2.0 * lowToMidMask);
                midToHighMask = midToHighMask * midToHighMask * (3.0 - 2.0 * midToHighMask);
            }

            float3 lowToMidColor = lerp(lowColor, midColor, lowToMidMask);
            return lerp(lowToMidColor, highColor, midToHighMask);
        }

        float3 SampleGroundColor(float noise)
        {
            return ApplyGlobalHsv(SampleGradient(
                noise,
                _GradientLow.rgb,
                _GradientMid.rgb,
                _GradientHigh.rgb));
        }

        float3 SampleTipColor(float noise)
        {
            return ApplyGlobalHsv(SampleGradient(
                noise,
                _TipGradientLow.rgb,
                _TipGradientMid.rgb,
                _TipGradientHigh.rgb));
        }

        float SampleWindNoise(float3 positionWS)
        {
            float result = 0.5;
            [branch]
            if (_WindEnabled >= 0.5)
            {
                float2 windDirection = normalize(_WindDirection.xz + float2(0.0001, 0.0001));
                float windTime = (_Time.y + _WindTimeOffset) * _WindSpeed;
                float2 advection = windDirection * windTime;
                float2 axisScale = max(abs(_WindNoiseAxisScale.xy), float2(0.001, 0.001));
                float2 stretchedPosition = positionWS.xz * axisScale;
                float largeWave = ProceduralWindNoise(
                    stretchedPosition * _WindLargeScale + advection);
                float smallDisturbance = ProceduralWindNoise(
                    stretchedPosition * _WindDetailScale
                    + advection * 1.37
                    + float2(0.37, 0.71));
                result = saturate(
                    (largeWave + smallDisturbance * _WindDetailStrength)
                    / max(1.0 + _WindDetailStrength, 0.001));
            }
            return result;
        }

        float3 ApplyWindColor(float3 color, float wind)
        {
            float3 result = color;
            [branch]
            if (_WindEnabled >= 0.5)
            {
                float waveLight = (wind - 0.5) * 2.0;
                float3 waveTint = waveLight < 0.0
                    ? lerp(1.0.xxx, _WaveShadowTint.rgb, -waveLight)
                    : lerp(1.0.xxx, _WaveHighlightTint.rgb, waveLight);
                float brightness = max(0.05, 1.0 + waveLight * _WaveLightStrength);
                result = color * waveTint * brightness;
            }
            return result;
        }

        float3 ApplyDynamicShadow(float3 color, float3 positionWS)
        {
            float3 result = color;
            [branch]
            if (_DynamicShadowEnabled >= 0.5)
            {
                float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float low = max(0.0, _ShadowThreshold - _ShadowSoftness);
                float high = min(1.0, _ShadowThreshold + _ShadowSoftness);
                float whiteMask = smoothstep(low, high, mainLight.shadowAttenuation);
                float blackMask = 1.0 - whiteMask;
                result = lerp(
                    color,
                    color * _DynamicShadowColor.rgb,
                    blackMask * _DynamicShadowStrength);
            }
            return result;
        }

        struct BladeAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
        };

        struct VertexToGeometry
        {
            float3 positionOS : TEXCOORD0;
            float3 normalOS : TEXCOORD1;
            float4 tangentOS : TEXCOORD2;
        };

        struct BladeVaryings
        {
            float4 positionCS : SV_POSITION;
            float3 samplePositionWS : TEXCOORD0;
            float2 uv : TEXCOORD1;
        };

        VertexToGeometry BladeVert(BladeAttributes input)
        {
            VertexToGeometry output;
            output.positionOS = input.positionOS.xyz;
            output.normalOS = input.normalOS;
            output.tangentOS = input.tangentOS;
            return output;
        }

        void AppendFieldVertex(
            inout TriangleStream<BladeVaryings> stream,
            float3 positionWS,
            float3 samplePositionWS,
            float2 uv)
        {
            BladeVaryings output = (BladeVaryings)0;
            output.samplePositionWS = samplePositionWS;
            output.uv = uv;
            output.positionCS = TransformWorldToHClip(positionWS);
            stream.Append(output);
        }

        void AppendGrassBlade(
            VertexToGeometry vertex0,
            VertexToGeometry vertex1,
            VertexToGeometry vertex2,
            inout TriangleStream<BladeVaryings> stream,
            float3 seed)
        {
            float root = sqrt(Random01(seed + 1.31));
            float baryY = Random01(seed.yzx + 4.79);
            float3 barycentric = float3(1.0 - root, root * (1.0 - baryY), root * baryY);
            float3 baseOS = vertex0.positionOS * barycentric.x
                + vertex1.positionOS * barycentric.y
                + vertex2.positionOS * barycentric.z;

            float3 normalOS = normalize(vertex0.normalOS * barycentric.x
                + vertex1.normalOS * barycentric.y
                + vertex2.normalOS * barycentric.z);
            float4 tangentOS = vertex0.tangentOS * barycentric.x
                + vertex1.tangentOS * barycentric.y
                + vertex2.tangentOS * barycentric.z;

            float3 baseWS = TransformObjectToWorld(baseOS);
            float3 surfaceUpWS = normalize(TransformObjectToWorldNormal(normalOS));
            float tangentSign = tangentOS.w < 0.0 ? -1.0 : 1.0;
            float3 tangentWS = float3(1.0, 0.0, 0.0);
            if (dot(tangentOS.xyz, tangentOS.xyz) > 0.000001)
            {
                tangentWS = TransformObjectToWorldDir(tangentOS.xyz);
                tangentWS -= surfaceUpWS * dot(surfaceUpWS, tangentWS);
            }
            else
            {
                float3 referenceAxisWS = abs(surfaceUpWS.y) < 0.999
                    ? float3(0.0, 1.0, 0.0)
                    : float3(1.0, 0.0, 0.0);
                tangentWS = cross(referenceAxisWS, surfaceUpWS);
            }
            tangentWS = normalize(tangentWS);
            float3 bitangentWS = normalize(cross(surfaceUpWS, tangentWS) * tangentSign);

            float wind = SampleWindNoise(baseWS);
            float waveLight = (wind - 0.5) * 2.0;
            float windEnabled = step(0.5, _WindEnabled);

            float yaw = SignedRandom(seed.zxy + 9.17) * PI * _VerticalRotation
                + waveLight * _WindRotationStrength;
            float yawSin;
            float yawCos;
            sincos(yaw, yawSin, yawCos);
            float3 bladeRightWS = normalize(tangentWS * yawCos + bitangentWS * yawSin);
            float3 bladeForwardWS = normalize(-tangentWS * yawSin + bitangentWS * yawCos);

            float sideTilt = SignedRandom(seed.xzy + 13.53) * _TiltCoefficient;
            float forwardTilt = SignedRandom(seed.zyx + 18.11) * _TiltCoefficient;
            float3 bladeUpWS = normalize(surfaceUpWS
                + bladeRightWS * sideTilt
                + bladeForwardWS * forwardTilt);
            bladeRightWS = normalize(cross(bladeForwardWS, bladeUpWS));

            float randomScale = lerp(_ScaleMinMax.x, _ScaleMinMax.y, Random01(seed + 22.43));
            float halfWidth = _BladeWidth * randomScale * 0.5;
            float heightScale = max(0.25, 1.0 + waveLight * _WindHeightInfluence);
            float height = _BladeHeight * randomScale * heightScale;
            baseWS += surfaceUpWS * _GroundOffset;

            float3 interactionBendWS;
            float interactionFlatten;
            SampleGrassInteraction(baseWS, surfaceUpWS, interactionBendWS, interactionFlatten);
            height *= lerp(1.0, 0.25, interactionFlatten);

            float2 windDirection = normalize(_WindDirection.xz + float2(0.0001, 0.0001));
            float3 windWS = float3(windDirection.x, 0.0, windDirection.y);
            float flattenAmount = saturate(1.0 - wind);
            float bendDistance = _WindBendStrength
                * _BladeHeight
                * randomScale
                * lerp(0.22, 1.0, flattenAmount)
                * windEnabled;

            [unroll]
            for (int segment = 0; segment <= BLADE_SEGMENTS; segment++)
            {
                float t = segment / (float)BLADE_SEGMENTS;
                float curve = pow(t, _WindBendCurve);
                float widthAtSegment = halfWidth * lerp(1.0, 0.94, t);
                float3 centerWS = baseWS
                    + bladeUpWS * (height * t)
                    + windWS * (bendDistance * curve)
                    + interactionBendWS * curve;

                AppendFieldVertex(
                    stream,
                    centerWS - bladeRightWS * widthAtSegment,
                    baseWS,
                    float2(0.0, t));
                AppendFieldVertex(
                    stream,
                    centerWS + bladeRightWS * widthAtSegment,
                    baseWS,
                    float2(1.0, t));
            }
            stream.RestartStrip();
        }

        [maxvertexcount(MAX_BLADES_PER_TRIANGLE * (BLADE_SEGMENTS + 1) * 2 + 3)]
        void BladeGeo(triangle VertexToGeometry input[3], inout TriangleStream<BladeVaryings> stream)
        {
            float3 ground0WS = TransformObjectToWorld(input[0].positionOS);
            float3 ground1WS = TransformObjectToWorld(input[1].positionOS);
            float3 ground2WS = TransformObjectToWorld(input[2].positionOS);
            AppendFieldVertex(stream, ground0WS, ground0WS, float2(-1.0, -1.0));
            AppendFieldVertex(stream, ground1WS, ground1WS, float2(-1.0, -1.0));
            AppendFieldVertex(stream, ground2WS, ground2WS, float2(-1.0, -1.0));
            stream.RestartStrip();

            float triangleArea = length(cross(ground1WS - ground0WS, ground2WS - ground0WS)) * 0.5;
            float3 faceCrossWS = cross(ground1WS - ground0WS, ground2WS - ground0WS);
            float faceCrossLength = length(faceCrossWS);
            float3 sourceNormalOS = input[0].normalOS + input[1].normalOS + input[2].normalOS;
            float3 slopeNormalWS = float3(0.0, 1.0, 0.0);
            if (dot(sourceNormalOS, sourceNormalOS) > 0.000001)
                slopeNormalWS = normalize(TransformObjectToWorldNormal(sourceNormalOS));
            else if (faceCrossLength > 0.000001)
                slopeNormalWS = faceCrossWS / faceCrossLength;
            if (_SlopeFilterEnabled > 0.5
                && dot(slopeNormalWS, float3(0.0, 1.0, 0.0)) < _MinGrassUpDot)
            {
                triangleArea = 0.0;
            }
            int bladeCap = min(
                MAX_BLADES_PER_TRIANGLE,
                max(1, (int)round(_MaxBladesPerTriangle)));
            float expectedBladeCount = min(
                triangleArea * max(_GrassDensity, 0.0),
                (float)bladeCap);
            int bladeCount = (int)floor(expectedBladeCount);
            float fractionalBlade = expectedBladeCount - bladeCount;
            float3 centerOS = (input[0].positionOS + input[1].positionOS + input[2].positionOS) / 3.0;
            float3 triangleSeed = centerOS
                + float3(_DistributionSeed, _DistributionSeed * 0.37, _DistributionSeed * 1.73);
            bladeCount += Random01(triangleSeed + 73.19) < fractionalBlade ? 1 : 0;

            [loop]
            for (int bladeIndex = 0; bladeIndex < MAX_BLADES_PER_TRIANGLE; bladeIndex++)
            {
                if (bladeIndex >= bladeCount)
                    break;

                float bladeIndexValue = (float)bladeIndex;
                float3 bladeSeed = triangleSeed + float3(
                    bladeIndexValue * 17.17 + 3.11,
                    bladeIndexValue * 31.73 + 7.29,
                    bladeIndexValue * 47.93 + 11.83);
                AppendGrassBlade(input[0], input[1], input[2], stream, bladeSeed);
            }
        }

        float4 FieldFrag(BladeVaryings input) : SV_Target
        {
            bool isBlade = input.uv.x >= 0.0;
            if (isBlade)
            {
                float4 maskSample = SAMPLE_TEXTURE2D(_BladeMask, sampler_BladeMask, input.uv);
                float mask = dot(maskSample.rgb, float3(0.299, 0.587, 0.114)) * maskSample.a;
                clip(mask - _AlphaCutoff);
            }

            float wind = SampleWindNoise(input.samplePositionWS);
            float fieldNoise = SampleFieldNoise(input.samplePositionWS);
            float3 groundColor = SampleGroundColor(fieldNoise);
            float3 color = groundColor;
            if (isBlade)
            {
                float tipBlend = smoothstep(0.05, 1.0, saturate(input.uv.y));
                color = lerp(groundColor, SampleTipColor(fieldNoise), tipBlend);
                color *= _UnlitBoost * lerp(1.0, _TipBrightness, tipBlend);
            }
            color = ApplyWindColor(color, wind);
            color = ApplyDynamicShadow(color, input.samplePositionWS);
            return float4(saturate(color), 1.0);
        }
        ENDHLSL

        Pass
        {
            Name "GrassFieldForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex BladeVert
            #pragma geometry BladeGeo
            #pragma fragment FieldFrag
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            ENDHLSL
        }
    }

    FallBack Off
}
