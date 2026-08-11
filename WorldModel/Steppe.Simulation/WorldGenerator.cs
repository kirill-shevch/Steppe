namespace Steppe.Simulation;

internal static class WorldGenerator
{
    private static readonly (int X, int Y)[] Neighbours =
    [
        (-1, -1), (0, -1), (1, -1),
        (-1, 0),           (1, 0),
        (-1, 1),  (0, 1),  (1, 1)
    ];

    public static WorldState Generate(WorldConfig config)
    {
        config.Validate();
        var state = new WorldState(config.CellCount);
        GenerateGeology(config, state);

        for (var pass = 0; pass < config.GeographyErosionPasses; pass++)
        {
            ComputeTerrainDerivatives(config, state);
            ErodeOnce(config, state);
        }

        ComputeTerrainDerivatives(config, state);
        ResolveDrainageSinks(config, state);
        ComputeSlopeAndAspect(config, state);
        AssignCatchments(config, state);
        InitializeLivingWorld(config, state);
        return state;
    }

    private static void GenerateGeology(WorldConfig config, WorldState state)
    {
        var widthDenominator = Math.Max(1, config.Width - 1);
        var heightDenominator = Math.Max(1, config.Height - 1);
        var angleA = DeterministicNoise.Hash01(3, 7, config.Seed) * MathF.PI;
        var angleB = DeterministicNoise.Hash01(11, 5, config.Seed + 31) * MathF.PI;
        var offsetA = DeterministicNoise.Hash01(17, 23, config.Seed + 67) * 0.6f - 0.3f;
        var offsetB = DeterministicNoise.Hash01(29, 13, config.Seed + 97) * 0.6f - 0.3f;

        for (var y = 0; y < config.Height; y++)
        {
            var ny = y / (float)heightDenominator;
            for (var x = 0; x < config.Width; x++)
            {
                var nx = x / (float)widthDenominator;
                var index = Index(x, y, config.Width);
                var px = nx - 0.5f;
                var py = ny - 0.5f;

                var faultA = LineInfluence(px, py, angleA, offsetA, 0.055f);
                var faultB = LineInfluence(px, py, angleB, offsetB, 0.038f);
                var fault = Math.Clamp(faultA * 0.72f + faultB * 0.5f, 0f, 1f);
                var continental = DeterministicNoise.Fractal(nx * 2.1f, ny * 2.1f, config.Seed, 5);
                var local = DeterministicNoise.Fractal(nx * 8.5f, ny * 8.5f, config.Seed + 7001, 4);
                var uplift = faultA * 150f - faultB * 75f;
                var edgeDistance = MathF.Min(MathF.Min(nx, 1f - nx), MathF.Min(ny, 1f - ny));
                var edgeOutlet = 180f * MathF.Pow(Math.Clamp(1f - edgeDistance / 0.12f, 0f, 1f), 2f);

                state.ElevationM[index] = 410f + continental * 190f + local * 42f + uplift - edgeOutlet;
                state.FaultInfluence[index] = fault;

                var sand = Math.Clamp(0.38f
                    + DeterministicNoise.Fractal(nx * 4.2f, ny * 4.2f, config.Seed + 201, 4) * 0.23f
                    + faultB * 0.10f, 0.08f, 0.78f);
                var clay = Math.Clamp(0.28f
                    + DeterministicNoise.Fractal(nx * 3.3f, ny * 3.3f, config.Seed + 401, 4) * 0.18f
                    + faultA * 0.08f, 0.07f, 0.66f);
                var silt = Math.Max(0.05f, 1f - sand - clay);
                var textureTotal = sand + clay + silt;

                state.SandFraction[index] = sand / textureTotal;
                state.SiltFraction[index] = silt / textureTotal;
                state.ClayFraction[index] = clay / textureTotal;
                state.SoilDepthM[index] = Math.Clamp(0.25f + (1f - fault) * 1.35f + local * 0.18f, 0.12f, 2.5f);
                state.Porosity[index] = Math.Clamp(0.31f + state.ClayFraction[index] * 0.24f, 0.28f, 0.58f);
                state.PermeabilityMmPerHour[index] = Math.Clamp(
                    2.5f + state.SandFraction[index] * 25f - state.ClayFraction[index] * 4.5f,
                    0.8f,
                    28f);
                state.MineralContent[index] = Math.Clamp(0.46f + fault * 0.26f - sand * 0.18f, 0.12f, 0.92f);
                state.RockHardness[index] = Math.Clamp(0.42f + fault * 0.38f + local * 0.09f, 0.18f, 0.95f);
            }
        }
    }

