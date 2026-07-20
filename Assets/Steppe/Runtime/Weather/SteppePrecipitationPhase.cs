using System;

namespace Steppe.Weather
{
    public static class SteppePrecipitationPhase
    {
        public static double SnowFraction(double airTemperatureC)
        {
            return 1.0 - SmoothStep(-1.5, 2.0, airTemperatureC);
        }

        public static double RainFraction(double airTemperatureC)
        {
            return 1.0 - SnowFraction(airTemperatureC);
        }

        private static double SmoothStep(double edge0, double edge1, double value)
        {
            var normalized = Math.Max(0.0, Math.Min(1.0, (value - edge0) / (edge1 - edge0)));
            return normalized * normalized * (3.0 - 2.0 * normalized);
        }
    }
}
