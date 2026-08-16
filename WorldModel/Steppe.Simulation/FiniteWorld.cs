namespace Steppe.Simulation;

/// <summary>
/// A finite world that advances independently of rendering, frame rate, or player position.
/// </summary>
public sealed partial class FiniteWorld
{
    private readonly object sync = new();
    private readonly WorldState state;
    private readonly WaterBudget waterBudget;
    private readonly NitrogenBudget nitrogenBudget;
    private readonly WorldFluxState fluxState;
    private readonly WorldHistory history;
    private readonly WorldEventLog events;
    private readonly WorldFauna fauna;

    public FiniteWorld(WorldConfig config)
    {
        Config = config.Validate();
        Clock = new WorldClock();
        state = WorldGenerator.Generate(Config);
        state.RebuildDerivedCaches(Config);
        waterBudget = new WaterBudget();
        waterBudget.Initialize(state);
        nitrogenBudget = new NitrogenBudget();
        nitrogenBudget.Initialize(state);
        fluxState = new WorldFluxState(Config.CellCount);
        fauna = WorldFauna.Generate(Config);
        history = new WorldHistory();
        history.Initialize(Clock.ElapsedHours, CaptureHistoryPoint, CaptureCellValues);
        events = new WorldEventLog();
        events.Initialize(CaptureRegimeMetrics());
    }

    internal FiniteWorld(
        WorldConfig config,
        WorldClock clock,
        WorldState state,
        WaterBudget waterBudget,
        NitrogenBudget nitrogenBudget,
        WorldFauna fauna)
    {
        Config = config.Validate();
        Clock = clock;
        this.state = state;
        this.state.RebuildDerivedCaches(Config);
        this.waterBudget = waterBudget;
        this.nitrogenBudget = nitrogenBudget;
        this.fauna = fauna;
        fluxState = new WorldFluxState(Config.CellCount);
        history = new WorldHistory();
        history.Initialize(Clock.ElapsedHours, CaptureHistoryPoint, CaptureCellValues);
        events = new WorldEventLog();
        events.Initialize(CaptureRegimeMetrics());
    }

    public WorldConfig Config { get; }
    public WorldClock Clock { get; }

