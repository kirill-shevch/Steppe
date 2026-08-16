namespace Steppe.Simulation;

public enum CaravanInterventionKind
{
    WithdrawSurfaceWater,
    CondenseAtmosphericWater,
    ReturnSurfaceWater,
    MeltAndWithdrawSnow,
    HarvestBiomass,
    ReturnOrganicMatter,
    CaptureDust,
    ReturnSediment,
    CompactTrail,
    CollectMolt
}

public sealed record CaravanInterventionResult(
    CaravanInterventionKind Kind,
    int X,
    int Y,
    float SurfaceWaterMm = 0,
    float AtmosphericWaterMm = 0,
    float SnowWaterEquivalentMm = 0,
    float LiveBiomassGm2 = 0,
    float DryBiomassGm2 = 0,
    float NitrogenGm2 = 0,
    float OrganicMatterGm2 = 0,
    float DustGm2 = 0,
    float SedimentKgM2 = 0,
    float CompactionDelta = 0,
    float ChitinKg = 0);

/// <summary>
/// Atomic material exchanges used both by a playable adapter and by the
/// autonomous qualification caravan. Every method returns the amount that was
/// actually moved; unavailable material is never invented.
/// </summary>
public sealed partial class FiniteWorld
{
    public CaravanInterventionResult WithdrawSurfaceWater(int x, int y, float maximumMillimeters)
    {
        ValidateCaravanAmount(maximumMillimeters, nameof(maximumMillimeters));
        lock (sync)
        {
            fluxState.Begin(SampleValue);
            var index = CheckedIndex(x, y);
            var result = CaravanInterventionEngine.WithdrawSurfaceWater(
                state, waterBudget, fluxState, index, x, y, maximumMillimeters);
            fluxState.Complete(0, SampleValue);
            return result;
        }
    }

    public CaravanInterventionResult ReturnSurfaceWater(int x, int y, float millimeters)
    {
        ValidateCaravanAmount(millimeters, nameof(millimeters));
        lock (sync)
        {
            fluxState.Begin(SampleValue);
            var index = CheckedIndex(x, y);
            var result = CaravanInterventionEngine.ReturnSurfaceWater(
                state, waterBudget, fluxState, index, x, y, millimeters);
            fluxState.Complete(0, SampleValue);
            return result;
        }
    }

    public CaravanInterventionResult CondenseAtmosphericWater(int x, int y, float maximumMillimeters)
    {
        ValidateCaravanAmount(maximumMillimeters, nameof(maximumMillimeters));
        lock (sync)
        {
            var index = CheckedIndex(x, y);
            fluxState.Begin(SampleValue);
            var result = CaravanInterventionEngine.CondenseAtmosphericWater(
                state, waterBudget, fluxState, index, x, y, maximumMillimeters);
            fluxState.Complete(0d, SampleValue);
            return result;
        }
    }

    public CaravanInterventionResult MeltAndWithdrawSnow(int x, int y, float maximumSweMillimeters)
    {
        ValidateCaravanAmount(maximumSweMillimeters, nameof(maximumSweMillimeters));
        lock (sync)
        {
            fluxState.Begin(SampleValue);
            var index = CheckedIndex(x, y);
            var result = CaravanInterventionEngine.WithdrawSnow(
                state, waterBudget, fluxState, index, x, y, maximumSweMillimeters);
            fluxState.Complete(0, SampleValue);
            return result;
        }
    }

    public CaravanInterventionResult HarvestBiomass(
        int x,
        int y,
        float maximumLiveGm2,
        float maximumDryGm2)
    {
        ValidateCaravanAmount(maximumLiveGm2, nameof(maximumLiveGm2));
        ValidateCaravanAmount(maximumDryGm2, nameof(maximumDryGm2));
        lock (sync)
        {
            fluxState.Begin(SampleValue);
            var index = CheckedIndex(x, y);
            var result = CaravanInterventionEngine.HarvestBiomass(
                state,
                nitrogenBudget,
                fluxState,
                index,
                x,
                y,
                maximumLiveGm2,
                maximumDryGm2);
            fluxState.Complete(0, SampleValue);
            return result;
        }
    }

    public CaravanInterventionResult ReturnOrganicMatter(
        int x,
        int y,
        float organicMatterGm2,
        float organicNitrogenGm2)
    {
        ValidateCaravanAmount(organicMatterGm2, nameof(organicMatterGm2));
        ValidateCaravanAmount(organicNitrogenGm2, nameof(organicNitrogenGm2));
        lock (sync)
        {
            fluxState.Begin(SampleValue);
            var index = CheckedIndex(x, y);
            var result = CaravanInterventionEngine.ReturnOrganicMatter(
                state,
                nitrogenBudget,
                fluxState,
                index,
                x,
                y,
                organicMatterGm2,
                organicNitrogenGm2);
            fluxState.Complete(0, SampleValue);
            return result;
        }
    }

