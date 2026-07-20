#ifndef STEPPE_TRACK_FIELD_INCLUDED
#define STEPPE_TRACK_FIELD_INCLUDED

TEXTURE2D(_SteppeTrackStateMap);
SAMPLER(sampler_SteppeTrackStateMap);

// xy = canonical modulo origin, z = inverse world size, w = world size.
float4 _SteppeTrackMapOriginSize;
// x = resolution, y = ready, z = cell size, w = canonical coordinate period.
float4 _SteppeTrackMapParameters;
float _SteppeTrackRutDepth;

struct SteppeTrackFieldSample
{
    half vegetationFlattening;
    half snowCompression;
    half soilRut;
    half wetPrint;
};

float3 SteppeTrackUvAndMask(float2 canonicalXZ)
{
    float period = max(1.0, _SteppeTrackMapParameters.w);
    float2 offset = canonicalXZ - _SteppeTrackMapOriginSize.xy;
    offset -= floor(offset / period) * period;
    float worldSize = max(1.0, _SteppeTrackMapOriginSize.w);
    float inside = step(offset.x, worldSize)
                   * step(offset.y, worldSize)
                   * saturate(_SteppeTrackMapParameters.y);
    return float3(saturate(offset * _SteppeTrackMapOriginSize.z), inside);
}

SteppeTrackFieldSample DecodeSteppeTrackField(half4 encoded, half valid)
{
    encoded *= valid;
    SteppeTrackFieldSample output;
    output.vegetationFlattening = encoded.r;
    output.snowCompression = encoded.g;
    output.soilRut = encoded.b;
    output.wetPrint = encoded.a;
    return output;
}

SteppeTrackFieldSample SampleSteppeTrackField(float2 canonicalXZ)
{
    float3 uvAndMask = SteppeTrackUvAndMask(canonicalXZ);
    half4 encoded = SAMPLE_TEXTURE2D(
        _SteppeTrackStateMap,
        sampler_SteppeTrackStateMap,
        uvAndMask.xy);
    return DecodeSteppeTrackField(encoded, (half)uvAndMask.z);
}

SteppeTrackFieldSample SampleSteppeTrackFieldLevel(float2 canonicalXZ, float lod)
{
    float3 uvAndMask = SteppeTrackUvAndMask(canonicalXZ);
    half4 encoded = SAMPLE_TEXTURE2D_LOD(
        _SteppeTrackStateMap,
        sampler_SteppeTrackStateMap,
        uvAndMask.xy,
        lod);
    return DecodeSteppeTrackField(encoded, (half)uvAndMask.z);
}

#endif
