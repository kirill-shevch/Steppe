namespace Steppe.Simulation;

/// <summary>
/// Structure-of-arrays storage keeps the whole finite world cache-friendly and serializable.
/// Arrays are internal: consumers observe immutable cell and layer snapshots.
/// </summary>
internal sealed class WorldState
{
    public WorldState(int cellCount)
    {
        ElevationM = New();
        Slope = New();
        AspectRadians = New();
        SoilDepthM = New();
        SandFraction = New();
        SiltFraction = New();
        ClayFraction = New();
        Porosity = New();
        PermeabilityMmPerHour = New();
        MineralContent = New();
        SoilCompactionFraction = New();
        FaultInfluence = New();
        RockHardness = New();
        DepressionStorageMm = New();
        DrainTo = new int[cellCount];
        CatchmentId = new int[cellCount];

        SolarRadiationWm2 = New();
        SurfaceTemperatureC = New();
        SoilTemperatureC = New();
        AirTemperatureC = New();
        AirPressureHpa = New();
        AirHumidityMm = New();
        CloudWaterMm = New();
        WindXMs = New();
        WindYMs = New();
        PrecipitationMmPerHour = New();

        SurfaceWaterMm = New();
        RootWaterMm = New();
        GroundwaterMm = New();
        SnowWaterEquivalentMm = New();
        FrozenSoilFraction = New();

        LiveBiomassGm2 = New();
        DryBiomassGm2 = New();
        LitterBiomassGm2 = New();
        SeedBank = New();
        SoilOrganicMatterGm2 = New();
        AvailableNitrogenGm2 = New();
        PlantNitrogenGm2 = New();
        OrganicNitrogenGm2 = New();
        LooseSedimentKgM2 = New();
        SurfaceCrustFraction = New();
        DustGm2 = New();
        FireIntensityFraction = New();
        BurnScarFraction = New();

        ScratchA = New();
        ScratchB = New();
        RunoffOutMm = New();
        RunoffVectorX = New();
        RunoffVectorY = New();

        TerrainNormalX = New();
        TerrainNormalY = New();
        TerrainNormalZ = New();
        ElevationCoolingC = New();
        BaseAirPressureHpa = New();
        PressureWaveSin = New();
        PressureWaveCos = New();
        PressureMeridionalSin = New();
        PressureMeridionalCos = New();
        PressureDiagonalSin = New();
        PressureDiagonalCos = New();
        SaturationHumidityMm = New();
        PercolationResponseAtBaseStep = New();

        float[] New() => new float[cellCount];
    }

    public float[] ElevationM { get; }
    public float[] Slope { get; }
    public float[] AspectRadians { get; }
    public float[] SoilDepthM { get; }
    public float[] SandFraction { get; }
    public float[] SiltFraction { get; }
    public float[] ClayFraction { get; }
    public float[] Porosity { get; }
    public float[] PermeabilityMmPerHour { get; }
    public float[] MineralContent { get; }
    public float[] SoilCompactionFraction { get; }
    public float[] FaultInfluence { get; }
    public float[] RockHardness { get; }
    public float[] DepressionStorageMm { get; }
    public int[] DrainTo { get; }
    public int[] CatchmentId { get; }

    public float[] SolarRadiationWm2 { get; }
    public float[] SurfaceTemperatureC { get; }
    public float[] SoilTemperatureC { get; }
    public float[] AirTemperatureC { get; }
    public float[] AirPressureHpa { get; }
    public float[] AirHumidityMm { get; }
    public float[] CloudWaterMm { get; }
    public float[] WindXMs { get; }
    public float[] WindYMs { get; }
    public float[] PrecipitationMmPerHour { get; }

    public float[] SurfaceWaterMm { get; }
    public float[] RootWaterMm { get; }
    public float[] GroundwaterMm { get; }
    public float[] SnowWaterEquivalentMm { get; }
    public float[] FrozenSoilFraction { get; }

    public float[] LiveBiomassGm2 { get; }
    public float[] DryBiomassGm2 { get; }
    public float[] LitterBiomassGm2 { get; }
    public float[] SeedBank { get; }
    public float[] SoilOrganicMatterGm2 { get; }
    public float[] AvailableNitrogenGm2 { get; }
    public float[] PlantNitrogenGm2 { get; }
    public float[] OrganicNitrogenGm2 { get; }
    public float[] LooseSedimentKgM2 { get; }
    public float[] SurfaceCrustFraction { get; }
    public float[] DustGm2 { get; }
    public float[] FireIntensityFraction { get; }
    public float[] BurnScarFraction { get; }

