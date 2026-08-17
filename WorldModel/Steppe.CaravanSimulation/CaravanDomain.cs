using Steppe.Simulation;

namespace Steppe.CaravanSimulation;

public enum CaravanCircuit
{
    Electricity,
    Water,
    Organic,
    Structural,
    Heat
}

public enum CaravanOrganKind
{
    Sail,
    SolarLeaf,
    WaterIntake,
    SnowCollector,
    WaterReservoir,
    LiveBiomassHarvester,
    DryBiomassCollector,
    Dryer,
    OrganicStorage,
    Furnace,
    Battery,
    ElectricMotor,
    ThermalOrgan,
    Radiator,
    GrowthTissue,
    Frame
}

public enum CaravanActivity
{
    Rest,
    Hibernate,
    Travel,
    CollectWater,
    CollectSnow,
    HarvestLiveBiomass,
    HarvestDryBiomass,
    DryOrganicMatter,
    CollectChitin,
    ReturnResidues,
    Maintain,
    EscapeWildfire
}

public enum CaravanMorphology
{
    Seed,
    SailNomad,
    SolarElectric,
    BiomassHeavy,
    Balanced
}

public enum CaravanPolicyKind
{
    StayPut,
    NearestWater,
    FollowBiomass,
    FollowSun,
    FollowWind,
    BalancedNomad
}

public enum CaravanOperatingMode
{
    Active,
    Hibernating
}

public enum CaravanHibernationReason
{
    None,
    WaterShortage,
    OrganicShortage,
    ThermalProtection,
    StructuralOverload
}

[Flags]
public enum CaravanRegime
{
    None = 0,
    WaterRich = 1 << 0,
    BiomassRich = 1 << 1,
    SolarRich = 1 << 2,
    WindRich = 1 << 3,
    ColdOrSnow = 1 << 4,
    DustStressed = 1 << 5,
    Flooded = 1 << 6,
    PostFire = 1 << 7
}

public sealed record CaravanOrganDefinition(
    CaravanOrganKind Kind,
    string Name,
    string SizeUnit,
    float MinimumSize,
    float MaximumSize,
    float StructuralKgPerSize,
    float MaintenanceWaterLitersPerSizeDay,
    float MaintenanceOrganicKgPerSizeDay,
    float MaintenanceElectricityKwhPerSizeDay,
    float AtrophyRecoveryFraction);

public sealed record CaravanOrganSnapshot(
    CaravanOrganKind Kind,
    float Size,
    string SizeUnit,
    float FunctionalFraction,
    float Usage,
    float UsageEma,
    float GrowthPriority,
    float IdleDays,
    double ActiveHours,
    float StructuralMassKg);

public sealed record CaravanStateSnapshot(
    string Blueprint,
    CaravanMorphology Morphology,
    float XCells,
    float YCells,
    double SimulatedHours,
    float DistanceKilometers,
    float TotalMassKg,
    float WaterLiters,
    float SnowWaterLiters,
    float WetOrganicDryKg,
    float WetOrganicWaterLiters,
    float DryOrganicKg,
    float OrganicNitrogenKg,
    float StructuralReserveKg,
    float StoredElectricityKwh,
    float StoredHeatKwh,
    float BodyTemperatureC,
    float CapturedDustKg,
    float PendingOrganicResidueKg,
    float PendingNitrogenKg,
    float PendingWaterVaporLiters,
    float PendingWasteHeatKwh,
    CaravanOperatingMode OperatingMode,
    CaravanHibernationReason HibernationReason,
    float CurrentHibernationHours,
    float CumulativeHibernationHours,
    int HibernationEpisodes,
    float WaterDeficitHours,
    float OrganicDeficitHours,
    float ThermalDeficitHours,
    float StructuralOverloadHours,
    float CumulativeSurfaceWaterLiters,
    float CumulativeSnowWaterLiters,
    float CumulativePlantTissueWaterLiters,
    float CumulativeLiveBiomassKg,
    float CumulativeDryBiomassKg,
    float CumulativeChitinKg,
    float CumulativeSolarElectricityKwh,
    float CumulativeFurnaceElectricityKwh,
    float CumulativeSailMechanicalKwh,
    float CumulativeMotorMechanicalKwh,
    float CumulativeOrganicMatterReturnedKg,
    float CumulativeWaterVaporReturnedLiters,
    float CumulativeWasteHeatReturnedKwh,
    IReadOnlyDictionary<CaravanOrganKind, CaravanOrganSnapshot> Organs,
    IReadOnlyDictionary<CaravanActivity, int> ActivitySteps,
    IReadOnlyDictionary<CaravanRegime, double> RegimeHours);

public sealed record CaravanObservation(
    int Year,
    int DayOfYear,
    double HourOfDay,
    float XCells,
    float YCells,
    int WorldWidthCells,
    int WorldHeightCells,
    float CellSizeMeters,
    CellSnapshot Cell,
    CaravanOpportunityScan Opportunities,
    CaravanRegime Regime);

public sealed record CaravanDecision(
    CaravanActivity Activity,
    float TargetXCells,
    float TargetYCells,
    IReadOnlyDictionary<CaravanOrganKind, float> GrowthPriorities,
    string Reason);

public sealed record CaravanActionRecord(
    string Action,
    float Amount,
    string Unit,
    CaravanOrganKind? Organ = null);

public sealed record CaravanCircuitBalance(
    CaravanCircuit Circuit,
    float Start,
    float ExternalInput,
    float Generated,
    float Consumed,
    float ExternalOutput,
    float Lost,
    float End,
    float Error);

public sealed record CaravanStepLedger(
    CaravanCircuitBalance Electricity,
    CaravanCircuitBalance Water,
    CaravanCircuitBalance Organic,
    CaravanCircuitBalance Structural,
    CaravanCircuitBalance Heat)
{
    public float MaximumAbsoluteError => new[]
    {
        Math.Abs(Electricity.Error),
        Math.Abs(Water.Error),
        Math.Abs(Organic.Error),
        Math.Abs(Structural.Error),
        Math.Abs(Heat.Error)
    }.Max();
}

public sealed record CaravanTrailCell(int X, int Y, float DistanceKilometers);

public sealed record CaravanStepResult(
    CaravanObservation Observation,
    CaravanDecision Decision,
    double Hours,
    float DistanceKilometers,
    float WaterFulfillment,
    float OrganicFulfillment,
    float ThermalFulfillment,
    CaravanPhysicalExchangeResult Intake,
    CaravanPhysicalExchangeResult? Return,
    CaravanStepLedger Ledger,
    CaravanActionRecord[] Actions,
    CaravanTrailCell[] Trail,
    CaravanStateSnapshot Snapshot);
