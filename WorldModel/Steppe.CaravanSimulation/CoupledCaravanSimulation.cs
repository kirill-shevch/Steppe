using Steppe.Simulation;

namespace Steppe.CaravanSimulation;

/// <summary>
/// Deterministic orchestrator for a physical caravan and the finite steppe.
/// Every tick runs inside one FiniteWorld lock and one diagnostic flux window.
/// </summary>
public sealed class CoupledCaravanSimulation
{
    private readonly FiniteWorld world;
    private readonly CaravanSimulation caravan;
    private readonly ICaravanPolicy policy;
    private readonly int scoutRadiusCells;

    public CoupledCaravanSimulation(
        FiniteWorld world,
        CaravanSimulation caravan,
        ICaravanPolicy policy,
        double tickHours = 3d,
        int scoutRadiusCells = 28)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(caravan);
        ArgumentNullException.ThrowIfNull(policy);
        if (!double.IsFinite(tickHours) || tickHours <= 0d || tickHours > 24d)
            throw new ArgumentOutOfRangeException(nameof(tickHours));
        if (scoutRadiusCells < 0 || scoutRadiusCells > Math.Max(world.Config.Width, world.Config.Height))
            throw new ArgumentOutOfRangeException(nameof(scoutRadiusCells));
        this.world = world;
        this.caravan = caravan;
        this.policy = policy;
        TickHours = tickHours;
        this.scoutRadiusCells = scoutRadiusCells;
    }

    public double TickHours { get; }
    public FiniteWorld World => world;
    public CaravanStateSnapshot CaptureCaravan() => caravan.Capture();
    public string PolicyName => policy.Name;

    public CaravanStepResult Advance(CancellationToken cancellationToken = default)
    {
        return world.AdvanceWithCaravan(
            TickHours,
            access =>
            {
                var before = caravan.Capture();
                var x = Math.Clamp((int)MathF.Round(before.XCells), 0, access.Config.Width - 1);
                var y = Math.Clamp((int)MathF.Round(before.YCells), 0, access.Config.Height - 1);
                var cell = access.SampleCell(x, y);
                var scan = access.ScanOpportunities(x, y, scoutRadiusCells);
                var observation = new CaravanObservation(
                    access.Clock.Year,
                    access.Clock.DayOfYear,
                    access.Clock.HourOfDay,
                    before.XCells,
                    before.YCells,
                    access.Config.Width,
                    access.Config.Height,
                    access.Config.CellSizeMeters,
                    cell,
                    scan,
                    ClassifyRegime(cell));
                var decision = policy.Decide(observation, before);
                var intakeRequest = caravan.BuildIntakeRequest(observation, decision, TickHours);
                var intake = access.Exchange(intakeRequest);
                var internalStep = caravan.AdvanceInternal(
                    observation,
                    decision,
                    intake,
                    TickHours,
                    access.Config.CellSizeMeters);

                ApplyTrail(access, internalStep.Trail, caravan.Capture().TotalMassKg);
                var returnRequest = caravan.BuildReturnRequest(decision);
                var returned = access.Exchange(returnRequest);
                caravan.AcknowledgeReturn(returned, internalStep.Accounting);
                var ledger = caravan.CompleteLedger(internalStep.Accounting);
                return new CaravanStepResult(
                    observation,
                    decision,
                    TickHours,
                    internalStep.DistanceKilometers,
                    internalStep.WaterFulfillment,
                    internalStep.OrganicFulfillment,
                    internalStep.NitrogenFulfillment,
                    internalStep.ThermalFulfillment,
                    intake,
                    returned,
                    ledger,
                    internalStep.Actions,
                    internalStep.Trail,
                    caravan.Capture());
            },
            cancellationToken);
    }

    public IReadOnlyList<CaravanStepResult> AdvanceDays(
        int days,
        CancellationToken cancellationToken = default)
    {
        if (days < 0) throw new ArgumentOutOfRangeException(nameof(days));
        var count = checked((int)Math.Ceiling(days * 24d / TickHours));
        var results = new List<CaravanStepResult>(count);
        for (var step = 0; step < count; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(Advance(cancellationToken));
        }
        return results;
    }

    private static void ApplyTrail(
        CaravanWorldAccess access,
        IReadOnlyList<CaravanTrailCell> trail,
        float massKg)
    {
        var massFactor = Math.Clamp(massKg / 8000f, 0.25f, 5f);
        foreach (var point in trail)
        {
            var x = Math.Clamp(point.X, 0, access.Config.Width - 1);
            var y = Math.Clamp(point.Y, 0, access.Config.Height - 1);
            var cell = access.SampleCell(x, y);
            var wetness = Math.Clamp(cell.SurfaceWaterMm / 8f, 0f, 1f);
            var frozenProtection = 1f - cell.FrozenSoilFraction * 0.75f;
            var vegetationKg = point.DistanceKilometers * 4f * massFactor
                * Math.Clamp((cell.LiveBiomassGm2 + cell.DryBiomassGm2) / 180f, 0.1f, 1.5f);
            var wind = MathF.Sqrt(cell.WindXMs * cell.WindXMs + cell.WindYMs * cell.WindYMs);
            var dryness = Math.Clamp(1f - wetness - cell.SnowWaterEquivalentMm / 12f, 0f, 1f);
            var dustKg = point.DistanceKilometers * 0.7f * massFactor * dryness
                * (0.4f + Math.Clamp(wind / 12f, 0f, 1f) * 0.6f);
            access.Exchange(new CaravanPhysicalExchangeRequest
            {
                X = x,
                Y = y,
                CompactionDelta = 0.00018f * massFactor * frozenProtection * (0.65f + wetness * 0.7f),
                TrailVegetationDamageKg = vegetationKg,
                DustLiftKg = dustKg
            });
        }
    }

    private static CaravanRegime ClassifyRegime(CellSnapshot cell)
    {
        var regime = CaravanRegime.None;
        var solar = cell.States.First(item => item.State == SimulationLayer.SolarRadiation).Value;
        var wind = MathF.Sqrt(cell.WindXMs * cell.WindXMs + cell.WindYMs * cell.WindYMs);
        if (cell.SurfaceWaterMm > 0.5f || cell.GroundwaterMm > 120f) regime |= CaravanRegime.WaterRich;
        if (cell.LiveBiomassGm2 + cell.DryBiomassGm2 > 150f) regime |= CaravanRegime.BiomassRich;
        if (solar > 520f) regime |= CaravanRegime.SolarRich;
        if (wind > 7.5f) regime |= CaravanRegime.WindRich;
        if (cell.AirTemperatureC < 2f || cell.SnowWaterEquivalentMm > 1f) regime |= CaravanRegime.ColdOrSnow;
        if (cell.DustGm2 > 0.08f) regime |= CaravanRegime.DustStressed;
        if (cell.SurfaceWaterMm > 10f) regime |= CaravanRegime.Flooded;
        if (cell.BurnScarFraction > 0.15f) regime |= CaravanRegime.PostFire;
        return regime;
    }
}
