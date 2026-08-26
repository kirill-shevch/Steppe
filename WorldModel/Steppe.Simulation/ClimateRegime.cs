namespace Steppe.Simulation;

public sealed record AnnualClimateRegime(
    int Year,
    float TemperatureAnomalyC,
    float MoistureMultiplier,
    float WindSpeedMultiplier,
    float StormFrequencyMultiplier,
    float StormIntensityMultiplier,
    float SeasonPhaseShiftDays,
    float WinterTemperatureOffsetC,
    float SpringTemperatureOffsetC,
    float SummerTemperatureOffsetC,
    float AutumnTemperatureOffsetC,
    float WinterMoistureMultiplier,
    float SpringMoistureMultiplier,
    float SummerMoistureMultiplier,
    float AutumnMoistureMultiplier,
    bool SevereHeat,
    bool SevereCold,
    bool SevereDrought,
    bool ExtremeWet,
    bool SevereWind);

public sealed record ClimateForcingSnapshot(
    int Year,
    float DayOfYear,
    float TemperatureOffsetC,
    float MoistureMultiplier,
    float WindSpeedMultiplier,
    float WindAnomalyXMs,
    float WindAnomalyYMs,
    float StormFrequencyMultiplier,
    float StormIntensityMultiplier,
    float SynopticStormPulse,
    float SeasonPhaseShiftDays,
    bool Heatwave,
    bool ColdSnap,
    bool RainBurst,
    bool WindStorm,
    AnnualClimateRegime AnnualRegime);

/// <summary>
/// Reproducible climate forcing generated only from the world seed and time.
/// It changes atmospheric boundary conditions; it never edits ecological
/// reservoirs directly, so water and material ledgers remain physical.
/// </summary>
public static class ClimateRegimeModel
{
    private const float DaysPerYear = 365f;
    private const float TwoPi = MathF.PI * 2f;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<ClimateKey, AnnualClimateRegime>
        AnnualCache = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<ClimateKey, double>
        StormCycleCache = new();

    public static AnnualClimateRegime GetAnnualRegime(WorldConfig config, int year)
    {
        RuntimeCompatibility.ThrowIfNull(config, nameof(config));
        if (year < 1) throw new ArgumentOutOfRangeException(nameof(year));

        var key = Key(config, year);
        return AnnualCache.GetOrAdd(key, _ => CreateAnnualRegime(config, year));
    }

