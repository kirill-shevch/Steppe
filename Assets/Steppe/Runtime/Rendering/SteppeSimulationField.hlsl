#ifndef STEPPE_SIMULATION_FIELD_INCLUDED
#define STEPPE_SIMULATION_FIELD_INCLUDED

TEXTURE2D(_SteppeSimulationNaturalMap);
SAMPLER(sampler_SteppeSimulationNaturalMap);
TEXTURE2D(_SteppeSimulationDiagnosticMap);
SAMPLER(sampler_SteppeSimulationDiagnosticMap);

// xy = minimum canonical XZ, zw = inverse finite-world width and height.
float4 _SteppeSimulationMapBounds;
float _SteppeSimulationNaturalReady;
float _SteppeDiagnosticOverlayStrength;

struct SteppeSimulationFieldSample
{
    half soilDryness;
    half burnScar;
    half fireIntensity;
    half soilCompaction;
    half valid;
};

float3 SteppeSimulationUvAndMask(float2 canonicalXZ)
{
    float2 uv = (canonicalXZ - _SteppeSimulationMapBounds.xy)
                * _SteppeSimulationMapBounds.zw;
    float inside = step(0.0, uv.x)
                   * step(0.0, uv.y)
                   * step(uv.x, 1.0)
                   * step(uv.y, 1.0)
                   * saturate(_SteppeSimulationNaturalReady);
    return float3(saturate(uv), inside);
}

SteppeSimulationFieldSample SampleSteppeSimulationFieldLevel(
    float2 canonicalXZ,
    float lod)
{
    float3 uvAndMask = SteppeSimulationUvAndMask(canonicalXZ);
    half4 encoded = SAMPLE_TEXTURE2D_LOD(
        _SteppeSimulationNaturalMap,
        sampler_SteppeSimulationNaturalMap,
        uvAndMask.xy,
        lod);
    encoded *= (half)uvAndMask.z;
    SteppeSimulationFieldSample result;
    result.soilDryness = encoded.r;
    result.burnScar = encoded.g;
    result.fireIntensity = encoded.b;
    result.soilCompaction = encoded.a;
    result.valid = (half)uvAndMask.z;
    return result;
}

SteppeSimulationFieldSample SampleSteppeSimulationField(float2 canonicalXZ)
{
    float3 uvAndMask = SteppeSimulationUvAndMask(canonicalXZ);
    half4 encoded = SAMPLE_TEXTURE2D(
        _SteppeSimulationNaturalMap,
        sampler_SteppeSimulationNaturalMap,
        uvAndMask.xy);
    encoded *= (half)uvAndMask.z;
    SteppeSimulationFieldSample result;
    result.soilDryness = encoded.r;
    result.burnScar = encoded.g;
    result.fireIntensity = encoded.b;
    result.soilCompaction = encoded.a;
    result.valid = (half)uvAndMask.z;
    return result;
}

half4 SampleSteppeDiagnosticField(float2 canonicalXZ)
{
    float3 uvAndMask = SteppeSimulationUvAndMask(canonicalXZ);
    half4 diagnostic = SAMPLE_TEXTURE2D(
        _SteppeSimulationDiagnosticMap,
        sampler_SteppeSimulationDiagnosticMap,
        uvAndMask.xy);
    diagnostic.a *= (half)uvAndMask.z * saturate(_SteppeDiagnosticOverlayStrength);
    return diagnostic;
}

#endif
