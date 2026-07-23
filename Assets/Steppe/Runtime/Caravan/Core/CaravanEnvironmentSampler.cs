using System;
using Steppe.Ecology;
using Steppe.Player;
using Steppe.Settings;
using Steppe.Surface;
using Steppe.Terrain;
using Steppe.Weather;
using Steppe.World;
using UnityEngine;

namespace Steppe.Caravan
{
    public readonly struct CaravanEnvironmentSample
    {
        public CaravanEnvironmentSample(
            SteppeWeatherSample weather,
            SurfaceSample surface,
            SteppeEcoCellState ecology,
            SteppeTraversalState traversal,
            SteppeDustState dust)
        {
            Weather = weather;
            Surface = surface;
            Ecology = ecology;
            Traversal = traversal;
            Dust = dust;
        }

        public SteppeWeatherSample Weather { get; }
        public SurfaceSample Surface { get; }
        public SteppeEcoCellState Ecology { get; }
        public SteppeTraversalState Traversal { get; }
        public SteppeDustState Dust { get; }
    }

    /// <summary>
    /// One authoritative bridge between the caravan and the existing steppe fields.
    /// Modules never sample presentation particles or terrain colors to infer state.
    /// </summary>
    public sealed class CaravanEnvironmentSampler
    {
        private readonly SteppeWorldSettings settings;
        private readonly FloatingOriginSystem floatingOrigin;
        private readonly SteppeWeatherSystem weatherSystem;
        private readonly SteppeEcologySystem ecologySystem;
        private readonly TerrainHeightGenerator terrain;
        private readonly SteppeSurfaceGenerator surface;

        public CaravanEnvironmentSampler(
            SteppeWorldSettings worldSettings,
            FloatingOriginSystem origin,
            SteppeWeatherSystem weather,
            SteppeEcologySystem ecology)
        {
            settings = worldSettings != null ? worldSettings : throw new ArgumentNullException(nameof(worldSettings));
            floatingOrigin = origin != null ? origin : throw new ArgumentNullException(nameof(origin));
            weatherSystem = weather != null ? weather : throw new ArgumentNullException(nameof(weather));
            ecologySystem = ecology != null ? ecology : throw new ArgumentNullException(nameof(ecology));
            terrain = new TerrainHeightGenerator(settings);
            surface = new SteppeSurfaceGenerator(settings);
        }

        public bool TrySample(Vector3 localPosition, out CaravanEnvironmentSample sample)
        {
            var world = floatingOrigin.LocalToWorld(localPosition);
            var coordinate = EcoCellCoordinate.FromWorld(world.X, world.Z, settings.EcologyCellSize);
            if (!ecologySystem.TryGetCell(coordinate, out var surfaceSample, out var ecology))
            {
                var height = terrain.SampleHeight(world.X, world.Z);
                var normal = terrain.SampleNormal(world.X, world.Z, 2.0);
                surfaceSample = surface.Sample(world.X, world.Z, height, normal.y);
                sample = default;
                return false;
            }

            var weather = weatherSystem.Sample(world.X, world.Z);
            var traversal = SteppeTraversalModel.Evaluate(settings, surfaceSample, ecology);
            var dust = SteppeDustModel.Evaluate(
                settings,
                surfaceSample,
                ecology,
                weather.SurfaceWind.magnitude,
                weather.RainIntensity);
            sample = new CaravanEnvironmentSample(weather, surfaceSample, ecology, traversal, dust);
            return true;
        }
    }
}
