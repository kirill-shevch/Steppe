namespace Steppe.Simulation;

/// <summary>
/// One physical exchange between a caravan and a single world cell. Public
/// callers use real quantities; FiniteWorld owns all density conversions.
/// </summary>
public sealed record CaravanPhysicalExchangeRequest
{
    public required int X { get; init; }
    public required int Y { get; init; }
    public float SurfaceWaterWithdrawalLiters { get; init; }
    public float SnowWithdrawalLiters { get; init; }
    public float LiveBiomassHarvestKg { get; init; }
    public float DryBiomassHarvestKg { get; init; }
    public float DustCaptureKg { get; init; }
    public float ChitinCollectionKg { get; init; }
    public float SurfaceWaterReturnLiters { get; init; }
    public float AtmosphericWaterReturnLiters { get; init; }
    public float OrganicMatterReturnKg { get; init; }
    public float OrganicNitrogenReturnKg { get; init; }
    public float SedimentReturnKg { get; init; }
    public float CompactionDelta { get; init; }
    public float TrailVegetationDamageKg { get; init; }
    public float DustLiftKg { get; init; }
    public float WasteHeatKwh { get; init; }
}

public sealed record CaravanPhysicalExchangeResult(
    int X,
    int Y,
    float SurfaceWaterWithdrawnLiters,
    float SnowWithdrawnLiters,
    float LiveBiomassHarvestedKg,
    float DryBiomassHarvestedKg,
    float PlantTissueWaterWithdrawnLiters,
    float OrganicNitrogenWithdrawnKg,
    float DustCapturedKg,
    float ChitinCollectedKg,
    float SurfaceWaterReturnedLiters,
    float AtmosphericWaterReturnedLiters,
    float OrganicMatterReturnedKg,
    float OrganicNitrogenReturnedKg,
    float SedimentReturnedKg,
    float CompactionApplied,
    float LiveVegetationDamagedKg,
    float DryVegetationDamagedKg,
    float DustLiftedKg,
    float WasteHeatAcceptedKwh,
    float AirTemperatureDeltaC);

/// <summary>
/// Scoped access to the locked FiniteWorld state during one coupled step. The
/// instance is invalid as soon as the AdvanceWithCaravan callback returns.
/// </summary>
public sealed class CaravanWorldAccess
{
    private readonly Func<int, int, CellSnapshot> sample;
    private readonly Func<int, int, int, CaravanOpportunityScan> scan;
    private readonly Func<CaravanPhysicalExchangeRequest, CaravanPhysicalExchangeResult> exchange;
    private bool active = true;

    internal CaravanWorldAccess(
        WorldConfig config,
        WorldClock clock,
        Func<int, int, CellSnapshot> sample,
        Func<int, int, int, CaravanOpportunityScan> scan,
        Func<CaravanPhysicalExchangeRequest, CaravanPhysicalExchangeResult> exchange)
    {
        Config = config;
        Clock = clock;
        this.sample = sample;
        this.scan = scan;
        this.exchange = exchange;
    }

    public WorldConfig Config { get; }
    public WorldClock Clock { get; }

    public CellSnapshot SampleCell(int x, int y)
    {
        EnsureActive();
        return sample(x, y);
    }

    public CaravanOpportunityScan ScanOpportunities(int x, int y, int radiusCells = 24)
    {
        EnsureActive();
        return scan(x, y, radiusCells);
    }

    public CaravanPhysicalExchangeResult Exchange(CaravanPhysicalExchangeRequest request)
    {
        EnsureActive();
        ArgumentNullException.ThrowIfNull(request);
        return exchange(request);
    }

    internal void Invalidate() => active = false;

    private void EnsureActive()
    {
        if (!active)
        {
            throw new InvalidOperationException("CaravanWorldAccess is valid only inside AdvanceWithCaravan.");
        }
    }
}

public sealed partial class FiniteWorld
{
    private const float PlantTissueWaterLitersPerKgDryMatter = 2.2f;

