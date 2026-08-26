namespace Steppe.Simulation;

public enum CaravanActionVerb
{
    Absorb,
    Transform,
    Return,
    Read,
    Migrate
}

public enum CaravanActionKind
{
    ReadFlows,
    Move,
    WithdrawSurfaceWater,
    CondenseAtmosphericWater,
    CollectSnow,
    MeltSnow,
    HarvestLiveBiomass,
    HarvestDryBiomass,
    CureFreshBiomass,
    SpoilFreshBiomass,
    ConsumeBiomass,
    ReturnSurfaceWater,
    ReturnOrganicMatter,
    CaptureDust,
    ReturnSediment,
    CompactTrail,
    CollectMolt,
    UseChitinForRepair
}

public enum CaravanEcologyGoal
{
    AvoidWildfire,
    SecureWater,
    GatherBiomass,
    CureBiomass,
    RestoreSoil,
    ReturnMinerals,
    CollectMolt,
    FollowGrowthFront,
    FollowHarvester,
    TrackRainFront,
    SeasonalOpportunity
}

public sealed record CaravanEcologyConfig
{
    public float WaterCapacityMmCells { get; init; } = 0.14f;
    public float BiomassCapacityGm2Cells { get; init; } = 2.8f;
    public float SedimentCapacityKgM2Cells { get; init; } = 0.035f;
    public float ChitinCapacityKg { get; init; } = 600f;
    public float DailyWaterUseMmCells { get; init; } = 0.0034f;
    public float DailyBiomassUseGm2Cells { get; init; } = 0.055f;
    public float MovementWaterUseMmCellsPerCell { get; init; } = 0.00020f;
    public float MovementBiomassUseGm2CellsPerCell { get; init; } = 0.0045f;
    public float MinimumCollectableSurfaceWaterMm { get; init; } = 0.008f;
    public float MaximumDailyWaterWithdrawalMmCells { get; init; } = 0.022f;
    public float MaximumDailySnowMeltMmCells { get; init; } = 0.016f;
    public float MaximumDailyAtmosphericCondensationMmCells { get; init; } = 0.0040f;
    public float AtmosphericCondensationBiomassPerWater { get; init; } = 3.0f;
    public float MaximumDailyLiveHarvestGm2Cells { get; init; } = 0.18f;
    public float MaximumDailyDryHarvestGm2Cells { get; init; } = 0.12f;
    public float FreshBiomassEnergyFraction { get; init; } = 0.35f;
    public float FreshBiomassDailySpoilageFraction { get; init; } = 0.035f;
    public int BaseScoutRadiusCells { get; init; } = 12;
    public int MaximumScoutRadiusCells { get; init; } = 72;
    public int TravelCellsPerDay { get; init; } = 4;
    public float InitialWaterMmCells { get; init; } = 0.065f;
    public float InitialFreshBiomassGm2Cells { get; init; } = 0.10f;
    public float InitialDryBiomassGm2Cells { get; init; } = 0.62f;
    public float InitialOrganicNitrogenGm2Cells { get; init; } = 0.045f;
}

public sealed record CaravanAction(
    CaravanActionVerb Verb,
    CaravanActionKind Kind,
    int X,
    int Y,
    float Amount,
    string Unit,
    CaravanOpportunityKind? Opportunity = null);

public sealed record CaravanDailyImpact(
    int Year,
    int DayOfYear,
    CaravanEcologyGoal Goal,
    CaravanOpportunityKind? TargetOpportunity,
    int FromX,
    int FromY,
    int ToX,
    int ToY,
    float DistanceCells,
    int ScoutRadiusCells,
    bool WaterNeedMet,
    bool BiomassNeedMet,
    float WaterDemandMmCells,
    float WaterConsumedMmCells,
    float BiomassDemandGm2Cells,
    float BiomassEnergyConsumedGm2Cells,
    float Condition,
    float WaterMmCells,
    float FreshBiomassGm2Cells,
    float DryBiomassGm2Cells,
    float OrganicResidueGm2Cells,
    float CapturedSedimentKgM2Cells,
    float ChitinKg,
    CaravanOpportunityKind[] DetectedOpportunities,
    CaravanAction[] Actions);

public sealed record CaravanEcologySnapshot(
    int X,
    int Y,
    CaravanEcologyGoal Goal,
    CaravanOpportunityKind? TargetOpportunity,
    int TargetX,
    int TargetY,
    int SimulatedDays,
    float DistanceCells,
    float WaterMmCells,
    float FreshBiomassGm2Cells,
    float DryBiomassGm2Cells,
    float CargoNitrogenGm2Cells,
    float OrganicResidueGm2Cells,
    float ResidueNitrogenGm2Cells,
    float CapturedSedimentKgM2Cells,
    float ChitinKg,
    float CumulativeWaterWithdrawnMmCells,
    float CumulativeAtmosphericWaterCondensedMmCells,
    float CumulativeSnowMeltedMmCells,
    float CumulativeWaterReturnedMmCells,
    float CumulativeLiveHarvestedGm2Cells,
    float CumulativeDryHarvestedGm2Cells,
    float CumulativeBiomassCuredGm2Cells,
    float CumulativeBiomassConsumedGm2Cells,
    float CumulativeOrganicMatterReturnedGm2Cells,
    float CumulativeNitrogenReturnedGm2Cells,
    float CumulativeDustCapturedGm2Cells,
    float CumulativeSedimentReturnedKgM2Cells,
    float CumulativeCompaction,
    float CumulativeChitinCollectedKg,
    float CumulativeChitinUsedKg,
    float CumulativeWaterDeficitMmCells,
    float CumulativeBiomassDeficitGm2Cells,
    int UnmetWaterDays,
    int UnmetBiomassDays,
    int MaximumUnmetWaterStreakDays,
    int MaximumUnmetBiomassStreakDays,
    int CriticalConditionDays,
    float Condition,
    float MinimumCondition,
    int MeaningfulExchangeDays,
    int WorldExchangeDays,
    IReadOnlyDictionary<CaravanEcologyGoal, int> GoalDays,
    IReadOnlyDictionary<CaravanActionVerb, int> VerbActions,
    IReadOnlyDictionary<CaravanActionKind, int> ActionCounts,
    IReadOnlyDictionary<CaravanOpportunityKind, int> OpportunityDays);

