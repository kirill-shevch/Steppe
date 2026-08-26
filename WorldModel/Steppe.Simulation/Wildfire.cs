namespace Steppe.Simulation;

internal static partial class WorldSystems
{
    private static readonly (int X, int Y)[] FireNeighbours =
    [
        (-1, -1), (0, -1), (1, -1),
        (-1, 0),           (1, 0),
        (-1, 1),  (0, 1),  (1, 1)
    ];

    private static void UpdateWildfire(
        WorldConfig config,
        WorldClock clock,
        WorldState state,
        WorldFluxState fluxState,
        ClimateForcingSnapshot climate,
        float hours,
        bool allowIgnition)
    {
        RuntimeCompatibility.Clear(state.ScratchA);

        if (config.WildfireEnabled)
        {
            PrepareFireIntensity(config, clock, state, climate, hours, allowIgnition);
            SpreadFire(config, state, fluxState, hours);
        }

        for (var index = 0; index < config.CellCount; index++)
        {
            var beforeIntensity = state.FireIntensityFraction[index];
            var nextIntensity = config.WildfireEnabled
                ? Math.Clamp(state.ScratchA[index], 0f, 1f)
                : 0f;

            var wetSuppression = Math.Clamp(
                state.SurfaceWaterMm[index] / 4f
                    + state.SnowWaterEquivalentMm[index] / 2f
                    + state.PrecipitationMmPerHour[index] * 0.7f,
                0f,
                1f);
            nextIntensity *= MathF.Exp(-wetSuppression * 1.8f * hours);
            if (nextIntensity < 0.0001f)
            {
                nextIntensity = 0f;
            }

            state.FireIntensityFraction[index] = nextIntensity;
            fluxState.Add(SimulationFlux.FireActivityChange, index, nextIntensity - beforeIntensity);

            var beforeScar = state.BurnScarFraction[index];
            if (nextIntensity > 0f)
            {
                BurnFuel(state, fluxState, index, nextIntensity, hours);
            }

            var recovery = beforeScar
                * (0.000045f
                    + Math.Clamp(state.LiveBiomassGm2[index] / 240f, 0f, 1f) * 0.00016f
                    + Math.Clamp(state.RootWaterMm[index] / 120f, 0f, 1f) * 0.000055f)
                * hours;
            var scarGain = (1f - beforeScar)
                * nextIntensity
                * FireDryness(state, index)
                * 0.075f
                * hours;
            var nextScar = Math.Clamp(beforeScar + scarGain - recovery, 0f, 1f);
            state.BurnScarFraction[index] = nextScar;
            fluxState.Add(SimulationFlux.BurnScarChange, index, nextScar - beforeScar);
        }
    }

    private static void PrepareFireIntensity(
        WorldConfig config,
        WorldClock clock,
        WorldState state,
        ClimateForcingSnapshot climate,
        float hours,
        bool allowIgnition)
    {
        var dayIndex = (int)Math.Floor(clock.ElapsedHours / 24d);
        var ignitionSeed = unchecked(config.Seed + dayIndex * 7919 + 0x51ED);
        for (var index = 0; index < config.CellCount; index++)
        {
            var current = state.FireIntensityFraction[index];
            var dryness = FireDryness(state, index);
            var fuel = FireFuel(state, index);
            var fuelFactor = Math.Clamp((fuel - 18f) / 90f, 0f, 1f);
            var wetSuppression = Math.Clamp(
                state.PrecipitationMmPerHour[index] * 0.8f
                    + state.SurfaceWaterMm[index] / 5f
                    + state.SnowWaterEquivalentMm[index] / 3f,
                0f,
                1f);

            if (current > 0f)
            {
                var decayRate = 0.022f + wetSuppression * 0.55f + (1f - fuelFactor) * 0.075f;
                var retained = current * MathF.Exp(-decayRate * hours);
                var sustained = current * dryness * fuelFactor * 0.018f * hours;
                state.ScratchA[index] = Math.Clamp(retained + sustained, 0f, 1f);
            }

            if (!allowIgnition
                || dryness <= 0.18f
                || fuelFactor <= 0.12f
                || state.SurfaceTemperatureC[index] < 22f
                || state.PrecipitationMmPerHour[index] > 0.15f
                || state.SnowWaterEquivalentMm[index] > 0.1f)
            {
                continue;
            }

            var temperatureRisk = Math.Clamp((state.SurfaceTemperatureC[index] - 22f) / 16f, 0f, 1f);
            var wind = MathF.Sqrt(
                state.WindXMs[index] * state.WindXMs[index]
                + state.WindYMs[index] * state.WindYMs[index]);
            var windRisk = 0.55f + Math.Clamp(wind / 16f, 0f, 1f) * 0.75f;
            var lightningRisk = 0.35f + climate.SynopticStormPulse * 3.2f;
            var probability = 1.2e-6f
                * dryness
                * fuelFactor
                * temperatureRisk
                * windRisk
                * lightningRisk;
            var x = index % config.Width;
            var y = index / config.Width;
            if (DeterministicNoise.Hash01(x, y, ignitionSeed) < probability)
            {
                state.ScratchA[index] = Math.Max(
                    state.ScratchA[index],
                    0.32f + DeterministicNoise.Hash01(y, x, ignitionSeed + 37) * 0.28f);
            }
        }
    }

