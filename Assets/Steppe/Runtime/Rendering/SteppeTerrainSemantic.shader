Shader "Steppe/Terrain Surface"
{
    Properties
    {
        _SoilSmoothness("Soil Smoothness", Range(0, 1)) = 0.055
        _ReliefStrength("Semantic Relief Strength", Range(0, 2)) = 1.0
        _DiagnosticStrength("Diagnostic Compatibility", Range(0, 1)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

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
            #include "Assets/Steppe/Runtime/Rendering/SteppeCloudShadow.hlsl"
            #include "Assets/Steppe/Runtime/Rendering/SteppeWindField.hlsl"
            #include "Assets/Steppe/Runtime/Rendering/SteppeTrackField.hlsl"
            #include "Assets/Steppe/Runtime/Rendering/SteppeSimulationField.hlsl"
            #include "Assets/Steppe/Runtime/Rendering/SteppeNaturalVisualField.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _SoilSmoothness;
                half _ReliefStrength;
                half _DiagnosticStrength;
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
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            float SteppeDepressionBowlHeight(
                float2 canonicalXZ,
                float depressionStorageMm)
            {
                float2 cellIndex = SteppeNaturalVisualCellIndex(canonicalXZ);
                float2 cellCenter = SteppeNaturalVisualCellCenter(cellIndex);
                float hash = frac(sin(dot(cellIndex, float2(71.71, 193.13))) * 43758.5453);
                float2 basinOffset = float2(hash - 0.5, frac(hash * 7.17) - 0.5)
                                     * _SteppeNaturalVisualGrid.z
                                     * 0.12;
                float2 fromCenter = canonicalXZ - (cellCenter + basinOffset);
                float radius = _SteppeNaturalVisualGrid.z * 0.43;
                float normalizedRadius = length(fromCenter) / max(0.01, radius);
                float bowl = 1.0 - smoothstep(0.12, 1.0, normalizedRadius);
                bowl *= bowl;
                return -min(0.22, max(0.0, depressionStorageMm) * 0.001) * bowl;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs undisplaced = GetVertexPositionInputs(input.positionOS.xyz);
                float2 canonicalXZ = undisplaced.positionWS.xz + _SteppeWorldOriginXZ.xz;
                float2 finiteCanonicalXZ = undisplaced.positionWS.xz
                                           + _SteppeFiniteWorldOriginXZ.xz;
                SteppeTrackFieldSample track = SampleSteppeTrackFieldLevel(canonicalXZ, 0.0);
                SteppeNaturalHydrologyFieldSample hydrology =
                    SampleSteppeNaturalHydrologyFieldLevel(finiteCanonicalXZ, 0.0);
                float3 displacedPosition = input.positionOS.xyz;
                displacedPosition.y -= track.soilRut * _SteppeTrackRutDepth;
                displacedPosition.y += SteppeDepressionBowlHeight(
                    finiteCanonicalXZ,
                    hydrology.depressionStorageMm);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(displacedPosition);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            float SteppePointSegmentDistance(
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

            float SteppeDrainageSegmentHeight(
                float2 canonicalXZ,
                float2 sourceCell,
                float4 drainage)
            {
                float directionLength = length(drainage.xy);
                if (directionLength < 0.35)
                {
                    return 0.0;
                }

                float2 stepDirection = sign(drainage.xy);
                float2 targetCell = sourceCell + stepDirection;
                if (targetCell.x < 0.0 || targetCell.y < 0.0
                    || targetCell.x >= _SteppeNaturalVisualGrid.x
                    || targetCell.y >= _SteppeNaturalVisualGrid.y)
                {
                    return 0.0;
                }

                float2 start = SteppeNaturalVisualCellCenter(sourceCell);
                float2 end = SteppeNaturalVisualCellCenter(targetCell);
                float alongMetres;
                float distanceToAxis = SteppePointSegmentDistance(
                    canonicalXZ,
                    start,
                    end,
                    alongMetres);
                float channelWidth = 0.72
                                     + drainage.z * 0.46
                                     + drainage.w * 0.34;
                float profile = 1.0 - smoothstep(
                    channelWidth,
                    channelWidth + 0.75,
                    distanceToAxis);
                profile *= profile;
                float channelDepth = 0.022
                                     + drainage.z * 0.0095
                                     + drainage.w * 0.011;

                // A saw-tooth scour train rises gradually and drops sharply in the
                // downstream direction. It communicates direction through landform,
                // never through a UI arrow or a second colour code.
                float rifflePhase = frac(alongMetres / 10.5);
                float directionalScour = lerp(0.78, 1.12, rifflePhase);
                return -channelDepth * profile * directionalScour;
            }

            float SteppeDrainageReliefHeight(float2 canonicalXZ)
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

                        float4 drainage = SampleSteppeNaturalDrainageCellLevel(
                            sourceCell,
                            0.0);
                        relief = min(
                            relief,
                            SteppeDrainageSegmentHeight(
                                canonicalXZ,
                                sourceCell,
                                drainage));
                    }
                }

                return relief;
            }

            half3 SteppeHydrologyNormal(
                float3 positionWS,
                half3 terrainNormal,
                float2 finiteCanonicalXZ,
                float snowWaterEquivalentMm)
            {
                if (snowWaterEquivalentMm > 0.12)
                {
                    return terrainNormal;
                }

                float drainageHeight = SteppeDrainageReliefHeight(finiteCanonicalXZ);
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

            float2 SteppeSoilHash22(float2 cell)
            {
                float2 projected = float2(
                    dot(cell, float2(127.1, 311.7)),
                    dot(cell, float2(269.5, 183.3)));
                return frac(sin(projected) * 43758.5453);
            }

            float SteppeCrackMask(float2 canonicalXZ, float clayFraction)
            {
                float cellSize = lerp(1.9, 0.72, saturate(clayFraction));
                float2 position = canonicalXZ / cellSize;
                float2 cell = floor(position);
                float2 local = frac(position);
                float nearest = 8.0;
                float secondNearest = 8.0;
                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 neighbour = float2(x, y);
                        float2 feature = neighbour
                                         + SteppeSoilHash22(cell + neighbour)
                                         - local;
                        float distanceSquared = dot(feature, feature);
                        if (distanceSquared < nearest)
                        {
                            secondNearest = nearest;
                            nearest = distanceSquared;
                        }
                        else if (distanceSquared < secondNearest)
                        {
                            secondNearest = distanceSquared;
                        }
                    }
                }

                float edgeDistance = sqrt(secondNearest) - sqrt(nearest);
                float seam = 1.0 - smoothstep(0.006, 0.026, edgeDistance);
                return seam * smoothstep(0.16, 0.58, clayFraction);
            }

            float SteppeCrustPlate(float2 canonicalXZ, float crustFraction)
            {
                float plateNoise = SteppeWindValueNoise(canonicalXZ / 0.92 + 14.8);
                float islands = smoothstep(0.64, 0.86, plateNoise);
                return islands * saturate(crustFraction);
            }

            float SteppePoreMask(float2 canonicalXZ, float porosity)
            {
                float pores = SteppeWindValueNoise(canonicalXZ / 0.145 + 71.3);
                float poreDensity = saturate((porosity - 0.25) / 0.40);
                return smoothstep(lerp(0.992, 0.925, poreDensity), 1.0, pores);
            }

            float SteppeSoilReliefHeight(
                float2 canonicalXZ,
                SteppeNaturalVisualFieldSample field)
            {
                float coarseNoise = SteppeWindValueNoise(canonicalXZ / 0.24 + 9.2);
                float coarseGrains = pow(saturate(coarseNoise), 4.0)
                                     * saturate(field.sandFraction)
                                     * 0.030;
                float finePowder = (SteppeWindValueNoise(canonicalXZ / 0.075 + 51.7) - 0.5)
                                   * saturate(field.siltFraction)
                                   * 0.010;
                float rippleWarp = SteppeWindValueNoise(canonicalXZ / 5.1 + 33.8) - 0.5;
                float rippleAxis = canonicalXZ.x * 0.59 + canonicalXZ.y * 0.27
                                   + rippleWarp * 2.9;
                float ripplePatch = smoothstep(
                    0.54,
                    0.79,
                    SteppeWindValueNoise(canonicalXZ / 3.6 + 82.1));
                float mobileBed = sin(rippleAxis * 3.1)
                                  * saturate(field.looseSedimentKg / 10.0)
                                  * ripplePatch
                                  * 0.017;
                float crackCut = SteppeCrackMask(canonicalXZ, field.clayFraction) * 0.009;
                float raisedCrust = SteppeCrustPlate(canonicalXZ, field.surfaceCrustFraction)
                                    * 0.021;
                float compression = lerp(1.0, 0.18, saturate(field.soilCompaction));
                return (coarseGrains + finePowder + mobileBed + raisedCrust - crackCut)
                       * compression
                       * _ReliefStrength;
            }

            half3 SteppeSemanticSoilNormal(
                float2 canonicalXZ,
                half3 terrainNormal,
                SteppeNaturalVisualFieldSample field)
            {
                const float epsilon = 0.055;
                float left = SteppeSoilReliefHeight(canonicalXZ - float2(epsilon, 0.0), field);
                float right = SteppeSoilReliefHeight(canonicalXZ + float2(epsilon, 0.0), field);
                float down = SteppeSoilReliefHeight(canonicalXZ - float2(0.0, epsilon), field);
                float up = SteppeSoilReliefHeight(canonicalXZ + float2(0.0, epsilon), field);
                float slopeX = (right - left) / (epsilon * 2.0);
                float slopeZ = (up - down) / (epsilon * 2.0);
                return normalize(terrainNormal + half3(-slopeX, 0.0, -slopeZ));
            }

            half3 SteppeRootWaterAlbedo(float2 canonicalXZ, float rootWaterMm)
            {
                // RootWater is the sole simulation owner of soil base albedo.
                // The two noises only add sub-cell material detail; they do not
                // carry another state or process.
                half moisture = smoothstep(12.0h, 158.0h, rootWaterMm);
                const half3 dryLoam = half3(0.405h, 0.275h, 0.145h);
                const half3 moistLoam = half3(0.155h, 0.102h, 0.054h);
                half broadMottle = SteppeWindValueNoise(canonicalXZ / 5.2h) - 0.5h;
                half fineMottle = SteppeWindValueNoise(canonicalXZ / 0.58h) - 0.5h;
                half detail = 1.0h + broadMottle * 0.105h + fineMottle * 0.035h;
                return lerp(dryLoam, moistLoam, moisture) * detail;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 canonicalXZ = input.positionWS.xz + _SteppeWorldOriginXZ.xz;
                float2 finiteCanonicalXZ = input.positionWS.xz
                                           + _SteppeFiniteWorldOriginXZ.xz;
                SteppeNaturalVisualFieldSample field =
                    SampleSteppeNaturalVisualField(finiteCanonicalXZ);
                half3 hydrologyNormal = SteppeHydrologyNormal(
                    input.positionWS,
                    normalize(input.normalWS),
                    finiteCanonicalXZ,
                    field.snowWaterEquivalentMm);
                half3 normal = SteppeSemanticSoilNormal(
                    canonicalXZ,
                    hydrologyNormal,
                    field);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half diffuse = saturate(dot(normal, mainLight.direction));
                half3 ambient = SampleSH(normal);
                half cloudLightAdjustment = SteppeCloudLightAdjustment(input.positionWS);
                half3 cloudAdjustedMainLight = mainLight.color * cloudLightAdjustment;
                half ambientAdjustment = SteppeCloudAmbientAdjustment(input.positionWS);
                half3 lighting = ambient * ambientAdjustment
                                 + cloudAdjustedMainLight
                                 * diffuse
                                 * mainLight.shadowAttenuation;

                half3 albedo = SteppeRootWaterAlbedo(canonicalXZ, field.rootWaterMm);

                // These are geometry shadows, not pigment changes. Clay owns the
                // connected shrink seams; porosity owns isolated cavities; crust
                // owns raised plate edges. Their spatial grammars stay distinct.
                half crackOcclusion = SteppeCrackMask(canonicalXZ, field.clayFraction);
                half poreOcclusion = SteppePoreMask(canonicalXZ, field.porosity);
                half crustPlate = SteppeCrustPlate(canonicalXZ, field.surfaceCrustFraction);
                half crustEdge = saturate(length(half2(ddx(crustPlate), ddy(crustPlate))) * 1.7h);
                half geometricOcclusion = saturate(
                    1.0h - crackOcclusion * 0.12h
                         - poreOcclusion * 0.18h
                         - crustEdge * 0.12h);
                half3 color = albedo * lighting * geometricOcclusion;

                half3 viewDirection = SafeNormalize(_WorldSpaceCameraPos - input.positionWS);
                half3 halfDirection = SafeNormalize(mainLight.direction + viewDirection);
                half baseSpecular = pow(saturate(dot(normal, halfDirection)), 72.0h)
                                    * _SoilSmoothness
                                    * 0.08h;
                color += cloudAdjustedMainLight
                         * baseSpecular
                         * mainLight.shadowAttenuation;

                // MineralContent owns sparse, sharp inclusions. No other state
                // changes their population or luminance.
                half mineralNoise = SteppeWindValueNoise(canonicalXZ / 0.19h + 104.6h);
                half inclusion = smoothstep(
                    lerp(0.997h, 0.948h, saturate(field.mineralContent)),
                    1.0h,
                    mineralNoise);
                half mineralGlint = pow(saturate(dot(normal, halfDirection)), 180.0h)
                                    * inclusion
                                    * saturate(field.mineralContent)
                                    * mainLight.shadowAttenuation;
                color += cloudAdjustedMainLight * mineralGlint * 0.72h;

                half4 diagnostic = SampleSteppeDiagnosticField(finiteCanonicalXZ);
                color = lerp(color, diagnostic.rgb, diagnostic.a * _DiagnosticStrength);
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
