namespace Steppe.Simulation;

public enum HistoryResolution
{
    Recent,
    Daily,
    Monthly
}

public sealed record WorldHistoryPoint(
    double ElapsedHours,
    int Year,
    int DayOfYear,
    double HourOfDay,
    float MeanSurfaceTemperatureC,
    float MeanPrecipitationMmPerHour,
    float MeanSurfaceWaterMm,
    float MeanRootWaterMm,
    float MeanGroundwaterMm,
    float MeanSnowMm,
    float MeanLiveBiomassGm2,
    float MeanDryBiomassGm2,
    float MeanDustGm2,
    float StoredWaterMmPerCell);

public sealed record WorldHistorySnapshot(
    HistoryResolution Resolution,
    double SampleIntervalHours,
    double RetentionHours,
    WorldHistoryPoint[] Points);

public sealed record StateHistoryPoint(
    double ElapsedHours,
    float Value);

public sealed record CellHistorySnapshot(
    int X,
    int Y,
    SimulationLayer State,
    string Unit,
    bool IsPinned,
    HistoryResolution Resolution,
    double SampleIntervalHours,
    double RetentionHours,
    StateHistoryPoint[] Points);

public sealed record PinnedCellSnapshot(
    int X,
    int Y,
    double StartedAtHours);

internal sealed class WorldHistory
{
    public const int MaximumPinnedCells = 16;

    private const double RecentIntervalHours = 6;
    private const int RecentCapacity = 4 * 180;
    private const double DailyIntervalHours = 24;
    private const int DailyCapacity = 365 * 10;
    private const double MonthlyIntervalHours = 24 * 30;
    private const int MonthlyCapacity = 2434;

    private readonly BoundedSeries<WorldHistoryPoint> recentWorld = new(RecentCapacity);
    private readonly BoundedSeries<WorldHistoryPoint> dailyWorld = new(DailyCapacity);
    private readonly BoundedSeries<WorldHistoryPoint> monthlyWorld = new(MonthlyCapacity);
    private readonly Dictionary<int, CellProbeHistory> probes = [];
    private long recentBucket = -1;
    private long dailyBucket = -1;
    private long monthlyBucket = -1;

    public void Initialize(
        double elapsedHours,
        Func<WorldHistoryPoint> captureWorld,
        Func<int, float[]> captureCell)
    {
        var point = captureWorld();
        recentWorld.Add(point);
        dailyWorld.Add(point);
        monthlyWorld.Add(point);
        recentBucket = Bucket(elapsedHours, RecentIntervalHours);
        dailyBucket = Bucket(elapsedHours, DailyIntervalHours);
        monthlyBucket = Bucket(elapsedHours, MonthlyIntervalHours);

        foreach (var probe in probes.Values)
        {
            probe.AddAll(elapsedHours, captureCell(probe.Index));
        }
    }

    public bool RecordIfDue(
        double elapsedHours,
        Func<WorldHistoryPoint> captureWorld,
        Func<int, float[]> captureCell)
    {
        var nextRecent = Bucket(elapsedHours, RecentIntervalHours);
        var nextDaily = Bucket(elapsedHours, DailyIntervalHours);
        var nextMonthly = Bucket(elapsedHours, MonthlyIntervalHours);
        var recordRecent = nextRecent > recentBucket;
        var recordDaily = nextDaily > dailyBucket;
        var recordMonthly = nextMonthly > monthlyBucket;
        if (!recordRecent && !recordDaily && !recordMonthly)
        {
            return false;
        }

        var worldPoint = captureWorld();
        if (recordRecent)
        {
            recentWorld.Add(worldPoint);
            recentBucket = nextRecent;
        }

        if (recordDaily)
        {
            dailyWorld.Add(worldPoint);
            dailyBucket = nextDaily;
        }

        if (recordMonthly)
        {
            monthlyWorld.Add(worldPoint);
            monthlyBucket = nextMonthly;
        }

        foreach (var probe in probes.Values)
        {
            var values = captureCell(probe.Index);
            probe.AddDue(elapsedHours, values, recordRecent, recordDaily, recordMonthly);
        }

        return recordRecent;
    }

    public bool PinCell(int index, int x, int y, double elapsedHours, Func<int, float[]> captureCell)
    {
        if (probes.ContainsKey(index))
        {
            return false;
        }

        if (probes.Count >= MaximumPinnedCells)
        {
            throw new InvalidOperationException($"No more than {MaximumPinnedCells} cells can be pinned.");
        }

        var probe = new CellProbeHistory(index, x, y, elapsedHours);
        probe.AddAll(elapsedHours, captureCell(index));
        probes.Add(index, probe);
        return true;
    }

    public bool UnpinCell(int index) => probes.Remove(index);

    public PinnedCellSnapshot[] PinnedCells() => probes.Values
        .OrderBy(probe => probe.StartedAtHours)
        .Select(probe => new PinnedCellSnapshot(probe.X, probe.Y, probe.StartedAtHours))
        .ToArray();

