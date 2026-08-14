namespace Steppe.Simulation;

internal static class WorldSystems
{
    private const float TwoPi = MathF.PI * 2f;

    public static void Step(
        WorldConfig config,
        WorldClock clock,
        WorldState state,
        WaterBudget waterBudget,
        WorldFluxState fluxState,
        double deltaHours)
    {
        var hours = (float)deltaHours;
        var climate = ClimateRegimeModel.GetForcing(config, clock);
        UpdateEnergy(config, clock, state, climate, hours);
        UpdateWind(config, clock, state, climate, hours);
        AdvectAtmosphere(config, clock, state, waterBudget, fluxState, climate, hours);
        UpdateCloudsAndPrecipitation(config, state, fluxState, hours);
        UpdateSnowAndSoilTemperature(config, state, waterBudget, fluxState, hours);
        UpdateHydrology(config, state, waterBudget, fluxState, hours);
        RecoverSoilCompaction(config, state, fluxState, hours);

        if (IsDue(clock.ElapsedHours, deltaHours, 3d))
        {
            UpdateBiology(config, state, fluxState, 3f);
        }

        if (IsDue(clock.ElapsedHours, deltaHours, 6d))
        {
            UpdateSedimentAndDust(config, state, fluxState, 6f);
        }
    }

    private static bool IsDue(double elapsedHours, double deltaHours, double intervalHours)
    {
        var before = Math.Floor((elapsedHours + 1e-8) / intervalHours);
        var after = Math.Floor((elapsedHours + deltaHours + 1e-8) / intervalHours);
        return after > before;
    }

    private static void UpdateEnergy(
        WorldConfig config,
        WorldClock clock,
        WorldState state,
        ClimateForcingSnapshot climate,
        float hours)
    {
        var solar = Astronomy.Calculate(config, clock);
        var sunElevation = (float)solar.ElevationRadians;
        var sunAzimuth = (float)solar.AzimuthRadians;
        var directRadiation = (float)solar.TopOfAtmosphereWm2;
        var sunHorizontal = MathF.Max(0f, MathF.Cos(sunElevation));
        var sunX = sunHorizontal * MathF.Cos(sunAzimuth);
        var sunY = sunHorizontal * MathF.Sin(sunAzimuth);
        var sunZ = MathF.Max(0f, MathF.Sin(sunElevation));
        var surfaceRelaxation = 1f - MathF.Exp(-0.22f * hours);
        var airRelaxation = 1f - MathF.Exp(-0.14f * hours);
        var soilRelaxation = 1f - MathF.Exp(-0.018f * hours);

        for (var index = 0; index < config.CellCount; index++)
        {
            var slope = MathF.Atan(state.Slope[index]);
            var aspect = state.AspectRadians[index];
            var normalX = MathF.Sin(slope) * MathF.Cos(aspect);
            var normalY = MathF.Sin(slope) * MathF.Sin(aspect);
            var normalZ = MathF.Cos(slope);
            var incidence = MathF.Max(0f, normalX * sunX + normalY * sunY + normalZ * sunZ);
            var cloudShadow = Math.Clamp(state.CloudWaterMm[index] / 8f, 0f, 0.82f);
            var snowAlbedo = Math.Clamp(state.SnowWaterEquivalentMm[index] / 18f, 0f, 1f);
            var albedo = 0.18f + snowAlbedo * 0.55f + Math.Clamp(state.DryBiomassGm2[index] / 2500f, 0f, 0.08f);
            var radiation = directRadiation * incidence * (1f - cloudShadow) * (1f - albedo);
            state.SolarRadiationWm2[index] = radiation;

            var seasonalBackground = SeasonalAirBaseline(clock, climate);
            var elevationCooling = Math.Max(0f, state.ElevationM[index] - 250f) * 0.0062f;
            var wetThermalBuffer = Math.Clamp(
                (state.SurfaceWaterMm[index] + state.RootWaterMm[index] * 0.08f) / 25f,
                0f,
                1f);
            var surfaceTarget = seasonalBackground - elevationCooling
                + radiation * 0.047f
                - (1f - wetThermalBuffer) * (solar.TopOfAtmosphereWm2 <= 0 ? 4f : 0f);
            state.SurfaceTemperatureC[index] += (surfaceTarget - state.SurfaceTemperatureC[index])
                * surfaceRelaxation
                * (1f - wetThermalBuffer * 0.28f);

            var airTarget = seasonalBackground - elevationCooling
                + (state.SurfaceTemperatureC[index] - seasonalBackground) * 0.42f;
            state.AirTemperatureC[index] += (airTarget - state.AirTemperatureC[index]) * airRelaxation;
            state.SoilTemperatureC[index] += (state.SurfaceTemperatureC[index] - state.SoilTemperatureC[index])
                * soilRelaxation;
        }
    }

