using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Steppe.Simulation;

var options = StressOptions.Parse(args);
Directory.CreateDirectory(options.OutputDirectory);
var stopwatch = Stopwatch.StartNew();
var world = new FiniteWorld(new WorldConfig
{
    Width = options.Size,
    Height = options.Size,
    Seed = options.Seed,
    BaseStepMinutes = options.StepMinutes,
    GeographyErosionPasses = options.ErosionPasses,
    GiantHarvesterCount = options.Harvesters,
    ClimateVariability = options.ClimateVariability
});

var layerAggregates = Enum.GetValues<SimulationLayer>()
    .ToDictionary(layer => layer, layer => new LayerAggregate(layer));
var fluxAggregates = Enum.GetValues<SimulationFlux>()
    .ToDictionary(flux => flux, flux => new FluxAggregate(flux));
var vectorAggregates = Enum.GetValues<VectorProcess>()
    .ToDictionary(process => process, process => new VectorAggregate(process));
var eventAggregates = Enum.GetValues<WorldEventKind>()
    .ToDictionary(kind => kind, kind => new EventAggregate(kind));
var temporal = new List<TemporalCheckpoint>(options.Years * (365 / options.SampleDays + 1));
var seenEvents = new HashSet<long>();
var annual = new List<AnnualCheckpoint>(options.Years);
var initialLayers = CaptureLayers(world);
var drainage = AnalyzeDrainage(initialLayers[SimulationLayer.Elevation], world.CaptureVectorProcess(VectorProcess.DrainageDirection));
var drainageVector = world.CaptureVectorProcess(VectorProcess.DrainageDirection);
var slope = initialLayers[SimulationLayer.Slope].Values;
var previousBudget = world.GetSummary().WaterBudget;
var globalNonFinite = 0L;
var chunkCount = 0;

Console.Error.WriteLine(
    $"stress qualification: {options.Years} years, {options.Size}×{options.Size}, seed {options.Seed}, "
    + $"climate {options.ClimateVariability:F2}, {options.StepMinutes}-minute step");

for (var year = 1; year <= options.Years; year++)
{
    var daysRemaining = 365;
    Dictionary<SimulationLayer, LayerSnapshot>? layers = null;
    while (daysRemaining > 0)
    {
        var days = Math.Min(options.SampleDays, daysRemaining);
        world.AdvanceHours(days * 24d);
        daysRemaining -= days;
        chunkCount++;

        layers = CaptureLayers(world);
        foreach (var (layer, snapshot) in layers)
        {
            layerAggregates[layer].Observe(snapshot);
            globalNonFinite += snapshot.Statistics.NonFiniteCount;
        }

        foreach (var flux in Enum.GetValues<SimulationFlux>())
        {
            fluxAggregates[flux].Observe(world.CaptureFlux(flux), world.Config.CellCount);
        }

        var wind = world.CaptureVectorProcess(VectorProcess.Wind);
        foreach (var process in Enum.GetValues<VectorProcess>())
        {
            var snapshot = process switch
            {
                VectorProcess.Wind => wind,
                VectorProcess.DrainageDirection => drainageVector,
                _ => world.CaptureVectorProcess(process)
            };
            var reference = ReferenceVectors(process, layers, wind, drainageVector, world.Config.Width);
            vectorAggregates[process].Observe(snapshot, reference.X, reference.Y, process == VectorProcess.AirTemperatureAdvection);
            globalNonFinite += snapshot.NonFiniteCount;
        }

        vectorAggregates[VectorProcess.Wind].ObserveAuxiliaryWind(
            wind,
            layers[SimulationLayer.Pressure].Values,
            slope,
            world.Config.Width,
            world.Config.Height);

        temporal.Add(new TemporalCheckpoint(
            year,
            365 - daysRemaining,
            world.Clock.ElapsedHours,
            layers.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value.Statistics.Mean),
            layers.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value.Statistics.Percentile02),
            layers.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value.Statistics.Percentile98),
            layers.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value.Maximum),
            ClimateRegimeModel.GetForcing(world.Config, year, Math.Max(1f, 365 - daysRemaining))));

        var events = world.CaptureRegimeEvents(256);
        foreach (var episode in events.Active.Concat(events.Recent))
        {
            if (seenEvents.Add(episode.Id))
            {
                eventAggregates[episode.Kind].Observe(episode);
            }
            else
            {
                eventAggregates[episode.Kind].RefreshPeak(episode);
            }
        }
    }

    var summary = world.GetSummary();
    layers ??= CaptureLayers(world);
    var cells = world.Config.CellCount;
    var annualInput = summary.WaterBudget.ExternalInputMmCells - previousBudget.ExternalInputMmCells;
    var annualOutput = summary.WaterBudget.ExternalOutputMmCells - previousBudget.ExternalOutputMmCells;
    annual.Add(new AnnualCheckpoint(
        year,
        summary.WaterBudget.StoredMmCells / cells,
        annualInput / cells,
        annualOutput / cells,
        summary.WaterBudget.RelativeError,
        summary.NitrogenBudget.StoredGm2Cells / cells,
        summary.NitrogenBudget.RelativeError,
        world.CaptureAnnualClimateRegime(year),
        layers.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value.Statistics.Mean),
        layers.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value.Maximum),
        layers.Values.Sum(layer => layer.Statistics.NonFiniteCount)));
    previousBudget = summary.WaterBudget;

    Console.Error.WriteLine(
        $"year {year,3}/{options.Years}: water {annual[^1].StoredWaterMmPerCell,7:F2} mm/cell, "
        + $"live {annual[^1].LayerMeans[nameof(SimulationLayer.LiveBiomass)],6:F1} g/m², "
        + $"snow {annual[^1].LayerMeans[nameof(SimulationLayer.Snow)],6:F1} mm, "
        + $"H2O {annual[^1].WaterRelativeError:E2}, N {annual[^1].NitrogenRelativeError:E2}, "
        + $"runtime {stopwatch.Elapsed.TotalSeconds:F1}s");
}

stopwatch.Stop();
var last = world.GetSummary();
var cycles = AnalyzeCycles(temporal);
var climate = AnalyzeClimate(world.Config, annual, temporal);
var recovery = AnalyzeRecovery(annual);
var failures = EvaluateFailures(
    annual,
    layerAggregates,
    fluxAggregates,
    vectorAggregates,
    drainage,
    cycles,
    climate,
    recovery,
    last,
    world.Config.CellCount);
