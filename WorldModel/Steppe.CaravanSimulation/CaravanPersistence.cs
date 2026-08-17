using System.Text.Json;
using System.Text.Json.Serialization;

namespace Steppe.CaravanSimulation;

public sealed record CaravanCheckpoint(
    int SchemaVersion,
    CaravanBlueprint Blueprint,
    CaravanStateSnapshot State);

public static class CaravanPersistence
{
    public const int CurrentSchemaVersion = 2;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static void Save(CaravanSimulation caravan, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(caravan);
        ArgumentNullException.ThrowIfNull(destination);
        var snapshot = caravan.Capture();
        var checkpoint = new CaravanCheckpoint(CurrentSchemaVersion, caravan.Blueprint, snapshot);
        JsonSerializer.Serialize(destination, checkpoint, Options);
    }

    public static void Save(CaravanSimulation caravan, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = File.Create(path);
        Save(caravan, stream);
    }

    public static CaravanSimulation Load(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var checkpoint = JsonSerializer.Deserialize<CaravanCheckpoint>(source, Options)
            ?? throw new InvalidDataException("The caravan checkpoint is empty.");
        if (checkpoint.SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Unsupported caravan checkpoint schema {checkpoint.SchemaVersion}.");
        }
        return new CaravanSimulation(checkpoint.Blueprint, checkpoint.State);
    }

    public static CaravanSimulation Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = File.OpenRead(path);
        return Load(stream);
    }
}
