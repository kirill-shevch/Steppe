namespace Steppe.Simulation;

public sealed class WorldClock
{
    public WorldClock(double elapsedHours = 0) => ElapsedHours = Math.Max(0, elapsedHours);

    public double ElapsedHours { get; internal set; }
    public int Year => (int)Math.Floor(ElapsedHours / (365d * 24d)) + 1;
    public int DayOfYear => (int)Math.Floor(ElapsedHours / 24d) % 365 + 1;
    public double HourOfDay => ElapsedHours % 24d;
    public string Season => DayOfYear switch
    {
        >= 80 and < 172 => "spring",
        >= 172 and < 266 => "summer",
        >= 266 and < 355 => "autumn",
        _ => "winter"
    };

    public DateTimeOffset AsDateTimeOffset()
    {
        var wholeHours = Math.Floor(ElapsedHours);
        var minutes = (ElapsedHours - wholeHours) * 60d;
        return new DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero)
            .AddHours(wholeHours)
            .AddMinutes(minutes);
    }

    internal void Advance(double hours) => ElapsedHours += hours;
}
