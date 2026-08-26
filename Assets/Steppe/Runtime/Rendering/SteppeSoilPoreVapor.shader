Shader "Steppe/Soil Pore Vapor"
{
    Properties
    {
        // Particle alpha is the only opacity carrier. Keeping the material alpha
        // neutral avoids accidentally suppressing the already sparse state cue.
        _BaseColor("Neutral Vapor", Color) = (0.84, 0.87, 0.84, 1.0)
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent+12"
            "RenderPipeline"="UniversalPipeline"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

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
                float3 positionWS : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.color = input.color * _BaseColor;
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 centered = input.uv * 2.0 - 1.0;
                float radial = length(float2(centered.x * 1.55, centered.y));
                half body = 1.0h - smoothstep(0.28h, 1.0h, radial);
                half ends = smoothstep(0.0h, 0.18h, input.uv.y)
                            * smoothstep(0.0h, 0.18h, 1.0h - input.uv.y);
                half flutter = 0.78h + 0.22h * sin(
                    input.positionWS.x * 2.7h
                    + input.positionWS.z * 1.9h
                    + _Time.y * 1.3h);
                half alpha = input.color.a * body * ends * flutter;
                clip(alpha - 0.004h);
                half3 color = MixFog(input.color.rgb, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
