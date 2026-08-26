Shader "Steppe/Ground Semantic Details"
{
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "RenderPipeline"="UniversalPipeline"
            "Queue"="AlphaTest+30"
        }
        Cull Off
        ZWrite On

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
            #include "Assets/Steppe/Runtime/Rendering/SteppeNaturalVisualField.hlsl"
            #include "Assets/Steppe/Runtime/Rendering/SteppeNaturalProcessField.hlsl"
            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
            #include "UnityIndirect.cginc"

            struct GroundDetailInstance
            {
                float4 positionRotation;
                float4 normalRandom;
            };

            StructuredBuffer<GroundDetailInstance> _SteppeGroundDetailInstances;

            CBUFFER_START(UnityPerMaterial)
                half4 _DetailColor;
                float _DetailKind;
                float _DetailScale;
                float _DetailFullDensityRadius;
                float _DetailDrawRadius;
            CBUFFER_END

            float4 _SteppeGroundDetailCellOrigin;
            float4 _SteppeFiniteWorldOriginXZ;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half visibility : TEXCOORD2;
                half randomThreshold : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                half stateResponse : TEXCOORD5;
                float2 finiteCanonicalXZ : TEXCOORD6;
            };

            half DetailDensity(
                SteppeNaturalGroundDetailFieldSample field,
                SteppeNaturalHydrologyFieldSample hydrology,
                float kind)
            {
                if (kind < 0.5)
                {
                    return smoothstep(0.025h, 0.72h, field.frozenSoilFraction) * 0.72h;
                }
                if (kind < 1.5)
                {
                    return smoothstep(10.0h, 105.0h, field.litterBiomassGrams) * 0.82h;
                }
                if (kind < 2.5)
                {
                    return smoothstep(0.10h, 0.72h, field.seedBankFraction) * 0.62h;
                }
                if (kind < 3.5)
                {
                    return smoothstep(450.0h, 2100.0h, field.soilOrganicMatterGrams) * 0.38h;
                }
                if (kind < 4.5)
                {
                    return smoothstep(0.18h, 1.10h, field.availableNitrogenGrams) * 0.64h;
                }
                if (kind < 5.5)
                {
                    return smoothstep(5.0h, 24.0h, field.organicNitrogenGrams) * 0.56h;
                }
                if (kind < 6.5)
                {
                    return smoothstep(0.035h, 0.82h, field.faultInfluence) * 0.12h;
                }
                if (kind < 7.5)
                {
                    return smoothstep(0.18h, 0.95h, field.rockHardness) * 0.115h;
                }
                if (kind < 8.5)
                {
                    half exposed = 1.0h - smoothstep(
                        0.6h,
                        5.0h,
                        field.snowWaterEquivalentMm);
                    return smoothstep(2.0h, 92.0h, field.groundwaterMm)
                           * 0.22h
                           * exposed;
                }
                if (kind < 9.5)
                {
                    return smoothstep(0.015h, 0.90h, field.burnScarFraction) * 0.52h;
                }

                half exposed = 1.0h - smoothstep(
                    12.0h,
                    60.0h,
                    field.snowWaterEquivalentMm);
                half routed = step(0.35h, length(hydrology.drainageDirection));
                half network = saturate((hydrology.contributingLog2 - 0.75h) / 7.0h);
                return routed * lerp(0.024h, 0.058h, network) * exposed;
            }

            Varyings Vert(Attributes input)
            {
                InitIndirectDrawArgs(0);
                GroundDetailInstance instance =
                    _SteppeGroundDetailInstances[GetIndirectInstanceID(input.instanceID)];
                float2 localRoot = _SteppeGroundDetailCellOrigin.xz
                                   + instance.positionRotation.xz;
                float2 finiteCanonicalRoot = localRoot + _SteppeFiniteWorldOriginXZ.xz;
                SteppeNaturalGroundDetailFieldSample field =
                    SampleSteppeNaturalGroundDetailFieldLevel(finiteCanonicalRoot, 0.0);
                SteppeNaturalHydrologyFieldSample hydrology =
                    SampleSteppeNaturalHydrologyFieldLevel(finiteCanonicalRoot, 0.0);
                half density = DetailDensity(field, hydrology, _DetailKind);

                float3 groundNormal = normalize(instance.normalRandom.xyz);
                float3 referenceAxis = abs(groundNormal.y) < 0.96
                    ? float3(0.0, 1.0, 0.0)
                    : float3(1.0, 0.0, 0.0);
                float3 tangent = normalize(cross(referenceAxis, groundNormal));
                float3 bitangent = normalize(cross(groundNormal, tangent));
                float sine = sin(instance.positionRotation.w);
                float cosine = cos(instance.positionRotation.w);
                float3 rotatedTangent = tangent * cosine + bitangent * sine;
                float3 rotatedBitangent = -tangent * sine + bitangent * cosine;
                if (_DetailKind > 9.5h)
                {
                    float2 cellIndex = SteppeNaturalVisualCellIndex(finiteCanonicalRoot);
                    float4 cellDrainage = SampleSteppeNaturalDrainageCellLevel(cellIndex, 0.0);
                    float2 flowXZ = cellDrainage.xy;
                    float flowLength = max(0.0001, length(flowXZ));
                    flowXZ /= flowLength;
                    float2 sideXZ = float2(-flowXZ.y, flowXZ.x);
                    float2 cellCenter = SteppeNaturalVisualCellCenter(cellIndex);
                    float2 fromCenter = finiteCanonicalRoot - cellCenter;
                    float signedCross = dot(fromCenter, sideXZ);
                    float bankOffset = 1.6 + cellDrainage.z * 0.52 + cellDrainage.w * 0.34;
                    half bankProximity = 1.0h - smoothstep(
                        bankOffset + 0.8h,
                        bankOffset + 2.5h,
                        abs(signedCross));
                    density *= bankProximity * step(0.35h, flowLength);

                    float bankSide = signedCross < 0.0 ? -1.0 : 1.0;
                    float2 snappedCanonical = finiteCanonicalRoot
                                              + sideXZ * (bankSide * bankOffset - signedCross);
                    localRoot += snappedCanonical - finiteCanonicalRoot;
                    finiteCanonicalRoot = snappedCanonical;

                    float3 flowTangent = float3(flowXZ.x, 0.0, flowXZ.y);
                    flowTangent -= groundNormal * dot(flowTangent, groundNormal);
                    rotatedTangent = normalize(flowTangent);
                    rotatedBitangent = normalize(cross(groundNormal, rotatedTangent));
                }
                float randomScale = lerp(
                    0.72,
                    1.28,
                    frac(instance.normalRandom.w * 7.193 + _DetailKind * 0.271));
                float distanceToCamera = distance(localRoot, _WorldSpaceCameraPos.xz);
                half distanceVisibility = 1.0h - smoothstep(
                    _DetailFullDensityRadius,
                    _DetailDrawRadius,
                    distanceToCamera);
                half randomThreshold = frac(
                    instance.normalRandom.w * (3.117h + _DetailKind * 1.931h)
                    + _DetailKind * 0.173h);
                half visibility = density * distanceVisibility * field.valid;
                // A newly admitted object grows out of its physical carrier over a
                // narrow continuous band. Macro interpolation therefore never turns
                // a whole needle, clod or seed head on at a simulation boundary.
                half revealWidth = _DetailKind > 5.5h ? 0.026h : 0.085h;
                half reveal = smoothstep(
                    0.0h,
                    revealWidth,
                    visibility - randomThreshold);
                float scale = _DetailScale * randomScale * reveal;
                float3 localOffset = input.positionOS * scale;
                if (_DetailKind > 9.5h)
                {
                    localOffset.x = input.positionOS.x * _DetailScale * randomScale * reveal;
                    localOffset.y = input.positionOS.y
                                    * (0.08 + saturate(field.soilDepthM / 2.5) * 0.44)
                                    * reveal;
                    localOffset.z = input.positionOS.z * _DetailScale * randomScale * reveal;
                }
                float3 worldPosition = float3(
                    localRoot.x,
                    instance.positionRotation.y,
                    localRoot.y);
                worldPosition += rotatedTangent * localOffset.x
                                 + groundNormal * localOffset.y
                                 + rotatedBitangent * localOffset.z;

                Varyings output;
                output.positionWS = worldPosition;
                output.positionCS = TransformWorldToHClip(worldPosition);
                output.normalWS = normalize(
                    rotatedTangent * input.normalOS.x
                    + groundNormal * input.normalOS.y
                    + rotatedBitangent * input.normalOS.z);
                output.visibility = visibility;
                output.randomThreshold = randomThreshold;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.stateResponse = density;
                output.finiteCanonicalXZ = finiteCanonicalRoot;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                clip(input.visibility - input.randomThreshold);
                half3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                half diffuse = saturate(abs(dot(normalWS, mainLight.direction)));
                half cloudLightAdjustment = SteppeCloudLightAdjustment(input.positionWS);
                half3 indirect = SampleSH(normalWS) * 0.72h;
                half3 direct = mainLight.color
                               * (0.22h + diffuse * 0.78h)
                               * mainLight.distanceAttenuation
                               * mainLight.shadowAttenuation
                               * cloudLightAdjustment;
                half randomTone = lerp(
                    0.82h,
                    1.14h,
                    frac(input.positionWS.x * 0.173h + input.positionWS.z * 0.297h));
                half3 illumination = max(indirect + direct, half3(0.32h, 0.32h, 0.32h));
                half3 color = _DetailColor.rgb * randomTone * illumination;
                SteppeNaturalHydrologyProcessSample process =
                    SampleSteppeNaturalHydrologyProcess(input.finiteCanonicalXZ);

                // Ice owns a crystalline highlight, not a blue ground tint. The other
                // five carriers remain matte so their material languages cannot merge.
                if (_DetailKind < 0.5)
                {
                    half3 viewDirection = SafeNormalize(_WorldSpaceCameraPos - input.positionWS);
                    half3 halfVector = SafeNormalize(mainLight.direction + viewDirection);
                    half sparkle = pow(saturate(abs(dot(normalWS, halfVector))), 42.0h);
                    color += mainLight.color * sparkle * input.stateResponse * 0.75h;
                }
                else if (_DetailKind > 6.5h && _DetailKind < 7.5h)
                {
                    half3 viewDirection = SafeNormalize(_WorldSpaceCameraPos - input.positionWS);
                    half3 halfVector = SafeNormalize(mainLight.direction + viewDirection);
                    half mineralGlint = pow(saturate(abs(dot(normalWS, halfVector))), 34.0h);
                    color *= 1.22h;
                    color += mainLight.color * mineralGlint * 0.12h;
                }
                else if (_DetailKind > 7.5h && _DetailKind < 8.5h)
                {
                    half3 waterNormal = normalWS.y < 0.0h ? -normalWS : normalWS;
                    half3 viewDirection = SafeNormalize(_WorldSpaceCameraPos - input.positionWS);
                    half3 halfVector = SafeNormalize(mainLight.direction + viewDirection);
                    half glint = pow(saturate(dot(waterNormal, halfVector)), 96.0h);
                    half fresnel = pow(1.0h - saturate(dot(waterNormal, viewDirection)), 4.0h);
                    color = lerp(color, SampleSH(waterNormal), 0.24h + fresnel * 0.34h);
                    color += mainLight.color * glint * 0.74h;

                    // A narrow highlight band travels only along the measured
                    // groundwater vector. Direction owns motion; magnitude owns
                    // its speed and visibility. The seep state still owns geometry.
                    float flowMagnitude = process.groundwaterFlowMagnitude;
                    float2 flowDirection = process.groundwaterFlowVector
                                           / max(0.0001, length(process.groundwaterFlowVector));
                    half flowStrength = smoothstep(0.0002h, 0.08h, flowMagnitude);
                    float downstream = dot(input.finiteCanonicalXZ, flowDirection);
                    float travel = frac(
                        downstream * 0.22
                        - _Time.y * lerp(0.18, 1.15, flowStrength));
                    half movingBand = pow(saturate(1.0h - abs(travel - 0.5h) * 2.0h), 18.0h);
                    color += half3(0.26h, 0.48h, 0.52h)
                             * movingBand
                             * flowStrength
                             * process.valid;
                }
                else if (_DetailKind > 9.5h)
                {
                    // Percolation is shown nowhere except exposed soil profiles:
                    // repeated capillary beads move downward from root layer to
                    // groundwater. Flux rate owns pulse density and luminance.
                    half percolationStrength = smoothstep(
                        0.0005h,
                        0.08h,
                        process.percolationRate);
                    half poreLane = smoothstep(
                        0.58h,
                        0.94h,
                        0.5h + 0.5h * sin(
                            input.finiteCanonicalXZ.x * 4.7h
                            + input.finiteCanonicalXZ.y * 2.1h));
                    float downwardPhase = frac(
                        input.positionWS.y * 1.55
                        + _Time.y * lerp(0.18, 1.4, percolationStrength));
                    half descendingBead = pow(
                        saturate(1.0h - abs(downwardPhase - 0.5h) * 2.0h),
                        8.0h);
                    // Bright, narrow mineral-water glints remain confined to the
                    // vertical profile mesh. This makes downward movement readable
                    // without borrowing the soil surface colour channel.
                    color += half3(0.34h, 0.62h, 0.68h)
                             * poreLane
                             * descendingBead
                             * percolationStrength
                             * 1.75h
                             * process.valid;
                }

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
