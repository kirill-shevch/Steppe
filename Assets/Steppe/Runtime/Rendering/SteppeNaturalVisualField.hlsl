#ifndef STEPPE_NATURAL_VISUAL_FIELD_INCLUDED
#define STEPPE_NATURAL_VISUAL_FIELD_INCLUDED

TEXTURE2D(_SteppeNaturalTerrainAFrom);
SAMPLER(sampler_SteppeNaturalTerrainAFrom);
TEXTURE2D(_SteppeNaturalTerrainATo);
SAMPLER(sampler_SteppeNaturalTerrainATo);
TEXTURE2D(_SteppeNaturalTerrainBFrom);
SAMPLER(sampler_SteppeNaturalTerrainBFrom);
TEXTURE2D(_SteppeNaturalTerrainBTo);
SAMPLER(sampler_SteppeNaturalTerrainBTo);
TEXTURE2D(_SteppeNaturalSoilAFrom);
SAMPLER(sampler_SteppeNaturalSoilAFrom);
TEXTURE2D(_SteppeNaturalSoilATo);
SAMPLER(sampler_SteppeNaturalSoilATo);
TEXTURE2D(_SteppeNaturalSoilBFrom);
SAMPLER(sampler_SteppeNaturalSoilBFrom);
TEXTURE2D(_SteppeNaturalSoilBTo);
SAMPLER(sampler_SteppeNaturalSoilBTo);
TEXTURE2D(_SteppeNaturalWaterAFrom);
SAMPLER(sampler_SteppeNaturalWaterAFrom);
TEXTURE2D(_SteppeNaturalWaterATo);
SAMPLER(sampler_SteppeNaturalWaterATo);
TEXTURE2D(_SteppeNaturalLifeAFrom);
SAMPLER(sampler_SteppeNaturalLifeAFrom);
TEXTURE2D(_SteppeNaturalLifeATo);
SAMPLER(sampler_SteppeNaturalLifeATo);
TEXTURE2D(_SteppeNaturalLifeBFrom);
SAMPLER(sampler_SteppeNaturalLifeBFrom);
TEXTURE2D(_SteppeNaturalLifeBTo);
SAMPLER(sampler_SteppeNaturalLifeBTo);
TEXTURE2D(_SteppeNaturalMaterialAFrom);
SAMPLER(sampler_SteppeNaturalMaterialAFrom);
TEXTURE2D(_SteppeNaturalMaterialATo);
SAMPLER(sampler_SteppeNaturalMaterialATo);
TEXTURE2D(_SteppeNaturalProcessAFrom);
SAMPLER(sampler_SteppeNaturalProcessAFrom);
TEXTURE2D(_SteppeNaturalProcessATo);
SAMPLER(sampler_SteppeNaturalProcessATo);
TEXTURE2D(_SteppeNaturalDrainageFrom);
SAMPLER(sampler_SteppeNaturalDrainageFrom);
TEXTURE2D(_SteppeNaturalDrainageTo);
SAMPLER(sampler_SteppeNaturalDrainageTo);

// xy = minimum canonical XZ, zw = inverse finite-world width and height.
float4 _SteppeNaturalVisualMapBounds;
// xy = raster dimensions, z = cell size in metres, w = inverse cell size.
float4 _SteppeNaturalVisualGrid;
float _SteppeNaturalVisualBlend;
float _SteppeNaturalVisualReady;

struct SteppeNaturalVisualFieldSample
{
    float depressionStorageMm;
    float soilDepthM;
    float sandFraction;
    float siltFraction;
    float clayFraction;
    float porosity;
    float permeabilityMmPerHour;
    float mineralContent;
    float soilCompaction;
    float rootWaterMm;
    float precipitationMmPerHour;
    float surfaceWaterMm;
    float groundwaterMm;
    float snowWaterEquivalentMm;
    float frozenSoilFraction;
    float liveBiomassGrams;
    float dryBiomassGrams;
    float litterBiomassGrams;
    float organicNitrogenGrams;
    float looseSedimentKg;
    float surfaceCrustFraction;
    float dustGrams;
    float fireIntensity;
    float burnScarFraction;
    float catchmentId;
    float valid;
};

struct SteppeNaturalVegetationFieldSample
{
    float frozenSoilFraction;
    float liveBiomassGrams;
    float dryBiomassGrams;
    float plantNitrogenGrams;
    float snowWaterEquivalentMm;
    float valid;
};

