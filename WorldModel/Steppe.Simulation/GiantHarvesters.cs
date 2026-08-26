namespace Steppe.Simulation;

public enum GiantHarvesterActivity
{
    Migrating,
    Grazing,
    Drinking,
    Molting
}

public sealed record GiantHarvesterSnapshot(
    int Id,
    float X,
    float Y,
    float HeadingX,
    float HeadingY,
    GiantHarvesterActivity Activity,
    float GrazedLiveBiomassGm2,
    float GrazedDryBiomassGm2,
    float DrunkWaterMm,
    float DistanceCells);

public sealed record GiantHarvesterMoltSnapshot(
    long Id,
    float X,
    float Y,
    float ChitinKg,
    double CreatedAtHours);

public sealed record GiantHarvesterPopulationSnapshot(
    GiantHarvesterSnapshot[] Harvesters,
    GiantHarvesterMoltSnapshot[] Molts);

internal sealed class GiantHarvesterAgent
{
    public int Id;
    public float X;
    public float Y;
    public float HeadingX;
    public float HeadingY;
    public GiantHarvesterActivity Activity;
    public float GrazedLiveBiomassGm2;
    public float GrazedDryBiomassGm2;
    public float DrunkWaterMm;
    public float DistanceCells;
    public double NextMoltAtHours;
}

internal sealed class GiantHarvesterMolt
{
    public long Id;
    public float X;
    public float Y;
    public float ChitinKg;
    public double CreatedAtHours;
}

internal sealed class WorldFauna
{
    private const double MoltIntervalHours = 150d * 24d;
    private readonly List<GiantHarvesterAgent> harvesters;
    private readonly List<GiantHarvesterMolt> molts;
    private long nextMoltId;

    private WorldFauna(
        List<GiantHarvesterAgent> harvesters,
        List<GiantHarvesterMolt> molts,
        long nextMoltId)
    {
        this.harvesters = harvesters;
        this.molts = molts;
        this.nextMoltId = nextMoltId;
    }

    public static WorldFauna Generate(WorldConfig config)
    {
        var agents = new List<GiantHarvesterAgent>(config.GiantHarvesterCount);
        for (var id = 0; id < config.GiantHarvesterCount; id++)
        {
            var x = 1f + DeterministicNoise.Hash01(id, 17, config.Seed + 40117) * (config.Width - 2f);
            var y = 1f + DeterministicNoise.Hash01(id, 29, config.Seed + 70429) * (config.Height - 2f);
            var angle = DeterministicNoise.Hash01(id, 43, config.Seed + 99041) * MathF.PI * 2f;
            agents.Add(new GiantHarvesterAgent
            {
                Id = id,
                X = x,
                Y = y,
                HeadingX = MathF.Cos(angle),
                HeadingY = MathF.Sin(angle),
                Activity = GiantHarvesterActivity.Migrating,
                NextMoltAtHours = (35d + DeterministicNoise.Hash01(id, 59, config.Seed + 12007) * 115d) * 24d
            });
        }

        return new WorldFauna(agents, [], 1);
    }

    public void Step(
        WorldConfig config,
        WorldClock clock,
        WorldState state,
        WaterBudget waterBudget,
        WorldFluxState fluxState,
        float hours)
    {
        foreach (var harvester in harvesters)
        {
            Move(config, clock, state, fluxState, harvester, hours);
            AffectTrail(config, state, waterBudget, fluxState, harvester, hours);
            if (clock.ElapsedHours + hours + 1e-8 >= harvester.NextMoltAtHours)
            {
                harvester.Activity = GiantHarvesterActivity.Molting;
                molts.Add(new GiantHarvesterMolt
                {
                    Id = nextMoltId++,
                    X = harvester.X,
                    Y = harvester.Y,
                    ChitinKg = 18f + 12f * DeterministicNoise.Hash01(
                        harvester.Id,
                        (int)(harvester.NextMoltAtHours / 24d),
                        config.Seed + 55001),
                    CreatedAtHours = clock.ElapsedHours + hours
                });
                harvester.NextMoltAtHours += MoltIntervalHours;
            }
        }

        molts.RemoveAll(molt => molt.ChitinKg <= 0.001f || clock.ElapsedHours + hours - molt.CreatedAtHours > 3d * 365d * 24d);
    }

