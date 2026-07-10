Shader "RoystanGrassDemo/URP Geometry Grass"
{
    Properties
    {
        _BottomColor ("Bottom Color", Color) = (0.14, 0.36, 0.10, 1)
        _TopColor ("Top Color", Color) = (0.56, 0.88, 0.34, 1)
        _ShadowColor ("Shadow Color", Color) = (0.08, 0.18, 0.08, 1)
        _BladeWidth ("Blade Width", Range(0.005, 0.12)) = 0.035
        _BladeWidthRandom ("Blade Width Random", Range(0, 0.08)) = 0.018
        _BladeHeight ("Blade Height", Range(0.05, 1.4)) = 0.46
        _BladeHeightRandom ("Blade Height Random", Range(0, 1.0)) = 0.34
        _BladeForward ("Forward Bend", Range(0, 0.8)) = 0.28
        _BladeCurve ("Blade Curve", Range(0.5, 6.0)) = 2.2
        _WindStrength ("Wind Strength", Range(0, 0.8)) = 0.18
        _WindFrequency ("Wind Frequency", Range(0, 8)) = 2.2
        _WindScale ("Wind Scale", Range(0.05, 8)) = 1.6
        _WindDirection ("Wind Direction", Vector) = (0.86, 0.0, 0.5, 0.0)
        _TranslucentGain ("Translucent Gain", Range(0, 1)) = 0.34
        _CartoonBanding ("Cartoon Banding", Range(0, 1)) = 0
        _RimColor ("Rim Color", Color) = (0.86, 1.0, 0.38, 1)
        _RimStrength ("Rim Strength", Range(0, 1)) = 0
        _RimPower ("Rim Power", Range(0.5, 6)) = 2.25
        _FogColor ("Fog Color", Color) = (0.62, 0.80, 0.88, 1)
        _FogStart ("Fog Start", Float) = 12
        _FogEnd ("Fog End", Float) = 36
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Cull Off
        ZWrite On

        HLSLINCLUDE
        #pragma target 4.5
        #pragma exclude_renderers gles gles3 glcore

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        #define BLADE_SEGMENTS 4

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
        };

        struct VertexToGeometry
        {
            float3 positionOS : TEXCOORD0;
            float3 normalOS : TEXCOORD1;
            float4 tangentOS : TEXCOORD2;
            float2 uv : TEXCOORD3;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float2 uv : TEXCOORD2;
            float4 shadowCoord : TEXCOORD3;
        };

        CBUFFER_START(UnityPerMaterial)
            float4 _BottomColor;
            float4 _TopColor;
            float4 _ShadowColor;
            float4 _FogColor;
            float4 _WindDirection;
            float4 _RimColor;
            float _BladeWidth;
            float _BladeWidthRandom;
            float _BladeHeight;
            float _BladeHeightRandom;
            float _BladeForward;
            float _BladeCurve;
            float _WindStrength;
            float _WindFrequency;
            float _WindScale;
            float _TranslucentGain;
            float _CartoonBanding;
            float _RimStrength;
            float _RimPower;
            float _FogStart;
            float _FogEnd;
        CBUFFER_END

        float Random01(float3 seed)
        {
            return frac(sin(dot(seed, float3(12.9898, 78.233, 37.719))) * 43758.5453);
        }

        float SignedRandom(float3 seed)
        {
            return Random01(seed) * 2.0 - 1.0;
        }

        VertexToGeometry Vert(Attributes input)
        {
            VertexToGeometry output;
            output.positionOS = input.positionOS.xyz;
            output.normalOS = input.normalOS;
            output.tangentOS = input.tangentOS;
            output.uv = input.uv;
            return output;
        }

        void AppendGrassVertex(
            inout TriangleStream<Varyings> stream,
            float3 baseWS,
            float3 bladeRightWS,
            float3 bladeForwardWS,
            float3 bladeUpWS,
            float sideOffset,
            float heightOffset,
            float forwardOffset,
            float2 uv,
            bool shadowPass)
        {
            float3 positionWS = baseWS
                + bladeRightWS * sideOffset
                + bladeUpWS * heightOffset
                + bladeForwardWS * forwardOffset;

            Varyings output;
            output.positionWS = positionWS;
            output.normalWS = normalize(bladeForwardWS + bladeUpWS * 0.18);
            output.uv = uv;
            output.shadowCoord = TransformWorldToShadowCoord(positionWS);
            output.positionCS = TransformWorldToHClip(positionWS);
            stream.Append(output);
        }

        void EmitBlade(
            triangle VertexToGeometry input[3],
            inout TriangleStream<Varyings> stream,
            bool shadowPass)
        {
            float3 baseOS = (input[0].positionOS + input[1].positionOS + input[2].positionOS) / 3.0;
            float3 normalOS = normalize(input[0].normalOS + input[1].normalOS + input[2].normalOS);
            float4 tangentOS = normalize(input[0].tangentOS + input[1].tangentOS + input[2].tangentOS);

            float3 baseWS = TransformObjectToWorld(baseOS);
            float3 upWS = normalize(TransformObjectToWorldNormal(normalOS));
            float3 tangentWS = normalize(TransformObjectToWorldDir(tangentOS.xyz));
            float3 bitangentWS = normalize(cross(upWS, tangentWS) * tangentOS.w);

            float angle = Random01(baseOS) * TWO_PI;
            float s, c;
            sincos(angle, s, c);

            float3 rightWS = normalize(tangentWS * c + bitangentWS * s);
            float3 forwardWS = normalize(-tangentWS * s + bitangentWS * c);

            float width = max(0.001, _BladeWidth + SignedRandom(baseOS.zyx + 3.17) * _BladeWidthRandom);
            float height = max(0.01, _BladeHeight + SignedRandom(baseOS.xzy + 7.91) * _BladeHeightRandom);
            float forwardBase = _BladeForward * Random01(baseOS.yxz + 11.47);

            float2 windDirXZ = normalize(_WindDirection.xz + float2(0.0001, 0.0001));
            float windA = sin(dot(baseWS.xz, windDirXZ) * _WindScale + _Time.y * _WindFrequency);
            float windB = sin(dot(baseWS.xz, windDirXZ.yx * float2(-1.0, 1.0)) * (_WindScale * 1.73) + _Time.y * (_WindFrequency * 0.63));
            float wind = (windA * 0.65 + windB * 0.35) * _WindStrength;
            float3 windWS = normalize(float3(windDirXZ.x, 0.0, windDirXZ.y));
            forwardWS = normalize(forwardWS + windWS * wind);

            [unroll]
            for (int i = 0; i < BLADE_SEGMENTS; i++)
            {
                float t = i / (float)BLADE_SEGMENTS;
                float segmentWidth = width * (1.0 - t);
                float segmentHeight = height * t;
                float segmentForward = pow(t, _BladeCurve) * forwardBase + wind * t * height * 0.35;

                AppendGrassVertex(stream, baseWS, rightWS, forwardWS, upWS, segmentWidth, segmentHeight, segmentForward, float2(0.0, t), shadowPass);
                AppendGrassVertex(stream, baseWS, rightWS, forwardWS, upWS, -segmentWidth, segmentHeight, segmentForward, float2(1.0, t), shadowPass);
            }

            float tipForward = forwardBase + wind * height * 0.42;
            AppendGrassVertex(stream, baseWS, rightWS, forwardWS, upWS, 0.0, height, tipForward, float2(0.5, 1.0), shadowPass);
        }

        [maxvertexcount(BLADE_SEGMENTS * 2 + 1)]
        void Geo(triangle VertexToGeometry input[3], inout TriangleStream<Varyings> stream)
        {
            EmitBlade(input, stream, false);
        }

        [maxvertexcount(BLADE_SEGMENTS * 2 + 1)]
        void GeoShadow(triangle VertexToGeometry input[3], inout TriangleStream<Varyings> stream)
        {
            EmitBlade(input, stream, true);
        }
        ENDHLSL

        Pass
        {
            Name "GrassForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma geometry Geo
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            float4 Frag(Varyings input, FRONT_FACE_TYPE frontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                #if defined(SHADER_STAGE_FRAGMENT)
                normalWS *= IS_FRONT_VFACE(frontFace, 1.0, -1.0);
                #endif

                Light mainLight = GetMainLight(input.shadowCoord);
                float ndl = saturate(dot(normalWS, normalize(mainLight.direction)) * 0.5 + 0.5);
                float backLight = saturate(dot(-normalWS, normalize(mainLight.direction))) * _TranslucentGain;

                float3 grassColor = lerp(_BottomColor.rgb, _TopColor.rgb, saturate(input.uv.y));
                float smoothBand = smoothstep(0.12, 0.78, ndl);
                float toonBand = saturate(floor(ndl * 3.0) * 0.5);
                float shadowBand = lerp(smoothBand, toonBand, saturate(_CartoonBanding));
                float3 litColor = lerp(_ShadowColor.rgb, grassColor, shadowBand);
                float smoothLight = 0.45 + ndl * 0.75 + backLight;
                float toonLight = 0.55 + shadowBand * 0.36 + backLight * 0.22;
                litColor *= mainLight.color.rgb * lerp(smoothLight, toonLight, saturate(_CartoonBanding));
                litColor *= lerp(0.42, 1.0, mainLight.shadowAttenuation);
                litColor += grassColor * SampleSH(normalWS) * lerp(0.36, 0.18, saturate(_CartoonBanding));

                float3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                float rim = pow(1.0 - saturate(abs(dot(viewDirWS, normalWS))), _RimPower);
                litColor += _RimColor.rgb * rim * _RimStrength;

                float distanceToCamera = distance(GetCameraPositionWS(), input.positionWS);
                float fog = saturate((distanceToCamera - _FogStart) / max(_FogEnd - _FogStart, 0.001));
                litColor = lerp(litColor, _FogColor.rgb, fog * fog);

                return float4(saturate(litColor), 1.0);
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
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma geometry GeoShadow
            #pragma fragment ShadowFrag

            float4 ShadowFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
