Shader "Steppe/Dust Wisp"
{
    Properties
    {
        _BaseColor("Dust Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent+10"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "DustWisp"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            float _SteppeDaylight;

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                half fogFactor : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.color = input.color * _BaseColor;
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half2 centered = (input.uv - 0.5h) * 2.0h;
                half body = 1.0h - smoothstep(0.28h, 1.0h, dot(centered, centered));
                half brokenEdge = saturate(0.82h + 0.18h * sin(
                    centered.x * 17.0h + centered.y * 11.0h));
                // Dust used to be effectively self-lit, so its warm billboards stayed
                // bright after sunset and looked like moving spots on the ground.
                // Keep a faint moonlit trace, but let daylight control both the tint
                // and opacity of the wind-driven wisps.
                half daylightVisibility = smoothstep(0.04h, 0.52h, _SteppeDaylight);
                half illumination = lerp(0.08h, 1.0h, daylightVisibility);
                half opacity = lerp(0.10h, 1.0h, daylightVisibility);
                half alpha = input.color.a * body * brokenEdge * opacity;
                half3 color = MixFog(input.color.rgb * illumination, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