    private static float LineInfluence(float x, float y, float angle, float offset, float width)
    {
        var distance = MathF.Abs(x * MathF.Cos(angle) + y * MathF.Sin(angle) - offset);
        var normalized = Math.Clamp(1f - distance / width, 0f, 1f);
        return normalized * normalized * (3f - 2f * normalized);
    }

    private static void ComputeTerrainDerivatives(WorldConfig config, WorldState state)
    {
        ComputeSlopeAndAspect(config, state);
        for (var y = 0; y < config.Height; y++)
        {
            for (var x = 0; x < config.Width; x++)
            {
                var index = Index(x, y, config.Width);
                var bestIndex = index;
                var bestDropPerMeter = 0f;
                foreach (var (offsetX, offsetY) in Neighbours)
                {
                    var neighbourX = x + offsetX;
                    var neighbourY = y + offsetY;
                    if (!Inside(neighbourX, neighbourY, config))
                    {
                        continue;
                    }

                    var neighbour = Index(neighbourX, neighbourY, config.Width);
                    var distance = offsetX != 0 && offsetY != 0 ? 1.41421356f : 1f;
                    var drop = (state.ElevationM[index] - state.ElevationM[neighbour]) / distance;
                    if (drop > bestDropPerMeter)
                    {
                        bestDropPerMeter = drop;
                        bestIndex = neighbour;
                    }
                }

                var edge = x == 0 || y == 0 || x == config.Width - 1 || y == config.Height - 1;
                state.DrainTo[index] = edge && bestIndex == index ? -1 : bestIndex;
            }
        }
    }

    private static void ComputeSlopeAndAspect(WorldConfig config, WorldState state)
    {
        for (var y = 0; y < config.Height; y++)
        {
            for (var x = 0; x < config.Width; x++)
            {
                var index = Index(x, y, config.Width);
                var left = state.ElevationM[Index(Math.Max(0, x - 1), y, config.Width)];
                var right = state.ElevationM[Index(Math.Min(config.Width - 1, x + 1), y, config.Width)];
                var down = state.ElevationM[Index(x, Math.Max(0, y - 1), config.Width)];
                var up = state.ElevationM[Index(x, Math.Min(config.Height - 1, y + 1), config.Width)];
                var dx = (right - left) / (2f * config.CellSizeMeters);
                var dy = (up - down) / (2f * config.CellSizeMeters);
                state.Slope[index] = MathF.Sqrt(dx * dx + dy * dy);
                state.AspectRadians[index] = MathF.Atan2(-dy, -dx);
            }
        }
    }

    private static void ErodeOnce(WorldConfig config, WorldState state)
    {
        var order = Enumerable.Range(0, config.CellCount)
            .OrderByDescending(index => state.ElevationM[index])
            .ToArray();
        Array.Fill(state.ScratchA, 1f);
        Array.Clear(state.ScratchB);

        foreach (var index in order)
        {
            var target = state.DrainTo[index];
            if (target >= 0 && target != index)
            {
                state.ScratchA[target] += state.ScratchA[index];
            }
        }

        foreach (var index in order)
        {
            var target = state.DrainTo[index];
            if (target < 0 || target == index)
            {
                continue;
            }

            var capacity = MathF.Pow(state.ScratchA[index], 0.32f)
                * state.Slope[index]
                * (1f - state.RockHardness[index])
                * 8.5f;
            var eroded = Math.Clamp(capacity, 0f, 0.75f);
            state.ScratchB[index] -= eroded;
            state.ScratchB[target] += eroded * 0.32f;
            state.SoilDepthM[index] = Math.Max(0.1f, state.SoilDepthM[index] - eroded * 0.0015f);
            state.SoilDepthM[target] = Math.Min(3f, state.SoilDepthM[target] + eroded * 0.0006f);
        }

        for (var index = 0; index < config.CellCount; index++)
        {
            state.ElevationM[index] += state.ScratchB[index];
        }
    }