var lastTwenty = annual.TakeLast(Math.Min(20, annual.Count)).ToArray();
var postSpinup = annual.Skip(Math.Min(20, annual.Count)).ToArray();
var report = new
{
    generatedAtUtc = DateTimeOffset.UtcNow,
    configuration = new
    {
        options.Years,
        options.Size,
        options.Seed,
        options.StepMinutes,
        options.SampleDays,
        options.ErosionPasses,
        options.Harvesters,
        options.ClimateVariability,
        cellSizeMeters = world.Config.CellSizeMeters,
        simulatedHours = world.Clock.ElapsedHours,
        internalSteps = world.Clock.ElapsedHours / (options.StepMinutes / 60d)
    },
    runtime = new
    {
        seconds = stopwatch.Elapsed.TotalSeconds,
        simulatedYearsPerSecond = options.Years / stopwatch.Elapsed.TotalSeconds,
        chunks = chunkCount
    },
    passed = failures.Count == 0,
    failures,
    derived = new
    {
        finalWaterRelativeError = last.WaterBudget.RelativeError,
        finalNitrogenRelativeError = last.NitrogenBudget.RelativeError,
        finalStoredNitrogenGm2PerCell = last.NitrogenBudget.StoredGm2Cells / world.Config.CellCount,
        finalStoredWaterMmPerCell = last.WaterBudget.StoredMmCells / world.Config.CellCount,
        storageTrendLast20YearsMmPerCellPerYear = LinearSlope(lastTwenty.Select(item => item.StoredWaterMmPerCell).ToArray()),
        storageTrendAfterSpinupMmPerCellPerYear = LinearSlope(postSpinup.Select(item => item.StoredWaterMmPerCell).ToArray()),
        liveBiomassTrendLast20YearsGm2PerYear = LinearSlope(lastTwenty.Select(item => (double)item.LayerMeans[nameof(SimulationLayer.LiveBiomass)]).ToArray()),
        liveBiomassTrendAfterSpinupGm2PerYear = LinearSlope(postSpinup.Select(item => (double)item.LayerMeans[nameof(SimulationLayer.LiveBiomass)]).ToArray()),
        finalLiveBiomassFractionOfYearOne = annual.Count > 0
            ? annual[^1].LayerMeans[nameof(SimulationLayer.LiveBiomass)]
                / annual[0].LayerMeans[nameof(SimulationLayer.LiveBiomass)]
            : 0,
        availableNitrogenTrendLast20YearsGm2PerYear = LinearSlope(lastTwenty.Select(item => (double)item.LayerMeans[nameof(SimulationLayer.AvailableNitrogen)]).ToArray()),
        finalAvailableNitrogenFractionOfYearOne = annual.Count > 0
            ? annual[^1].LayerMeans[nameof(SimulationLayer.AvailableNitrogen)]
                / annual[0].LayerMeans[nameof(SimulationLayer.AvailableNitrogen)]
            : 0,
        surfaceCrustTrendLast20YearsPerYear = LinearSlope(lastTwenty.Select(item => (double)item.LayerMeans[nameof(SimulationLayer.SurfaceCrust)]).ToArray()),
        soilCompactionTrendLast20YearsPerYear = LinearSlope(lastTwenty.Select(item => (double)item.LayerMeans[nameof(SimulationLayer.SoilCompaction)]).ToArray()),
        maximumObservedSoilCompaction = layerAggregates[SimulationLayer.SoilCompaction].ObservedMaximum,
        soilOrganicMatterTrendLast20YearsGm2PerYear = LinearSlope(lastTwenty.Select(item => (double)item.LayerMeans[nameof(SimulationLayer.SoilOrganicMatter)]).ToArray()),
        soilDepthTrendLast20YearsMPerYear = LinearSlope(lastTwenty.Select(item => (double)item.LayerMeans[nameof(SimulationLayer.SoilDepth)]).ToArray()),
        globalNonFiniteObservations = globalNonFinite
    },
    drainage,
    cycles,
    climate,
    recovery,
    layers = layerAggregates.Values.OrderBy(item => (int)item.Layer).Select(item => item.Snapshot()).ToArray(),
    fluxes = fluxAggregates.Values.OrderBy(item => (int)item.Flux).Select(item => item.Snapshot(options.Years, world.Config.CellCount)).ToArray(),
    vectors = vectorAggregates.Values.OrderBy(item => (int)item.Process).Select(item => item.Snapshot()).ToArray(),
    events = eventAggregates.Values.OrderBy(item => (int)item.Kind).Select(item => item.Snapshot()).ToArray(),
    annual,
    temporal
};

var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Converters = { new JsonStringEnumConverter() }
};
var faunaSuffix = options.Harvesters > 0 ? $"-harvesters{options.Harvesters}" : "";
var climateSuffix = $"-climate{options.ClimateVariability.ToString("0.##", CultureInfo.InvariantCulture)}";
var stem = $"stress-{options.Years}y-{options.Size}x{options.Size}-seed{options.Seed}{faunaSuffix}{climateSuffix}";
var jsonPath = Path.Combine(options.OutputDirectory, stem + ".json");
var csvPath = Path.Combine(options.OutputDirectory, stem + "-annual.csv");
var temporalCsvPath = Path.Combine(options.OutputDirectory, stem + "-temporal.csv");
await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(report, jsonOptions));
await WriteAnnualCsv(csvPath, annual);
await WriteTemporalCsv(temporalCsvPath, temporal);

Console.WriteLine(failures.Count == 0 ? "PASS" : "FAIL");
Console.WriteLine($"Runtime: {stopwatch.Elapsed}");
Console.WriteLine($"JSON: {Path.GetFullPath(jsonPath)}");
Console.WriteLine($"CSV:  {Path.GetFullPath(csvPath)}");
Console.WriteLine($"Temporal CSV: {Path.GetFullPath(temporalCsvPath)}");
if (failures.Count > 0)
{
    foreach (var failure in failures)
    {
        Console.WriteLine($"- {failure}");
    }
}

return failures.Count == 0 ? 0 : 2;

static Dictionary<SimulationLayer, LayerSnapshot> CaptureLayers(FiniteWorld world) =>
    Enum.GetValues<SimulationLayer>().ToDictionary(layer => layer, world.CaptureLayer);

static (float[] X, float[] Y) ReferenceVectors(
    VectorProcess process,
    IReadOnlyDictionary<SimulationLayer, LayerSnapshot> layers,
    VectorProcessSnapshot wind,
    VectorProcessSnapshot drainage,
    int width)
{
    if (process is VectorProcess.HumidityAdvection or VectorProcess.CloudAdvection
        or VectorProcess.AirTemperatureAdvection or VectorProcess.SnowTransport or VectorProcess.DustAdvection)
    {
        return (wind.VectorX, wind.VectorY);
    }

    if (process is VectorProcess.SurfaceRunoff or VectorProcess.SedimentTransport)
    {
        return (drainage.VectorX, drainage.VectorY);
    }

    if (process == VectorProcess.GroundwaterFlow)
    {
        var elevation = layers[SimulationLayer.Elevation].Values;
        var depth = layers[SimulationLayer.SoilDepth].Values;
        var groundwater = layers[SimulationLayer.Groundwater].Values;
        var height = elevation.Length / width;
        var x = new float[elevation.Length];
        var y = new float[elevation.Length];
        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                var index = row * width + column;
                var left = HydraulicHead(elevation, depth, groundwater, row * width + Math.Max(0, column - 1));
                var right = HydraulicHead(elevation, depth, groundwater, row * width + Math.Min(width - 1, column + 1));
                var down = HydraulicHead(elevation, depth, groundwater, Math.Max(0, row - 1) * width + column);
                var up = HydraulicHead(elevation, depth, groundwater, Math.Min(height - 1, row + 1) * width + column);
                x[index] = left - right;
                y[index] = down - up;
            }
        }

        return (x, y);
    }

    return (Array.Empty<float>(), Array.Empty<float>());
}

