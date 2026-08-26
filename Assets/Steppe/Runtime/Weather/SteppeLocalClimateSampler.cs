using System;
using Steppe.Settings;
using Steppe.Integration;
using Steppe.Surface;
using Steppe.Terrain;
using Steppe.Time;

namespace Steppe.Weather
{
    /// <summary>
    /// Shared presentation-side lookup for the current air temperature at an absolute
    /// position. The canonical climate model remains the single source of temperature.
    /// </summary>
    public sealed class SteppeLocalClimateSampler
    {
        private readonly TerrainHeightGenerator terrain;
        private readonly SteppeSurfaceGenerator surface;
        private readonly SteppeClimateModel climate;
        private readonly FiniteWorldEnvironmentAdapter finiteWorld;

        public SteppeLocalClimateSampler(
            SteppeWorldSettings settings,
            FiniteWorldEnvironmentAdapter finiteEnvironment = null)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            terrain = new TerrainHeightGenerator(settings);
            surface = new SteppeSurfaceGenerator(settings);
            climate = new SteppeClimateModel(settings);
            finiteWorld = finiteEnvironment;
        }

        public double SampleTemperature(double worldX, double worldZ, SteppeTimeSnapshot time)
        {
            if (finiteWorld != null
                && finiteWorld.TrySampleAirTemperature(worldX, worldZ, out var temperature))
            {
                return temperature;
            }

            var height = terrain.SampleHeight(worldX, worldZ);
            var normal = terrain.SampleNormal(worldX, worldZ, 2.0);
            var surfaceSample = surface.Sample(worldX, worldZ, height, normal.y);
            return climate.Evaluate(surfaceSample, time).AirTemperatureC;
        }
    }
}