    private static void AssignCatchments(WorldConfig config, WorldState state)
    {
        Array.Fill(state.CatchmentId, -1);
        var terminalIds = new Dictionary<int, int>();
        var nextCatchment = 0;

        for (var start = 0; start < config.CellCount; start++)
        {
            if (state.CatchmentId[start] >= 0)
            {
                continue;
            }

            var path = new List<int>(32);
            var current = start;
            while (state.CatchmentId[current] < 0)
            {
                path.Add(current);
                var next = state.DrainTo[current];
                if (next < 0 || next == current)
                {
                    var terminalKey = next < 0 ? -(current + 1) : current + 1;
                    if (!terminalIds.TryGetValue(terminalKey, out var terminalId))
                    {
                        terminalId = nextCatchment++;
                        terminalIds.Add(terminalKey, terminalId);
                    }

                    foreach (var index in path)
                    {
                        state.CatchmentId[index] = terminalId;
                    }

                    break;
                }

                current = next;
                if (state.CatchmentId[current] >= 0)
                {
                    foreach (var index in path)
                    {
                        state.CatchmentId[index] = state.CatchmentId[current];
                    }

                    break;
                }
            }
        }
    }

    /// <summary>
    /// Priority-flood constructs a monotonically draining hydrological surface without
    /// erasing the visible terrain. The difference to the real elevation becomes the
    /// finite storage of a depression; excess water follows the spill path to an edge.
    /// </summary>
    private static void ResolveDrainageSinks(WorldConfig config, WorldState state)
    {
        var visited = new bool[config.CellCount];
        var queue = new PriorityQueue<int, float>();

        void AddBoundary(int x, int y)
        {
            var index = Index(x, y, config.Width);
            if (visited[index])
            {
                return;
            }

            visited[index] = true;
            state.ScratchA[index] = state.ElevationM[index];
            state.DepressionStorageMm[index] = 0f;
            state.DrainTo[index] = -1;
            queue.Enqueue(index, state.ScratchA[index]);
        }

        for (var x = 0; x < config.Width; x++)
        {
            AddBoundary(x, 0);
            AddBoundary(x, config.Height - 1);
        }

        for (var y = 1; y < config.Height - 1; y++)
        {
            AddBoundary(0, y);
            AddBoundary(config.Width - 1, y);
        }

        while (queue.TryDequeue(out var current, out _))
        {
            var currentX = current % config.Width;
            var currentY = current / config.Width;
            foreach (var (offsetX, offsetY) in Neighbours)
            {
                var neighbourX = currentX + offsetX;
                var neighbourY = currentY + offsetY;
                if (!Inside(neighbourX, neighbourY, config))
                {
                    continue;
                }

                var neighbour = Index(neighbourX, neighbourY, config.Width);
                if (visited[neighbour])
                {
                    continue;
                }

                visited[neighbour] = true;
                var filledElevation = Math.Max(state.ElevationM[neighbour], state.ScratchA[current]);
                state.ScratchA[neighbour] = filledElevation;
                state.DrainTo[neighbour] = current;
                queue.Enqueue(neighbour, filledElevation);
            }
        }

        // At 250 m resolution, metre-deep single-cell pits are sampling artefacts,
        // not meaningful lakes. Preserve at most 220 mm of local depression while
        // raising deeper artefacts to their depositional fill surface. The remaining
        // spill rise is now commensurate with the water actually stored in the cell.
        const float maximumDepressionDepthM = 0.22f;
        for (var index = 0; index < config.CellCount; index++)
        {
            var filledElevation = state.ScratchA[index];
            var conditionedElevation = Math.Max(state.ElevationM[index], filledElevation - maximumDepressionDepthM);
            state.ElevationM[index] = conditionedElevation;
            state.DepressionStorageMm[index] = Math.Max(0f, (filledElevation - conditionedElevation) * 1000f);
        }
    }