static float HydraulicHead(float[] elevation, float[] depth, float[] groundwater, int index) =>
    elevation[index] - depth[index] * 0.5f + groundwater[index] * 0.004f;

static DrainageDiagnostics AnalyzeDrainage(LayerSnapshot elevation, VectorProcessSnapshot drainage)
{
    var width = elevation.Width;
    var height = elevation.Height;
    var cells = width * height;
    var outlets = 0;
    var interiorSinks = 0;
    var downhill = 0;
    var uphill = 0;
    var maximumUphill = 0f;
    var routeLengths = new List<int>(cells);
    var loopCells = 0;

    int Target(int index)
    {
        var dx = Math.Sign(drainage.VectorX[index]);
        var dy = Math.Sign(drainage.VectorY[index]);
        if (dx == 0 && dy == 0) return -1;
        var x = index % width + dx;
        var y = index / width + dy;
        return x < 0 || y < 0 || x >= width || y >= height ? -1 : y * width + x;
    }

    for (var index = 0; index < cells; index++)
    {
        var target = Target(index);
        if (target < 0)
        {
            var x = index % width;
            var y = index / width;
            if (x == 0 || y == 0 || x == width - 1 || y == height - 1) outlets++;
            else interiorSinks++;
            continue;
        }

        var rise = elevation.Values[target] - elevation.Values[index];
        if (rise <= 1e-5f) downhill++; else { uphill++; maximumUphill = Math.Max(maximumUphill, rise); }

        var visited = new HashSet<int>();
        var cursor = index;
        var length = 0;
        while (cursor >= 0 && visited.Add(cursor) && length <= cells)
        {
            cursor = Target(cursor);
            length++;
        }

        if (cursor >= 0) loopCells++;
        else routeLengths.Add(length);
    }

    return new DrainageDiagnostics(
        outlets,
        interiorSinks,
        loopCells,
        downhill,
        uphill,
        maximumUphill,
        routeLengths.Count == 0 ? 0 : routeLengths.Average(),
        routeLengths.Count == 0 ? 0 : routeLengths.Max());
}