    private static AnnualClimateRegime CreateAnnualRegime(WorldConfig config, int year)
    {

        var strength = config.ClimateVariability;
        if (strength <= 0f)
        {
            return NormalRegime(year);
        }

        var temperature = Persistent(config, year, 11003) * 1.55f
            + Symmetric(config, year, 11027) * 0.85f;
        var temperatureExtreme = DeterministicNoise.Hash01(year, 11041, config.Seed);
        if (temperatureExtreme < 0.10f)
        {
            var sign = DeterministicNoise.Hash01(year, 11047, config.Seed) < 0.5f ? -1f : 1f;
            temperature += sign * (2.7f + DeterministicNoise.Hash01(year, 11059, config.Seed) * 1.9f);
        }
        temperature = Math.Clamp(temperature * strength, -6.5f, 6.5f);

        var moisture = MathF.Exp(
            Persistent(config, year, 12007) * 0.24f
            + Symmetric(config, year, 12011) * 0.12f);
        var moistureExtreme = DeterministicNoise.Hash01(year, 12037, config.Seed);
        if (moistureExtreme < 0.07f)
        {
            moisture = 0.45f + DeterministicNoise.Hash01(year, 12041, config.Seed) * 0.18f;
        }
        else if (moistureExtreme < 0.14f)
        {
            moisture = 1.50f + DeterministicNoise.Hash01(year, 12049, config.Seed) * 0.38f;
        }
        moisture = ScaleMultiplier(moisture, strength, 0.38f, 2.05f);

        var wind = 1f
            + Persistent(config, year, 13001) * 0.17f
            + Symmetric(config, year, 13007) * 0.10f;
        if (DeterministicNoise.Hash01(year, 13033, config.Seed) < 0.11f)
        {
            wind += 0.28f + DeterministicNoise.Hash01(year, 13037, config.Seed) * 0.30f;
        }
        wind = ScaleMultiplier(wind, strength, 0.58f, 1.75f);

        var stormFrequency = 1f
            + Persistent(config, year, 14009) * 0.22f
            + Symmetric(config, year, 14011) * 0.14f
            + (moisture - 1f) * 0.28f;
        stormFrequency = ScaleMultiplier(stormFrequency, strength, 0.48f, 1.75f);
        var stormIntensity = 1f
            + Persistent(config, year, 14029) * 0.20f
            + Symmetric(config, year, 14033) * 0.15f
            + Math.Max(0f, moisture - 1f) * 0.32f;
        stormIntensity = ScaleMultiplier(stormIntensity, strength, 0.55f, 1.85f);

        var phaseShift = Math.Clamp(
            (Persistent(config, year, 15013) * 10f + Symmetric(config, year, 15017) * 5f) * strength,
            -21f,
            21f);

        float SeasonalTemperature(int channel)
        {
            var ordinary = Persistent(config, year, channel) * 1.25f
                + Symmetric(config, year, channel + 17) * 0.65f;
            return Math.Clamp(ordinary * strength, -3.2f, 3.2f);
        }

        float SeasonalMoisture(int channel)
        {
            var value = MathF.Exp(
                Persistent(config, year, channel) * 0.18f
                + Symmetric(config, year, channel + 19) * 0.10f);
            return ScaleMultiplier(value, strength, 0.67f, 1.48f);
        }

        var winterTemperature = SeasonalTemperature(16001);
        var springTemperature = SeasonalTemperature(16033);
        var summerTemperature = SeasonalTemperature(16067);
        var autumnTemperature = SeasonalTemperature(16091);
        if (temperatureExtreme < 0.10f)
        {
            if (temperature > 0f) summerTemperature += 1.4f * strength;
            else winterTemperature -= 1.4f * strength;
        }

        winterTemperature = Math.Clamp(winterTemperature, -4.5f, 4.5f);
        springTemperature = Math.Clamp(springTemperature, -4.5f, 4.5f);
        summerTemperature = Math.Clamp(summerTemperature, -4.5f, 4.5f);
        autumnTemperature = Math.Clamp(autumnTemperature, -4.5f, 4.5f);

        var winterMoisture = SeasonalMoisture(17011);
        var springMoisture = SeasonalMoisture(17041);
        var summerMoisture = SeasonalMoisture(17077);
        var autumnMoisture = SeasonalMoisture(17107);
        if (moistureExtreme < 0.07f) summerMoisture *= 1f - 0.28f * strength;
        if (moistureExtreme is >= 0.07f and < 0.14f) springMoisture *= 1f + 0.20f * strength;

        winterMoisture = Math.Clamp(winterMoisture, 0.58f, 1.65f);
        springMoisture = Math.Clamp(springMoisture, 0.58f, 1.65f);
        summerMoisture = Math.Clamp(summerMoisture, 0.58f, 1.65f);
        autumnMoisture = Math.Clamp(autumnMoisture, 0.58f, 1.65f);

        return new AnnualClimateRegime(
            year,
            temperature,
            moisture,
            wind,
            stormFrequency,
            stormIntensity,
            phaseShift,
            winterTemperature,
            springTemperature,
            summerTemperature,
            autumnTemperature,
            winterMoisture,
            springMoisture,
            summerMoisture,
            autumnMoisture,
            temperature + summerTemperature >= 4.2f,
            temperature + winterTemperature <= -4.2f,
            moisture * summerMoisture <= 0.66f,
            moisture * springMoisture >= 1.48f,
            wind >= 1.34f);
    }