/// <summary>
/// Deterministic qualification caravan. It persists needs, cargo, target memory
/// and failed-search memory while reading only the same world states and flows
/// that a player-facing adapter can expose.
/// </summary>
public sealed class CaravanEcologyAgent
{
    private readonly record struct NeedResult(float Demand, float Consumed)
    {
        public float Deficit => Math.Max(0f, Demand - Consumed);
        public float Fulfillment => Demand <= 1e-8f ? 1f : Math.Clamp(Consumed / Demand, 0f, 1f);
        public bool IsMet => Deficit <= 1e-8f;
    }

    private readonly CaravanEcologyConfig config;
    private readonly int[] goalDays = new int[RuntimeCompatibility.GetEnumValues<CaravanEcologyGoal>().Length];
    private readonly int[] verbActions = new int[RuntimeCompatibility.GetEnumValues<CaravanActionVerb>().Length];
    private readonly int[] actionCounts = new int[RuntimeCompatibility.GetEnumValues<CaravanActionKind>().Length];
    private readonly int[] opportunityDays = new int[RuntimeCompatibility.GetEnumValues<CaravanOpportunityKind>().Length];
    private int x = -1;
    private int y = -1;
    private int targetX = -1;
    private int targetY = -1;
    private CaravanOpportunityKind? targetOpportunity;
    private CaravanEcologyGoal goal = CaravanEcologyGoal.SeasonalOpportunity;
    private int simulatedDays;
    private int failedWaterSearchDays;
    private int failedBiomassSearchDays;
    private int unmetWaterDays;
    private int unmetBiomassDays;
    private int currentUnmetWaterStreak;
    private int currentUnmetBiomassStreak;
    private int maximumUnmetWaterStreak;
    private int maximumUnmetBiomassStreak;
    private int criticalConditionDays;
    private int meaningfulExchangeDays;
    private int worldExchangeDays;
    private float distanceCells;
    private float waterMmCells;
    private float freshBiomassGm2Cells;
    private float dryBiomassGm2Cells;
    private float cargoNitrogenGm2Cells;
    private float organicResidueGm2Cells;
    private float residueNitrogenGm2Cells;
    private float capturedSedimentKgM2Cells;
    private float chitinKg;
    private float cumulativeWaterWithdrawn;
    private float cumulativeAtmosphericWaterCondensed;
    private float cumulativeSnowMelted;
    private float cumulativeWaterReturned;
    private float cumulativeLiveHarvested;
    private float cumulativeDryHarvested;
    private float cumulativeBiomassCured;
    private float cumulativeBiomassConsumed;
    private float cumulativeReturnedMatter;
    private float cumulativeReturnedNitrogen;
    private float cumulativeDustCaptured;
    private float cumulativeSedimentReturned;
    private float cumulativeCompaction;
    private float cumulativeChitinCollected;
    private float cumulativeChitinUsed;
    private float cumulativeWaterDeficit;
    private float cumulativeBiomassDeficit;
    private float condition = 1f;
    private float minimumCondition = 1f;

    public CaravanEcologyAgent(CaravanEcologyConfig? config = null)
    {
        this.config = config ?? new CaravanEcologyConfig();
        ValidateConfig(this.config);
        waterMmCells = this.config.InitialWaterMmCells;
        freshBiomassGm2Cells = this.config.InitialFreshBiomassGm2Cells;
        dryBiomassGm2Cells = this.config.InitialDryBiomassGm2Cells;
        cargoNitrogenGm2Cells = this.config.InitialOrganicNitrogenGm2Cells;
    }

    public CaravanEcologySnapshot Capture() => new(
        x,
        y,
        goal,
        targetOpportunity,
        targetX,
        targetY,
        simulatedDays,
        distanceCells,
        waterMmCells,
        freshBiomassGm2Cells,
        dryBiomassGm2Cells,
        cargoNitrogenGm2Cells,
        organicResidueGm2Cells,
        residueNitrogenGm2Cells,
        capturedSedimentKgM2Cells,
        chitinKg,
        cumulativeWaterWithdrawn,
        cumulativeAtmosphericWaterCondensed,
        cumulativeSnowMelted,
        cumulativeWaterReturned,
        cumulativeLiveHarvested,
        cumulativeDryHarvested,
        cumulativeBiomassCured,
        cumulativeBiomassConsumed,
        cumulativeReturnedMatter,
        cumulativeReturnedNitrogen,
        cumulativeDustCaptured,
        cumulativeSedimentReturned,
        cumulativeCompaction,
        cumulativeChitinCollected,
        cumulativeChitinUsed,
        cumulativeWaterDeficit,
        cumulativeBiomassDeficit,
        unmetWaterDays,
        unmetBiomassDays,
        maximumUnmetWaterStreak,
        maximumUnmetBiomassStreak,
        criticalConditionDays,
        condition,
        minimumCondition,
        meaningfulExchangeDays,
        worldExchangeDays,
        RuntimeCompatibility.GetEnumValues<CaravanEcologyGoal>().ToDictionary(item => item, item => goalDays[(int)item]),
        RuntimeCompatibility.GetEnumValues<CaravanActionVerb>().ToDictionary(item => item, item => verbActions[(int)item]),
        RuntimeCompatibility.GetEnumValues<CaravanActionKind>().ToDictionary(item => item, item => actionCounts[(int)item]),
        RuntimeCompatibility.GetEnumValues<CaravanOpportunityKind>().ToDictionary(item => item, item => opportunityDays[(int)item]));

