using System.Text.Json;
using System.Text.Json.Serialization;

namespace Steppe.CaravanSimulation;

public sealed record CaravanCheckpoint(
    int SchemaVersion,
    CaravanBlueprint Blueprint,
    CaravanStateSnapshot State);

public static class CaravanPersistence
{
    public const int CurrentSchemaVersion = 3;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static void Save(CaravanSimulation caravan, Stream destination)
    {
        RuntimeCompatibility.ThrowIfNull(caravan, nameof(caravan));
        RuntimeCompatibility.ThrowIfNull(destination, nameof(destination));
        var snapshot = caravan.Capture();
        var checkpoint = new CaravanCheckpoint(CurrentSchemaVersion, caravan.Blueprint, snapshot);
        JsonSerializer.Serialize(destination, checkpoint, Options);
    }

    public static void Save(CaravanSimulation caravan, string path)
    {
        RuntimeCompatibility.ThrowIfNullOrWhiteSpace(path, nameof(path));
        using var stream = File.Create(path);
        Save(caravan, stream);
    }

    public static CaravanSimulation Load(Stream source)
    {
        RuntimeCompatibility.ThrowIfNull(source, nameof(source));
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
        RuntimeCompatibility.ThrowIfNullOrWhiteSpace(path, nameof(path));
        using var stream = File.OpenRead(path);
        return Load(stream);
    }
}
