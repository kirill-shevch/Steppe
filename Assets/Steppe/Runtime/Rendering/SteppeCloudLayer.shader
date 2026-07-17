Shader "Steppe/Cloud Layer"
{
    Properties
    {
        [NoScaleOffset] _WeatherMap("Weather Map", 2D) = "black" {}
        [NoScaleOffset] _CloudNoise("Cloud Detail", 2D) = "gray" {}
        _DryCloudColor("Dry Cloud", Color) = (0.98, 0.985, 1.0, 1)
        _WetCloudColor("Water-heavy Cloud", Color) = (0.38, 0.42, 0.48, 1)
        _NightCloudColor("Night Cloud", Color) = (0.035, 0.045, 0.065, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent-50"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "CloudDeck"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_WeatherMap);
            SAMPLER(sampler_WeatherMap);
            TEXTURE2D(_CloudNoise);
            SAMPLER(sampler_CloudNoise);

            CBUFFER_START(UnityPerMaterial)
                half4 _DryCloudColor;
                half4 _WetCloudColor;
                half4 _NightCloudColor;
                float4 _WeatherMapCenterLocal;
                float4 _WeatherMapAdvectionLocal;
                float4 _CloudNoiseWorldPhase;
                float4 _CloudNoiseAdvectionLocal;
                float _WeatherMapWorldSize;
                float _CloudNoiseWorldSize;
            CBUFFER_END

            float _SteppeDaylight;

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 positionWSXZ : TEXCOORD0;
                half edgeFade : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWSXZ = positions.positionWS.xz;
                output.edgeFade = input.color.a;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // The CPU map updates infrequently. Reading the old field upwind by the
                // elapsed displacement advects it smoothly between map publications.
                float2 uv = (input.positionWSXZ - _WeatherMapCenterLocal.xy - _WeatherMapAdvectionLocal.xy)
                            / max(_WeatherMapWorldSize, 1.0) + 0.5;
                // Never use a distant mip average for the semantic weather field: it
                // would spread the wet front into an indistinct full-sky veil.
                half3 weather = SAMPLE_TEXTURE2D_LOD(_WeatherMap, sampler_WeatherMap, uv, 0).rgb;
                half coverage = weather.r;
                half cloudWater = weather.g;
                half rain = weather.b;

                float2 canonicalOffset = input.positionWSXZ - _WeatherMapCenterLocal.xy
                                         + _CloudNoiseWorldPhase.xy
                                         - _CloudNoiseAdvectionLocal.xy;
                float2 noiseUv = canonicalOffset / max(_CloudNoiseWorldSize, 1.0);
                half4 detail = SAMPLE_TEXTURE2D_LOD(_CloudNoise, sampler_CloudNoise, noiseUv, 0);
                float2 warp = (detail.gb - 0.5h) * 0.075h;
                half4 warpedDetail = SAMPLE_TEXTURE2D_LOD(
                    _CloudNoise,
                    sampler_CloudNoise,
                    noiseUv + warp,
                    0);
                half secondaryDetail = SAMPLE_TEXTURE2D_LOD(
                    _CloudNoise,
                    sampler_CloudNoise,
                    noiseUv * 1.83h + half2(0.173h, 0.419h),
                    0).g;
                half shiftedDetail = SAMPLE_TEXTURE2D_LOD(
                    _CloudNoise,
                    sampler_CloudNoise,
                    noiseUv + half2(0.011h, -0.008h),
                    0).g;

                half organicShape = detail.r * 0.42h
                                    + warpedDetail.a * 0.38h
                                    + secondaryDetail * 0.20h;
                // The detail field owns topology. Letting coverage move this threshold
                // made an incoming cloud suddenly rebuild its silhouette at x100.
                // A front now changes only the visibility of a stable travelling shape.
                half structureMask = smoothstep(0.43h, 0.59h, organicShape);
                half coverageOpacity = smoothstep(0.035h, 0.62h, coverage);
                half cloudMask = structureMask * coverageOpacity;
                half waterDarkening = smoothstep(0.18h, 0.92h, cloudWater);
                half daylight = saturate(_SteppeDaylight);
                half3 daylightColor = lerp(_DryCloudColor.rgb, _WetCloudColor.rgb, waterDarkening);
                daylightColor *= lerp(1.0h, 0.58h, rain);
                half relief = (warpedDetail.g - shiftedDetail) * 1.15h
                              + (secondaryDetail - 0.5h) * 0.24h;
                daylightColor *= 0.82h + organicShape * 0.36h + relief;
                half3 color = lerp(_NightCloudColor.rgb, daylightColor, 0.12h + daylight * 0.88h);

                half opticalOpacity = lerp(0.72h, 0.985h, waterDarkening);
                half alpha = cloudMask * opticalOpacity * input.edgeFade;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
