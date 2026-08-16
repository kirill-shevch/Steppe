namespace Steppe.Simulation;

public enum SimulationLayer
{
    Elevation,
    Slope,
    Aspect,
    SoilDepth,
    SandFraction,
    SiltFraction,
    ClayFraction,
    Porosity,
    Permeability,
    MineralContent,
    SoilCompaction,
    FaultInfluence,
    RockHardness,
    DepressionStorage,
    Drainage,
    Catchments,
    SolarRadiation,
    SurfaceTemperature,
    SoilTemperature,
    AirTemperature,
    Pressure,
    Wind,
    Humidity,
    CloudWater,
    Precipitation,
    SurfaceWater,
    RootWater,
    Groundwater,
    Snow,
    FrozenSoil,
    LiveBiomass,
    DryBiomass,
    LitterBiomass,
    SeedBank,
    SoilOrganicMatter,
    AvailableNitrogen,
    PlantNitrogen,
    OrganicNitrogen,
    LooseSediment,
    SurfaceCrust,
    Dust,
    FireIntensity,
    BurnScar,
}

public enum StateGroup
{
    Terrain,
    Soil,
    Atmosphere,
    Water,
    Life,
    Material
}

public enum StateKind
{
    Raw,
    Diagnostic
}

public enum StateScale
{
    Linear,
    Logarithmic,
    Diverging,
    Cyclic,
    Categorical
}

public sealed record StateDescriptor(
    SimulationLayer Id,
    StateGroup Group,
    string GroupTitle,
    int Order,
    string Title,
    string ShortTitle,
    string Description,
    string Unit,
    StateKind Kind,
    StateScale Scale,
    float ScaleMinimum,
    float ScaleMaximum,
    int Precision,
    string Swatch,
    string[] Palette);

public sealed record LayerStatistics(
    float Mean,
    float Percentile02,
    float Percentile10,
    float Median,
    float Percentile90,
    float Percentile98,
    int BelowScaleCount,
    int AboveScaleCount,
    int NonFiniteCount,
    int[] Histogram);

public sealed record LayerSnapshot(
    SimulationLayer Layer,
    int Width,
    int Height,
    float[] Values,
    float Minimum,
    float Maximum,
    string Unit,
    LayerStatistics Statistics,
    float[]? VectorX = null,
    float[]? VectorY = null);

public sealed record CellStateValue(
    SimulationLayer State,
    float Value);

public sealed record FluxContribution(
    SimulationFlux Flux,
    float Amount,
    float StateContribution,
    string FluxUnit,
    string StateUnit);

public sealed record StateExplanation(
    SimulationLayer State,
    float Value,
    float Delta,
    double PeriodHours,
    float ExplainedDelta,
    float UnexplainedDelta,
    FluxContribution[] Contributions);

public sealed record CellSnapshot(
    int X,
    int Y,
    float ElevationM,
    float Slope,
    int CatchmentId,
    float SurfaceTemperatureC,
    float SoilTemperatureC,
    float AirTemperatureC,
    float AirPressureHpa,
    float HumidityMm,
    float CloudWaterMm,
    float WindXMs,
    float WindYMs,
    float PrecipitationMmPerHour,
    float SurfaceWaterMm,
    float RootWaterMm,
    float GroundwaterMm,
    float SnowWaterEquivalentMm,
    float FrozenSoilFraction,
    float LiveBiomassGm2,
    float DryBiomassGm2,
    float LitterBiomassGm2,
    float AvailableNitrogenGm2,
    float SoilCompactionFraction,
    float LooseSedimentKgM2,
    float DustGm2,
    float FireIntensityFraction,
    float BurnScarFraction,
    string BiomeDescription,
    int? DrainToX,
    int? DrainToY,
    CellStateValue[] States);

public sealed record WorldSummary(
    int Width,
    int Height,
    float CellSizeMeters,
    int Seed,
    double ElapsedHours,
    int Year,
    int DayOfYear,
    double HourOfDay,
    string Season,
    double DayLengthHours,
    double SunElevationDegrees,
    float MeanSurfaceTemperatureC,
    float MeanPrecipitationMmPerHour,
    float MeanLiveBiomassGm2,
    WaterBudgetSnapshot WaterBudget,
    NitrogenBudgetSnapshot NitrogenBudget);

internal static class BiomeClassifier
{
    public static string Describe(WorldState state, int index)
    {
        if (state.BurnScarFraction[index] > 0.35f)
        {
            return state.FireIntensityFraction[index] > 0.02f ? "burning steppe" : "burnt steppe";
        }

        if (state.SurfaceWaterMm[index] > 18f || state.GroundwaterMm[index] > 145f)
        {
            return "seasonal wetland";
        }

        if (state.SnowWaterEquivalentMm[index] > 25f)
        {
            return "snow-covered steppe";
        }

        if (state.RootWaterMm[index] > 90f && state.LiveBiomassGm2[index] > 320f)
        {
            return "meadow steppe";
        }

        if (state.RootWaterMm[index] < 28f && state.LiveBiomassGm2[index] < 100f)
        {
            return state.SurfaceCrustFraction[index] > 0.48f ? "crusted semi-desert" : "dry steppe";
        }

        return state.LiveBiomassGm2[index] > 200f ? "grass steppe" : "sparse steppe";
    }
}