    internal CaravanDailyImpact Step(
        WorldConfig worldConfig,
        WorldClock clock,
        WorldState state,
        WaterBudget waterBudget,
        NitrogenBudget nitrogenBudget,
        WorldFauna fauna,
        WorldFluxState flux)
    {
        if (x < 0 || y < 0)
        {
            x = worldConfig.Width / 2;
            y = worldConfig.Height / 2;
            targetX = x;
            targetY = y;
        }

        var actions = new List<CaravanAction>(20);
        var originIndex = y * worldConfig.Width + x;
        var scoutRadius = Math.Min(
            Math.Min(config.MaximumScoutRadiusCells, Math.Max(worldConfig.Width, worldConfig.Height)),
            config.BaseScoutRadiusCells + Math.Max(failedWaterSearchDays, failedBiomassSearchDays) * 2);
        var scan = CaravanOpportunityDetector.Scan(worldConfig, state, fauna, flux, x, y, scoutRadius);
        foreach (var kind in scan.Opportunities.Select(item => item.Kind).Distinct())
        {
            opportunityDays[(int)kind]++;
        }
        AddAction(actions, CaravanActionVerb.Read, CaravanActionKind.ReadFlows, x, y,
            scan.LocalFlows.Length, "потоков");

        goal = SelectGoal(scan);
        goalDays[(int)goal]++;
        var selected = SelectOpportunity(scan, goal);
        if (selected is not null)
        {
            targetOpportunity = selected.Kind;
            (targetX, targetY) = LeadTarget(worldConfig, selected);
        }
        else if (goal == CaravanEcologyGoal.AvoidWildfire
                 && Find(scan, CaravanOpportunityKind.Wildfire) is { } fire)
        {
            targetOpportunity = CaravanOpportunityKind.Wildfire;
            targetX = Math.Clamp(x + Math.Sign(x - fire.X) * config.TravelCellsPerDay * 2, 0, worldConfig.Width - 1);
            targetY = Math.Clamp(y + Math.Sign(y - fire.Y) * config.TravelCellsPerDay * 2, 0, worldConfig.Height - 1);
        }
        else if (targetX < 0 || targetY < 0 || targetX == x && targetY == y)
        {
            SelectFallbackTarget(worldConfig, state, scan);
        }

        var fromX = x;
        var fromY = y;
        var path = MoveToward(worldConfig);
        var moved = Distance(fromX, fromY, x, y);
        if (moved > 1e-6f)
        {
            distanceCells += moved;
            AddAction(actions, CaravanActionVerb.Migrate, CaravanActionKind.Move, x, y,
                moved, "ячейка", targetOpportunity);
            var compaction = 0f;
            foreach (var (trailX, trailY) in path)
            {
                var index = trailY * worldConfig.Width + trailX;
                var request = 0.0012f * (1f - state.FrozenSoilFraction[index] * 0.75f);
                compaction += CaravanInterventionEngine.CompactTrail(
                    state, flux, index, trailX, trailY, request).CompactionDelta;
            }
            cumulativeCompaction += compaction;
            AddAction(actions, CaravanActionVerb.Migrate, CaravanActionKind.CompactTrail, x, y,
                compaction, "доля", targetOpportunity);
        }

        var demands = CalculateDailyDemands(state, originIndex, moved);
        var waterNeed = ConsumeWater(demands.WaterMmCells);
        var biomassNeed = ConsumeBiomass(demands.BiomassGm2Cells, actions);
        RecordSurvival(waterNeed, biomassNeed);
        var waterNeedMet = waterNeed.IsMet;
        var biomassNeedMet = biomassNeed.IsMet;

        var destinationIndex = y * worldConfig.Width + x;
        var waterBefore = cumulativeWaterWithdrawn + cumulativeSnowMelted;
        var biomassBefore = cumulativeLiveHarvested + cumulativeDryHarvested;
        AbsorbAtDestination(worldConfig, state, waterBudget, nitrogenBudget, fauna, flux, destinationIndex, actions);
        TransformAtDestination(state, destinationIndex, actions);
        ReturnAtDestination(state, waterBudget, nitrogenBudget, flux, destinationIndex, actions);

        if (cumulativeWaterWithdrawn + cumulativeSnowMelted > waterBefore + 1e-8f
            && waterMmCells >= config.DailyWaterUseMmCells * 4f)
        {
            failedWaterSearchDays = 0;
        }
        else if (goal == CaravanEcologyGoal.SecureWater)
        {
            failedWaterSearchDays = Math.Min(30, failedWaterSearchDays + 1);
        }

        var usableBiomassAfterHarvest = dryBiomassGm2Cells
            + freshBiomassGm2Cells * config.FreshBiomassEnergyFraction;
        if (cumulativeLiveHarvested + cumulativeDryHarvested > biomassBefore + 1e-8f
            && usableBiomassAfterHarvest >= config.DailyBiomassUseGm2Cells * 5f)
        {
            failedBiomassSearchDays = 0;
        }
        else if (goal == CaravanEcologyGoal.GatherBiomass)
        {
            failedBiomassSearchDays = Math.Min(30, failedBiomassSearchDays + 1);
        }

        if (actions.Any(item => item.Amount > 1e-8f
                                && item.Verb is CaravanActionVerb.Absorb
                                    or CaravanActionVerb.Transform
                                    or CaravanActionVerb.Return))
        {
            meaningfulExchangeDays++;
        }
        if (actions.Any(item => item.Amount > 1e-8f
                                && item.Verb is CaravanActionVerb.Absorb or CaravanActionVerb.Return))
        {
            worldExchangeDays++;
        }

        simulatedDays++;
        return new CaravanDailyImpact(
            clock.Year,
            clock.DayOfYear,
            goal,
            targetOpportunity,
            fromX,
            fromY,
            x,
            y,
            moved,
            scoutRadius,
            waterNeedMet,
            biomassNeedMet,
            waterNeed.Demand,
            waterNeed.Consumed,
            biomassNeed.Demand,
            biomassNeed.Consumed,
            condition,
            waterMmCells,
            freshBiomassGm2Cells,
            dryBiomassGm2Cells,
            organicResidueGm2Cells,
            capturedSedimentKgM2Cells,
            chitinKg,
            scan.Opportunities.Select(item => item.Kind).Distinct().OrderBy(item => item).ToArray(),
            actions.ToArray());
    }

    private NeedResult ConsumeWater(float demand)
    {
        var consumed = Math.Min(waterMmCells, demand);
        waterMmCells -= consumed;
        return new NeedResult(demand, consumed);
    }

    private NeedResult ConsumeBiomass(float demand, List<CaravanAction> actions)
    {
        var totalBefore = freshBiomassGm2Cells + dryBiomassGm2Cells;
        var dry = Math.Min(dryBiomassGm2Cells, demand);
        dryBiomassGm2Cells -= dry;
        var remainingDemand = Math.Max(0f, demand - dry);
        var fresh = Math.Min(
            freshBiomassGm2Cells,
            remainingDemand / config.FreshBiomassEnergyFraction);
        freshBiomassGm2Cells -= fresh;
        var consumedMass = dry + fresh;
        var consumedEnergy = dry + fresh * config.FreshBiomassEnergyFraction;
        var consumedNitrogen = totalBefore > 1e-8f
            ? Math.Min(cargoNitrogenGm2Cells, cargoNitrogenGm2Cells * consumedMass / totalBefore)
            : 0f;
        cargoNitrogenGm2Cells -= consumedNitrogen;
        organicResidueGm2Cells += consumedMass * 0.32f;
        residueNitrogenGm2Cells += consumedNitrogen;
        cumulativeBiomassConsumed += consumedMass;
        AddAction(actions, CaravanActionVerb.Transform, CaravanActionKind.ConsumeBiomass, x, y,
            consumedMass, "г/м²·ячейка");
        return new NeedResult(demand, consumedEnergy);
    }

