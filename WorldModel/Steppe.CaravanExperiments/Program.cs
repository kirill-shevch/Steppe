using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Steppe.CaravanSimulation;
using Steppe.Simulation;

var options = ExperimentOptions.Parse(args);
Directory.CreateDirectory(options.OutputDirectory);
var combinations = options.Matrix
    ? (from seed in options.Seeds
       from morphology in options.Morphologies
       from policy in options.Policies
       select new ExperimentCase(seed, morphology, policy)).ToArray()
    : [new ExperimentCase(options.Seeds[0], options.Morphologies[0], options.Policies[0])];

Console.WriteLine(
    $"Caravan experiments: {combinations.Length} case(s), {options.Years} year(s), "
    + $"{options.Size}x{options.Size}, {options.TickHours:0.###}-hour tick");
var reports = new List<CaravanExperimentReport>(combinations.Length);
foreach (var experiment in combinations)
{
    Console.WriteLine($"RUN  seed {experiment.Seed}, {experiment.Morphology}, {experiment.Policy}");
    var report = await RunExperiment(options, experiment);
    reports.Add(report);
    Console.WriteLine(
        $"{(report.Qualification.Passed ? "PASS" : "FAIL")} "
        + $"{report.FinalState.Blueprint}/{report.Policy}: "
        + $"{report.FinalState.DistanceKilometers:0.0} km, "
        + $"{report.NicheKinds} niches, mode {report.FinalState.OperatingMode}");
}

if (options.Matrix)
{
    var summary = BuildMatrixSummary(options, reports);
    var path = Path.Combine(options.OutputDirectory, "matrix-summary.json");
    await File.WriteAllTextAsync(path, JsonSerializer.Serialize(summary, ExperimentJson.Options));
    Console.WriteLine($"Matrix summary: {Path.GetFullPath(path)}");
}

return reports.All(item => item.Qualification.Passed) || options.Matrix ? 0 : 1;