    public GiantHarvesterPopulationSnapshot Capture() => new(
        harvesters.Select(agent => new GiantHarvesterSnapshot(
            agent.Id,
            agent.X,
            agent.Y,
            agent.HeadingX,
            agent.HeadingY,
            agent.Activity,
            agent.GrazedLiveBiomassGm2,
            agent.GrazedDryBiomassGm2,
            agent.DrunkWaterMm,
            agent.DistanceCells)).ToArray(),
        molts.Select(molt => new GiantHarvesterMoltSnapshot(
            molt.Id,
            molt.X,
            molt.Y,
            molt.ChitinKg,
            molt.CreatedAtHours)).ToArray());

    public float CollectMolt(float x, float y, float radiusCells, float maximumKg)
    {
        var remaining = Math.Max(0f, maximumKg);
        var collected = 0f;
        foreach (var molt in molts.OrderBy(item => DistanceSquared(item.X, item.Y, x, y)))
        {
            if (remaining <= 0f || DistanceSquared(molt.X, molt.Y, x, y) > radiusCells * radiusCells)
            {
                continue;
            }

            var amount = Math.Min(remaining, molt.ChitinKg);
            molt.ChitinKg -= amount;
            remaining -= amount;
            collected += amount;
        }

        molts.RemoveAll(molt => molt.ChitinKg <= 0.001f);
        return collected;
    }

    public void Save(BinaryWriter writer)
    {
        writer.Write(nextMoltId);
        writer.Write(harvesters.Count);
        foreach (var agent in harvesters)
        {
            writer.Write(agent.Id);
            writer.Write(agent.X);
            writer.Write(agent.Y);
            writer.Write(agent.HeadingX);
            writer.Write(agent.HeadingY);
            writer.Write((int)agent.Activity);
            writer.Write(agent.GrazedLiveBiomassGm2);
            writer.Write(agent.GrazedDryBiomassGm2);
            writer.Write(agent.DrunkWaterMm);
            writer.Write(agent.DistanceCells);
            writer.Write(agent.NextMoltAtHours);
        }

        writer.Write(molts.Count);
        foreach (var molt in molts)
        {
            writer.Write(molt.Id);
            writer.Write(molt.X);
            writer.Write(molt.Y);
            writer.Write(molt.ChitinKg);
            writer.Write(molt.CreatedAtHours);
        }
    }

    public static WorldFauna Load(BinaryReader reader)
    {
        var nextId = reader.ReadInt64();
        var agentCount = reader.ReadInt32();
        var agents = new List<GiantHarvesterAgent>(agentCount);
        for (var index = 0; index < agentCount; index++)
        {
            agents.Add(new GiantHarvesterAgent
            {
                Id = reader.ReadInt32(),
                X = reader.ReadSingle(),
                Y = reader.ReadSingle(),
                HeadingX = reader.ReadSingle(),
                HeadingY = reader.ReadSingle(),
                Activity = (GiantHarvesterActivity)reader.ReadInt32(),
                GrazedLiveBiomassGm2 = reader.ReadSingle(),
                GrazedDryBiomassGm2 = reader.ReadSingle(),
                DrunkWaterMm = reader.ReadSingle(),
                DistanceCells = reader.ReadSingle(),
                NextMoltAtHours = reader.ReadDouble()
            });
        }

        var moltCount = reader.ReadInt32();
        var molts = new List<GiantHarvesterMolt>(moltCount);
        for (var index = 0; index < moltCount; index++)
        {
            molts.Add(new GiantHarvesterMolt
            {
                Id = reader.ReadInt64(),
                X = reader.ReadSingle(),
                Y = reader.ReadSingle(),
                ChitinKg = reader.ReadSingle(),
                CreatedAtHours = reader.ReadDouble()
            });
        }

        return new WorldFauna(agents, molts, nextId);
    }

