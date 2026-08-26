namespace Steppe.CaravanSimulation;

public static class CaravanOrganCatalog
{
    private static readonly CaravanOrganDefinition[] Definitions =
    [
        D(CaravanOrganKind.Sail, "Парус", "м²", 5f, 2000f, 0.9f, 0.001f, 0.00005f, 0f, 0.88f),
        D(CaravanOrganKind.SolarLeaf, "Солнечный лист", "м²", 5f, 1500f, 5f, 0.002f, 0.00012f, 0.0002f, 0.82f),
        D(CaravanOrganKind.WaterIntake, "Водозабор", "л/ч", 10f, 10_000f, 0.12f, 0.0004f, 0.00015f, 0.0004f, 0.86f),
        D(CaravanOrganKind.SnowCollector, "Снегосборник", "м²", 5f, 1000f, 2f, 0.001f, 0.0001f, 0f, 0.88f),
        D(CaravanOrganKind.WaterReservoir, "Резервуар", "л", 200f, 50_000f, 0.08f, 0.00004f, 0.00001f, 0f, 0.9f),
        D(CaravanOrganKind.LiveBiomassHarvester, "Жатка живой массы", "кг сух. в-ва/ч", 1f, 400f, 30f, 0.01f, 0.004f, 0.02f, 0.84f),
        D(CaravanOrganKind.DryBiomassCollector, "Сборщик сухой массы", "кг/ч", 1f, 400f, 20f, 0.006f, 0.003f, 0.012f, 0.86f),
        D(CaravanOrganKind.Dryer, "Сушильный орган", "кг сух. в-ва/ч", 1f, 300f, 22f, 0.008f, 0.003f, 0.018f, 0.84f),
        D(CaravanOrganKind.OrganicStorage, "Органическое хранилище", "кг", 100f, 30_000f, 0.12f, 0.00003f, 0.00001f, 0f, 0.9f),
        D(CaravanOrganKind.Furnace, "Биореактор / топка", "кВт", 2f, 500f, 12f, 0.006f, 0.003f, 0.004f, 0.82f),
        D(CaravanOrganKind.Battery, "Батарея", "кВт·ч", 5f, 5000f, 8f, 0.003f, 0.0005f, 0.0004f, 0.8f),
        D(CaravanOrganKind.ElectricMotor, "Электромотор", "кВт", 5f, 1500f, 6f, 0.004f, 0.001f, 0.001f, 0.82f),
        D(CaravanOrganKind.ThermalOrgan, "Тепловой орган", "кВт", 2f, 500f, 10f, 0.006f, 0.002f, 0.004f, 0.84f),
        D(CaravanOrganKind.Radiator, "Охлаждающий орган", "кВт", 2f, 500f, 8f, 0.004f, 0.001f, 0.001f, 0.86f),
        D(CaravanOrganKind.GrowthTissue, "Ростовая ткань", "кг структуры/сут", 0.2f, 200f, 8f, 0.8f, 0.12f, 0.3f, 0.78f),
        D(CaravanOrganKind.Frame, "Каркас", "кг несущей способности", 3000f, 100_000f, 0.18f, 0.0005f, 0.00002f, 0f, 0.92f)
    ];

    private static readonly IReadOnlyDictionary<CaravanOrganKind, CaravanOrganDefinition> ByKind =
        Definitions.ToDictionary(item => item.Kind);

    public static IReadOnlyCollection<CaravanOrganDefinition> All => Definitions;

    public static CaravanOrganDefinition Get(CaravanOrganKind kind) => ByKind[kind];

    private static CaravanOrganDefinition D(
        CaravanOrganKind kind,
        string name,
        string unit,
        float minimum,
        float maximum,
        float structuralKgPerSize,
        float waterMaintenance,
        float organicMaintenance,
        float electricityMaintenance,
        float atrophyRecovery) =>
        new(
            kind,
            name,
            unit,
            minimum,
            maximum,
            structuralKgPerSize,
            waterMaintenance,
            organicMaintenance,
            electricityMaintenance,
            atrophyRecovery);
}

public sealed record CaravanBlueprint(
    string Name,
    CaravanMorphology Morphology,
    float InitialWaterLiters,
    float InitialWetOrganicDryKg,
    float InitialWetOrganicWaterLiters,
    float InitialDryOrganicKg,
    float InitialOrganicNitrogenKg,
    float InitialStructuralReserveKg,
    float InitialElectricityKwh,
    float InitialHeatKwh,
    IReadOnlyDictionary<CaravanOrganKind, float> OrganSizes)
{
    public static CaravanBlueprint Create(CaravanMorphology morphology) => morphology switch
    {
        CaravanMorphology.Seed => Build(
            "seed",
            morphology,
            1400f, 100f, 150f, 420f, 8f, 650f, 32f, 8f,
            20f, 20f, 120f, 20f, 2500f, 5f, 5f, 5f, 1000f, 12f, 50f, 40f, 10f, 12f, 1f, 12_000f),
        CaravanMorphology.SailNomad => Build(
            "sail-nomad",
            morphology,
            1500f, 80f, 120f, 300f, 6f, 520f, 25f, 6f,
            420f, 25f, 90f, 80f, 2300f, 3f, 7f, 4f, 800f, 8f, 45f, 30f, 8f, 18f, 0.8f, 12_000f),
        CaravanMorphology.SolarElectric => Build(
            "solar-electric",
            morphology,
            2100f, 90f, 140f, 360f, 7f, 760f, 250f, 10f,
            45f, 260f, 180f, 40f, 3600f, 5f, 6f, 10f, 1200f, 14f, 520f, 210f, 35f, 55f, 1.2f, 22_000f),
        CaravanMorphology.BiomassHeavy => Build(
            "biomass-heavy",
            morphology,
            4200f, 550f, 900f, 2600f, 42f, 1100f, 90f, 35f,
            60f, 55f, 420f, 110f, 7000f, 75f, 90f, 70f, 5500f, 180f, 180f, 140f, 130f, 120f, 2.5f, 45_000f),
        CaravanMorphology.Balanced => Build(
            "balanced",
            morphology,
            2800f, 220f, 360f, 1050f, 18f, 900f, 180f, 18f,
            160f, 135f, 280f, 75f, 4800f, 28f, 34f, 32f, 2600f, 75f, 280f, 130f, 65f, 80f, 1.8f, 32_000f),
        _ => throw new ArgumentOutOfRangeException(nameof(morphology))
    };

    private static CaravanBlueprint Build(
        string name,
        CaravanMorphology morphology,
        float water,
        float wetDry,
        float wetWater,
        float dry,
        float nitrogen,
        float structure,
        float electricity,
        float heat,
        params float[] sizes)
    {
        var kinds = RuntimeCompatibility.GetEnumValues<CaravanOrganKind>();
        if (sizes.Length != kinds.Length)
        {
            throw new InvalidOperationException("Every caravan blueprint must specify every organ.");
        }

        var organs = kinds.ToDictionary(kind => kind, kind => sizes[(int)kind]);
        foreach (var (kind, size) in organs)
        {
            var definition = CaravanOrganCatalog.Get(kind);
            if (size < definition.MinimumSize || size > definition.MaximumSize)
            {
                throw new InvalidOperationException($"{name}: {kind} size {size} is outside its physical range.");
            }
        }

        return new CaravanBlueprint(
            name,
            morphology,
            water,
            wetDry,
            wetWater,
            dry,
            nitrogen,
            structure,
            electricity,
            heat,
            organs);
    }
}
