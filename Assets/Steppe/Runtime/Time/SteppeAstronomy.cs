using System;
using UnityEngine;

namespace Steppe.Time
{
    public readonly struct SolarState
    {
        public SolarState(Vector3 direction, double elevationDegrees, double declinationDegrees, double daylight)
        {
            Direction = direction;
            ElevationDegrees = elevationDegrees;
            DeclinationDegrees = declinationDegrees;
            Daylight = daylight;
        }

        public Vector3 Direction { get; }
        public double ElevationDegrees { get; }
        public double DeclinationDegrees { get; }
        public double Daylight { get; }
    }

    public static class SteppeAstronomy
    {
        public static SolarState Evaluate(SteppeTimeSnapshot time, double latitudeDegrees)
        {
            var latitude = latitudeDegrees * Mathf.Deg2Rad;
            var declinationDegrees = -23.44 * Math.Cos(Math.PI * 2.0 * (time.YearFraction + 10.0 / 365.0));
            var declination = declinationDegrees * Mathf.Deg2Rad;
            var hourAngle = (time.Hour - 12.0) * 15.0 * Mathf.Deg2Rad;

            var east = -Math.Cos(declination) * Math.Sin(hourAngle);
            var north = Math.Cos(latitude) * Math.Sin(declination)
                        - Math.Sin(latitude) * Math.Cos(declination) * Math.Cos(hourAngle);
            var up = Math.Sin(latitude) * Math.Sin(declination)
                     + Math.Cos(latitude) * Math.Cos(declination) * Math.Cos(hourAngle);
            var direction = new Vector3((float)east, (float)up, (float)north).normalized;
            var elevation = Math.Asin(Math.Max(-1.0, Math.Min(1.0, up))) * Mathf.Rad2Deg;
            var daylight = SmoothStep(-6.0, 3.0, elevation);

            return new SolarState(direction, elevation, declinationDegrees, daylight);
        }

        private static double SmoothStep(double edge0, double edge1, double value)
        {
            var t = Math.Max(0.0, Math.Min(1.0, (value - edge0) / (edge1 - edge0)));
            return t * t * (3.0 - 2.0 * t);
        }
    }
}
