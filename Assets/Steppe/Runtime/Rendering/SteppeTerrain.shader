Shader "Steppe/Terrain Surface"
{
    Properties
    {
        _BaseColor("Tint", Color) = (1, 1, 1, 1)
        _Smoothness("Smoothness", Range(0, 1)) = 0.05
        _FarVegetationDetail("Far Vegetation Detail", Range(0, 0.3)) = 0.12
        _FarWindGustContrast("Far Wind Gust Contrast", Range(0, 0.2)) = 0.075
        _WetDarkening("Wet Soil Darkening", Range(0, 0.75)) = 0.46
        _WetSmoothness("Wet Soil Smoothness", Range(0, 1)) = 0.72
        _CrustLightening("Dry Crust Lightening", Range(0, 0.25)) = 0.08
        _SnowColor("Snow Color", Color) = (0.82, 0.88, 0.92, 1)
        _SnowSmoothness("Snow Smoothness", Range(0, 1)) = 0.28
        _CompactedSnowColor("Compacted Snow Color", Color) = (0.66, 0.75, 0.82, 1)
        _CrackStrength("Dry Crack Strength", Range(0, 1)) = 0.42
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
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

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Smoothness;
                half _FarVegetationDetail;
                half _FarWindGustContrast;
                half _WetDarkening;
                half _WetSmoothness;
                half _CrustLightening;
                half4 _SnowColor;
                half _SnowSmoothness;
                half4 _CompactedSnowColor;
                half _CrackStrength;
            CBUFFER_END

            float4 _SteppeWorldOriginXZ;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half4 color : COLOR;
                half fogFactor : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs undisplaced = GetVertexPositionInputs(input.positionOS.xyz);
                float2 canonicalXZ = undisplaced.positionWS.xz + _SteppeWorldOriginXZ.xz;
                SteppeTrackFieldSample track = SampleSteppeTrackFieldLevel(canonicalXZ, 0.0);
                float3 displacedPosition = input.positionOS.xyz;
                displacedPosition.y -= track.soilRut * _SteppeTrackRutDepth;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(displacedPosition);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.color = input.color * _BaseColor;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half diffuse = saturate(dot(normal, mainLight.direction));
                half3 ambient = SampleSH(normal);
                half3 lighting = ambient + mainLight.color * diffuse * mainLight.shadowAttenuation;
                float2 canonicalXZ = input.positionWS.xz + _SteppeWorldOriginXZ.xz;
                half broadVariation = SteppeWindValueNoise(canonicalXZ / 38.0) - 0.5h;
                half tuftVariation = SteppeWindValueNoise(canonicalXZ / 6.5) - 0.5h;
                half vegetationSignal = saturate(
                    0.42h + (input.color.g - input.color.r) * 2.8h);
                SteppeEcologyFieldSample ecology = SampleSteppeEcologyField(canonicalXZ);
                SteppeTrackFieldSample track = SampleSteppeTrackField(canonicalXZ);
                SteppeWindFieldSample wind = SampleSteppeWindField(
                    canonicalXZ,
                    vegetationSignal,
                    0.0);
                half fieldDetail = 1.0h
                                   + (broadVariation * 0.72h + tuftVariation * 0.28h)
                                   * _FarVegetationDetail
                                   * vegetationSignal
                                   + (0.5h - wind.broad)
                                   * _FarWindGustContrast
                                   * vegetationSignal;
                half soilSignal = saturate(1.0h - vegetationSignal * 0.86h);
                half wetness = max(
                                   smoothstep(0.005h, 0.12h, ecology.surfaceWater),
                                   track.wetPrint * 0.82h)
                               * lerp(0.38h, 1.0h, soilSignal);
                half crust = ecology.surfaceCrust
                             * soilSignal
                             * (1.0h - wetness);
                half3 albedo = input.color.rgb * fieldDetail;
                half3 curedTint = half3(1.07h, 0.99h, 0.82h);
                half3 livingTint = half3(0.88h, 1.12h, 0.76h);
                half phenologyWeight = vegetationSignal * ecology.biomass;
                albedo *= lerp(
                    1.0h.xxx,
                    lerp(curedTint, livingTint, ecology.greenFraction),
                    phenologyWeight * 0.58h);
                albedo *= 1.0h - wetness * _WetDarkening;
                albedo = lerp(albedo, albedo * half3(0.86h, 0.93h, 1.0h), wetness * 0.24h);
                albedo *= 1.0h + crust * _CrustLightening;
                // A warped cellular lattice turns dry cohesive crust into a
                // readable crack network without storing another state channel.
                float2 crackWarp = canonicalXZ
                                   + float2(
                                       SteppeWindValueNoise(canonicalXZ / 8.7h),
                                       SteppeWindValueNoise(canonicalXZ / 7.9h + 23.4h))
                                   * 1.8h;
                float2 crackCell = abs(frac(crackWarp / 2.6h) - 0.5h);
                half crackLine = smoothstep(0.455h, 0.495h, max(crackCell.x, crackCell.y));
                half cracks = crackLine * crust * _CrackStrength;
                albedo *= 1.0h - cracks * 0.48h;
                half looseGrain = (SteppeWindValueNoise(canonicalXZ / 0.72h) - 0.5h)
                                  * soilSignal
                                  * (1.0h - crust)
                                  * (1.0h - wetness)
                                  * 0.09h;
                albedo *= 1.0h + looseGrain;
                albedo *= 1.0h - track.soilRut * 0.12h;
                half snowCoverage = smoothstep(0.015h, 0.22h, ecology.snowWater)
                                    * smoothstep(0.42h, 0.82h, normal.y);
                half frost = ecology.frozenFraction
                             * (1.0h - snowCoverage)
                             * soilSignal
                             * 0.18h;
                albedo = lerp(albedo, albedo * half3(0.86h, 0.94h, 1.04h), frost);
                half compactedSnow = snowCoverage
                                     * saturate(max(ecology.snowCompaction, track.snowCompression));
                half3 snowTint = lerp(_SnowColor.rgb, _CompactedSnowColor.rgb, compactedSnow * 0.72h);
                albedo = lerp(albedo, snowTint, snowCoverage);
                half3 color = albedo * lighting;

                half snowSmoothness = lerp(_SnowSmoothness, 0.58h, compactedSnow);
                half smoothness = saturate(
                    lerp(_Smoothness + wetness * _WetSmoothness, snowSmoothness, snowCoverage));
                half3 viewDirection = SafeNormalize(_WorldSpaceCameraPos - input.positionWS);
                half3 halfDirection = SafeNormalize(mainLight.direction + viewDirection);
                half specularPower = lerp(12.0h, 150.0h, smoothness);
                half specular = pow(saturate(dot(normal, halfDirection)), specularPower)
                                * smoothness
                                * lerp(0.06h, 0.30h, wetness)
                                * mainLight.shadowAttenuation;
                color += mainLight.color * specular;
                half sparkleNoise = SteppeWindValueNoise(canonicalXZ / 0.85h);
                half sparkle = smoothstep(0.965h, 1.0h, sparkleNoise)
                               * snowCoverage
                               * saturate(dot(normal, mainLight.direction))
                               * 0.24h;
                color += mainLight.color * sparkle;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
