Shader "Hidden/Steppe/Volumetric Clouds"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Transparent"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        HLSLINCLUDE
        #pragma target 4.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        struct Attributes
        {
            uint vertexID : SV_VertexID;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings FullscreenVert(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
            output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
            return output;
        }
        ENDHLSL

        Pass
        {
            Name "RaymarchCloudVolume"
            Blend Off

            HLSLPROGRAM
            #pragma vertex FullscreenVert
            #pragma fragment RaymarchFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/Steppe/Runtime/Rendering/SteppeNaturalProcessField.hlsl"

            #define STEPPE_CLOUD_VIEW_STEPS 20
            #define STEPPE_CLOUD_LIGHT_STEPS 3

            TEXTURE2D(_SteppeCloudWeatherMap);
            SAMPLER(sampler_SteppeCloudWeatherMap);
            TEXTURE3D(_SteppeCloudNoise3D);
            SAMPLER(sampler_SteppeCloudNoise3D);

            float _SteppeCloudRendererActive;
            float4 _SteppeCloudMapCenterLocal;
            float4 _SteppeCloudMapAdvectionLocal;
            float4 _SteppeCloudNoiseWorldPhase;
            float4 _SteppeCloudNoiseAdvectionLocal;
            float4 _SteppeCloudLayerParameters;
            float _SteppeCloudMapWorldSize;
            float _SteppeDaylight;
            float4 _SteppeFiniteWorldOriginXZ;

            float Hash12(float2 value)
            {
                float3 p = frac(float3(value.xyx) * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float4 SampleWeather(float3 positionWS)
            {
                float2 mapPosition = positionWS.xz
                                     - _SteppeCloudMapCenterLocal.xy
                                     - _SteppeCloudMapAdvectionLocal.xy;
                float2 uv = mapPosition / max(_SteppeCloudMapWorldSize, 1.0) + 0.5;
                float edgeDistance = min(uv.x, min(uv.y, min(1.0 - uv.x, 1.0 - uv.y)));
                float mapFade = smoothstep(0.0, 0.055, edgeDistance);
                float3 weather = SAMPLE_TEXTURE2D_LOD(
                    _SteppeCloudWeatherMap,
                    sampler_SteppeCloudWeatherMap,
                    saturate(uv),
                    0).rgb;
                return float4(weather, mapFade);
            }

            float SampleDensity(float3 positionWS, float4 weather, float distanceFade)
            {
                float cloudBase = _SteppeCloudLayerParameters.x;
                float thickness = max(_SteppeCloudLayerParameters.y, 1.0);
                float height01 = saturate((positionWS.y - cloudBase) / thickness);
                float bottomProfile = smoothstep(0.0, 0.14, height01);
                float topStart = lerp(0.58, 0.76, weather.g);
                float topProfile = 1.0 - smoothstep(topStart, 1.0, height01);
                float heightProfile = bottomProfile * topProfile;

                float2 canonicalXZ = positionWS.xz
                                     - _SteppeCloudMapCenterLocal.xy
                                     + _SteppeCloudNoiseWorldPhase.xy
                                     - _SteppeCloudNoiseAdvectionLocal.xy;
                float3 noiseUv = frac(float3(
                    canonicalXZ.x / 4800.0,
                    (positionWS.y - cloudBase) / (thickness * 1.18),
                    canonicalXZ.y / 4800.0));
                float4 noise = SAMPLE_TEXTURE3D_LOD(
                    _SteppeCloudNoise3D,
                    sampler_SteppeCloudNoise3D,
                    noiseUv,
                    0);

                float broadShape = noise.r * 0.62 + noise.g * 0.26 + noise.b * 0.12;
                float coverageShape = broadShape + weather.r * 0.56 + weather.g * 0.14 - 0.68;
                float density = saturate(coverageShape * 3.6);
                density = saturate(density - (1.0 - noise.a) * 0.22 * (1.0 - density));
                density *= heightProfile;
                density *= lerp(0.82, 1.32, weather.g);
                density *= weather.a * distanceFade;
                return saturate(density);
            }

            float LightTransmission(float3 positionWS, float3 lightDirection, float4 weather)
            {
                float opticalDepth = 0.0;
                const float lightStepLength = 260.0;

                [unroll]
                for (int stepIndex = 0; stepIndex < STEPPE_CLOUD_LIGHT_STEPS; stepIndex++)
                {
                    float3 lightPosition = positionWS
                                         + lightDirection * (stepIndex + 0.65) * lightStepLength;
                    opticalDepth += SampleDensity(lightPosition, weather, 1.0) * lightStepLength;
                }

                return exp(-opticalDepth * 0.0024);
            }

            half4 RaymarchFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                if (_SteppeCloudRendererActive < 0.5)
                {
                    return 0;
                }

                float rawFarDepth = UNITY_REVERSED_Z ? 0.0 : 1.0;
                float3 farPositionWS = ComputeWorldSpacePosition(
                    input.uv,
                    rawFarDepth,
                    UNITY_MATRIX_I_VP);
                float3 rayOrigin = GetCameraPositionWS();
                float3 rayDirection = normalize(farPositionWS - rayOrigin);
                if (rayDirection.y <= 0.0005)
                {
                    return 0;
                }

                float cloudBase = _SteppeCloudLayerParameters.x;
                float cloudTop = cloudBase + _SteppeCloudLayerParameters.y;
                float maximumDistance = max(_SteppeCloudLayerParameters.z, 1.0);
                float entryDistance = (cloudBase - rayOrigin.y) / rayDirection.y;
                float exitDistance = (cloudTop - rayOrigin.y) / rayDirection.y;
                float marchStart = max(entryDistance, 0.0);
                float marchEnd = min(exitDistance, maximumDistance);
                if (marchEnd <= marchStart)
                {
                    return 0;
                }

                float stepLength = (marchEnd - marchStart) / STEPPE_CLOUD_VIEW_STEPS;
                float jitter = Hash12(floor(input.positionCS.xy));
                float sampleDistance = marchStart + stepLength * jitter;
                float transmittance = 1.0;
                float3 integratedLight = 0.0;
                Light mainLight = GetMainLight();
                float3 lightDirection = normalize(mainLight.direction);
                float daylight = saturate(_SteppeDaylight);
                float3 ambientColor = lerp(
                    float3(0.025, 0.035, 0.060),
                    float3(0.46, 0.53, 0.61),
                    0.12 + daylight * 0.88);
                float forwardPhase = 0.58
                                   + pow(saturate(dot(rayDirection, lightDirection)), 8.0) * 0.72;

                [loop]
                for (int stepIndex = 0; stepIndex < STEPPE_CLOUD_VIEW_STEPS; stepIndex++)
                {
                    if (sampleDistance >= marchEnd || transmittance < 0.012)
                    {
                        break;
                    }

                    float3 samplePosition = rayOrigin + rayDirection * sampleDistance;
                    float farFade = 1.0 - smoothstep(
                        maximumDistance * 0.76,
                        maximumDistance,
                        sampleDistance);
                    float4 weather = SampleWeather(samplePosition);
                    float density = SampleDensity(samplePosition, weather, farFade);
                    float2 finiteCanonicalXZ = samplePosition.xz
                                               + _SteppeFiniteWorldOriginXZ.xz;
                    SteppeNaturalCloudProcessSample cloudProcess =
                        SampleSteppeNaturalCloudProcessLevel(finiteCanonicalXZ, 0.0);
                    float evaporationStrength = sqrt(saturate(
                        cloudProcess.cloudEvaporationRate / 0.03));
                    if (evaporationStrength > 0.0001)
                    {
                        float edge = 1.0 - smoothstep(0.16, 0.68, density);
                        // Vertical perforation shafts expand radially through dilute
                        // cloud matter. A view ray can therefore see the phase loss
                        // through the whole layer instead of having the void filled by
                        // cloud farther along the same ray. Rate owns shaft population.
                        float2 dissolveCoordinates = finiteCanonicalXZ / 310.0;
                        float2 dissolveCell = floor(dissolveCoordinates);
                        float2 dissolveLocal = frac(dissolveCoordinates) - 0.5;
                        float dissolveSeed = Hash12(dissolveCell + 23.71);
                        float dissolvePhase = frac(dissolveSeed + _Time.y * 0.27);
                        float dissolveRadius = lerp(0.08, 0.94, dissolvePhase);
                        float radialDistance = length(dissolveLocal * 2.0);
                        float expandingVoid = 1.0 - smoothstep(
                            dissolveRadius - 0.11,
                            dissolveRadius + 0.08,
                            radialDistance);
                        float populationSeed = Hash12(dissolveCell + 91.43);
                        float shaftPopulation = smoothstep(
                            1.0 - evaporationStrength,
                            min(1.0, 1.12 - evaporationStrength),
                            populationSeed);
                        float perforation = expandingVoid * edge * shaftPopulation;
                        density *= 1.0 - saturate(perforation * 0.98);
                    }
                    if (density > 0.004)
                    {
                        float lightTransmission = LightTransmission(
                            samplePosition,
                            lightDirection,
                            weather);
                        float waterWeight = smoothstep(0.18, 0.92, weather.g);
                        float ambientStrength = lerp(0.54, 0.25, waterWeight);
                        float3 directLight = mainLight.color
                                           * (0.18 + lightTransmission * 0.92)
                                           * forwardPhase;
                        float3 sampleLight = ambientColor * ambientStrength + directLight;

                        // Condensation: stationary pearlescent micro-beads inside
                        // existing cloud matter; flux owns their occurrence only.
                        float condensationStrength = sqrt(saturate(
                            cloudProcess.condensationRate / 2.5));
                        float pearlNoise = Hash12(
                            floor(finiteCanonicalXZ / 58.0)
                            + floor(samplePosition.y / 46.0));
                        float pearl = smoothstep(0.91, 0.995, pearlNoise)
                                      * condensationStrength;

                        // Cloud transport: concentric consolidation/dispersion arcs.
                        // Positive net change contracts; negative change expands.
                        float2 processCell = floor(
                            (finiteCanonicalXZ - _SteppeNaturalProcessMapBounds.xy)
                            * _SteppeNaturalProcessGrid.w);
                        float2 processCenter = _SteppeNaturalProcessMapBounds.xy
                                             + (processCell + 0.5)
                                               * _SteppeNaturalProcessGrid.z;
                        float transportStrength = sqrt(saturate(
                            abs(cloudProcess.cloudTransportRate) / 0.06));
                        float transportSign = cloudProcess.cloudTransportRate >= 0.0
                            ? 1.0
                            : -1.0;
                        float radialPhase = length(finiteCanonicalXZ - processCenter) * 0.033
                                          + _Time.y * transportSign * 0.62;
                        float transportArc = pow(
                            0.5 + 0.5 * sin(radialPhase),
                            12.0) * transportStrength;

                        // Cloud advection: combed pulses travel only along measured XY.
                        float2 cloudDirection = cloudProcess.cloudAdvectionVector
                                              / max(
                                                  0.0001,
                                                  length(cloudProcess.cloudAdvectionVector));
                        float2 cloudSide = float2(-cloudDirection.y, cloudDirection.x);
                        float advectionStrength = sqrt(saturate(
                            cloudProcess.cloudAdvectionGrossMagnitude / 0.8));
                        float along = dot(finiteCanonicalXZ, cloudDirection);
                        float across = dot(finiteCanonicalXZ, cloudSide);
                        float movingPulse = pow(
                            0.5 + 0.5 * sin(
                                along * 0.014
                                - _Time.y * lerp(0.22, 1.3, advectionStrength)),
                            10.0);
                        float comb = pow(0.5 + 0.5 * sin(across * 0.021), 8.0);
                        float filament = movingPulse * comb * advectionStrength;
                        sampleLight *= 1.0
                                     + pearl * 0.16
                                     + transportArc * 0.075
                                     + filament * 0.10;
                        float sampleOpacity = 1.0 - exp(-density * stepLength * 0.00215);
                        integratedLight += transmittance * sampleOpacity * sampleLight;
                        transmittance *= 1.0 - sampleOpacity;
                    }

                    sampleDistance += stepLength;
                }

                float alpha = saturate(1.0 - transmittance);
                return half4(integratedLight, alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "CompositeCloudVolume"
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex FullscreenVert
            #pragma fragment CompositeFrag

            TEXTURE2D_X(_SteppeCloudTexture);
            TEXTURE2D_X_FLOAT(_SteppeCloudCameraDepthTexture);
            float4 _SteppeCloudUvScaleBias;
            float4 _SteppeDepthUvScaleBias;

            half4 CompositeFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 cloudUv = input.uv * _SteppeCloudUvScaleBias.xy
                               + _SteppeCloudUvScaleBias.zw;
                float2 depthUv = input.uv * _SteppeDepthUvScaleBias.xy
                               + _SteppeDepthUvScaleBias.zw;
                float rawDepth = SAMPLE_TEXTURE2D_X_LOD(
                    _SteppeCloudCameraDepthTexture,
                    sampler_PointClamp,
                    depthUv,
                    0).r;

                #if UNITY_REVERSED_Z
                    if (rawDepth > 0.0001)
                    {
                        return 0;
                    }
                #else
                    if (rawDepth < 0.9999)
                    {
                        return 0;
                    }
                #endif

                return SAMPLE_TEXTURE2D_X(
                    _SteppeCloudTexture,
                    sampler_LinearClamp,
                    cloudUv);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