static List<string> EvaluateFailures(
    IReadOnlyList<AnnualCheckpoint> annual,
    IReadOnlyDictionary<SimulationLayer, LayerAggregate> layers,
    IReadOnlyDictionary<SimulationFlux, FluxAggregate> fluxes,
    IReadOnlyDictionary<VectorProcess, VectorAggregate> vectors,
    DrainageDiagnostics drainage,
    CycleDiagnostics cycles,
    ClimateDiagnostics climate,
    EcologicalRecoveryDiagnostics recovery,
    WorldSummary summary,
    int cellCount)
{
    var failures = new List<string>();
    if (layers.Values.Any(item => item.MaximumNonFinite > 0)) failures.Add("non-finite state values observed");
    if (fluxes.Values.Any(item => item.NonFinite > 0)) failures.Add("non-finite process fluxes observed");
    if (vectors.Values.Any(item => item.NonFinite > 0)) failures.Add("non-finite vector processes observed");
    if (Math.Abs(summary.WaterBudget.RelativeError) >= 0.00005) failures.Add("water ledger exceeds 0.005%");
    if (Math.Abs(summary.NitrogenBudget.RelativeError) >= 0.00005) failures.Add("nitrogen ledger exceeds 0.005%");
    if (layers[SimulationLayer.SurfaceWater].ObservedMaximum >= 320) failures.Add("surface water exceeds 320 mm");
    if (layers[SimulationLayer.Groundwater].ObservedMaximum >= 225) failures.Add("groundwater exceeds 225 mm");
    if (layers[SimulationLayer.Snow].ObservedMaximum >= 900) failures.Add("snow exceeds 900 mm SWE");
    if (layers[SimulationLayer.Precipitation].ObservedMaximum >= 60) failures.Add("precipitation exceeds 60 mm/hour");
    if (layers[SimulationLayer.Wind].ObservedMaximum >= 45) failures.Add("wind exceeds 45 m/s");
    if (layers[SimulationLayer.SurfaceTemperature].ObservedMinimum <= -65
        || layers[SimulationLayer.SurfaceTemperature].ObservedMaximum >= 65)
        failures.Add("surface temperature leaves the qualified -65..65 C envelope");
    if (layers[SimulationLayer.LiveBiomass].ObservedMinimum < -1e-5f) failures.Add("negative live biomass");
    if (layers[SimulationLayer.SoilDepth].ObservedMinimum < 0.079f) failures.Add("soil depth fell below hard floor");
    if (layers[SimulationLayer.SoilCompaction].ObservedMaximum >= 0.95f)
        failures.Add("soil compaction saturates above 95%, erasing readable trail gradients");
    if (annual.Any(item => Math.Abs(item.WaterRelativeError) >= 0.00005)) failures.Add("annual water ledger excursion exceeds 0.005%");
    if (annual.Any(item => Math.Abs(item.NitrogenRelativeError) >= 0.00005)) failures.Add("annual nitrogen ledger excursion exceeds 0.005%");
    if (drainage.LoopCells > 0) failures.Add("dry-terrain drainage contains a loop");
    if (drainage.MaximumUphillStepM > 0.5f) failures.Add("drainage crosses an uphill step larger than 0.5 m");
    if (layers[SimulationLayer.RootWater].MaximumAboveScale > cellCount / 4)
        failures.Add("root-zone water exceeds its readable scale in more than 25% of cells");
    if (layers[SimulationLayer.Precipitation].MaximumPercentile98
        > StateCatalog.Get(SimulationLayer.Precipitation).ScaleMaximum)
        failures.Add("precipitation p98 exceeds its readable scale");
    if (layers[SimulationLayer.LooseSediment].MaximumPercentile98
        > StateCatalog.Get(SimulationLayer.LooseSediment).ScaleMaximum * 1.25f)
        failures.Add("loose-sediment p98 exceeds 125% of its readable scale");
    if (annual.Count > 0)
    {
        var final = annual[^1].LayerMeans;
        if (final[nameof(SimulationLayer.LiveBiomass)] < 20f)
            failures.Add("living biomass collapses below the sustainable-steppe threshold");
        if (final[nameof(SimulationLayer.AvailableNitrogen)] < 0.5f)
            failures.Add("available nitrogen collapses below the nutrient-reserve threshold");
        if (final[nameof(SimulationLayer.SurfaceCrust)] > 0.75f)
            failures.Add("mean surface crust exceeds 75%");
        if (final[nameof(SimulationLayer.SoilCompaction)] > 0.15f)
            failures.Add("mean soil compaction exceeds the 15% sustainable-landscape threshold");
    }
    if (annual.Count >= 20)
    {
        var stabilityWindow = annual.TakeLast(20).ToArray();
        var waterTrend = LinearSlope(stabilityWindow.Select(item => item.StoredWaterMmPerCell).ToArray());
        var biomassTrend = LinearSlope(stabilityWindow
            .Select(item => (double)item.LayerMeans[nameof(SimulationLayer.LiveBiomass)]).ToArray());
        var availableNitrogenTrend = LinearSlope(stabilityWindow
            .Select(item => (double)item.LayerMeans[nameof(SimulationLayer.AvailableNitrogen)]).ToArray());
        var compactionTrend = LinearSlope(stabilityWindow
            .Select(item => (double)item.LayerMeans[nameof(SimulationLayer.SoilCompaction)]).ToArray());
        var variableClimate = climate.MoistureMultiplierRange > 0.2f
            || climate.TemperatureAnomalyRangeC > 2f;
        if (!variableClimate)
        {
            if (Math.Abs(waterTrend) > 0.1)
                failures.Add($"absolute 20-year water-storage trend exceeds 0.1 mm/cell/year ({waterTrend:F4})");
            if (Math.Abs(biomassTrend) > 0.5)
                failures.Add($"absolute 20-year living-biomass trend exceeds 0.5 g/m2/year ({biomassTrend:F4})");
            if (Math.Abs(availableNitrogenTrend) > 0.02)
                failures.Add($"absolute 20-year available-nitrogen trend exceeds 0.02 g/m2/year ({availableNitrogenTrend:F4})");
        }
        else if (annual.Count >= 60)
        {
            var longWindow = annual.Skip(20).ToArray();
            var longWaterTrend = LinearSlope(longWindow.Select(item => item.StoredWaterMmPerCell).ToArray());
            var longBiomassTrend = LinearSlope(longWindow
                .Select(item => (double)item.LayerMeans[nameof(SimulationLayer.LiveBiomass)]).ToArray());
            var longNitrogenTrend = LinearSlope(longWindow
                .Select(item => (double)item.LayerMeans[nameof(SimulationLayer.AvailableNitrogen)]).ToArray());
            if (Math.Abs(longWaterTrend) > 0.6)
                failures.Add($"climate-adjusted 80-year water trend exceeds 0.6 mm/cell/year ({longWaterTrend:F4})");
            if (Math.Abs(longBiomassTrend) > 0.3)
                failures.Add($"climate-adjusted 80-year biomass trend exceeds 0.3 g/m2/year ({longBiomassTrend:F4})");
            if (Math.Abs(longNitrogenTrend) > 0.015)
                failures.Add($"climate-adjusted 80-year available-nitrogen trend exceeds 0.015 g/m2/year ({longNitrogenTrend:F4})");
        }
        if (compactionTrend > 0.005)
            failures.Add($"20-year soil-compaction trend exceeds 0.005/year ({compactionTrend:F4})");
    }
    if (annual.Count >= 80)
    {
        if (climate.TemperatureAnomalyRangeC < 7f)
            failures.Add($"century temperature-anomaly range is too narrow ({climate.TemperatureAnomalyRangeC:F2} C)");
        if (climate.MoistureMultiplierRange < 1f)
            failures.Add($"century moisture-multiplier range is too narrow ({climate.MoistureMultiplierRange:F2})");
        if (climate.WindMultiplierRange < 0.65f)
            failures.Add($"century wind-multiplier range is too narrow ({climate.WindMultiplierRange:F2})");
        if (climate.SevereHeatYears == 0) failures.Add("century contains no severe hot year");
        if (climate.SevereColdYears == 0) failures.Add("century contains no severe cold year");
        if (climate.SevereDroughtYears == 0) failures.Add("century contains no severe drought year");
        if (climate.ExtremeWetYears == 0) failures.Add("century contains no extreme wet year");
        if (climate.SevereWindYears == 0) failures.Add("century contains no severe windy year");
        if (climate.HeatwaveDays == 0) failures.Add("century contains no heatwave episode");
        if (climate.ColdSnapDays == 0) failures.Add("century contains no cold-snap episode");
        if (climate.RainBurstDays == 0) failures.Add("century contains no extreme-rain episode");
        if (climate.WindStormDays == 0) failures.Add("century contains no windstorm episode");
        if (cycles.ConsecutiveYearTemperatureCorrelation > 0.9985)
            failures.Add($"annual temperature shapes remain too repetitive ({cycles.ConsecutiveYearTemperatureCorrelation:F6})");
        if (climate.TemperatureResponseCorrelation < 0.35)
            failures.Add($"surface temperature weakly follows climate anomalies ({climate.TemperatureResponseCorrelation:F3})");
        if (climate.MoistureInputCorrelation < 0.15)
            failures.Add($"water input weakly follows climate moisture regimes ({climate.MoistureInputCorrelation:F3})");
        if (recovery.EvaluableExtremeYears > 0 && recovery.RecoveredWithinFiveYearsFraction < 0.75)
            failures.Add($"ecology recovers after only {recovery.RecoveredWithinFiveYearsFraction:P0} of evaluable extremes");
    }
    return failures;
}

static double LinearSlope(double[] values)
{
    if (values.Length < 2) return 0;
    var meanX = (values.Length - 1) * 0.5;
    var meanY = values.Average();
    double numerator = 0;
    double denominator = 0;
    for (var index = 0; index < values.Length; index++)
    {
        var dx = index - meanX;
        numerator += dx * (values[index] - meanY);
        denominator += dx * dx;
    }

    return denominator > 0 ? numerator / denominator : 0;
}