    private static void UpdateWind(
        WorldConfig config,
        WorldClock clock,
        WorldState state,
        ClimateForcingSnapshot climate,
        float hours)
    {
        var shiftedDay = clock.DayOfYear - climate.SeasonPhaseShiftDays;
        var seasonalWestWind = (5.2f + 1.6f * MathF.Cos(TwoPi * (shiftedDay - 20f) / 365f))
            * climate.WindSpeedMultiplier;
        var synopticPhase = (float)(clock.ElapsedHours / (24d * 5.5d));
        var response = 1f - MathF.Exp(-0.28f * hours);

        for (var y = 0; y < config.Height; y++)
        {
            for (var x = 0; x < config.Width; x++)
            {
                var index = Index(x, y, config.Width);
                var wave = MathF.Sin(TwoPi * (x / (float)config.Width - synopticPhase));
                var meridionalWave = MathF.Cos(TwoPi * (y / (float)config.Height + synopticPhase * 0.43f));
                var thermalAnomaly = state.AirTemperatureC[index] - state.SoilTemperatureC[index];
                state.AirPressureHpa[index] = 1013.25f * MathF.Exp(-state.ElevationM[index] / 8500f)
                    + wave * 5.5f
                    + meridionalWave * 2.2f
                    - thermalAnomaly * 0.38f;
            }
        }

        for (var y = 0; y < config.Height; y++)
        {
            for (var x = 0; x < config.Width; x++)
            {
                var index = Index(x, y, config.Width);
                var left = state.AirPressureHpa[Index(Math.Max(0, x - 1), y, config.Width)];
                var right = state.AirPressureHpa[Index(Math.Min(config.Width - 1, x + 1), y, config.Width)];
                var down = state.AirPressureHpa[Index(x, Math.Max(0, y - 1), config.Width)];
                var up = state.AirPressureHpa[Index(x, Math.Min(config.Height - 1, y + 1), config.Width)];
                var gradientX = right - left;
                var gradientY = up - down;
                var terrainDrag = 1f / (1f + state.Slope[index] * 18f + state.LiveBiomassGm2[index] / 1800f);
                var targetX = (seasonalWestWind + climate.WindAnomalyXMs - gradientX * 0.85f) * terrainDrag;
                var targetY = (-gradientY * 0.85f
                        + MathF.Sin((float)clock.ElapsedHours * 0.018f) * 0.8f
                        + climate.WindAnomalyYMs)
                    * terrainDrag;
                state.WindXMs[index] += (targetX - state.WindXMs[index]) * response;
                state.WindYMs[index] += (targetY - state.WindYMs[index]) * response;
            }
        }
    }

    private static void AdvectAtmosphere(
        WorldConfig config,
        WorldClock clock,
        WorldState state,
        WaterBudget waterBudget,
        WorldFluxState fluxState,
        ClimateForcingSnapshot climate,
        float hours)
    {
        AdvectWaterField(config, clock, state, state.AirHumidityMm, state.ScratchA, hours, false, waterBudget, fluxState, SimulationFlux.HumidityTransport, climate);
        Array.Copy(state.ScratchA, state.AirHumidityMm, config.CellCount);
        AdvectWaterField(config, clock, state, state.CloudWaterMm, state.ScratchA, hours, true, waterBudget, fluxState, SimulationFlux.CloudTransport, climate);
        Array.Copy(state.ScratchA, state.CloudWaterMm, config.CellCount);

        var boundaryTemperature = SeasonalAirBaseline(clock, climate);
        AdvectScalar(
            config,
            state,
            state.AirTemperatureC,
            state.ScratchA,
            hours,
            boundaryTemperature,
            fluxState,
            VectorProcess.AirTemperatureAdvection);
        for (var index = 0; index < config.CellCount; index++)
        {
            fluxState.Add(
                SimulationFlux.AirTemperatureTransport,
                index,
                state.ScratchA[index] - state.AirTemperatureC[index]);
        }

        Array.Copy(state.ScratchA, state.AirTemperatureC, config.CellCount);
    }

    private static void AdvectWaterField(
        WorldConfig config,
        WorldClock clock,
        WorldState state,
        float[] source,
        float[] destination,
        float hours,
        bool cloud,
        WaterBudget waterBudget,
        WorldFluxState fluxState,
        SimulationFlux transportFlux,
        ClimateForcingSnapshot climate)
    {
        double before = 0;
        double after = 0;
        var stormPulse = climate.SynopticStormPulse * climate.StormIntensityMultiplier;
        var shiftedDay = clock.DayOfYear - climate.SeasonPhaseShiftDays;
        var seasonWetness = 0.5f + 0.5f * MathF.Cos(TwoPi * (shiftedDay - 115f) / 365f);

        for (var y = 0; y < config.Height; y++)
        {
            for (var x = 0; x < config.Width; x++)
            {
                var index = Index(x, y, config.Width);
                before += source[index];
                var displacementX = state.WindXMs[index] * hours * 0.055f;
                var displacementY = state.WindYMs[index] * hours * 0.055f;
                var sourceX = x - displacementX;
                var sourceY = y - displacementY;
                float value;
                if (sourceX < 0 || sourceY < 0 || sourceX > config.Width - 1 || sourceY > config.Height - 1)
                {
                    var latitudeBand = 0.72f + 0.28f * MathF.Cos((y / (float)config.Height - 0.45f) * MathF.PI);
                    value = cloud
                        ? stormPulse * (0.8f + 1.4f * seasonWetness)
                            * climate.MoistureMultiplier * latitudeBand
                        : (3.8f + (1.2f + 4.5f * seasonWetness + stormPulse * 3.5f)
                            * climate.MoistureMultiplier) * latitudeBand;
                }
                else
                {
                    value = Bilinear(source, config.Width, config.Height, sourceX, sourceY);
                }

                destination[index] = Math.Max(0f, value);
                fluxState.Add(transportFlux, index, destination[index] - source[index]);
                var vectorProcess = transportFlux == SimulationFlux.CloudTransport
                    ? VectorProcess.CloudAdvection
                    : VectorProcess.HumidityAdvection;
                fluxState.AddVector(
                    vectorProcess,
                    index,
                    destination[index] * displacementX,
                    destination[index] * displacementY);
                after += destination[index];
            }
        }

        waterBudget.AddExternal(after - before);
    }