    private static void Move(
        WorldConfig config,
        WorldClock clock,
        WorldState state,
        WorldFluxState fluxState,
        GiantHarvesterAgent agent,
        float hours)
    {
        var bestX = agent.X + agent.HeadingX * 2.5f;
        var bestY = agent.Y + agent.HeadingY * 2.5f;
        var bestScore = float.NegativeInfinity;
        for (var direction = 0; direction < 12; direction++)
        {
            var angle = direction * MathF.PI * 2f / 12f;
            var candidateX = Math.Clamp(agent.X + MathF.Cos(angle) * 3f, 0.5f, config.Width - 1.5f);
            var candidateY = Math.Clamp(agent.Y + MathF.Sin(angle) * 3f, 0.5f, config.Height - 1.5f);
            var index = Index(candidateX, candidateY, config.Width, config.Height);
            var temperatureFit = 1f - Math.Clamp(MathF.Abs(state.SoilTemperatureC[index] - 17f) / 28f, 0f, 1f);
            var score = state.LiveBiomassGm2[index] * 0.017f
                + state.DryBiomassGm2[index] * 0.004f
                + Math.Min(1.5f, state.SurfaceWaterMm[index] * 0.45f)
                + state.RootWaterMm[index] * 0.004f
                + temperatureFit * 1.2f
                - state.SnowWaterEquivalentMm[index] * 0.025f
                - state.Slope[index] * 140f
                - state.SoilCompactionFraction[index] * 0.7f
                + DeterministicNoise.Hash01(index, agent.Id, config.Seed + clock.DayOfYear * 31) * 0.08f;
            if (score > bestScore)
            {
                bestScore = score;
                bestX = candidateX;
                bestY = candidateY;
            }
        }

        var desiredX = bestX - agent.X;
        var desiredY = bestY - agent.Y;
        Normalize(ref desiredX, ref desiredY);
        var headingX = agent.HeadingX * 0.58f + desiredX * 0.42f;
        var headingY = agent.HeadingY * 0.58f + desiredY * 0.42f;
        Normalize(ref headingX, ref headingY);
        agent.HeadingX = headingX;
        agent.HeadingY = headingY;

        var origin = Index(agent.X, agent.Y, config.Width, config.Height);
        var terrainPenalty = 1f + state.Slope[origin] * 80f + state.SnowWaterEquivalentMm[origin] * 0.018f;
        var distance = Math.Min(0.15f * hours / terrainPenalty, 0.75f);
        var nextX = Math.Clamp(agent.X + headingX * distance, 0.25f, config.Width - 1.25f);
        var nextY = Math.Clamp(agent.Y + headingY * distance, 0.25f, config.Height - 1.25f);
        var movedX = nextX - agent.X;
        var movedY = nextY - agent.Y;
        agent.X = nextX;
        agent.Y = nextY;
        agent.DistanceCells += MathF.Sqrt(movedX * movedX + movedY * movedY);
        fluxState.AddVector(VectorProcess.GiantHarvesterMovement, origin, movedX, movedY);
        agent.Activity = GiantHarvesterActivity.Migrating;
    }