static CycleDiagnostics AnalyzeCycles(IReadOnlyList<TemporalCheckpoint> checkpoints)
{
    var years = checkpoints.GroupBy(item => item.Year).OrderBy(group => group.Key).ToArray();
    var temperatureCycles = 0;
    var snowCycles = 0;
    var biomassCycles = 0;
    var hydrologyCycles = 0;
    var temperatureAmplitudes = new List<double>();
    var snowAmplitudes = new List<double>();
    var biomassAmplitudes = new List<double>();
    var rootWaterAmplitudes = new List<double>();
    var consecutiveTemperatureCorrelations = new List<double>();
    double[]? previousTemperature = null;
    foreach (var year in years)
    {
        var ordered = year.OrderBy(item => item.DayOfYear).ToArray();
        var temperature = ordered.Select(item => (double)item.LayerMeans[nameof(SimulationLayer.SurfaceTemperature)]).ToArray();
        var snow = ordered.Select(item => (double)item.LayerMeans[nameof(SimulationLayer.Snow)]).ToArray();
        var biomass = ordered.Select(item => (double)item.LayerMeans[nameof(SimulationLayer.LiveBiomass)]).ToArray();
        var rootWater = ordered.Select(item => (double)item.LayerMeans[nameof(SimulationLayer.RootWater)]).ToArray();
        var temperatureAmplitude = temperature.Max() - temperature.Min();
        var snowAmplitude = snow.Max() - snow.Min();
        var biomassAmplitude = biomass.Max() - biomass.Min();
        var rootWaterAmplitude = rootWater.Max() - rootWater.Min();
        temperatureAmplitudes.Add(temperatureAmplitude);
        snowAmplitudes.Add(snowAmplitude);
        biomassAmplitudes.Add(biomassAmplitude);
        rootWaterAmplitudes.Add(rootWaterAmplitude);
        if (temperatureAmplitude >= 15) temperatureCycles++;
        if (snow.Max() >= 1 && snow.Min() <= 0.5) snowCycles++;
        if (biomassAmplitude >= 8) biomassCycles++;
        if (rootWaterAmplitude >= 8) hydrologyCycles++;
        if (previousTemperature is not null && previousTemperature.Length == temperature.Length)
        {
            var correlation = PearsonSeries(previousTemperature, temperature);
            if (double.IsFinite(correlation)) consecutiveTemperatureCorrelations.Add(correlation);
        }
        previousTemperature = temperature;
    }

    return new CycleDiagnostics(
        years.Length,
        temperatureCycles,
        snowCycles,
        biomassCycles,
        hydrologyCycles,
        Median(temperatureAmplitudes),
        Median(snowAmplitudes),
        Median(biomassAmplitudes),
        Median(rootWaterAmplitudes),
        consecutiveTemperatureCorrelations.Count > 0 ? consecutiveTemperatureCorrelations.Average() : 0,
        checkpoints.Count(item => item.LayerMeans[nameof(SimulationLayer.Precipitation)] > 0.01f),
        checkpoints.Count(item => item.LayerMeans[nameof(SimulationLayer.Dust)] > 0.02f));
}

static double PearsonSeries(double[] a, double[] b)
{
    if (a.Length != b.Length || a.Length == 0) return double.NaN;
    var meanA = a.Average();
    var meanB = b.Average();
    double covariance = 0;
    double varianceA = 0;
    double varianceB = 0;
    for (var index = 0; index < a.Length; index++)
    {
        var da = a[index] - meanA;
        var db = b[index] - meanB;
        covariance += da * db;
        varianceA += da * da;
        varianceB += db * db;
    }
    return varianceA > 0 && varianceB > 0 ? covariance / Math.Sqrt(varianceA * varianceB) : double.NaN;
}

static double Median(IReadOnlyList<double> values)
{
    if (values.Count == 0) return 0;
    var sorted = values.Order().ToArray();
    var middle = sorted.Length / 2;
    return sorted.Length % 2 == 0 ? (sorted[middle - 1] + sorted[middle]) * 0.5 : sorted[middle];
}

static ClimateDiagnostics AnalyzeClimate(
    WorldConfig config,
    IReadOnlyList<AnnualCheckpoint> annual,
    IReadOnlyList<TemporalCheckpoint> temporal)
{
    if (annual.Count == 0)
    {
        return new ClimateDiagnostics(
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0);
    }

    var regimes = annual.Select(item => item.Climate).ToArray();
    var heatwaveDays = 0;
    var coldSnapDays = 0;
    var rainBurstDays = 0;
    var windStormDays = 0;
    var maximumStormIntensity = float.NegativeInfinity;
    var maximumWindMultiplier = float.NegativeInfinity;
    var minimumForcingTemperature = float.PositiveInfinity;
    var maximumForcingTemperature = float.NegativeInfinity;
    for (var year = 1; year <= annual.Count; year++)
    {
        for (var day = 1; day <= 365; day++)
        {
            var forcing = ClimateRegimeModel.GetForcing(config, year, day);
            if (forcing.Heatwave) heatwaveDays++;
            if (forcing.ColdSnap) coldSnapDays++;
            if (forcing.RainBurst) rainBurstDays++;
            if (forcing.WindStorm) windStormDays++;
            maximumStormIntensity = Math.Max(maximumStormIntensity, forcing.StormIntensityMultiplier);
            maximumWindMultiplier = Math.Max(maximumWindMultiplier, forcing.WindSpeedMultiplier);
            minimumForcingTemperature = Math.Min(minimumForcingTemperature, forcing.TemperatureOffsetC);
            maximumForcingTemperature = Math.Max(maximumForcingTemperature, forcing.TemperatureOffsetC);
        }
    }

    var annualSurfaceTemperature = annual.Select(item =>
    {
        var samples = temporal.Where(sample => sample.Year == item.Year).ToArray();
        return samples.Length == 0
            ? (double)item.LayerMeans[nameof(SimulationLayer.SurfaceTemperature)]
            : samples.Average(sample => (double)sample.LayerMeans[nameof(SimulationLayer.SurfaceTemperature)]);
    }).ToArray();
    var temperatureResponse = PearsonSeries(
        regimes.Select(item => (double)item.TemperatureAnomalyC).ToArray(),
        annualSurfaceTemperature);
    var moistureInput = PearsonSeries(
        regimes.Select(item => (double)item.MoistureMultiplier).ToArray(),
        annual.Select(item => item.ExternalInputMmPerCell).ToArray());

    return new ClimateDiagnostics(
        annual.Count,
        regimes.Min(item => item.TemperatureAnomalyC),
        regimes.Max(item => item.TemperatureAnomalyC),
        regimes.Max(item => item.TemperatureAnomalyC) - regimes.Min(item => item.TemperatureAnomalyC),
        regimes.Min(item => item.MoistureMultiplier),
        regimes.Max(item => item.MoistureMultiplier),
        regimes.Max(item => item.MoistureMultiplier) - regimes.Min(item => item.MoistureMultiplier),
        regimes.Min(item => item.WindSpeedMultiplier),
        regimes.Max(item => item.WindSpeedMultiplier),
        regimes.Max(item => item.WindSpeedMultiplier) - regimes.Min(item => item.WindSpeedMultiplier),
        regimes.Count(item => item.SevereHeat),
        regimes.Count(item => item.SevereCold),
        regimes.Count(item => item.SevereDrought),
        regimes.Count(item => item.ExtremeWet),
        regimes.Count(item => item.SevereWind),
        heatwaveDays,
        coldSnapDays,
        rainBurstDays,
        windStormDays,
        maximumStormIntensity,
        maximumWindMultiplier,
        minimumForcingTemperature,
        maximumForcingTemperature,
        double.IsFinite(temperatureResponse) ? temperatureResponse : 0,
        double.IsFinite(moistureInput) ? moistureInput : 0);
}