    private (float WaterMmCells, float BiomassGm2Cells) CalculateDailyDemands(
        WorldState state,
        int index,
        float moved)
    {
        var temperature = state.AirTemperatureC[index];
        var humidity = state.AirHumidityMm[index];
        var wind = MathF.Sqrt(
            state.WindXMs[index] * state.WindXMs[index]
            + state.WindYMs[index] * state.WindYMs[index]);
        var heat = Math.Clamp((temperature - 17f) / 14f, 0f, 1.25f);
        var dryHeat = heat * Math.Clamp((9f - humidity) / 7f, 0f, 1f);
        var windExposure = Math.Clamp((wind - 7f) / 11f, 0f, 0.55f);
        var cold = Math.Clamp((4f - temperature) / 20f, 0f, 1.15f);
        var water = config.DailyWaterUseMmCells
            * (1f + heat * 0.70f + dryHeat * 0.45f + windExposure * 0.20f)
            + moved * config.MovementWaterUseMmCellsPerCell * (1f + heat * 0.35f);
        var biomass = config.DailyBiomassUseGm2Cells * (1f + cold * 0.70f)
            + moved * config.MovementBiomassUseGm2CellsPerCell
            * (1f + state.FrozenSoilFraction[index] * 0.25f);
        return (water, biomass);
    }

    private void RecordSurvival(NeedResult water, NeedResult biomass)
    {
        if (water.IsMet)
        {
            currentUnmetWaterStreak = 0;
        }
        else
        {
            unmetWaterDays++;
            currentUnmetWaterStreak++;
            maximumUnmetWaterStreak = Math.Max(maximumUnmetWaterStreak, currentUnmetWaterStreak);
            cumulativeWaterDeficit += water.Deficit;
        }

        if (biomass.IsMet)
        {
            currentUnmetBiomassStreak = 0;
        }
        else
        {
            unmetBiomassDays++;
            currentUnmetBiomassStreak++;
            maximumUnmetBiomassStreak = Math.Max(maximumUnmetBiomassStreak, currentUnmetBiomassStreak);
            cumulativeBiomassDeficit += biomass.Deficit;
        }

        var reserve = Math.Min(
            waterMmCells / config.WaterCapacityMmCells,
            (freshBiomassGm2Cells + dryBiomassGm2Cells) / config.BiomassCapacityGm2Cells);
        var recovery = Math.Min(water.Fulfillment, biomass.Fulfillment) * 0.010f
            + (water.IsMet && biomass.IsMet ? 0.004f + reserve * 0.006f : 0f);
        condition = Math.Clamp(
            condition
            + recovery
            - (1f - water.Fulfillment) * 0.07f
            - (1f - biomass.Fulfillment) * 0.045f,
            0f,
            1f);
        minimumCondition = Math.Min(minimumCondition, condition);
        if (condition < 0.30f) criticalConditionDays++;
    }

    private CaravanEcologyGoal SelectGoal(CaravanOpportunityScan scan)
    {
        var fire = Find(scan, CaravanOpportunityKind.Wildfire);
        if (fire is { DistanceCells: < 6f }) return CaravanEcologyGoal.AvoidWildfire;
        var usableBiomass = dryBiomassGm2Cells
            + freshBiomassGm2Cells * config.FreshBiomassEnergyFraction;
        if (usableBiomass < config.DailyBiomassUseGm2Cells * 2f)
            return CaravanEcologyGoal.GatherBiomass;
        if (waterMmCells < config.DailyWaterUseMmCells * 8f || failedWaterSearchDays > 0)
            return CaravanEcologyGoal.SecureWater;
        if (organicResidueGm2Cells > 0.16f
            && (Find(scan, CaravanOpportunityKind.OrganicPoorSoil) is not null
                || Find(scan, CaravanOpportunityKind.RecoverableSoil) is not null))
            return CaravanEcologyGoal.RestoreSoil;
        if (capturedSedimentKgM2Cells > config.SedimentCapacityKgM2Cells * 0.001f
            && Find(scan, CaravanOpportunityKind.DepositionZone) is not null)
            return CaravanEcologyGoal.ReturnMinerals;
        if (freshBiomassGm2Cells > 0.35f && Find(scan, CaravanOpportunityKind.DryingWindow) is not null)
            return CaravanEcologyGoal.CureBiomass;
        if (usableBiomass < config.DailyBiomassUseGm2Cells * 10f
            || failedBiomassSearchDays > 0)
            return CaravanEcologyGoal.GatherBiomass;
        if (chitinKg < config.ChitinCapacityKg * 0.8f && Find(scan, CaravanOpportunityKind.HarvesterMolt) is not null)
            return CaravanEcologyGoal.CollectMolt;
        if (Find(scan, CaravanOpportunityKind.GreenGrowth) is not null)
            return CaravanEcologyGoal.FollowGrowthFront;
        if (Find(scan, CaravanOpportunityKind.HarvesterFront) is not null)
            return CaravanEcologyGoal.FollowHarvester;
        if (Find(scan, CaravanOpportunityKind.RainFront) is not null)
            return CaravanEcologyGoal.TrackRainFront;
        return CaravanEcologyGoal.SeasonalOpportunity;
    }

    private static CaravanOpportunity? SelectOpportunity(
        CaravanOpportunityScan scan,
        CaravanEcologyGoal selectedGoal)
    {
        CaravanOpportunityKind[] kinds = selectedGoal switch
        {
            CaravanEcologyGoal.AvoidWildfire => [],
            CaravanEcologyGoal.SecureWater =>
            [CaravanOpportunityKind.SurfaceWater, CaravanOpportunityKind.SnowmeltRunoff,
                CaravanOpportunityKind.GroundwaterDischarge, CaravanOpportunityKind.RainFront,
                CaravanOpportunityKind.Snowpack],
            CaravanEcologyGoal.GatherBiomass =>
            [CaravanOpportunityKind.GreenGrowth, CaravanOpportunityKind.DryStandingBiomass],
            CaravanEcologyGoal.CureBiomass => [CaravanOpportunityKind.DryingWindow],
            CaravanEcologyGoal.RestoreSoil =>
            [CaravanOpportunityKind.OrganicPoorSoil, CaravanOpportunityKind.RecoverableSoil],
            CaravanEcologyGoal.ReturnMinerals => [CaravanOpportunityKind.DepositionZone],
            CaravanEcologyGoal.CollectMolt => [CaravanOpportunityKind.HarvesterMolt],
            CaravanEcologyGoal.FollowGrowthFront => [CaravanOpportunityKind.GreenGrowth],
            CaravanEcologyGoal.FollowHarvester => [CaravanOpportunityKind.HarvesterFront],
            CaravanEcologyGoal.TrackRainFront => [CaravanOpportunityKind.RainFront],
            _ =>
            [CaravanOpportunityKind.DryingWindow, CaravanOpportunityKind.FrozenCorridor,
                CaravanOpportunityKind.HarvesterFront, CaravanOpportunityKind.GreenGrowth,
                CaravanOpportunityKind.DryStandingBiomass]
        };
        if (selectedGoal == CaravanEcologyGoal.SecureWater)
        {
            foreach (var kind in kinds)
            {
                if (Find(scan, kind) is { } opportunity) return opportunity;
            }
            return null;
        }
        return kinds
            .Select(kind => Find(scan, kind))
            .Where(item => item is not null)
            .OrderByDescending(item => item!.Score)
            .FirstOrDefault();
    }