struct SteppeNaturalGroundDetailFieldSample
{
    float soilDepthM;
    float frozenSoilFraction;
    float litterBiomassGrams;
    float seedBankFraction;
    float soilOrganicMatterGrams;
    float availableNitrogenGrams;
    float organicNitrogenGrams;
    float faultInfluence;
    float rockHardness;
    float groundwaterMm;
    float snowWaterEquivalentMm;
    float burnScarFraction;
    float valid;
};

struct SteppeNaturalHydrologyFieldSample
{
    float2 drainageDirection;
    float contributingLog2;
    float branchOrder;
    float depressionStorageMm;
    float soilDepthM;
    float catchmentId;
    float valid;
};

float3 SteppeNaturalVisualUvAndMask(float2 canonicalXZ)
{
    float2 uv = (canonicalXZ - _SteppeNaturalVisualMapBounds.xy)
                * _SteppeNaturalVisualMapBounds.zw;
    float inside = step(0.0, uv.x)
                   * step(0.0, uv.y)
                   * step(uv.x, 1.0)
                   * step(uv.y, 1.0)
                   * saturate(_SteppeNaturalVisualReady);
    return float3(saturate(uv), inside);
}

float2 SteppeNaturalVisualCellIndex(float2 canonicalXZ)
{
    float2 relativeCells = (canonicalXZ - _SteppeNaturalVisualMapBounds.xy)
                           * _SteppeNaturalVisualGrid.w;
    return clamp(
        floor(relativeCells),
        float2(0.0, 0.0),
        _SteppeNaturalVisualGrid.xy - 1.0);
}

float2 SteppeNaturalVisualCellCenter(float2 cellIndex)
{
    return _SteppeNaturalVisualMapBounds.xy
           + (cellIndex + 0.5) * _SteppeNaturalVisualGrid.z;
}

float4 SampleSteppeNaturalDrainageCellLevel(float2 cellIndex, float lod)
{
    float2 uv = (cellIndex + 0.5) / _SteppeNaturalVisualGrid.xy;
    return lerp(
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalDrainageFrom,
            sampler_SteppeNaturalDrainageFrom,
            uv,
            lod),
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalDrainageTo,
            sampler_SteppeNaturalDrainageTo,
            uv,
            lod),
        saturate(_SteppeNaturalVisualBlend));
}

SteppeNaturalHydrologyFieldSample SampleSteppeNaturalHydrologyFieldLevel(
    float2 canonicalXZ,
    float lod)
{
    float3 uvAndMask = SteppeNaturalVisualUvAndMask(canonicalXZ);
    float blend = saturate(_SteppeNaturalVisualBlend);
    float4 terrainA = lerp(
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalTerrainAFrom,
            sampler_SteppeNaturalTerrainAFrom,
            uvAndMask.xy,
            lod),
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalTerrainATo,
            sampler_SteppeNaturalTerrainATo,
            uvAndMask.xy,
            lod),
        blend);
    float4 terrainB = lerp(
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalTerrainBFrom,
            sampler_SteppeNaturalTerrainBFrom,
            uvAndMask.xy,
            lod),
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalTerrainBTo,
            sampler_SteppeNaturalTerrainBTo,
            uvAndMask.xy,
            lod),
        blend);
    float4 processA = lerp(
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalProcessAFrom,
            sampler_SteppeNaturalProcessAFrom,
            uvAndMask.xy,
            lod),
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalProcessATo,
            sampler_SteppeNaturalProcessATo,
            uvAndMask.xy,
            lod),
        blend);
    float4 drainage = lerp(
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalDrainageFrom,
            sampler_SteppeNaturalDrainageFrom,
            uvAndMask.xy,
            lod),
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalDrainageTo,
            sampler_SteppeNaturalDrainageTo,
            uvAndMask.xy,
            lod),
        blend);

    SteppeNaturalHydrologyFieldSample result;
    result.drainageDirection = drainage.xy * uvAndMask.z;
    result.contributingLog2 = drainage.z * uvAndMask.z;
    result.branchOrder = drainage.w * uvAndMask.z;
    result.depressionStorageMm = terrainB.b * uvAndMask.z;
    result.soilDepthM = lerp(1.1, terrainA.a, uvAndMask.z);
    result.catchmentId = processA.b * uvAndMask.z;
    result.valid = uvAndMask.z;
    return result;
}

