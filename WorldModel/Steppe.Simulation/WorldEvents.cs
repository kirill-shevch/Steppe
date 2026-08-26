namespace Steppe.Simulation;

public enum WorldEventKind
{
    Snowmelt,
    FloodPulse,
    GreenUp,
    Drought,
    DustEpisode,
    Wildfire
}

public sealed record WorldEventDescriptor(
    WorldEventKind Kind,
    string Title,
    string ShortTitle,
    string Description,
    string IndicatorTitle,
    string IndicatorUnit,
    float EnterThreshold,
    float ExitThreshold,
    string Color,
    SimulationLayer FocusLayer);

public static class WorldEventCatalog
{
    private static readonly WorldEventDescriptor[] Descriptors =
    [
        new(
            WorldEventKind.Snowmelt,
            "Снеготаяние",
            "Снеготаяние",
            "Устойчивое уменьшение снежного запаса, возвращающее зимнюю воду в поверхностный и почвенный контуры.",
            "Потеря снежного запаса",
            "мм SWE/сут",
            0.12f,
            0.025f,
            "#b9dce0",
            SimulationLayer.Snow),
        new(
            WorldEventKind.FloodPulse,
            "Паводковый импульс",
            "Паводок",
            "Устойчивый рост среднего запаса поверхностной воды при наличии затопленных ячеек.",
            "Рост поверхностной воды",
            "мм/сут",
            0.12f,
            0.025f,
            "#65c7bb",
            SimulationLayer.SurfaceWater),
        new(
            WorldEventKind.GreenUp,
            "Волна озеленения",
            "Озеленение",
            "Устойчивый положительный прирост живой биомассы в достаточно тёплой степи.",
            "Прирост живой биомассы",
            "г/м²/сут",
            0.08f,
            0.015f,
            "#b9db67",
            SimulationLayer.LiveBiomass),
        new(
            WorldEventKind.Drought,
            "Почвенная засуха",
            "Засуха",
            "Значительная доля степи имеет менее 35 мм воды в корневой зоне.",
            "Доля степи в водном стрессе",
            "доля",
            0.25f,
            0.15f,
            "#e4b861",
            SimulationLayer.RootWater),
        new(
            WorldEventKind.DustEpisode,
            "Пылевой эпизод",
            "Пыль",
            "Устойчивое присутствие поднятого ветром мелкого материала над заметной частью степи.",
            "Средняя пыль в воздухе",
            "г/м²",
            0.010f,
            0.003f,
            "#c78f68",
            SimulationLayer.Dust),
        new(
            WorldEventKind.Wildfire,
            "Степной пожар",
            "Пожар",
            "Устойчивое активное горение сухостоя и подстилки с распространяющимся по ветру фронтом.",
            "Доля горящей степи",
            "доля",
            0.0002f,
            0.00005f,
            "#ef7628",
            SimulationLayer.FireIntensity)
    ];

    private static readonly IReadOnlyDictionary<WorldEventKind, WorldEventDescriptor> ByKind =
        Descriptors.ToDictionary(descriptor => descriptor.Kind);

    public static IReadOnlyList<WorldEventDescriptor> All { get; } = Array.AsReadOnly(Descriptors);

    public static WorldEventDescriptor Get(WorldEventKind kind) =>
        ByKind.TryGetValue(kind, out var descriptor)
            ? descriptor
            : throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
}

public sealed record WorldRegimeMetrics(
    double ElapsedHours,
    int Year,
    int DayOfYear,
    double HourOfDay,
    float MeanSurfaceTemperatureC,
    float MeanPrecipitationMmPerHour,
    float MeanSnowMm,
    float SnowCoveredFraction,
    float MeanSurfaceWaterMm,
    float FloodedFraction,
    float MeanRootWaterMm,
    float WaterStressFraction,
    float MeanLiveBiomassGm2,
    float GreenFraction,
    float MeanDustGm2,
    float DustAffectedFraction,
    float MeanWindSpeedMs,
    float MeanFireIntensity,
    float BurningFraction);

public sealed record WorldRegimeEvent(
    long Id,
    WorldEventKind Kind,
    double StartedAtHours,
    double PeakAtHours,
    double? EndedAtHours,
    float PeakIndicator,
    float CurrentIndicator,
    float PeakSeverity,
    WorldRegimeMetrics PeakMetrics,
    WorldRegimeMetrics CurrentMetrics);

public sealed record WorldRegimeSnapshot(
    double ObservationIntervalHours,
    WorldRegimeMetrics Current,
    WorldRegimeEvent[] Active,
    WorldRegimeEvent[] Recent);

internal sealed class WorldEventLog
{
    public const double ObservationIntervalHours = 6;

    private const int TrendSamples = 5;
    private const int ConfirmationSamples = 2;
    private const int CompletedCapacity = 256;

    private readonly Queue<WorldRegimeMetrics> trend = new(TrendSamples);
    private readonly Queue<WorldRegimeEvent> completed = new(CompletedCapacity);
    private readonly Dictionary<WorldEventKind, EventTracker> trackers = RuntimeCompatibility
        .GetEnumValues<WorldEventKind>()
        .ToDictionary(kind => kind, kind => new EventTracker(WorldEventCatalog.Get(kind)));
    private long nextId = 1;
    private WorldRegimeMetrics? current;

    public void Initialize(WorldRegimeMetrics metrics)
    {
        current = metrics;
        trend.Clear();
        trend.Enqueue(metrics);
    }

