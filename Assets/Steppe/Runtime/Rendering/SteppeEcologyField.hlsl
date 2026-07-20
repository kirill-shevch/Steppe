#ifndef STEPPE_ECOLOGY_FIELD_INCLUDED
#define STEPPE_ECOLOGY_FIELD_INCLUDED

TEXTURE2D(_SteppeEcologyStateMap);
SAMPLER(sampler_SteppeEcologyStateMap);
TEXTURE2D(_SteppeCryosphereStateMap);
SAMPLER(sampler_SteppeCryosphereStateMap);

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
    half snowWater;
    half snowCompaction;
    half frozenFraction;
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

SteppeEcologyFieldSample DecodeSteppeEcologyField(
    half4 encoded,
    half4 cryosphere,
    half valid)
{
    // Missing/unpublished data must leave the existing static world unchanged.
    half4 neutral = half4(0.0h, 1.0h, 0.5h, 0.0h);
    encoded = lerp(neutral, encoded, valid);
    cryosphere *= valid;
    SteppeEcologyFieldSample output;
    output.surfaceWater = encoded.r;
    output.biomass = encoded.g;
    output.greenFraction = encoded.b;
    output.surfaceCrust = encoded.a;
    output.snowWater = cryosphere.r;
    output.snowCompaction = cryosphere.g;
    output.frozenFraction = cryosphere.b;
    return output;
}

SteppeEcologyFieldSample SampleSteppeEcologyField(float2 canonicalXZ)
{
    float3 uvAndMask = SteppeEcologyUvAndMask(canonicalXZ);
    half4 encoded = SAMPLE_TEXTURE2D(
        _SteppeEcologyStateMap,
        sampler_SteppeEcologyStateMap,
        uvAndMask.xy);
    half4 cryosphere = SAMPLE_TEXTURE2D(
        _SteppeCryosphereStateMap,
        sampler_SteppeCryosphereStateMap,
        uvAndMask.xy);
    return DecodeSteppeEcologyField(encoded, cryosphere, (half)uvAndMask.z);
}

SteppeEcologyFieldSample SampleSteppeEcologyFieldLevel(float2 canonicalXZ, float lod)
{
    float3 uvAndMask = SteppeEcologyUvAndMask(canonicalXZ);
    half4 encoded = SAMPLE_TEXTURE2D_LOD(
        _SteppeEcologyStateMap,
        sampler_SteppeEcologyStateMap,
        uvAndMask.xy,
        lod);
    half4 cryosphere = SAMPLE_TEXTURE2D_LOD(
        _SteppeCryosphereStateMap,
        sampler_SteppeCryosphereStateMap,
        uvAndMask.xy,
        lod);
    return DecodeSteppeEcologyField(encoded, cryosphere, (half)uvAndMask.z);
}

#endif