float SampleSteppeNaturalSurfaceWaterLevel(float2 canonicalXZ, float lod)
{
    float3 uvAndMask = SteppeNaturalVisualUvAndMask(canonicalXZ);
    float4 waterA = lerp(
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalWaterAFrom,
            sampler_SteppeNaturalWaterAFrom,
            uvAndMask.xy,
            lod),
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalWaterATo,
            sampler_SteppeNaturalWaterATo,
            uvAndMask.xy,
            lod),
        saturate(_SteppeNaturalVisualBlend));
    return waterA.g * uvAndMask.z;
}

float SampleSteppeNaturalSurfaceWater(float2 canonicalXZ)
{
    return SampleSteppeNaturalSurfaceWaterLevel(canonicalXZ, 0.0);
}

float SampleSteppeNaturalSnowLevel(float2 canonicalXZ, float lod)
{
    float3 uvAndMask = SteppeNaturalVisualUvAndMask(canonicalXZ);
    float4 waterA = lerp(
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalWaterAFrom,
            sampler_SteppeNaturalWaterAFrom,
            uvAndMask.xy,
            lod),
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalWaterATo,
            sampler_SteppeNaturalWaterATo,
            uvAndMask.xy,
            lod),
        saturate(_SteppeNaturalVisualBlend));
    return waterA.a * uvAndMask.z;
}

float SampleSteppeNaturalSnow(float2 canonicalXZ)
{
    return SampleSteppeNaturalSnowLevel(canonicalXZ, 0.0);
}

SteppeNaturalVegetationFieldSample SampleSteppeNaturalVegetationFieldLevel(
    float2 canonicalXZ,
    float lod)
{
    float3 uvAndMask = SteppeNaturalVisualUvAndMask(canonicalXZ);
    float blend = saturate(_SteppeNaturalVisualBlend);
    float4 waterA = lerp(
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalWaterAFrom,
            sampler_SteppeNaturalWaterAFrom,
            uvAndMask.xy,
            lod),
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalWaterATo,
            sampler_SteppeNaturalWaterATo,
            uvAndMask.xy,
            lod),
        blend);
    float4 lifeA = lerp(
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalLifeAFrom,
            sampler_SteppeNaturalLifeAFrom,
            uvAndMask.xy,
            lod),
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalLifeATo,
            sampler_SteppeNaturalLifeATo,
            uvAndMask.xy,
            lod),
        blend);
    float4 lifeB = lerp(
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalLifeBFrom,
            sampler_SteppeNaturalLifeBFrom,
            uvAndMask.xy,
            lod),
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalLifeBTo,
            sampler_SteppeNaturalLifeBTo,
            uvAndMask.xy,
            lod),
        blend);

    float valid = uvAndMask.z;
    waterA = lerp(float4(0.0, 0.0, 42.0, 0.0), waterA, valid);
    lifeA = lerp(float4(0.0, 90.0, 80.0, 60.0), lifeA, valid);
    lifeB = lerp(float4(0.45, 1200.0, 3.0, 0.8), lifeB, valid);

    SteppeNaturalVegetationFieldSample result;
    result.frozenSoilFraction = lifeA.r;
    result.liveBiomassGrams = lifeA.g;
    result.dryBiomassGrams = lifeA.b;
    result.plantNitrogenGrams = lifeB.a;
    result.snowWaterEquivalentMm = waterA.a;
    result.valid = valid;
    return result;
}

