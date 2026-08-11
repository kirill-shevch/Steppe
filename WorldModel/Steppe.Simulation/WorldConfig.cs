namespace Steppe.Simulation;

/// <summary>
/// Physical and numerical dimensions of one finite steppe world.
/// All quantities use explicit physical units; normalized values are limited to fractions.
/// </summary>
public sealed record WorldConfig
{
    public const int CurrentSchemaVersion = 3;

    public int Width { get; init; } = 96;
    public int Height { get; init; } = 96;
    public float CellSizeMeters { get; init; } = 250f;
    public int Seed { get; init; } = 12345;
    public double LatitudeDegrees { get; init; } = 48.0;
    public double AxialTiltDegrees { get; init; } = 23.44;
    public int BaseStepMinutes { get; init; } = 60;
    public int GeographyErosionPasses { get; init; } = 18;

    public int CellCount => checked(Width * Height);
    public float WidthKilometers => Width * CellSizeMeters / 1000f;
    public float HeightKilometers => Height * CellSizeMeters / 1000f;

    public WorldConfig Validate()
    {
        if (Width is < 8 or > 512 || Height is < 8 or > 512)
        {
            throw new ArgumentOutOfRangeException(nameof(Width), "World dimensions must be between 8 and 512 cells.");
        }

        if (CellSizeMeters is < 25f or > 5000f)
        {
            throw new ArgumentOutOfRangeException(nameof(CellSizeMeters));
        }

        if (LatitudeDegrees is < -75 or > 75)
        {
            throw new ArgumentOutOfRangeException(nameof(LatitudeDegrees));
        }

        if (BaseStepMinutes is < 10 or > 360 || 1440 % BaseStepMinutes != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(BaseStepMinutes), "The base step must divide a day exactly.");
        }

        if (GeographyErosionPasses is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(GeographyErosionPasses));
        }

        return this;
    }
}
