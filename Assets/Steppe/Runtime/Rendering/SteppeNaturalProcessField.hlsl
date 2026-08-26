#ifndef STEPPE_NATURAL_PROCESS_FIELD_INCLUDED
#define STEPPE_NATURAL_PROCESS_FIELD_INCLUDED

TEXTURE2D(_SteppeNaturalFlux00From);
SAMPLER(sampler_SteppeNaturalFlux00From);
TEXTURE2D(_SteppeNaturalFlux00To);
SAMPLER(sampler_SteppeNaturalFlux00To);
TEXTURE2D(_SteppeNaturalFlux01From);
SAMPLER(sampler_SteppeNaturalFlux01From);
TEXTURE2D(_SteppeNaturalFlux01To);
SAMPLER(sampler_SteppeNaturalFlux01To);
TEXTURE2D(_SteppeNaturalFlux02From);
SAMPLER(sampler_SteppeNaturalFlux02From);
TEXTURE2D(_SteppeNaturalFlux02To);
SAMPLER(sampler_SteppeNaturalFlux02To);
TEXTURE2D(_SteppeNaturalFlux03From);
SAMPLER(sampler_SteppeNaturalFlux03From);
TEXTURE2D(_SteppeNaturalFlux03To);
SAMPLER(sampler_SteppeNaturalFlux03To);
TEXTURE2D(_SteppeNaturalFlux04From);
SAMPLER(sampler_SteppeNaturalFlux04From);
TEXTURE2D(_SteppeNaturalFlux04To);
SAMPLER(sampler_SteppeNaturalFlux04To);

TEXTURE2D(_SteppeNaturalVectorSurfaceRunoffFrom);
SAMPLER(sampler_SteppeNaturalVectorSurfaceRunoffFrom);
TEXTURE2D(_SteppeNaturalVectorSurfaceRunoffTo);
SAMPLER(sampler_SteppeNaturalVectorSurfaceRunoffTo);
TEXTURE2D(_SteppeNaturalVectorGroundwaterFlowFrom);
SAMPLER(sampler_SteppeNaturalVectorGroundwaterFlowFrom);
TEXTURE2D(_SteppeNaturalVectorGroundwaterFlowTo);
SAMPLER(sampler_SteppeNaturalVectorGroundwaterFlowTo);
TEXTURE2D(_SteppeNaturalVectorSnowTransportFrom);
SAMPLER(sampler_SteppeNaturalVectorSnowTransportFrom);
TEXTURE2D(_SteppeNaturalVectorSnowTransportTo);
SAMPLER(sampler_SteppeNaturalVectorSnowTransportTo);

TEXTURE2D(_SteppeNaturalVectorAirTemperatureAdvectionFrom);
SAMPLER(sampler_SteppeNaturalVectorAirTemperatureAdvectionFrom);
TEXTURE2D(_SteppeNaturalVectorAirTemperatureAdvectionTo);
SAMPLER(sampler_SteppeNaturalVectorAirTemperatureAdvectionTo);
TEXTURE2D(_SteppeNaturalVectorHumidityAdvectionFrom);
SAMPLER(sampler_SteppeNaturalVectorHumidityAdvectionFrom);
TEXTURE2D(_SteppeNaturalVectorHumidityAdvectionTo);
SAMPLER(sampler_SteppeNaturalVectorHumidityAdvectionTo);
TEXTURE2D(_SteppeNaturalVectorCloudAdvectionFrom);
SAMPLER(sampler_SteppeNaturalVectorCloudAdvectionFrom);
TEXTURE2D(_SteppeNaturalVectorCloudAdvectionTo);
SAMPLER(sampler_SteppeNaturalVectorCloudAdvectionTo);

float4 _SteppeNaturalProcessMapBounds;
float4 _SteppeNaturalProcessGrid;
float4 _SteppeNaturalProcessPeriodHours;
float _SteppeNaturalProcessBlend;
float _SteppeNaturalProcessReady;

struct SteppeNaturalHydrologyProcessSample
{
    float rainfallRate;
    float snowfallRate;
    float snowMeltRate;
    float snowSublimationRate;
    float snowTransportRate;
    float snowExportRate;
    float infiltrationRate;
    float percolationRate;
    float runoffInRate;
    float runoffOutRate;
    float groundwaterTransportRate;
    float groundwaterDischargeRate;
    float surfaceEvaporationRate;
    float transpirationRate;
    float2 surfaceRunoffVector;
    float surfaceRunoffMagnitude;
    float surfaceRunoffGrossMagnitude;
    float2 groundwaterFlowVector;
    float groundwaterFlowMagnitude;
    float2 snowTransportVector;
    float snowTransportMagnitude;
    float valid;
};