    public CaravanInterventionResult CaptureDust(int x, int y, float maximumDustGm2)
    {
        ValidateCaravanAmount(maximumDustGm2, nameof(maximumDustGm2));
        lock (sync)
        {
            fluxState.Begin(SampleValue);
            var index = CheckedIndex(x, y);
            var result = CaravanInterventionEngine.CaptureDust(
                state, fluxState, index, x, y, maximumDustGm2);
            fluxState.Complete(0, SampleValue);
            return result;
        }
    }

    public CaravanInterventionResult ReturnSediment(int x, int y, float sedimentKgM2)
    {
        ValidateCaravanAmount(sedimentKgM2, nameof(sedimentKgM2));
        lock (sync)
        {
            fluxState.Begin(SampleValue);
            var index = CheckedIndex(x, y);
            var result = CaravanInterventionEngine.ReturnSediment(
                state, fluxState, index, x, y, sedimentKgM2);
            fluxState.Complete(0, SampleValue);
            return result;
        }
    }

    public CaravanInterventionResult CompactTrail(int x, int y, float compactionDelta)
    {
        ValidateCaravanAmount(compactionDelta, nameof(compactionDelta));
        lock (sync)
        {
            fluxState.Begin(SampleValue);
            var index = CheckedIndex(x, y);
            var result = CaravanInterventionEngine.CompactTrail(
                state, fluxState, index, x, y, compactionDelta);
            fluxState.Complete(0, SampleValue);
            return result;
        }
    }

    private static void ValidateCaravanAmount(float value, string name)
    {
        if (!float.IsFinite(value) || value < 0f)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }
}

internal static class CaravanInterventionEngine
{
    private const float ProtectedLiveBiomassGm2 = 8f;
    private const float ProtectedDryBiomassGm2 = 3f;

    public static CaravanInterventionResult WithdrawSurfaceWater(
        WorldState state,
        WaterBudget budget,
        WorldFluxState flux,
        int index,
        int x,
        int y,
        float maximum)
    {
        var amount = Math.Min(state.SurfaceWaterMm[index], maximum);
        state.SurfaceWaterMm[index] -= amount;
        budget.AddExternal(-amount);
        flux.Add(SimulationFlux.CaravanSurfaceWaterWithdrawal, index, amount);
        return new CaravanInterventionResult(
            CaravanInterventionKind.WithdrawSurfaceWater,
            x,
            y,
            SurfaceWaterMm: amount);
    }

    public static CaravanInterventionResult ReturnSurfaceWater(
        WorldState state,
        WaterBudget budget,
        WorldFluxState flux,
        int index,
        int x,
        int y,
        float amount)
    {
        state.SurfaceWaterMm[index] += amount;
        budget.AddExternal(amount);
        flux.Add(SimulationFlux.CaravanSurfaceWaterReturn, index, amount);
        return new CaravanInterventionResult(
            CaravanInterventionKind.ReturnSurfaceWater,
            x,
            y,
            SurfaceWaterMm: amount);
    }

    public static CaravanInterventionResult CondenseAtmosphericWater(
        WorldState state,
        WaterBudget budget,
        WorldFluxState flux,
        int index,
        int x,
        int y,
        float maximum)
    {
        const float protectedHumidityMm = 1.5f;
        var amount = Math.Min(maximum, Math.Max(0f, state.AirHumidityMm[index] - protectedHumidityMm));
        state.AirHumidityMm[index] -= amount;
        budget.AddExternal(-amount);
        flux.Add(SimulationFlux.CaravanAtmosphericWaterWithdrawal, index, amount);
        return new CaravanInterventionResult(
            CaravanInterventionKind.CondenseAtmosphericWater,
            x,
            y,
            AtmosphericWaterMm: amount);
    }

    public static CaravanInterventionResult WithdrawSnow(
        WorldState state,
        WaterBudget budget,
        WorldFluxState flux,
        int index,
        int x,
        int y,
        float maximum)
    {
        var amount = Math.Min(state.SnowWaterEquivalentMm[index], maximum);
        state.SnowWaterEquivalentMm[index] -= amount;
        budget.AddExternal(-amount);
        flux.Add(SimulationFlux.CaravanSnowWithdrawal, index, amount);
        return new CaravanInterventionResult(
            CaravanInterventionKind.MeltAndWithdrawSnow,
            x,
            y,
            SnowWaterEquivalentMm: amount);
    }