    public static ClimateForcingSnapshot GetForcing(
        WorldConfig config,
        int year,
        float dayOfYear)
    {
        RuntimeCompatibility.ThrowIfNull(config, nameof(config));
        if (year < 1) throw new ArgumentOutOfRangeException(nameof(year));
        if (!RuntimeCompatibility.IsFinite(dayOfYear) || dayOfYear < 1f || dayOfYear >= 366f)
        {
            throw new ArgumentOutOfRangeException(nameof(dayOfYear));
        }

        var current = GetAnnualRegime(config, year);
        var previous = GetAnnualRegime(config, Math.Max(1, year - 1));
        var next = GetAnnualRegime(config, year + 1);

        float Annual(Func<AnnualClimateRegime, float> selector) =>
            BlendYearEdges(dayOfYear, selector(previous), selector(current), selector(next));

        var annualTemperature = Annual(item => item.TemperatureAnomalyC);
        var seasonalTemperature = BlendYearEdges(
            dayOfYear,
            SeasonalTemperature(previous, dayOfYear),
            SeasonalTemperature(current, dayOfYear),
            SeasonalTemperature(next, dayOfYear));
        var moisture = BlendYearEdges(
            dayOfYear,
            previous.MoistureMultiplier * SeasonalMoisture(previous, dayOfYear),
            current.MoistureMultiplier * SeasonalMoisture(current, dayOfYear),
            next.MoistureMultiplier * SeasonalMoisture(next, dayOfYear));
        var wind = Annual(item => item.WindSpeedMultiplier);
        var stormFrequency = Annual(item => item.StormFrequencyMultiplier);
        var stormIntensity = Annual(item => item.StormIntensityMultiplier);
        var phaseShift = Annual(item => item.SeasonPhaseShiftDays);

        var heatPulse = EventPulse(config, year, dayOfYear, 18101, 155f, 245f, 0.42f, 6f, 13f);
        var coldPulse = ColdEventPulse(config, year, dayOfYear);
        var temperatureOffset = annualTemperature + seasonalTemperature
            + heatPulse.Pulse * heatPulse.Magnitude
            - coldPulse.Pulse * coldPulse.Magnitude;

        var rainBurst = 0f;
        for (var eventIndex = 0; eventIndex < 4; eventIndex++)
        {
            var pulse = EventPulse(
                config,
                year,
                dayOfYear,
                19001 + eventIndex * 101,
                25f,
                340f,
                Math.Clamp(0.30f * current.StormFrequencyMultiplier, 0.12f, 0.68f),
                1.5f,
                4.5f);
            rainBurst += pulse.Pulse * pulse.Magnitude;
        }

        var windPulse = 0f;
        var absoluteDay = (year - 1d) * DaysPerYear + dayOfYear;
        var primaryPeriod = 9.5f + DeterministicNoise.Hash01(20001, 17, config.Seed) * 5.5f;
        var secondaryPeriod = 4.5f + DeterministicNoise.Hash01(20003, 29, config.Seed) * 3.5f;
        var primaryPhase = TwoPi * (float)(absoluteDay / primaryPeriod)
            + DeterministicNoise.Hash01(20005, 41, config.Seed) * TwoPi;
        var secondaryPhase = TwoPi * (float)(absoluteDay / secondaryPeriod)
            + DeterministicNoise.Hash01(20007, 53, config.Seed) * TwoPi;
        var windX = (MathF.Sin(primaryPhase) * 6.2f + MathF.Sin(secondaryPhase) * 1.35f)
            * config.ClimateVariability;
        var windY = (MathF.Cos(primaryPhase * 0.83f + 0.7f) * 4.1f
                + MathF.Sin(secondaryPhase * 1.17f) * 1.25f)
            * config.ClimateVariability;
        for (var eventIndex = 0; eventIndex < 3; eventIndex++)
        {
            var channel = 20011 + eventIndex * 103;
            var pulse = EventPulse(config, year, dayOfYear, channel, 1f, 365f, 0.48f, 2f, 6f);
            if (pulse.Pulse <= 0f) continue;
            var direction = DeterministicNoise.Hash01(year, channel + 31, config.Seed) * MathF.PI * 2f;
            var speed = pulse.Pulse * (3.2f
                + DeterministicNoise.Hash01(year, channel + 37, config.Seed) * 3.8f)
                * config.ClimateVariability;
            windPulse = Math.Max(windPulse, pulse.Pulse);
            windX += MathF.Cos(direction) * speed;
            windY += MathF.Sin(direction) * speed;
        }

        var rainBurstMultiplier = 1f + rainBurst;
        var synopticStormPulse = ContinuousStormPulse(config, year, dayOfYear, current);
        return new ClimateForcingSnapshot(
            year,
            dayOfYear,
            temperatureOffset,
            Math.Clamp(moisture, 0.30f, 2.30f),
            Math.Clamp(wind * (1f + windPulse * 0.42f * config.ClimateVariability), 0.50f, 2.20f),
            windX,
            windY,
            Math.Clamp(stormFrequency, 0.40f, 1.90f),
            Math.Clamp(stormIntensity * rainBurstMultiplier, 0.45f, 5.5f),
            synopticStormPulse,
            phaseShift,
            heatPulse.Pulse > 0.20f,
            coldPulse.Pulse > 0.20f,
            rainBurst > 0.20f,
            windPulse > 0.20f,
            current);
    }