    private static void AdvectScalar(
        WorldConfig config,
        WorldState state,
        float[] source,
        float[] destination,
        float hours,
        float boundaryValue,
        WorldFluxState fluxState,
        VectorProcess vectorProcess)
    {
        for (var y = 0; y < config.Height; y++)
        {
            for (var x = 0; x < config.Width; x++)
            {
                var index = Index(x, y, config.Width);
                var sourceX = x - state.WindXMs[index] * hours * 0.055f;
                var sourceY = y - state.WindYMs[index] * hours * 0.055f;
                destination[index] = sourceX < 0 || sourceY < 0
                    || sourceX > config.Width - 1 || sourceY > config.Height - 1
                    ? boundaryValue
                    : Bilinear(source, config.Width, config.Height, sourceX, sourceY);
                fluxState.AddVector(
                    vectorProcess,
                    index,
                    destination[index] * state.WindXMs[index] * hours * 0.055f,
                    destination[index] * state.WindYMs[index] * hours * 0.055f);
            }
        }
    }

    private static void UpdateCloudsAndPrecipitation(
        WorldConfig config,
        WorldState state,
        WorldFluxState fluxState,
        float hours)
    {
        for (var index = 0; index < config.CellCount; index++)
        {
            state.PrecipitationMmPerHour[index] = 0f;
            var saturation = Math.Clamp(5.2f * MathF.Exp(0.055f * state.AirTemperatureC[index]), 1.4f, 32f);
            if (state.AirHumidityMm[index] > saturation)
            {
                var condensed = Math.Min(
                    state.AirHumidityMm[index] - saturation,
                    (state.AirHumidityMm[index] - saturation) * (1f - MathF.Exp(-0.8f * hours)));
                state.AirHumidityMm[index] -= condensed;
                state.CloudWaterMm[index] += condensed;
                fluxState.Add(SimulationFlux.Condensation, index, condensed);
            }
            else if (state.CloudWaterMm[index] > 0 && state.AirHumidityMm[index] < saturation * 0.82f)
            {
                var evaporated = Math.Min(
                    state.CloudWaterMm[index],
                    (saturation * 0.82f - state.AirHumidityMm[index]) * 0.2f * hours);
                state.CloudWaterMm[index] -= evaporated;
                state.AirHumidityMm[index] += evaporated;
                fluxState.Add(SimulationFlux.CloudEvaporation, index, evaporated);
            }

            var orographicLift = 1f + Math.Clamp(state.Slope[index] * MathF.Max(0f, state.WindXMs[index]) * 4f, 0f, 1.4f);
            var rainThreshold = 1.1f;
            var precipitation = Math.Min(
                state.CloudWaterMm[index],
                Math.Max(0f, state.CloudWaterMm[index] - rainThreshold) * 0.16f * orographicLift * hours);
            state.CloudWaterMm[index] -= precipitation;
            state.PrecipitationMmPerHour[index] = precipitation / Math.Max(0.001f, hours);

            var snowFraction = Math.Clamp((2f - state.AirTemperatureC[index]) / 4f, 0f, 1f);
            var snowfall = precipitation * snowFraction;
            var rainfall = precipitation - snowfall;
            state.SnowWaterEquivalentMm[index] += snowfall;
            state.SurfaceWaterMm[index] += rainfall;
            fluxState.Add(SimulationFlux.Snowfall, index, snowfall);
            fluxState.Add(SimulationFlux.Rainfall, index, rainfall);
        }
    }

    private static void UpdateSnowAndSoilTemperature(
        WorldConfig config,
        WorldState state,
        WaterBudget waterBudget,
        WorldFluxState fluxState,
        float hours)
    {
        for (var index = 0; index < config.CellCount; index++)
        {
            var meltEnergy = Math.Max(0f, state.SurfaceTemperatureC[index]) * 0.18f
                + state.SolarRadiationWm2[index] * 0.00045f;
            var melt = Math.Min(state.SnowWaterEquivalentMm[index], meltEnergy * hours);
            state.SnowWaterEquivalentMm[index] -= melt;
            state.SurfaceWaterMm[index] += melt;
            fluxState.Add(SimulationFlux.SnowMelt, index, melt);

            var wind = MathF.Sqrt(
                state.WindXMs[index] * state.WindXMs[index]
                + state.WindYMs[index] * state.WindYMs[index]);
            var saturation = Math.Clamp(5.2f * MathF.Exp(0.055f * state.AirTemperatureC[index]), 1.4f, 32f);
            var vaporDeficit = Math.Clamp(1f - state.AirHumidityMm[index] / saturation, 0f, 1f);
            var sublimation = Math.Min(
                state.SnowWaterEquivalentMm[index],
                0.012f * wind * (0.3f + state.SolarRadiationWm2[index] / 700f) * vaporDeficit * hours);
            state.SnowWaterEquivalentMm[index] -= sublimation;
            state.AirHumidityMm[index] += sublimation;
            fluxState.Add(SimulationFlux.SnowSublimation, index, sublimation);

            var freezeTarget = Math.Clamp((-state.SoilTemperatureC[index] + 1.2f) / 8f, 0f, 1f);
            var response = 1f - MathF.Exp(-(freezeTarget > state.FrozenSoilFraction[index] ? 0.07f : 0.16f) * hours);
            state.FrozenSoilFraction[index] += (freezeTarget - state.FrozenSoilFraction[index]) * response;
        }

        RedistributeSnow(config, state, waterBudget, fluxState, hours);
    }