    public WorldHistorySnapshot CaptureWorld(HistoryResolution resolution)
    {
        var (series, interval, retention) = WorldSeries(resolution);
        return new WorldHistorySnapshot(resolution, interval, retention, series.ToArray());
    }

    public CellHistorySnapshot CaptureCell(
        int index,
        int x,
        int y,
        SimulationLayer layer,
        HistoryResolution resolution)
    {
        if (!probes.TryGetValue(index, out var probe))
        {
            var (_, interval, retention) = EmptyCellSeries(resolution);
            return new CellHistorySnapshot(
                x,
                y,
                layer,
                StateCatalog.Get(layer).Unit,
                false,
                resolution,
                interval,
                retention,
                []);
        }

        var (series, sampleInterval, retentionHours) = probe.Series(resolution);
        var points = series
            .Select(point => new StateHistoryPoint(point.ElapsedHours, point.Values[(int)layer]))
            .ToArray();
        return new CellHistorySnapshot(
            x,
            y,
            layer,
            StateCatalog.Get(layer).Unit,
            true,
            resolution,
            sampleInterval,
            retentionHours,
            points);
    }

    private (BoundedSeries<WorldHistoryPoint> Series, double Interval, double Retention) WorldSeries(
        HistoryResolution resolution) => resolution switch
    {
        HistoryResolution.Recent => (recentWorld, RecentIntervalHours, RecentIntervalHours * RecentCapacity),
        HistoryResolution.Daily => (dailyWorld, DailyIntervalHours, DailyIntervalHours * DailyCapacity),
        HistoryResolution.Monthly => (monthlyWorld, MonthlyIntervalHours, MonthlyIntervalHours * MonthlyCapacity),
        _ => throw new ArgumentOutOfRangeException(nameof(resolution), resolution, null)
    };

    private static (BoundedSeries<CellHistoryPoint>? Series, double Interval, double Retention) EmptyCellSeries(
        HistoryResolution resolution) => resolution switch
    {
        HistoryResolution.Recent => (null, RecentIntervalHours, RecentIntervalHours * RecentCapacity),
        HistoryResolution.Daily => (null, DailyIntervalHours, DailyIntervalHours * DailyCapacity),
        HistoryResolution.Monthly => (null, MonthlyIntervalHours, MonthlyIntervalHours * MonthlyCapacity),
        _ => throw new ArgumentOutOfRangeException(nameof(resolution), resolution, null)
    };

    private static long Bucket(double elapsedHours, double intervalHours) =>
        (long)Math.Floor((elapsedHours + 1e-8) / intervalHours);

    private sealed class CellProbeHistory(int index, int x, int y, double startedAtHours)
    {
        private readonly BoundedSeries<CellHistoryPoint> recent = new(RecentCapacity);
        private readonly BoundedSeries<CellHistoryPoint> daily = new(DailyCapacity);
        private readonly BoundedSeries<CellHistoryPoint> monthly = new(MonthlyCapacity);

        public int Index { get; } = index;
        public int X { get; } = x;
        public int Y { get; } = y;
        public double StartedAtHours { get; } = startedAtHours;

        public void AddAll(double elapsedHours, float[] values)
        {
            recent.Add(new CellHistoryPoint(elapsedHours, values));
            daily.Add(new CellHistoryPoint(elapsedHours, (float[])values.Clone()));
            monthly.Add(new CellHistoryPoint(elapsedHours, (float[])values.Clone()));
        }

        public void AddDue(
            double elapsedHours,
            float[] values,
            bool addRecent,
            bool addDaily,
            bool addMonthly)
        {
            if (addRecent)
            {
                recent.Add(new CellHistoryPoint(elapsedHours, values));
            }

            if (addDaily)
            {
                daily.Add(new CellHistoryPoint(elapsedHours, (float[])values.Clone()));
            }

            if (addMonthly)
            {
                monthly.Add(new CellHistoryPoint(elapsedHours, (float[])values.Clone()));
            }
        }

        public (IReadOnlyList<CellHistoryPoint> Series, double Interval, double Retention) Series(
            HistoryResolution resolution) => resolution switch
        {
            HistoryResolution.Recent => (recent.ToArray(), RecentIntervalHours, RecentIntervalHours * RecentCapacity),
            HistoryResolution.Daily => (daily.ToArray(), DailyIntervalHours, DailyIntervalHours * DailyCapacity),
            HistoryResolution.Monthly => (monthly.ToArray(), MonthlyIntervalHours, MonthlyIntervalHours * MonthlyCapacity),
            _ => throw new ArgumentOutOfRangeException(nameof(resolution), resolution, null)
        };
    }

    private sealed record CellHistoryPoint(double ElapsedHours, float[] Values);

    private sealed class BoundedSeries<T>(int capacity)
    {
        private readonly Queue<T> points = new(capacity);

        public void Add(T point)
        {
            if (points.Count == capacity)
            {
                points.Dequeue();
            }

            points.Enqueue(point);
        }

        public T[] ToArray() => points.ToArray();
    }
}