    private static CaravanOpportunity? Find(CaravanOpportunityScan scan, CaravanOpportunityKind kind) =>
        scan.Opportunities.FirstOrDefault(item => item.Kind == kind);

    private static (int X, int Y) LeadTarget(WorldConfig config, CaravanOpportunity opportunity)
    {
        var lead = opportunity.Kind switch
        {
            CaravanOpportunityKind.RainFront => 4f,
            CaravanOpportunityKind.SnowmeltRunoff => 3f,
            CaravanOpportunityKind.GreenGrowth => 2f,
            CaravanOpportunityKind.HarvesterFront => 3f,
            _ => 0f
        };
        if (lead <= 0f || opportunity.FlowMagnitude <= 1e-6f)
            return (opportunity.X, opportunity.Y);
        return (
            Math.Clamp((int)MathF.Round(opportunity.X + opportunity.FlowX / opportunity.FlowMagnitude * lead), 0, config.Width - 1),
            Math.Clamp((int)MathF.Round(opportunity.Y + opportunity.FlowY / opportunity.FlowMagnitude * lead), 0, config.Height - 1));
    }

    private void SelectFallbackTarget(
        WorldConfig worldConfig,
        WorldState state,
        CaravanOpportunityScan scan)
    {
        var fallbackFlow = goal == CaravanEcologyGoal.SecureWater
            ? scan.LocalFlows.First(item => item.Process == VectorProcess.DrainageDirection)
            : scan.LocalFlows.First(item => item.Process == VectorProcess.Wind);
        var directionX = fallbackFlow.Magnitude > 0.2f ? fallbackFlow.X / fallbackFlow.Magnitude : 1f;
        var directionY = fallbackFlow.Magnitude > 0.2f ? fallbackFlow.Y / fallbackFlow.Magnitude : 0f;
        var dayBand = simulatedDays / 12;
        if ((dayBand & 1) != 0)
        {
            (directionX, directionY) = (-directionY, directionX);
        }
        targetX = Math.Clamp((int)MathF.Round(x + directionX * config.TravelCellsPerDay * 2), 0, worldConfig.Width - 1);
        targetY = Math.Clamp((int)MathF.Round(y + directionY * config.TravelCellsPerDay * 2), 0, worldConfig.Height - 1);
        if (targetX == x && targetY == y)
        {
            var sign = (dayBand & 2) == 0 ? 1f : -1f;
            targetX = Math.Clamp((int)MathF.Round(x - directionY * sign * config.TravelCellsPerDay * 2), 0, worldConfig.Width - 1);
            targetY = Math.Clamp((int)MathF.Round(y + directionX * sign * config.TravelCellsPerDay * 2), 0, worldConfig.Height - 1);
        }
        if (state.FireIntensityFraction[targetY * worldConfig.Width + targetX] > 0.01f)
        {
            targetX = Math.Clamp(x - Math.Sign(targetX - x) * config.TravelCellsPerDay, 0, worldConfig.Width - 1);
            targetY = Math.Clamp(y - Math.Sign(targetY - y) * config.TravelCellsPerDay, 0, worldConfig.Height - 1);
        }
        targetOpportunity = null;
    }

    private List<(int X, int Y)> MoveToward(WorldConfig worldConfig)
    {
        var fromX = x;
        var fromY = y;
        var dx = targetX - x;
        var dy = targetY - y;
        var distance = MathF.Sqrt(dx * dx + dy * dy);
        if (distance <= 1e-6f) return [];
        var mobility = 0.42f + condition * 0.58f;
        var travel = Math.Min(distance, config.TravelCellsPerDay * mobility);
        x = Math.Clamp((int)MathF.Round(x + dx / distance * travel), 0, worldConfig.Width - 1);
        y = Math.Clamp((int)MathF.Round(y + dy / distance * travel), 0, worldConfig.Height - 1);
        var steps = Math.Max(1, (int)MathF.Ceiling(Distance(fromX, fromY, x, y)));
        var path = new List<(int X, int Y)>(steps);
        for (var step = 1; step <= steps; step++)
        {
            var trailX = Math.Clamp((int)MathF.Round(fromX + (x - fromX) * step / (float)steps), 0, worldConfig.Width - 1);
            var trailY = Math.Clamp((int)MathF.Round(fromY + (y - fromY) * step / (float)steps), 0, worldConfig.Height - 1);
            if (path.Count == 0 || path[^1] != (trailX, trailY)) path.Add((trailX, trailY));
        }
        return path;
    }