    private static void RedistributeSnow(
        WorldConfig config,
        WorldState state,
        WaterBudget waterBudget,
        WorldFluxState fluxState,
        float hours)
    {
        Array.Clear(state.ScratchA);
        for (var y = 0; y < config.Height; y++)
        {
            for (var x = 0; x < config.Width; x++)
            {
                var index = Index(x, y, config.Width);
                if (state.SnowWaterEquivalentMm[index] <= 2f)
                {
                    continue;
                }

                var wind = MathF.Sqrt(
                    state.WindXMs[index] * state.WindXMs[index]
                    + state.WindYMs[index] * state.WindYMs[index]);
                // Deep drifts protrude into faster air and become mobile even when
                // the near-surface wind in the cell is locally sheltered.
                var exposedDriftWind = wind + Math.Max(0f, state.SnowWaterEquivalentMm[index] - 420f) / 110f;
                if (exposedDriftWind <= 3f)
                {
                    continue;
                }

                var stepX = Math.Sign(state.WindXMs[index]);
                var stepY = Math.Abs(state.WindYMs[index]) > Math.Abs(state.WindXMs[index]) * 0.65f
                    ? Math.Sign(state.WindYMs[index])
                    : 0;
                var targetX = x + stepX;
                var targetY = y + stepY;
                var fraction = Math.Clamp((exposedDriftWind - 3f) * 0.0025f * hours, 0f, 0.08f);
                var drift = Math.Min(
                    state.SnowWaterEquivalentMm[index] - 2f,
                    state.SnowWaterEquivalentMm[index] * fraction);
                state.ScratchA[index] -= drift;
                fluxState.AddVector(VectorProcess.SnowTransport, index, drift * stepX, drift * stepY);
                if (targetX < 0 || targetY < 0 || targetX >= config.Width || targetY >= config.Height)
                {
                    waterBudget.AddExternal(-drift);
                    fluxState.Add(SimulationFlux.SnowExport, index, drift);
                    continue;
                }

                var target = Index(targetX, targetY, config.Width);
                var depositionEfficiency = 1f / (1f + state.SnowWaterEquivalentMm[target] / 180f);
                var deposited = drift * depositionEfficiency;
                state.ScratchA[target] += deposited;
                fluxState.Add(SimulationFlux.SnowTransport, index, -deposited);
                fluxState.Add(SimulationFlux.SnowTransport, target, deposited);
                var suspendedLoss = drift - deposited;
                if (suspendedLoss > 0f)
                {
                    state.AirHumidityMm[index] += suspendedLoss;
                    fluxState.Add(SimulationFlux.SnowSublimation, index, suspendedLoss);
                }
            }
        }

        for (var index = 0; index < config.CellCount; index++)
        {
            state.SnowWaterEquivalentMm[index] = Math.Max(
                0f,
                state.SnowWaterEquivalentMm[index] + state.ScratchA[index]);
        }
    }

    private static void UpdateHydrology(
        WorldConfig config,
        WorldState state,
        WaterBudget waterBudget,
        WorldFluxState fluxState,
        float hours)
    {
        InfiltrateAndPercolate(config, state, fluxState, hours);
        Array.Clear(state.RunoffOutMm);
        Array.Clear(state.RunoffVectorX);
        Array.Clear(state.RunoffVectorY);
        const int runoffSubsteps = 4;
        for (var substep = 0; substep < runoffSubsteps; substep++)
        {
            RouteSurfaceWater(config, state, waterBudget, hours / runoffSubsteps, fluxState);
        }
        ExchangeGroundwater(config, state, fluxState, hours);
        DischargeGroundwater(config, state, fluxState, hours);
        EvaporateAndTranspire(config, state, fluxState, hours);
    }

    private static void InfiltrateAndPercolate(
        WorldConfig config,
        WorldState state,
        WorldFluxState fluxState,
        float hours)
    {
        for (var index = 0; index < config.CellCount; index++)
        {
            var rootCapacity = RootCapacityMm(state, index);
            var poreSpace = Math.Max(0f, rootCapacity - state.RootWaterMm[index]);
            var infiltrationCapacity = state.PermeabilityMmPerHour[index]
                * (1f - state.FrozenSoilFraction[index])
                * (1f - state.SurfaceCrustFraction[index] * 0.72f)
                * (1f - state.SoilCompactionFraction[index] * 0.68f)
                * hours;
            var infiltration = Math.Min(state.SurfaceWaterMm[index], Math.Min(poreSpace, infiltrationCapacity));
            state.SurfaceWaterMm[index] -= infiltration;
            state.RootWaterMm[index] += infiltration;
            fluxState.Add(SimulationFlux.Infiltration, index, infiltration);

            var fieldCapacity = rootCapacity * (0.46f + state.ClayFraction[index] * 0.18f);
            var excess = Math.Max(0f, state.RootWaterMm[index] - fieldCapacity);
            var drainageResponse = 1f - MathF.Exp(-state.PermeabilityMmPerHour[index] * 0.0012f * hours);
            var groundwaterSpace = Math.Max(0f, GroundwaterCapacityMm(state, index) - state.GroundwaterMm[index]);
            var percolation = Math.Min(excess * drainageResponse, groundwaterSpace);
            state.RootWaterMm[index] -= percolation;
            state.GroundwaterMm[index] += percolation;
            fluxState.Add(SimulationFlux.Percolation, index, percolation);

        }
    }