    public static CaravanInterventionResult HarvestBiomass(
        WorldState state,
        NitrogenBudget budget,
        WorldFluxState flux,
        int index,
        int x,
        int y,
        float maximumLive,
        float maximumDry)
    {
        var liveBefore = state.LiveBiomassGm2[index];
        var live = Math.Min(maximumLive, Math.Max(0f, liveBefore - ProtectedLiveBiomassGm2));
        var plantNitrogen = liveBefore > 1e-8f
            ? Math.Min(state.PlantNitrogenGm2[index], state.PlantNitrogenGm2[index] * live / liveBefore)
            : 0f;
        var dry = Math.Min(maximumDry, Math.Max(0f, state.DryBiomassGm2[index] - ProtectedDryBiomassGm2));
        var organicNitrogen = Math.Min(
            state.OrganicNitrogenGm2[index],
            Math.Min(dry * 0.012f, state.OrganicNitrogenGm2[index] * 0.02f));

        state.LiveBiomassGm2[index] -= live;
        state.DryBiomassGm2[index] -= dry;
        state.PlantNitrogenGm2[index] -= plantNitrogen;
        state.OrganicNitrogenGm2[index] -= organicNitrogen;
        budget.AddExternal(-(plantNitrogen + organicNitrogen));
        flux.Add(SimulationFlux.CaravanLiveHarvest, index, live);
        flux.Add(SimulationFlux.CaravanDryHarvest, index, dry);
        flux.Add(SimulationFlux.CaravanPlantNitrogenWithdrawal, index, plantNitrogen);
        flux.Add(SimulationFlux.CaravanOrganicNitrogenWithdrawal, index, organicNitrogen);
        return new CaravanInterventionResult(
            CaravanInterventionKind.HarvestBiomass,
            x,
            y,
            LiveBiomassGm2: live,
            DryBiomassGm2: dry,
            NitrogenGm2: plantNitrogen + organicNitrogen);
    }

    public static CaravanInterventionResult ReturnOrganicMatter(
        WorldState state,
        NitrogenBudget budget,
        WorldFluxState flux,
        int index,
        int x,
        int y,
        float matter,
        float nitrogen)
    {
        state.LitterBiomassGm2[index] += matter;
        state.OrganicNitrogenGm2[index] += nitrogen;
        budget.AddExternal(nitrogen);
        flux.Add(SimulationFlux.CaravanOrganicMatterReturn, index, matter);
        flux.Add(SimulationFlux.CaravanOrganicNitrogenReturn, index, nitrogen);
        return new CaravanInterventionResult(
            CaravanInterventionKind.ReturnOrganicMatter,
            x,
            y,
            NitrogenGm2: nitrogen,
            OrganicMatterGm2: matter);
    }

    public static CaravanInterventionResult CaptureDust(
        WorldState state,
        WorldFluxState flux,
        int index,
        int x,
        int y,
        float maximumDust)
    {
        var dust = Math.Min(state.DustGm2[index], maximumDust);
        state.DustGm2[index] -= dust;
        flux.Add(SimulationFlux.CaravanDustCapture, index, dust);
        return new CaravanInterventionResult(
            CaravanInterventionKind.CaptureDust,
            x,
            y,
            DustGm2: dust,
            SedimentKgM2: dust * 0.001f);
    }

    public static CaravanInterventionResult ReturnSediment(
        WorldState state,
        WorldFluxState flux,
        int index,
        int x,
        int y,
        float sediment)
    {
        state.LooseSedimentKgM2[index] += sediment;
        flux.Add(SimulationFlux.CaravanSedimentReturn, index, sediment);
        return new CaravanInterventionResult(
            CaravanInterventionKind.ReturnSediment,
            x,
            y,
            SedimentKgM2: sediment);
    }

    public static CaravanInterventionResult CompactTrail(
        WorldState state,
        WorldFluxState flux,
        int index,
        int x,
        int y,
        float requested)
    {
        var before = state.SoilCompactionFraction[index];
        state.SoilCompactionFraction[index] = Math.Min(1f, before + requested);
        var applied = state.SoilCompactionFraction[index] - before;
        flux.Add(SimulationFlux.CaravanCompaction, index, applied);
        return new CaravanInterventionResult(
            CaravanInterventionKind.CompactTrail,
            x,
            y,
            CompactionDelta: applied);
    }
}