static EcologicalRecoveryDiagnostics AnalyzeRecovery(IReadOnlyList<AnnualCheckpoint> annual)
{
    var recoveryYears = new List<double>();
    var recoveryRatios = new List<double>();
    var evaluable = 0;
    var recovered = 0;
    for (var index = 20; index + 5 < annual.Count; index++)
    {
        var regime = annual[index].Climate;
        if (!regime.SevereDrought && !regime.SevereHeat) continue;

        var baseline = annual.Skip(index - 3).Take(3)
            .Average(item => (double)item.LayerMeans[nameof(SimulationLayer.LiveBiomass)]);
        if (baseline <= 1e-8) continue;
        evaluable++;
        var bestRatio = 0d;
        var yearRecovered = 0;
        for (var offset = 1; offset <= 5; offset++)
        {
            var ratio = annual[index + offset].LayerMeans[nameof(SimulationLayer.LiveBiomass)] / baseline;
            bestRatio = Math.Max(bestRatio, ratio);
            if (yearRecovered == 0 && ratio >= 0.85)
            {
                yearRecovered = offset;
            }
        }

        recoveryRatios.Add(bestRatio);
        if (yearRecovered > 0)
        {
            recovered++;
            recoveryYears.Add(yearRecovered);
        }
    }

    return new EcologicalRecoveryDiagnostics(
        evaluable,
        recovered,
        evaluable > 0 ? recovered / (double)evaluable : 1d,
        recoveryYears.Count > 0 ? Median(recoveryYears) : 0d,
        recoveryRatios.Count > 0 ? recoveryRatios.Min() : 1d,
        recoveryRatios.Count > 0 ? recoveryRatios.Average() : 1d);
}

static async Task WriteTemporalCsv(string path, IReadOnlyList<TemporalCheckpoint> checkpoints)
{
    await using var writer = new StreamWriter(path);
    await writer.WriteLineAsync("year,day,elapsed_hours,temperature_c,precipitation_mm_h,surface_water_mm,root_water_mm,groundwater_mm,snow_mm,live_biomass_gm2,dry_biomass_gm2,soil_compaction,dust_gm2,climate_temperature_offset_c,climate_moisture_multiplier,climate_wind_multiplier,climate_storm_frequency,climate_storm_intensity,climate_phase_shift_days,heatwave,cold_snap,rain_burst,wind_storm");
    foreach (var item in checkpoints)
    {
        string F(double value) => value.ToString("G17", CultureInfo.InvariantCulture);
        await writer.WriteLineAsync(string.Join(',',
            item.Year,
            item.DayOfYear,
            F(item.ElapsedHours),
            F(item.LayerMeans[nameof(SimulationLayer.SurfaceTemperature)]),
            F(item.LayerMeans[nameof(SimulationLayer.Precipitation)]),
            F(item.LayerMeans[nameof(SimulationLayer.SurfaceWater)]),
            F(item.LayerMeans[nameof(SimulationLayer.RootWater)]),
            F(item.LayerMeans[nameof(SimulationLayer.Groundwater)]),
            F(item.LayerMeans[nameof(SimulationLayer.Snow)]),
            F(item.LayerMeans[nameof(SimulationLayer.LiveBiomass)]),
            F(item.LayerMeans[nameof(SimulationLayer.DryBiomass)]),
            F(item.LayerMeans[nameof(SimulationLayer.SoilCompaction)]),
            F(item.LayerMeans[nameof(SimulationLayer.Dust)]),
            F(item.Climate.TemperatureOffsetC),
            F(item.Climate.MoistureMultiplier),
            F(item.Climate.WindSpeedMultiplier),
            F(item.Climate.StormFrequencyMultiplier),
            F(item.Climate.StormIntensityMultiplier),
            F(item.Climate.SeasonPhaseShiftDays),
            item.Climate.Heatwave ? 1 : 0,
            item.Climate.ColdSnap ? 1 : 0,
            item.Climate.RainBurst ? 1 : 0,
            item.Climate.WindStorm ? 1 : 0));
    }
}

static async Task WriteAnnualCsv(string path, IReadOnlyList<AnnualCheckpoint> annual)
{
    await using var writer = new StreamWriter(path);
    await writer.WriteLineAsync("year,stored_water_mm_per_cell,input_mm_per_cell,output_mm_per_cell,water_error,nitrogen_gm2_per_cell,nitrogen_error,climate_temperature_anomaly_c,climate_moisture_multiplier,climate_wind_multiplier,climate_storm_frequency,climate_storm_intensity,climate_phase_shift_days,severe_heat,severe_cold,severe_drought,extreme_wet,severe_wind,temperature_c,surface_water_mm,root_water_mm,groundwater_mm,snow_mm,live_biomass_gm2,dry_biomass_gm2,available_nitrogen_gm2,plant_nitrogen_gm2,organic_nitrogen_gm2,soil_compaction,soil_depth_m,dust_gm2,non_finite");
    foreach (var item in annual)
    {
        string F(double value) => value.ToString("G17", CultureInfo.InvariantCulture);
        await writer.WriteLineAsync(string.Join(',',
            item.Year,
            F(item.StoredWaterMmPerCell), F(item.ExternalInputMmPerCell), F(item.ExternalOutputMmPerCell), F(item.WaterRelativeError),
            F(item.StoredNitrogenGm2PerCell), F(item.NitrogenRelativeError),
            F(item.Climate.TemperatureAnomalyC),
            F(item.Climate.MoistureMultiplier),
            F(item.Climate.WindSpeedMultiplier),
            F(item.Climate.StormFrequencyMultiplier),
            F(item.Climate.StormIntensityMultiplier),
            F(item.Climate.SeasonPhaseShiftDays),
            item.Climate.SevereHeat ? 1 : 0,
            item.Climate.SevereCold ? 1 : 0,
            item.Climate.SevereDrought ? 1 : 0,
            item.Climate.ExtremeWet ? 1 : 0,
            item.Climate.SevereWind ? 1 : 0,
            F(item.LayerMeans[nameof(SimulationLayer.SurfaceTemperature)]),
            F(item.LayerMeans[nameof(SimulationLayer.SurfaceWater)]),
            F(item.LayerMeans[nameof(SimulationLayer.RootWater)]),
            F(item.LayerMeans[nameof(SimulationLayer.Groundwater)]),
            F(item.LayerMeans[nameof(SimulationLayer.Snow)]),
            F(item.LayerMeans[nameof(SimulationLayer.LiveBiomass)]),
            F(item.LayerMeans[nameof(SimulationLayer.DryBiomass)]),
            F(item.LayerMeans[nameof(SimulationLayer.AvailableNitrogen)]),
            F(item.LayerMeans[nameof(SimulationLayer.PlantNitrogen)]),
            F(item.LayerMeans[nameof(SimulationLayer.OrganicNitrogen)]),
            F(item.LayerMeans[nameof(SimulationLayer.SoilCompaction)]),
            F(item.LayerMeans[nameof(SimulationLayer.SoilDepth)]),
            F(item.LayerMeans[nameof(SimulationLayer.Dust)]),
            item.NonFiniteCount));
    }
}

