namespace Steppe.Simulation;

public enum CaravanOpportunityKind
{
    SurfaceWater,
    RainFront,
    GroundwaterDischarge,
    Snowpack,
    SnowmeltRunoff,
    GreenGrowth,
    DryStandingBiomass,
    DryingWindow,
    DustFront,
    DepositionZone,
    ErosionFlow,
    RecoverableSoil,
    OrganicPoorSoil,
    FrozenCorridor,
    HarvesterFront,
    HarvesterMolt,
    Wildfire
}

public sealed record CaravanFlowReading(
    VectorProcess Process,
    float X,
    float Y,
    float Magnitude);

public sealed record CaravanOpportunity(
    CaravanOpportunityKind Kind,
    int X,
    int Y,
    float Signal,
    float Score,
    float DistanceCells,
    VectorProcess PrimaryFlow,
    float FlowX,
    float FlowY,
    float FlowMagnitude);

public sealed record CaravanOpportunityScan(
    int OriginX,
    int OriginY,
    int RadiusCells,
    CaravanFlowReading[] LocalFlows,
    CaravanOpportunity[] Opportunities);

/// <summary>
/// Interprets the physical world as readable caravan opportunities. It owns no
/// world state and does not create resources: it is the simulation equivalent
/// of what a player can infer by looking at terrain, weather and moving fronts.
/// </summary>
public sealed partial class FiniteWorld
{
    public CaravanOpportunityScan ScanCaravanOpportunities(int x, int y, int radiusCells = 24)
    {
        if (radiusCells < 0 || radiusCells > Math.Max(Config.Width, Config.Height))
        {
            throw new ArgumentOutOfRangeException(nameof(radiusCells));
        }

        lock (sync)
        {
            CheckedIndex(x, y);
            return CaravanOpportunityDetector.Scan(
                Config, state, fauna, fluxState, x, y, radiusCells);
        }
    }
}

internal static class CaravanOpportunityDetector
{
    public static CaravanOpportunityScan Scan(
        WorldConfig config,
        WorldState state,
        WorldFauna fauna,
        WorldFluxState flux,
        int originX,
        int originY,
        int radius)
    {
        var best = new Dictionary<CaravanOpportunityKind, CaravanOpportunity>();
        var minimumX = Math.Max(0, originX - radius);
        var maximumX = Math.Min(config.Width - 1, originX + radius);
        var minimumY = Math.Max(0, originY - radius);
        var maximumY = Math.Min(config.Height - 1, originY + radius);
        var radiusSquared = radius * radius;
        for (var y = minimumY; y <= maximumY; y++)
        {
            for (var x = minimumX; x <= maximumX; x++)
            {
                var dx = x - originX;
                var dy = y - originY;
                if (dx * dx + dy * dy > radiusSquared) continue;
                var index = y * config.Width + x;
                var distance = MathF.Sqrt(dx * dx + dy * dy);
                ObserveCell(config, state, flux, best, index, x, y, distance);
            }
        }

        var faunaSnapshot = fauna.Capture();
        foreach (var harvester in faunaSnapshot.Harvesters)
        {
            var distance = Distance(harvester.X, harvester.Y, originX, originY);
            if (distance > radius) continue;
            var x = Math.Clamp((int)MathF.Round(harvester.X), 0, config.Width - 1);
            var y = Math.Clamp((int)MathF.Round(harvester.Y), 0, config.Height - 1);
            var index = y * config.Width + x;
            var signal = 1.5f
                + state.LiveBiomassGm2[index] * 0.01f
                + (harvester.Activity == GiantHarvesterActivity.Migrating ? 0.8f : 0.25f);
            var opportunity = Create(
                config,
                state,
                flux,
                CaravanOpportunityKind.HarvesterFront,
                x,
                y,
                signal,
                signal - distance * 0.02f,
                distance,
                VectorProcess.GiantHarvesterMovement);
            if (opportunity.FlowMagnitude <= 1e-6f)
            {
                opportunity = opportunity with
                {
                    FlowX = harvester.HeadingX,
                    FlowY = harvester.HeadingY,
                    FlowMagnitude = 1f
                };
            }
            Consider(best, opportunity);
        }

        foreach (var molt in faunaSnapshot.Molts)
        {
            var distance = Distance(molt.X, molt.Y, originX, originY);
            if (distance > radius) continue;
            var x = Math.Clamp((int)MathF.Round(molt.X), 0, config.Width - 1);
            var y = Math.Clamp((int)MathF.Round(molt.Y), 0, config.Height - 1);
            var signal = MathF.Min(8f, molt.ChitinKg * 0.18f);
            Consider(best, Create(
                config,
                state,
                flux,
                CaravanOpportunityKind.HarvesterMolt,
                x,
                y,
                signal,
                signal - distance * 0.025f,
                distance,
                VectorProcess.GiantHarvesterMovement));
        }

        var originIndex = originY * config.Width + originX;
        var localFlows = Enum.GetValues<VectorProcess>()
            .Select(process => ReadFlow(config, state, flux, process, originIndex))
            .ToArray();
        return new CaravanOpportunityScan(
            originX,
            originY,
            radius,
            localFlows,
            best.Values.OrderByDescending(item => item.Score).ThenBy(item => item.Kind).ToArray());
    }

