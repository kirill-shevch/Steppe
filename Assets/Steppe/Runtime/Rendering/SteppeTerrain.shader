Shader "Steppe/Terrain Surface"
{
    Properties
    {
        _BaseColor("Tint", Color) = (1, 1, 1, 1)
        _Smoothness("Smoothness", Range(0, 1)) = 0.05
        _FarVegetationDetail("Far Vegetation Detail", Range(0, 0.3)) = 0.12
        _FarWindGustContrast("Far Wind Gust Contrast", Range(0, 0.2)) = 0.075
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

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Smoothness;
                half _FarVegetationDetail;
                half _FarWindGustContrast;
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
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
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
                float2 canonicalXZ = input.positionWS.xz + _SteppeWorldOriginXZ.xy;
                half broadVariation = SteppeWindValueNoise(canonicalXZ / 38.0) - 0.5h;
                half tuftVariation = SteppeWindValueNoise(canonicalXZ / 6.5) - 0.5h;
                half vegetationSignal = saturate(
                    0.42h + (input.color.g - input.color.r) * 2.8h);
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
                half3 color = input.color.rgb * fieldDetail * lighting;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
