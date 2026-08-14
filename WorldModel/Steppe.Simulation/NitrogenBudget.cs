namespace Steppe.Simulation;

public readonly record struct NitrogenBudgetSnapshot(
    double StoredGm2Cells,
    double InitialStoredGm2Cells,
    double ExternalInputGm2Cells,
    double ExternalOutputGm2Cells,
    double BalanceErrorGm2Cells)
{
    public double RelativeError => Math.Abs(InitialStoredGm2Cells + ExternalInputGm2Cells) < 1e-9
        ? 0
        : BalanceErrorGm2Cells / (InitialStoredGm2Cells + ExternalInputGm2Cells);
}

/// <summary>
/// The simulated nitrogen cycle is closed: nitrogen can change chemical or
/// biological reservoirs, but cannot silently appear or disappear.
/// </summary>
internal sealed class NitrogenBudget
{
    public double InitialStored { get; private set; }
    public double ExternalInput { get; private set; }
    public double ExternalOutput { get; private set; }

    public void Initialize(WorldState state) => InitialStored = SumStored(state);

    public NitrogenBudgetSnapshot Snapshot(WorldState state)
    {
        var stored = SumStored(state);
        return new NitrogenBudgetSnapshot(
            stored,
            InitialStored,
            ExternalInput,
            ExternalOutput,
            stored - InitialStored - ExternalInput + ExternalOutput);
    }

    public void AddExternal(double signedAmount)
    {
        if (signedAmount >= 0) ExternalInput += signedAmount;
        else ExternalOutput -= signedAmount;
    }

    public void Restore(double initial, double input = 0, double output = 0)
    {
        InitialStored = initial;
        ExternalInput = input;
        ExternalOutput = output;
    }

    private static double SumStored(WorldState state)
    {
        double total = 0;
        for (var index = 0; index < state.AvailableNitrogenGm2.Length; index++)
        {
            total += state.AvailableNitrogenGm2[index]
                + state.PlantNitrogenGm2[index]
                + state.OrganicNitrogenGm2[index];
        }

        return total;
    }
}