    private void AbsorbAtDestination(
        WorldConfig worldConfig,
        WorldState state,
        WaterBudget waterBudget,
        NitrogenBudget nitrogenBudget,
        WorldFauna fauna,
        WorldFluxState flux,
        int index,
        List<CaravanAction> actions)
    {
        var waterCapacity = Math.Max(0f, config.WaterCapacityMmCells - waterMmCells);
        var shouldRefillWater = goal == CaravanEcologyGoal.SecureWater
            || waterMmCells < config.DailyWaterUseMmCells * 12f;
        var activeSpring = flux.Value(SimulationFlux.GroundwaterDischarge, index) > 0.0005f;
        if (shouldRefillWater
            && waterCapacity > 1e-8f
            && (state.SurfaceWaterMm[index] > config.MinimumCollectableSurfaceWaterMm
                || activeSpring && state.SurfaceWaterMm[index] > 0.0001f))
        {
            var result = CaravanInterventionEngine.WithdrawSurfaceWater(
                state,
                waterBudget,
                flux,
                index,
                x,
                y,
                Math.Min(
                    config.MaximumDailyWaterWithdrawalMmCells * (0.35f + condition * 0.65f),
                    waterCapacity));
            waterMmCells += result.SurfaceWaterMm;
            cumulativeWaterWithdrawn += result.SurfaceWaterMm;
            AddAction(actions, CaravanActionVerb.Absorb, CaravanActionKind.WithdrawSurfaceWater,
                x, y, result.SurfaceWaterMm, "мм·ячейка", CaravanOpportunityKind.SurfaceWater);
        }

        waterCapacity = Math.Max(0f, config.WaterCapacityMmCells - waterMmCells);
        if (shouldRefillWater && waterCapacity > 1e-8f && state.SnowWaterEquivalentMm[index] > 0.05f)
        {
            var solarMelt = state.SolarRadiationWm2[index] > 110f || state.AirTemperatureC[index] > 1f;
            var fuelAvailable = dryBiomassGm2Cells > 0.01f;
            if (solarMelt || fuelAvailable)
            {
                var maximum = Math.Min(
                    config.MaximumDailySnowMeltMmCells * (0.35f + condition * 0.65f),
                    waterCapacity);
                if (!solarMelt) maximum = Math.Min(maximum, dryBiomassGm2Cells * 0.18f);
                var result = CaravanInterventionEngine.WithdrawSnow(
                    state, waterBudget, flux, index, x, y, maximum);
                if (!solarMelt)
                {
                    var biomassBeforeFuel = freshBiomassGm2Cells + dryBiomassGm2Cells;
                    var fuel = Math.Min(dryBiomassGm2Cells, result.SnowWaterEquivalentMm / 0.18f);
                    var fuelNitrogen = biomassBeforeFuel > 1e-8f
                        ? Math.Min(cargoNitrogenGm2Cells, cargoNitrogenGm2Cells * fuel / biomassBeforeFuel)
                        : 0f;
                    dryBiomassGm2Cells -= fuel;
                    cargoNitrogenGm2Cells -= fuelNitrogen;
                    residueNitrogenGm2Cells += fuelNitrogen;
                    cumulativeBiomassConsumed += fuel;
                    organicResidueGm2Cells += fuel * 0.12f;
                    AddAction(actions, CaravanActionVerb.Transform, CaravanActionKind.ConsumeBiomass,
                        x, y, fuel, "г/м²·ячейка", CaravanOpportunityKind.Snowpack);
                }
                waterMmCells += result.SnowWaterEquivalentMm;
                cumulativeSnowMelted += result.SnowWaterEquivalentMm;
                AddAction(actions, CaravanActionVerb.Absorb, CaravanActionKind.CollectSnow,
                    x, y, result.SnowWaterEquivalentMm, "мм SWE·ячейка", CaravanOpportunityKind.Snowpack);
                AddAction(actions, CaravanActionVerb.Transform, CaravanActionKind.MeltSnow,
                    x, y, result.SnowWaterEquivalentMm, "мм·ячейка", CaravanOpportunityKind.Snowpack);
            }
        }

        waterCapacity = Math.Max(0f, config.WaterCapacityMmCells - waterMmCells);
        var shouldCondense = waterMmCells < config.DailyWaterUseMmCells * 8f;
        if (shouldCondense
            && waterCapacity > 1e-8f
            && state.AirHumidityMm[index] > 1.5f
            && dryBiomassGm2Cells > 0.005f)
        {
            var humidityFactor = Math.Clamp((state.AirHumidityMm[index] - 1.5f) / 7f, 0f, 1f);
            var operatingFactor = 0.70f + condition * 0.30f;
            var maximum = Math.Min(
                waterCapacity,
                config.MaximumDailyAtmosphericCondensationMmCells
                * (0.45f + humidityFactor * 0.55f)
                * operatingFactor);
            maximum = Math.Min(
                maximum,
                dryBiomassGm2Cells / config.AtmosphericCondensationBiomassPerWater);
            var result = CaravanInterventionEngine.CondenseAtmosphericWater(
                state, waterBudget, flux, index, x, y, maximum);
            var fuel = Math.Min(
                dryBiomassGm2Cells,
                result.AtmosphericWaterMm * config.AtmosphericCondensationBiomassPerWater);
            var biomassBeforeFuel = freshBiomassGm2Cells + dryBiomassGm2Cells;
            var fuelNitrogen = biomassBeforeFuel > 1e-8f
                ? Math.Min(cargoNitrogenGm2Cells, cargoNitrogenGm2Cells * fuel / biomassBeforeFuel)
                : 0f;
            dryBiomassGm2Cells -= fuel;
            cargoNitrogenGm2Cells -= fuelNitrogen;
            organicResidueGm2Cells += fuel * 0.12f;
            residueNitrogenGm2Cells += fuelNitrogen;
            cumulativeBiomassConsumed += fuel;
            waterMmCells += result.AtmosphericWaterMm;
            cumulativeAtmosphericWaterCondensed += result.AtmosphericWaterMm;
            AddAction(actions, CaravanActionVerb.Absorb, CaravanActionKind.CondenseAtmosphericWater,
                x, y, result.AtmosphericWaterMm, "мм·ячейка");
            AddAction(actions, CaravanActionVerb.Transform, CaravanActionKind.ConsumeBiomass,
                x, y, fuel, "г/м²·ячейка");
        }

        var biomassCapacity = Math.Max(
            0f,
            config.BiomassCapacityGm2Cells - freshBiomassGm2Cells - dryBiomassGm2Cells);
        var biomassCargo = freshBiomassGm2Cells + dryBiomassGm2Cells;
        var usableBiomass = dryBiomassGm2Cells
            + freshBiomassGm2Cells * config.FreshBiomassEnergyFraction;
        var shouldHarvest = biomassCargo < config.BiomassCapacityGm2Cells * 0.8f
            && (goal is CaravanEcologyGoal.GatherBiomass or CaravanEcologyGoal.FollowGrowthFront
                || usableBiomass < config.DailyBiomassUseGm2Cells * 6f);
        if (shouldHarvest && biomassCapacity > 1e-8f)
        {
            var preferDry = targetOpportunity == CaravanOpportunityKind.DryStandingBiomass;
            var activeGrowth = flux.Value(SimulationFlux.PlantGrowth, index) > 0.004f
                || state.LiveBiomassGm2[index] > 34f;
            var harvestableDry = state.DryBiomassGm2[index] > 14f;
            var throughput = 0.40f + condition * 0.60f;
            var maximumLive = activeGrowth
                ? Math.Min(
                    preferDry
                        ? config.MaximumDailyLiveHarvestGm2Cells * 0.25f
                        : config.MaximumDailyLiveHarvestGm2Cells,
                    biomassCapacity) * throughput
                : 0f;
            var maximumDry = harvestableDry
                ? Math.Min(
                    config.MaximumDailyDryHarvestGm2Cells,
                    Math.Max(0f, biomassCapacity - maximumLive)) * throughput
                : 0f;
            var result = CaravanInterventionEngine.HarvestBiomass(
                state,
                nitrogenBudget,
                flux,
                index,
                x,
                y,
                maximumLive,
                maximumDry);
            freshBiomassGm2Cells += result.LiveBiomassGm2;
            dryBiomassGm2Cells += result.DryBiomassGm2;
            cargoNitrogenGm2Cells += result.NitrogenGm2;
            cumulativeLiveHarvested += result.LiveBiomassGm2;
            cumulativeDryHarvested += result.DryBiomassGm2;
            AddAction(actions, CaravanActionVerb.Absorb, CaravanActionKind.HarvestLiveBiomass,
                x, y, result.LiveBiomassGm2, "г/м²·ячейка", CaravanOpportunityKind.GreenGrowth);
            AddAction(actions, CaravanActionVerb.Absorb, CaravanActionKind.HarvestDryBiomass,
                x, y, result.DryBiomassGm2, "г/м²·ячейка", CaravanOpportunityKind.DryStandingBiomass);
        }

        var dustCapacity = Math.Max(0f, config.SedimentCapacityKgM2Cells - capturedSedimentKgM2Cells);
        if (dustCapacity > 1e-8f
            && state.DustGm2[index] > 0.025f
            && (targetOpportunity == CaravanOpportunityKind.DustFront || state.DustGm2[index] > 0.12f))
        {
            var result = CaravanInterventionEngine.CaptureDust(
                state, flux, index, x, y, Math.Min(0.045f, dustCapacity * 1000f));
            capturedSedimentKgM2Cells += result.SedimentKgM2;
            cumulativeDustCaptured += result.DustGm2;
            AddAction(actions, CaravanActionVerb.Absorb, CaravanActionKind.CaptureDust,
                x, y, result.DustGm2, "г/м²·ячейка", CaravanOpportunityKind.DustFront);
        }

        if (chitinKg < config.ChitinCapacityKg)
        {
            var collected = fauna.CollectMolt(x, y, 1.75f, Math.Min(18f, config.ChitinCapacityKg - chitinKg));
            chitinKg += collected;
            cumulativeChitinCollected += collected;
            AddAction(actions, CaravanActionVerb.Absorb, CaravanActionKind.CollectMolt,
                x, y, collected, "кг", CaravanOpportunityKind.HarvesterMolt);
        }
    }