    /// <summary>
    /// Runs one deterministic caravan interaction and then advances nature and
    /// fauna inside the same diagnostic flux window.
    /// </summary>
    public TResult AdvanceWithCaravan<TResult>(
        double hours,
        Func<CaravanWorldAccess, TResult> interaction,
        CancellationToken cancellationToken = default)
    {
        if (!double.IsFinite(hours) || hours < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(hours));
        }

        ArgumentNullException.ThrowIfNull(interaction);
        lock (sync)
        {
            fluxState.Begin(SampleValue);
            var advanced = 0d;
            var access = new CaravanWorldAccess(
                Config,
                Clock,
                SampleCell,
                (x, y, radius) => CaravanOpportunityDetector.Scan(
                    Config, state, fauna, fluxState, x, y, radius),
                ApplyPhysicalCaravanExchange);
            try
            {
                var result = interaction(access);
                advanced = AdvanceCore(hours, cancellationToken);
                return result;
            }
            finally
            {
                access.Invalidate();
                fluxState.Complete(advanced, SampleValue);
            }
        }
    }

    private CaravanPhysicalExchangeResult ApplyPhysicalCaravanExchange(
        CaravanPhysicalExchangeRequest request)
    {
        ValidatePhysicalRequest(request);
        var index = CheckedIndex(request.X, request.Y);
        var cellAreaM2 = Config.CellSizeMeters * Config.CellSizeMeters;

        var water = CaravanInterventionEngine.WithdrawSurfaceWater(
            state,
            waterBudget,
            fluxState,
            index,
            request.X,
            request.Y,
            LitersToMillimeters(request.SurfaceWaterWithdrawalLiters, cellAreaM2));
        var snow = CaravanInterventionEngine.WithdrawSnow(
            state,
            waterBudget,
            fluxState,
            index,
            request.X,
            request.Y,
            LitersToMillimeters(request.SnowWithdrawalLiters, cellAreaM2));
        var biomass = CaravanInterventionEngine.HarvestBiomass(
            state,
            nitrogenBudget,
            fluxState,
            index,
            request.X,
            request.Y,
            KilogramsToGramsPerSquareMeter(request.LiveBiomassHarvestKg, cellAreaM2),
            KilogramsToGramsPerSquareMeter(request.DryBiomassHarvestKg, cellAreaM2));

        var liveHarvestKg = GramsPerSquareMeterToKilograms(biomass.LiveBiomassGm2, cellAreaM2);
        var tissueWaterRequestedLiters = liveHarvestKg * PlantTissueWaterLitersPerKgDryMatter;
        var tissueWaterMm = Math.Min(
            state.RootWaterMm[index],
            LitersToMillimeters(tissueWaterRequestedLiters, cellAreaM2));
        state.RootWaterMm[index] -= tissueWaterMm;
        waterBudget.AddExternal(-tissueWaterMm);
        fluxState.Add(SimulationFlux.CaravanPlantTissueWaterWithdrawal, index, tissueWaterMm);

        var capturedDust = CaravanInterventionEngine.CaptureDust(
            state,
            fluxState,
            index,
            request.X,
            request.Y,
            KilogramsToGramsPerSquareMeter(request.DustCaptureKg, cellAreaM2));
        var chitin = fauna.CollectMolt(
            request.X,
            request.Y,
            1.75f,
            request.ChitinCollectionKg);

        var returnedWater = CaravanInterventionEngine.ReturnSurfaceWater(
            state,
            waterBudget,
            fluxState,
            index,
            request.X,
            request.Y,
            LitersToMillimeters(request.SurfaceWaterReturnLiters, cellAreaM2));
        var returnedVaporMm = LitersToMillimeters(request.AtmosphericWaterReturnLiters, cellAreaM2);
        state.AirHumidityMm[index] += returnedVaporMm;
        waterBudget.AddExternal(returnedVaporMm);
        fluxState.Add(SimulationFlux.CaravanAtmosphericWaterReturn, index, returnedVaporMm);

        var returnedOrganic = CaravanInterventionEngine.ReturnOrganicMatter(
            state,
            nitrogenBudget,
            fluxState,
            index,
            request.X,
            request.Y,
            KilogramsToGramsPerSquareMeter(request.OrganicMatterReturnKg, cellAreaM2),
            KilogramsToGramsPerSquareMeter(request.OrganicNitrogenReturnKg, cellAreaM2));
        var returnedSediment = CaravanInterventionEngine.ReturnSediment(
            state,
            fluxState,
            index,
            request.X,
            request.Y,
            KilogramsToKilogramsPerSquareMeter(request.SedimentReturnKg, cellAreaM2));
        var compaction = CaravanInterventionEngine.CompactTrail(
            state,
            fluxState,
            index,
            request.X,
            request.Y,
            request.CompactionDelta);

        var damage = ApplyTrailVegetationDamage(
            index,
            request.TrailVegetationDamageKg,
            cellAreaM2);
        var dustLiftedKg = ApplyCaravanDustLift(index, request.DustLiftKg, cellAreaM2);
        var heat = ApplyCaravanWasteHeat(index, request.WasteHeatKwh, cellAreaM2);

        return new CaravanPhysicalExchangeResult(
            request.X,
            request.Y,
            MillimetersToLiters(water.SurfaceWaterMm, cellAreaM2),
            MillimetersToLiters(snow.SnowWaterEquivalentMm, cellAreaM2),
            liveHarvestKg,
            GramsPerSquareMeterToKilograms(biomass.DryBiomassGm2, cellAreaM2),
            MillimetersToLiters(tissueWaterMm, cellAreaM2),
            GramsPerSquareMeterToKilograms(biomass.NitrogenGm2, cellAreaM2),
            GramsPerSquareMeterToKilograms(capturedDust.DustGm2, cellAreaM2),
            chitin,
            MillimetersToLiters(returnedWater.SurfaceWaterMm, cellAreaM2),
            MillimetersToLiters(returnedVaporMm, cellAreaM2),
            GramsPerSquareMeterToKilograms(returnedOrganic.OrganicMatterGm2, cellAreaM2),
            GramsPerSquareMeterToKilograms(returnedOrganic.NitrogenGm2, cellAreaM2),
            KilogramsPerSquareMeterToKilograms(returnedSediment.SedimentKgM2, cellAreaM2),
            compaction.CompactionDelta,
            damage.LiveKg,
            damage.DryKg,
            dustLiftedKg,
            heat.AcceptedKwh,
            heat.TemperatureDeltaC);
    }

    private (float LiveKg, float DryKg) ApplyTrailVegetationDamage(
        int index,
        float requestedKg,
        float cellAreaM2)
    {
        if (requestedKg <= 0f) return (0f, 0f);
        var requestedGm2 = KilogramsToGramsPerSquareMeter(requestedKg, cellAreaM2);
        var liveAvailable = Math.Max(0f, state.LiveBiomassGm2[index] - 8f);
        var dryAvailable = Math.Max(0f, state.DryBiomassGm2[index] - 3f);
        var live = Math.Min(liveAvailable, requestedGm2 * 0.65f);
        var dry = Math.Min(dryAvailable, requestedGm2 - live);
        live += Math.Min(liveAvailable - live, requestedGm2 - live - dry);

        var liveBefore = state.LiveBiomassGm2[index];
        var nitrogen = liveBefore > 1e-8f
            ? Math.Min(state.PlantNitrogenGm2[index], state.PlantNitrogenGm2[index] * live / liveBefore)
            : 0f;
        state.LiveBiomassGm2[index] -= live;
        state.DryBiomassGm2[index] -= dry;
        state.LitterBiomassGm2[index] += live + dry;
        state.PlantNitrogenGm2[index] -= nitrogen;
        state.OrganicNitrogenGm2[index] += nitrogen;
        fluxState.Add(SimulationFlux.CaravanLiveVegetationDamage, index, live);
        fluxState.Add(SimulationFlux.CaravanDryVegetationDamage, index, dry);
        fluxState.Add(SimulationFlux.CaravanPlantNitrogenTransfer, index, nitrogen);
        return (
            GramsPerSquareMeterToKilograms(live, cellAreaM2),
            GramsPerSquareMeterToKilograms(dry, cellAreaM2));
    }

    private float ApplyCaravanDustLift(int index, float requestedKg, float cellAreaM2)
    {
        if (requestedKg <= 0f) return 0f;
        var requestedGm2 = KilogramsToGramsPerSquareMeter(requestedKg, cellAreaM2);
        var availableGm2 = Math.Max(0f, state.LooseSedimentKgM2[index] - 0.001f) * 1000f;
        var liftedGm2 = Math.Min(requestedGm2, availableGm2);
        state.LooseSedimentKgM2[index] -= liftedGm2 * 0.001f;
        state.DustGm2[index] += liftedGm2;
        fluxState.Add(SimulationFlux.CaravanDustLift, index, liftedGm2);
        return GramsPerSquareMeterToKilograms(liftedGm2, cellAreaM2);
    }

    private (float AcceptedKwh, float TemperatureDeltaC) ApplyCaravanWasteHeat(
        int index,
        float requestedKwh,
        float cellAreaM2)
    {
        if (requestedKwh <= 0f) return (0f, 0f);
        const float mixingHeightM = 20f;
        const float airDensityKgM3 = 1.2f;
        const float airHeatCapacityKjKgC = 1.005f;
        var heatCapacityKwhC = cellAreaM2 * mixingHeightM * airDensityKgM3
            * airHeatCapacityKjKgC / 3600f;
        var delta = Math.Min(2f, requestedKwh / Math.Max(1e-6f, heatCapacityKwhC));
        var accepted = delta * heatCapacityKwhC;
        state.AirTemperatureC[index] += delta;
        fluxState.Add(SimulationFlux.CaravanWasteHeat, index, delta);
        return (accepted, delta);
    }

    private static void ValidatePhysicalRequest(CaravanPhysicalExchangeRequest request)
    {
        var values = new[]
        {
            request.SurfaceWaterWithdrawalLiters,
            request.SnowWithdrawalLiters,
            request.LiveBiomassHarvestKg,
            request.DryBiomassHarvestKg,
            request.DustCaptureKg,
            request.ChitinCollectionKg,
            request.SurfaceWaterReturnLiters,
            request.AtmosphericWaterReturnLiters,
            request.OrganicMatterReturnKg,
            request.OrganicNitrogenReturnKg,
            request.SedimentReturnKg,
            request.CompactionDelta,
            request.TrailVegetationDamageKg,
            request.DustLiftKg,
            request.WasteHeatKwh
        };
        if (values.Any(value => !float.IsFinite(value) || value < 0f))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Physical exchange amounts must be finite and non-negative.");
        }
    }

    private static float LitersToMillimeters(float liters, float cellAreaM2) => liters / cellAreaM2;
    private static float MillimetersToLiters(float millimeters, float cellAreaM2) => millimeters * cellAreaM2;
    private static float KilogramsToGramsPerSquareMeter(float kilograms, float cellAreaM2) => kilograms * 1000f / cellAreaM2;
    private static float GramsPerSquareMeterToKilograms(float gramsPerSquareMeter, float cellAreaM2) => gramsPerSquareMeter * cellAreaM2 / 1000f;
    private static float KilogramsToKilogramsPerSquareMeter(float kilograms, float cellAreaM2) => kilograms / cellAreaM2;
    private static float KilogramsPerSquareMeterToKilograms(float kilogramsPerSquareMeter, float cellAreaM2) => kilogramsPerSquareMeter * cellAreaM2;
}
