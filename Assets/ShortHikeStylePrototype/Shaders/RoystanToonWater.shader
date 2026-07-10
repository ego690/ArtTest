Shader "ShortHikeStylePrototype/WaterFoam/RoystanToonWater"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.36, 0.92, 0.86, 1)
        _DeepColor ("Deep Color", Color) = (0.05, 0.32, 0.60, 1)
        _DepthMaxDistance ("Depth Max Distance", Range(0.05, 8)) = 2.4
        _Alpha ("Water Alpha", Range(0, 1)) = 0.78

        _FoamColor ("Foam Color", Color) = (0.96, 1.0, 0.90, 1)
        _FoamTex ("Surface Noise", 2D) = "white" {}
        _FoamMinDistance ("Foam Min Distance", Range(0.01, 3)) = 0.12
        _FoamMaxDistance ("Foam Max Distance", Range(0.01, 3)) = 0.78
        _FoamCutoff ("Foam Cutoff", Range(0, 1)) = 0.58
        _FoamSoftness ("Foam Softness", Range(0.001, 0.4)) = 0.055
        _FoamDisplayThreshold ("Foam Display Threshold", Range(0, 1)) = 0.5
        _FoamNoiseScale ("Foam Noise Scale", Range(0.001, 40)) = 7.0
        _FoamVariationStrength ("Foam Anti Tiling Strength", Range(0, 1)) = 0.65
        _FoamVariationScale ("Foam Variation Scale", Range(0.2, 4)) = 1.73
        _SurfaceNoiseScroll ("Surface Noise Scroll Amount", Vector) = (0.03, 0.03, 0, 0)
        [HideInInspector] _FoamSpeed ("Legacy Foam Speed", Vector) = (0.055, 0.025, -0.035, 0.04)

        _SurfaceFoamAmount ("Open Water Foam Amount", Range(0, 1)) = 0.16
        _SurfaceFoamCutoff ("Open Water Foam Cutoff", Range(0, 1)) = 0.78

        _DistortionTex ("Surface Distortion", 2D) = "gray" {}
        _SurfaceDistortionAmount ("Surface Distortion Amount", Range(0, 1)) = 0.27
        [HideInInspector] _DistortionStrength ("Legacy Distortion Strength", Range(0, 0.25)) = 0.055
        _DistortionScale ("Distortion Scale", Range(0.1, 40)) = 4.0
        _DistortionSpeed ("Distortion Speed", Vector) = (0.03, -0.02, 0, 0)
        _RefractionStrength ("Scene Refraction Strength", Range(0, 0.04)) = 0.006
        _RefractionBlend ("Scene Refraction Visibility", Range(0, 1)) = 0.36
        _RefractionWaterlineFade ("Refraction Waterline Fade", Range(0.001, 1.5)) = 0.22
        _RefractionDepthFade ("Refraction Deep Fade", Range(0, 1)) = 0.45

        [Toggle] _UseVoronoiWater ("Use Voronoi Water Texture", Float) = 0
        _VoronoiLightColor ("Voronoi Mask Black / Light Blue", Color) = (0.34, 0.88, 1.0, 1)
        _VoronoiDarkColor ("Voronoi Mask White / Deep Blue", Color) = (0.02, 0.18, 0.42, 1)
        [HDR] _VoronoiCausticColor ("Voronoi Caustic Emission", Color) = (1.35, 2.25, 2.55, 1)
        _VoronoiColorBlend ("Voronoi Color Blend", Range(0, 1)) = 1
        _VoronoiSurfaceOpacity ("Voronoi Surface Opacity", Range(0, 1)) = 1
        _VoronoiCausticStrength ("Voronoi Caustic Strength", Range(0, 4)) = 1.25
        _VoronoiCausticOpacity ("Voronoi Caustic Opacity", Range(0, 1)) = 1
        _VoronoiCausticDepthFade ("Voronoi Caustic Depth Fade", Range(0.05, 12)) = 6
        _VoronoiScale ("Voronoi Scale", Range(0.5, 24)) = 5.5
        _VoronoiLineWidth ("Voronoi Line Width", Range(0.005, 0.35)) = 0.08
        _VoronoiLineSoftness ("Voronoi Line Softness", Range(0, 0.25)) = 0.045
        _VoronoiDistortionScale ("Voronoi Distortion Scale", Range(0.05, 80)) = 2.2
        _VoronoiDistortionStrength ("Voronoi Distortion Strength", Range(-10, 10)) = 0.18
        _VoronoiScroll ("Voronoi Scroll", Vector) = (0.025, 0.055, -0.018, 0.032)

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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

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
                float _FoamDisplayThreshold;
                float _FoamNoiseScale;
                float _FoamVariationStrength;
                float _FoamVariationScale;
                float4 _SurfaceNoiseScroll;
                float4 _FoamSpeed;

                float _SurfaceFoamAmount;
                float _SurfaceFoamCutoff;

                float _SurfaceDistortionAmount;
                float _DistortionStrength;
                float _DistortionScale;
                float4 _DistortionSpeed;
                float _RefractionStrength;
                float _RefractionBlend;
                float _RefractionWaterlineFade;
                float _RefractionDepthFade;

                float _UseVoronoiWater;
                float4 _VoronoiLightColor;
                float4 _VoronoiDarkColor;
                float4 _VoronoiCausticColor;
                float _VoronoiColorBlend;
                float _VoronoiSurfaceOpacity;
                float _VoronoiCausticStrength;
                float _VoronoiCausticOpacity;
                float _VoronoiCausticDepthFade;
                float _VoronoiScale;
                float _VoronoiLineWidth;
                float _VoronoiLineSoftness;
                float _VoronoiDistortionScale;
                float _VoronoiDistortionStrength;
                float4 _VoronoiScroll;

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
                float2 distortSample = (SAMPLE_TEXTURE2D(_DistortionTex, sampler_DistortionTex, distortionUv).rg * 2.0 - 1.0) * _SurfaceDistortionAmount;

                float2 noiseUv = uv * _FoamNoiseScale + _SurfaceNoiseScroll.xy * timeValue + distortSample;
                float foamA = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, noiseUv).r;

                float2 rotatedUv = float2(uv.x * 0.62 - uv.y * 0.78, uv.x * 0.78 + uv.y * 0.62);
                float2 variationDistortionUv = rotatedUv * (_DistortionScale * 0.73) - _DistortionSpeed.yx * timeValue * 0.61;
                float2 variationDistortSample = (SAMPLE_TEXTURE2D(_DistortionTex, sampler_DistortionTex, variationDistortionUv).rg * 2.0 - 1.0) * _SurfaceDistortionAmount;
                float2 variationNoiseUv = rotatedUv * (_FoamNoiseScale * _FoamVariationScale) - _SurfaceNoiseScroll.yx * timeValue * 0.79 + variationDistortSample;
                float foamB = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, variationNoiseUv).r;

                return lerp(foamA, saturate((foamA + foamB) * 0.5), _FoamVariationStrength);
            }

            float2 Hash22(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(443.8975, 397.2973, 491.1871));
                p3 += dot(p3, p3.yzx + 19.19);
                return frac(float2((p3.x + p3.y) * p3.z, (p3.x + p3.z) * p3.y));
            }

            float ValueNoise01(float2 p)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                float a = Hash22(cell).x;
                float b = Hash22(cell + float2(1.0, 0.0)).x;
                float c = Hash22(cell + float2(0.0, 1.0)).x;
                float d = Hash22(cell + float2(1.0, 1.0)).x;
                float2 curve = local * local * (3.0 - 2.0 * local);
                return lerp(lerp(a, b, curve.x), lerp(c, d, curve.x), curve.y);
            }

            float Fbm01(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                value += ValueNoise01(p) * amplitude;
                p = p * 2.03 + 17.13;
                amplitude *= 0.5;
                value += ValueNoise01(p) * amplitude;
                p = p * 2.07 + 41.73;
                amplitude *= 0.5;
                value += ValueNoise01(p) * amplitude;
                return value / 0.875;
            }

            float VoronoiLineMask(float2 uv)
            {
                float2 cell = floor(uv);
                float2 local = frac(uv);
                float nearest = 8.0;
                float2 nearestDelta = 0.0;

                [unroll]
                for (int y = -2; y <= 2; y++)
                {
                    [unroll]
                    for (int x = -2; x <= 2; x++)
                    {
                        float2 offset = float2(x, y);
                        float2 siteDelta = offset + Hash22(cell + offset) - local;
                        float dist = dot(siteDelta, siteDelta);

                        if (dist < nearest)
                        {
                            nearest = dist;
                            nearestDelta = siteDelta;
                        }
                    }
                }

                float edgeDistance = 8.0;
                [unroll]
                for (int y = -2; y <= 2; y++)
                {
                    [unroll]
                    for (int x = -2; x <= 2; x++)
                    {
                        float2 offset = float2(x, y);
                        float2 siteDelta = offset + Hash22(cell + offset) - local;
                        float2 siteSeparation = siteDelta - nearestDelta;
                        float separationSq = dot(siteSeparation, siteSeparation);

                        if (separationSq > 0.00001)
                        {
                            float2 edgeNormal = siteSeparation * rsqrt(separationSq);
                            float distanceToEdge = dot(0.5 * (nearestDelta + siteDelta), edgeNormal);
                            edgeDistance = min(edgeDistance, distanceToEdge);
                        }
                    }
                }

                float lineSoftness = max(_VoronoiLineSoftness, 0.0001);
                return 1.0 - smoothstep(_VoronoiLineWidth, _VoronoiLineWidth + lineSoftness, edgeDistance);
            }

            void ApplyVoronoiWater(float2 waterUv, float depthDifference, float timeValue, inout float3 waterColor, out float3 causticColor)
            {
                causticColor = 0.0;
                float useVoronoi = saturate(_UseVoronoiWater);
                if (useVoronoi <= 0.001)
                    return;

                float2 sharedScroll = _VoronoiScroll.xy * timeValue;
                float2 baseUv = waterUv + sharedScroll;
                float scaleWave = saturate(0.5 + 0.32 * sin(timeValue * 0.37 + 1.3) + 0.18 * cos(timeValue * 0.19 + 2.4));
                float strengthWave = saturate(0.5 + 0.30 * sin(timeValue * 0.74) + 0.20 * cos(timeValue * 0.43 + 0.8));
                float scaleTimeFactor = lerp(0.72, 1.0, scaleWave);
                float strengthTimeFactor = lerp(0.35, 1.0, strengthWave);
                float effectiveDistortionScale = max(_VoronoiDistortionScale * scaleTimeFactor, 0.001);
                float effectiveDistortionStrength = _VoronoiDistortionStrength * strengthTimeFactor;
                float fbmScale = max(effectiveDistortionScale * 0.08, 0.05);
                float fbm = Fbm01(baseUv * fbmScale + _VoronoiScroll.zw * timeValue * 0.16) * 2.0 - 1.0;
                baseUv += float2(fbm, -fbm) * effectiveDistortionStrength;

                float2 colorUv = baseUv * _VoronoiScale;
                float2 causticUv = baseUv * (_VoronoiScale * 1.17) + 13.7;

                float colorLineMask = VoronoiLineMask(colorUv);
                float causticLineMask = VoronoiLineMask(causticUv);
                float causticPulse = lerp(0.78, 1.22, FoamSample(waterUv * 0.37 + timeValue * 0.04));

                float3 voronoiBase = lerp(_VoronoiDarkColor.rgb, _VoronoiLightColor.rgb, colorLineMask);
                float colorBlend = saturate(_VoronoiColorBlend * _VoronoiSurfaceOpacity * useVoronoi);
                waterColor = lerp(waterColor, voronoiBase, colorBlend);

                float causticDepthMax = max(_VoronoiCausticDepthFade, 0.001);
                float depthFade = 1.0 - smoothstep(causticDepthMax * 0.72, causticDepthMax, depthDifference);
                float causticAmount = causticLineMask * _VoronoiCausticStrength * _VoronoiCausticOpacity * causticPulse * depthFade * useVoronoi;
                causticColor = _VoronoiCausticColor.rgb * causticAmount;
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

                float timeValue = _Time.y;
                float3 voronoiCausticColor;
                ApplyVoronoiWater(waterUv, depthDifference, timeValue, waterColor, voronoiCausticColor);

                float2 refractionUvA = waterUv * _DistortionScale + _DistortionSpeed.xy * timeValue;
                float2 refractionUvB = waterUv * (_DistortionScale * 0.57 + 0.13) - _DistortionSpeed.yx * timeValue * 0.73;
                float2 refractionA = SAMPLE_TEXTURE2D(_DistortionTex, sampler_DistortionTex, refractionUvA).rg * 2.0 - 1.0;
                float2 refractionB = SAMPLE_TEXTURE2D(_DistortionTex, sampler_DistortionTex, refractionUvB).rg * 2.0 - 1.0;
                float2 refractionOffset = normalize(refractionA + refractionB * 0.55 + 0.0001);
                float submergedMask = smoothstep(0.015, max(_RefractionWaterlineFade, 0.016), depthDifference);
                float deepFade = lerp(1.0, 1.0 - depth01, _RefractionDepthFade);
                float refractionMask = saturate(submergedMask * deepFade);
                float2 refractedScreenUv = screenUv + refractionOffset * (_RefractionStrength * refractionMask);
                float3 refractedSceneColor = SampleSceneColor(refractedScreenUv);

                float shoreCutoff = foamDepthDifference01 * _FoamCutoff;
                float shoreFoam = smoothstep(shoreCutoff - _FoamSoftness, shoreCutoff + _FoamSoftness, foamNoise);

                float openFoam = smoothstep(_SurfaceFoamCutoff - _FoamSoftness, _SurfaceFoamCutoff + _FoamSoftness, foamNoise);
                openFoam *= _SurfaceFoamAmount * smoothstep(0.18, 1.0, depth01);

                float foam = saturate(max(shoreFoam, openFoam));

                float3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                float fresnel = pow(saturate(1.0 - dot(viewDirWS, surfaceNormalWS)), 3.0) * _ReflectionStrength;
                waterColor = lerp(waterColor, _ReflectionColor.rgb, fresnel);
                waterColor = lerp(waterColor, refractedSceneColor, saturate(_RefractionBlend * refractionMask));
                waterColor += voronoiCausticColor;

                float visibleFoam = step(_FoamDisplayThreshold, foam);

                float alpha = saturate(_Alpha + visibleFoam * 0.18);
                float4 water = float4(waterColor, alpha);
                float4 foamColor = float4(_FoamColor.rgb, visibleFoam * _FoamColor.a);
                return AlphaBlend(foamColor, water);
            }
            ENDHLSL
        }
    }
}
