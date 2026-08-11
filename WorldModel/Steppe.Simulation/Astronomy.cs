namespace Steppe.Simulation;

public readonly record struct SolarState(
    double DeclinationRadians,
    double ElevationRadians,
    double AzimuthRadians,
    double TopOfAtmosphereWm2,
    double DayLengthHours);

public static class Astronomy
{
    public static SolarState Calculate(WorldConfig config, WorldClock clock)
    {
        var latitude = DegreesToRadians(config.LatitudeDegrees);
        var tilt = DegreesToRadians(config.AxialTiltDegrees);
        var orbitalAngle = 2d * Math.PI * (clock.DayOfYear - 80d) / 365d;
        var declination = Math.Asin(Math.Sin(tilt) * Math.Sin(orbitalAngle));
        var hourAngle = Math.PI / 12d * (clock.HourOfDay - 12d);
        var sinElevation = Math.Sin(latitude) * Math.Sin(declination)
            + Math.Cos(latitude) * Math.Cos(declination) * Math.Cos(hourAngle);
        var elevation = Math.Asin(Math.Clamp(sinElevation, -1d, 1d));
        var azimuth = Math.Atan2(
            -Math.Sin(hourAngle) * Math.Cos(declination),
            Math.Sin(declination) * Math.Cos(latitude)
                - Math.Cos(declination) * Math.Sin(latitude) * Math.Cos(hourAngle));
        var sunsetArgument = -Math.Tan(latitude) * Math.Tan(declination);
        var sunsetAngle = Math.Acos(Math.Clamp(sunsetArgument, -1d, 1d));
        var dayLength = 24d * sunsetAngle / Math.PI;
        var radiation = elevation > 0 ? 1361d * Math.Sin(elevation) : 0d;
        return new SolarState(declination, elevation, azimuth, radiation, dayLength);
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;
}
