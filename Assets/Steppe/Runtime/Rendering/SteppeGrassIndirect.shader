Shader "Steppe/Grass Indirect"
{
    Properties
    {
        _BaseMap("Grass Silhouette", 2D) = "white" {}
        _AlphaCutoff("Alpha Cutoff", Range(0, 1)) = 0.36
        _GrassAssetScale("Grass Asset Scale", Float) = 1
        _RootDarkening("Root Darkening", Range(0, 1)) = 0.32
        _TipBrightness("Tip Brightness", Range(0.5, 1.5)) = 1.0
        _SilverStrength("Feather Silver", Range(0, 1)) = 0.16
        _WindBendStrength("Wind Tip Bend", Range(0, 0.75)) = 0.48
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest+20" }
        Cull Off
        ZWrite On
        AlphaToMask On

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/Steppe/Runtime/Rendering/SteppeWindField.hlsl"
            #include "Assets/Steppe/Runtime/Rendering/SteppeEcologyField.hlsl"
            #include "Assets/Steppe/Runtime/Rendering/SteppeTrackField.hlsl"
            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
            #include "UnityIndirect.cginc"

            struct GrassInstance
            {
                float4 positionHeight;
                float4 colorWidth;
                float4 parameters;
                float4 motion;
            };

            StructuredBuffer<GrassInstance> _SteppeGrassInstances;
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half _AlphaCutoff;
                float _GrassAssetScale;
                half _RootDarkening;
                half _TipBrightness;
                half _SilverStrength;
                half _WindBendStrength;
                float _GrassFullDensityRadius;
                float _GrassDrawRadius;
            CBUFFER_END

            float4 _SteppeGrassCellOrigin;
            float4 _SteppeWorldOriginXZ;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 cluster : TEXCOORD1;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half3 color : COLOR;
                half heightAlongBlade : TEXCOORD2;
                half visibility : TEXCOORD3;
                half randomThreshold : TEXCOORD4;
                half fogFactor : TEXCOORD5;
                float2 uv : TEXCOORD6;
                half gust : TEXCOORD7;
                half greenFraction : TEXCOORD8;
                half snowCoverage : TEXCOORD9;
            };

            float3 RotateAroundAxis(float3 value, float3 axis, float angle)
            {
                float sine = sin(angle);
                float cosine = cos(angle);
                return value * cosine
                       + cross(axis, value) * sine
                       + axis * dot(axis, value) * (1.0 - cosine);
            }

            Varyings Vert(Attributes input)
            {
                InitIndirectDrawArgs(0);
                GrassInstance instance = _SteppeGrassInstances[GetIndirectInstanceID(input.instanceID)];
                float sine = instance.parameters.x;
                float cosine = instance.parameters.y;
                float3 assetPosition = input.positionOS * _GrassAssetScale;

                float2 rotatedPosition = float2(
                    assetPosition.x * cosine - assetPosition.z * sine,
                    assetPosition.x * sine + assetPosition.z * cosine);
                float3 rotatedNormal = float3(
                    input.normalOS.x * cosine - input.normalOS.z * sine,
                    input.normalOS.y,
                    input.normalOS.x * sine + input.normalOS.z * cosine);

                float2 rootPosition = _SteppeGrassCellOrigin.xz + instance.positionHeight.xz;
                float2 canonicalRoot = rootPosition + _SteppeWorldOriginXZ.xz;
                SteppeEcologyFieldSample ecology = SampleSteppeEcologyFieldLevel(canonicalRoot, 0.0);
                SteppeTrackFieldSample track = SampleSteppeTrackFieldLevel(canonicalRoot, 0.0);
                half snowCoverage = smoothstep(0.015h, 0.22h, ecology.snowWater);
                float snowLoad = snowCoverage * (1.0h - ecology.snowCompaction * 0.35h);
                float standingHeight = instance.positionHeight.w
                                       * ecology.biomass
                                       * (1.0 - snowLoad * 0.16);
                float dynamicHeight = standingHeight
                                      * lerp(1.0, 0.20, track.vegetationFlattening);
                SteppeWindFieldSample wind = SampleSteppeWindField(
                    canonicalRoot,
                    instance.parameters.z,
                    frac(instance.parameters.w + input.cluster.x));
                float normalizedHeight = saturate(assetPosition.y);
                float bendProfile = pow(normalizedHeight, 1.65);
                float flexibility = lerp(
                    0.28,
                    1.0,
                    smoothstep(0.05, 0.85, instance.parameters.z));
                flexibility *= lerp(1.0, 0.42, ecology.frozenFraction);

                // Broad gusts establish the average down-wind posture. A separate local
                // response clock makes every plant continually flex around that posture,
                // even while the broad pressure field changes only slowly at x1.
                float windStrength = saturate(_SteppeWindVelocity.w);
                float responseHz = lerp(0.35, 1.25, saturate(instance.motion.x))
                                   * lerp(0.65, 1.25, windStrength);
                float spatialPhase = dot(canonicalRoot, wind.direction)
                                     / max(32.0, _SteppeGustScales.z * 2.0)
                                     * 6.2831853;
                float clusterPhase = frac(instance.motion.y + input.cluster.x);
                float randomPhase = clusterPhase
                                    * lerp(1.0, 0.18, saturate(instance.parameters.z))
                                    * 6.2831853;
                float responsePhase = _SteppeWindAnimationTime
                                      * responseHz
                                      * 6.2831853
                                      + spatialPhase
                                      + randomPhase;
                float response = sin(responsePhase);
                float meanLeanRatio = _WindBendStrength
                                      * windStrength
                                      * flexibility
                                      * lerp(0.42, 1.0, wind.strength);
                float responseAmplitude = _WindBendStrength
                                          * windStrength
                                          * flexibility
                                          * lerp(0.07, 0.32, windStrength);
                float alongLeanRatio = clamp(
                    meanLeanRatio + response * responseAmplitude,
                    0.0,
                    0.72);
                float2 crossWind = float2(-wind.direction.y, wind.direction.x);
                float lateralLeanRatio = sin(responsePhase * 0.73 + clusterPhase * 4.1)
                                         * _WindBendStrength
                                         * windStrength
                                         * flexibility
                                         * 0.045;
                float2 leanVector = wind.direction * alongLeanRatio
                                    + crossWind * lateralLeanRatio;
                float totalLeanRatio = min(length(leanVector), 0.72);
                float2 leanDirection = totalLeanRatio > 0.0001
                    ? leanVector / totalLeanRatio
                    : wind.direction;
                float2 bendDistance = dynamicHeight * leanVector * bendProfile;
                float trackAngle = SteppeWindValueNoise(canonicalRoot / 2.15h) * 6.2831853;
                float2 trackDirection = float2(cos(trackAngle), sin(trackAngle));
                float2 trackLay = trackDirection
                                  * standingHeight
                                  * track.vegetationFlattening
                                  * 0.58
                                  * bendProfile;
                float verticalDrop = dynamicHeight
                                     * (1.0 - sqrt(max(0.01, 1.0 - totalLeanRatio * totalLeanRatio)))
                                     * bendProfile;

                float3 worldPosition;
                worldPosition.xz = rootPosition
                                   + rotatedPosition * instance.colorWidth.w
                                   + bendDistance
                                   + trackLay;
                worldPosition.y = instance.positionHeight.y
                                  + assetPosition.y * dynamicHeight
                                  - verticalDrop;

                float bendAngle = asin(totalLeanRatio) * bendProfile;
                float3 bendAxis = float3(leanDirection.y, 0.0, -leanDirection.x);
                rotatedNormal = RotateAroundAxis(rotatedNormal, bendAxis, bendAngle);

                Varyings output;
                output.positionWS = worldPosition;
                output.positionCS = TransformWorldToHClip(worldPosition);
                output.normalWS = normalize(rotatedNormal);
                output.color = instance.colorWidth.rgb;
                output.heightAlongBlade = normalizedHeight;
                output.uv = input.uv;
                output.gust = wind.broad;
                output.greenFraction = ecology.greenFraction;
                output.snowCoverage = snowCoverage;

                float distanceToCamera = distance(worldPosition.xz, _WorldSpaceCameraPos.xz);
                output.visibility = min(ecology.biomass, 1.0h - smoothstep(
                    _GrassFullDensityRadius,
                    _GrassDrawRadius,
                    distanceToCamera));
                output.randomThreshold = instance.parameters.w;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a;
                clip(alpha - _AlphaCutoff);
                clip(input.visibility - input.randomThreshold);

                half3 normal = normalize(input.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half diffuse = 0.30h + abs(dot(normal, mainLight.direction)) * 0.52h;
                half3 ambient = SampleSH(half3(0, 1, 0));
                half3 lighting = ambient * 0.46h
                                 + mainLight.color * diffuse * mainLight.shadowAttenuation;

                half rootFactor = lerp(1.0h - _RootDarkening, _TipBrightness, input.heightAlongBlade);
                half3 viewDirection = SafeNormalize(_WorldSpaceCameraPos - input.positionWS);
                half grazing = pow(1.0h - saturate(abs(dot(normal, viewDirection))), 3.0h);
                half silver = grazing
                              * input.heightAlongBlade
                              * _SilverStrength
                              * lerp(0.62h, 1.24h, input.gust);
                half gustTone = lerp(1.02h, 0.94h, input.gust);
                half3 curedColor = input.color * half3(1.08h, 0.98h, 0.76h);
                half3 livingColor = input.color * half3(0.82h, 1.16h, 0.70h);
                half3 phenologyColor = lerp(curedColor, livingColor, input.greenFraction);
                half snowOnBlade = input.snowCoverage
                                   * smoothstep(0.46h, 0.94h, input.heightAlongBlade);
                phenologyColor = lerp(
                    phenologyColor,
                    half3(0.84h, 0.89h, 0.92h),
                    snowOnBlade * 0.78h);
                half3 color = phenologyColor * rootFactor * lighting * gustTone
                              + silver * mainLight.color;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