    internal static void RouteSurfaceWater(
        WorldConfig config,
        WorldState state,
        WaterBudget waterBudget,
        float hours,
        WorldFluxState? fluxState = null)
    {
        Array.Clear(state.ScratchA);
        for (var index = 0; index < config.CellCount; index++)
        {
            const float surfaceRetentionMm = 0.6f;
            var available = Math.Max(0f, state.SurfaceWaterMm[index] - surfaceRetentionMm);
            if (available <= 0f
                || !TryFindSurfaceFlowTarget(config, state, index, out var target, out var dx, out var dy, out var headDropMm))
            {
                continue;
            }

            var mobility = Math.Clamp(
                (0.08f + state.Slope[index] * 210f) * (1f + state.SoilCompactionFraction[index] * 0.35f),
                0.06f,
                0.82f);
            var flow = Math.Min(
                available,
                Math.Min(headDropMm * 0.48f, available * mobility * hours));
            flow = Math.Max(0f, flow);
            state.ScratchA[index] -= flow;
            state.RunoffOutMm[index] += flow;
            fluxState?.Add(SimulationFlux.RunoffOut, index, flow);
            var length = MathF.Max(1f, MathF.Sqrt(dx * dx + dy * dy));
            state.RunoffVectorX[index] += flow * dx / length;
            state.RunoffVectorY[index] += flow * dy / length;
            fluxState?.AddVector(
                VectorProcess.SurfaceRunoff,
                index,
                flow * dx / length,
                flow * dy / length);
            if (target >= 0)
            {
                state.ScratchA[target] += flow;
                fluxState?.Add(SimulationFlux.RunoffIn, target, flow);
            }
            else
            {
                waterBudget.AddExternal(-flow);
            }
        }

        for (var index = 0; index < config.CellCount; index++)
        {
            state.SurfaceWaterMm[index] = Math.Max(0f, state.SurfaceWaterMm[index] + state.ScratchA[index]);
        }
    }

    private static bool TryFindSurfaceFlowTarget(
        WorldConfig config,
        WorldState state,
        int index,
        out int target,
        out float directionX,
        out float directionY,
        out float headDropMm)
    {
        var x = index % config.Width;
        var y = index / config.Width;
        var localHeadM = state.ElevationM[index] + state.SurfaceWaterMm[index] * 0.001f;
        var bestHeadM = localHeadM;
        target = index;
        directionX = 0f;
        directionY = 0f;

        for (var offsetY = -1; offsetY <= 1; offsetY++)
        {
            for (var offsetX = -1; offsetX <= 1; offsetX++)
            {
                if (offsetX == 0 && offsetY == 0) continue;
                var neighbourX = x + offsetX;
                var neighbourY = y + offsetY;
                if (neighbourX < 0 || neighbourY < 0 || neighbourX >= config.Width || neighbourY >= config.Height)
                {
                    continue;
                }

                var neighbour = Index(neighbourX, neighbourY, config.Width);
                var neighbourHeadM = state.ElevationM[neighbour] + state.SurfaceWaterMm[neighbour] * 0.001f;
                if (neighbourHeadM < bestHeadM - 1e-6f)
                {
                    bestHeadM = neighbourHeadM;
                    target = neighbour;
                    directionX = offsetX;
                    directionY = offsetY;
                }
            }
        }

        var boundary = x == 0 || y == 0 || x == config.Width - 1 || y == config.Height - 1;
        if (boundary)
        {
            var exteriorHeadM = state.ElevationM[index] + 0.0006f;
            if (exteriorHeadM < bestHeadM - 1e-6f)
            {
                bestHeadM = exteriorHeadM;
                target = -1;
                directionX = x == 0 ? -1f : x == config.Width - 1 ? 1f : 0f;
                directionY = y == 0 ? -1f : y == config.Height - 1 ? 1f : 0f;
            }
        }

        headDropMm = Math.Max(0f, (localHeadM - bestHeadM) * 1000f);
        return target != index && headDropMm > 0.001f;
    }

    private static void ExchangeGroundwater(
        WorldConfig config,
        WorldState state,
        WorldFluxState fluxState,
        float hours)
    {
        Array.Clear(state.ScratchA);
        for (var y = 0; y < config.Height; y++)
        {
            for (var x = 0; x < config.Width; x++)
            {
                var index = Index(x, y, config.Width);
                if (x + 1 < config.Width)
                {
                    var neighbour = Index(x + 1, y, config.Width);
                    var flow = ExchangePair(state, index, neighbour, hours);
                    if (flow > 0)
                    {
                        fluxState.AddVector(VectorProcess.GroundwaterFlow, index, flow, 0);
                    }
                    else if (flow < 0)
                    {
                        fluxState.AddVector(VectorProcess.GroundwaterFlow, neighbour, flow, 0);
                    }
                }

                if (y + 1 < config.Height)
                {
                    var neighbour = Index(x, y + 1, config.Width);
                    var flow = ExchangePair(state, index, neighbour, hours);
                    if (flow > 0)
                    {
                        fluxState.AddVector(VectorProcess.GroundwaterFlow, index, 0, flow);
                    }
                    else if (flow < 0)
                    {
                        fluxState.AddVector(VectorProcess.GroundwaterFlow, neighbour, 0, flow);
                    }
                }
            }
        }

        for (var index = 0; index < config.CellCount; index++)
        {
            state.GroundwaterMm[index] = Math.Max(0f, state.GroundwaterMm[index] + state.ScratchA[index]);
            fluxState.Add(SimulationFlux.GroundwaterTransport, index, state.ScratchA[index]);
        }
    }

    private static void DischargeGroundwater(
        WorldConfig config,
        WorldState state,
        WorldFluxState fluxState,
        float hours)
    {
        for (var index = 0; index < config.CellCount; index++)
        {
            var groundwaterCapacity = GroundwaterCapacityMm(state, index);
            if (state.GroundwaterMm[index] <= groundwaterCapacity)
            {
                continue;
            }

            var spring = Math.Min(
                state.GroundwaterMm[index] - groundwaterCapacity,
                (state.GroundwaterMm[index] - groundwaterCapacity)
                    * (1f - MathF.Exp(-0.08f * hours)));
            state.GroundwaterMm[index] -= spring;
            state.SurfaceWaterMm[index] += spring;
            fluxState.Add(SimulationFlux.GroundwaterDischarge, index, spring);
        }
    }

