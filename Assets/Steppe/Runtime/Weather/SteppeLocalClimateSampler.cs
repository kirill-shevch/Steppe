using System;
using Steppe.Settings;
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

        public SteppeLocalClimateSampler(SteppeWorldSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            terrain = new TerrainHeightGenerator(settings);
            surface = new SteppeSurfaceGenerator(settings);
            climate = new SteppeClimateModel(settings);
        }

        public double SampleTemperature(double worldX, double worldZ, SteppeTimeSnapshot time)
        {
            var height = terrain.SampleHeight(worldX, worldZ);
            var normal = terrain.SampleNormal(worldX, worldZ, 2.0);
            var surfaceSample = surface.Sample(worldX, worldZ, height, normal.y);
            return climate.Evaluate(surfaceSample, time).AirTemperatureC;
        }
    }
}