SteppeNaturalGroundDetailFieldSample SampleSteppeNaturalGroundDetailFieldLevel(
    float2 canonicalXZ,
    float lod)
{
    float3 uvAndMask = SteppeNaturalVisualUvAndMask(canonicalXZ);
    float blend = saturate(_SteppeNaturalVisualBlend);
    float4 terrainA = lerp(
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalTerrainAFrom,
            sampler_SteppeNaturalTerrainAFrom,
            uvAndMask.xy,
            lod),
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalTerrainATo,
            sampler_SteppeNaturalTerrainATo,
            uvAndMask.xy,
            lod),
        blend);
    float4 terrainB = lerp(
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalTerrainBFrom,
            sampler_SteppeNaturalTerrainBFrom,
            uvAndMask.xy,
            lod),
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalTerrainBTo,
            sampler_SteppeNaturalTerrainBTo,
            uvAndMask.xy,
            lod),
        blend);
    float4 lifeA = lerp(
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalLifeAFrom,
            sampler_SteppeNaturalLifeAFrom,
            uvAndMask.xy,
            lod),
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalLifeATo,
            sampler_SteppeNaturalLifeATo,
            uvAndMask.xy,
            lod),
        blend);
    float4 lifeB = lerp(
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalLifeBFrom,
            sampler_SteppeNaturalLifeBFrom,
            uvAndMask.xy,
            lod),
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalLifeBTo,
            sampler_SteppeNaturalLifeBTo,
            uvAndMask.xy,
            lod),
        blend);
    float4 materialA = lerp(
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalMaterialAFrom,
            sampler_SteppeNaturalMaterialAFrom,
            uvAndMask.xy,
            lod),
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalMaterialATo,
            sampler_SteppeNaturalMaterialATo,
            uvAndMask.xy,
            lod),
        blend);
    float4 waterA = lerp(
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalWaterAFrom,
            sampler_SteppeNaturalWaterAFrom,
            uvAndMask.xy,
            lod),
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalWaterATo,
            sampler_SteppeNaturalWaterATo,
            uvAndMask.xy,
            lod),
        blend);
    float4 processA = lerp(
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalProcessAFrom,
            sampler_SteppeNaturalProcessAFrom,
            uvAndMask.xy,
            lod),
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalProcessATo,
            sampler_SteppeNaturalProcessATo,
            uvAndMask.xy,
            lod),
        blend);

    float valid = uvAndMask.z;
    terrainA = lerp(float4(0.0, 0.0, 0.0, 1.1), terrainA, valid);
    lifeA = lerp(float4(0.0, 90.0, 80.0, 60.0), lifeA, valid);
    lifeB = lerp(float4(0.45, 1200.0, 0.65, 0.8), lifeB, valid);
    materialA = lerp(float4(8.0, 0.8, 0.16, 0.0), materialA, valid);
    terrainB = lerp(float4(0.0, 0.42, 0.0, 0.0), terrainB, valid);
    waterA = lerp(float4(0.0, 0.0, 42.0, 0.0), waterA, valid);
    processA = lerp(float4(0.0, 0.0, 0.0, 0.0), processA, valid);

    SteppeNaturalGroundDetailFieldSample result;
    result.soilDepthM = terrainA.a;
    result.frozenSoilFraction = lifeA.r;
    result.litterBiomassGrams = lifeA.a;
    result.seedBankFraction = lifeB.r;
    result.soilOrganicMatterGrams = lifeB.g;
    result.availableNitrogenGrams = lifeB.b;
    result.organicNitrogenGrams = materialA.r;
    result.faultInfluence = terrainB.r;
    result.rockHardness = terrainB.g;
    result.groundwaterMm = waterA.b;
    result.snowWaterEquivalentMm = waterA.a;
    result.burnScarFraction = processA.g;
    result.valid = valid;
    return result;
}