struct SteppeNaturalAtmosphereProcessSample
{
    float humidityTransportRate;
    float cloudTransportRate;
    float airTemperatureTransportRate;
    float condensationRate;
    float cloudEvaporationRate;
    float2 airTemperatureAdvectionVector;
    float airTemperatureAdvectionMagnitude;
    float airTemperatureAdvectionGrossMagnitude;
    float2 humidityAdvectionVector;
    float humidityAdvectionMagnitude;
    float humidityAdvectionGrossMagnitude;
    float2 cloudAdvectionVector;
    float cloudAdvectionMagnitude;
    float cloudAdvectionGrossMagnitude;
    float valid;
};

struct SteppeNaturalCloudProcessSample
{
    float cloudTransportRate;
    float condensationRate;
    float cloudEvaporationRate;
    float2 cloudAdvectionVector;
    float cloudAdvectionMagnitude;
    float cloudAdvectionGrossMagnitude;
    float valid;
};

float3 SteppeNaturalProcessUvAndMask(float2 canonicalXZ)
{
    float2 uv = (canonicalXZ - _SteppeNaturalProcessMapBounds.xy)
                * _SteppeNaturalProcessMapBounds.zw;
    float inside = step(0.0, uv.x)
                   * step(0.0, uv.y)
                   * step(uv.x, 1.0)
                   * step(uv.y, 1.0)
                   * saturate(_SteppeNaturalProcessReady);
    return float3(saturate(uv), inside);
}

