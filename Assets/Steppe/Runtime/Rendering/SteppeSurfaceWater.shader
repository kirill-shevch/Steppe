Shader "Steppe/Surface Water"
{
    Properties
    {
        _ShallowColor("Shallow Water", Color) = (0.18, 0.34, 0.31, 0.34)
        _DeepColor("Deep Water", Color) = (0.035, 0.13, 0.17, 0.64)
        _SurfaceLift("Surface Lift", Range(0.005, 0.08)) = 0.026
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent-20"
        }

        Pass
        {
            Name "SurfaceWater"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/Steppe/Runtime/Rendering/SteppeCloudShadow.hlsl"
            #include "Assets/Steppe/Runtime/Rendering/SteppeWindField.hlsl"
            #include "Assets/Steppe/Runtime/Rendering/SteppeNaturalVisualField.hlsl"
            #include "Assets/Steppe/Runtime/Rendering/SteppeNaturalProcessField.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half _SurfaceLift;
            CBUFFER_END

            float4 _SteppeWorldOriginXZ;
            float4 _SteppeFiniteWorldOriginXZ;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 detailCanonicalXZ : TEXCOORD2;
                float2 finiteCanonicalXZ : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 undisplacedWS = TransformObjectToWorld(input.positionOS.xyz);
                float2 detailCanonicalXZ = undisplacedWS.xz + _SteppeWorldOriginXZ.xz;
                float2 finiteCanonicalXZ = undisplacedWS.xz
                                           + _SteppeFiniteWorldOriginXZ.xz;
                float surfaceWaterMm = SampleSteppeNaturalSurfaceWaterLevel(
                    finiteCanonicalXZ,
                    0.0);
                float coverage = smoothstep(0.025, 0.30, surfaceWaterMm);
                float3 displacedOS = input.positionOS.xyz;
                displacedOS.y += coverage
                                 * (_SurfaceLift + min(surfaceWaterMm * 0.004, 0.24));
                VertexPositionInputs positionInputs = GetVertexPositionInputs(displacedOS);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.detailCanonicalXZ = detailCanonicalXZ;
                output.finiteCanonicalXZ = finiteCanonicalXZ;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            float2 SteppeWaterHash22(float2 cell)
            {
                float2 projected = float2(
                    dot(cell, float2(41.7, 289.1)),
                    dot(cell, float2(173.3, 97.5)));
                return frac(sin(projected) * 43758.5453);
            }

            float SteppeInfiltrationFrontHeight(
                float2 canonicalXZ,
                float permeabilityMmPerHour,
                float infiltrationRate)
            {
                float2 frontPosition = canonicalXZ / 2.7;
                float2 frontCell = floor(frontPosition);
                float2 centered = frac(frontPosition) - 0.5;
                float radius = length(centered) * 1.85;
                float normalizedSpeed = saturate(permeabilityMmPerHour / 30.0);
                float phase = frac(
                    radius
                    + _Time.y * normalizedSpeed * 0.34
                    + SteppeWaterHash22(frontCell).x);
                // Positive time moves a constant phase toward the source: these are
                // converging infiltration fronts, not precipitation ripples.
                float ring = 1.0 - smoothstep(0.035, 0.115, abs(phase - 0.5));
                float activeInfiltration = 1.0 - exp(-max(0.0, infiltrationRate) * 5.0);
                return -ring
                       * smoothstep(0.0, 0.08, normalizedSpeed)
                       * activeInfiltration
                       * 0.0032;
            }

            float SteppeWaterProcessHeight(
                float2 canonicalXZ,
                SteppeNaturalHydrologyProcessSample process)
            {
                float2 impactPosition = canonicalXZ / 0.78;
                float2 impactCell = floor(impactPosition);
                float2 impactCenter = SteppeWaterHash22(impactCell) - 0.5;
                float impactRadius = length(frac(impactPosition) - 0.5 - impactCenter * 0.42);
                float rainfallPhase = frac(
                    impactRadius * 1.9
                    - _Time.y * 1.35
                    + SteppeWaterHash22(impactCell).y);
                float rainfallRing = 1.0 - smoothstep(0.025, 0.095, abs(rainfallPhase - 0.5));
                float rainfallActivity = 1.0 - exp(-process.rainfallRate * 7.0);

                float2 eddyPosition = canonicalXZ / 4.8;
                float2 eddyCell = floor(eddyPosition);
                float2 eddyLocal = frac(eddyPosition) - 0.5;
                float eddyRadius = length(eddyLocal);
                float eddyAngle = atan2(eddyLocal.y, eddyLocal.x);
                float eddy = sin(
                    eddyAngle * 2.0
                    - _Time.y * 1.8
                    + eddyRadius * 13.0
                    + SteppeWaterHash22(eddyCell).x * 6.28318);
                eddy *= 1.0 - smoothstep(0.08, 0.48, eddyRadius);
                float runoffInActivity = 1.0 - exp(-process.runoffInRate * 4.0);

                float2 runoffVector = process.surfaceRunoffVector;
                float runoffMagnitude = max(
                    process.surfaceRunoffMagnitude,
                    process.runoffOutRate);
                float2 runoffDirection = runoffVector
                                          / max(0.0001, length(runoffVector));
                float alongFlow = dot(canonicalXZ, runoffDirection);
                float acrossFlow = dot(
                    canonicalXZ,
                    float2(-runoffDirection.y, runoffDirection.x));
                float directionalWave = sin(
                    alongFlow * 1.45
                    - _Time.y * (1.2 + min(4.0, runoffMagnitude * 2.0)))
                    * (0.62 + 0.38 * sin(acrossFlow * 0.53));
                float runoffActivity = (1.0 - exp(-runoffMagnitude * 4.0))
                                       * step(0.0001, length(runoffVector));

                float dischargeNoise = SteppeWindValueNoise(canonicalXZ / 1.35 + 118.7);
                float dischargeBulge = pow(saturate(dischargeNoise), 9.0)
                                       * sin(_Time.y * 4.1 + dischargeNoise * 9.0);
                float dischargeActivity = 1.0
                                          - exp(-process.groundwaterDischargeRate * 8.0);

                return rainfallRing * rainfallActivity * 0.0038
                       + eddy * runoffInActivity * 0.0055
                       + directionalWave * runoffActivity * 0.0065
                       + dischargeBulge * dischargeActivity * 0.0045;
            }

            float SteppeWaterDimpleHeight(
                float2 canonicalXZ,
                float permeabilityMmPerHour,
                SteppeNaturalHydrologyProcessSample process)
            {
                float2 position = canonicalXZ / 0.72;
                float2 cell = floor(position);
                float2 local = frac(position);
                float nearest = 4.0;
                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 neighbour = float2(x, y);
                        float2 feature = neighbour
                                         + SteppeWaterHash22(cell + neighbour)
                                         - local;
                        nearest = min(nearest, dot(feature, feature));
                    }
                }

                // Isotropic capillary dimples do not imply a flow direction.
                float capillary = cos(sqrt(nearest) * 13.0)
                                  * exp(-nearest * 8.0)
                                  * 0.0045;
                return capillary
                       + SteppeInfiltrationFrontHeight(
                           canonicalXZ,
                           permeabilityMmPerHour,
                           process.infiltrationRate)
                       + SteppeWaterProcessHeight(canonicalXZ, process);
            }

            half3 SteppeWaterNormal(
                float2 canonicalXZ,
                half3 terrainNormal,
                float permeabilityMmPerHour,
                SteppeNaturalHydrologyProcessSample process)
            {
                const float epsilon = 0.045;
                float left = SteppeWaterDimpleHeight(
                    canonicalXZ - float2(epsilon, 0.0),
                    permeabilityMmPerHour,
                    process);
                float right = SteppeWaterDimpleHeight(
                    canonicalXZ + float2(epsilon, 0.0),
                    permeabilityMmPerHour,
                    process);
                float down = SteppeWaterDimpleHeight(
                    canonicalXZ - float2(0.0, epsilon),
                    permeabilityMmPerHour,
                    process);
                float up = SteppeWaterDimpleHeight(
                    canonicalXZ + float2(0.0, epsilon),
                    permeabilityMmPerHour,
                    process);
                return normalize(terrainNormal + half3(
                    -(right - left) / (epsilon * 2.0),
                    0.0,
                    -(up - down) / (epsilon * 2.0)));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                SteppeNaturalVisualFieldSample field = SampleSteppeNaturalVisualField(
                    input.finiteCanonicalXZ);
                SteppeNaturalHydrologyProcessSample process =
                    SampleSteppeNaturalHydrologyProcess(input.finiteCanonicalXZ);
                float surfaceWaterMm = field.surfaceWaterMm;
                clip(surfaceWaterMm - 0.025);

                half coverage = smoothstep(0.025h, 1.5h, surfaceWaterMm);
                half depth = smoothstep(0.20h, 38.0h, surfaceWaterMm);
                half3 normal = SteppeWaterNormal(
                    input.detailCanonicalXZ,
                    normalize(input.normalWS),
                    field.permeabilityMmPerHour,
                    process);
                half3 viewDirection = SafeNormalize(_WorldSpaceCameraPos - input.positionWS);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half3 halfDirection = SafeNormalize(mainLight.direction + viewDirection);
                half fresnel = pow(1.0h - saturate(dot(normal, viewDirection)), 4.0h);
                half specular = pow(saturate(dot(normal, halfDirection)), 150.0h)
                                * mainLight.shadowAttenuation;
                half cloudLightAdjustment = SteppeCloudLightAdjustment(input.positionWS);
                half3 tint = lerp(_ShallowColor.rgb, _DeepColor.rgb, depth);
                half3 skyReflection = SampleSH(normal);
                half3 color = lerp(tint, skyReflection, 0.18h + fresnel * 0.52h);
                color += mainLight.color
                         * cloudLightAdjustment
                         * specular
                         * 0.82h;
                color = MixFog(color, input.fogFactor);
                half alpha = lerp(_ShallowColor.a, _DeepColor.a, depth)
                             * lerp(0.42h, 1.0h, coverage)
                             + fresnel * 0.12h;
                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
