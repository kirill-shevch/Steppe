namespace Steppe.Simulation;

public readonly record struct NitrogenBudgetSnapshot(
    double StoredGm2Cells,
    double InitialStoredGm2Cells,
    double BalanceErrorGm2Cells)
{
    public double RelativeError => Math.Abs(InitialStoredGm2Cells) < 1e-9
        ? 0
        : BalanceErrorGm2Cells / InitialStoredGm2Cells;
}

/// <summary>
/// The simulated nitrogen cycle is closed: nitrogen can change chemical or
/// biological reservoirs, but cannot silently appear or disappear.
/// </summary>
internal sealed class NitrogenBudget
{
    public double InitialStored { get; private set; }

    public void Initialize(WorldState state) => InitialStored = SumStored(state);

    public NitrogenBudgetSnapshot Snapshot(WorldState state)
    {
        var stored = SumStored(state);
        return new NitrogenBudgetSnapshot(stored, InitialStored, stored - InitialStored);
    }

    public void Restore(double initial) => InitialStored = initial;

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