SteppeNaturalVisualFieldSample SampleSteppeNaturalVisualField(float2 canonicalXZ)
{
    float3 uvAndMask = SteppeNaturalVisualUvAndMask(canonicalXZ);
    float blend = saturate(_SteppeNaturalVisualBlend);
    float4 terrainA = lerp(
        SAMPLE_TEXTURE2D(_SteppeNaturalTerrainAFrom, sampler_SteppeNaturalTerrainAFrom, uvAndMask.xy),
        SAMPLE_TEXTURE2D(_SteppeNaturalTerrainATo, sampler_SteppeNaturalTerrainATo, uvAndMask.xy),
        blend);
    float4 terrainB = lerp(
        SAMPLE_TEXTURE2D(_SteppeNaturalTerrainBFrom, sampler_SteppeNaturalTerrainBFrom, uvAndMask.xy),
        SAMPLE_TEXTURE2D(_SteppeNaturalTerrainBTo, sampler_SteppeNaturalTerrainBTo, uvAndMask.xy),
        blend);
    float4 soilA = lerp(
        SAMPLE_TEXTURE2D(_SteppeNaturalSoilAFrom, sampler_SteppeNaturalSoilAFrom, uvAndMask.xy),
        SAMPLE_TEXTURE2D(_SteppeNaturalSoilATo, sampler_SteppeNaturalSoilATo, uvAndMask.xy),
        blend);
    float4 soilB = lerp(
        SAMPLE_TEXTURE2D(_SteppeNaturalSoilBFrom, sampler_SteppeNaturalSoilBFrom, uvAndMask.xy),
        SAMPLE_TEXTURE2D(_SteppeNaturalSoilBTo, sampler_SteppeNaturalSoilBTo, uvAndMask.xy),
        blend);
    float4 waterA = lerp(
        SAMPLE_TEXTURE2D(_SteppeNaturalWaterAFrom, sampler_SteppeNaturalWaterAFrom, uvAndMask.xy),
        SAMPLE_TEXTURE2D(_SteppeNaturalWaterATo, sampler_SteppeNaturalWaterATo, uvAndMask.xy),
        blend);
    float4 lifeA = lerp(
        SAMPLE_TEXTURE2D(_SteppeNaturalLifeAFrom, sampler_SteppeNaturalLifeAFrom, uvAndMask.xy),
        SAMPLE_TEXTURE2D(_SteppeNaturalLifeATo, sampler_SteppeNaturalLifeATo, uvAndMask.xy),
        blend);
    float4 materialA = lerp(
        SAMPLE_TEXTURE2D(_SteppeNaturalMaterialAFrom, sampler_SteppeNaturalMaterialAFrom, uvAndMask.xy),
        SAMPLE_TEXTURE2D(_SteppeNaturalMaterialATo, sampler_SteppeNaturalMaterialATo, uvAndMask.xy),
        blend);
    float4 processA = lerp(
        SAMPLE_TEXTURE2D(_SteppeNaturalProcessAFrom, sampler_SteppeNaturalProcessAFrom, uvAndMask.xy),
        SAMPLE_TEXTURE2D(_SteppeNaturalProcessATo, sampler_SteppeNaturalProcessATo, uvAndMask.xy),
        blend);

    // The fallback is intentionally ordinary semi-dry loam. It only exists during
    // bootstrap or outside the finite world and does not encode another state.
    float valid = uvAndMask.z;
    terrainA = lerp(float4(0.0, 0.0, 0.0, 1.1), terrainA, valid);
    terrainB = lerp(float4(0.0, 0.42, 0.0, 0.0), terrainB, valid);
    soilA = lerp(float4(0.38, 0.36, 0.26, 0.43), soilA, valid);
    soilB = lerp(float4(8.0, 0.45, 0.05, 58.0), soilB, valid);
    waterA = lerp(float4(0.0, 0.0, 42.0, 0.0), waterA, valid);
    lifeA = lerp(float4(0.0, 90.0, 80.0, 60.0), lifeA, valid);
    materialA = lerp(float4(8.0, 0.8, 0.16, 0.0), materialA, valid);
    processA = lerp(float4(0.0, 0.0, 0.0, 0.0), processA, valid);

    SteppeNaturalVisualFieldSample result;
    result.depressionStorageMm = terrainB.b;
    result.soilDepthM = terrainA.a;
    result.sandFraction = soilA.r;
    result.siltFraction = soilA.g;
    result.clayFraction = soilA.b;
    result.porosity = soilA.a;
    result.permeabilityMmPerHour = soilB.r;
    result.mineralContent = soilB.g;
    result.soilCompaction = soilB.b;
    result.rootWaterMm = soilB.a;
    result.precipitationMmPerHour = waterA.r;
    result.surfaceWaterMm = waterA.g;
    result.groundwaterMm = waterA.b;
    result.snowWaterEquivalentMm = waterA.a;
    result.frozenSoilFraction = lifeA.r;
    result.liveBiomassGrams = lifeA.g;
    result.dryBiomassGrams = lifeA.b;
    result.litterBiomassGrams = lifeA.a;
    result.organicNitrogenGrams = materialA.r;
    result.looseSedimentKg = materialA.g;
    result.surfaceCrustFraction = materialA.b;
    result.dustGrams = materialA.a;
    result.fireIntensity = processA.r;
    result.burnScarFraction = processA.g;
    result.catchmentId = processA.b;
    result.valid = valid;
    return result;
}

#endif
