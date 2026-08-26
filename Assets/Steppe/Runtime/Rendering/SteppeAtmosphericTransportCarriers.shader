Shader "Steppe/Atmospheric Transport Carriers"
{
    Properties
    {
        _BaseColor("Fixed Carrier Color", Color) = (0.88, 0.9, 0.88, 0.2)
        _CarrierKind("Carrier Kind", Float) = 0
        _FluxSignal("Signed Flux Signal", Range(-1, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent+13"
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
                float _FluxSignal;
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
                half fluxMagnitude = saturate(abs(_FluxSignal));
                float signedTravel = _Time.y * lerp(0.25, 1.45, fluxMagnitude)
                                     * (_FluxSignal >= 0.0 ? 1.0 : -1.0);
                if (_CarrierKind < 0.5)
                {
                    // Paired colorless lens blades: the outer pulse contracts for
                    // positive local temperature transport and expands for negative.
                    float lens = abs(p.x) + p.y * p.y * 0.34;
                    half blade = 1.0h - smoothstep(0.09h, 0.31h, lens);
                    half paired = 0.72h + 0.28h * sin(p.y * 8.0h + signedTravel * 3.0h);
                    shape = blade * paired * lerp(0.36h, 1.0h, fluxMagnitude);
                }
                else if (_CarrierKind < 1.5)
                {
                    // Moisture owns concentric beads, not haze opacity. Ring phase
                    // contracts/expands with signed net transport while the particle
                    // itself travels along the authoritative humidity vector.
                    float radius = length(float2(p.x * 1.2, p.y));
                    float ringPhase = frac(radius * 1.7 - signedTravel * 0.16);
                    half ring = pow(saturate(1.0h - abs(ringPhase - 0.5h) * 2.0h), 5.0h);
                    half body = 1.0h - smoothstep(0.34h, 0.98h, radius);
                    shape = body * lerp(0.28h, 1.0h, ring * fluxMagnitude);
                }
                else
                {
                    // Broken, expanding vapor rims bound holes eroded through the
                    // cloud layer. Size-over-lifetime owns the outward direction;
                    // this scalloped hollow geometry is not used by condensation.
                    float radius = length(p);
                    float angle = atan2(p.y, p.x);
                    float raggedRadius = 0.58
                                       + sin(angle * 7.0 + input.positionWS.x * 0.013) * 0.090
                                       + sin(angle * 11.0 + input.positionWS.z * 0.009) * 0.052;
                    half rim = 1.0h - smoothstep(
                        0.045h,
                        0.135h,
                        abs(radius - raggedRadius));
                    half broken = smoothstep(
                        -0.22h,
                        0.48h,
                        sin(angle * 5.0h + input.positionWS.x * 0.021h));
                    half outwardWisps = 1.0h - smoothstep(
                        raggedRadius,
                        1.0h,
                        radius);
                    outwardWisps *= smoothstep(raggedRadius, raggedRadius + 0.10h, radius);
                    shape = saturate(rim * (0.22h + broken * 0.78h)
                                     + outwardWisps * 0.18h)
                            * lerp(0.18h, 1.0h, fluxMagnitude);
                }

                half alpha = input.color.a * shape;
                clip(alpha - 0.002h);
                half3 color = MixFog(input.color.rgb, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