    private static float ExchangePair(WorldState state, int a, int b, float hours)
    {
        var hydraulicHeadA = state.ElevationM[a] - state.SoilDepthM[a] * 0.5f + state.GroundwaterMm[a] * 0.004f;
        var hydraulicHeadB = state.ElevationM[b] - state.SoilDepthM[b] * 0.5f + state.GroundwaterMm[b] * 0.004f;
        var flow = Math.Clamp((hydraulicHeadA - hydraulicHeadB) * 0.0025f * hours, -0.12f, 0.12f);
        if (flow > 0)
        {
            flow = Math.Min(flow, state.GroundwaterMm[a] * 0.01f);
        }
        else
        {
            flow = -Math.Min(-flow, state.GroundwaterMm[b] * 0.01f);
        }

        state.ScratchA[a] -= flow;
        state.ScratchA[b] += flow;
        return flow;
    }

    private static void EvaporateAndTranspire(
        WorldConfig config,
        WorldState state,
        WorldFluxState fluxState,
        float hours)
    {
        for (var index = 0; index < config.CellCount; index++)
        {
            var energy = Math.Clamp(state.SolarRadiationWm2[index] / 720f, 0f, 1.4f);
            var vaporDeficit = Math.Clamp(
                1f - state.AirHumidityMm[index]
                    / Math.Clamp(5.2f * MathF.Exp(0.055f * state.AirTemperatureC[index]), 1.4f, 32f),
                0f,
                1f);
            var wind = MathF.Sqrt(
                state.WindXMs[index] * state.WindXMs[index]
                + state.WindYMs[index] * state.WindYMs[index]);
            var evaporativeDemand = (0.025f + energy * 0.16f)
                * (0.35f + vaporDeficit * 0.65f)
                * (0.7f + Math.Min(wind, 12f) * 0.035f)
                * hours;
            var surfaceEvaporation = Math.Min(state.SurfaceWaterMm[index], evaporativeDemand);
            state.SurfaceWaterMm[index] -= surfaceEvaporation;
            fluxState.Add(SimulationFlux.SurfaceEvaporation, index, surfaceEvaporation);

            var rootCapacity = RootCapacityMm(state, index);
            var plantWaterAccess = Math.Clamp(state.RootWaterMm[index] / Math.Max(1f, rootCapacity * 0.6f), 0f, 1f)
                * (1f - state.FrozenSoilFraction[index]);
            var transpiration = Math.Min(
                state.RootWaterMm[index],
                state.LiveBiomassGm2[index] / 650f * energy * plantWaterAccess * 0.11f * hours);
            state.RootWaterMm[index] -= transpiration;
            state.AirHumidityMm[index] += surfaceEvaporation + transpiration;
            fluxState.Add(SimulationFlux.Transpiration, index, transpiration);
        }
    }