    private static void SpreadFire(
        WorldConfig config,
        WorldState state,
        WorldFluxState fluxState,
        float hours)
    {
        for (var y = 0; y < config.Height; y++)
        {
            for (var x = 0; x < config.Width; x++)
            {
                var index = Index(x, y, config.Width);
                var sourceIntensity = state.FireIntensityFraction[index];
                if (sourceIntensity < 0.002f)
                {
                    continue;
                }

                var windX = state.WindXMs[index];
                var windY = state.WindYMs[index];
                var windSpeed = MathF.Sqrt(windX * windX + windY * windY);
                var inverseWind = windSpeed > 1e-5f ? 1f / windSpeed : 0f;
                foreach (var (offsetX, offsetY) in FireNeighbours)
                {
                    var targetX = x + offsetX;
                    var targetY = y + offsetY;
                    if (targetX < 0 || targetY < 0 || targetX >= config.Width || targetY >= config.Height)
                    {
                        continue;
                    }

                    var target = Index(targetX, targetY, config.Width);
                    var dryness = FireDryness(state, target);
                    var fuelFactor = Math.Clamp((FireFuel(state, target) - 18f) / 90f, 0f, 1f);
                    if (dryness <= 0f || fuelFactor <= 0f)
                    {
                        continue;
                    }

                    var length = offsetX != 0 && offsetY != 0 ? 1.41421356f : 1f;
                    var alignment = inverseWind > 0f
                        ? Math.Max(0f, (windX * offsetX + windY * offsetY) * inverseWind / length)
                        : 0f;
                    var windAssist = Math.Clamp(windSpeed / 14f, 0f, 1f);
                    var spread = sourceIntensity
                        * dryness
                        * fuelFactor
                        * (0.018f + alignment * (0.095f + windAssist * 0.055f))
                        * hours
                        / length;
                    var acceptedSpread = Math.Max(0f, spread - state.ScratchA[target]);
                    if (acceptedSpread <= 0f)
                    {
                        continue;
                    }

                    state.ScratchA[target] = spread;
                    fluxState.AddVector(
                        VectorProcess.FireSpread,
                        index,
                        acceptedSpread * offsetX / length,
                        acceptedSpread * offsetY / length);
                }
            }
        }
    }

    private static void BurnFuel(
        WorldState state,
        WorldFluxState fluxState,
        int index,
        float intensity,
        float hours)
    {
        var dryness = FireDryness(state, index);
        if (dryness <= 0f)
        {
            return;
        }

        var drive = intensity * dryness * hours;
        var liveBefore = state.LiveBiomassGm2[index];
        var dryBefore = state.DryBiomassGm2[index];
        var litterBefore = state.LitterBiomassGm2[index];
        var liveBurn = Math.Min(liveBefore, drive * 0.55f);
        var dryBurn = Math.Min(dryBefore, drive * 2.2f);
        var litterBurn = Math.Min(litterBefore, drive * 1.1f);

        state.LiveBiomassGm2[index] -= liveBurn;
        state.DryBiomassGm2[index] -= dryBurn;
        state.LitterBiomassGm2[index] -= litterBurn;
        fluxState.Add(SimulationFlux.FireLiveCombustion, index, liveBurn);
        fluxState.Add(SimulationFlux.FireDryCombustion, index, dryBurn);
        fluxState.Add(SimulationFlux.FireLitterCombustion, index, litterBurn);

        var plantNitrogenRelease = liveBefore > 1e-6f
            ? Math.Min(state.PlantNitrogenGm2[index], state.PlantNitrogenGm2[index] * liveBurn / liveBefore)
            : 0f;
        if (plantNitrogenRelease > 0f)
        {
            state.PlantNitrogenGm2[index] -= plantNitrogenRelease;
            state.AvailableNitrogenGm2[index] += plantNitrogenRelease * 0.7f;
            state.OrganicNitrogenGm2[index] += plantNitrogenRelease * 0.3f;
            fluxState.Add(SimulationFlux.FirePlantNitrogenRelease, index, plantNitrogenRelease);
        }

        var organicNitrogenRelease = Math.Min(
            state.OrganicNitrogenGm2[index],
            (dryBurn + litterBurn) * 0.0105f);
        if (organicNitrogenRelease > 0f)
        {
            state.OrganicNitrogenGm2[index] -= organicNitrogenRelease;
            state.AvailableNitrogenGm2[index] += organicNitrogenRelease;
            fluxState.Add(SimulationFlux.FireOrganicNitrogenRelease, index, organicNitrogenRelease);
        }

        var seedLoss = Math.Min(
            state.SeedBank[index],
            state.SeedBank[index] * intensity * dryness * 0.012f * hours);
        state.SeedBank[index] -= seedLoss;
        fluxState.Add(SimulationFlux.FireSeedBankDamage, index, seedLoss);
    }

    private static float FireDryness(WorldState state, int index)
    {
        var rootDryness = Math.Clamp((82f - state.RootWaterMm[index]) / 62f, 0f, 1f);
        var surfaceWetness = Math.Clamp(state.SurfaceWaterMm[index] / 3f, 0f, 1f);
        var rainSuppression = Math.Clamp(state.PrecipitationMmPerHour[index] / 0.25f, 0f, 1f);
        var snowSuppression = Math.Clamp(state.SnowWaterEquivalentMm[index] / 0.5f, 0f, 1f);
        return rootDryness
            * (1f - surfaceWetness)
            * (1f - rainSuppression)
            * (1f - snowSuppression);
    }

    private static float FireFuel(WorldState state, int index) =>
        state.DryBiomassGm2[index]
        + state.LitterBiomassGm2[index] * 0.72f
        + state.LiveBiomassGm2[index] * 0.16f;
}
