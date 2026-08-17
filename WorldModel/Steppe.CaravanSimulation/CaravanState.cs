namespace Steppe.CaravanSimulation;

internal sealed class CaravanOrganState
{
    public CaravanOrganState(CaravanOrganDefinition definition, float size)
    {
        Definition = definition;
        Size = size;
    }

    public CaravanOrganState(CaravanOrganDefinition definition, CaravanOrganSnapshot snapshot)
    {
        Definition = definition;
        Size = snapshot.Size;
        FunctionalFraction = snapshot.FunctionalFraction;
        Usage = snapshot.Usage;
        UsageEma = snapshot.UsageEma;
        GrowthPriority = snapshot.GrowthPriority;
        IdleDays = snapshot.IdleDays;
        ActiveHours = snapshot.ActiveHours;
    }

    public CaravanOrganDefinition Definition { get; }
    public float Size { get; private set; }
    public float FunctionalFraction { get; private set; } = 1f;
    public float Usage { get; private set; }
    public float UsageEma { get; private set; }
    public float GrowthPriority { get; set; }
    public float IdleDays { get; private set; }
    public double ActiveHours { get; private set; }

    public float FunctionalSize => Size * FunctionalFraction;
    public float StructuralMassKg => Size * Definition.StructuralKgPerSize;

    public void RecordUsage(float normalizedUsage, double hours)
    {
        Usage = Math.Clamp(normalizedUsage, 0f, 1.5f);
        if (Usage > 0.01f) ActiveHours += hours;
        var days = (float)(hours / 24d);
        var alpha = 1f - MathF.Exp(-days / 30f);
        UsageEma += (Usage - UsageEma) * alpha;
        IdleDays = Usage < 0.03f ? IdleDays + days : 0f;
    }

    public void ApplyMaintenance(float fulfillment, double hours)
    {
        var days = (float)(hours / 24d);
        var target = Math.Clamp(fulfillment, 0f, 1f);
        var rate = target >= FunctionalFraction ? 0.02f : 0.012f;
        FunctionalFraction = Math.Clamp(
            FunctionalFraction + (target - FunctionalFraction) * rate * days,
            0.05f,
            1f);
    }

    public float GrowByStructuralMass(float structuralKg)
    {
        if (structuralKg <= 0f || Definition.StructuralKgPerSize <= 0f) return 0f;
        var possibleSize = structuralKg / Definition.StructuralKgPerSize;
        var addedSize = Math.Min(possibleSize, Definition.MaximumSize - Size);
        Size += addedSize;
        return addedSize * Definition.StructuralKgPerSize;
    }

    public (float RecoveredKg, float LostKg) Atrophy(double hours)
    {
        if (IdleDays < 90f || GrowthPriority > 0.05f || Size <= Definition.MinimumSize) return (0f, 0f);
        var days = (float)(hours / 24d);
        var atrophiedSize = Math.Min(
            Size - Definition.MinimumSize,
            Size * 0.00055f * days);
        if (atrophiedSize <= 0f) return (0f, 0f);
        Size -= atrophiedSize;
        var mass = atrophiedSize * Definition.StructuralKgPerSize;
        var recovered = mass * Definition.AtrophyRecoveryFraction;
        return (recovered, mass - recovered);
    }

    public CaravanOrganSnapshot Capture() => new(
        Definition.Kind,
        Size,
        Definition.SizeUnit,
        FunctionalFraction,
        Usage,
        UsageEma,
        GrowthPriority,
        IdleDays,
        ActiveHours,
        StructuralMassKg);
}

internal sealed class CaravanState
{
    private readonly Dictionary<CaravanActivity, int> activitySteps =
        Enum.GetValues<CaravanActivity>().ToDictionary(item => item, _ => 0);
    private readonly Dictionary<CaravanRegime, double> regimeHours =
        Enum.GetValues<CaravanRegime>()
            .Where(item => item is not CaravanRegime.None)
            .ToDictionary(item => item, _ => 0d);

    public CaravanState(CaravanBlueprint blueprint, float xCells, float yCells)
    {
        Blueprint = blueprint;
        XCells = xCells;
        YCells = yCells;
        WaterLiters = blueprint.InitialWaterLiters;
        WetOrganicDryKg = blueprint.InitialWetOrganicDryKg;
        WetOrganicWaterLiters = blueprint.InitialWetOrganicWaterLiters;
        DryOrganicKg = blueprint.InitialDryOrganicKg;
        OrganicNitrogenKg = blueprint.InitialOrganicNitrogenKg;
        StructuralReserveKg = blueprint.InitialStructuralReserveKg;
        StoredElectricityKwh = blueprint.InitialElectricityKwh;
        StoredHeatKwh = blueprint.InitialHeatKwh;
        Organs = blueprint.OrganSizes.ToDictionary(
            item => item.Key,
            item => new CaravanOrganState(CaravanOrganCatalog.Get(item.Key), item.Value));
    }

