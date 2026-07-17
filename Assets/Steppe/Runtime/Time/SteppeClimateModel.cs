using System;
using Steppe.Settings;
using Steppe.Surface;

namespace Steppe.Time
{
    public readonly struct ClimateSnapshot
    {
        public ClimateSnapshot(double airTemperatureC, SteppeSeason season, SteppeBiome biome)
        {
            AirTemperatureC = airTemperatureC;
            Season = season;
            Biome = biome;
        }

        public double AirTemperatureC { get; }
        public SteppeSeason Season { get; }
        public SteppeBiome Biome { get; }
    }

    public sealed class SteppeClimateModel
    {
        private readonly SteppeWorldSettings settings;

        public SteppeClimateModel(SteppeWorldSettings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public ClimateSnapshot Evaluate(SurfaceSample surface, SteppeTimeSnapshot time)
        {
            // The seasonal peak lags the solstice; the daily peak arrives around 15:00.
            var seasonal = -settings.SeasonalTemperatureAmplitude
                           * Math.Cos(Math.PI * 2.0 * (time.YearFraction - 0.02));
            var diurnal = settings.DiurnalTemperatureAmplitude
                          * Math.Cos(Math.PI * 2.0 * (time.Hour - 15.0) / 24.0);
            var temperature = surface.MeanAnnualTemperatureC + seasonal + diurnal;
            return new ClimateSnapshot(temperature, time.Season, surface.DominantBiome);
        }
    }
}
