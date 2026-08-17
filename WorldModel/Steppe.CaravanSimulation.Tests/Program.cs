using Steppe.CaravanSimulation;
using Steppe.Simulation;
using System.Text.Json;
using System.Text.Json.Serialization;

var tests = new (string Name, Action Run)[]
{
    ("blueprints contain every bounded organ", BlueprintsContainEveryBoundedOrgan),
    ("physical world exchange preserves units and ledgers", PhysicalWorldExchangePreservesUnitsAndLedgers),
    ("coupled caravan closes all five internal circuits", CoupledCaravanClosesAllFiveInternalCircuits),
    ("coupled caravan remains deterministic", CoupledCaravanRemainsDeterministic),
    ("caravan checkpoint preserves morphology and circulation state", CaravanCheckpointPreservesState),
    ("policies produce distinct mobility", PoliciesProduceDistinctMobility),
    ("surplus grows used organs and idleness atrophies unused organs", GrowthAndAtrophyChangeMorphology),
    ("resource exhaustion hibernates without death or work", ResourceExhaustionHibernatesWithoutDeathOrWork)
};

var failures = 0;
foreach (var (name, run) in tests)
{
    try
    {
        run();
        Console.WriteLine($"PASS  {name}");
    }
    catch (Exception exception)
    {
        failures++;
        Console.Error.WriteLine($"FAIL  {name}: {exception.Message}");
    }
}

Console.WriteLine($"{tests.Length - failures}/{tests.Length} tests passed");
return failures == 0 ? 0 : 1;

static void BlueprintsContainEveryBoundedOrgan()
{
    foreach (var morphology in Enum.GetValues<CaravanMorphology>())
    {
        var blueprint = CaravanBlueprint.Create(morphology);
        Assert(blueprint.OrganSizes.Count == Enum.GetValues<CaravanOrganKind>().Length,
            $"{morphology} omits an organ");
        foreach (var (kind, size) in blueprint.OrganSizes)
        {
            var definition = CaravanOrganCatalog.Get(kind);
            Assert(size >= definition.MinimumSize && size <= definition.MaximumSize,
                $"{morphology}/{kind} is outside its physical range");
        }
    }
}

static void PhysicalWorldExchangePreservesUnitsAndLedgers()
{
    var world = CreateWorld(16, 123);
    const int x = 8;
    const int y = 8;
    world.AddSurfaceWater(x, y, 2f);
    var before = world.SampleCell(x, y);
    var result = world.AdvanceWithCaravan(
        0d,
        access => access.Exchange(new CaravanPhysicalExchangeRequest
        {
            X = x,
            Y = y,
            SurfaceWaterWithdrawalLiters = 1000f,
            LiveBiomassHarvestKg = 25f,
            AtmosphericWaterReturnLiters = 80f,
            OrganicMatterReturnKg = 2f,
            OrganicNitrogenReturnKg = 0.04f,
            CompactionDelta = 0.01f,
            TrailVegetationDamageKg = 3f,
            DustLiftKg = 0.2f,
            WasteHeatKwh = 4f
        }));
    var after = world.SampleCell(x, y);
    AssertNear(result.SurfaceWaterWithdrawnLiters, 1000f, 0.02f, "water conversion changed liters");
    Assert(after.SurfaceWaterMm < before.SurfaceWaterMm, "surface water did not leave the cell");
    Assert(result.LiveBiomassHarvestedKg > 0f, "physical live biomass request harvested nothing");
    Assert(result.PlantTissueWaterWithdrawnLiters > 0f, "live harvest did not carry tissue water");
    Assert(result.OrganicMatterReturnedKg > 1.99f, "organic return lost kilograms at the boundary");
    Assert(after.SoilCompactionFraction > before.SoilCompactionFraction, "physical trail did not compact soil");
    Assert(world.CaptureFlux(SimulationFlux.CaravanSurfaceWaterWithdrawal).Values.Any(value => value > 0f),
        "water withdrawal is absent from the world flux window");
    Assert(world.CaptureFlux(SimulationFlux.CaravanAtmosphericWaterReturn).Values.Any(value => value > 0f),
        "water vapor return is absent from the world flux window");
    var summary = world.GetSummary();
    Assert(Math.Abs(summary.WaterBudget.RelativeError) < 1e-4,
        $"physical exchange violates world water ledger ({summary.WaterBudget.RelativeError})");
    Assert(Math.Abs(summary.NitrogenBudget.RelativeError) < 1e-4,
        $"physical exchange violates world nitrogen ledger ({summary.NitrogenBudget.RelativeError})");
}