static async Task<CaravanExperimentReport> RunExperiment(
    ExperimentOptions options,
    ExperimentCase experiment)
{
    var config = new WorldConfig
    {
        Width = options.Size,
        Height = options.Size,
        CellSizeMeters = options.CellSizeMeters,
        Seed = experiment.Seed,
        BaseStepMinutes = options.WorldStepMinutes,
        GiantHarvesterCount = options.HarvesterCount,
        ClimateVariability = options.ClimateVariability,
        WildfireEnabled = options.WildfireEnabled
    };
    var world = new FiniteWorld(config);
    var caravan = new CaravanSimulation(
        CaravanBlueprint.Create(experiment.Morphology),
        config.Width / 2f,
        config.Height / 2f);
    var coupled = new CoupledCaravanSimulation(
        world,
        caravan,
        CaravanPolicyCatalog.Create(experiment.Policy),
        options.TickHours,
        Math.Min(options.ScoutRadiusCells, Math.Max(config.Width, config.Height)));
    var stem = $"caravan-{options.Years}y-{options.Size}x{options.Size}-seed{experiment.Seed}"
        + $"-{Slug(experiment.Morphology)}-{Slug(experiment.Policy)}";
    var csvPath = Path.Combine(options.OutputDirectory, stem + "-daily.csv");
    var seasonalPath = Path.Combine(options.OutputDirectory, stem + "-seasonal.csv");
    var seasonalOrgansPath = Path.Combine(options.OutputDirectory, stem + "-seasonal-organs.csv");
    var jsonPath = Path.Combine(options.OutputDirectory, stem + ".json");

    await using var writer = new StreamWriter(csvPath);
    await writer.WriteLineAsync(
        "day,year,day_of_year,x,y,distance_km,water_l,snow_l,wet_organic_kg,dry_organic_kg,"
        + "organic_nitrogen_kg,structural_nitrogen_kg,electricity_kwh,heat_kwh,mass_kg,"
        + "water_deficit_hours,organic_deficit_hours,"
        + "thermal_deficit_hours,niche_count,mode,hibernation_reason,hibernation_hours,"
        + "max_ledger_error,actions");

    var totalSteps = checked((int)Math.Ceiling(options.Years * 365d * 24d / options.TickHours));
    var actionCounts = new Dictionary<string, int>(StringComparer.Ordinal);
    var maximumLedgerError = 0f;
    var waterScarcityHours = 0d;
    var organicScarcityHours = 0d;
    var nitrogenScarcityHours = 0d;
    var thermalScarcityHours = 0d;
    var currentWaterStreak = 0d;
    var currentOrganicStreak = 0d;
    var currentNitrogenStreak = 0d;
    var currentThermalStreak = 0d;
    var maximumWaterStreak = 0d;
    var maximumOrganicStreak = 0d;
    var maximumNitrogenStreak = 0d;
    var maximumThermalStreak = 0d;
    var extrema = new CaravanExtremaAccumulator(coupled.CaptureCaravan());
    var seasonal = new CaravanSeasonalAccumulator();
    var elapsed = 0d;
    var nextDay = 24d;
    var simulatedDays = 0;
    double? firstHibernationAtHours = null;
    for (var step = 0; step < totalSteps; step++)
    {
        var result = coupled.Advance();
        elapsed += result.Hours;
        extrema.Observe(result);
        seasonal.Observe(result);
        maximumLedgerError = Math.Max(maximumLedgerError, result.Ledger.MaximumAbsoluteError);
        foreach (var action in result.Actions)
        {
            actionCounts[action.Action] = actionCounts.GetValueOrDefault(action.Action) + 1;
        }
        if (result.WaterFulfillment < 0.999f)
        {
            waterScarcityHours += result.Hours;
            currentWaterStreak += result.Hours;
            maximumWaterStreak = Math.Max(maximumWaterStreak, currentWaterStreak);
        }
        else currentWaterStreak = 0d;
        if (result.OrganicFulfillment < 0.999f)
        {
            organicScarcityHours += result.Hours;
            currentOrganicStreak += result.Hours;
            maximumOrganicStreak = Math.Max(maximumOrganicStreak, currentOrganicStreak);
        }
        else currentOrganicStreak = 0d;
        if (result.NitrogenFulfillment < 0.999f)
        {
            nitrogenScarcityHours += result.Hours;
            currentNitrogenStreak += result.Hours;
            maximumNitrogenStreak = Math.Max(maximumNitrogenStreak, currentNitrogenStreak);
        }
        else currentNitrogenStreak = 0d;
        if (result.ThermalFulfillment < 0.999f)
        {
            thermalScarcityHours += result.Hours;
            currentThermalStreak += result.Hours;
            maximumThermalStreak = Math.Max(maximumThermalStreak, currentThermalStreak);
        }
        else currentThermalStreak = 0d;
        if (result.Snapshot.OperatingMode == CaravanOperatingMode.Hibernating
            && firstHibernationAtHours is null)
            firstHibernationAtHours = elapsed;

        if (elapsed + 1e-7d >= nextDay || step == totalSteps - 1)
        {
            simulatedDays++;
            var snapshot = result.Snapshot;
            var actionSummary = string.Join(
                '|',
                result.Actions.Select(item => item.Action).Distinct(StringComparer.Ordinal));
            await writer.WriteLineAsync(string.Join(',', new[]
            {
                simulatedDays.ToString(CultureInfo.InvariantCulture),
                result.Observation.Year.ToString(CultureInfo.InvariantCulture),
                result.Observation.DayOfYear.ToString(CultureInfo.InvariantCulture),
                F(snapshot.XCells),
                F(snapshot.YCells),
                F(snapshot.DistanceKilometers),
                F(snapshot.WaterLiters),
                F(snapshot.SnowWaterLiters),
                F(snapshot.WetOrganicDryKg),
                F(snapshot.DryOrganicKg),
                F(snapshot.OrganicNitrogenKg),
                F(snapshot.StructuralNitrogenKg),
                F(snapshot.StoredElectricityKwh),
                F(snapshot.StoredHeatKwh),
                F(snapshot.TotalMassKg),
                F(snapshot.WaterDeficitHours),
                F(snapshot.OrganicDeficitHours),
                F(snapshot.ThermalDeficitHours),
                snapshot.RegimeHours.Count(item => item.Value > 0d).ToString(CultureInfo.InvariantCulture),
                snapshot.OperatingMode.ToString(),
                snapshot.HibernationReason.ToString(),
                F(snapshot.CumulativeHibernationHours),
                F(maximumLedgerError),
                Csv(actionSummary)
            }));
            nextDay += 24d;
        }
    }

    var final = coupled.CaptureCaravan();
    var worldSummary = world.GetSummary();
    var seasonalStatistics = seasonal.CapturePeriods();
    var seasonalProfile = seasonal.CaptureProfile();
    var seasonalOscillation = BuildSeasonalOscillation(seasonalProfile);
    var usedOrgans = final.Organs.Values.Count(item => item.ActiveHours > 1d);
    var nicheKinds = final.RegimeHours.Count(item => item.Value > 0d);
    var qualification = Qualify(
        final,
        worldSummary,
        options,
        maximumLedgerError,
        usedOrgans,
        nicheKinds,
        actionCounts,
        waterScarcityHours,
        organicScarcityHours,
        thermalScarcityHours);
    var actionFrequencyPerYear = actionCounts.ToDictionary(
        item => item.Key,
        item => item.Value / (double)options.Years,
        StringComparer.Ordinal);
    var activityFrequencyPerYear = final.ActivitySteps.ToDictionary(
        item => item.Key,
        item => item.Value / (double)options.Years);
    var organActiveFraction = final.Organs.ToDictionary(
        item => item.Key,
        item => item.Value.ActiveHours / Math.Max(1d, final.SimulatedHours));
    var report = new CaravanExperimentReport(
        experiment.Seed,
        experiment.Morphology,
        experiment.Policy.ToString(),
        options.Years,
        options.Size,
        options.CellSizeMeters,
        options.TickHours,
        totalSteps,
        firstHibernationAtHours,
        waterScarcityHours,
        organicScarcityHours,
        nitrogenScarcityHours,
        thermalScarcityHours,
        maximumWaterStreak,
        maximumOrganicStreak,
        maximumNitrogenStreak,
        maximumThermalStreak,
        maximumLedgerError,
        usedOrgans,
        nicheKinds,
        actionCounts,
        actionFrequencyPerYear,
        activityFrequencyPerYear,
        organActiveFraction,
        extrema.Capture(),
        seasonalStatistics,
        seasonalProfile,
        seasonalOscillation,
        final,
        worldSummary.WaterBudget.RelativeError,
        worldSummary.NitrogenBudget.RelativeError,
        qualification);
    await WriteSeasonalCsv(seasonalPath, seasonalStatistics);
    await WriteSeasonalOrgansCsv(seasonalOrgansPath, seasonalStatistics);
    await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(report, ExperimentJson.Options));
    return report;
}

