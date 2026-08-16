using System.IO.Compression;
using System.Text;

namespace Steppe.Simulation;

internal static class WorldPersistence
{
    private const string Magic = "STEPPE-WORLD-MODEL";

    public static void Save(
        Stream destination,
        WorldConfig config,
        WorldClock clock,
        WorldState state,
        WaterBudget budget,
        NitrogenBudget nitrogenBudget,
        WorldFauna fauna)
    {
        using var gzip = new GZipStream(destination, CompressionLevel.Optimal, leaveOpen: true);
        using var writer = new BinaryWriter(gzip, Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(WorldConfig.CurrentSchemaVersion);
        writer.Write(config.Width);
        writer.Write(config.Height);
        writer.Write(config.CellSizeMeters);
        writer.Write(config.Seed);
        writer.Write(config.LatitudeDegrees);
        writer.Write(config.AxialTiltDegrees);
        writer.Write(config.BaseStepMinutes);
        writer.Write(config.GeographyErosionPasses);
        writer.Write(config.GiantHarvesterCount);
        writer.Write(config.ClimateVariability);
        writer.Write(config.WildfireEnabled);
        writer.Write(clock.ElapsedHours);

        var snapshot = budget.Snapshot(state);
        writer.Write(snapshot.InitialStoredMmCells);
        writer.Write(snapshot.ExternalInputMmCells);
        writer.Write(snapshot.ExternalOutputMmCells);
        var nitrogen = nitrogenBudget.Snapshot(state);
        writer.Write(nitrogen.InitialStoredGm2Cells);
        writer.Write(nitrogen.ExternalInputGm2Cells);
        writer.Write(nitrogen.ExternalOutputGm2Cells);

        var fields = state.SerializableFloatFields().ToArray();
        writer.Write(fields.Length);
        foreach (var field in fields)
        {
            Write(field, writer);
        }

        Write(state.DrainTo, writer);
        Write(state.CatchmentId, writer);
        fauna.Save(writer);
    }

    public static FiniteWorld Load(Stream source)
    {
        using var gzip = new GZipStream(source, CompressionMode.Decompress, leaveOpen: true);
        using var reader = new BinaryReader(gzip, Encoding.UTF8, leaveOpen: true);
        if (reader.ReadString() != Magic)
        {
            throw new InvalidDataException("Not a Steppe world model file.");
        }

        var schemaVersion = reader.ReadInt32();
        if (schemaVersion is < 3 or > WorldConfig.CurrentSchemaVersion)
        {
            throw new InvalidDataException($"Unsupported world schema {schemaVersion}.");
        }

        var config = new WorldConfig
        {
            Width = reader.ReadInt32(),
            Height = reader.ReadInt32(),
            CellSizeMeters = reader.ReadSingle(),
            Seed = reader.ReadInt32(),
            LatitudeDegrees = reader.ReadDouble(),
            AxialTiltDegrees = reader.ReadDouble(),
            BaseStepMinutes = reader.ReadInt32(),
            GeographyErosionPasses = reader.ReadInt32(),
            GiantHarvesterCount = schemaVersion >= 4 ? reader.ReadInt32() : 0,
            ClimateVariability = schemaVersion >= 5 ? reader.ReadSingle() : 1f,
            WildfireEnabled = schemaVersion >= 6 ? reader.ReadBoolean() : true
        }.Validate();
        var clock = new WorldClock(reader.ReadDouble());
        var initialWater = reader.ReadDouble();
        var externalInput = reader.ReadDouble();
        var externalOutput = reader.ReadDouble();
        var initialNitrogen = reader.ReadDouble();
        var externalNitrogenInput = schemaVersion >= 4 ? reader.ReadDouble() : 0d;
        var externalNitrogenOutput = schemaVersion >= 4 ? reader.ReadDouble() : 0d;
        var state = new WorldState(config.CellCount);
        var fields = state.SerializableFloatFields()
            .Where(field => schemaVersion >= 4 || !ReferenceEquals(field, state.SoilCompactionFraction))
            .Where(field => schemaVersion >= 6
                || (!ReferenceEquals(field, state.FireIntensityFraction)
                    && !ReferenceEquals(field, state.BurnScarFraction)))
            .ToArray();
        var fieldCount = reader.ReadInt32();
        if (fieldCount != fields.Length)
        {
            throw new InvalidDataException("World field count does not match this schema.");
        }

        foreach (var field in fields)
        {
            Read(field, reader);
        }

        Read(state.DrainTo, reader);
        Read(state.CatchmentId, reader);
        var fauna = schemaVersion >= 4 ? WorldFauna.Load(reader) : WorldFauna.Generate(config);
        var budget = new WaterBudget();
        budget.Restore(initialWater, externalInput, externalOutput);
        var nitrogenBudget = new NitrogenBudget();
        nitrogenBudget.Restore(initialNitrogen, externalNitrogenInput, externalNitrogenOutput);
        return new FiniteWorld(config, clock, state, budget, nitrogenBudget, fauna);
    }

    private static void Write(float[] values, BinaryWriter writer)
    {
        writer.Write(values.Length);
        foreach (var value in values)
        {
            writer.Write(value);
        }
    }

    private static void Write(int[] values, BinaryWriter writer)
    {
        writer.Write(values.Length);
        foreach (var value in values)
        {
            writer.Write(value);
        }
    }

    private static void Read(float[] destination, BinaryReader reader)
    {
        var length = reader.ReadInt32();
        if (length != destination.Length)
        {
            throw new InvalidDataException("World field has an unexpected length.");
        }

        for (var index = 0; index < destination.Length; index++)
        {
            destination[index] = reader.ReadSingle();
        }
    }

    private static void Read(int[] destination, BinaryReader reader)
    {
        var length = reader.ReadInt32();
        if (length != destination.Length)
        {
            throw new InvalidDataException("World field has an unexpected length.");
        }

        for (var index = 0; index < destination.Length; index++)
        {
            destination[index] = reader.ReadInt32();
        }
    }
}