    private static void UpdateBiology(
        WorldConfig config,
        WorldState state,
        WorldFluxState fluxState,
        float hours)
    {
        for (var index = 0; index < config.CellCount; index++)
        {
            var rootCapacity = RootCapacityMm(state, index);
            var waterResponse = SmoothStep(0.06f, 0.38f, state.RootWaterMm[index] / Math.Max(1f, rootCapacity));
            var temperatureResponse = SmoothStep(1f, 14f, state.SoilTemperatureC[index])
                * (1f - SmoothStep(31f, 43f, state.SurfaceTemperatureC[index]));
            var lightResponse = Math.Clamp(state.SolarRadiationWm2[index] / 380f, 0f, 1f);
            var nutrientResponse = state.AvailableNitrogenGm2[index]
                / (state.AvailableNitrogenGm2[index] + 2.5f);
            var spaceResponse = Math.Clamp(1f - state.LiveBiomassGm2[index] / 720f, 0f, 1f);
            var potentialGrowth = 0.34f * hours * waterResponse * temperatureResponse
                * lightResponse * nutrientResponse * spaceResponse
                * (0.35f + state.SeedBank[index] * 0.65f)
                * (1f - state.SoilCompactionFraction[index] * 0.42f);
            var nitrogenNeeded = potentialGrowth * 0.018f;
            var nitrogenScale = nitrogenNeeded <= 0
                ? 0f
                : Math.Min(1f, state.AvailableNitrogenGm2[index] / nitrogenNeeded);
            var growth = potentialGrowth * nitrogenScale;
            var nitrogenUptake = growth * 0.018f;
            state.AvailableNitrogenGm2[index] -= nitrogenUptake;
            state.PlantNitrogenGm2[index] += nitrogenUptake;
            state.LiveBiomassGm2[index] += growth;
            fluxState.Add(SimulationFlux.PlantGrowth, index, growth);
            fluxState.Add(SimulationFlux.NitrogenUptake, index, nitrogenUptake);

            var coldStress = SmoothStep(1f, -8f, state.SoilTemperatureC[index]);
            var droughtStress = 1f - waterResponse;
            var heatStress = SmoothStep(32f, 43f, state.SurfaceTemperatureC[index]);
            var liveBeforeMortality = state.LiveBiomassGm2[index];
            var mortality = liveBeforeMortality
                * (coldStress * 0.00035f + droughtStress * 0.00008f + heatStress * 0.0005f)
                * hours;
            mortality = Math.Min(mortality, liveBeforeMortality);
            var senescedNitrogen = liveBeforeMortality > 1e-8f
                ? state.PlantNitrogenGm2[index] * mortality / liveBeforeMortality
                : 0f;
            state.LiveBiomassGm2[index] -= mortality;
            state.DryBiomassGm2[index] += mortality;
            state.PlantNitrogenGm2[index] = Math.Max(0f, state.PlantNitrogenGm2[index] - senescedNitrogen);
            state.OrganicNitrogenGm2[index] += senescedNitrogen;
            fluxState.Add(SimulationFlux.PlantMortality, index, mortality);
            fluxState.Add(SimulationFlux.PlantNitrogenSenescence, index, senescedNitrogen);

            var lodging = state.DryBiomassGm2[index] * 0.00025f * hours
                * (1f + state.PrecipitationMmPerHour[index] * 0.22f);
            lodging = Math.Min(lodging, state.DryBiomassGm2[index]);
            state.DryBiomassGm2[index] -= lodging;
            state.LitterBiomassGm2[index] += lodging;
            fluxState.Add(SimulationFlux.DryBiomassLodging, index, lodging);

            var decompositionClimate = SmoothStep(0f, 24f, state.SoilTemperatureC[index])
                * SmoothStep(0.08f, 0.55f, state.RootWaterMm[index] / Math.Max(1f, rootCapacity));
            var decomposition = Math.Min(
                state.LitterBiomassGm2[index],
                state.LitterBiomassGm2[index] * 0.00042f * decompositionClimate * hours);
            state.LitterBiomassGm2[index] -= decomposition;
            state.SoilOrganicMatterGm2[index] += decomposition * 0.35f;
            var respiration = Math.Min(
                state.SoilOrganicMatterGm2[index],
                state.SoilOrganicMatterGm2[index] * 0.000005f * decompositionClimate * hours);
            state.SoilOrganicMatterGm2[index] -= respiration;
            var nitrogenMineralization = Math.Min(
                state.OrganicNitrogenGm2[index],
                state.OrganicNitrogenGm2[index] * 0.00002f * decompositionClimate * hours);
            state.OrganicNitrogenGm2[index] -= nitrogenMineralization;
            state.AvailableNitrogenGm2[index] += nitrogenMineralization;
            fluxState.Add(SimulationFlux.LitterDecomposition, index, decomposition);
            fluxState.Add(SimulationFlux.SoilOrganicMatterRespiration, index, respiration);
            fluxState.Add(SimulationFlux.NitrogenMineralization, index, nitrogenMineralization);
            var previousSeedBank = state.SeedBank[index];
            state.SeedBank[index] = Math.Clamp(
                state.SeedBank[index] + growth * 0.00008f - droughtStress * 0.00012f * hours,
                0.05f,
                1.2f);
            fluxState.Add(SimulationFlux.SeedBankChange, index, state.SeedBank[index] - previousSeedBank);
        }
    }