static CaravanQualification Qualify(
    CaravanStateSnapshot final,
    WorldSummary world,
    ExperimentOptions options,
    float maximumLedgerError,
    int usedOrgans,
    int nicheKinds,
    IReadOnlyDictionary<string, int> actions,
    double waterScarcityHours,
    double organicScarcityHours,
    double thermalScarcityHours)
{
    var failures = new List<string>();
    if (maximumLedgerError > 0.05f) failures.Add($"internal ledger error {maximumLedgerError}");
    if (Math.Abs(world.WaterBudget.RelativeError) > 5e-5)
        failures.Add($"world water ledger error {world.WaterBudget.RelativeError}");
    var nitrogenTolerance = Math.Max(
        5e-5,
        options.Years
        * 3e-6
        * (60d / options.WorldStepMinutes)
        * Math.Sqrt(64d / (options.Size * options.Size)));
    if (Math.Abs(world.NitrogenBudget.RelativeError) > nitrogenTolerance)
        failures.Add(
            $"world nitrogen ledger error {world.NitrogenBudget.RelativeError} "
            + $"exceeds drift allowance {nitrogenTolerance}");
    if (options.Years >= 1 && usedOrgans < 7) failures.Add($"only {usedOrgans} organs were used");
    if (options.Years >= 1 && nicheKinds < 3) failures.Add($"only {nicheKinds} physical niches were observed");
    if (options.Years >= 1 && actions.Count < 6) failures.Add($"only {actions.Count} action kinds occurred");
    if (options.Years >= 1
        && waterScarcityHours + organicScarcityHours + thermalScarcityHours <= 0d)
        failures.Add("the survival balance produced no resource or thermal scarcity");
    return new CaravanQualification(failures.Count == 0, failures.ToArray());
}

static CaravanMatrixSummary BuildMatrixSummary(
    ExperimentOptions options,
    IReadOnlyList<CaravanExperimentReport> reports)
{
    var winners = reports
        .GroupBy(item => item.Seed)
        .Select(group => group
            .OrderByDescending(Score)
            .ThenBy(item => item.Morphology)
            .ThenBy(item => item.Policy, StringComparer.Ordinal)
            .First())
        .ToArray();
    var winnerKeys = winners
        .Select(item => $"{item.Morphology}/{item.Policy}")
        .Distinct(StringComparer.Ordinal)
        .ToArray();
    return new CaravanMatrixSummary(
        options.Years,
        options.Size,
        reports.Count,
        reports.Count(item => item.FinalState.OperatingMode == CaravanOperatingMode.Active),
        reports.Select(item => item.Morphology).Distinct().Count(),
        reports.Select(item => item.Policy).Distinct(StringComparer.Ordinal).Count(),
        reports.Select(item => DominantProfile(item.FinalState)).Distinct(StringComparer.Ordinal).Count(),
        winnerKeys.Length == 1 ? winnerKeys[0] : null,
        winnerKeys,
        reports.Select(item => new CaravanMatrixCase(
            item.Seed,
            item.Morphology,
            item.Policy,
            item.FinalState.OperatingMode == CaravanOperatingMode.Active,
            item.FinalState.CumulativeHibernationHours
                / Math.Max(1d, item.FinalState.SimulatedHours),
            item.FinalState.DistanceKilometers,
            item.NicheKinds,
            DominantProfile(item.FinalState),
            Score(item),
            item.Qualification.Passed)).ToArray());
}

static double Score(CaravanExperimentReport report)
{
    var activeFraction = 1d - report.FinalState.CumulativeHibernationHours
        / Math.Max(1d, report.FinalState.SimulatedHours);
    return activeFraction * 1000d
        + report.NicheKinds * 35d
        + Math.Min(200d, report.FinalState.DistanceKilometers * 0.05d)
        + report.UsedOrgans * 8d
        - report.WaterScarcityHours * 0.01d
        - report.OrganicScarcityHours * 0.01d;
}

static string DominantProfile(CaravanStateSnapshot state)
{
    var electric = state.CumulativeSolarElectricityKwh >= state.CumulativeFurnaceElectricityKwh
        ? "solar"
        : "biomass";
    var traction = state.CumulativeSailMechanicalKwh >= state.CumulativeMotorMechanicalKwh
        ? "sail"
        : "motor";
    var water = state.CumulativeSurfaceWaterLiters >= state.CumulativeSnowWaterLiters
        ? "surface-water"
        : "snow";
    return $"{electric}/{traction}/{water}";
}