    public CaravanState(CaravanBlueprint blueprint, CaravanStateSnapshot snapshot)
    {
        Blueprint = blueprint;
        XCells = snapshot.XCells;
        YCells = snapshot.YCells;
        SimulatedHours = snapshot.SimulatedHours;
        DistanceKilometers = snapshot.DistanceKilometers;
        WaterLiters = snapshot.WaterLiters;
        SnowWaterLiters = snapshot.SnowWaterLiters;
        WetOrganicDryKg = snapshot.WetOrganicDryKg;
        WetOrganicWaterLiters = snapshot.WetOrganicWaterLiters;
        DryOrganicKg = snapshot.DryOrganicKg;
        OrganicNitrogenKg = snapshot.OrganicNitrogenKg;
        StructuralReserveKg = snapshot.StructuralReserveKg;
        StoredElectricityKwh = snapshot.StoredElectricityKwh;
        StoredHeatKwh = snapshot.StoredHeatKwh;
        BodyTemperatureC = snapshot.BodyTemperatureC;
        CapturedDustKg = snapshot.CapturedDustKg;
        PendingOrganicResidueKg = snapshot.PendingOrganicResidueKg;
        PendingNitrogenKg = snapshot.PendingNitrogenKg;
        PendingWaterVaporLiters = snapshot.PendingWaterVaporLiters;
        PendingWasteHeatKwh = snapshot.PendingWasteHeatKwh;
        OperatingMode = snapshot.OperatingMode;
        HibernationReason = snapshot.HibernationReason;
        CurrentHibernationHours = snapshot.CurrentHibernationHours;
        CumulativeHibernationHours = snapshot.CumulativeHibernationHours;
        HibernationEpisodes = snapshot.HibernationEpisodes;
        WaterDeficitHours = snapshot.WaterDeficitHours;
        OrganicDeficitHours = snapshot.OrganicDeficitHours;
        ThermalDeficitHours = snapshot.ThermalDeficitHours;
        StructuralOverloadHours = snapshot.StructuralOverloadHours;
        CumulativeSurfaceWaterLiters = snapshot.CumulativeSurfaceWaterLiters;
        CumulativeSnowWaterLiters = snapshot.CumulativeSnowWaterLiters;
        CumulativePlantTissueWaterLiters = snapshot.CumulativePlantTissueWaterLiters;
        CumulativeLiveBiomassKg = snapshot.CumulativeLiveBiomassKg;
        CumulativeDryBiomassKg = snapshot.CumulativeDryBiomassKg;
        CumulativeChitinKg = snapshot.CumulativeChitinKg;
        CumulativeSolarElectricityKwh = snapshot.CumulativeSolarElectricityKwh;
        CumulativeFurnaceElectricityKwh = snapshot.CumulativeFurnaceElectricityKwh;
        CumulativeSailMechanicalKwh = snapshot.CumulativeSailMechanicalKwh;
        CumulativeMotorMechanicalKwh = snapshot.CumulativeMotorMechanicalKwh;
        CumulativeOrganicMatterReturnedKg = snapshot.CumulativeOrganicMatterReturnedKg;
        CumulativeWaterVaporReturnedLiters = snapshot.CumulativeWaterVaporReturnedLiters;
        CumulativeWasteHeatReturnedKwh = snapshot.CumulativeWasteHeatReturnedKwh;
        Organs = snapshot.Organs.ToDictionary(
            item => item.Key,
            item => new CaravanOrganState(CaravanOrganCatalog.Get(item.Key), item.Value));
        foreach (var (activity, count) in snapshot.ActivitySteps) activitySteps[activity] = count;
        foreach (var (regime, hours) in snapshot.RegimeHours) regimeHours[regime] = hours;
    }