internal sealed class LayerAggregate(SimulationLayer layer)
{
    private double meanSum;
    private int observations;
    public SimulationLayer Layer { get; } = layer;
    public float ObservedMinimum { get; private set; } = float.PositiveInfinity;
    public float ObservedMaximum { get; private set; } = float.NegativeInfinity;
    public float MinimumMean { get; private set; } = float.PositiveInfinity;
    public float MaximumMean { get; private set; } = float.NegativeInfinity;
    public float MaximumPercentile98 { get; private set; } = float.NegativeInfinity;
    public int MaximumBelowScale { get; private set; }
    public int MaximumAboveScale { get; private set; }
    public int MaximumNonFinite { get; private set; }

    public void Observe(LayerSnapshot snapshot)
    {
        observations++;
        meanSum += snapshot.Statistics.Mean;
        ObservedMinimum = Math.Min(ObservedMinimum, snapshot.Minimum);
        ObservedMaximum = Math.Max(ObservedMaximum, snapshot.Maximum);
        MinimumMean = Math.Min(MinimumMean, snapshot.Statistics.Mean);
        MaximumMean = Math.Max(MaximumMean, snapshot.Statistics.Mean);
        MaximumPercentile98 = Math.Max(MaximumPercentile98, snapshot.Statistics.Percentile98);
        MaximumBelowScale = Math.Max(MaximumBelowScale, snapshot.Statistics.BelowScaleCount);
        MaximumAboveScale = Math.Max(MaximumAboveScale, snapshot.Statistics.AboveScaleCount);
        MaximumNonFinite = Math.Max(MaximumNonFinite, snapshot.Statistics.NonFiniteCount);
    }

    public object Snapshot() => new
    {
        Layer,
        observations,
        observedMinimum = ObservedMinimum,
        observedMaximum = ObservedMaximum,
        minimumMean = MinimumMean,
        maximumMean = MaximumMean,
        meanAcrossObservations = observations > 0 ? meanSum / observations : 0,
        maximumPercentile98 = MaximumPercentile98,
        maximumBelowScale = MaximumBelowScale,
        maximumAboveScale = MaximumAboveScale,
        maximumNonFinite = MaximumNonFinite
    };
}

internal sealed class FluxAggregate(SimulationFlux flux)
{
    private double signedTotal;
    private double absoluteTotal;
    private double positiveTotal;
    private double negativeTotal;
    private long activeCellSamples;
    private long cellSamples;
    private int observations;
    public SimulationFlux Flux { get; } = flux;
    public long NonFinite { get; private set; }
    public double MaximumAbsolutePerObservation { get; private set; }

    public void Observe(FluxSnapshot snapshot, int cells)
    {
        observations++;
        signedTotal += snapshot.SignedTotal;
        absoluteTotal += snapshot.AbsoluteTotal;
        positiveTotal += snapshot.PositiveTotal;
        negativeTotal += snapshot.NegativeTotal;
        activeCellSamples += snapshot.ActiveCellCount;
        cellSamples += cells;
        NonFinite += snapshot.NonFiniteCount;
        MaximumAbsolutePerObservation = Math.Max(MaximumAbsolutePerObservation, snapshot.AbsoluteTotal);
    }

    public object Snapshot(int years, int cells) => new
    {
        Flux,
        descriptor = FluxCatalog.Get(Flux),
        observations,
        signedTotal,
        absoluteTotal,
        positiveTotal,
        negativeTotal,
        annualAbsolutePerCell = absoluteTotal / Math.Max(1, years * cells),
        activeCellFraction = cellSamples > 0 ? activeCellSamples / (double)cellSamples : 0,
        maximumAbsolutePerObservation = MaximumAbsolutePerObservation,
        nonFinite = NonFinite
    };
}

internal sealed class VectorAggregate(VectorProcess process)
{
    private double totalMagnitude;
    private double totalGrossMagnitude;
    private double weightedAlignment;
    private double alignmentWeight;
    private long activeCellSamples;
    private long cellSamples;
    private int observations;
    private double meanWindX;
    private double meanWindY;
    private long windVectorSamples;
    private long westwardSamples;
    private long extremeWindSamples;
    private double pressureAlignmentSum;
    private long pressureAlignmentSamples;
    private double slopeCorrelationSum;
    private int slopeCorrelationSamples;
    public VectorProcess Process { get; } = process;
    public float MaximumMagnitude { get; private set; }
    public long NonFinite { get; private set; }

    public void Observe(VectorProcessSnapshot snapshot, float[] referenceX, float[] referenceY, bool axisOnly)
    {
        observations++;
        totalMagnitude += snapshot.TotalMagnitude;
        totalGrossMagnitude += snapshot.TotalGrossMagnitude;
        activeCellSamples += snapshot.ActiveCellCount;
        cellSamples += snapshot.Magnitude.Length;
        MaximumMagnitude = Math.Max(MaximumMagnitude, snapshot.MaximumMagnitude);
        NonFinite += snapshot.NonFiniteCount;
        if (referenceX.Length != snapshot.VectorX.Length) return;
        for (var index = 0; index < snapshot.VectorX.Length; index++)
        {
            var magnitude = snapshot.Magnitude[index];
            var referenceMagnitude = MathF.Sqrt(referenceX[index] * referenceX[index] + referenceY[index] * referenceY[index]);
            var weight = snapshot.GrossMagnitude[index];
            if (magnitude <= 1e-8f || referenceMagnitude <= 1e-8f || weight <= 1e-8f) continue;
            var cosine = (snapshot.VectorX[index] * referenceX[index] + snapshot.VectorY[index] * referenceY[index])
                / (magnitude * referenceMagnitude);
            weightedAlignment += (axisOnly ? Math.Abs(cosine) : cosine) * weight;
            alignmentWeight += weight;
        }
    }