SteppeNaturalHydrologyProcessSample SampleSteppeNaturalHydrologyProcess(
    float2 canonicalXZ)
{
    float3 uvAndMask = SteppeNaturalProcessUvAndMask(canonicalXZ);
    float blend = saturate(_SteppeNaturalProcessBlend);
    float4 flux01 = lerp(
        SAMPLE_TEXTURE2D(_SteppeNaturalFlux01From, sampler_SteppeNaturalFlux01From, uvAndMask.xy),
        SAMPLE_TEXTURE2D(_SteppeNaturalFlux01To, sampler_SteppeNaturalFlux01To, uvAndMask.xy),
        blend);
    float4 flux02 = lerp(
        SAMPLE_TEXTURE2D(_SteppeNaturalFlux02From, sampler_SteppeNaturalFlux02From, uvAndMask.xy),
        SAMPLE_TEXTURE2D(_SteppeNaturalFlux02To, sampler_SteppeNaturalFlux02To, uvAndMask.xy),
        blend);
    float4 flux03 = lerp(
        SAMPLE_TEXTURE2D(_SteppeNaturalFlux03From, sampler_SteppeNaturalFlux03From, uvAndMask.xy),
        SAMPLE_TEXTURE2D(_SteppeNaturalFlux03To, sampler_SteppeNaturalFlux03To, uvAndMask.xy),
        blend);
    float4 flux04 = lerp(
        SAMPLE_TEXTURE2D(_SteppeNaturalFlux04From, sampler_SteppeNaturalFlux04From, uvAndMask.xy),
        SAMPLE_TEXTURE2D(_SteppeNaturalFlux04To, sampler_SteppeNaturalFlux04To, uvAndMask.xy),
        blend);
    float4 runoff = lerp(
        SAMPLE_TEXTURE2D(
            _SteppeNaturalVectorSurfaceRunoffFrom,
            sampler_SteppeNaturalVectorSurfaceRunoffFrom,
            uvAndMask.xy),
        SAMPLE_TEXTURE2D(
            _SteppeNaturalVectorSurfaceRunoffTo,
            sampler_SteppeNaturalVectorSurfaceRunoffTo,
            uvAndMask.xy),
        blend);
    float4 groundwater = lerp(
        SAMPLE_TEXTURE2D(
            _SteppeNaturalVectorGroundwaterFlowFrom,
            sampler_SteppeNaturalVectorGroundwaterFlowFrom,
            uvAndMask.xy),
        SAMPLE_TEXTURE2D(
            _SteppeNaturalVectorGroundwaterFlowTo,
            sampler_SteppeNaturalVectorGroundwaterFlowTo,
            uvAndMask.xy),
        blend);
    float4 snowTransport = lerp(
        SAMPLE_TEXTURE2D(
            _SteppeNaturalVectorSnowTransportFrom,
            sampler_SteppeNaturalVectorSnowTransportFrom,
            uvAndMask.xy),
        SAMPLE_TEXTURE2D(
            _SteppeNaturalVectorSnowTransportTo,
            sampler_SteppeNaturalVectorSnowTransportTo,
            uvAndMask.xy),
        blend);

    float periodHours = max(
        0.0001,
        lerp(
            _SteppeNaturalProcessPeriodHours.x,
            _SteppeNaturalProcessPeriodHours.y,
            blend));
    float rateScale = uvAndMask.z / periodHours;
    SteppeNaturalHydrologyProcessSample result;
    // Flux01: CloudEvaporation, Rainfall, Snowfall, SnowMelt.
    result.rainfallRate = max(0.0, flux01.g) * rateScale;
    result.snowfallRate = max(0.0, flux01.b) * rateScale;
    result.snowMeltRate = max(0.0, flux01.a) * rateScale;
    // Flux02: SnowSublimation, SnowTransport, SnowExport, Infiltration.
    result.snowSublimationRate = max(0.0, flux02.r) * rateScale;
    result.snowTransportRate = abs(flux02.g) * rateScale;
    result.snowExportRate = max(0.0, flux02.b) * rateScale;
    result.infiltrationRate = max(0.0, flux02.a) * rateScale;
    // Flux03: Percolation, RunoffIn, RunoffOut, GroundwaterTransport.
    result.percolationRate = max(0.0, flux03.r) * rateScale;
    result.runoffInRate = max(0.0, flux03.g) * rateScale;
    result.runoffOutRate = max(0.0, flux03.b) * rateScale;
    result.groundwaterTransportRate = abs(flux03.a) * rateScale;
    // Flux04: GroundwaterDischarge, SurfaceEvaporation, Transpiration, PlantGrowth.
    result.groundwaterDischargeRate = max(0.0, flux04.r) * rateScale;
    result.surfaceEvaporationRate = max(0.0, flux04.g) * rateScale;
    result.transpirationRate = max(0.0, flux04.b) * rateScale;
    result.surfaceRunoffVector = runoff.xy * rateScale;
    result.surfaceRunoffMagnitude = runoff.z * rateScale;
    result.surfaceRunoffGrossMagnitude = runoff.w * rateScale;
    result.groundwaterFlowVector = groundwater.xy * rateScale;
    result.groundwaterFlowMagnitude = groundwater.z * rateScale;
    result.snowTransportVector = snowTransport.xy * rateScale;
    result.snowTransportMagnitude = snowTransport.z * rateScale;
    result.valid = uvAndMask.z;
    return result;
}