    private void TransformAtDestination(
        WorldState state,
        int index,
        List<CaravanAction> actions)
    {
        var wind = MathF.Sqrt(state.WindXMs[index] * state.WindXMs[index] + state.WindYMs[index] * state.WindYMs[index]);
        var drying = state.SolarRadiationWm2[index] > 180f
            && state.AirHumidityMm[index] < 19f
            && state.PrecipitationMmPerHour[index] < 0.015f
            && wind > 0.7f;
        if (drying && freshBiomassGm2Cells > 0f)
        {
            var weatherFactor = Math.Clamp(
                state.SolarRadiationWm2[index] / 600f
                + wind / 18f
                + (19f - state.AirHumidityMm[index]) / 38f,
                0.12f,
                1f);
            var amount = Math.Min(freshBiomassGm2Cells, 0.62f * weatherFactor);
            freshBiomassGm2Cells -= amount;
            dryBiomassGm2Cells += amount;
            cumulativeBiomassCured += amount;
            AddAction(actions, CaravanActionVerb.Transform, CaravanActionKind.CureFreshBiomass,
                x, y, amount, "г/м²·ячейка", CaravanOpportunityKind.DryingWindow);
        }
        else if (freshBiomassGm2Cells > 0.01f && state.AirTemperatureC[index] > 2f)
        {
            var freshBefore = freshBiomassGm2Cells;
            var humidityFactor = Math.Clamp(state.AirHumidityMm[index] / 12f, 0.45f, 1.35f);
            var spoilage = Math.Min(
                freshBiomassGm2Cells,
                freshBiomassGm2Cells * config.FreshBiomassDailySpoilageFraction * humidityFactor);
            var spoiledNitrogen = freshBefore > 1e-8f
                ? Math.Min(cargoNitrogenGm2Cells, cargoNitrogenGm2Cells * spoilage / freshBefore)
                : 0f;
            freshBiomassGm2Cells -= spoilage;
            cargoNitrogenGm2Cells -= spoiledNitrogen;
            organicResidueGm2Cells += spoilage;
            residueNitrogenGm2Cells += spoiledNitrogen;
            AddAction(actions, CaravanActionVerb.Transform, CaravanActionKind.SpoilFreshBiomass,
                x, y, spoilage, "г/м²·ячейка");
        }

        if (chitinKg > 0f && simulatedDays > 0 && simulatedDays % 30 == 0)
        {
            var used = Math.Min(0.8f, chitinKg);
            chitinKg -= used;
            cumulativeChitinUsed += used;
            AddAction(actions, CaravanActionVerb.Transform, CaravanActionKind.UseChitinForRepair,
                x, y, used, "кг", CaravanOpportunityKind.HarvesterMolt);
        }
    }