    public void AdvanceHours(double hours, CancellationToken cancellationToken = default)
    {
        if (!double.IsFinite(hours) || hours < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hours));
        }

        lock (sync)
        {
            fluxState.Begin(SampleValue);
            var remaining = hours;
            var advanced = 0d;
            var baseStepHours = Config.BaseStepMinutes / 60d;
            try
            {
                while (remaining > 1e-9)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var step = Math.Min(baseStepHours, remaining);
                    WorldSystems.Step(Config, Clock, state, waterBudget, fluxState, step);
                    fauna.Step(Config, Clock, state, waterBudget, fluxState, (float)step);
                    Clock.Advance(step);
                    if (history.RecordIfDue(Clock.ElapsedHours, CaptureHistoryPoint, CaptureCellValues))
                    {
                        events.Observe(CaptureRegimeMetrics());
                    }
                    advanced += step;
                    remaining -= step;
                }
            }
            finally
            {
                fluxState.Complete(advanced, SampleValue);
            }
        }
    }

    public void AddSurfaceWater(int x, int y, float millimeters)
    {
        if (millimeters < 0 || !float.IsFinite(millimeters))
        {
            throw new ArgumentOutOfRangeException(nameof(millimeters));
        }

        lock (sync)
        {
            fluxState.Begin(SampleValue);
            var index = CheckedIndex(x, y);
            state.SurfaceWaterMm[index] += millimeters;
            waterBudget.AddExternal(millimeters);
            fluxState.Add(SimulationFlux.ExternalSurfaceWater, index, millimeters);
            fluxState.Complete(0, SampleValue);
        }
    }

    public void IgniteFire(int x, int y, float intensity = 1f)
    {
        if (!float.IsFinite(intensity) || intensity is <= 0f or > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(intensity));
        }

        lock (sync)
        {
            fluxState.Begin(SampleValue);
            var index = CheckedIndex(x, y);
            var before = state.FireIntensityFraction[index];
            state.FireIntensityFraction[index] = Math.Max(before, intensity);
            fluxState.Add(
                SimulationFlux.ExternalIgnition,
                index,
                state.FireIntensityFraction[index] - before);
            fluxState.Complete(0, SampleValue);
        }
    }

    public LayerSnapshot CaptureLayer(SimulationLayer layer)
    {
        lock (sync)
        {
            var descriptor = StateCatalog.Get(layer);
            var source = GetLayerSource(layer);
            var values = new float[Config.CellCount];
            float[]? vectorX = null;
            float[]? vectorY = null;
            var minimum = float.PositiveInfinity;
            var maximum = float.NegativeInfinity;

            if (layer == SimulationLayer.Wind)
            {
                vectorX = (float[])state.WindXMs.Clone();
                vectorY = (float[])state.WindYMs.Clone();
                for (var index = 0; index < Config.CellCount; index++)
                {
                    values[index] = MathF.Sqrt(
                        state.WindXMs[index] * state.WindXMs[index]
                        + state.WindYMs[index] * state.WindYMs[index]);
                }
            }
            else if (layer == SimulationLayer.Drainage)
            {
                vectorX = new float[Config.CellCount];
                vectorY = new float[Config.CellCount];
                for (var index = 0; index < Config.CellCount; index++)
                {
                    var target = state.DrainTo[index];
                    if (target < 0 || target == index)
                    {
                        continue;
                    }

                    var dx = target % Config.Width - index % Config.Width;
                    var dy = target / Config.Width - index / Config.Width;
                    var magnitude = MathF.Sqrt(dx * dx + dy * dy);
                    if (magnitude <= 0f)
                    {
                        continue;
                    }

                    vectorX[index] = dx / magnitude;
                    vectorY[index] = dy / magnitude;
                }
            }
            else
            {
                Array.Copy(source, values, values.Length);
            }

            foreach (var value in values)
            {
                if (!float.IsFinite(value))
                {
                    continue;
                }

                minimum = Math.Min(minimum, value);
                maximum = Math.Max(maximum, value);
            }

            if (!float.IsFinite(minimum))
            {
                minimum = maximum = 0f;
            }

            return new LayerSnapshot(
                layer,
                Config.Width,
                Config.Height,
                values,
                minimum,
                maximum,
                descriptor.Unit,
                BuildStatistics(values, descriptor, minimum, maximum),
                vectorX,
                vectorY);
        }
    }

    public FluxSnapshot CaptureFlux(SimulationFlux flux)
    {
        lock (sync)
        {
            var descriptor = FluxCatalog.Get(flux);
            var values = fluxState.CopyFlux(flux);
            double signed = 0;
            double absolute = 0;
            double positive = 0;
            double negative = 0;
            var active = 0;
            var nonFinite = 0;
            foreach (var value in values)
            {
                if (!float.IsFinite(value))
                {
                    nonFinite++;
                    continue;
                }

                signed += value;
                absolute += Math.Abs(value);
                if (value > 0) positive += value;
                if (value < 0) negative += -value;
                if (Math.Abs(value) > 1e-8f) active++;
            }

            return new FluxSnapshot(
                flux,
                Config.Width,
                Config.Height,
                fluxState.PeriodHours,
                descriptor.Unit,
                values,
                signed,
                absolute,
                positive,
                negative,
                active,
                nonFinite);
        }
    }

    public VectorProcessSnapshot CaptureVectorProcess(VectorProcess process)
    {
        lock (sync)
        {
            var descriptor = VectorProcessCatalog.Get(process);
            float[] vectorX;
            float[] vectorY;
            float[] grossMagnitude;
            var period = fluxState.PeriodHours;
            if (process == VectorProcess.Wind)
            {
                vectorX = (float[])state.WindXMs.Clone();
                vectorY = (float[])state.WindYMs.Clone();
                grossMagnitude = new float[Config.CellCount];
                period = 0;
            }
            else if (process == VectorProcess.DrainageDirection)
            {
                vectorX = new float[Config.CellCount];
                vectorY = new float[Config.CellCount];
                grossMagnitude = new float[Config.CellCount];
                for (var index = 0; index < Config.CellCount; index++)
                {
                    var target = state.DrainTo[index];
                    if (target < 0 || target == index)
                    {
                        continue;
                    }

                    var dx = target % Config.Width - index % Config.Width;
                    var dy = target / Config.Width - index / Config.Width;
                    var length = MathF.Sqrt(dx * dx + dy * dy);
                    vectorX[index] = dx / length;
                    vectorY[index] = dy / length;
                    grossMagnitude[index] = 1f;
                }

                period = 0;
            }
            else
            {
                (vectorX, vectorY) = fluxState.CopyVector(process);
                grossMagnitude = fluxState.CopyVectorGross(process);
            }

            var magnitude = new float[Config.CellCount];
            double total = 0;
            double totalGross = 0;
            var maximum = 0f;
            var active = 0;
            var nonFinite = 0;
            for (var index = 0; index < Config.CellCount; index++)
            {
                var value = MathF.Sqrt(vectorX[index] * vectorX[index] + vectorY[index] * vectorY[index]);
                magnitude[index] = value;
                if (!float.IsFinite(value) || !float.IsFinite(grossMagnitude[index]))
                {
                    nonFinite++;
                    continue;
                }

                total += value;
                if (process is VectorProcess.Wind or VectorProcess.DrainageDirection)
                {
                    grossMagnitude[index] = value;
                }
                totalGross += grossMagnitude[index];
                maximum = Math.Max(maximum, value);
                if (value > 1e-8f) active++;
            }

            return new VectorProcessSnapshot(
                process,
                Config.Width,
                Config.Height,
                period,
                descriptor.Unit,
                descriptor.Measure,
                vectorX,
                vectorY,
                magnitude,
                grossMagnitude,
                total,
                totalGross,
                totalGross > 1e-12 ? (float)(total / totalGross) : 0f,
                (float)(total / Config.CellCount),
                maximum,
                active,
                nonFinite);
        }
    }

    public CellSnapshot SampleCell(int x, int y)
    {
        lock (sync)
        {
            var index = CheckedIndex(x, y);
            var drainTo = state.DrainTo[index];
            return new CellSnapshot(
                x,
                y,
                state.ElevationM[index],
                state.Slope[index],
                state.CatchmentId[index],
                state.SurfaceTemperatureC[index],
                state.SoilTemperatureC[index],
                state.AirTemperatureC[index],
                state.AirPressureHpa[index],
                state.AirHumidityMm[index],
                state.CloudWaterMm[index],
                state.WindXMs[index],
                state.WindYMs[index],
                state.PrecipitationMmPerHour[index],
                state.SurfaceWaterMm[index],
                state.RootWaterMm[index],
                state.GroundwaterMm[index],
                state.SnowWaterEquivalentMm[index],
                state.FrozenSoilFraction[index],
                state.LiveBiomassGm2[index],
                state.DryBiomassGm2[index],
                state.LitterBiomassGm2[index],
                state.AvailableNitrogenGm2[index],
                state.SoilCompactionFraction[index],
                state.LooseSedimentKgM2[index],
                state.DustGm2[index],
                state.FireIntensityFraction[index],
                state.BurnScarFraction[index],
                BiomeClassifier.Describe(state, index),
                drainTo >= 0 ? drainTo % Config.Width : null,
                drainTo >= 0 ? drainTo / Config.Width : null,
                StateCatalog.All
                    .Select(descriptor => new CellStateValue(descriptor.Id, SampleValue(descriptor.Id, index)))
                    .ToArray());
        }
    }

    public StateExplanation ExplainCellState(int x, int y, SimulationLayer layer)
    {
        lock (sync)
        {
            var index = CheckedIndex(x, y);
            var stateUnit = StateCatalog.Get(layer).Unit;
            var contributions = FluxCatalog.Affecting(layer)
                .Select(item =>
                {
                    var amount = fluxState.Value(item.Descriptor.Id, index);
                    return new FluxContribution(
                        item.Descriptor.Id,
                        amount,
                        amount * item.Effect.Factor,
                        item.Descriptor.Unit,
                        stateUnit);
                })
                .Where(contribution => MathF.Abs(contribution.Amount) > 1e-8f)
                .OrderByDescending(contribution => MathF.Abs(contribution.StateContribution))
                .ToArray();
            var delta = fluxState.Delta(layer, index);
            var explained = contributions.Sum(contribution => contribution.StateContribution);
            var unexplained = delta - explained;
            if (MathF.Abs(unexplained) < 1e-6f)
            {
                unexplained = 0f;
            }

            return new StateExplanation(
                layer,
                SampleValue(layer, index),
                delta,
                fluxState.PeriodHours,
                explained,
                unexplained,
                contributions);
        }
    }

    public WorldHistorySnapshot CaptureWorldHistory(HistoryResolution resolution = HistoryResolution.Recent)
    {
        lock (sync)
        {
            return history.CaptureWorld(resolution);
        }
    }

    public CellHistorySnapshot CaptureCellHistory(
        int x,
        int y,
        SimulationLayer layer,
        HistoryResolution resolution = HistoryResolution.Recent)
    {
        lock (sync)
        {
            var index = CheckedIndex(x, y);
            return history.CaptureCell(index, x, y, layer, resolution);
        }
    }

    public bool PinCell(int x, int y)
    {
        lock (sync)
        {
            var index = CheckedIndex(x, y);
            return history.PinCell(index, x, y, Clock.ElapsedHours, CaptureCellValues);
        }
    }

    public bool UnpinCell(int x, int y)
    {
        lock (sync)
        {
            return history.UnpinCell(CheckedIndex(x, y));
        }
    }

    public PinnedCellSnapshot[] GetPinnedCells()
    {
        lock (sync)
        {
            return history.PinnedCells();
        }
    }

    public WorldRegimeSnapshot CaptureRegimeEvents(int recentLimit = 24)
    {
        lock (sync)
        {
            return events.Capture(recentLimit);
        }
    }

    public GiantHarvesterPopulationSnapshot CaptureGiantHarvesters()
    {
        lock (sync)
        {
            return fauna.Capture();
        }
    }

    public AnnualClimateRegime CaptureAnnualClimateRegime(int year)
    {
        lock (sync)
        {
            return ClimateRegimeModel.GetAnnualRegime(Config, year);
        }
    }

    public ClimateForcingSnapshot CaptureClimateForcing()
    {
        lock (sync)
        {
            return ClimateRegimeModel.GetForcing(Config, Clock);
        }
    }

    public WorldSummary GetSummary()
    {
        lock (sync)
        {
            var solar = Astronomy.Calculate(Config, Clock);
            return new WorldSummary(
                Config.Width,
                Config.Height,
                Config.CellSizeMeters,
                Config.Seed,
                Clock.ElapsedHours,
                Clock.Year,
                Clock.DayOfYear,
                Clock.HourOfDay,
                Clock.Season,
                solar.DayLengthHours,
                solar.ElevationRadians * 180d / Math.PI,
                Mean(state.SurfaceTemperatureC),
                Mean(state.PrecipitationMmPerHour),
                Mean(state.LiveBiomassGm2),
                waterBudget.Snapshot(state),
                nitrogenBudget.Snapshot(state));
        }
    }

    public void Save(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        lock (sync)
        {
            WorldPersistence.Save(destination, Config, Clock, state, waterBudget, nitrogenBudget, fauna);
        }
    }

    public void Save(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = File.Create(path);
        Save(stream);
    }

    public static FiniteWorld Load(Stream source) => WorldPersistence.Load(source);

    public static FiniteWorld Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    private float[] GetLayerSource(SimulationLayer layer) => layer switch
    {
        SimulationLayer.Elevation => state.ElevationM,
        SimulationLayer.Slope => state.Slope,
        SimulationLayer.Aspect => state.AspectRadians,
        SimulationLayer.SoilDepth => state.SoilDepthM,
        SimulationLayer.SandFraction => state.SandFraction,
        SimulationLayer.SiltFraction => state.SiltFraction,
        SimulationLayer.ClayFraction => state.ClayFraction,
        SimulationLayer.Porosity => state.Porosity,
        SimulationLayer.Permeability => state.PermeabilityMmPerHour,
        SimulationLayer.MineralContent => state.MineralContent,
        SimulationLayer.SoilCompaction => state.SoilCompactionFraction,
        SimulationLayer.FaultInfluence => state.FaultInfluence,
        SimulationLayer.RockHardness => state.RockHardness,
        SimulationLayer.DepressionStorage => state.DepressionStorageMm,
        SimulationLayer.Drainage => state.ScratchB,
        SimulationLayer.Catchments => CatchmentsAsFloat(state),
        SimulationLayer.SolarRadiation => state.SolarRadiationWm2,
        SimulationLayer.SurfaceTemperature => state.SurfaceTemperatureC,
        SimulationLayer.SoilTemperature => state.SoilTemperatureC,
        SimulationLayer.AirTemperature => state.AirTemperatureC,
        SimulationLayer.Pressure => state.AirPressureHpa,
        SimulationLayer.Wind => state.ScratchB,
        SimulationLayer.Humidity => state.AirHumidityMm,
        SimulationLayer.CloudWater => state.CloudWaterMm,
        SimulationLayer.Precipitation => state.PrecipitationMmPerHour,
        SimulationLayer.SurfaceWater => state.SurfaceWaterMm,
        SimulationLayer.RootWater => state.RootWaterMm,
        SimulationLayer.Groundwater => state.GroundwaterMm,
        SimulationLayer.Snow => state.SnowWaterEquivalentMm,
        SimulationLayer.FrozenSoil => state.FrozenSoilFraction,
        SimulationLayer.LiveBiomass => state.LiveBiomassGm2,
        SimulationLayer.DryBiomass => state.DryBiomassGm2,
        SimulationLayer.LitterBiomass => state.LitterBiomassGm2,
        SimulationLayer.SeedBank => state.SeedBank,
        SimulationLayer.SoilOrganicMatter => state.SoilOrganicMatterGm2,
        SimulationLayer.AvailableNitrogen => state.AvailableNitrogenGm2,
        SimulationLayer.PlantNitrogen => state.PlantNitrogenGm2,
        SimulationLayer.OrganicNitrogen => state.OrganicNitrogenGm2,
        SimulationLayer.LooseSediment => state.LooseSedimentKgM2,
        SimulationLayer.SurfaceCrust => state.SurfaceCrustFraction,
        SimulationLayer.Dust => state.DustGm2,
        SimulationLayer.FireIntensity => state.FireIntensityFraction,
        SimulationLayer.BurnScar => state.BurnScarFraction,
        _ => throw new ArgumentOutOfRangeException(nameof(layer), layer, null)
    };

    private float SampleValue(SimulationLayer layer, int index) => layer switch
    {
        SimulationLayer.Elevation => state.ElevationM[index],
        SimulationLayer.Slope => state.Slope[index],
        SimulationLayer.Aspect => state.AspectRadians[index],
        SimulationLayer.SoilDepth => state.SoilDepthM[index],
        SimulationLayer.SandFraction => state.SandFraction[index],
        SimulationLayer.SiltFraction => state.SiltFraction[index],
        SimulationLayer.ClayFraction => state.ClayFraction[index],
        SimulationLayer.Porosity => state.Porosity[index],
        SimulationLayer.Permeability => state.PermeabilityMmPerHour[index],
        SimulationLayer.MineralContent => state.MineralContent[index],
        SimulationLayer.SoilCompaction => state.SoilCompactionFraction[index],
        SimulationLayer.FaultInfluence => state.FaultInfluence[index],
        SimulationLayer.RockHardness => state.RockHardness[index],
        SimulationLayer.DepressionStorage => state.DepressionStorageMm[index],
        SimulationLayer.Drainage => 0f,
        SimulationLayer.Catchments => state.CatchmentId[index],
        SimulationLayer.SolarRadiation => state.SolarRadiationWm2[index],
        SimulationLayer.SurfaceTemperature => state.SurfaceTemperatureC[index],
        SimulationLayer.SoilTemperature => state.SoilTemperatureC[index],
        SimulationLayer.AirTemperature => state.AirTemperatureC[index],
        SimulationLayer.Pressure => state.AirPressureHpa[index],
        SimulationLayer.Wind => MathF.Sqrt(
            state.WindXMs[index] * state.WindXMs[index]
            + state.WindYMs[index] * state.WindYMs[index]),
        SimulationLayer.Humidity => state.AirHumidityMm[index],
        SimulationLayer.CloudWater => state.CloudWaterMm[index],
        SimulationLayer.Precipitation => state.PrecipitationMmPerHour[index],
        SimulationLayer.SurfaceWater => state.SurfaceWaterMm[index],
        SimulationLayer.RootWater => state.RootWaterMm[index],
        SimulationLayer.Groundwater => state.GroundwaterMm[index],
        SimulationLayer.Snow => state.SnowWaterEquivalentMm[index],
        SimulationLayer.FrozenSoil => state.FrozenSoilFraction[index],
        SimulationLayer.LiveBiomass => state.LiveBiomassGm2[index],
        SimulationLayer.DryBiomass => state.DryBiomassGm2[index],
        SimulationLayer.LitterBiomass => state.LitterBiomassGm2[index],
        SimulationLayer.SeedBank => state.SeedBank[index],
        SimulationLayer.SoilOrganicMatter => state.SoilOrganicMatterGm2[index],
        SimulationLayer.AvailableNitrogen => state.AvailableNitrogenGm2[index],
        SimulationLayer.PlantNitrogen => state.PlantNitrogenGm2[index],
        SimulationLayer.OrganicNitrogen => state.OrganicNitrogenGm2[index],
        SimulationLayer.LooseSediment => state.LooseSedimentKgM2[index],
        SimulationLayer.SurfaceCrust => state.SurfaceCrustFraction[index],
        SimulationLayer.Dust => state.DustGm2[index],
        SimulationLayer.FireIntensity => state.FireIntensityFraction[index],
        SimulationLayer.BurnScar => state.BurnScarFraction[index],
        _ => throw new ArgumentOutOfRangeException(nameof(layer), layer, null)
    };

    private static float[] CatchmentsAsFloat(WorldState state)
    {
        var values = state.ScratchB;
        for (var index = 0; index < values.Length; index++)
        {
            values[index] = state.CatchmentId[index];
        }

        return values;
    }

    private static LayerStatistics BuildStatistics(
        float[] values,
        StateDescriptor descriptor,
        float minimum,
        float maximum)
    {
        const int binCount = 32;
        const int quantileBinCount = 2048;
        var finiteCount = 0;
        var nonFiniteCount = 0;
        double sum = 0;
        var below = 0;
        var above = 0;
        foreach (var value in values)
        {
            if (!float.IsFinite(value))
            {
                nonFiniteCount++;
                continue;
            }

            finiteCount++;
            sum += value;
            if (descriptor.Scale != StateScale.Categorical)
            {
                if (value < descriptor.ScaleMinimum) below++;
                else if (value > descriptor.ScaleMaximum) above++;
            }
        }

        if (finiteCount == 0)
        {
            return new LayerStatistics(0, 0, 0, 0, 0, 0, 0, 0, nonFiniteCount, new int[binCount]);
        }

        var histogram = new int[binCount];
        Span<int> quantileCounts = stackalloc int[quantileBinCount];
        Span<float> quantileMinimums = stackalloc float[quantileBinCount];
        Span<float> quantileMaximums = stackalloc float[quantileBinCount];
        quantileMinimums.Fill(float.PositiveInfinity);
        quantileMaximums.Fill(float.NegativeInfinity);
        var quantileSpan = Math.Max(1e-12f, maximum - minimum);
        foreach (var value in values)
        {
            if (!float.IsFinite(value))
            {
                continue;
            }

            var normalized = NormalizeForScale(value, descriptor, minimum, maximum);
            var bin = Math.Min(binCount - 1, (int)(normalized * binCount));
            histogram[bin]++;
            var quantileBin = Math.Min(
                quantileBinCount - 1,
                (int)Math.Clamp((value - minimum) / quantileSpan * quantileBinCount, 0f, quantileBinCount - 1));
            quantileCounts[quantileBin]++;
            quantileMinimums[quantileBin] = Math.Min(quantileMinimums[quantileBin], value);
            quantileMaximums[quantileBin] = Math.Max(quantileMaximums[quantileBin], value);
        }

        return new LayerStatistics(
            (float)(sum / finiteCount),
            ApproximatePercentile(quantileCounts, quantileMinimums, quantileMaximums, finiteCount, 0.02f),
            ApproximatePercentile(quantileCounts, quantileMinimums, quantileMaximums, finiteCount, 0.10f),
            ApproximatePercentile(quantileCounts, quantileMinimums, quantileMaximums, finiteCount, 0.50f),
            ApproximatePercentile(quantileCounts, quantileMinimums, quantileMaximums, finiteCount, 0.90f),
            ApproximatePercentile(quantileCounts, quantileMinimums, quantileMaximums, finiteCount, 0.98f),
            below,
            above,
            nonFiniteCount,
            histogram);
    }

    private static float NormalizeForScale(
        float value,
        StateDescriptor descriptor,
        float actualMinimum,
        float actualMaximum)
    {
        if (descriptor.Scale == StateScale.Categorical)
        {
            return Math.Clamp((value - actualMinimum) / Math.Max(1e-7f, actualMaximum - actualMinimum), 0f, 1f);
        }

        if (descriptor.Scale == StateScale.Cyclic)
        {
            var span = descriptor.ScaleMaximum - descriptor.ScaleMinimum;
            var wrapped = (value - descriptor.ScaleMinimum) % span;
            if (wrapped < 0f)
            {
                wrapped += span;
            }

            return wrapped / span;
        }

        if (descriptor.Scale == StateScale.Diverging
            && descriptor.ScaleMinimum < 0f
            && descriptor.ScaleMaximum > 0f)
        {
            return value <= 0f
                ? 0.5f * Math.Clamp((value - descriptor.ScaleMinimum) / -descriptor.ScaleMinimum, 0f, 1f)
                : 0.5f + 0.5f * Math.Clamp(value / descriptor.ScaleMaximum, 0f, 1f);
        }

        var linear = Math.Clamp(
            (value - descriptor.ScaleMinimum) / Math.Max(1e-7f, descriptor.ScaleMaximum - descriptor.ScaleMinimum),
            0f,
            1f);
        return descriptor.Scale == StateScale.Logarithmic
            ? MathF.Log10(1f + 9f * linear)
            : linear;
    }

    private static float ApproximatePercentile(
        ReadOnlySpan<int> counts,
        ReadOnlySpan<float> minimums,
        ReadOnlySpan<float> maximums,
        int valueCount,
        float percentile)
    {
        var target = percentile * (valueCount - 1);
        var before = 0;
        for (var bin = 0; bin < counts.Length; bin++)
        {
            var count = counts[bin];
            if (count == 0) continue;
            if (target >= before + count)
            {
                before += count;
                continue;
            }

            if (count == 1 || maximums[bin] <= minimums[bin]) return minimums[bin];
            var within = Math.Clamp((target - before) / (count - 1), 0f, 1f);
            return minimums[bin] + (maximums[bin] - minimums[bin]) * within;
        }

        return maximums[^1];
    }

    private WorldHistoryPoint CaptureHistoryPoint()
    {
        var budget = waterBudget.Snapshot(state);
        return new WorldHistoryPoint(
            Clock.ElapsedHours,
            Clock.Year,
            Clock.DayOfYear,
            Clock.HourOfDay,
            Mean(state.SurfaceTemperatureC),
            Mean(state.PrecipitationMmPerHour),
            Mean(state.SurfaceWaterMm),
            Mean(state.RootWaterMm),
            Mean(state.GroundwaterMm),
            Mean(state.SnowWaterEquivalentMm),
            Mean(state.LiveBiomassGm2),
            Mean(state.DryBiomassGm2),
            Mean(state.DustGm2),
            (float)(budget.StoredMmCells / Config.CellCount));
    }

    private WorldRegimeMetrics CaptureRegimeMetrics()
    {
        double temperature = 0;
        double precipitation = 0;
        double snow = 0;
        double surfaceWater = 0;
        double rootWater = 0;
        double liveBiomass = 0;
        double dust = 0;
        double wind = 0;
        double fire = 0;
        var snowCovered = 0;
        var flooded = 0;
        var waterStressed = 0;
        var green = 0;
        var dustAffected = 0;
        var burning = 0;

        for (var index = 0; index < Config.CellCount; index++)
        {
            temperature += state.SurfaceTemperatureC[index];
            precipitation += state.PrecipitationMmPerHour[index];
            snow += state.SnowWaterEquivalentMm[index];
            surfaceWater += state.SurfaceWaterMm[index];
            rootWater += state.RootWaterMm[index];
            liveBiomass += state.LiveBiomassGm2[index];
            dust += state.DustGm2[index];
            fire += state.FireIntensityFraction[index];
            wind += MathF.Sqrt(
                state.WindXMs[index] * state.WindXMs[index]
                + state.WindYMs[index] * state.WindYMs[index]);
            if (state.SnowWaterEquivalentMm[index] >= 1f) snowCovered++;
            if (state.SurfaceWaterMm[index] >= 10f) flooded++;
            if (state.RootWaterMm[index] < 35f) waterStressed++;
            if (state.LiveBiomassGm2[index] >= 150f) green++;
            if (state.DustGm2[index] >= 0.02f) dustAffected++;
            if (state.FireIntensityFraction[index] >= 0.01f) burning++;
        }

        var scale = 1f / Config.CellCount;
        return new WorldRegimeMetrics(
            Clock.ElapsedHours,
            Clock.Year,
            Clock.DayOfYear,
            Clock.HourOfDay,
            (float)(temperature * scale),
            (float)(precipitation * scale),
            (float)(snow * scale),
            snowCovered * scale,
            (float)(surfaceWater * scale),
            flooded * scale,
            (float)(rootWater * scale),
            waterStressed * scale,
            (float)(liveBiomass * scale),
            green * scale,
            (float)(dust * scale),
            dustAffected * scale,
            (float)(wind * scale),
            (float)(fire * scale),
            burning * scale);
    }

    private float[] CaptureCellValues(int index)
    {
        var values = new float[Enum.GetValues<SimulationLayer>().Length];
        foreach (var layer in Enum.GetValues<SimulationLayer>())
        {
            values[(int)layer] = SampleValue(layer, index);
        }

        return values;
    }

    private int CheckedIndex(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Config.Width || y >= Config.Height)
        {
            throw new ArgumentOutOfRangeException($"Cell ({x}, {y}) is outside the world.");
        }

        return y * Config.Width + x;
    }

    private static float Mean(float[] values)
    {
        double sum = 0;
        foreach (var value in values)
        {
            sum += value;
        }

        return (float)(sum / values.Length);
    }
}