static CaravanSeasonalOscillation BuildSeasonalOscillation(
    IReadOnlyList<CaravanSeasonStatistics> profile)
{
    if (profile.Count == 0) return CaravanSeasonalOscillation.Empty;
    static float Amplitude(IEnumerable<float> values)
    {
        var array = values.ToArray();
        return array.Max() - array.Min();
    }

    var varyingActivities = Enum.GetValues<CaravanActivity>()
        .Where(activity =>
        {
            var rates = profile.Select(item =>
                item.ActivitySteps.GetValueOrDefault(activity) * 1000d / Math.Max(1d, item.Hours)).ToArray();
            return rates.Max() - rates.Min() >= 0.5d
                && rates.Max() >= Math.Max(1d, rates.Min() * 1.15d);
        })
        .ToArray();
    var varyingOrgans = Enum.GetValues<CaravanOrganKind>()
        .Where(organ => Amplitude(profile.Select(item => item.Organs[organ].MeanUsage)) >= 0.03f)
        .ToArray();
    var movementRates = profile.Select(item =>
        (float)(item.DistanceKilometers * 1000d / Math.Max(1d, item.Hours))).ToArray();
    var hibernationFractions = profile.Select(item =>
        (float)(item.HibernationHours / Math.Max(1d, item.Hours))).ToArray();
    return new CaravanSeasonalOscillation(
        Amplitude(profile.Select(item => item.WaterLiters.Mean)),
        Amplitude(profile.Select(item => item.WetOrganicDryKg.Mean + item.DryOrganicKg.Mean)),
        Amplitude(profile.Select(item => item.OrganicNitrogenKg.Mean)),
        Amplitude(profile.Select(item => item.StructuralNitrogenKg.Mean)),
        Amplitude(profile.Select(item => item.ElectricityKwh.Mean)),
        Amplitude(profile.Select(item => item.StoredHeatKwh.Mean)),
        Amplitude(profile.Select(item => item.BodyTemperatureC.Mean)),
        Amplitude(profile.Select(item => item.TotalMassKg.Mean)),
        movementRates.Max() - movementRates.Min(),
        hibernationFractions.Max() - hibernationFractions.Min(),
        varyingActivities,
        varyingOrgans);
}

static async Task WriteSeasonalCsv(
    string path,
    IReadOnlyList<CaravanSeasonStatistics> seasons)
{
    await using var writer = new StreamWriter(path);
    await writer.WriteLineAsync(
        "year,season,hours,hibernation_hours,distance_km,water_scarcity_hours,"
        + "organic_scarcity_hours,nitrogen_scarcity_hours,thermal_scarcity_hours,"
        + "water_min,water_mean,water_max,"
        + "organic_min,organic_mean,organic_max,electricity_min,electricity_mean,electricity_max,"
        + "organic_nitrogen_min,organic_nitrogen_mean,organic_nitrogen_max,"
        + "structural_nitrogen_min,structural_nitrogen_mean,structural_nitrogen_max,"
        + "heat_min,heat_mean,heat_max,body_temperature_min,body_temperature_mean,"
        + "body_temperature_max,total_mass_min,total_mass_mean,total_mass_max,activities,actions");
    foreach (var season in seasons)
    {
        var organic = new CaravanRangeStatistics(
            season.WetOrganicDryKg.Minimum + season.DryOrganicKg.Minimum,
            season.WetOrganicDryKg.Mean + season.DryOrganicKg.Mean,
            season.WetOrganicDryKg.Maximum + season.DryOrganicKg.Maximum);
        await writer.WriteLineAsync(string.Join(',', new[]
        {
            season.Year?.ToString(CultureInfo.InvariantCulture) ?? "all",
            season.Season.ToString(),
            F(season.Hours),
            F(season.HibernationHours),
            F(season.DistanceKilometers),
            F(season.WaterScarcityHours),
            F(season.OrganicScarcityHours),
            F(season.NitrogenScarcityHours),
            F(season.ThermalScarcityHours),
            F(season.WaterLiters.Minimum), F(season.WaterLiters.Mean), F(season.WaterLiters.Maximum),
            F(organic.Minimum), F(organic.Mean), F(organic.Maximum),
            F(season.ElectricityKwh.Minimum), F(season.ElectricityKwh.Mean), F(season.ElectricityKwh.Maximum),
            F(season.OrganicNitrogenKg.Minimum), F(season.OrganicNitrogenKg.Mean), F(season.OrganicNitrogenKg.Maximum),
            F(season.StructuralNitrogenKg.Minimum), F(season.StructuralNitrogenKg.Mean), F(season.StructuralNitrogenKg.Maximum),
            F(season.StoredHeatKwh.Minimum), F(season.StoredHeatKwh.Mean), F(season.StoredHeatKwh.Maximum),
            F(season.BodyTemperatureC.Minimum), F(season.BodyTemperatureC.Mean), F(season.BodyTemperatureC.Maximum),
            F(season.TotalMassKg.Minimum), F(season.TotalMassKg.Mean), F(season.TotalMassKg.Maximum),
            Csv(CompactCounts(season.ActivitySteps)),
            Csv(CompactCounts(season.ActionCounts))
        }));
    }
}