    private void ReturnAtDestination(
        WorldState state,
        WaterBudget waterBudget,
        NitrogenBudget nitrogenBudget,
        WorldFluxState flux,
        int index,
        List<CaravanAction> actions)
    {
        var poorOrganic = state.SoilOrganicMatterGm2[index] < 760f
            || state.AvailableNitrogenGm2[index] < 3.8f;
        var recoverable = state.LiveBiomassGm2[index] < 28f
            && state.SeedBank[index] > 0.2f
            && state.FireIntensityFraction[index] < 0.02f;
        if ((goal == CaravanEcologyGoal.RestoreSoil || poorOrganic && organicResidueGm2Cells > 0.16f)
            && organicResidueGm2Cells > 0.015f)
        {
            var matter = Math.Min(0.22f, organicResidueGm2Cells);
            var nitrogen = organicResidueGm2Cells > 1e-8f
                ? Math.Min(residueNitrogenGm2Cells, residueNitrogenGm2Cells * matter / organicResidueGm2Cells)
                : 0f;
            var result = CaravanInterventionEngine.ReturnOrganicMatter(
                state, nitrogenBudget, flux, index, x, y, matter, nitrogen);
            organicResidueGm2Cells -= result.OrganicMatterGm2;
            residueNitrogenGm2Cells -= result.NitrogenGm2;
            cumulativeReturnedMatter += result.OrganicMatterGm2;
            cumulativeReturnedNitrogen += result.NitrogenGm2;
            AddAction(actions, CaravanActionVerb.Return, CaravanActionKind.ReturnOrganicMatter,
                x, y, result.OrganicMatterGm2, "г/м²·ячейка",
                poorOrganic ? CaravanOpportunityKind.OrganicPoorSoil : CaravanOpportunityKind.RecoverableSoil);
        }

        var deposition = flux.Value(SimulationFlux.DustDeposition, index) > 0.0001f
            || flux.Value(SimulationFlux.SedimentDeposition, index) > 0.0001f;
        if ((goal == CaravanEcologyGoal.ReturnMinerals
             || deposition && capturedSedimentKgM2Cells > config.SedimentCapacityKgM2Cells * 0.25f)
            && capturedSedimentKgM2Cells > 1e-7f)
        {
            var sediment = Math.Min(0.0025f, capturedSedimentKgM2Cells);
            var result = CaravanInterventionEngine.ReturnSediment(state, flux, index, x, y, sediment);
            capturedSedimentKgM2Cells -= result.SedimentKgM2;
            cumulativeSedimentReturned += result.SedimentKgM2;
            AddAction(actions, CaravanActionVerb.Return, CaravanActionKind.ReturnSediment,
                x, y, result.SedimentKgM2, "кг/м²·ячейка", CaravanOpportunityKind.DepositionZone);
        }

        if (goal == CaravanEcologyGoal.RestoreSoil
            && (recoverable || poorOrganic)
            && state.SeedBank[index] > 0.2f
            && waterMmCells > config.WaterCapacityMmCells * 0.8f
            && state.PrecipitationMmPerHour[index] < 0.001f)
        {
            var amount = Math.Min(0.0005f, waterMmCells - config.WaterCapacityMmCells * 0.8f);
            var result = CaravanInterventionEngine.ReturnSurfaceWater(
                state, waterBudget, flux, index, x, y, amount);
            waterMmCells -= result.SurfaceWaterMm;
            cumulativeWaterReturned += result.SurfaceWaterMm;
            AddAction(actions, CaravanActionVerb.Return, CaravanActionKind.ReturnSurfaceWater,
                x, y, result.SurfaceWaterMm, "мм·ячейка", CaravanOpportunityKind.RecoverableSoil);
        }
    }

    private void AddAction(
        List<CaravanAction> actions,
        CaravanActionVerb verb,
        CaravanActionKind kind,
        int actionX,
        int actionY,
        float amount,
        string unit,
        CaravanOpportunityKind? opportunity = null)
    {
        if (!RuntimeCompatibility.IsFinite(amount) || amount <= 1e-8f) return;
        actions.Add(new CaravanAction(verb, kind, actionX, actionY, amount, unit, opportunity));
        verbActions[(int)verb]++;
        actionCounts[(int)kind]++;
    }

    private static float Distance(float ax, float ay, float bx, float by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static void ValidateConfig(CaravanEcologyConfig value)
    {
        if (value.WaterCapacityMmCells <= 0f
            || value.BiomassCapacityGm2Cells <= 0f
            || value.SedimentCapacityKgM2Cells <= 0f
            || value.ChitinCapacityKg <= 0f
            || value.DailyWaterUseMmCells < 0f
            || value.DailyBiomassUseGm2Cells < 0f
            || value.MovementWaterUseMmCellsPerCell < 0f
            || value.MovementBiomassUseGm2CellsPerCell < 0f
            || value.MinimumCollectableSurfaceWaterMm < 0f
            || value.MaximumDailyWaterWithdrawalMmCells <= 0f
            || value.MaximumDailySnowMeltMmCells <= 0f
            || value.MaximumDailyAtmosphericCondensationMmCells <= 0f
            || value.AtmosphericCondensationBiomassPerWater <= 0f
            || value.MaximumDailyLiveHarvestGm2Cells <= 0f
            || value.MaximumDailyDryHarvestGm2Cells <= 0f
            || value.FreshBiomassEnergyFraction is <= 0f or > 1f
            || value.FreshBiomassDailySpoilageFraction is < 0f or > 1f
            || value.BaseScoutRadiusCells < 1
            || value.MaximumScoutRadiusCells < value.BaseScoutRadiusCells
            || value.TravelCellsPerDay < 1
            || value.InitialWaterMmCells < 0f
            || value.InitialWaterMmCells > value.WaterCapacityMmCells
            || value.InitialFreshBiomassGm2Cells < 0f
            || value.InitialDryBiomassGm2Cells < 0f
            || value.InitialFreshBiomassGm2Cells + value.InitialDryBiomassGm2Cells > value.BiomassCapacityGm2Cells
            || value.InitialOrganicNitrogenGm2Cells < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }
    }
}

public sealed partial class FiniteWorld
{
    /// <summary>
    /// Advances nature, fauna and one caravan day inside one diagnostic window.
    /// Set startObservationWindow on the first day of a sampled interval; later
    /// days then accumulate into the same flux and vector fields.
    /// </summary>
    public CaravanDailyImpact AdvanceDayWithCaravan(
        CaravanEcologyAgent caravan,
        bool startObservationWindow = true,
        CancellationToken cancellationToken = default)
    {
        RuntimeCompatibility.ThrowIfNull(caravan, nameof(caravan));
        lock (sync)
        {
            var priorPeriod = startObservationWindow ? 0d : fluxState.PeriodHours;
            if (startObservationWindow) fluxState.Begin(SampleValue);
            var advanced = 0d;
            CaravanDailyImpact? impact = null;
            try
            {
                advanced = AdvanceCore(12d, cancellationToken);
                impact = caravan.Step(
                    Config,
                    Clock,
                    state,
                    waterBudget,
                    nitrogenBudget,
                    fauna,
                    fluxState);
                advanced += AdvanceCore(12d, cancellationToken);
                return impact;
            }
            finally
            {
                fluxState.Complete(priorPeriod + advanced, SampleValue);
            }
        }
    }
}
