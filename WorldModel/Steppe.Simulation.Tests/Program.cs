using System.IO.Compression;
using System.Text;
using Steppe.Simulation;

var tests = new (string Name, Action Run)[]
{
    ("generation is deterministic", GenerationIsDeterministic),
    ("drainage spill rises match finite depression storage", DrainageSpillsMatchStorage),
    ("runoff reaches a lower neighbour", RunoffReachesLowerNeighbour),
    ("solar seasons emerge from astronomy", SolarSeasonsEmergeFromAstronomy),
    ("climate regimes are deterministic and diverse", ClimateRegimesAreDeterministicAndDiverse),
    ("anomalous climate remains ecologically bounded", AnomalousClimateRemainsEcologicallyBounded),
    ("water ledger closes across open boundaries", WaterLedgerCloses),
    ("save and load preserve the world", SaveLoadRoundTrip),
    ("giant harvesters migrate and leave readable trails", GiantHarvestersLeaveReadableTrails),
    ("save and load preserve fauna and trails", SaveLoadPreservesFaunaAndTrails),
    ("schema 3 saves migrate without fauna or compaction", SchemaThreeSaveMigrates),
    ("long simulation remains finite", LongSimulationRemainsFinite),
    ("seasonal steppe remains sustainable across seeds", SeasonalSteppeRemainsSustainableAcrossSeeds),
    ("every state is catalogued and observable", EveryStateIsCataloguedAndObservable),
    ("recorded process fluxes explain material changes", RecordedProcessFluxesExplainMaterialChanges),
    ("every vector process is observable", EveryVectorProcessIsObservable),
    ("world and pinned cells retain bounded history", WorldAndPinnedCellsRetainBoundedHistory),
    ("regime episodes use confirmation and hysteresis", RegimeEpisodesUseConfirmationAndHysteresis),
    ("seasonal simulation emits readable regime events", SeasonalSimulationEmitsReadableRegimeEvents)
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

static WorldConfig SmallConfig(int seed = 42) => new()
{
    Width = 24,
    Height = 24,
    Seed = seed,
    BaseStepMinutes = 180,
    GeographyErosionPasses = 4,
    GiantHarvesterCount = 0
};

static void GenerationIsDeterministic()
{
    var a = new FiniteWorld(SmallConfig(917)).CaptureLayer(SimulationLayer.Elevation);
    var b = new FiniteWorld(SmallConfig(917)).CaptureLayer(SimulationLayer.Elevation);
    Assert(a.Values.SequenceEqual(b.Values), "same seed produced different elevations");

    var c = new FiniteWorld(SmallConfig(918)).CaptureLayer(SimulationLayer.Elevation);
    Assert(!a.Values.SequenceEqual(c.Values), "different seeds produced identical elevations");
}

static void DrainageSpillsMatchStorage()
{
    var world = new FiniteWorld(SmallConfig(317));
    var elevation = world.CaptureLayer(SimulationLayer.Elevation);
    var storage = world.CaptureLayer(SimulationLayer.DepressionStorage);
    var drainage = world.CaptureVectorProcess(VectorProcess.DrainageDirection);
    var width = world.Config.Width;
    var visited = new HashSet<int>();

    for (var index = 0; index < world.Config.CellCount; index++)
    {
        var dx = Math.Sign(drainage.VectorX[index]);
        var dy = Math.Sign(drainage.VectorY[index]);
        if (dx == 0 && dy == 0) continue;
        var targetX = index % width + dx;
        var targetY = index / width + dy;
        if (targetX < 0 || targetY < 0 || targetX >= world.Config.Width || targetY >= world.Config.Height)
            continue;
        var target = targetY * width + targetX;
        var riseMm = Math.Max(0f, elevation.Values[target] - elevation.Values[index]) * 1000f;
        Assert(riseMm <= storage.Values[index] + 0.01f,
            $"cell {index} needs {riseMm:F2} mm head but stores only {storage.Values[index]:F2} mm");
        Assert(riseMm <= 221f, $"cell {index} retains an unresolved {riseMm:F1} mm pit");

        visited.Clear();
        var cursor = index;
        var looped = false;
        while (true)
        {
            if (!visited.Add(cursor))
            {
                looped = true;
                break;
            }
            var stepX = Math.Sign(drainage.VectorX[cursor]);
            var stepY = Math.Sign(drainage.VectorY[cursor]);
            if (stepX == 0 && stepY == 0) break;
            var x = cursor % width + stepX;
            var y = cursor / width + stepY;
            if (x < 0 || y < 0 || x >= world.Config.Width || y >= world.Config.Height) break;
            cursor = y * width + x;
        }

        Assert(!looped, $"drainage route from {index} contains a loop");
    }
}

static void RunoffReachesLowerNeighbour()
{
    var config = new WorldConfig
    {
        Width = 8,
        Height = 8,
        GeographyErosionPasses = 0,
        BaseStepMinutes = 60
    };
    var state = new WorldState(config.CellCount);
    var source = 3 * config.Width + 3;
    var target = 3 * config.Width + 4;
    Array.Fill(state.ElevationM, 10f);
    state.ElevationM[source] = 1f;
    state.ElevationM[target] = 0f;
    state.SurfaceWaterMm[source] = 40f;
    state.Slope[source] = 0.05f;
    state.DrainTo[source] = target;
    for (var index = 0; index < config.CellCount; index++)
    {
        if (index != source)
        {
            state.DrainTo[index] = index;
        }
    }

    var budget = new WaterBudget();
    budget.Initialize(state);
    WorldSystems.RouteSurfaceWater(config, state, budget, 1f);
    Assert(state.SurfaceWaterMm[source] < 40f, "source retained all runoff");
    Assert(state.SurfaceWaterMm[target] > 0f, "lower neighbour received no runoff");
    Assert(Math.Abs(budget.Snapshot(state).BalanceErrorMmCells) < 0.0001, "internal routing lost water");
}

static void SolarSeasonsEmergeFromAstronomy()
{
    var config = SmallConfig() with { LatitudeDegrees = 48 };
    var winter = new WorldClock((355 - 1) * 24 + 12);
    var summer = new WorldClock((172 - 1) * 24 + 12);
    var winterSun = Astronomy.Calculate(config, winter);
    var summerSun = Astronomy.Calculate(config, summer);
    Assert(summerSun.TopOfAtmosphereWm2 > winterSun.TopOfAtmosphereWm2 * 2, "summer noon is not energetically stronger");
    Assert(summerSun.DayLengthHours > winterSun.DayLengthHours, "summer day is not longer");
}

static void ClimateRegimesAreDeterministicAndDiverse()
{
    var config = SmallConfig(123) with { ClimateVariability = 1f };
    var first = Enumerable.Range(1, 100)
        .Select(year => ClimateRegimeModel.GetAnnualRegime(config, year))
        .ToArray();
    var repeated = Enumerable.Range(1, 100)
        .Select(year => ClimateRegimeModel.GetAnnualRegime(config, year))
        .ToArray();
    Assert(first.SequenceEqual(repeated), "annual climate regimes changed for a fixed seed");

    var temperatureRange = first.Max(item => item.TemperatureAnomalyC)
        - first.Min(item => item.TemperatureAnomalyC);
    var moistureRange = first.Max(item => item.MoistureMultiplier)
        - first.Min(item => item.MoistureMultiplier);
    var windRange = first.Max(item => item.WindSpeedMultiplier)
        - first.Min(item => item.WindSpeedMultiplier);
    Assert(temperatureRange >= 7f, $"century temperature anomaly range is only {temperatureRange:F2} C");
    Assert(moistureRange >= 1f, $"century moisture multiplier range is only {moistureRange:F2}");
    Assert(windRange >= 0.65f, $"century wind multiplier range is only {windRange:F2}");
    Assert(first.Any(item => item.SevereHeat), "century contains no severe hot year");
    Assert(first.Any(item => item.SevereCold), "century contains no severe cold year");
    Assert(first.Any(item => item.SevereDrought), "century contains no severe drought");
    Assert(first.Any(item => item.ExtremeWet), "century contains no extreme wet year");
    Assert(first.Any(item => item.SevereWind), "century contains no severe windy year");

    var forcing = new List<ClimateForcingSnapshot>();
    for (var year = 1; year <= 100; year++)
    {
        for (var day = 1; day <= 365; day += 2)
        {
            forcing.Add(ClimateRegimeModel.GetForcing(config, year, day));
        }
    }

    Assert(forcing.Any(item => item.Heatwave), "century contains no heatwave episode");
    Assert(forcing.Any(item => item.ColdSnap), "century contains no cold-snap episode");
    Assert(forcing.Any(item => item.RainBurst), "century contains no extreme-rain episode");
    Assert(forcing.Any(item => item.WindStorm), "century contains no windstorm episode");
    Assert(forcing.Max(item => item.StormIntensityMultiplier) >= 3f,
        "extreme rain never becomes materially stronger than normal storms");
    Assert(forcing.Max(item => item.WindSpeedMultiplier) >= 1.65f,
        "windstorms never become materially stronger than normal wind");

    var quietConfig = config with { ClimateVariability = 0f };
    var quiet = ClimateRegimeModel.GetForcing(quietConfig, 17, 200f);
    Assert(quiet.TemperatureOffsetC == 0f
            && quiet.MoistureMultiplier == 1f
            && quiet.WindSpeedMultiplier == 1f
            && !quiet.Heatwave
            && !quiet.ColdSnap
            && !quiet.RainBurst
            && !quiet.WindStorm,
        "zero climate variability does not reproduce the deterministic seasonal baseline");
}

static void AnomalousClimateRemainsEcologicallyBounded()
{
    const double yearHours = 365d * 24d;
    foreach (var (seed, years) in new[] { (123, 40), (7, 24), (999, 24) })
    {
        var world = new FiniteWorld(new WorldConfig
        {
            Width = 16,
            Height = 16,
            Seed = seed,
            BaseStepMinutes = 180,
            GeographyErosionPasses = 4,
            GiantHarvesterCount = 3,
            ClimateVariability = 1f
        });
        var biomass = new List<double>();
        var storedWater = new List<double>();
        var availableNitrogen = new List<double>();
        for (var year = 1; year <= years; year++)
        {
            world.AdvanceHours(yearHours);
            var summary = world.GetSummary();
            biomass.Add(summary.MeanLiveBiomassGm2);
            storedWater.Add(summary.WaterBudget.StoredMmCells / world.Config.CellCount);
            availableNitrogen.Add(Mean(world.CaptureLayer(SimulationLayer.AvailableNitrogen).Values));
            Assert(world.CaptureLayer(SimulationLayer.SurfaceTemperature).Statistics.NonFiniteCount == 0,
                $"seed {seed}, year {year} contains non-finite temperature");
            Assert(world.CaptureLayer(SimulationLayer.Wind).Statistics.NonFiniteCount == 0,
                $"seed {seed}, year {year} contains non-finite wind");
        }

        var final = world.GetSummary();
        Assert(Math.Abs(final.WaterBudget.RelativeError) < 0.00005,
            $"seed {seed} anomalous climate violates the water ledger ({final.WaterBudget.RelativeError})");
        Assert(Math.Abs(final.NitrogenBudget.RelativeError) < 0.00005,
            $"seed {seed} anomalous climate violates the nitrogen ledger ({final.NitrogenBudget.RelativeError})");
        Assert(biomass.Skip(10).Min() > 5f, $"seed {seed} vegetation collapses after a climate anomaly");
        Assert(storedWater.Skip(10).Min() > 20f, $"seed {seed} water storage collapses after an anomaly");
        Assert(availableNitrogen.Skip(10).Min() > 0.15f,
            $"seed {seed} available nitrogen collapses under climate anomalies");
        Assert(biomass.TakeLast(5).Average() > biomass.Skip(10).Take(5).Average() * 0.55,
            $"seed {seed} vegetation shows unresolved long-term collapse");
    }
}

static void WaterLedgerCloses()
{
    var world = new FiniteWorld(SmallConfig(122));
    world.AdvanceHours(24 * 20);
    var budget = world.GetSummary().WaterBudget;
    Assert(Math.Abs(budget.RelativeError) < 0.00002, $"relative water error is {budget.RelativeError}");
    var nitrogen = world.GetSummary().NitrogenBudget;
    Assert(Math.Abs(nitrogen.RelativeError) < 0.00002, $"relative nitrogen error is {nitrogen.RelativeError}");
}

static void SaveLoadRoundTrip()
{
    var world = new FiniteWorld(SmallConfig(731));
    world.AdvanceHours(73);
    using var stream = new MemoryStream();
    world.Save(stream);
    stream.Position = 0;
    var loaded = FiniteWorld.Load(stream);
    var original = world.CaptureLayer(SimulationLayer.LiveBiomass);
    var copy = loaded.CaptureLayer(SimulationLayer.LiveBiomass);
    Assert(original.Values.SequenceEqual(copy.Values), "biomass changed during round trip");
    Assert(Math.Abs(world.Clock.ElapsedHours - loaded.Clock.ElapsedHours) < 1e-9, "clock changed during round trip");
}

static void GiantHarvestersLeaveReadableTrails()
{
    var config = SmallConfig(147) with { GiantHarvesterCount = 3 };
    var first = new FiniteWorld(config);
    var second = new FiniteWorld(config);
    first.AdvanceHours(60 * 24);
    second.AdvanceHours(60 * 24);

    var fauna = first.CaptureGiantHarvesters();
    var repeated = second.CaptureGiantHarvesters();
    Assert(fauna.Harvesters.Length == 3, "configured giant harvesters were not generated");
    Assert(fauna.Harvesters.SequenceEqual(repeated.Harvesters),
        "giant harvester migration is not deterministic for a fixed seed");
    Assert(fauna.Harvesters.All(item => item.DistanceCells > 1f),
        "a giant harvester did not migrate through the landscape");
    Assert(fauna.Harvesters.Sum(item => item.GrazedLiveBiomassGm2 + item.GrazedDryBiomassGm2) > 1f,
        "giant harvesters did not graze");

    var compaction = first.CaptureLayer(SimulationLayer.SoilCompaction);
    Assert(compaction.Maximum > 0.01f, "giant harvesters left no compacted trail");
    Assert(compaction.Values.Count(value => value > 0.002f) > 3,
        "giant harvester trail is not spatially readable");
    var movement = first.CaptureVectorProcess(VectorProcess.GiantHarvesterMovement);
    Assert(movement.TotalGrossMagnitude > 1, "giant harvester movement has no vector telemetry");
    Assert(first.CaptureFlux(SimulationFlux.GiantHarvesterLiveGrazing).PositiveTotal > 0,
        "grazing has no process telemetry");
    Assert(first.CaptureFlux(SimulationFlux.GiantHarvesterCompaction).PositiveTotal > 0,
        "trail compaction has no process telemetry");
    var summary = first.GetSummary();
    Assert(Math.Abs(summary.WaterBudget.RelativeError) < 0.00005,
        $"harvester drinking violates the water ledger ({summary.WaterBudget.RelativeError})");
    Assert(Math.Abs(summary.NitrogenBudget.RelativeError) < 0.00005,
        $"harvester grazing violates the nitrogen ledger ({summary.NitrogenBudget.RelativeError})");

    first.AdvanceHours(105 * 24);
    var molt = first.CaptureGiantHarvesters().Molts.FirstOrDefault()
        ?? throw new InvalidOperationException("giant harvesters produced no collectible molt");
    var collected = first.CollectGiantHarvesterMolt(
        (int)MathF.Round(molt.X),
        (int)MathF.Round(molt.Y),
        2f,
        molt.ChitinKg);
    Assert(collected > 1f, "a nearby giant-harvester molt could not be collected");
}

static void SaveLoadPreservesFaunaAndTrails()
{
    var world = new FiniteWorld(SmallConfig(602) with { GiantHarvesterCount = 2 });
    world.AdvanceHours(55 * 24);
    using var stream = new MemoryStream();
    world.Save(stream);
    stream.Position = 0;
    var loaded = FiniteWorld.Load(stream);

    var fauna = world.CaptureGiantHarvesters();
    var loadedFauna = loaded.CaptureGiantHarvesters();
    Assert(fauna.Harvesters.SequenceEqual(loadedFauna.Harvesters)
            && fauna.Molts.SequenceEqual(loadedFauna.Molts),
        "fauna changed during save/load round trip");
    Assert(world.CaptureLayer(SimulationLayer.SoilCompaction).Values.SequenceEqual(
            loaded.CaptureLayer(SimulationLayer.SoilCompaction).Values),
        "soil compaction changed during save/load round trip");
    Assert(world.GetSummary().WaterBudget == loaded.GetSummary().WaterBudget,
        "fauna water withdrawals changed during save/load round trip");
    Assert(world.GetSummary().NitrogenBudget == loaded.GetSummary().NitrogenBudget,
        "fauna nitrogen ledger changed during save/load round trip");
}

static void SchemaThreeSaveMigrates()
{
    var config = new WorldConfig
    {
        Width = 8,
        Height = 8,
        Seed = 911,
        BaseStepMinutes = 180,
        GeographyErosionPasses = 0
    };
    var state = new WorldState(config.CellCount);
    state.LiveBiomassGm2[17] = 42f;
    using var stream = WriteSchemaThreeWorld(config, state);
    var loaded = FiniteWorld.Load(stream);

    Assert(loaded.Config.GiantHarvesterCount == 0,
        "legacy save unexpectedly generated fauna");
    Assert(loaded.CaptureGiantHarvesters().Harvesters.Length == 0,
        "legacy save contains giant harvesters");
    Assert(loaded.CaptureLayer(SimulationLayer.SoilCompaction).Values.All(value => value == 0f),
        "legacy save did not initialize the new compaction field to zero");
    Assert(Math.Abs(loaded.CaptureLayer(SimulationLayer.LiveBiomass).Values[17] - 42f) < 1e-6f,
        "legacy state fields shifted during schema migration");
}

static MemoryStream WriteSchemaThreeWorld(WorldConfig config, WorldState state)
{
    var stream = new MemoryStream();
    using (var gzip = new GZipStream(stream, CompressionLevel.Optimal, leaveOpen: true))
    using (var writer = new BinaryWriter(gzip, Encoding.UTF8, leaveOpen: true))
    {
        writer.Write("STEPPE-WORLD-MODEL");
        writer.Write(3);
        writer.Write(config.Width);
        writer.Write(config.Height);
        writer.Write(config.CellSizeMeters);
        writer.Write(config.Seed);
        writer.Write(config.LatitudeDegrees);
        writer.Write(config.AxialTiltDegrees);
        writer.Write(config.BaseStepMinutes);
        writer.Write(config.GeographyErosionPasses);
        writer.Write(0d);
        writer.Write(0d);
        writer.Write(0d);
        writer.Write(0d);
        writer.Write(0d);
        var fields = state.SerializableFloatFields()
            .Where(field => !ReferenceEquals(field, state.SoilCompactionFraction))
            .ToArray();
        writer.Write(fields.Length);
        foreach (var field in fields)
        {
            writer.Write(field.Length);
            foreach (var value in field) writer.Write(value);
        }
        writer.Write(state.DrainTo.Length);
        foreach (var value in state.DrainTo) writer.Write(value);
        writer.Write(state.CatchmentId.Length);
        foreach (var value in state.CatchmentId) writer.Write(value);
    }

    stream.Position = 0;
    return stream;
}

static void LongSimulationRemainsFinite()
{
    var world = new FiniteWorld(SmallConfig(81));
    world.AdvanceHours(24 * 365);
    foreach (var layer in Enum.GetValues<SimulationLayer>())
    {
        var snapshot = world.CaptureLayer(layer);
        Assert(snapshot.Values.All(float.IsFinite), $"{layer} contains a non-finite value");
        Assert(snapshot.Values.All(value => value > -100_000 && value < 100_000), $"{layer} diverged");
    }
}

static void EveryStateIsCataloguedAndObservable()
{
    var layers = Enum.GetValues<SimulationLayer>();
    var descriptors = StateCatalog.All;
    Assert(descriptors.Count == layers.Length,
        $"catalog has {descriptors.Count} descriptors for {layers.Length} states");
    Assert(descriptors.Select(descriptor => descriptor.Id).Distinct().Count() == layers.Length,
        "catalog contains duplicate state identifiers");

    var world = new FiniteWorld(SmallConfig(619));
    foreach (var layer in layers)
    {
        var descriptor = StateCatalog.Get(layer);
        Assert(!string.IsNullOrWhiteSpace(descriptor.Title), $"{layer} has no readable title");
        Assert(!string.IsNullOrWhiteSpace(descriptor.Description), $"{layer} has no explanation");
        Assert(!string.IsNullOrWhiteSpace(descriptor.Unit), $"{layer} has no unit");
        Assert(descriptor.ScaleMaximum > descriptor.ScaleMinimum, $"{layer} has an invalid fixed scale");
        Assert(descriptor.Palette.Length >= 2, $"{layer} has no readable palette");

        var snapshot = world.CaptureLayer(layer);
        Assert(snapshot.Values.Length == world.Config.CellCount, $"{layer} has an incomplete raster");
        Assert(snapshot.Unit == descriptor.Unit, $"{layer} uses a unit outside the catalog");
        Assert(snapshot.Statistics.Histogram.Sum() + snapshot.Statistics.NonFiniteCount == snapshot.Values.Length,
            $"{layer} distribution does not cover every cell");
    }

    var cell = world.SampleCell(7, 11);
    Assert(cell.States.Length == layers.Length, "cell snapshot omits observable states");
    Assert(cell.States.Select(value => value.State).Distinct().Count() == layers.Length,
        "cell snapshot duplicates state values");
    Assert(cell.States.All(value => float.IsFinite(value.Value)), "cell snapshot contains a non-finite state");

    var wind = world.CaptureLayer(SimulationLayer.Wind);
    var drainage = world.CaptureLayer(SimulationLayer.Drainage);
    Assert(wind.VectorX is not null && wind.VectorY is not null, "wind lost its vector field");
    Assert(drainage.VectorX is not null && drainage.VectorY is not null, "drainage is not readable as vectors");
}

static void RecordedProcessFluxesExplainMaterialChanges()
{
    var fluxes = Enum.GetValues<SimulationFlux>();
    Assert(FluxCatalog.All.Count == fluxes.Length,
        $"flux catalog has {FluxCatalog.All.Count} descriptors for {fluxes.Length} processes");
    Assert(FluxCatalog.All.Select(descriptor => descriptor.Id).Distinct().Count() == fluxes.Length,
        "flux catalog contains duplicate identifiers");
    foreach (var descriptor in FluxCatalog.All)
    {
        Assert(!string.IsNullOrWhiteSpace(descriptor.Title), $"{descriptor.Id} has no readable title");
        Assert(!string.IsNullOrWhiteSpace(descriptor.Unit), $"{descriptor.Id} has no unit");
        Assert(descriptor.Effects.Length > 0, $"{descriptor.Id} has no state effects");
        Assert(descriptor.Effects.All(effect => float.IsFinite(effect.Factor) && effect.Factor != 0),
            $"{descriptor.Id} has an invalid state effect");
    }

    var world = new FiniteWorld(SmallConfig(448));
    const int x = 10;
    const int y = 10;
    world.AddSurfaceWater(x, y, 50f);
    var intervention = world.ExplainCellState(x, y, SimulationLayer.SurfaceWater);
    Assert(intervention.Delta > 49.999f, "external water was not captured as a state delta");
    Assert(intervention.Contributions.Any(item => item.Flux == SimulationFlux.ExternalSurfaceWater),
        "external water has no recorded process contribution");
    Assert(Math.Abs(intervention.UnexplainedDelta) < 0.0001f,
        $"external water leaves {intervention.UnexplainedDelta} mm unexplained");

    world.AdvanceHours(6);
    var layersWithMaterialLedgers = new[]
    {
        SimulationLayer.Humidity,
        SimulationLayer.CloudWater,
        SimulationLayer.SurfaceWater,
        SimulationLayer.RootWater,
        SimulationLayer.Groundwater,
        SimulationLayer.Snow,
        SimulationLayer.LiveBiomass,
        SimulationLayer.DryBiomass,
        SimulationLayer.LitterBiomass,
        SimulationLayer.SeedBank,
        SimulationLayer.SoilOrganicMatter,
        SimulationLayer.AvailableNitrogen,
        SimulationLayer.PlantNitrogen,
        SimulationLayer.OrganicNitrogen,
        SimulationLayer.SoilDepth,
        SimulationLayer.LooseSediment,
        SimulationLayer.SurfaceCrust,
        SimulationLayer.Dust
    };

    foreach (var layer in layersWithMaterialLedgers)
    {
        var explanation = world.ExplainCellState(x, y, layer);
        Assert(Math.Abs(explanation.PeriodHours - 6) < 1e-9, $"{layer} reports the wrong observation period");
        Assert(Math.Abs(explanation.UnexplainedDelta) < 0.001f,
            $"{layer} leaves {explanation.UnexplainedDelta} {StateCatalog.Get(layer).Unit} unexplained");
    }

    var surface = world.ExplainCellState(x, y, SimulationLayer.SurfaceWater);
    Assert(surface.Contributions.Any(item => item.Flux is SimulationFlux.Infiltration or SimulationFlux.RunoffOut),
        "surface-water change does not expose infiltration or runoff");
}

static void EveryVectorProcessIsObservable()
{
    var processes = Enum.GetValues<VectorProcess>();
    Assert(VectorProcessCatalog.All.Count == processes.Length,
        $"vector catalog has {VectorProcessCatalog.All.Count} descriptors for {processes.Length} processes");
    Assert(VectorProcessCatalog.All.Select(descriptor => descriptor.Process).Distinct().Count() == processes.Length,
        "vector catalog contains duplicate identifiers");

    var world = new FiniteWorld(SmallConfig(991));
    world.AdvanceHours(30 * 24);
    foreach (var process in processes)
    {
        var descriptor = VectorProcessCatalog.Get(process);
        Assert(!string.IsNullOrWhiteSpace(descriptor.Title), $"{process} has no readable title");
        Assert(!string.IsNullOrWhiteSpace(descriptor.Description), $"{process} has no explanation");
        Assert(!string.IsNullOrWhiteSpace(descriptor.Unit), $"{process} has no unit");

        var snapshot = world.CaptureVectorProcess(process);
        Assert(snapshot.VectorX.Length == world.Config.CellCount, $"{process} has an incomplete X raster");
        Assert(snapshot.VectorY.Length == world.Config.CellCount, $"{process} has an incomplete Y raster");
        Assert(snapshot.Magnitude.Length == world.Config.CellCount, $"{process} has an incomplete magnitude raster");
        Assert(snapshot.GrossMagnitude.Length == world.Config.CellCount, $"{process} has an incomplete gross raster");
        Assert(snapshot.NonFiniteCount == 0, $"{process} contains non-finite telemetry");
        Assert(snapshot.VectorX.All(float.IsFinite) && snapshot.VectorY.All(float.IsFinite),
            $"{process} contains a non-finite vector component");
        Assert(snapshot.GrossMagnitude.Zip(snapshot.Magnitude, (gross, net) => gross + 1e-5f >= net).All(value => value),
            $"{process} reports net transfer larger than gross transfer");
    }
}

static void WorldAndPinnedCellsRetainBoundedHistory()
{
    var world = new FiniteWorld(SmallConfig(224));
    var initial = world.CaptureWorldHistory();
    Assert(initial.Points.Length == 1, "new world has no initial history point");
    Assert(initial.SampleIntervalHours == 6, "recent world history uses the wrong cadence");

    world.AdvanceHours(25);
    var recent = world.CaptureWorldHistory(HistoryResolution.Recent);
    var daily = world.CaptureWorldHistory(HistoryResolution.Daily);
    var monthly = world.CaptureWorldHistory(HistoryResolution.Monthly);
    Assert(recent.Points.Length == 5, $"25 hours produced {recent.Points.Length} recent checkpoints instead of 5");
    Assert(daily.Points.Length == 2, "daily history missed the first day boundary");
    Assert(monthly.Points.Length == 1, "monthly history advanced before its first month");
    Assert(recent.Points.Zip(recent.Points.Skip(1), (a, b) => b.ElapsedHours > a.ElapsedHours).All(value => value),
        "world history is not chronological");

    const int x = 8;
    const int y = 9;
    Assert(world.PinCell(x, y), "cell could not be pinned");
    Assert(!world.PinCell(x, y), "pinning the same cell twice created a duplicate probe");
    var pinnedStart = world.CaptureCellHistory(x, y, SimulationLayer.SurfaceTemperature);
    Assert(pinnedStart.IsPinned && pinnedStart.Points.Length == 1, "pinned cell did not capture its starting state");

    world.AdvanceHours(30);
    var temperature = world.CaptureCellHistory(x, y, SimulationLayer.SurfaceTemperature);
    var rootWater = world.CaptureCellHistory(x, y, SimulationLayer.RootWater);
    Assert(temperature.Points.Length >= 5, "pinned cell did not record six-hour checkpoints");
    Assert(rootWater.Points.Length == temperature.Points.Length, "cell state histories use different time axes");
    Assert(temperature.Points.Select(point => point.Value).Distinct().Count() > 1,
        "pinned temperature history contains no temporal change");
    Assert(world.GetPinnedCells().Length == 1, "pinned-cell registry lost the probe");

    world.AdvanceHours(181 * 24);
    var boundedRecent = world.CaptureCellHistory(x, y, SimulationLayer.SurfaceTemperature);
    Assert(boundedRecent.Points.Length == 720,
        $"recent cell history exceeded its 180-day bound ({boundedRecent.Points.Length} points)");
    Assert(boundedRecent.Points[0].ElapsedHours > pinnedStart.Points[0].ElapsedHours,
        "recent cell history did not evict its oldest checkpoint");
    Assert(world.CaptureWorldHistory(HistoryResolution.Recent).Points.Length == 720,
        "recent world history exceeded its 180-day bound");

    Assert(world.UnpinCell(x, y), "cell could not be unpinned");
    var removed = world.CaptureCellHistory(x, y, SimulationLayer.SurfaceTemperature);
    Assert(!removed.IsPinned && removed.Points.Length == 0, "unpinned cell retained an active history series");
}

static void RegimeEpisodesUseConfirmationAndHysteresis()
{
    Assert(WorldEventCatalog.All.Count == Enum.GetValues<WorldEventKind>().Length,
        "event catalog does not describe every regime event");
    Assert(WorldEventCatalog.All.All(item => item.EnterThreshold > item.ExitThreshold),
        "event hysteresis thresholds are inverted");

    var log = new WorldEventLog();
    log.Initialize(RisingRegime(0));
    for (var hours = 6; hours <= 30; hours += 6)
    {
        log.Observe(RisingRegime(hours));
    }

    var active = log.Capture(32);
    Assert(active.Active.Length == Enum.GetValues<WorldEventKind>().Length,
        $"synthetic transition activated {active.Active.Length} of {Enum.GetValues<WorldEventKind>().Length} events");
    Assert(active.Active.All(item => item.StartedAtHours == 24),
        "confirmation did not preserve the first above-threshold observation");
    Assert(active.Active.All(item => item.EndedAtHours is null), "active episode has an end time");

    for (var hours = 36; hours <= 66; hours += 6)
    {
        log.Observe(StableRegime(hours));
    }

    var completed = log.Capture(32);
    Assert(completed.Active.Length == 0, "events did not leave their hysteresis bands");
    Assert(completed.Recent.Length == Enum.GetValues<WorldEventKind>().Length,
        "completed event log lost an episode");
    Assert(completed.Recent.All(item => item.EndedAtHours > item.StartedAtHours),
        "completed episode has an invalid interval");
    Assert(completed.Recent.All(item => float.IsFinite(item.PeakIndicator) && float.IsFinite(item.PeakSeverity)),
        "completed episode contains non-finite evidence");
}

static void SeasonalSimulationEmitsReadableRegimeEvents()
{
    var world = new FiniteWorld(new WorldConfig
    {
        Width = 16,
        Height = 16,
        Seed = 123,
        BaseStepMinutes = 180,
        GeographyErosionPasses = 4,
        GiantHarvesterCount = 0
    });
    world.AdvanceHours(365 * 24);
    var snapshot = world.CaptureRegimeEvents(256);
    var episodes = snapshot.Active.Concat(snapshot.Recent).ToArray();
    Assert(episodes.Any(item => item.Kind == WorldEventKind.Snowmelt),
        "annual cycle did not expose snowmelt");
    Assert(episodes.Any(item => item.Kind == WorldEventKind.GreenUp),
        "annual cycle did not expose green-up");
    Assert(episodes.All(item => item.PeakMetrics.ElapsedHours == item.PeakAtHours),
        "event peak time and evidence disagree");
}

static WorldRegimeMetrics RisingRegime(double elapsedHours) => new(
    elapsedHours,
    1,
    (int)(elapsedHours / 24) + 1,
    elapsedHours % 24,
    10,
    0,
    10f - (float)elapsedHours * 0.1f,
    0.5f,
    (float)elapsedHours * 0.02f,
    0.1f,
    80,
    0.4f,
    100f + (float)elapsedHours * 0.02f,
    0.2f,
    0.02f,
    0.1f,
    7);

static WorldRegimeMetrics StableRegime(double elapsedHours) => new(
    elapsedHours,
    1,
    (int)(elapsedHours / 24) + 1,
    elapsedHours % 24,
    10,
    0,
    7,
    0.5f,
    0.6f,
    0.1f,
    80,
    0.1f,
    100.6f,
    0.2f,
    0.001f,
    0,
    2);

static void SeasonalSteppeRemainsSustainableAcrossSeeds()
{
    const double yearHours = 365 * 24;
    foreach (var seed in new[] { 7, 123, 999 })
    {
        var world = new FiniteWorld(new WorldConfig
        {
            Width = 20,
            Height = 20,
            Seed = seed,
            BaseStepMinutes = 180,
            GeographyErosionPasses = 4,
            GiantHarvesterCount = 0,
            ClimateVariability = 0f
        });

        world.AdvanceHours(yearHours * 4);
        var winterBefore = world.GetSummary();
        world.AdvanceHours(yearHours);
        var winterAfter = world.GetSummary();
        var cells = world.Config.CellCount;
        var annualStorageDrift = Math.Abs(
            winterAfter.WaterBudget.StoredMmCells - winterBefore.WaterBudget.StoredMmCells) / cells;
        var winterSnow = world.CaptureLayer(SimulationLayer.Snow);
        var winterSurface = world.CaptureLayer(SimulationLayer.SurfaceWater);
        var winterGroundwater = world.CaptureLayer(SimulationLayer.Groundwater);
        var winterRootWater = world.CaptureLayer(SimulationLayer.RootWater);
        var winterBiomass = world.CaptureLayer(SimulationLayer.LiveBiomass);
        var winterNitrogen = world.CaptureLayer(SimulationLayer.AvailableNitrogen);

        Assert(annualStorageDrift < 18,
            $"seed {seed} drifts by {annualStorageDrift:F2} mm/cell/year");
        Assert(Math.Abs(winterAfter.WaterBudget.RelativeError) < 0.00005,
            $"seed {seed} violates its water ledger");
        Assert(Math.Abs(winterAfter.NitrogenBudget.RelativeError) < 0.00005,
            $"seed {seed} violates its nitrogen ledger");
        Assert(Mean(winterSnow.Values) is > 1 and < 80,
            $"seed {seed} has no bounded winter snowpack");
        Assert(winterSnow.Maximum < 900,
            $"seed {seed} forms an unbounded snow cell ({winterSnow.Maximum:F1} mm)");
        Assert(winterSurface.Maximum < 320,
            $"seed {seed} forms an unbounded surface-water cell ({winterSurface.Maximum:F1} mm)");
        Assert(winterGroundwater.Maximum < 225,
            $"seed {seed} forms an unbounded groundwater cell ({winterGroundwater.Maximum:F1} mm)");
        Assert(winterRootWater.Maximum < 180,
            $"seed {seed} exceeds the readable root-water scale ({winterRootWater.Maximum:F1} mm)");
        Assert(Mean(winterBiomass.Values) > 20,
            $"seed {seed} loses its vegetation during winter");
        Assert(Mean(winterNitrogen.Values) > 0.45,
            $"seed {seed} locks all available nitrogen in organic reservoirs");

        world.AdvanceHours(200 * 24 + 12);
        var summerSnow = world.CaptureLayer(SimulationLayer.Snow);
        var summerSurface = world.CaptureLayer(SimulationLayer.SurfaceWater);
        var summerBiomass = world.CaptureLayer(SimulationLayer.LiveBiomass);
        Assert(summerSnow.Maximum < 0.5,
            $"seed {seed} retains snow through midsummer");
        Assert(summerSurface.Maximum < 320,
            $"seed {seed} forms an unbounded summer lake");
        Assert(Mean(summerBiomass.Values) > 40,
            $"seed {seed} does not regrow a summer steppe");
    }
}

static double Mean(float[] values)
{
    double sum = 0;
    foreach (var value in values)
    {
        sum += value;
    }

    return sum / values.Length;
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