static void CoupledCaravanClosesAllFiveInternalCircuits()
{
    var coupled = CreateCoupled(CaravanMorphology.Balanced, CaravanPolicyKind.BalancedNomad, 24, 321);
    var maximumError = 0f;
    var actions = 0;
    for (var step = 0; step < 30 * 8; step++)
    {
        var result = coupled.Advance();
        maximumError = Math.Max(maximumError, result.Ledger.MaximumAbsoluteError);
        actions += result.Actions.Length;
    }
    var snapshot = coupled.CaptureCaravan();
    Assert(maximumError < 0.02f, $"an internal circuit leaks by {maximumError}");
    Assert(actions > 100, "coupled caravan did not exercise its organs");
    Assert(snapshot.CumulativeSolarElectricityKwh > 0f, "solar circuit never generated electricity");
    Assert(snapshot.CumulativeOrganicMatterReturnedKg > 0f, "organic circuit never returned residue");
    Assert(snapshot.CumulativeWaterVaporReturnedLiters > 0f, "water circuit never returned vapor");
}

static void CoupledCaravanRemainsDeterministic()
{
    var first = CreateCoupled(CaravanMorphology.Balanced, CaravanPolicyKind.BalancedNomad, 20, 733);
    var second = CreateCoupled(CaravanMorphology.Balanced, CaravanPolicyKind.BalancedNomad, 20, 733);
    for (var step = 0; step < 15 * 8; step++)
    {
        first.Advance();
        second.Advance();
    }
    var a = first.CaptureCaravan();
    var b = second.CaptureCaravan();
    AssertNear(a.XCells, b.XCells, 1e-5f, "deterministic route diverged on X");
    AssertNear(a.YCells, b.YCells, 1e-5f, "deterministic route diverged on Y");
    AssertNear(a.WaterLiters, b.WaterLiters, 1e-4f, "deterministic water state diverged");
    AssertNear(a.DryOrganicKg, b.DryOrganicKg, 1e-4f, "deterministic organic state diverged");
    AssertNear(a.TotalMassKg, b.TotalMassKg, 1e-3f, "deterministic morphology diverged");
}

static void PoliciesProduceDistinctMobility()
{
    var stationary = CreateCoupled(CaravanMorphology.SailNomad, CaravanPolicyKind.StayPut, 20, 91);
    var wind = CreateCoupled(CaravanMorphology.SailNomad, CaravanPolicyKind.FollowWind, 20, 91);
    for (var step = 0; step < 10 * 8; step++)
    {
        stationary.Advance();
        wind.Advance();
    }
    var stayed = stationary.CaptureCaravan();
    var moved = wind.CaptureCaravan();
    Assert(stayed.DistanceKilometers < 0.01f, "StayPut control unexpectedly migrated");
    Assert(moved.DistanceKilometers > 2f, "FollowWind did not produce a distinct route");
}

static void CaravanCheckpointPreservesState()
{
    var original = new CaravanSimulation(
        CaravanBlueprint.Create(CaravanMorphology.Balanced),
        10f,
        10f);
    var originalWorld = CreateWorld(20, 405);
    var originalCoupled = new CoupledCaravanSimulation(
        originalWorld,
        original,
        CaravanPolicyCatalog.Create(CaravanPolicyKind.BalancedNomad),
        scoutRadiusCells: 10);
    for (var step = 0; step < 8; step++) originalCoupled.Advance();
    var expected = original.Capture();
    using var stream = new MemoryStream();
    CaravanPersistence.Save(original, stream);
    stream.Position = 0;
    var restored = CaravanPersistence.Load(stream).Capture();
    AssertNear(restored.XCells, expected.XCells, 1e-5f, "checkpoint lost position");
    AssertNear(restored.WaterLiters, expected.WaterLiters, 1e-4f, "checkpoint lost water");
    AssertNear(restored.TotalMassKg, expected.TotalMassKg, 1e-3f, "checkpoint lost physical mass");
    AssertNear(
        restored.Organs[CaravanOrganKind.SolarLeaf].Size,
        expected.Organs[CaravanOrganKind.SolarLeaf].Size,
        1e-5f,
        "checkpoint lost organ morphology");
}

