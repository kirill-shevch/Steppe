Shader "Steppe/Hydrological Vapor Carriers"
{
    Properties
    {
        _BaseColor("Fixed Carrier Color", Color) = (0.85, 0.9, 0.9, 0.4)
        _CarrierKind("Carrier Kind", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent+14"
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
                float _CarrierKind;
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
                float2 p = input.uv * 2.0 - 1.0;
                half shape;
                if (_CarrierKind < 0.5)
                {
                    // A six-ray crystal: discrete solid-to-vapour release from snow.
                    float2 diagonalA = normalize(float2(0.866, 0.5));
                    float2 diagonalB = normalize(float2(0.866, -0.5));
                    float ray = min(
                        abs(p.x),
                        min(abs(dot(p, diagonalA)), abs(dot(p, diagonalB))));
                    half arms = 1.0h - smoothstep(0.055h, 0.17h, ray);
                    half radius = 1.0h - smoothstep(0.36h, 0.96h, length(p));
                    half core = 1.0h - smoothstep(0.12h, 0.34h, length(p));
                    shape = saturate(arms * radius + core);
                }
                else if (_CarrierKind < 1.5)
                {
                    // Wide, low vapor ribbons are never used by the other carriers.
                    float waveCenter = sin(
                        p.x * 5.2
                        + input.positionWS.x * 0.08
                        + input.positionWS.z * 0.05
                        + _Time.y * 0.7) * 0.18;
                    half ribbon = 1.0h - smoothstep(0.16h, 0.72h, abs(p.y - waveCenter));
                    half ends = 1.0h - smoothstep(0.52h, 1.0h, abs(p.x));
                    shape = ribbon * ends * 0.72h;
                }
                else if (_CarrierKind < 2.5)
                {
                    // A narrow sinuous filament starts at canopy height.
                    float center = sin(
                        p.y * 5.8
                        + input.positionWS.x * 0.37
                        + _Time.y * 1.15) * 0.17;
                    half thread = 1.0h - smoothstep(0.055h, 0.19h, abs(p.x - center));
                    half taper = smoothstep(0.0h, 0.24h, input.uv.y)
                                 * smoothstep(0.0h, 0.34h, 1.0h - input.uv.y);
                    shape = thread * taper;
                }
                else
                {
                    // A short paired pore-water bead. The particle travels only
                    // downward and vanishes at the soil surface, unlike rain streaks.
                    half center = 1.0h - smoothstep(0.10h, 0.28h, abs(p.x));
                    half upperBead = 1.0h - smoothstep(
                        0.18h,
                        0.38h,
                        length((p - float2(0.0, 0.34)) * float2(1.0, 1.45)));
                    half lowerBead = 1.0h - smoothstep(
                        0.14h,
                        0.31h,
                        length((p - float2(0.0, -0.31)) * float2(1.0, 1.55)));
                    half neck = center
                              * (1.0h - smoothstep(0.18h, 0.74h, abs(p.y)))
                              * 0.52h;
                    shape = saturate(upperBead + lowerBead + neck);
                }

                half alpha = input.color.a * shape;
                clip(alpha - 0.003h);
                half3 color = MixFog(input.color.rgb, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
