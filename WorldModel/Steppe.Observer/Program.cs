using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.ResponseCompression;
using Steppe.Observer;
using Steppe.Simulation;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<GzipCompressionProvider>();
});

var size = ReadArgument(args, "--size", 96);
var seed = ReadArgument(args, "--seed", 12345);
builder.Services.AddSingleton(new ObserverWorldHost(new WorldConfig
{
    Width = size,
    Height = size,
    Seed = seed,
    BaseStepMinutes = 180
}));

var app = builder.Build();
app.UseResponseCompression();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/summary", (ObserverWorldHost host) => host.Read(world => world.GetSummary()));
app.MapGet("/api/states", () => StateCatalog.All);
app.MapGet("/api/fluxes", () => FluxCatalog.All);
app.MapGet("/api/events/catalog", () => WorldEventCatalog.All);
app.MapGet("/api/events", (int? limit, ObserverWorldHost host) =>
{
    var requested = limit ?? 12;
    return requested is >= 0 and <= 256
        ? Results.Ok(host.Read(world => world.CaptureRegimeEvents(requested)))
        : Results.BadRequest(new { message = "limit must be between 0 and 256" });
});
app.MapGet("/api/layers", () => Enum.GetNames<SimulationLayer>());
app.MapGet("/api/layer/{layer}", (string layer, ObserverWorldHost host) =>
{
    return Enum.TryParse<SimulationLayer>(layer, ignoreCase: true, out var parsed)
        ? Results.Ok(host.Read(world => world.CaptureLayer(parsed)))
        : Results.NotFound(new { message = $"Unknown layer '{layer}'." });
});
app.MapGet("/api/cell/{x:int}/{y:int}", (int x, int y, ObserverWorldHost host) =>
{
    try
    {
        return Results.Ok(host.Read(world => world.SampleCell(x, y)));
    }
    catch (ArgumentOutOfRangeException)
    {
        return Results.NotFound();
    }
});
app.MapGet("/api/cell/{x:int}/{y:int}/explain/{layer}",
    (int x, int y, string layer, ObserverWorldHost host) =>
    {
        if (!Enum.TryParse<SimulationLayer>(layer, ignoreCase: true, out var parsed))
        {
            return Results.NotFound(new { message = $"Unknown state '{layer}'." });
        }

        try
        {
            return Results.Ok(host.Read(world => world.ExplainCellState(x, y, parsed)));
        }
        catch (ArgumentOutOfRangeException)
        {
            return Results.NotFound();
        }
    });
app.MapGet("/api/history/world", (string? resolution, ObserverWorldHost host) =>
{
    if (!TryReadHistoryResolution(resolution, out var parsed))
    {
        return Results.BadRequest(new { message = $"Unknown history resolution '{resolution}'." });
    }

    return Results.Ok(host.Read(world => world.CaptureWorldHistory(parsed)));
});
app.MapGet("/api/history/cells", (ObserverWorldHost host) =>
    Results.Ok(host.Read(world => world.GetPinnedCells())));
app.MapGet("/api/cell/{x:int}/{y:int}/history/{layer}",
    (int x, int y, string layer, string? resolution, ObserverWorldHost host) =>
    {
        if (!Enum.TryParse<SimulationLayer>(layer, ignoreCase: true, out var parsedLayer))
        {
            return Results.NotFound(new { message = $"Unknown state '{layer}'." });
        }

        if (!TryReadHistoryResolution(resolution, out var parsedResolution))
        {
            return Results.BadRequest(new { message = $"Unknown history resolution '{resolution}'." });
        }

        try
        {
            return Results.Ok(host.Read(world =>
                world.CaptureCellHistory(x, y, parsedLayer, parsedResolution)));
        }
        catch (ArgumentOutOfRangeException)
        {
            return Results.NotFound();
        }
    });
app.MapPost("/api/cell/{x:int}/{y:int}/pin", (int x, int y, ObserverWorldHost host) =>
{
    try
    {
        var added = host.Read(world => world.PinCell(x, y));
        return Results.Ok(new { added, pinnedCells = host.Read(world => world.GetPinnedCells()) });
    }
    catch (ArgumentOutOfRangeException)
    {
        return Results.NotFound();
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(new { message = exception.Message });
    }
});
app.MapDelete("/api/cell/{x:int}/{y:int}/pin", (int x, int y, ObserverWorldHost host) =>
{
    try
    {
        var removed = host.Read(world => world.UnpinCell(x, y));
        return Results.Ok(new { removed, pinnedCells = host.Read(world => world.GetPinnedCells()) });
    }
    catch (ArgumentOutOfRangeException)
    {
        return Results.NotFound();
    }
});
app.MapPost("/api/advance", async (double hours, ObserverWorldHost host, HttpContext context) =>
{
    if (!double.IsFinite(hours) || hours is <= 0 or > 24 * 365)
    {
        return Results.BadRequest(new { message = "hours must be between 0 and 8760" });
    }

    var summary = await host.AdvanceAsync(hours, world => world.GetSummary(), context.RequestAborted);
    return Results.Ok(summary);
});
app.MapPost("/api/reset", async (int seed, int size, ObserverWorldHost host, HttpContext context) =>
{
    if (size is < 16 or > 256)
    {
        return Results.BadRequest(new { message = "size must be between 16 and 256" });
    }

    return Results.Ok(await host.ResetAsync(seed, size, context.RequestAborted));
});
app.MapGet("/api/save", (ObserverWorldHost host) =>
{
    var stream = new MemoryStream();
    host.Read(world =>
    {
        world.Save(stream);
        return 0;
    });
    stream.Position = 0;
    return Results.File(stream, "application/octet-stream", "steppe-world.steppe");
});

app.Run();

static int ReadArgument(string[] arguments, string name, int fallback)
{
    var index = Array.IndexOf(arguments, name);
    return index >= 0 && index + 1 < arguments.Length && int.TryParse(arguments[index + 1], out var value)
        ? value
        : fallback;
}

static bool TryReadHistoryResolution(string? value, out HistoryResolution resolution)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        resolution = HistoryResolution.Recent;
        return true;
    }

    return Enum.TryParse(value, ignoreCase: true, out resolution);
}