static async Task WriteSeasonalOrgansCsv(
    string path,
    IReadOnlyList<CaravanSeasonStatistics> seasons)
{
    await using var writer = new StreamWriter(path);
    await writer.WriteLineAsync(
        "year,season,organ,size_unit,size_min,size_mean,size_max,function_min,function_mean,"
        + "function_max,mean_usage,active_fraction");
    foreach (var season in seasons)
    {
        foreach (var (organ, statistics) in season.Organs.OrderBy(item => item.Key))
        {
            await writer.WriteLineAsync(string.Join(',', new[]
            {
                season.Year?.ToString(CultureInfo.InvariantCulture) ?? "all",
                season.Season.ToString(),
                organ.ToString(),
                Csv(CaravanOrganCatalog.Get(organ).SizeUnit),
                F(statistics.Size.Minimum), F(statistics.Size.Mean), F(statistics.Size.Maximum),
                F(statistics.FunctionalFraction.Minimum), F(statistics.FunctionalFraction.Mean),
                F(statistics.FunctionalFraction.Maximum), F(statistics.MeanUsage),
                F(statistics.ActiveFraction)
            }));
        }
    }
}

static string CompactCounts<TKey>(IReadOnlyDictionary<TKey, int> counts) where TKey : notnull =>
    string.Join('|', counts.Where(item => item.Value > 0).Select(item => $"{item.Key}:{item.Value}"));

