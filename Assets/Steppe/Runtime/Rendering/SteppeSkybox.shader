Shader "Steppe/Skybox"
{
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float _SteppeDaylight;
            float _SteppeNightAmount;
            float _SteppeMoonVisibility;
            float4 _SteppeSunDirection;
            float4 _SteppeMoonDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            float SkyHash21(float2 value)
            {
                float3 p = frac(float3(value.xyx) * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                float horizon = pow(saturate(1.0 - abs(direction.y)), 3.0);
                float upperSky = smoothstep(-0.12, 0.72, direction.y);

                half3 dayHorizon = half3(0.69, 0.79, 0.87);
                half3 dayZenith = half3(0.24, 0.48, 0.74);
                half3 nightHorizon = half3(0.035, 0.050, 0.085);
                half3 nightZenith = half3(0.004, 0.009, 0.027);
                half3 daySky = lerp(dayHorizon, dayZenith, upperSky);
                half3 nightSky = lerp(nightHorizon, nightZenith, upperSky);
                half3 color = lerp(nightSky, daySky, saturate(_SteppeDaylight));

                float sunDot = saturate(dot(direction, normalize(_SteppeSunDirection.xyz)));
                float twilight = pow(sunDot, 28.0) * horizon;
                color += half3(0.90, 0.29, 0.10)
                         * twilight
                         * (1.0 - saturate(_SteppeDaylight))
                         * 0.48;
                float sunDisk = smoothstep(0.99972, 0.99991, sunDot);
                float sunHalo = pow(sunDot, 420.0);
                color += half3(1.0, 0.86, 0.59)
                         * (sunDisk * 3.2 + sunHalo * 0.45)
                         * saturate(_SteppeDaylight);

                // A stable latitude/longitude lattice keeps the star field deterministic
                // without a texture or per-star objects. Two hash samples vary rarity and
                // brightness while the local falloff keeps each star smaller than its cell.
                float longitude = atan2(direction.x, direction.z) * 0.15915494 + 0.5;
                float latitude = asin(clamp(direction.y, -1.0, 1.0)) * 0.31830989 + 0.5;
                float2 starGrid = float2(longitude, latitude) * float2(520.0, 260.0);
                float2 starCell = floor(starGrid);
                float2 starLocal = frac(starGrid) - 0.5;
                float rarity = SkyHash21(starCell);
                float brightness = SkyHash21(starCell + 17.31);
                float radius = lerp(0.075, 0.19, brightness);
                float starShape = 1.0 - smoothstep(radius, radius + 0.10, length(starLocal));
                float star = starShape
                             * step(0.986, rarity)
                             * lerp(0.38, 1.35, brightness)
                             * smoothstep(-0.08, 0.16, direction.y)
                             * saturate(_SteppeNightAmount);
                color += star * lerp(half3(0.58, 0.70, 1.0), half3(1.0, 0.88, 0.67), brightness);

                float moonDot = saturate(dot(direction, normalize(_SteppeMoonDirection.xyz)));
                float moonDisk = smoothstep(0.99955, 0.99982, moonDot);
                float moonHalo = pow(moonDot, 520.0) * 0.22;
                float moonMottle = lerp(
                    0.76,
                    1.0,
                    SkyHash21(floor(direction.xz * 420.0)));
                color += half3(0.70, 0.80, 1.0)
                         * moonHalo
                         * saturate(_SteppeMoonVisibility);
                color = lerp(
                    color,
                    half3(0.78, 0.84, 0.91) * moonMottle,
                    moonDisk * saturate(_SteppeMoonVisibility));

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