    public float[] ScratchA { get; }
    public float[] ScratchB { get; }
    public float[] RunoffOutMm { get; }
    public float[] RunoffVectorX { get; }
    public float[] RunoffVectorY { get; }

    // Rebuilt deterministic caches. They are derived from persistent terrain/configuration
    // and therefore deliberately excluded from SerializableFloatFields().
    public float[] TerrainNormalX { get; }
    public float[] TerrainNormalY { get; }
    public float[] TerrainNormalZ { get; }
    public float[] ElevationCoolingC { get; }
    public float[] BaseAirPressureHpa { get; }
    public float[] PressureWaveSin { get; }
    public float[] PressureWaveCos { get; }
    public float[] PressureMeridionalSin { get; }
    public float[] PressureMeridionalCos { get; }
    public float[] PressureDiagonalSin { get; }
    public float[] PressureDiagonalCos { get; }
    public float[] SaturationHumidityMm { get; }
    public float[] PercolationResponseAtBaseStep { get; }

    public void RebuildDerivedCaches(WorldConfig config)
    {
        const float twoPi = MathF.PI * 2f;
        var spatialScale = config.CellSizeMeters / 16_000f;
        var baseStepHours = config.BaseStepMinutes / 60f;
        for (var y = 0; y < config.Height; y++)
        {
            var py = y * spatialScale;
            for (var x = 0; x < config.Width; x++)
            {
                var index = y * config.Width + x;
                var slopeRadians = MathF.Atan(Slope[index]);
                var sinSlope = MathF.Sin(slopeRadians);
                TerrainNormalX[index] = sinSlope * MathF.Cos(AspectRadians[index]);
                TerrainNormalY[index] = sinSlope * MathF.Sin(AspectRadians[index]);
                TerrainNormalZ[index] = MathF.Cos(slopeRadians);
                ElevationCoolingC[index] = Math.Max(0f, ElevationM[index] - 250f) * 0.0062f;
                BaseAirPressureHpa[index] = 1013.25f * MathF.Exp(-ElevationM[index] / 8500f);
                PercolationResponseAtBaseStep[index] = 1f
                    - MathF.Exp(-PermeabilityMmPerHour[index] * 0.0012f * baseStepHours);

                var px = x * spatialScale;
                var waveSpatialPhase = twoPi * (px + MathF.Sin(py * twoPi) * 0.08f);
                PressureWaveSin[index] = MathF.Sin(waveSpatialPhase);
                PressureWaveCos[index] = MathF.Cos(waveSpatialPhase);
                var meridionalSpatialPhase = twoPi * py * 0.72f;
                PressureMeridionalSin[index] = MathF.Sin(meridionalSpatialPhase);
                PressureMeridionalCos[index] = MathF.Cos(meridionalSpatialPhase);
                var diagonalSpatialPhase = twoPi * (px * 0.55f + py * 0.70f);
                PressureDiagonalSin[index] = MathF.Sin(diagonalSpatialPhase);
                PressureDiagonalCos[index] = MathF.Cos(diagonalSpatialPhase);
            }
        }
    }

    public IEnumerable<float[]> SerializableFloatFields()
    {
        yield return ElevationM;
        yield return Slope;
        yield return AspectRadians;
        yield return SoilDepthM;
        yield return SandFraction;
        yield return SiltFraction;
        yield return ClayFraction;
        yield return Porosity;
        yield return PermeabilityMmPerHour;
        yield return MineralContent;
        yield return SoilCompactionFraction;
        yield return FaultInfluence;
        yield return RockHardness;
        yield return DepressionStorageMm;
        yield return SolarRadiationWm2;
        yield return SurfaceTemperatureC;
        yield return SoilTemperatureC;
        yield return AirTemperatureC;
        yield return AirPressureHpa;
        yield return AirHumidityMm;
        yield return CloudWaterMm;
        yield return WindXMs;
        yield return WindYMs;
        yield return PrecipitationMmPerHour;
        yield return SurfaceWaterMm;
        yield return RootWaterMm;
        yield return GroundwaterMm;
        yield return SnowWaterEquivalentMm;
        yield return FrozenSoilFraction;
        yield return LiveBiomassGm2;
        yield return DryBiomassGm2;
        yield return LitterBiomassGm2;
        yield return SeedBank;
        yield return SoilOrganicMatterGm2;
        yield return AvailableNitrogenGm2;
        yield return PlantNitrogenGm2;
        yield return OrganicNitrogenGm2;
        yield return LooseSedimentKgM2;
        yield return SurfaceCrustFraction;
        yield return DustGm2;
        yield return FireIntensityFraction;
        yield return BurnScarFraction;
    }
}