SteppeNaturalAtmosphereProcessSample SampleSteppeNaturalAtmosphereProcess(
    float2 canonicalXZ)
{
    float3 uvAndMask = SteppeNaturalProcessUvAndMask(canonicalXZ);
    float blend = saturate(_SteppeNaturalProcessBlend);
    float4 flux00 = lerp(
        SAMPLE_TEXTURE2D(_SteppeNaturalFlux00From, sampler_SteppeNaturalFlux00From, uvAndMask.xy),
        SAMPLE_TEXTURE2D(_SteppeNaturalFlux00To, sampler_SteppeNaturalFlux00To, uvAndMask.xy),
        blend);
    float4 flux01 = lerp(
        SAMPLE_TEXTURE2D(_SteppeNaturalFlux01From, sampler_SteppeNaturalFlux01From, uvAndMask.xy),
        SAMPLE_TEXTURE2D(_SteppeNaturalFlux01To, sampler_SteppeNaturalFlux01To, uvAndMask.xy),
        blend);
    float4 airTemperature = lerp(
        SAMPLE_TEXTURE2D(
            _SteppeNaturalVectorAirTemperatureAdvectionFrom,
            sampler_SteppeNaturalVectorAirTemperatureAdvectionFrom,
            uvAndMask.xy),
        SAMPLE_TEXTURE2D(
            _SteppeNaturalVectorAirTemperatureAdvectionTo,
            sampler_SteppeNaturalVectorAirTemperatureAdvectionTo,
            uvAndMask.xy),
        blend);
    float4 humidity = lerp(
        SAMPLE_TEXTURE2D(
            _SteppeNaturalVectorHumidityAdvectionFrom,
            sampler_SteppeNaturalVectorHumidityAdvectionFrom,
            uvAndMask.xy),
        SAMPLE_TEXTURE2D(
            _SteppeNaturalVectorHumidityAdvectionTo,
            sampler_SteppeNaturalVectorHumidityAdvectionTo,
            uvAndMask.xy),
        blend);
    float4 cloud = lerp(
        SAMPLE_TEXTURE2D(
            _SteppeNaturalVectorCloudAdvectionFrom,
            sampler_SteppeNaturalVectorCloudAdvectionFrom,
            uvAndMask.xy),
        SAMPLE_TEXTURE2D(
            _SteppeNaturalVectorCloudAdvectionTo,
            sampler_SteppeNaturalVectorCloudAdvectionTo,
            uvAndMask.xy),
        blend);
    float periodHours = max(
        0.0001,
        lerp(
            _SteppeNaturalProcessPeriodHours.x,
            _SteppeNaturalProcessPeriodHours.y,
            blend));
    float rateScale = uvAndMask.z / periodHours;
    SteppeNaturalAtmosphereProcessSample result;
    // Flux00: HumidityTransport, CloudTransport,
    // AirTemperatureTransport, Condensation.
    result.humidityTransportRate = flux00.r * rateScale;
    result.cloudTransportRate = flux00.g * rateScale;
    result.airTemperatureTransportRate = flux00.b * rateScale;
    result.condensationRate = max(0.0, flux00.a) * rateScale;
    // Flux01.r is CloudEvaporation.
    result.cloudEvaporationRate = max(0.0, flux01.r) * rateScale;
    result.airTemperatureAdvectionVector = airTemperature.xy * rateScale;
    result.airTemperatureAdvectionMagnitude = airTemperature.z * rateScale;
    result.airTemperatureAdvectionGrossMagnitude = airTemperature.w * rateScale;
    result.humidityAdvectionVector = humidity.xy * rateScale;
    result.humidityAdvectionMagnitude = humidity.z * rateScale;
    result.humidityAdvectionGrossMagnitude = humidity.w * rateScale;
    result.cloudAdvectionVector = cloud.xy * rateScale;
    result.cloudAdvectionMagnitude = cloud.z * rateScale;
    result.cloudAdvectionGrossMagnitude = cloud.w * rateScale;
    result.valid = uvAndMask.z;
    return result;
}

SteppeNaturalCloudProcessSample SampleSteppeNaturalCloudProcessLevel(
    float2 canonicalXZ,
    float level)
{
    float3 uvAndMask = SteppeNaturalProcessUvAndMask(canonicalXZ);
    float blend = saturate(_SteppeNaturalProcessBlend);
    float4 flux00 = lerp(
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalFlux00From,
            sampler_SteppeNaturalFlux00From,
            uvAndMask.xy,
            level),
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalFlux00To,
            sampler_SteppeNaturalFlux00To,
            uvAndMask.xy,
            level),
        blend);
    float4 flux01 = lerp(
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalFlux01From,
            sampler_SteppeNaturalFlux01From,
            uvAndMask.xy,
            level),
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalFlux01To,
            sampler_SteppeNaturalFlux01To,
            uvAndMask.xy,
            level),
        blend);
    float4 cloud = lerp(
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalVectorCloudAdvectionFrom,
            sampler_SteppeNaturalVectorCloudAdvectionFrom,
            uvAndMask.xy,
            level),
        SAMPLE_TEXTURE2D_LOD(
            _SteppeNaturalVectorCloudAdvectionTo,
            sampler_SteppeNaturalVectorCloudAdvectionTo,
            uvAndMask.xy,
            level),
        blend);
    float periodHours = max(
        0.0001,
        lerp(
            _SteppeNaturalProcessPeriodHours.x,
            _SteppeNaturalProcessPeriodHours.y,
            blend));
    float rateScale = uvAndMask.z / periodHours;
    SteppeNaturalCloudProcessSample result;
    result.cloudTransportRate = flux00.g * rateScale;
    result.condensationRate = max(0.0, flux00.a) * rateScale;
    result.cloudEvaporationRate = max(0.0, flux01.r) * rateScale;
    result.cloudAdvectionVector = cloud.xy * rateScale;
    result.cloudAdvectionMagnitude = cloud.z * rateScale;
    result.cloudAdvectionGrossMagnitude = cloud.w * rateScale;
    result.valid = uvAndMask.z;
    return result;
}

#endif
