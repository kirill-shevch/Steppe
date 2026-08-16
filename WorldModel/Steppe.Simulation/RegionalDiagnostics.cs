namespace Steppe.Simulation;

public sealed record RegionalCellSnapshot(
    int X,
    int Y,
    int MinimumCellX,
    int MinimumCellY,
    int WidthCells,
    int HeightCells,
    int CellCount,
    float MeanSurfaceTemperatureC,
    float MeanSnowMm,
    float MeanSurfaceWaterMm,
    float MaximumSurfaceWaterMm,
    float FloodedFraction,
    float MeanRootWaterMm,
    float WaterStressFraction,
    float MeanLiveBiomassGm2,
    float GreenFraction,
    float MeanDustGm2,
    float DustAffectedFraction,
    float MeanFireIntensity,
    float BurningFraction,
    float MeanBurnScar);

public sealed record RegionalWorldSnapshot(
    double ElapsedHours,
    int Columns,
    int Rows,
    RegionalCellSnapshot[] Regions);

public sealed record EventMaskSnapshot(
    WorldEventKind Kind,
    int Width,
    int Height,
    float[] Values,
    float Mean,
    float Maximum,
    int ActiveCellCount);

public sealed partial class FiniteWorld
{
    public RegionalWorldSnapshot CaptureRegionalSnapshot(int columns = 8, int rows = 8)
    {
        if (columns is < 1 or > 64 || rows is < 1 or > 64
            || columns > Config.Width || rows > Config.Height)
        {
            throw new ArgumentOutOfRangeException(
                nameof(columns),
                "Regional dimensions must be between 1 and 64 and fit inside the world.");
        }

        lock (sync)
        {
            var count = columns * rows;
            var cells = new int[count];
            var temperature = new double[count];
            var snow = new double[count];
            var surface = new double[count];
            var surfaceMaximum = new float[count];
            var root = new double[count];
            var live = new double[count];
            var dust = new double[count];
            var fire = new double[count];
            var scar = new double[count];
            var flooded = new int[count];
            var stressed = new int[count];
            var green = new int[count];
            var dusty = new int[count];
            var burning = new int[count];

            for (var y = 0; y < Config.Height; y++)
            {
                var regionY = Math.Min(rows - 1, y * rows / Config.Height);
                for (var x = 0; x < Config.Width; x++)
                {
                    var regionX = Math.Min(columns - 1, x * columns / Config.Width);
                    var region = regionY * columns + regionX;
                    var index = y * Config.Width + x;
                    cells[region]++;
                    temperature[region] += state.SurfaceTemperatureC[index];
                    snow[region] += state.SnowWaterEquivalentMm[index];
                    surface[region] += state.SurfaceWaterMm[index];
                    surfaceMaximum[region] = Math.Max(surfaceMaximum[region], state.SurfaceWaterMm[index]);
                    root[region] += state.RootWaterMm[index];
                    live[region] += state.LiveBiomassGm2[index];
                    dust[region] += state.DustGm2[index];
                    fire[region] += state.FireIntensityFraction[index];
                    scar[region] += state.BurnScarFraction[index];
                    if (state.SurfaceWaterMm[index] >= 10f) flooded[region]++;
                    if (state.RootWaterMm[index] < 35f) stressed[region]++;
                    if (state.LiveBiomassGm2[index] >= 150f) green[region]++;
                    if (state.DustGm2[index] >= 0.02f) dusty[region]++;
                    if (state.FireIntensityFraction[index] >= 0.01f) burning[region]++;
                }
            }

            var snapshots = new RegionalCellSnapshot[count];
            for (var regionY = 0; regionY < rows; regionY++)
            {
                var minimumY = regionY * Config.Height / rows;
                var maximumY = (regionY + 1) * Config.Height / rows;
                for (var regionX = 0; regionX < columns; regionX++)
                {
                    var minimumX = regionX * Config.Width / columns;
                    var maximumX = (regionX + 1) * Config.Width / columns;
                    var region = regionY * columns + regionX;
                    var scale = 1f / Math.Max(1, cells[region]);
                    snapshots[region] = new RegionalCellSnapshot(
                        regionX,
                        regionY,
                        minimumX,
                        minimumY,
                        maximumX - minimumX,
                        maximumY - minimumY,
                        cells[region],
                        (float)(temperature[region] * scale),
                        (float)(snow[region] * scale),
                        (float)(surface[region] * scale),
                        surfaceMaximum[region],
                        flooded[region] * scale,
                        (float)(root[region] * scale),
                        stressed[region] * scale,
                        (float)(live[region] * scale),
                        green[region] * scale,
                        (float)(dust[region] * scale),
                        dusty[region] * scale,
                        (float)(fire[region] * scale),
                        burning[region] * scale,
                        (float)(scar[region] * scale));
                }
            }

            return new RegionalWorldSnapshot(Clock.ElapsedHours, columns, rows, snapshots);
        }
    }

    public EventMaskSnapshot CaptureEventMask(WorldEventKind kind)
    {
        lock (sync)
        {
            var values = new float[Config.CellCount];
            var period = Math.Max(1e-6, fluxState.PeriodHours);
            double total = 0;
            var maximum = 0f;
            var active = 0;
            for (var index = 0; index < values.Length; index++)
            {
                var value = kind switch
                {
                    WorldEventKind.Snowmelt => Math.Clamp(
                        fluxState.Value(SimulationFlux.SnowMelt, index) / (float)period / 0.12f,
                        0f,
                        1f),
                    WorldEventKind.FloodPulse => Math.Clamp(Math.Max(
                        state.SurfaceWaterMm[index] / 100f,
                        fluxState.Value(SimulationFlux.RunoffIn, index) / (float)period / 2f), 0f, 1f),
                    WorldEventKind.GreenUp => Math.Clamp(
                        fluxState.Value(SimulationFlux.PlantGrowth, index) / (float)period / 0.08f,
                        0f,
                        1f),
                    WorldEventKind.Drought => Math.Clamp((35f - state.RootWaterMm[index]) / 25f, 0f, 1f),
                    WorldEventKind.DustEpisode => Math.Clamp(state.DustGm2[index] / 0.08f, 0f, 1f),
                    WorldEventKind.Wildfire => Math.Clamp(state.FireIntensityFraction[index], 0f, 1f),
                    _ => 0f
                };
                values[index] = value;
                total += value;
                maximum = Math.Max(maximum, value);
                if (value >= 0.1f) active++;
            }

            return new EventMaskSnapshot(
                kind,
                Config.Width,
                Config.Height,
                values,
                (float)(total / values.Length),
                maximum,
                active);
        }
    }
}
