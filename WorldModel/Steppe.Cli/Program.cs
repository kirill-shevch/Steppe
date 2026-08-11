using System.Diagnostics;
using System.Globalization;
using Steppe.Simulation;

var options = CliOptions.Parse(args);
if (options.ShowHelp)
{
    CliOptions.PrintHelp();
    return 0;
}

try
{
    var stopwatch = Stopwatch.StartNew();
    var world = options.LoadPath is not null
        ? FiniteWorld.Load(options.LoadPath)
        : new FiniteWorld(new WorldConfig
        {
            Width = options.Width,
            Height = options.Height,
            CellSizeMeters = options.CellSizeMeters,
            Seed = options.Seed,
            LatitudeDegrees = options.Latitude,
            BaseStepMinutes = options.StepMinutes
        });

    if (options.AdvanceHours > 0)
    {
        Console.Error.WriteLine(
            $"Advancing {options.AdvanceHours:N0} simulated hours on {world.Config.Width}×{world.Config.Height} cells…");
        var remaining = options.AdvanceHours;
        while (remaining > 0)
        {
            var chunk = Math.Min(remaining, 24d * 30d);
            world.AdvanceHours(chunk);
            remaining -= chunk;
            if (options.AdvanceHours >= 24d * 30d)
            {
                Console.Error.Write($"\rYear {world.Clock.Year}, day {world.Clock.DayOfYear:000}   ");
            }
        }

        Console.Error.WriteLine();
    }

    if (options.SavePath is not null)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(options.SavePath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        world.Save(options.SavePath);
    }

    if (options.CsvPath is not null)
    {
        ExportCsv(world.CaptureLayer(options.CsvLayer), options.CsvPath);
    }

    stopwatch.Stop();
    PrintSummary(world.GetSummary(), stopwatch.Elapsed, options.SavePath);
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"steppe-sim: {exception.Message}");
    return 1;
}

static void ExportCsv(LayerSnapshot layer, string path)
{
    using var writer = new StreamWriter(path);
    writer.WriteLine($"# {layer.Layer} ({layer.Unit})");
    for (var y = 0; y < layer.Height; y++)
    {
        for (var x = 0; x < layer.Width; x++)
        {
            if (x > 0)
            {
                writer.Write(',');
            }

            writer.Write(layer.Values[y * layer.Width + x].ToString("G9", CultureInfo.InvariantCulture));
        }

        writer.WriteLine();
    }
}

static void PrintSummary(WorldSummary summary, TimeSpan runtime, string? savePath)
{
    Console.WriteLine("STEPPE WORLD MODEL");
    Console.WriteLine($"World       {summary.Width} × {summary.Height} cells, {summary.CellSizeMeters:N0} m/cell, seed {summary.Seed}");
    Console.WriteLine($"Time        year {summary.Year}, day {summary.DayOfYear:000}, {summary.HourOfDay:00.0}h ({summary.Season})");
    Console.WriteLine($"Sun         {summary.SunElevationDegrees,6:N1}°, day length {summary.DayLengthHours:N1}h");
    Console.WriteLine($"Surface     {summary.MeanSurfaceTemperatureC,6:N1} °C mean");
    Console.WriteLine($"Rain        {summary.MeanPrecipitationMmPerHour,6:N3} mm/h mean");
    Console.WriteLine($"Biomass     {summary.MeanLiveBiomassGm2,6:N1} g/m² mean live");
    Console.WriteLine($"Water       {summary.WaterBudget.StoredMmCells,12:N1} mm·cells stored");
    Console.WriteLine($"Water error {summary.WaterBudget.BalanceErrorMmCells,12:N6} mm·cells ({summary.WaterBudget.RelativeError:P6})");
    Console.WriteLine($"Nitrogen    {summary.NitrogenBudget.StoredGm2Cells,12:N3} g/m²·cells stored");
    Console.WriteLine($"N error     {summary.NitrogenBudget.BalanceErrorGm2Cells,12:N6} g/m²·cells ({summary.NitrogenBudget.RelativeError:P6})");
    Console.WriteLine($"Runtime     {runtime.TotalSeconds:N2} s");
    if (savePath is not null)
    {
        Console.WriteLine($"Saved       {Path.GetFullPath(savePath)}");
    }
}

internal sealed record CliOptions(
    int Width,
    int Height,
    int Seed,
    float CellSizeMeters,
    double Latitude,
    int StepMinutes,
    double AdvanceHours,
    string? LoadPath,
    string? SavePath,
    string? CsvPath,
    SimulationLayer CsvLayer,
    bool ShowHelp)
{
    public static CliOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unexpected argument: {argument}");
            }

            var key = argument[2..];
            if (key is "help" or "h")
            {
                values[key] = null;
                continue;
            }

            if (index + 1 >= args.Length)
            {
                throw new ArgumentException($"Missing value for {argument}");
            }

            values[key] = args[++index];
        }

        var size = ReadInt(values, "size", 96);
        var hours = ReadDouble(values, "hours", 0)
            + ReadDouble(values, "days", 0) * 24d
            + ReadDouble(values, "years", 0) * 365d * 24d;
        var layerName = ReadString(values, "layer") ?? nameof(SimulationLayer.SurfaceWater);
        if (!Enum.TryParse<SimulationLayer>(layerName, ignoreCase: true, out var layer))
        {
            throw new ArgumentException($"Unknown layer: {layerName}");
        }

        return new CliOptions(
            ReadInt(values, "width", size),
            ReadInt(values, "height", size),
            ReadInt(values, "seed", 12345),
            (float)ReadDouble(values, "cell-size", 250),
            ReadDouble(values, "latitude", 48),
            ReadInt(values, "step-minutes", 180),
            hours,
            ReadString(values, "load"),
            ReadString(values, "save"),
            ReadString(values, "csv"),
            layer,
            values.ContainsKey("help") || values.ContainsKey("h"));
    }

    public static void PrintHelp()
    {
        Console.WriteLine("""
            steppe-sim — autonomous finite-steppe simulator

            Usage:
              dotnet run --project Steppe.Cli -- --seed 123 --years 10 --save world.steppe

            Options:
              --size N             Square grid size (default 96)
              --width N            Grid width
              --height N           Grid height
              --cell-size METERS   Simulation cell size (default 250)
              --seed N             Deterministic world seed
              --latitude DEGREES   World latitude (default 48)
              --step-minutes N     Base simulation step dividing 1440 (default 180)
              --hours N            Hours to advance
              --days N             Days to advance
              --years N            365-day years to advance
              --load PATH          Continue a saved .steppe world
              --save PATH          Save the resulting world
              --csv PATH           Export one displayed layer as CSV
              --layer NAME         Layer used by --csv (default SurfaceWater)
              --help               Show this help
            """);
    }

    private static string? ReadString(IReadOnlyDictionary<string, string?> values, string key) =>
        values.TryGetValue(key, out var value) ? value : null;

    private static int ReadInt(IReadOnlyDictionary<string, string?> values, string key, int fallback) =>
        ReadString(values, key) is { } value
            ? int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture)
            : fallback;

    private static double ReadDouble(IReadOnlyDictionary<string, string?> values, string key, double fallback) =>
        ReadString(values, key) is { } value
            ? double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture)
            : fallback;
}
