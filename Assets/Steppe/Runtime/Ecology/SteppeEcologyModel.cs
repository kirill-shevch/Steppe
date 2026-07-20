using System;
using Steppe.Settings;
using Steppe.Surface;
using Steppe.Weather;

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
            var frozenFraction = 1.0 - SmoothStep(-4.0, 3.0, airTemperatureC);
            var greenFraction = Clamp01(rootWater * warmth * (1.0 - frozenFraction) * 1.12);
            var initialBiomassFraction = 0.56 + warmth * 0.44;
            var crust = Clamp01(
                (1.0 - rootWater)
                * (0.18 + surface.ClayContent * 0.58)
                * (0.45 + surface.ExposedGround * 0.55));

            return new SteppeEcoCellState(
                0.0,
                rootWater,
                Clamp01(surface.VegetationPotential * initialBiomassFraction),
                greenFraction,
                crust,
                0.0,
                0.0,
                frozenFraction,
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
            var capacity = Clamp01(surface.VegetationPotential);
            var biomass = Clamp(state.Biomass, 0.0, capacity);
            var frozenFraction = Clamp01(state.FrozenFraction);
            var snowWater = Clamp01(state.SnowWater);
            var snowCompaction = Clamp01(state.SnowCompaction);

            var snowFraction = SteppePrecipitationPhase.SnowFraction(forcing.AirTemperatureC);
            var rainInput = forcing.RainIntensity
                            * (1.0 - snowFraction)
                            * settings.EcologyRainStoragePerHour
                            * hours;
            var snowfallInput = forcing.RainIntensity
                                * snowFraction
                                * settings.SnowStoragePerHour
                                * hours;
            var windScouring = SmoothStep(7.0, 20.0, forcing.SurfaceWindSpeed)
                               * (0.72 + exposed * 0.28);
            var snowCapture = Clamp01(
                0.58
                + capacity * 0.38
                - windScouring * (0.18 + exposed * 0.22));
            var capturedSnow = snowfallInput * snowCapture;
            if (capturedSnow > 0.0)
            {
                var previousSnow = snowWater;
                snowWater = Clamp01(snowWater + capturedSnow);
                snowCompaction *= previousSnow / Math.Max(0.001, previousSnow + capturedSnow);
            }

            var surfaceWater = Clamp(state.SurfaceWater + rainInput, 0.0, 1.35);
            var rootWater = Clamp01(state.RootWater);

            var meltWarmth = SmoothStep(-1.0, 8.0, forcing.AirTemperatureC);
            var melt = Math.Min(
                snowWater,
                settings.SnowMeltPerHour
                * hours
                * meltWarmth
                * (0.28 + forcing.Daylight * 0.72)
                * (1.0 - snowCompaction * 0.24));
            snowWater -= melt;
            surfaceWater += melt * 0.94;

            var sublimation = Math.Min(
                snowWater,
                snowWater
                * hours
                * 0.0014
                * (0.35 + Math.Min(1.0, forcing.SurfaceWindSpeed / 18.0) * 0.65)
                * (0.55 + forcing.Daylight * 0.45));
            snowWater -= sublimation;

            // Sandy ground accepts water quickly but retains less of it. Clay admits
            // water more slowly while keeping a larger fraction in the root zone.
            var infiltrationRate = Lerp(0.19, 0.085, clay);
            var infiltrationCapacity = hours
                                       * infiltrationRate
                                       * (0.42 + (1.0 - rootWater) * 0.58)
                                       * (1.0 - frozenFraction * 0.86);
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
                           * (1.18 - retention * 0.36)
                           * (1.0 - frozenFraction * 0.82);
            rootWater = Math.Max(0.0, rootWater - rootLoss);

            var crust = Clamp01(state.SurfaceCrust);
            crust -= rainInput * (0.58 + (1.0 - clay) * 0.16);
            if (surfaceWater < 0.08 && forcing.RainIntensity < 0.02 && snowWater < 0.02)
            {
                var dryness = 1.0 - surfaceWater / 0.08;
                crust += hours * 0.0032 * clay * dryness * (0.45 + exposed * 0.55);
            }

            var snowCoverage = SmoothStep(0.015, 0.22, snowWater);
            if (snowWater > 0.01)
            {
                snowCompaction += hours
                                  * 0.008
                                  * snowCoverage
                                  * (0.35 + Math.Min(1.0, forcing.SurfaceWindSpeed / 18.0) * 0.45
                                     + meltWarmth * 0.20);
            }
            else
            {
                snowCompaction = MoveTowards(snowCompaction, 0.0, hours * 0.08);
            }
            snowCompaction = Clamp01(snowCompaction);

            var freezeTarget = 1.0 - SmoothStep(-4.0, 3.0, forcing.AirTemperatureC);
            var freezeRate = freezeTarget > frozenFraction
                ? settings.SoilFreezePerHour
                : settings.SoilThawPerHour;
            frozenFraction = MoveTowards(
                frozenFraction,
                freezeTarget,
                freezeRate * hours * (1.0 - snowCoverage * 0.35));

            // Phenology follows root-zone water and temperature rather than rain itself.
            // Green tissue responds within hours to days. Curing transfers the visible
            // stand from live to dry material without deleting it; only later decay and
            // lodging remove that dry structure and open space for spring recovery.
            var days = hours / 24.0;
            var growthWarmth = SmoothStep(2.0, 14.0, forcing.AirTemperatureC);
            var heatStress = SmoothStep(29.0, 41.0, forcing.AirTemperatureC);
            var coldStress = 1.0 - SmoothStep(-7.0, 4.0, forcing.AirTemperatureC);
            var liquidRootWater = rootWater * (1.0 - frozenFraction);
            var waterAccess = SmoothStep(0.08, 0.58, liquidRootWater);
            var greenTarget = Clamp01(
                waterAccess
                * growthWarmth
                * (1.0 - heatStress * 0.72)
                * (1.0 - coldStress));
            var greenFraction = Clamp01(state.GreenFraction);
            var greenRate = greenTarget > greenFraction
                ? settings.VegetationGreeningPerDay
                : settings.VegetationCuringPerDay;
            greenFraction = MoveTowards(
                greenFraction,
                greenTarget,
                greenRate * days);

            var relativeBiomass = capacity > 0.001 ? biomass / capacity : 0.0;
            var growthSuitability = growthWarmth
                                    * (1.0 - heatStress)
                                    * waterAccess
                                    * (0.35 + Clamp01(surface.Fertility) * 0.65)
                                    * (0.35 + greenFraction * 0.65);
            var growth = capacity
                         * settings.VegetationBiomassGrowthPerDay
                         * days
                         * growthSuitability
                         * (1.0 - relativeBiomass);

            var liveBiomass = biomass * greenFraction;
            var dryBiomass = Math.Max(0.0, biomass - liveBiomass);
            var decompositionMoisture = SmoothStep(
                0.08,
                0.62,
                Math.Max(surfaceWater, liquidRootWater));
            var decompositionClimate = 0.22
                                       + growthWarmth * 0.38
                                       + decompositionMoisture * 0.40;
            var dryDecay = dryBiomass
                           * settings.VegetationDryBiomassDecayPerDay
                           * days
                           * decompositionClimate;
            var strongWind = SmoothStep(9.0, 22.0, forcing.SurfaceWindSpeed);
            var snowLoad = snowCoverage * (1.0 - snowCompaction * 0.35);
            var thawing = frozenFraction + 0.001 < state.FrozenFraction ? 1.0 : 0.0;
            var lodgingDriver = Clamp01(
                strongWind * 0.48
                + snowLoad * 0.72
                + thawing * 0.32);
            var dryLodging = dryBiomass
                             * settings.VegetationLodgingPerDay
                             * days
                             * lodgingDriver;
            var dryLoss = Math.Min(dryBiomass, dryDecay + dryLodging);

            liveBiomass += growth;
            dryBiomass -= dryLoss;
            biomass = liveBiomass + dryBiomass;
            if (biomass > capacity)
            {
                var overflow = biomass - capacity;
                var dryDisplacement = Math.Min(dryBiomass, overflow);
                dryBiomass -= dryDisplacement;
                overflow -= dryDisplacement;
                liveBiomass = Math.Max(0.0, liveBiomass - overflow);
                biomass = liveBiomass + dryBiomass;
            }

            var standingMinimum = capacity * 0.04;
            if (biomass < standingMinimum)
            {
                dryBiomass += standingMinimum - biomass;
                biomass = standingMinimum;
            }
            greenFraction = biomass > 0.0001
                ? Clamp01(liveBiomass / biomass)
                : 0.0;

            return new SteppeEcoCellState(
                Clamp01(surfaceWater),
                Clamp01(rootWater),
                Clamp01(biomass),
                greenFraction,
                Clamp01(crust),
                Clamp01(snowWater),
                snowCompaction,
                Clamp01(frozenFraction),
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

        private static double MoveTowards(double current, double target, double maximumDelta)
        {
            if (Math.Abs(target - current) <= maximumDelta)
            {
                return target;
            }

            return current + Math.Sign(target - current) * maximumDelta;
        }
    }
}
