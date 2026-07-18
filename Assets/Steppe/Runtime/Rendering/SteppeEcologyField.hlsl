#ifndef STEPPE_ECOLOGY_FIELD_INCLUDED
#define STEPPE_ECOLOGY_FIELD_INCLUDED

TEXTURE2D(_SteppeEcologyStateMap);
SAMPLER(sampler_SteppeEcologyStateMap);

// xy = canonical modulo origin, z = inverse world size, w = world size.
float4 _SteppeEcologyMapOriginSize;
// x = resolution, y = ready, z = cell size, w = canonical coordinate period.
float4 _SteppeEcologyMapParameters;

struct SteppeEcologyFieldSample
{
    half surfaceWater;
    half biomass;
    half greenFraction;
    half surfaceCrust;
};

float3 SteppeEcologyUvAndMask(float2 canonicalXZ)
{
    float period = max(1.0, _SteppeEcologyMapParameters.w);
    float2 offset = canonicalXZ - _SteppeEcologyMapOriginSize.xy;
    offset -= floor(offset / period) * period;
    float worldSize = max(1.0, _SteppeEcologyMapOriginSize.w);
    float inside = step(offset.x, worldSize)
                   * step(offset.y, worldSize)
                   * saturate(_SteppeEcologyMapParameters.y);
    return float3(saturate(offset * _SteppeEcologyMapOriginSize.z), inside);
}

SteppeEcologyFieldSample DecodeSteppeEcologyField(half4 encoded, half valid)
{
    // Missing/unpublished data must leave the existing static world unchanged.
    half4 neutral = half4(0.0h, 1.0h, 0.5h, 0.0h);
    encoded = lerp(neutral, encoded, valid);
    SteppeEcologyFieldSample output;
    output.surfaceWater = encoded.r;
    output.biomass = encoded.g;
    output.greenFraction = encoded.b;
    output.surfaceCrust = encoded.a;
    return output;
}

SteppeEcologyFieldSample SampleSteppeEcologyField(float2 canonicalXZ)
{
    float3 uvAndMask = SteppeEcologyUvAndMask(canonicalXZ);
    half4 encoded = SAMPLE_TEXTURE2D(
        _SteppeEcologyStateMap,
        sampler_SteppeEcologyStateMap,
        uvAndMask.xy);
    return DecodeSteppeEcologyField(encoded, (half)uvAndMask.z);
}

SteppeEcologyFieldSample SampleSteppeEcologyFieldLevel(float2 canonicalXZ, float lod)
{
    float3 uvAndMask = SteppeEcologyUvAndMask(canonicalXZ);
    half4 encoded = SAMPLE_TEXTURE2D_LOD(
        _SteppeEcologyStateMap,
        sampler_SteppeEcologyStateMap,
        uvAndMask.xy,
        lod);
    return DecodeSteppeEcologyField(encoded, (half)uvAndMask.z);
}

#endif
