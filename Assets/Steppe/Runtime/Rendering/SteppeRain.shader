Shader "Steppe/Rain Streak"
{
    Properties
    {
        _BaseColor("Rain Color", Color) = (0.68, 0.78, 0.9, 0.72)
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent+20"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "RainStreak"
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
                half across = 1.0h - smoothstep(0.18h, 1.0h, abs(input.uv.x - 0.5h) * 2.0h);
                half head = smoothstep(0.0h, 0.16h, input.uv.y);
                half tail = 1.0h - smoothstep(0.78h, 1.0h, input.uv.y);
                half alpha = input.color.a * across * head * tail;
                half3 color = MixFog(input.color.rgb, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