    internal static ClimateForcingSnapshot GetForcing(WorldConfig config, WorldClock clock) =>
        GetForcing(config, clock.Year, clock.DayOfYear + (float)(clock.HourOfDay / 24d));

    private static AnnualClimateRegime NormalRegime(int year) => new(
        year,
        0f,
        1f,
        1f,
        1f,
        1f,
        0f,
        0f,
        0f,
        0f,
        0f,
        1f,
        1f,
        1f,
        1f,
        false,
        false,
        false,
        false,
        false);

    private static float Persistent(WorldConfig config, int year, int channel) =>
        DeterministicNoise.Value(year * 0.31f, channel * 0.001f, config.Seed + channel);

    private static float Symmetric(WorldConfig config, int year, int channel) =>
        DeterministicNoise.Hash01(year, channel, config.Seed + channel * 3) * 2f - 1f;

    private static float ScaleMultiplier(float target, float strength, float minimum, float maximum) =>
        Math.Clamp(1f + (target - 1f) * strength, minimum, maximum);

    private static float SeasonalTemperature(AnnualClimateRegime regime, float day) =>
        CircularSeasonLerp(
            day,
            regime.WinterTemperatureOffsetC,
            regime.SpringTemperatureOffsetC,
            regime.SummerTemperatureOffsetC,
            regime.AutumnTemperatureOffsetC);

    private static float SeasonalMoisture(AnnualClimateRegime regime, float day) =>
        CircularSeasonLerp(
            day,
            regime.WinterMoistureMultiplier,
            regime.SpringMoistureMultiplier,
            regime.SummerMoistureMultiplier,
            regime.AutumnMoistureMultiplier);

    private static float CircularSeasonLerp(float day, float winter, float spring, float summer, float autumn)
    {
        if (day is >= 20f and < 126f) return SmoothLerp(winter, spring, (day - 20f) / 106f);
        if (day is >= 126f and < 219f) return SmoothLerp(spring, summer, (day - 126f) / 93f);
        if (day is >= 219f and < 310f) return SmoothLerp(summer, autumn, (day - 219f) / 91f);
        var wrappedDay = day < 20f ? day + DaysPerYear : day;
        return SmoothLerp(autumn, winter, (wrappedDay - 310f) / 75f);
    }

