namespace Steppe.Simulation;

public readonly record struct WaterBudgetSnapshot(
    double StoredMmCells,
    double InitialStoredMmCells,
    double ExternalInputMmCells,
    double ExternalOutputMmCells,
    double BalanceErrorMmCells)
{
    public double RelativeError => Math.Abs(InitialStoredMmCells + ExternalInputMmCells) < 1e-9
        ? 0
        : BalanceErrorMmCells / (InitialStoredMmCells + ExternalInputMmCells);
}

internal sealed class WaterBudget
{
    public double InitialStored { get; private set; }
    public double ExternalInput { get; private set; }
    public double ExternalOutput { get; private set; }

    public void Initialize(WorldState state) => InitialStored = SumStored(state);
    public void AddExternal(double signedAmount)
    {
        if (signedAmount >= 0)
        {
            ExternalInput += signedAmount;
        }
        else
        {
            ExternalOutput -= signedAmount;
        }
    }

    public WaterBudgetSnapshot Snapshot(WorldState state)
    {
        var stored = SumStored(state);
        return new WaterBudgetSnapshot(
            stored,
            InitialStored,
            ExternalInput,
            ExternalOutput,
            stored - InitialStored - ExternalInput + ExternalOutput);
    }

    public void Restore(double initial, double input, double output)
    {
        InitialStored = initial;
        ExternalInput = input;
        ExternalOutput = output;
    }

    private static double SumStored(WorldState state)
    {
        double total = 0;
        for (var index = 0; index < state.SurfaceWaterMm.Length; index++)
        {
            total += state.SurfaceWaterMm[index]
                + state.RootWaterMm[index]
                + state.GroundwaterMm[index]
                + state.SnowWaterEquivalentMm[index]
                + state.AirHumidityMm[index]
                + state.CloudWaterMm[index];
        }

        return total;
    }
}