    private static void InitializeLivingWorld(WorldConfig config, WorldState state)
    {
        for (var y = 0; y < config.Height; y++)
        {
            for (var x = 0; x < config.Width; x++)
            {
                var index = Index(x, y, config.Width);
                var nx = x / (float)Math.Max(1, config.Width - 1);
                var ny = y / (float)Math.Max(1, config.Height - 1);
                var weather = DeterministicNoise.Fractal(nx * 3.8f, ny * 3.8f, config.Seed + 9001, 4);
                var basin = state.DepressionStorageMm[index] > 1f ? 1f : 0f;
                var elevationCooling = Math.Max(0f, state.ElevationM[index] - 250f) * 0.0062f;

                state.SurfaceTemperatureC[index] = -3.5f - elevationCooling + weather * 2f;
                state.SoilTemperatureC[index] = 1.5f - elevationCooling * 0.55f;
                state.AirTemperatureC[index] = -4.5f - elevationCooling + weather * 1.3f;
                state.AirPressureHpa[index] = 1013.25f * MathF.Exp(-state.ElevationM[index] / 8500f);
                state.AirHumidityMm[index] = Math.Clamp(7.2f + weather * 2.8f + (1f - nx) * 2f, 2f, 16f);
                state.CloudWaterMm[index] = Math.Max(0f, weather * 1.4f + 0.25f);
                state.WindXMs[index] = 5.5f + weather * 1.2f;
                state.WindYMs[index] = DeterministicNoise.Value(nx * 5f, ny * 5f, config.Seed + 42) * 1.5f;

                state.SurfaceWaterMm[index] = basin * Math.Min(5f, state.DepressionStorageMm[index] * 0.02f);
                state.RootWaterMm[index] = Math.Clamp(48f + weather * 16f + state.ClayFraction[index] * 18f, 12f, 110f);
                // Start the slow reservoir close to the century-scale attractor. The old
                // 55 mm baseline relaxed towards roughly 30 mm for decades, creating an
                // artificial drying trend even though the open water ledger was closed.
                state.GroundwaterMm[index] = Math.Clamp(29.5f + basin * 16f + weather * 8f, 12f, 90f);
                state.SnowWaterEquivalentMm[index] = Math.Clamp(5f + elevationCooling * 1.7f + weather * 2f, 0f, 24f);
                state.FrozenSoilFraction[index] = Math.Clamp((-state.SoilTemperatureC[index] + 1f) / 8f, 0f, 0.85f);

                var fertility = state.MineralContent[index] * state.SoilDepthM[index];
                // January initialization represents an established dormant steppe, not
                // a freshly provisioned growth pulse. These pools are centred on the
                // stable winter state measured after the 320x320 century baseline.
                state.LiveBiomassGm2[index] = Math.Clamp(16f + fertility * 22f + weather * 6f, 12f, 80f);
                state.DryBiomassGm2[index] = Math.Clamp(12f + fertility * 16f - weather * 4f, 6f, 80f);
                state.LitterBiomassGm2[index] = Math.Clamp(32f + fertility * 34f, 18f, 110f);
                state.SeedBank[index] = Math.Clamp(0.24f + fertility * 0.20f, 0.15f, 0.8f);
                state.SoilOrganicMatterGm2[index] = Math.Clamp(900f + fertility * 870f, 650f, 2200f);
                state.AvailableNitrogenGm2[index] = Math.Clamp(0.35f + fertility * 0.44f, 0.25f, 1.2f);
                state.PlantNitrogenGm2[index] = state.LiveBiomassGm2[index] * 0.018f;
                var totalNitrogen = Math.Clamp(14.4f + fertility * 8.75f, 14f, 24f);
                state.OrganicNitrogenGm2[index] = Math.Clamp(
                    totalNitrogen - state.AvailableNitrogenGm2[index] - state.PlantNitrogenGm2[index],
                    4f,
                    30f);
                state.LooseSedimentKgM2[index] = Math.Clamp(2.2f + (1f - state.RockHardness[index]) * 5f, 0.2f, 8f);
                state.SurfaceCrustFraction[index] = Math.Clamp(
                    0.34f + state.ClayFraction[index] * 0.65f - state.LiveBiomassGm2[index] / 1200f,
                    0f,
                    0.8f);
            }
        }
    }

    private static bool Inside(int x, int y, WorldConfig config) =>
        x >= 0 && y >= 0 && x < config.Width && y < config.Height;

    private static int Index(int x, int y, int width) => y * width + x;
}