    private static void UpdateSedimentAndDust(
        WorldConfig config,
        WorldState state,
        WorldFluxState fluxState,
        float hours)
    {
        Array.Clear(state.ScratchA);
        for (var index = 0; index < config.CellCount; index++)
        {
            var runoff = state.RunoffOutMm[index];
            var vectorLength = MathF.Sqrt(
                state.RunoffVectorX[index] * state.RunoffVectorX[index]
                + state.RunoffVectorY[index] * state.RunoffVectorY[index]);
            if (runoff > 0f && vectorLength > 1e-8f)
            {
                var x = index % config.Width;
                var y = index / config.Width;
                var stepX = Math.Sign(state.RunoffVectorX[index]);
                var stepY = Math.Sign(state.RunoffVectorY[index]);
                var targetX = x + stepX;
                var targetY = y + stepY;
                var inside = targetX >= 0 && targetY >= 0
                    && targetX < config.Width && targetY < config.Height;
                var target = inside ? Index(targetX, targetY, config.Width) : -1;
                var flowErosion = Math.Clamp(
                    runoff * state.Slope[index] * 0.012f * (1f - state.SurfaceCrustFraction[index] * 0.55f),
                    0f,
                    state.LooseSedimentKgM2[index] * 0.025f);
                state.ScratchA[index] -= flowErosion;
                fluxState.AddVector(
                    VectorProcess.SedimentTransport,
                    index,
                    flowErosion * state.RunoffVectorX[index] / vectorLength,
                    flowErosion * state.RunoffVectorY[index] / vectorLength);
                var sourceSoilBefore = state.SoilDepthM[index];
                state.SoilDepthM[index] = Math.Max(0.08f, state.SoilDepthM[index] - flowErosion * 0.00004f);
                fluxState.Add(SimulationFlux.SoilDepthChange, index, state.SoilDepthM[index] - sourceSoilBefore);
                if (target >= 0)
                {
                    state.ScratchA[target] += flowErosion;
                    fluxState.Add(SimulationFlux.WaterErosion, index, flowErosion);
                    fluxState.Add(SimulationFlux.SedimentDeposition, target, flowErosion);
                    var targetSoilBefore = state.SoilDepthM[target];
                    state.SoilDepthM[target] = Math.Min(3.2f, state.SoilDepthM[target] + flowErosion * 0.00004f);
                    fluxState.Add(SimulationFlux.SoilDepthChange, target, state.SoilDepthM[target] - targetSoilBefore);
                }
                else
                {
                    fluxState.Add(SimulationFlux.SedimentExport, index, flowErosion);
                }
            }

            var wind = MathF.Sqrt(
                state.WindXMs[index] * state.WindXMs[index]
                + state.WindYMs[index] * state.WindYMs[index]);
            var dryness = 1f - Math.Clamp(
                (state.SurfaceWaterMm[index] + state.RootWaterMm[index] * 0.04f) / 8f,
                0f,
                1f);
            var exposure = 1f - Math.Clamp(
                (state.LiveBiomassGm2[index] + state.DryBiomassGm2[index] * 0.35f) / 520f,
                0f,
                0.95f);
            var liftedGm2 = Math.Max(0f, wind - 4.5f) * dryness * exposure * 0.01f * hours;
            var availableGm2 = Math.Max(0f, (state.LooseSedimentKgM2[index] + state.ScratchA[index]) * 1000f);
            liftedGm2 = Math.Min(liftedGm2, availableGm2);
            state.ScratchA[index] -= liftedGm2 / 1000f;
            state.DustGm2[index] += liftedGm2;
            fluxState.Add(SimulationFlux.DustLift, index, liftedGm2);

            var deposition = Math.Min(
                state.DustGm2[index],
                state.DustGm2[index] * (0.012f + state.PrecipitationMmPerHour[index] * 0.035f) * hours);
            state.DustGm2[index] -= deposition;
            state.ScratchA[index] += deposition / 1000f;
            fluxState.Add(SimulationFlux.DustDeposition, index, deposition);
            var previousCrust = state.SurfaceCrustFraction[index];
            var formation = dryness * exposure * (0.35f + state.ClayFraction[index])
                * (1f - previousCrust)
                * 0.00003f;
            var breakdown = previousCrust
                * (0.000008f
                    + state.LiveBiomassGm2[index] / 720f * 0.000035f
                    + state.PrecipitationMmPerHour[index] * 0.0018f
                    + (state.SurfaceWaterMm[index] > 2f ? 0.000025f : 0f));
            state.SurfaceCrustFraction[index] = Math.Clamp(
                previousCrust + (formation - breakdown) * hours,
                0f,
                1f);
            fluxState.Add(
                SimulationFlux.SurfaceCrustChange,
                index,
                state.SurfaceCrustFraction[index] - previousCrust);
        }

        for (var index = 0; index < config.CellCount; index++)
        {
            state.LooseSedimentKgM2[index] = Math.Max(0f, state.LooseSedimentKgM2[index] + state.ScratchA[index]);
        }

        Array.Copy(state.DustGm2, state.ScratchB, config.CellCount);
        AdvectScalar(
            config,
            state,
            state.DustGm2,
            state.ScratchA,
            hours,
            0f,
            fluxState,
            VectorProcess.DustAdvection);
        for (var index = 0; index < config.CellCount; index++)
        {
            fluxState.Add(SimulationFlux.DustTransport, index, state.ScratchA[index] - state.ScratchB[index]);
        }

        Array.Copy(state.ScratchA, state.DustGm2, config.CellCount);
    }

    private static void RecoverSoilCompaction(
        WorldConfig config,
        WorldState state,
        WorldFluxState fluxState,
        float hours)
    {
        for (var index = 0; index < config.CellCount; index++)
        {
            var compaction = state.SoilCompactionFraction[index];
            if (compaction <= 0f) continue;
            var biologicalLoosening = Math.Clamp(state.LiveBiomassGm2[index] / 360f, 0f, 1f);
            var wetting = Math.Clamp((state.RootWaterMm[index] - 35f) / 90f, 0f, 1f);
            var freezeThaw = state.FrozenSoilFraction[index] is > 0.05f and < 0.8f ? 1f : 0f;
            var recovery = Math.Min(
                compaction,
                compaction * (0.000018f + biologicalLoosening * 0.000035f
                    + wetting * 0.00002f + freezeThaw * 0.00008f) * hours);
            state.SoilCompactionFraction[index] -= recovery;
            fluxState.Add(SimulationFlux.SoilCompactionRecovery, index, recovery);
        }
    }

    private static float RootCapacityMm(WorldState state, int index) =>
        Math.Max(
            18f,
            Math.Min(0.45f, state.SoilDepthM[index] * 0.46f)
                * state.Porosity[index]
                * 1000f
                * 0.88f);

    private static float GroundwaterCapacityMm(WorldState state, int index) =>
        170f + Math.Min(1.8f, state.SoilDepthM[index]) * 20f;

    private static float SeasonalAirBaseline(WorldClock clock, ClimateForcingSnapshot climate) =>
        5f + 12f * MathF.Sin(
            TwoPi * ((float)(clock.DayOfYear + clock.HourOfDay / 24d) - 80f - climate.SeasonPhaseShiftDays) / 365f)
        + climate.TemperatureOffsetC;

    private static float Bilinear(float[] values, int width, int height, float x, float y)
    {
        var x0 = Math.Clamp((int)MathF.Floor(x), 0, width - 1);
        var y0 = Math.Clamp((int)MathF.Floor(y), 0, height - 1);
        var x1 = Math.Min(width - 1, x0 + 1);
        var y1 = Math.Min(height - 1, y0 + 1);
        var tx = Math.Clamp(x - x0, 0f, 1f);
        var ty = Math.Clamp(y - y0, 0f, 1f);
        var a = values[Index(x0, y0, width)];
        var b = values[Index(x1, y0, width)];
        var c = values[Index(x0, y1, width)];
        var d = values[Index(x1, y1, width)];
        return Lerp(Lerp(a, b, tx), Lerp(c, d, tx), ty);
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        if (Math.Abs(edge1 - edge0) < 0.000001f)
        {
            return value >= edge1 ? 1f : 0f;
        }

        var t = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    private static int Index(int x, int y, int width) => y * width + x;
}