    private static void ObserveCell(
        WorldConfig config,
        WorldState state,
        WorldFluxState flux,
        Dictionary<CaravanOpportunityKind, CaravanOpportunity> best,
        int index,
        int x,
        int y,
        float distance)
    {
        var surfaceWater = state.SurfaceWaterMm[index];
        var runoff = Magnitude(ReadFlow(config, state, flux, VectorProcess.SurfaceRunoff, index));
        if (surfaceWater > 0.008f)
        {
            var signal = MathF.Min(18f, MathF.Sqrt(surfaceWater) * 4.5f + runoff * 0.8f);
            Add(best, config, state, flux, CaravanOpportunityKind.SurfaceWater, x, y, signal, distance, VectorProcess.SurfaceRunoff);
        }

        var rainfall = flux.Value(SimulationFlux.Rainfall, index);
        var rainSignal = state.PrecipitationMmPerHour[index] * 3.5f
            + state.CloudWaterMm[index] * 0.32f
            + rainfall * 0.45f;
        if (state.PrecipitationMmPerHour[index] > 0.001f || state.CloudWaterMm[index] > 0.12f)
        {
            Add(best, config, state, flux, CaravanOpportunityKind.RainFront, x, y, rainSignal, distance, VectorProcess.CloudAdvection);
        }

        var discharge = flux.Value(SimulationFlux.GroundwaterDischarge, index);
        if (discharge > 0.0005f)
        {
            Add(best, config, state, flux, CaravanOpportunityKind.GroundwaterDischarge, x, y,
                discharge * 12f + surfaceWater * 0.6f, distance, VectorProcess.GroundwaterFlow);
        }

        var snow = state.SnowWaterEquivalentMm[index];
        if (snow > 0.5f)
        {
            Add(best, config, state, flux, CaravanOpportunityKind.Snowpack, x, y,
                MathF.Min(15f, MathF.Sqrt(snow) * 0.55f), distance, VectorProcess.SnowTransport);
        }

        var melt = flux.Value(SimulationFlux.SnowMelt, index);
        if (melt > 1e-5f && runoff > 1e-6f)
        {
            Add(best, config, state, flux, CaravanOpportunityKind.SnowmeltRunoff, x, y,
                melt * 10f + runoff * 1.4f + surfaceWater * 0.2f, distance, VectorProcess.SurfaceRunoff);
        }

        var growth = flux.Value(SimulationFlux.PlantGrowth, index);
        if (growth > 0.004f)
        {
            Add(best, config, state, flux, CaravanOpportunityKind.GreenGrowth, x, y,
                growth * 1.8f + state.LiveBiomassGm2[index] * 0.012f, distance, VectorProcess.HumidityAdvection);
        }

        if (state.DryBiomassGm2[index] > 14f)
        {
            Add(best, config, state, flux, CaravanOpportunityKind.DryStandingBiomass, x, y,
                state.DryBiomassGm2[index] * 0.022f, distance, VectorProcess.Wind);
        }

        var wind = MathF.Sqrt(state.WindXMs[index] * state.WindXMs[index] + state.WindYMs[index] * state.WindYMs[index]);
        if (state.SolarRadiationWm2[index] > 180f
            && state.PrecipitationMmPerHour[index] < 0.015f
            && state.AirHumidityMm[index] < 19f
            && wind > 0.7f)
        {
            var signal = state.SolarRadiationWm2[index] / 260f
                + MathF.Min(wind, 12f) * 0.09f
                + MathF.Max(0f, 1f - state.AirHumidityMm[index] / 19f)
                - state.CloudWaterMm[index] * 0.05f;
            Add(best, config, state, flux, CaravanOpportunityKind.DryingWindow, x, y, signal, distance, VectorProcess.Wind);
        }

        var dustLift = flux.Value(SimulationFlux.DustLift, index);
        if (state.DustGm2[index] > 0.025f || dustLift > 0.004f)
        {
            Add(best, config, state, flux, CaravanOpportunityKind.DustFront, x, y,
                state.DustGm2[index] * 2.2f + dustLift * 4f, distance, VectorProcess.DustAdvection);
        }

        var dustDeposition = flux.Value(SimulationFlux.DustDeposition, index);
        var sedimentDeposition = flux.Value(SimulationFlux.SedimentDeposition, index);
        if (dustDeposition > 0.002f || sedimentDeposition > 0.002f)
        {
            Add(best, config, state, flux, CaravanOpportunityKind.DepositionZone, x, y,
                dustDeposition * 3f + sedimentDeposition * 2f + 0.2f, distance, VectorProcess.SedimentTransport);
        }

        var erosion = flux.Value(SimulationFlux.WaterErosion, index);
        if (erosion > 0.0001f)
        {
            Add(best, config, state, flux, CaravanOpportunityKind.ErosionFlow, x, y,
                erosion * 3f + runoff * 0.6f, distance, VectorProcess.SedimentTransport);
        }

        if (state.LiveBiomassGm2[index] < 24f
            && state.RootWaterMm[index] < 55f
            && state.SeedBank[index] > 0.22f
            && state.SoilTemperatureC[index] is > -4f and < 34f
            && state.FireIntensityFraction[index] < 0.02f)
        {
            var signal = (1f - state.LiveBiomassGm2[index] / 24f)
                * state.SeedBank[index]
                * (1f - Math.Clamp(state.RootWaterMm[index] / 55f, 0f, 1f));
            Add(best, config, state, flux, CaravanOpportunityKind.RecoverableSoil, x, y, signal, distance, VectorProcess.HumidityAdvection);
        }

        if ((state.SoilOrganicMatterGm2[index] < 1250f
             || state.AvailableNitrogenGm2[index] < 0.65f)
            && state.SeedBank[index] > 0.18f)
        {
            var signal = MathF.Max(0f, 1f - state.SoilOrganicMatterGm2[index] / 1250f)
                + MathF.Max(0f, 1f - state.AvailableNitrogenGm2[index] / 0.65f)
                + state.SeedBank[index] * 0.3f;
            Add(best, config, state, flux, CaravanOpportunityKind.OrganicPoorSoil, x, y, signal, distance, VectorProcess.HumidityAdvection);
        }

        if (state.FrozenSoilFraction[index] > 0.45f
            && surfaceWater < 0.08f
            && snow < 18f
            && state.Slope[index] < 0.16f)
        {
            var signal = state.FrozenSoilFraction[index]
                + (0.16f - state.Slope[index]) * 3f
                - snow * 0.015f;
            Add(best, config, state, flux, CaravanOpportunityKind.FrozenCorridor, x, y, signal, distance, VectorProcess.DrainageDirection);
        }

        if (state.FireIntensityFraction[index] > 0.005f)
        {
            Add(best, config, state, flux, CaravanOpportunityKind.Wildfire, x, y,
                state.FireIntensityFraction[index] * 12f, distance, VectorProcess.FireSpread);
        }
    }

