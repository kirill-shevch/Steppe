Shader "Hidden/Steppe/Surface Heat Haze"
{
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "RenderPipeline"="UniversalPipeline"
        }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "SurfaceTemperatureRefraction"

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _SteppeHeatHaze;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord.xy;
                float lowerAirBand = smoothstep(0.015, 0.18, uv.y)
                                     * (1.0 - smoothstep(0.70, 0.94, uv.y));
                float broadWave = sin(
                    uv.y * 176.0
                    + uv.x * 31.0
                    + _Time.y * 7.3);
                float fineWave = sin(
                    uv.y * 337.0
                    - uv.x * 19.0
                    - _Time.y * 11.7);
                float amplitude = saturate(_SteppeHeatHaze)
                                  * lowerAirBand
                                  * 0.00145;
                float2 refractedUv = uv + float2(
                    broadWave + fineWave * 0.42,
                    fineWave * 0.16) * amplitude;
                return SAMPLE_TEXTURE2D_X_LOD(
                    _BlitTexture,
                    sampler_LinearClamp,
                    saturate(refractedUv),
                    _BlitMipLevel);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
