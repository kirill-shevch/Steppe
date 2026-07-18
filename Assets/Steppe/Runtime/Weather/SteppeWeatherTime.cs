using System;
using Steppe.Settings;

namespace Steppe.Weather
{
    /// <summary>
    /// Converts the authoritative world clock into the slower atmosphere timeline.
    /// Weather time is derived rather than accumulated, so pause, acceleration,
    /// save/load, and deterministic ecology catch-up all observe the same timestamp.
    /// </summary>
    public static class SteppeWeatherTime
    {
        public static double FromSimulationSeconds(
            SteppeWorldSettings settings,
            double elapsedSimulationSeconds)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var simulationRate = Math.Max(1.0, settings.SimulationSecondsPerRealSecond);
            var weatherRate = Math.Max(0.0, settings.WeatherSecondsPerRealSecond);
            return Math.Max(0.0, elapsedSimulationSeconds) * weatherRate / simulationRate;
        }

        public static double ToSimulationSeconds(
            SteppeWorldSettings settings,
            double weatherSeconds)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var weatherRate = settings.WeatherSecondsPerRealSecond;
            if (weatherRate <= double.Epsilon)
            {
                return 0.0;
            }

            return Math.Max(0.0, weatherSeconds)
                   * Math.Max(1.0, settings.SimulationSecondsPerRealSecond)
                   / weatherRate;
        }
    }
}
