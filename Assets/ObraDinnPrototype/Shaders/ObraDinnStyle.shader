Shader "Hidden/ObraDinnPrototype/ObraDinnStyle"
{
    Properties
    {
        _DarkColor ("Dark Color", Color) = (0.035, 0.032, 0.026, 1)
        _LightColor ("Light Color", Color) = (0.91, 0.84, 0.66, 1)
        _DarkEdgeColor ("Dark Edge Color", Color) = (0.035, 0.032, 0.026, 1)
        _LightEdgeColor ("Light Edge Color", Color) = (0.91, 0.84, 0.66, 1)
        _BlueNoiseTex ("Blue Noise", 2D) = "gray" {}
        _PixelScale ("Pixel Scale", Range(1, 8)) = 1
        _Contrast ("Contrast", Range(0.25, 4)) = 1.45
        _Brightness ("Brightness", Range(-1, 1)) = 0.02
        _Gamma ("Gamma", Range(0.25, 4)) = 0.82
        _BlueNoiseWeight ("Blue Noise Weight", Range(0, 1)) = 0.82
        _WorldDitherWeight ("World Dither Weight", Range(0, 1)) = 0.68
        _WorldDitherScale ("World Dither Scale", Range(4, 96)) = 36
        _WorldTriplanarSharpness ("World Triplanar Sharpness", Range(1, 12)) = 4
        _ToneFieldStrength ("Tone Field Strength", Range(0, 0.35)) = 0.08
        _ToneFieldScale ("Tone Field Scale", Range(8, 128)) = 42
        _ToneFieldHatchStrength ("Tone Field Hatch Strength", Range(0, 0.25)) = 0.05
        _ToneFieldHatchAngle ("Tone Field Hatch Angle", Range(-3.1416, 3.1416)) = 0.62
        _FaceMatrixScale ("Face Matrix Scale", Range(0.5, 8)) = 1.4
        _FaceMatrixOffset ("Face Matrix Offset", Vector) = (3, 5, 0, 0)
        _FaceBlueNoiseWeight ("Face Blue Noise Weight", Range(0, 1)) = 0.14
        _ThresholdBias ("Threshold Bias", Range(-0.5, 0.5)) = 0
        _EdgeStrength ("Edge Strength", Range(0, 1)) = 0.72
        [Toggle] _UseLightEdgeColor ("Use Light Edge Color", Float) = 1
        _DepthEdgeScale ("Depth Edge Scale", Range(0, 200)) = 42
        _NormalEdgeScale ("Normal Edge Scale", Range(0, 20)) = 4
        _RotationOffset ("Rotation Offset", Vector) = (0, 0, 0, 0)
        _OffsetStrength ("Offset Strength", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        ZWrite Off
        ZTest Always
        Cull Off

        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            TEXTURE2D(_BlueNoiseTex);
            SAMPLER(sampler_BlueNoiseTex);

            float4 _DarkColor;
            float4 _LightColor;
            float4 _DarkEdgeColor;
            float4 _LightEdgeColor;
            float4 _BlueNoiseTex_TexelSize;
            float4 _FaceMatrixOffset;
            float4 _RotationOffset;
            float _PixelScale;
            float _Contrast;
            float _Brightness;
            float _Gamma;
            float _BlueNoiseWeight;
            float _WorldDitherWeight;
            float _WorldDitherScale;
            float _WorldTriplanarSharpness;
            float _ToneFieldStrength;
            float _ToneFieldScale;
            float _ToneFieldHatchStrength;
            float _ToneFieldHatchAngle;
            float _FaceMatrixScale;
            float _FaceBlueNoiseWeight;
            float _ThresholdBias;
            float _EdgeStrength;
            float _UseLightEdgeColor;
            float _DepthEdgeScale;
            float _NormalEdgeScale;
            float _OffsetStrength;

            float PosterizedLuma(float3 color)
            {
                float luma = dot(color, float3(0.2126, 0.7152, 0.0722));
                luma = (luma - 0.5) * _Contrast + 0.5 + _Brightness;
                luma = saturate(luma);
                luma = pow(luma, max(_Gamma, 0.001));
                return luma;
            }

            float BlueNoise(int2 p)
            {
                float2 uv = frac((float2(p) + 0.5) * _BlueNoiseTex_TexelSize.xy);
                return SAMPLE_TEXTURE2D(_BlueNoiseTex, sampler_BlueNoiseTex, uv).r;
            }

            float SmoothBlueNoise(float2 pixel, float scale)
            {
                float2 fieldPixel = floor(pixel / max(scale, 1.0));
                float2 fraction = frac(pixel / max(scale, 1.0));
                int2 basePixel = int2(fieldPixel);

                float a = BlueNoise(basePixel);
                float b = BlueNoise(basePixel + int2(1, 0));
                float c = BlueNoise(basePixel + int2(0, 1));
                float d = BlueNoise(basePixel + int2(1, 1));
                float2 blend = fraction * fraction * (3.0 - 2.0 * fraction);

                return lerp(lerp(a, b, blend.x), lerp(c, d, blend.x), blend.y);
            }

            float ToneField(float2 pixel)
            {
                float scale = max(_ToneFieldScale, 1.0);
                float low = SmoothBlueNoise(pixel + _RotationOffset.xy * 0.17, scale);
                float lower = SmoothBlueNoise(pixel * 0.43 + 37.0, scale * 1.9);

                float2 direction = float2(cos(_ToneFieldHatchAngle), sin(_ToneFieldHatchAngle));
                float hatch = frac(dot(pixel, direction) / max(scale * 0.35, 1.0) + lower * 0.55);
                hatch = smoothstep(0.28, 0.62, hatch) - 0.5;

                float field = (low - 0.5) * _ToneFieldStrength;
                field += hatch * _ToneFieldHatchStrength;
                return field;
            }

            float OtherThreshold(int2 p, float luma)
            {
                return lerp(0.5, BlueNoise(p), saturate(_BlueNoiseWeight)) + _ThresholdBias;
            }

            float FaceThreshold(int2 p)
            {
                int2 faceOffset = int2(round(_FaceMatrixOffset.xy));
                float scale = max(_FaceMatrixScale, 0.5);
                int2 matrixPixel = int2(floor((float2(p + faceOffset)) / scale));
                float sparseNoise = BlueNoise(matrixPixel + faceOffset * 7);
                return lerp(0.5, sparseNoise, saturate(_FaceBlueNoiseWeight)) + _ThresholdBias;
            }

            float SurfaceThreshold(int2 p, float luma, float faceMode)
            {
                return lerp(OtherThreshold(p, luma), FaceThreshold(p), faceMode);
            }

            float3 TriplanarWeights(float3 normalWS)
            {
                float sharpness = max(_WorldTriplanarSharpness, 1.0);
                float3 weights = pow(abs(normalize(normalWS)), float3(sharpness, sharpness, sharpness));
                return weights / max(weights.x + weights.y + weights.z, 0.0001);
            }

            float2 WorldPlanePixel(float3 positionWS, int axis)
            {
                float scale = max(_WorldDitherScale, 0.001);

                if (axis == 0)
                    return positionWS.zy * scale;

                if (axis == 1)
                    return positionWS.xz * scale;

                return positionWS.xy * scale;
            }

            float WorldThreshold(float3 positionWS, float3 normalWS, float luma, float faceMode)
            {
                float3 weights = TriplanarWeights(normalWS);

                float thresholdX = SurfaceThreshold(int2(floor(WorldPlanePixel(positionWS, 0))), luma, faceMode);
                float thresholdY = SurfaceThreshold(int2(floor(WorldPlanePixel(positionWS, 1))), luma, faceMode);
                float thresholdZ = SurfaceThreshold(int2(floor(WorldPlanePixel(positionWS, 2))), luma, faceMode);

                return dot(float3(thresholdX, thresholdY, thresholdZ), weights);
            }

            float WorldToneField(float3 positionWS, float3 normalWS)
            {
                float3 weights = TriplanarWeights(normalWS);

                float fieldX = ToneField(WorldPlanePixel(positionWS, 0));
                float fieldY = ToneField(WorldPlanePixel(positionWS, 1));
                float fieldZ = ToneField(WorldPlanePixel(positionWS, 2));

                return dot(float3(fieldX, fieldY, fieldZ), weights);
            }

            float SurfaceMask(float deviceDepth)
            {
                #if UNITY_REVERSED_Z
                    return step(0.00001, deviceDepth);
                #else
                    return step(deviceDepth, 0.99999);
                #endif
            }

            float EdgeFactor(float2 uv)
            {
                float2 texel = _BlitTexture_TexelSize.xy * max(_PixelScale, 1.0);

                float centerDepth = Linear01Depth(SampleSceneDepth(uv), _ZBufferParams);
                float depthRight = Linear01Depth(SampleSceneDepth(uv + float2(texel.x, 0)), _ZBufferParams);
                float depthUp = Linear01Depth(SampleSceneDepth(uv + float2(0, texel.y)), _ZBufferParams);
                float depthEdge = abs(centerDepth - depthRight) + abs(centerDepth - depthUp);

                float3 centerNormal = SampleSceneNormals(uv);
                float3 normalRight = SampleSceneNormals(uv + float2(texel.x, 0));
                float3 normalUp = SampleSceneNormals(uv + float2(0, texel.y));
                float normalEdge = length(centerNormal - normalRight) + length(centerNormal - normalUp);

                float edge = depthEdge * _DepthEdgeScale + normalEdge * _NormalEdgeScale;
                return saturate(edge);
            }

            float4 RenderDither(float2 uv, float faceMode)
            {
                float2 screenSize = max(_BlitTexture_TexelSize.zw, float2(1.0, 1.0));
                float2 pixel = floor(uv * screenSize / max(_PixelScale, 1.0));
                float2 offsetPixels = _RotationOffset.xy * _OffsetStrength;
                int2 ditherPixel = int2(floor(pixel + offsetPixels));
                float rawDepth = SampleSceneDepth(uv);
                float worldBlend = saturate(_WorldDitherWeight) * SurfaceMask(rawDepth);

                #if UNITY_REVERSED_Z
                    float deviceDepth = rawDepth;
                #else
                    float deviceDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif

                float3 positionWS = ComputeWorldSpacePosition(uv, deviceDepth, UNITY_MATRIX_I_VP);
                float3 normalWS = SampleSceneNormals(uv);

                float3 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).rgb;
                float luma = PosterizedLuma(source);
                float toneField = lerp(ToneField(pixel), WorldToneField(positionWS, normalWS), worldBlend);
                luma = saturate(luma + toneField * lerp(1.0, 0.38, faceMode));

                float screenThreshold = SurfaceThreshold(ditherPixel, luma, faceMode);
                float worldThreshold = WorldThreshold(positionWS, normalWS, luma, faceMode);
                float threshold = lerp(screenThreshold, worldThreshold, worldBlend);
                float bit = step(threshold, luma);
                float edge = EdgeFactor(uv) * _EdgeStrength;

                float3 color = lerp(_DarkColor.rgb, _LightColor.rgb, bit);
                float3 edgeColor = lerp(_DarkEdgeColor.rgb, _LightEdgeColor.rgb, step(0.5, _UseLightEdgeColor));
                color = lerp(color, edgeColor, edge);
                return float4(color, 1);
            }
        ENDHLSL

        Pass
        {
            Name "OtherAndTransition"
            Stencil
            {
                Ref 9
                Comp NotEqual
                Pass Keep
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return RenderDither(input.texcoord.xy, 0.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "FaceMatrixOffset"
            Stencil
            {
                Ref 9
                Comp Equal
                Pass Keep
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return RenderDither(input.texcoord.xy, 1.0);
            }
            ENDHLSL
        }
    }

    CustomEditor "ObraDinnPrototype.Editor.ObraDinnShaderGUI"
}