static void GrowthAndAtrophyChangeMorphology()
{
    var baseBlueprint = CaravanBlueprint.Create(CaravanMorphology.SolarElectric);
    var sizes = new Dictionary<CaravanOrganKind, float>(baseBlueprint.OrganSizes)
    {
        [CaravanOrganKind.WaterReservoir] = 50_000f,
        [CaravanOrganKind.OrganicStorage] = 30_000f,
        [CaravanOrganKind.Frame] = 100_000f,
        [CaravanOrganKind.GrowthTissue] = 8f,
        [CaravanOrganKind.Furnace] = 100f,
        [CaravanOrganKind.Battery] = 1000f,
        [CaravanOrganKind.ThermalOrgan] = 100f,
        [CaravanOrganKind.Radiator] = 100f
    };
    var blueprint = baseBlueprint with
    {
        Name = "long-lived-growth-test",
        InitialWaterLiters = 25_000f,
        InitialDryOrganicKg = 15_000f,
        InitialStructuralReserveKg = 5000f,
        InitialElectricityKwh = 800f,
        InitialHeatKwh = 120f,
        OrganSizes = sizes
    };
    var world = CreateWorld(16, 501);
    var caravan = new CaravanSimulation(blueprint, 8f, 8f);
    var coupled = new CoupledCaravanSimulation(
        world,
        caravan,
        new GrowthTestPolicy(),
        tickHours: 6d,
        scoutRadiusCells: 8);
    var before = coupled.CaptureCaravan();
    for (var step = 0; step < 180 * 4; step++) coupled.Advance();
    var after = coupled.CaptureCaravan();
    Assert(after.Organs[CaravanOrganKind.SolarLeaf].Size > before.Organs[CaravanOrganKind.SolarLeaf].Size,
        "surplus and high growth priority did not grow the solar organ");
    Assert(after.Organs[CaravanOrganKind.WaterIntake].Size
           < before.Organs[CaravanOrganKind.WaterIntake].Size,
        $"an unused, unprioritized organ did not atrophy after a season "
        + $"(mode {after.OperatingMode}, idle {after.Organs[CaravanOrganKind.WaterIntake].IdleDays}, "
        + $"size {after.Organs[CaravanOrganKind.WaterIntake].Size})");
}

static void ResourceExhaustionHibernatesWithoutDeathOrWork()
{
    var blueprint = CaravanBlueprint.Create(CaravanMorphology.Balanced);
    var initial = new CaravanSimulation(blueprint, 8f, 8f).Capture();
    var dormant = initial with
    {
        WaterLiters = 0f,
        OperatingMode = CaravanOperatingMode.Hibernating,
        HibernationReason = CaravanHibernationReason.WaterShortage,
        CurrentHibernationHours = 24f,
        CumulativeHibernationHours = 24f,
        HibernationEpisodes = 1,
        WaterDeficitHours = 30f * 24f
    };
    var checkpoint = new CaravanCheckpoint(
        CaravanPersistence.CurrentSchemaVersion,
        blueprint,
        dormant);
    var jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    using var stream = new MemoryStream();
    JsonSerializer.Serialize(stream, checkpoint, jsonOptions);
    stream.Position = 0;
    var caravan = CaravanPersistence.Load(stream);
    var coupled = new CoupledCaravanSimulation(
        CreateWorld(16, 809),
        caravan,
        CaravanPolicyCatalog.Create(CaravanPolicyKind.FollowWind),
        tickHours: 3d,
        scoutRadiusCells: 8);
    var before = coupled.CaptureCaravan();
    for (var step = 0; step < 40; step++) coupled.Advance();
    var after = coupled.CaptureCaravan();
    Assert(after.OperatingMode == CaravanOperatingMode.Hibernating,
        "an empty caravan left hibernation without a physical reserve");
    Assert(after.HibernationReason == CaravanHibernationReason.WaterShortage,
        "hibernation lost its physical cause");
    AssertNear(after.DistanceKilometers, before.DistanceKilometers, 1e-6f,
        "hibernating caravan moved");
    AssertNear(after.CumulativeSolarElectricityKwh, before.CumulativeSolarElectricityKwh, 1e-6f,
        "hibernating caravan produced electricity");
    Assert(after.CumulativeHibernationHours >= before.CumulativeHibernationHours + 120f,
        "hibernation time was not accumulated");
    Assert(after.ActivitySteps[CaravanActivity.Hibernate] == 40,
        "hibernation was not recorded as the effective activity");
}

static FiniteWorld CreateWorld(int size, int seed) => new(new WorldConfig
{
    Width = size,
    Height = size,
    Seed = seed,
    BaseStepMinutes = 60,
    GiantHarvesterCount = 4,
    ClimateVariability = 1f,
    WildfireEnabled = true
});

static CoupledCaravanSimulation CreateCoupled(
    CaravanMorphology morphology,
    CaravanPolicyKind policy,
    int size,
    int seed)
{
    var world = CreateWorld(size, seed);
    var caravan = new CaravanSimulation(CaravanBlueprint.Create(morphology), size / 2f, size / 2f);
    return new CoupledCaravanSimulation(
        world,
        caravan,
        CaravanPolicyCatalog.Create(policy),
        tickHours: 3d,
        scoutRadiusCells: Math.Min(10, size));
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void AssertNear(float actual, float expected, float tolerance, string message)
{
    if (Math.Abs(actual - expected) > tolerance)
        throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
}

sealed class GrowthTestPolicy : ICaravanPolicy
{
    private static readonly IReadOnlyDictionary<CaravanOrganKind, float> Growth =
        Enum.GetValues<CaravanOrganKind>().ToDictionary(
            item => item,
            item => item == CaravanOrganKind.SolarLeaf ? 1f : 0f);

    public string Name => "growth-test";

    public CaravanDecision Decide(CaravanObservation observation, CaravanStateSnapshot caravan) =>
        new(
            CaravanActivity.Maintain,
            observation.XCells,
            observation.YCells,
            Growth,
            "controlled growth and atrophy test");
}