    private static void AffectTrail(
        WorldConfig config,
        WorldState state,
        WaterBudget waterBudget,
        WorldFluxState fluxState,
        GiantHarvesterAgent agent,
        float hours)
    {
        var centerX = (int)MathF.Round(agent.X);
        var centerY = (int)MathF.Round(agent.Y);
        var drank = 0f;
        var grazed = 0f;
        for (var offsetY = -1; offsetY <= 1; offsetY++)
        {
            for (var offsetX = -1; offsetX <= 1; offsetX++)
            {
                var x = centerX + offsetX;
                var y = centerY + offsetY;
                if (x < 0 || y < 0 || x >= config.Width || y >= config.Height) continue;
                var distance = MathF.Sqrt(offsetX * offsetX + offsetY * offsetY);
                var weight = MathF.Max(0f, 1f - distance / 2f);
                var index = y * config.Width + x;

                var liveBefore = state.LiveBiomassGm2[index];
                var liveTaken = Math.Min(Math.Max(0f, liveBefore - 10f), 0.18f * hours * weight);
                var plantNitrogen = liveBefore > 1e-8f
                    ? Math.Min(state.PlantNitrogenGm2[index], state.PlantNitrogenGm2[index] * liveTaken / liveBefore)
                    : 0f;
                var dryTaken = Math.Min(Math.Max(0f, state.DryBiomassGm2[index] - 4f), 0.055f * hours * weight);
                var litter = liveTaken * 0.28f + dryTaken * 0.35f;
                state.LiveBiomassGm2[index] -= liveTaken;
                state.DryBiomassGm2[index] -= dryTaken;
                state.LitterBiomassGm2[index] += litter;
                state.PlantNitrogenGm2[index] -= plantNitrogen;
                state.OrganicNitrogenGm2[index] += plantNitrogen;
                var manure = (liveTaken + dryTaken - litter) * 0.055f;
                state.SoilOrganicMatterGm2[index] += manure;

                var waterTaken = Math.Min(state.SurfaceWaterMm[index], 0.014f * hours * weight);
                state.SurfaceWaterMm[index] -= waterTaken;
                waterBudget.AddExternal(-waterTaken);

                var seedBefore = state.SeedBank[index];
                state.SeedBank[index] = Math.Min(1.2f, seedBefore + 0.00012f * hours * weight);
                var crustBefore = state.SurfaceCrustFraction[index];
                state.SurfaceCrustFraction[index] = Math.Max(0f, crustBefore - 0.0007f * hours * weight);
                var compactionBefore = state.SoilCompactionFraction[index];
                state.SoilCompactionFraction[index] = Math.Min(
                    1f,
                    compactionBefore + 0.00004f * hours * weight * (1f - state.FrozenSoilFraction[index] * 0.7f));

                fluxState.Add(SimulationFlux.GiantHarvesterLiveGrazing, index, liveTaken);
                fluxState.Add(SimulationFlux.GiantHarvesterDryGrazing, index, dryTaken);
                fluxState.Add(SimulationFlux.GiantHarvesterTramplingLitter, index, litter);
                fluxState.Add(SimulationFlux.GiantHarvesterPlantNitrogenReturn, index, plantNitrogen);
                fluxState.Add(SimulationFlux.GiantHarvesterManure, index, manure);
                fluxState.Add(SimulationFlux.GiantHarvesterDrinking, index, waterTaken);
                fluxState.Add(SimulationFlux.GiantHarvesterSeedDispersal, index, state.SeedBank[index] - seedBefore);
                fluxState.Add(SimulationFlux.GiantHarvesterCrustBreakdown, index, crustBefore - state.SurfaceCrustFraction[index]);
                fluxState.Add(SimulationFlux.GiantHarvesterCompaction, index, state.SoilCompactionFraction[index] - compactionBefore);
                drank += waterTaken;
                grazed += liveTaken + dryTaken;
                agent.GrazedLiveBiomassGm2 += liveTaken;
                agent.GrazedDryBiomassGm2 += dryTaken;
                agent.DrunkWaterMm += waterTaken;
            }
        }

        if (drank > 0.0001f) agent.Activity = GiantHarvesterActivity.Drinking;
        else if (grazed > 0.01f) agent.Activity = GiantHarvesterActivity.Grazing;
    }

    private static int Index(float x, float y, int width, int height) =>
        Math.Clamp((int)MathF.Round(y), 0, height - 1) * width + Math.Clamp((int)MathF.Round(x), 0, width - 1);

    private static void Normalize(ref float x, ref float y)
    {
        var length = MathF.Sqrt(x * x + y * y);
        if (length < 1e-6f)
        {
            x = 1f;
            y = 0f;
            return;
        }

        x /= length;
        y /= length;
    }

    private static float DistanceSquared(float ax, float ay, float bx, float by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return dx * dx + dy * dy;
    }
}

public sealed partial class FiniteWorld
{
    public float CollectGiantHarvesterMolt(int x, int y, float radiusCells, float maximumKg)
    {
        if (!RuntimeCompatibility.IsFinite(radiusCells) || radiusCells < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(radiusCells));
        }
        if (!RuntimeCompatibility.IsFinite(maximumKg) || maximumKg < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumKg));
        }

        lock (sync)
        {
            CheckedIndex(x, y);
            return fauna.CollectMolt(x, y, radiusCells, maximumKg);
        }
    }
}