static string Slug<T>(T value) where T : struct, Enum => value.ToString().ToLowerInvariant();
static string F(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
static string Csv(string value) => '"' + value.Replace("\"", "\"\"") + '"';

internal sealed record ExperimentCase(
    int Seed,
    CaravanMorphology Morphology,
    CaravanPolicyKind Policy);

internal sealed record CaravanQualification(bool Passed, string[] Failures);

internal sealed record CaravanExperimentReport(
    int Seed,
    CaravanMorphology Morphology,
    string Policy,
    int Years,
    int Size,
    float CellSizeMeters,
    double TickHours,
    int Steps,
    double? FirstHibernationAtHours,
    double WaterScarcityHours,
    double OrganicScarcityHours,
    double NitrogenScarcityHours,
    double ThermalScarcityHours,
    double MaximumWaterScarcityStreakHours,
    double MaximumOrganicScarcityStreakHours,
    double MaximumNitrogenScarcityStreakHours,
    double MaximumThermalScarcityStreakHours,
    float MaximumInternalLedgerError,
    int UsedOrgans,
    int NicheKinds,
    IReadOnlyDictionary<string, int> ActionCounts,
    IReadOnlyDictionary<string, double> ActionFrequencyPerYear,
    IReadOnlyDictionary<CaravanActivity, double> ActivityFrequencyPerYear,
    IReadOnlyDictionary<CaravanOrganKind, double> OrganActiveFraction,
    CaravanObservedExtremes Extrema,
    CaravanSeasonStatistics[] SeasonalStatistics,
    CaravanSeasonStatistics[] SeasonalProfile,
    CaravanSeasonalOscillation SeasonalOscillation,
    CaravanStateSnapshot FinalState,
    double WorldWaterLedgerError,
    double WorldNitrogenLedgerError,
    CaravanQualification Qualification);

internal sealed record CaravanObservedExtremes(
    float MinimumWaterLiters,
    float MaximumWaterLiters,
    float MinimumOrganicDryEquivalentKg,
    float MaximumOrganicDryEquivalentKg,
    float MinimumOrganicNitrogenKg,
    float MaximumOrganicNitrogenKg,
    float MinimumStructuralNitrogenKg,
    float MaximumStructuralNitrogenKg,
    float MinimumElectricityKwh,
    float MaximumElectricityKwh,
    float MinimumStoredHeatKwh,
    float MaximumStoredHeatKwh,
    float MinimumBodyTemperatureC,
    float MaximumBodyTemperatureC,
    float MinimumTotalMassKg,
    float MaximumTotalMassKg,
    float MinimumOrganFunctionalFraction,
    float MinimumWaterFulfillment,
    float MinimumOrganicFulfillment,
    float MinimumNitrogenFulfillment,
    float MinimumThermalFulfillment);

internal sealed class CaravanExtremaAccumulator
{
    private float minimumWater;
    private float maximumWater;
    private float minimumOrganic;
    private float maximumOrganic;
    private float minimumOrganicNitrogen;
    private float maximumOrganicNitrogen;
    private float minimumStructuralNitrogen;
    private float maximumStructuralNitrogen;
    private float minimumElectricity;
    private float maximumElectricity;
    private float minimumHeat;
    private float maximumHeat;
    private float minimumTemperature;
    private float maximumTemperature;
    private float minimumMass;
    private float maximumMass;
    private float minimumOrganFunction;
    private float minimumWaterFulfillment = 1f;
    private float minimumOrganicFulfillment = 1f;
    private float minimumNitrogenFulfillment = 1f;
    private float minimumThermalFulfillment = 1f;

    public CaravanExtremaAccumulator(CaravanStateSnapshot initial)
    {
        minimumWater = maximumWater = initial.WaterLiters;
        minimumOrganic = maximumOrganic = OrganicDryEquivalent(initial);
        minimumOrganicNitrogen = maximumOrganicNitrogen = initial.OrganicNitrogenKg;
        minimumStructuralNitrogen = maximumStructuralNitrogen = initial.StructuralNitrogenKg;
        minimumElectricity = maximumElectricity = initial.StoredElectricityKwh;
        minimumHeat = maximumHeat = initial.StoredHeatKwh;
        minimumTemperature = maximumTemperature = initial.BodyTemperatureC;
        minimumMass = maximumMass = initial.TotalMassKg;
        minimumOrganFunction = initial.Organs.Values.Min(item => item.FunctionalFraction);
    }

    public void Observe(CaravanStepResult result)
    {
        var state = result.Snapshot;
        minimumWater = Math.Min(minimumWater, state.WaterLiters);
        maximumWater = Math.Max(maximumWater, state.WaterLiters);
        var organic = OrganicDryEquivalent(state);
        minimumOrganic = Math.Min(minimumOrganic, organic);
        maximumOrganic = Math.Max(maximumOrganic, organic);
        minimumOrganicNitrogen = Math.Min(minimumOrganicNitrogen, state.OrganicNitrogenKg);
        maximumOrganicNitrogen = Math.Max(maximumOrganicNitrogen, state.OrganicNitrogenKg);
        minimumStructuralNitrogen = Math.Min(minimumStructuralNitrogen, state.StructuralNitrogenKg);
        maximumStructuralNitrogen = Math.Max(maximumStructuralNitrogen, state.StructuralNitrogenKg);
        minimumElectricity = Math.Min(minimumElectricity, state.StoredElectricityKwh);
        maximumElectricity = Math.Max(maximumElectricity, state.StoredElectricityKwh);
        minimumHeat = Math.Min(minimumHeat, state.StoredHeatKwh);
        maximumHeat = Math.Max(maximumHeat, state.StoredHeatKwh);
        minimumTemperature = Math.Min(minimumTemperature, state.BodyTemperatureC);
        maximumTemperature = Math.Max(maximumTemperature, state.BodyTemperatureC);
        minimumMass = Math.Min(minimumMass, state.TotalMassKg);
        maximumMass = Math.Max(maximumMass, state.TotalMassKg);
        minimumOrganFunction = Math.Min(
            minimumOrganFunction,
            state.Organs.Values.Min(item => item.FunctionalFraction));
        minimumWaterFulfillment = Math.Min(minimumWaterFulfillment, result.WaterFulfillment);
        minimumOrganicFulfillment = Math.Min(minimumOrganicFulfillment, result.OrganicFulfillment);
        minimumNitrogenFulfillment = Math.Min(minimumNitrogenFulfillment, result.NitrogenFulfillment);
        minimumThermalFulfillment = Math.Min(minimumThermalFulfillment, result.ThermalFulfillment);
    }

    public CaravanObservedExtremes Capture() => new(
        minimumWater,
        maximumWater,
        minimumOrganic,
        maximumOrganic,
        minimumOrganicNitrogen,
        maximumOrganicNitrogen,
        minimumStructuralNitrogen,
        maximumStructuralNitrogen,
        minimumElectricity,
        maximumElectricity,
        minimumHeat,
        maximumHeat,
        minimumTemperature,
        maximumTemperature,
        minimumMass,
        maximumMass,
        minimumOrganFunction,
        minimumWaterFulfillment,
        minimumOrganicFulfillment,
        minimumNitrogenFulfillment,
        minimumThermalFulfillment);

    private static float OrganicDryEquivalent(CaravanStateSnapshot state) =>
        state.WetOrganicDryKg + state.DryOrganicKg;
}

internal enum CaravanSeason
{
    Winter,
    Spring,
    Summer,
    Autumn
}

internal sealed record CaravanRangeStatistics(
    float Minimum,
    float Mean,
    float Maximum);

internal sealed record CaravanSeasonalOrganStatistics(
    CaravanRangeStatistics Size,
    CaravanRangeStatistics FunctionalFraction,
    float MeanUsage,
    double ActiveFraction);

internal sealed record CaravanSeasonStatistics(
    int? Year,
    CaravanSeason Season,
    double Hours,
    double HibernationHours,
    double DistanceKilometers,
    double WaterScarcityHours,
    double OrganicScarcityHours,
    double NitrogenScarcityHours,
    double ThermalScarcityHours,
    CaravanRangeStatistics WaterLiters,
    CaravanRangeStatistics SnowWaterLiters,
    CaravanRangeStatistics WetOrganicDryKg,
    CaravanRangeStatistics WetOrganicWaterLiters,
    CaravanRangeStatistics DryOrganicKg,
    CaravanRangeStatistics OrganicNitrogenKg,
    CaravanRangeStatistics StructuralNitrogenKg,
    CaravanRangeStatistics StructuralReserveKg,
    CaravanRangeStatistics ElectricityKwh,
    CaravanRangeStatistics StoredHeatKwh,
    CaravanRangeStatistics BodyTemperatureC,
    CaravanRangeStatistics TotalMassKg,
    IReadOnlyDictionary<CaravanActivity, int> ActivitySteps,
    IReadOnlyDictionary<string, int> ActionCounts,
    IReadOnlyDictionary<CaravanOrganKind, CaravanSeasonalOrganStatistics> Organs);

internal sealed record CaravanSeasonalOscillation(
    float MeanWaterAmplitudeLiters,
    float MeanOrganicAmplitudeKg,
    float MeanOrganicNitrogenAmplitudeKg,
    float MeanStructuralNitrogenAmplitudeKg,
    float MeanElectricityAmplitudeKwh,
    float MeanStoredHeatAmplitudeKwh,
    float MeanBodyTemperatureAmplitudeC,
    float MeanTotalMassAmplitudeKg,
    float MovementRateAmplitudeKmPerThousandHours,
    float HibernationFractionAmplitude,
    CaravanActivity[] SeasonallyVariableActivities,
    CaravanOrganKind[] SeasonallyVariableOrganUsage)
{
    public static CaravanSeasonalOscillation Empty { get; } = new(
        0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, [], []);
}

internal sealed class CaravanSeasonalAccumulator
{
    private readonly Dictionary<(int Year, CaravanSeason Season), SeasonBucket> periods = [];
    private readonly Dictionary<CaravanSeason, SeasonBucket> profile = [];

    public void Observe(CaravanStepResult result)
    {
        var season = SeasonOf(result.Observation.DayOfYear);
        var key = (result.Observation.Year, season);
        if (!periods.TryGetValue(key, out var period)) periods[key] = period = new SeasonBucket();
        if (!profile.TryGetValue(season, out var aggregate)) profile[season] = aggregate = new SeasonBucket();
        period.Observe(result);
        aggregate.Observe(result);
    }

    public CaravanSeasonStatistics[] CapturePeriods() => periods
        .OrderBy(item => item.Key.Year)
        .ThenBy(item => item.Key.Season)
        .Select(item => item.Value.Capture(item.Key.Year, item.Key.Season))
        .ToArray();

    public CaravanSeasonStatistics[] CaptureProfile() => profile
        .OrderBy(item => item.Key)
        .Select(item => item.Value.Capture(null, item.Key))
        .ToArray();

    private static CaravanSeason SeasonOf(int dayOfYear) => dayOfYear switch
    {
        <= 59 or >= 335 => CaravanSeason.Winter,
        <= 151 => CaravanSeason.Spring,
        <= 243 => CaravanSeason.Summer,
        _ => CaravanSeason.Autumn
    };

    private sealed class SeasonBucket
    {
        private readonly WeightedRange water = new();
        private readonly WeightedRange snow = new();
        private readonly WeightedRange wetOrganic = new();
        private readonly WeightedRange wetOrganicWater = new();
        private readonly WeightedRange dryOrganic = new();
        private readonly WeightedRange organicNitrogen = new();
        private readonly WeightedRange structuralNitrogen = new();
        private readonly WeightedRange structuralReserve = new();
        private readonly WeightedRange electricity = new();
        private readonly WeightedRange heat = new();
        private readonly WeightedRange bodyTemperature = new();
        private readonly WeightedRange totalMass = new();
        private readonly Dictionary<CaravanActivity, int> activities =
            Enum.GetValues<CaravanActivity>().ToDictionary(item => item, _ => 0);
        private readonly Dictionary<string, int> actions = new(StringComparer.Ordinal);
        private readonly Dictionary<CaravanOrganKind, OrganBucket> organs =
            Enum.GetValues<CaravanOrganKind>().ToDictionary(item => item, _ => new OrganBucket());

        public double Hours { get; private set; }
        public double HibernationHours { get; private set; }
        public double DistanceKilometers { get; private set; }
        public double WaterScarcityHours { get; private set; }
        public double OrganicScarcityHours { get; private set; }
        public double NitrogenScarcityHours { get; private set; }
        public double ThermalScarcityHours { get; private set; }

        public void Observe(CaravanStepResult result)
        {
            var hours = result.Hours;
            var state = result.Snapshot;
            var hibernating = result.Actions.Any(item => item.Action == "hibernate");
            Hours += hours;
            if (hibernating) HibernationHours += hours;
            DistanceKilometers += result.DistanceKilometers;
            if (result.WaterFulfillment < 0.999f) WaterScarcityHours += hours;
            if (result.OrganicFulfillment < 0.999f) OrganicScarcityHours += hours;
            if (result.NitrogenFulfillment < 0.999f) NitrogenScarcityHours += hours;
            if (result.ThermalFulfillment < 0.999f) ThermalScarcityHours += hours;
            activities[hibernating ? CaravanActivity.Hibernate : result.Decision.Activity]++;
            foreach (var action in result.Actions)
                actions[action.Action] = actions.GetValueOrDefault(action.Action) + 1;

            water.Add(state.WaterLiters, hours);
            snow.Add(state.SnowWaterLiters, hours);
            wetOrganic.Add(state.WetOrganicDryKg, hours);
            wetOrganicWater.Add(state.WetOrganicWaterLiters, hours);
            dryOrganic.Add(state.DryOrganicKg, hours);
            organicNitrogen.Add(state.OrganicNitrogenKg, hours);
            structuralNitrogen.Add(state.StructuralNitrogenKg, hours);
            structuralReserve.Add(state.StructuralReserveKg, hours);
            electricity.Add(state.StoredElectricityKwh, hours);
            heat.Add(state.StoredHeatKwh, hours);
            bodyTemperature.Add(state.BodyTemperatureC, hours);
            totalMass.Add(state.TotalMassKg, hours);
            foreach (var (kind, organ) in state.Organs) organs[kind].Observe(organ, hours);
        }

        public CaravanSeasonStatistics Capture(int? year, CaravanSeason season) => new(
            year,
            season,
            Hours,
            HibernationHours,
            DistanceKilometers,
            WaterScarcityHours,
            OrganicScarcityHours,
            NitrogenScarcityHours,
            ThermalScarcityHours,
            water.Capture(),
            snow.Capture(),
            wetOrganic.Capture(),
            wetOrganicWater.Capture(),
            dryOrganic.Capture(),
            organicNitrogen.Capture(),
            structuralNitrogen.Capture(),
            structuralReserve.Capture(),
            electricity.Capture(),
            heat.Capture(),
            bodyTemperature.Capture(),
            totalMass.Capture(),
            new Dictionary<CaravanActivity, int>(activities),
            new Dictionary<string, int>(actions, StringComparer.Ordinal),
            organs.ToDictionary(item => item.Key, item => item.Value.Capture()));
    }

    private sealed class OrganBucket
    {
        private readonly WeightedRange size = new();
        private readonly WeightedRange function = new();
        private readonly WeightedRange usage = new();
        private double activeHours;

        public void Observe(CaravanOrganSnapshot organ, double hours)
        {
            size.Add(organ.Size, hours);
            function.Add(organ.FunctionalFraction, hours);
            usage.Add(organ.Usage, hours);
            if (organ.Usage > 0.01f) activeHours += hours;
        }

        public CaravanSeasonalOrganStatistics Capture() => new(
            size.Capture(),
            function.Capture(),
            usage.Capture().Mean,
            activeHours / Math.Max(1d, size.Weight));
    }

    private sealed class WeightedRange
    {
        private float minimum = float.PositiveInfinity;
        private float maximum = float.NegativeInfinity;
        private double weightedSum;
        public double Weight { get; private set; }

        public void Add(float value, double weight)
        {
            minimum = Math.Min(minimum, value);
            maximum = Math.Max(maximum, value);
            weightedSum += value * weight;
            Weight += weight;
        }

        public CaravanRangeStatistics Capture() => Weight <= 0d
            ? new CaravanRangeStatistics(0f, 0f, 0f)
            : new CaravanRangeStatistics(minimum, (float)(weightedSum / Weight), maximum);
    }
}

internal sealed record CaravanMatrixSummary(
    int Years,
    int Size,
    int Cases,
    int ActiveAtEndCases,
    int Morphologies,
    int Policies,
    int DistinctProfiles,
    string? UniversalBestConfiguration,
    string[] WinningConfigurations,
    CaravanMatrixCase[] Results);

internal sealed record CaravanMatrixCase(
    int Seed,
    CaravanMorphology Morphology,
    string Policy,
    bool ActiveAtEnd,
    double HibernationFraction,
    float DistanceKilometers,
    int NicheKinds,
    string DominantProfile,
    double Score,
    bool Qualified);

internal sealed record ExperimentOptions(
    int Years,
    int Size,
    float CellSizeMeters,
    int[] Seeds,
    CaravanMorphology[] Morphologies,
    CaravanPolicyKind[] Policies,
    double TickHours,
    int WorldStepMinutes,
    int ScoutRadiusCells,
    int HarvesterCount,
    float ClimateVariability,
    bool WildfireEnabled,
    bool Matrix,
    string OutputDirectory)
{
    public static ExperimentOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index++)
        {
            var key = args[index].TrimStart('-');
            if (index + 1 >= args.Length || args[index + 1].StartsWith('-'))
                values[key] = "true";
            else
                values[key] = args[++index];
        }

        int I(string key, int fallback) => values.TryGetValue(key, out var raw)
            ? int.Parse(raw, CultureInfo.InvariantCulture)
            : fallback;
        float Float(string key, float fallback) => values.TryGetValue(key, out var raw)
            ? float.Parse(raw, CultureInfo.InvariantCulture)
            : fallback;
        double Double(string key, double fallback) => values.TryGetValue(key, out var raw)
            ? double.Parse(raw, CultureInfo.InvariantCulture)
            : fallback;
        bool Bool(string key, bool fallback) => values.TryGetValue(key, out var raw)
            ? bool.Parse(raw)
            : fallback;
        T[] Enums<T>(string key, T fallback) where T : struct, Enum =>
            values.TryGetValue(key, out var raw)
                ? raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(Enum.Parse<T>)
                    .ToArray()
                : [fallback];
        var seeds = values.TryGetValue("seeds", out var seedValues)
            ? seedValues.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(item => int.Parse(item, CultureInfo.InvariantCulture))
                .ToArray()
            : [I("seed", 123)];
        var matrix = Bool("matrix", false);
        var morphologies = matrix && !values.ContainsKey("morphologies")
            ? new[]
            {
                CaravanMorphology.SailNomad,
                CaravanMorphology.SolarElectric,
                CaravanMorphology.BiomassHeavy,
                CaravanMorphology.Balanced
            }
            : Enums("morphologies", CaravanMorphology.Balanced);
        var policies = matrix && !values.ContainsKey("policies")
            ? Enum.GetValues<CaravanPolicyKind>()
            : Enums("policies", CaravanPolicyKind.BalancedNomad);
        return new ExperimentOptions(
            I("years", 1),
            I("size", 32),
            Float("cell-size", 250f),
            seeds,
            morphologies,
            policies,
            Double("tick-hours", 3d),
            I("step-minutes", 60),
            I("scout-radius", 28),
            I("harvesters", 10),
            Float("climate-variability", 1f),
            Bool("wildfire", true),
            matrix,
            values.GetValueOrDefault(
                "output",
                Path.Combine("WorldModel", "CaravanExperimentResults")));
    }
}

internal static class ExperimentJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
