using System;
using Steppe.Settings;
using Steppe.Surface;

namespace Steppe.Ecology
{
    public readonly struct SteppeEcologyForcing
    {
        public SteppeEcologyForcing(
            double rainIntensity,
            double surfaceWindSpeed,
            double airTemperatureC,
            double daylight)
        {
            RainIntensity = Clamp01(rainIntensity);
            SurfaceWindSpeed = Math.Max(0.0, surfaceWindSpeed);
            AirTemperatureC = airTemperatureC;
            Daylight = Clamp01(daylight);
        }

        public double RainIntensity { get; }
        public double SurfaceWindSpeed { get; }
        public double AirTemperatureC { get; }
        public double Daylight { get; }

        private static double Clamp01(double value)
        {
            return Math.Max(0.0, Math.Min(1.0, value));
        }
    }

    /// <summary>
    /// Pure fixed-step soil water balance. Rendering never feeds back into this model;
    /// the only inputs are immutable surface potential and canonical atmospheric forcing.
    /// </summary>
    public sealed class SteppeEcologyModel
    {
        private readonly SteppeWorldSettings settings;

        public SteppeEcologyModel(SteppeWorldSettings worldSettings)
        {
            settings = worldSettings != null ? worldSettings : throw new ArgumentNullException(nameof(worldSettings));
        }

        public SteppeEcoCellState CreateInitialState(
            SurfaceSample surface,
            double airTemperatureC,
            double simulationSeconds)
        {
            var rootWater = Clamp01(surface.MoisturePotential * 0.72);
            var warmth = SmoothStep(-4.0, 10.0, airTemperatureC);
            var greenFraction = Clamp01(rootWater * warmth * 1.12);
            var crust = Clamp01(
                (1.0 - rootWater)
                * (0.18 + surface.ClayContent * 0.58)
                * (0.45 + surface.ExposedGround * 0.55));

            return new SteppeEcoCellState(
                0.0,
                rootWater,
                Clamp01(surface.VegetationPotential),
                greenFraction,
                crust,
                0.0,
                0.0,
                0.0,
                Math.Max(0.0, simulationSeconds));
        }

        public SteppeEcoCellState Advance(
            SteppeEcoCellState state,
            SurfaceSample surface,
            SteppeEcologyForcing forcing,
            double durationSeconds)
        {
            var clampedDuration = Math.Max(0.0, durationSeconds);
            if (clampedDuration <= double.Epsilon)
            {
                return state;
            }

            var hours = clampedDuration / 3600.0;
            var clay = Clamp01(surface.ClayContent);
            var retention = Clamp01(surface.WaterRetention);
            var exposed = Clamp01(surface.ExposedGround);
            var biomass = Clamp01(state.Biomass);

            var rainInput = forcing.RainIntensity * settings.EcologyRainStoragePerHour * hours;
            var surfaceWater = Clamp(state.SurfaceWater + rainInput, 0.0, 1.35);
            var rootWater = Clamp01(state.RootWater);

            // Sandy ground accepts water quickly but retains less of it. Clay admits
            // water more slowly while keeping a larger fraction in the root zone.
            var infiltrationRate = Lerp(0.19, 0.085, clay);
            var infiltrationCapacity = hours
                                       * infiltrationRate
                                       * (0.42 + (1.0 - rootWater) * 0.58);
            var infiltrated = Math.Min(surfaceWater, infiltrationCapacity);
            surfaceWater -= infiltrated;
            rootWater += infiltrated * (0.58 + retention * 0.38);

            // Root saturation and surface ponding are bounded reservoirs. Overflow is
            // local runoff/loss for now; rivers are deliberately outside this milestone.
            if (rootWater > 1.0)
            {
                surfaceWater += (rootWater - 1.0) * 0.35;
                rootWater = 1.0;
            }

            var thermalDrying = SmoothStep(-4.0, 34.0, forcing.AirTemperatureC);
            var windDrying = 0.78 + Math.Min(1.0, forcing.SurfaceWindSpeed / 18.0) * 0.58;
            var lightDrying = 0.28 + forcing.Daylight * 0.72;
            var exposureDrying = 0.36 + exposed * 0.64;
            var evaporation = settings.EcologySurfaceEvaporationPerHour
                              * hours
                              * thermalDrying
                              * windDrying
                              * lightDrying
                              * exposureDrying;
            surfaceWater = Math.Max(0.0, surfaceWater - evaporation);

            var rootLoss = settings.EcologyRootWaterLossPerHour
                           * hours
                           * (0.45 + thermalDrying * 0.55)
                           * (0.55 + biomass * 0.45)
                           * (0.8 + windDrying * 0.2)
                           * (1.18 - retention * 0.36);
            rootWater = Math.Max(0.0, rootWater - rootLoss);

            var crust = Clamp01(state.SurfaceCrust);
            crust -= rainInput * (0.58 + (1.0 - clay) * 0.16);
            if (surfaceWater < 0.08 && forcing.RainIntensity < 0.02)
            {
                var dryness = 1.0 - surfaceWater / 0.08;
                crust += hours * 0.0032 * clay * dryness * (0.45 + exposed * 0.55);
            }

            return new SteppeEcoCellState(
                Clamp01(surfaceWater),
                Clamp01(rootWater),
                biomass,
                Clamp01(state.GreenFraction),
                Clamp01(crust),
                Clamp01(state.SnowWater),
                Clamp01(state.SnowCompaction),
                Clamp01(state.FrozenFraction),
                state.LastSimulationSeconds + clampedDuration);
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static double Clamp01(double value)
        {
            return Clamp(value, 0.0, 1.0);
        }

        private static double SmoothStep(double edge0, double edge1, double value)
        {
            var normalized = Clamp01((value - edge0) / (edge1 - edge0));
            return normalized * normalized * (3.0 - 2.0 * normalized);
        }

        private static double Lerp(double from, double to, double value)
        {
            return from + (to - from) * value;
        }
    }
}