    private static void Add(
        Dictionary<CaravanOpportunityKind, CaravanOpportunity> best,
        WorldConfig config,
        WorldState state,
        WorldFluxState flux,
        CaravanOpportunityKind kind,
        int x,
        int y,
        float signal,
        float distance,
        VectorProcess flow)
    {
        if (!float.IsFinite(signal) || signal <= 0f) return;
        var score = signal - distance * 0.018f;
        Consider(best, Create(config, state, flux, kind, x, y, signal, score, distance, flow));
    }

    private static CaravanOpportunity Create(
        WorldConfig config,
        WorldState state,
        WorldFluxState flux,
        CaravanOpportunityKind kind,
        int x,
        int y,
        float signal,
        float score,
        float distance,
        VectorProcess process)
    {
        var reading = ReadFlow(config, state, flux, process, y * config.Width + x);
        return new CaravanOpportunity(
            kind,
            x,
            y,
            signal,
            score,
            distance,
            process,
            reading.X,
            reading.Y,
            reading.Magnitude);
    }

    private static void Consider(
        Dictionary<CaravanOpportunityKind, CaravanOpportunity> best,
        CaravanOpportunity candidate)
    {
        if (!float.IsFinite(candidate.Score) || candidate.Score <= 0.01f) return;
        if (!best.TryGetValue(candidate.Kind, out var current)
            || candidate.Score > current.Score
            || candidate.Score == current.Score && candidate.DistanceCells < current.DistanceCells)
        {
            best[candidate.Kind] = candidate;
        }
    }

    private static CaravanFlowReading ReadFlow(
        WorldConfig config,
        WorldState state,
        WorldFluxState flux,
        VectorProcess process,
        int index)
    {
        float x;
        float y;
        if (process == VectorProcess.Wind)
        {
            x = state.WindXMs[index];
            y = state.WindYMs[index];
        }
        else if (process == VectorProcess.DrainageDirection)
        {
            var target = state.DrainTo[index];
            if (target < 0 || target == index)
            {
                x = 0f;
                y = 0f;
            }
            else
            {
                x = target % config.Width - index % config.Width;
                y = target / config.Width - index / config.Width;
                var length = MathF.Sqrt(x * x + y * y);
                if (length > 1e-8f)
                {
                    x /= length;
                    y /= length;
                }
            }
        }
        else
        {
            var value = flux.VectorValue(process, index);
            x = value.X;
            y = value.Y;
        }

        return new CaravanFlowReading(process, x, y, MathF.Sqrt(x * x + y * y));
    }

    private static float Magnitude(CaravanFlowReading reading) => reading.Magnitude;

    private static float Distance(float ax, float ay, float bx, float by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}