    public CaravanBlueprint Blueprint { get; }
    public Dictionary<CaravanOrganKind, CaravanOrganState> Organs { get; }
    public float XCells { get; set; }
    public float YCells { get; set; }
    public double SimulatedHours { get; set; }
    public float DistanceKilometers { get; set; }
    public float WaterLiters { get; set; }
    public float SnowWaterLiters { get; set; }
    public float WetOrganicDryKg { get; set; }
    public float WetOrganicWaterLiters { get; set; }
    public float DryOrganicKg { get; set; }
    public float OrganicNitrogenKg { get; set; }
    public float StructuralReserveKg { get; set; }
    public float StoredElectricityKwh { get; set; }
    public float StoredHeatKwh { get; set; }
    public float BodyTemperatureC { get; set; } = 18f;
    public float CapturedDustKg { get; set; }
    public float PendingOrganicResidueKg { get; set; }
    public float PendingNitrogenKg { get; set; }
    public float PendingWaterVaporLiters { get; set; }
    public float PendingWasteHeatKwh { get; set; }
    public float PendingSurfaceWaterReturnLiters { get; set; }
    public float PendingSedimentReturnKg { get; set; }
    public CaravanOperatingMode OperatingMode { get; set; }
    public CaravanHibernationReason HibernationReason { get; set; }
    public float CurrentHibernationHours { get; set; }
    public float CumulativeHibernationHours { get; set; }
    public int HibernationEpisodes { get; set; }
    public float WaterDeficitHours { get; set; }
    public float OrganicDeficitHours { get; set; }
    public float ThermalDeficitHours { get; set; }
    public float StructuralOverloadHours { get; set; }
    public float CumulativeSurfaceWaterLiters { get; set; }
    public float CumulativeSnowWaterLiters { get; set; }
    public float CumulativePlantTissueWaterLiters { get; set; }
    public float CumulativeLiveBiomassKg { get; set; }
    public float CumulativeDryBiomassKg { get; set; }
    public float CumulativeChitinKg { get; set; }
    public float CumulativeSolarElectricityKwh { get; set; }
    public float CumulativeFurnaceElectricityKwh { get; set; }
    public float CumulativeSailMechanicalKwh { get; set; }
    public float CumulativeMotorMechanicalKwh { get; set; }
    public float CumulativeOrganicMatterReturnedKg { get; set; }
    public float CumulativeWaterVaporReturnedLiters { get; set; }
    public float CumulativeWasteHeatReturnedKwh { get; set; }

    public bool IsHibernating => OperatingMode == CaravanOperatingMode.Hibernating;

    public float TotalStructuralMassKg => StructuralReserveKg + Organs.Values.Sum(item => item.StructuralMassKg);

    public float TotalMassKg => TotalStructuralMassKg
        + WaterLiters
        + SnowWaterLiters
        + WetOrganicWaterLiters
        + WetOrganicDryKg
        + DryOrganicKg
        + CapturedDustKg;

    public float OrganSize(CaravanOrganKind kind) => Organs[kind].FunctionalSize;

    public float WaterCapacityLiters => OrganSize(CaravanOrganKind.WaterReservoir);
    public float OrganicCapacityKg => OrganSize(CaravanOrganKind.OrganicStorage);
    public float BatteryCapacityKwh => OrganSize(CaravanOrganKind.Battery);
    public float FrameCapacityKg => OrganSize(CaravanOrganKind.Frame);
    public float HeatCapacityKwh => Math.Max(4f, OrganSize(CaravanOrganKind.ThermalOrgan) * 1.5f);

    public void RecordActivity(CaravanActivity activity) => activitySteps[activity]++;

    public void RecordRegime(CaravanRegime regime, double hours)
    {
        foreach (var flag in regimeHours.Keys)
        {
            if ((regime & flag) != 0) regimeHours[flag] += hours;
        }
    }

    public CaravanStateSnapshot Capture() => new(
        Blueprint.Name,
        Blueprint.Morphology,
        XCells,
        YCells,
        SimulatedHours,
        DistanceKilometers,
        TotalMassKg,
        WaterLiters,
        SnowWaterLiters,
        WetOrganicDryKg,
        WetOrganicWaterLiters,
        DryOrganicKg,
        OrganicNitrogenKg,
        StructuralReserveKg,
        StoredElectricityKwh,
        StoredHeatKwh,
        BodyTemperatureC,
        CapturedDustKg,
        PendingOrganicResidueKg,
        PendingNitrogenKg,
        PendingWaterVaporLiters,
        PendingWasteHeatKwh,
        OperatingMode,
        HibernationReason,
        CurrentHibernationHours,
        CumulativeHibernationHours,
        HibernationEpisodes,
        WaterDeficitHours,
        OrganicDeficitHours,
        ThermalDeficitHours,
        StructuralOverloadHours,
        CumulativeSurfaceWaterLiters,
        CumulativeSnowWaterLiters,
        CumulativePlantTissueWaterLiters,
        CumulativeLiveBiomassKg,
        CumulativeDryBiomassKg,
        CumulativeChitinKg,
        CumulativeSolarElectricityKwh,
        CumulativeFurnaceElectricityKwh,
        CumulativeSailMechanicalKwh,
        CumulativeMotorMechanicalKwh,
        CumulativeOrganicMatterReturnedKg,
        CumulativeWaterVaporReturnedLiters,
        CumulativeWasteHeatReturnedKwh,
        Organs.ToDictionary(item => item.Key, item => item.Value.Capture()),
        new Dictionary<CaravanActivity, int>(activitySteps),
        new Dictionary<CaravanRegime, double>(regimeHours));
}
