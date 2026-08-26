Shader "Steppe/Snow Layer"
{
    Properties
    {
        _FreshSnowColor("Fresh Snow", Color) = (0.78, 0.85, 0.88, 1)
        _DenseSnowColor("Dense Snow", Color) = (0.64, 0.73, 0.78, 1)
        _SurfaceLift("Surface Lift", Range(0.005, 0.08)) = 0.032
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Geometry+15"
        }

        Pass
        {
            Name "SnowLayer"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
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
                half4 _FreshSnowColor;
                half4 _DenseSnowColor;
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
                half3 normalWS : TEXCOORD1;
                float2 detailCanonicalXZ : TEXCOORD2;
                float2 finiteCanonicalXZ : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            };

            float SteppeSnowDepressionHeight(
                float2 canonicalXZ,
                float depressionStorageMm)
            {
                float2 cellIndex = SteppeNaturalVisualCellIndex(canonicalXZ);
                float2 cellCenter = SteppeNaturalVisualCellCenter(cellIndex);
                float hash = frac(sin(dot(cellIndex, float2(71.71, 193.13))) * 43758.5453);
                float2 basinOffset = float2(hash - 0.5, frac(hash * 7.17) - 0.5)
                                     * _SteppeNaturalVisualGrid.z
                                     * 0.12;
                float normalizedRadius = length(
                    canonicalXZ - (cellCenter + basinOffset))
                    / max(0.01, _SteppeNaturalVisualGrid.z * 0.43);
                float bowl = 1.0 - smoothstep(0.12, 1.0, normalizedRadius);
                return -min(0.22, max(0.0, depressionStorageMm) * 0.001)
                       * bowl
                       * bowl;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 undisplacedWS = TransformObjectToWorld(input.positionOS.xyz);
                float2 detailCanonicalXZ = undisplacedWS.xz + _SteppeWorldOriginXZ.xz;
                float2 finiteCanonicalXZ = undisplacedWS.xz
                                           + _SteppeFiniteWorldOriginXZ.xz;
                float snowMm = SampleSteppeNaturalSnowLevel(finiteCanonicalXZ, 0.0);
                SteppeNaturalHydrologyFieldSample hydrology =
                    SampleSteppeNaturalHydrologyFieldLevel(finiteCanonicalXZ, 0.0);
                float coverage = smoothstep(0.12, 1.2, snowMm)
                                 * smoothstep(0.34, 0.76, input.normalOS.y);
                float3 displacedOS = input.positionOS.xyz;
                displacedOS.y += SteppeSnowDepressionHeight(
                    finiteCanonicalXZ,
                    hydrology.depressionStorageMm);
                displacedOS.y += coverage
                                 * (_SurfaceLift + min(snowMm * 0.0055, 0.68));
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

            float SteppeSnowSegmentDistance(
                float2 samplePoint,
                float2 segmentStart,
                float2 segmentEnd,
                out float alongMetres)
            {
                float2 segment = segmentEnd - segmentStart;
                float lengthSquared = max(0.0001, dot(segment, segment));
                float t = saturate(dot(samplePoint - segmentStart, segment) / lengthSquared);
                alongMetres = t * sqrt(lengthSquared);
                return length(samplePoint - (segmentStart + segment * t));
            }

            float SteppeSnowDrainageSegmentHeight(
                float2 canonicalXZ,
                float2 sourceCell,
                float4 drainage)
            {
                if (length(drainage.xy) < 0.35)
                {
                    return 0.0;
                }

                float2 targetCell = sourceCell + sign(drainage.xy);
                if (targetCell.x < 0.0 || targetCell.y < 0.0
                    || targetCell.x >= _SteppeNaturalVisualGrid.x
                    || targetCell.y >= _SteppeNaturalVisualGrid.y)
                {
                    return 0.0;
                }

                float alongMetres;
                float distanceToAxis = SteppeSnowSegmentDistance(
                    canonicalXZ,
                    SteppeNaturalVisualCellCenter(sourceCell),
                    SteppeNaturalVisualCellCenter(targetCell),
                    alongMetres);
                float width = 0.72 + drainage.z * 0.46 + drainage.w * 0.34;
                float profile = 1.0 - smoothstep(width, width + 0.75, distanceToAxis);
                profile *= profile;
                float depth = 0.022 + drainage.z * 0.0095 + drainage.w * 0.011;
                float downstreamScour = lerp(0.78, 1.12, frac(alongMetres / 10.5));
                return -depth * profile * downstreamScour;
            }

            float SteppeSnowDrainageRelief(float2 canonicalXZ, float snowMm)
            {
                float3 uvAndMask = SteppeNaturalVisualUvAndMask(canonicalXZ);
                if (uvAndMask.z < 0.5)
                {
                    return 0.0;
                }

                float2 centerCell = SteppeNaturalVisualCellIndex(canonicalXZ);
                float relief = 0.0;
                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 sourceCell = centerCell + float2(x, y);
                        if (sourceCell.x < 0.0 || sourceCell.y < 0.0
                            || sourceCell.x >= _SteppeNaturalVisualGrid.x
                            || sourceCell.y >= _SteppeNaturalVisualGrid.y)
                        {
                            continue;
                        }

                        relief = min(
                            relief,
                            SteppeSnowDrainageSegmentHeight(
                                canonicalXZ,
                                sourceCell,
                                SampleSteppeNaturalDrainageCellLevel(sourceCell, 0.0)));
                    }
                }

                // Deep snow gradually fills small rills, while the catchment network
                // remains legible through the shallow winter cover used at startup.
                return relief * (1.0 - smoothstep(18.0, 90.0, snowMm));
            }

            half3 SteppeSnowHydrologyNormal(
                float3 positionWS,
                half3 terrainNormal,
                float2 finiteCanonicalXZ,
                float snowMm)
            {
                float drainageHeight = SteppeSnowDrainageRelief(
                    finiteCanonicalXZ,
                    snowMm);
                float3 tangentX = ddx(positionWS);
                float3 tangentY = ddy(positionWS);
                tangentX.y += ddx(drainageHeight);
                tangentY.y += ddy(drainageHeight);
                half3 reliefNormal = normalize(cross(tangentY, tangentX));
                if (dot(reliefNormal, terrainNormal) < 0.0h)
                {
                    reliefNormal = -reliefNormal;
                }
                return normalize(lerp(terrainNormal, reliefNormal, 0.84h));
            }

            float SteppeSnowProcessRelief(
                float2 canonicalXZ,
                SteppeNaturalHydrologyProcessSample process,
                float2 drainageDirection)
            {
                float2 impactPosition = canonicalXZ / 0.66;
                float2 impactCell = floor(impactPosition);
                float impactHash = frac(
                    sin(dot(impactCell, float2(39.13, 211.7))) * 43758.5453);
                float impactRadius = length(
                    frac(impactPosition)
                    - 0.5
                    - float2(impactHash - 0.5, frac(impactHash * 5.27) - 0.5) * 0.32);
                float impact = -exp(-impactRadius * impactRadius * 74.0)
                               * (1.0 - exp(-process.snowfallRate * 7.0))
                               * 0.0045;

                float transportLength = length(process.snowTransportVector);
                float2 transportDirection = process.snowTransportVector
                                            / max(0.0001, transportLength);
                float2 transportSide = float2(-transportDirection.y, transportDirection.x);
                float transportRidge = sin(
                    dot(canonicalXZ, transportSide) * 0.82
                    + dot(canonicalXZ, transportDirection) * 0.055
                    - _Time.y * min(4.0, process.snowTransportMagnitude * 2.4));
                transportRidge *= (1.0 - exp(-process.snowTransportMagnitude * 4.0))
                                  * step(0.0001, transportLength)
                                  * 0.0085;

                float drainageLength = length(drainageDirection);
                float2 meltDirection = drainageDirection / max(0.0001, drainageLength);
                float2 meltSide = float2(-meltDirection.y, meltDirection.x);
                float meltChannel = pow(
                    0.5 + 0.5 * sin(dot(canonicalXZ, meltSide) * 2.7),
                    7.0);
                float meltPulse = 0.72
                                  + 0.28 * sin(
                                      dot(canonicalXZ, meltDirection) * 1.2
                                      - _Time.y * 2.1);
                float melt = -meltChannel
                             * meltPulse
                             * (1.0 - exp(-process.snowMeltRate * 6.0))
                             * step(0.35, drainageLength)
                             * 0.0075;
                return impact + transportRidge + melt;
            }

            float SteppeSnowRelief(
                float2 canonicalXZ,
                float snowMm,
                SteppeNaturalHydrologyProcessSample process,
                float2 drainageDirection)
            {
                float powder = SteppeWindValueNoise(canonicalXZ / 0.34h + 12.7h) - 0.5h;
                float fine = SteppeWindValueNoise(canonicalXZ / 0.095h + 63.4h) - 0.5h;
                float packed = SteppeWindValueNoise(canonicalXZ / 1.8h + 28.1h);
                float depthResponse = smoothstep(0.12h, 18.0h, snowMm);
                return (powder * 0.022h + fine * 0.005h)
                       * depthResponse
                       * lerp(1.0h, 0.34h, packed)
                       + SteppeSnowProcessRelief(
                           canonicalXZ,
                           process,
                           drainageDirection);
            }

            half3 SteppeSnowNormal(
                float2 canonicalXZ,
                half3 terrainNormal,
                float snowMm,
                half detailFade,
                SteppeNaturalHydrologyProcessSample process,
                float2 drainageDirection)
            {
                const float epsilon = 0.06;
                float left = SteppeSnowRelief(
                    canonicalXZ - float2(epsilon, 0.0),
                    snowMm,
                    process,
                    drainageDirection);
                float right = SteppeSnowRelief(
                    canonicalXZ + float2(epsilon, 0.0),
                    snowMm,
                    process,
                    drainageDirection);
                float down = SteppeSnowRelief(
                    canonicalXZ - float2(0.0, epsilon),
                    snowMm,
                    process,
                    drainageDirection);
                float up = SteppeSnowRelief(
                    canonicalXZ + float2(0.0, epsilon),
                    snowMm,
                    process,
                    drainageDirection);
                return normalize(terrainNormal + half3(
                    -(right - left) / (epsilon * 2.0) * detailFade,
                    0.0,
                    -(up - down) / (epsilon * 2.0) * detailFade));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float snowMm = SampleSteppeNaturalSnow(input.finiteCanonicalXZ);
                SteppeNaturalHydrologyProcessSample process =
                    SampleSteppeNaturalHydrologyProcess(input.finiteCanonicalXZ);
                SteppeNaturalHydrologyFieldSample hydrology =
                    SampleSteppeNaturalHydrologyFieldLevel(
                        input.finiteCanonicalXZ,
                        0.0);
                half slopeCoverage = smoothstep(0.34h, 0.76h, normalize(input.normalWS).y);
                half coverage = smoothstep(0.12h, 1.2h, snowMm) * slopeCoverage;
                clip(coverage - 0.015h);

                half detailFade = 1.0h - smoothstep(
                    2.0h,
                    12.0h,
                    distance(input.positionWS.xz, _WorldSpaceCameraPos.xz));
                half3 hydrologyNormal = SteppeSnowHydrologyNormal(
                    input.positionWS,
                    normalize(input.normalWS),
                    input.finiteCanonicalXZ,
                    snowMm);
                half3 normal = SteppeSnowNormal(
                    input.detailCanonicalXZ,
                    hydrologyNormal,
                    snowMm,
                    detailFade,
                    process,
                    hydrology.drainageDirection);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half diffuse = saturate(dot(normal, mainLight.direction));
                half cloudLightAdjustment = SteppeCloudLightAdjustment(input.positionWS);
                half3 direct = mainLight.color
                               * cloudLightAdjustment
                               * diffuse
                               * mainLight.shadowAttenuation;
                half3 ambient = SampleSH(normal) * SteppeCloudAmbientAdjustment(input.positionWS);
                half density = smoothstep(3.0h, 90.0h, snowMm);
                half windPacking = SteppeWindValueNoise(
                    input.detailCanonicalXZ / 2.7h + 18.3h);
                half3 snowColor = lerp(
                    _FreshSnowColor.rgb,
                    _DenseSnowColor.rgb,
                    density * lerp(0.72h, 1.0h, windPacking));
                half granularTone = lerp(
                    0.93h,
                    1.045h,
                    SteppeWindValueNoise(input.detailCanonicalXZ / 0.23h + 7.9h));
                snowColor *= lerp(1.0h, granularTone, detailFade);
                half3 color = snowColor * (ambient + direct);

                half3 viewDirection = SafeNormalize(_WorldSpaceCameraPos - input.positionWS);
                half3 halfDirection = SafeNormalize(mainLight.direction + viewDirection);
                half specular = pow(saturate(dot(normal, halfDirection)), 86.0h)
                                * lerp(0.12h, 0.30h, density)
                                * mainLight.shadowAttenuation;
                half crystalNoise = SteppeWindValueNoise(
                    input.detailCanonicalXZ / 0.13h + 91.4h);
                half sparkle = smoothstep(0.985h, 1.0h, crystalNoise)
                               * pow(saturate(dot(normal, halfDirection)), 180.0h)
                               * 0.55h
                               * detailFade;
                color += mainLight.color
                         * cloudLightAdjustment
                         * (specular + sparkle);
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