    public void Observe(WorldRegimeMetrics metrics)
    {
        current = metrics;
        trend.Enqueue(metrics);
        while (trend.Count > TrendSamples)
        {
            trend.Dequeue();
        }

        var previous = trend.Peek();
        var trendHours = metrics.ElapsedHours - previous.ElapsedHours;
        if (trendHours < 23.9)
        {
            return;
        }

        var dayScale = 24f / (float)trendHours;
        var signals = new Dictionary<WorldEventKind, float>
        {
            [WorldEventKind.Snowmelt] = metrics.SnowCoveredFraction >= 0.005f
                ? Math.Max(0f, previous.MeanSnowMm - metrics.MeanSnowMm) * dayScale
                : 0f,
            [WorldEventKind.FloodPulse] = metrics.FloodedFraction >= 0.005f
                ? Math.Max(0f, metrics.MeanSurfaceWaterMm - previous.MeanSurfaceWaterMm) * dayScale
                : 0f,
            [WorldEventKind.GreenUp] = metrics.MeanSurfaceTemperatureC >= 1f
                ? Math.Max(0f, metrics.MeanLiveBiomassGm2 - previous.MeanLiveBiomassGm2) * dayScale
                : 0f,
            [WorldEventKind.Drought] = metrics.WaterStressFraction,
            [WorldEventKind.DustEpisode] = metrics.DustAffectedFraction >= 0.005f
                ? metrics.MeanDustGm2
                : 0f,
            [WorldEventKind.Wildfire] = metrics.BurningFraction
        };

        foreach (var (kind, indicator) in signals)
        {
            var finished = trackers[kind].Observe(metrics, indicator, ref nextId);
            if (finished is null)
            {
                continue;
            }

            if (completed.Count == CompletedCapacity)
            {
                completed.Dequeue();
            }

            completed.Enqueue(finished);
        }
    }

    public WorldRegimeSnapshot Capture(int recentLimit)
    {
        if (current is null)
        {
            throw new InvalidOperationException("Event log was not initialized.");
        }

        var active = trackers.Values
            .Select(tracker => tracker.CaptureActive())
            .Where(item => item is not null)
            .Cast<WorldRegimeEvent>()
            .OrderBy(item => item.StartedAtHours)
            .ToArray();
        var recent = completed
            .Reverse()
            .Take(Math.Clamp(recentLimit, 0, CompletedCapacity))
            .ToArray();
        return new WorldRegimeSnapshot(ObservationIntervalHours, current, active, recent);
    }

    private sealed class EventTracker(WorldEventDescriptor descriptor)
    {
        private MutableEvent? active;
        private Candidate? candidate;
        private int enterSamples;
        private int exitSamples;

        public WorldRegimeEvent? Observe(WorldRegimeMetrics metrics, float indicator, ref long nextId)
        {
            if (active is null)
            {
                if (indicator < descriptor.EnterThreshold)
                {
                    candidate = null;
                    enterSamples = 0;
                    return null;
                }

                enterSamples++;
                candidate ??= new Candidate(metrics, indicator);
                candidate.Update(metrics, indicator);
                if (enterSamples < ConfirmationSamples)
                {
                    return null;
                }

                active = new MutableEvent(nextId++, descriptor, candidate);
                candidate = null;
                enterSamples = 0;
                return null;
            }

            active.Update(metrics, indicator);
            if (indicator > descriptor.ExitThreshold)
            {
                exitSamples = 0;
                return null;
            }

            exitSamples++;
            if (exitSamples < ConfirmationSamples)
            {
                return null;
            }

            var finished = active.Capture(metrics.ElapsedHours);
            active = null;
            exitSamples = 0;
            return finished;
        }

        public WorldRegimeEvent? CaptureActive() => active?.Capture(null);
    }

    private sealed class Candidate(WorldRegimeMetrics started, float indicator)
    {
        public WorldRegimeMetrics Started { get; } = started;
        public WorldRegimeMetrics Peak { get; private set; } = started;
        public WorldRegimeMetrics Current { get; private set; } = started;
        public float PeakIndicator { get; private set; } = indicator;
        public float CurrentIndicator { get; private set; } = indicator;

        public void Update(WorldRegimeMetrics metrics, float value)
        {
            Current = metrics;
            CurrentIndicator = value;
            if (value <= PeakIndicator)
            {
                return;
            }

            Peak = metrics;
            PeakIndicator = value;
        }
    }

    private sealed class MutableEvent(
        long id,
        WorldEventDescriptor descriptor,
        Candidate candidate)
    {
        private WorldRegimeMetrics peak = candidate.Peak;
        private WorldRegimeMetrics current = candidate.Current;
        private float peakIndicator = candidate.PeakIndicator;
        private float currentIndicator = candidate.CurrentIndicator;

        public void Update(WorldRegimeMetrics metrics, float indicator)
        {
            current = metrics;
            currentIndicator = indicator;
            if (indicator <= peakIndicator)
            {
                return;
            }

            peak = metrics;
            peakIndicator = indicator;
        }

        public WorldRegimeEvent Capture(double? endedAtHours) => new(
            id,
            descriptor.Kind,
            candidate.Started.ElapsedHours,
            peak.ElapsedHours,
            endedAtHours,
            peakIndicator,
            currentIndicator,
            Math.Clamp(peakIndicator / Math.Max(1e-6f, descriptor.EnterThreshold * 3f), 0f, 1f),
            peak,
            current);
    }
}