    private static float BlendYearEdges(float day, float previous, float current, float next)
    {
        const float transitionDays = 20f;
        if (day < 1f + transitionDays)
        {
            return SmoothLerp(previous, current, (day - 1f) / transitionDays);
        }
        if (day > DaysPerYear - transitionDays)
        {
            return SmoothLerp(current, next, (day - (DaysPerYear - transitionDays)) / transitionDays);
        }
        return current;
    }

    private static (float Pulse, float Magnitude) EventPulse(
        WorldConfig config,
        int year,
        float day,
        int channel,
        float firstDay,
        float lastDay,
        float probability,
        float minimumHalfWidth,
        float maximumHalfWidth)
    {
        if (config.ClimateVariability <= 0f
            || DeterministicNoise.Hash01(year, channel, config.Seed) >= probability)
        {
            return (0f, 0f);
        }

        var center = firstDay
            + DeterministicNoise.Hash01(year, channel + 1, config.Seed) * (lastDay - firstDay);
        var halfWidth = minimumHalfWidth
            + DeterministicNoise.Hash01(year, channel + 3, config.Seed) * (maximumHalfWidth - minimumHalfWidth);
        var distance = CircularDistance(day, center);
        var pulse = distance >= halfWidth
            ? 0f
            : 0.5f + 0.5f * MathF.Cos(MathF.PI * distance / halfWidth);
        var magnitude = 3.8f + DeterministicNoise.Hash01(year, channel + 7, config.Seed) * 4.4f;
        return (pulse, magnitude * config.ClimateVariability);
    }

    private static (float Pulse, float Magnitude) ColdEventPulse(
        WorldConfig config,
        int year,
        float day)
    {
        const int channel = 18203;
        if (config.ClimateVariability <= 0f
            || DeterministicNoise.Hash01(year, channel, config.Seed) >= 0.40f)
        {
            return (0f, 0f);
        }

        var firstHalf = DeterministicNoise.Hash01(year, channel + 1, config.Seed) < 0.5f;
        var center = firstHalf
            ? 8f + DeterministicNoise.Hash01(year, channel + 3, config.Seed) * 92f
            : 300f + DeterministicNoise.Hash01(year, channel + 3, config.Seed) * 60f;
        var halfWidth = 5f + DeterministicNoise.Hash01(year, channel + 5, config.Seed) * 8f;
        var distance = CircularDistance(day, center);
        var pulse = distance >= halfWidth
            ? 0f
            : 0.5f + 0.5f * MathF.Cos(MathF.PI * distance / halfWidth);
        var magnitude = 4.5f + DeterministicNoise.Hash01(year, channel + 7, config.Seed) * 4.5f;
        return (pulse, magnitude * config.ClimateVariability);
    }

    private static float CircularDistance(float a, float b)
    {
        var direct = MathF.Abs(a - b);
        return Math.Min(direct, DaysPerYear - direct);
    }

    private static float ContinuousStormPulse(
        WorldConfig config,
        int year,
        float dayOfYear,
        AnnualClimateRegime current)
    {
        var key = Key(config, year);
        var completedCycles = StormCycleCache.GetOrAdd(key, _ =>
        {
            double cycles = 0;
            for (var completedYear = 1; completedYear < year; completedYear++)
            {
                cycles += DaysPerYear / 8.5d
                    * GetAnnualRegime(config, completedYear).StormFrequencyMultiplier;
            }
            return cycles;
        });
        completedCycles += (dayOfYear - 1f) / 8.5d * current.StormFrequencyMultiplier;
        var phase = (float)(completedCycles - Math.Floor(completedCycles));
        return MathF.Pow(MathF.Max(0f, MathF.Sin(MathF.PI * 2f * phase)), 10f);
    }

    private static ClimateKey Key(WorldConfig config, int year) =>
        new(config.Seed, BitConverter.SingleToInt32Bits(config.ClimateVariability), year);

    private static float SmoothLerp(float a, float b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        t = t * t * (3f - 2f * t);
        return a + (b - a) * t;
    }

    private readonly record struct ClimateKey(int Seed, int VariabilityBits, int Year);
}