    public void ObserveAuxiliaryWind(VectorProcessSnapshot wind, float[] pressure, float[] slope, int width, int height)
    {
        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                var index = row * width + column;
                meanWindX += wind.VectorX[index];
                meanWindY += wind.VectorY[index];
                windVectorSamples++;
                if (wind.VectorX[index] < 0) westwardSamples++;
                if (wind.Magnitude[index] > 20) extremeWindSamples++;
                var left = pressure[row * width + Math.Max(0, column - 1)];
                var right = pressure[row * width + Math.Min(width - 1, column + 1)];
                var down = pressure[Math.Max(0, row - 1) * width + column];
                var up = pressure[Math.Min(height - 1, row + 1) * width + column];
                var gradientX = left - right;
                var gradientY = down - up;
                var gradientMagnitude = MathF.Sqrt(gradientX * gradientX + gradientY * gradientY);
                if (gradientMagnitude <= 1e-6f || wind.Magnitude[index] <= 1e-6f) continue;
                pressureAlignmentSum += (wind.VectorX[index] * gradientX + wind.VectorY[index] * gradientY)
                    / (wind.Magnitude[index] * gradientMagnitude);
                pressureAlignmentSamples++;
            }
        }

        var correlation = Pearson(wind.Magnitude, slope);
        if (double.IsFinite(correlation))
        {
            slopeCorrelationSum += correlation;
            slopeCorrelationSamples++;
        }
    }

    public object Snapshot() => new
    {
        Process,
        descriptor = VectorProcessCatalog.Get(Process),
        observations,
        totalMagnitude,
        totalGrossMagnitude,
        directionalCoherence = totalGrossMagnitude > 0 ? totalMagnitude / totalGrossMagnitude : 0,
        weightedAlignment = alignmentWeight > 0 ? weightedAlignment / alignmentWeight : (double?)null,
        activeCellFraction = cellSamples > 0 ? activeCellSamples / (double)cellSamples : 0,
        maximumMagnitude = MaximumMagnitude,
        nonFinite = NonFinite,
        wind = Process == VectorProcess.Wind ? new
        {
            meanX = windVectorSamples > 0 ? meanWindX / windVectorSamples : 0,
            meanY = windVectorSamples > 0 ? meanWindY / windVectorSamples : 0,
            westwardFraction = windVectorSamples > 0 ? westwardSamples / (double)windVectorSamples : 0,
            above20MsFraction = windVectorSamples > 0 ? extremeWindSamples / (double)windVectorSamples : 0,
            pressureGradientAlignment = pressureAlignmentSamples > 0 ? pressureAlignmentSum / pressureAlignmentSamples : 0,
            slopeSpeedCorrelation = slopeCorrelationSamples > 0 ? slopeCorrelationSum / slopeCorrelationSamples : 0
        } : null
    };

    private static double Pearson(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return double.NaN;
        var meanA = a.Average(value => (double)value);
        var meanB = b.Average(value => (double)value);
        double covariance = 0;
        double varianceA = 0;
        double varianceB = 0;
        for (var index = 0; index < a.Length; index++)
        {
            var da = a[index] - meanA;
            var db = b[index] - meanB;
            covariance += da * db;
            varianceA += da * da;
            varianceB += db * db;
        }

        return varianceA > 0 && varianceB > 0 ? covariance / Math.Sqrt(varianceA * varianceB) : double.NaN;
    }
}

internal sealed class EventAggregate(WorldEventKind kind)
{
    private int count;
    private float maximumSeverity;
    private float maximumIndicator;
    public WorldEventKind Kind { get; } = kind;

    public void Observe(WorldRegimeEvent episode)
    {
        count++;
        RefreshPeak(episode);
    }

    public void RefreshPeak(WorldRegimeEvent episode)
    {
        maximumSeverity = Math.Max(maximumSeverity, episode.PeakSeverity);
        maximumIndicator = Math.Max(maximumIndicator, episode.PeakIndicator);
    }

    public object Snapshot() => new { Kind, count, maximumSeverity, maximumIndicator };
}

internal sealed record AnnualCheckpoint(
    int Year,
    double StoredWaterMmPerCell,
    double ExternalInputMmPerCell,
    double ExternalOutputMmPerCell,
    double WaterRelativeError,
    double StoredNitrogenGm2PerCell,
    double NitrogenRelativeError,
    AnnualClimateRegime Climate,
    Dictionary<string, float> LayerMeans,
    Dictionary<string, float> LayerMaxima,
    int NonFiniteCount);

internal sealed record TemporalCheckpoint(
    int Year,
    int DayOfYear,
    double ElapsedHours,
    Dictionary<string, float> LayerMeans,
    Dictionary<string, float> LayerPercentile02,
    Dictionary<string, float> LayerPercentile98,
    Dictionary<string, float> LayerMaxima,
    ClimateForcingSnapshot Climate);

internal sealed record CycleDiagnostics(
    int CompleteAnnualCycles,
    int TemperatureCycles,
    int SnowCycles,
    int BiomassCycles,
    int HydrologyCycles,
    double MedianTemperatureAmplitudeC,
    double MedianSnowAmplitudeMm,
    double MedianBiomassAmplitudeGm2,
    double MedianRootWaterAmplitudeMm,
    double ConsecutiveYearTemperatureCorrelation,
    int WetObservationWindows,
    int DustyObservationWindows);

internal sealed record ClimateDiagnostics(
    int Years,
    float MinimumTemperatureAnomalyC,
    float MaximumTemperatureAnomalyC,
    float TemperatureAnomalyRangeC,
    float MinimumMoistureMultiplier,
    float MaximumMoistureMultiplier,
    float MoistureMultiplierRange,
    float MinimumWindMultiplier,
    float MaximumWindMultiplier,
    float WindMultiplierRange,
    int SevereHeatYears,
    int SevereColdYears,
    int SevereDroughtYears,
    int ExtremeWetYears,
    int SevereWindYears,
    int HeatwaveDays,
    int ColdSnapDays,
    int RainBurstDays,
    int WindStormDays,
    float MaximumStormIntensityMultiplier,
    float MaximumForcingWindMultiplier,
    float MinimumForcingTemperatureOffsetC,
    float MaximumForcingTemperatureOffsetC,
    double TemperatureResponseCorrelation,
    double MoistureInputCorrelation);

internal sealed record EcologicalRecoveryDiagnostics(
    int EvaluableExtremeYears,
    int RecoveredWithinFiveYears,
    double RecoveredWithinFiveYearsFraction,
    double MedianRecoveryYears,
    double WorstFiveYearRecoveryRatio,
    double MeanFiveYearRecoveryRatio);

internal sealed record DrainageDiagnostics(
    int OutletCells,
    int InteriorSinks,
    int LoopCells,
    int DownhillLinks,
    int UphillLinks,
    float MaximumUphillStepM,
    double MeanRouteLengthCells,
    int MaximumRouteLengthCells);

internal sealed record StressOptions(
    int Years,
    int Size,
    int Seed,
    int StepMinutes,
    int SampleDays,
    int ErosionPasses,
    int Harvesters,
    float ClimateVariability,
    string OutputDirectory)
{
    public static StressOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index += 2)
        {
            if (index + 1 >= args.Length || !args[index].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException("Arguments must be --name value pairs.");
            }

            values[args[index][2..]] = args[index + 1];
        }

        int Read(string name, int fallback) => values.TryGetValue(name, out var value)
            ? int.Parse(value, CultureInfo.InvariantCulture)
            : fallback;
        float ReadFloat(string name, float fallback) => values.TryGetValue(name, out var value)
            ? float.Parse(value, CultureInfo.InvariantCulture)
            : fallback;
        return new StressOptions(
            Read("years", 100),
            Read("size", 32),
            Read("seed", 123),
            Read("step-minutes", 180),
            Read("sample-days", 15),
            Read("erosion-passes", 8),
            Read("harvesters", 10),
            ReadFloat("climate-variability", 1f),
            values.GetValueOrDefault("output", Path.Combine("WorldModel", "StressResults")));
    }
}
