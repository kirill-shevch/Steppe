using System;
using Steppe.Ecology;
using Steppe.Settings;
using Steppe.Surface;
using Steppe.Terrain;
using Steppe.Time;
using Steppe.Weather;
using Steppe.World;
using UnityEngine;

namespace Steppe.Rendering
{
    /// <summary>
    /// Makes otherwise abstract air temperature and near-surface humidity visible
    /// in the horizon. It runs after the celestial pass, which establishes the
    /// day/night base fog, and adds only the local weather response.
    /// </summary>
    [DefaultExecutionOrder(400)]
    [DisallowMultipleComponent]
    public sealed class SteppeAtmospherePresentation : MonoBehaviour
    {
        private static readonly int HeatHazeId = Shader.PropertyToID("_SteppeHeatHaze");
        private static readonly int AirHumidityId = Shader.PropertyToID("_SteppeAirHumiditySignal");

        private SteppeWorldSettings settings;
        private SteppeTimeSystem timeSystem;
        private SteppeWeatherSystem weatherSystem;
        private SteppeEcologySystem ecologySystem;
        private FloatingOriginSystem floatingOrigin;
        private Transform focus;
        private TerrainHeightGenerator terrainGenerator;
        private SteppeSurfaceGenerator surfaceGenerator;
        private SteppeClimateModel climateModel;

        public float CurrentAirTemperatureC { get; private set; }
        public float CurrentHeatHaze { get; private set; }
        public float CurrentHumidityHaze { get; private set; }

        public void Configure(
            SteppeWorldSettings worldSettings,
            SteppeTimeSystem clock,
            SteppeWeatherSystem weather,
            SteppeEcologySystem ecology,
            FloatingOriginSystem origin,
            Transform focusTransform)
        {
            settings = worldSettings != null ? worldSettings : throw new ArgumentNullException(nameof(worldSettings));
            timeSystem = clock != null ? clock : throw new ArgumentNullException(nameof(clock));
            weatherSystem = weather != null ? weather : throw new ArgumentNullException(nameof(weather));
            ecologySystem = ecology != null ? ecology : throw new ArgumentNullException(nameof(ecology));
            floatingOrigin = origin != null ? origin : throw new ArgumentNullException(nameof(origin));
            focus = focusTransform != null ? focusTransform : throw new ArgumentNullException(nameof(focusTransform));
            terrainGenerator = new TerrainHeightGenerator(settings);
            surfaceGenerator = new SteppeSurfaceGenerator(settings);
            climateModel = new SteppeClimateModel(settings);
        }

        private void LateUpdate()
        {
            if (settings == null || focus == null)
            {
                return;
            }

            var world = floatingOrigin.LocalToWorld(focus.position);
            var height = terrainGenerator.SampleHeight(world.X, world.Z);
            var normal = terrainGenerator.SampleNormal(world.X, world.Z, 2.0);
            var surface = surfaceGenerator.Sample(world.X, world.Z, height, normal.y);
            var climate = climateModel.Evaluate(surface, timeSystem.Current);
            var solar = SteppeAstronomy.Evaluate(timeSystem.Current, settings.LatitudeDegrees);
            ecologySystem.TryGetState(world.X, world.Z, out var ecology);

            CurrentAirTemperatureC = (float)climate.AirTemperatureC;
            var drySurface = 1f - Mathf.Clamp01((float)(ecology.SurfaceWater * 2.5 + ecology.RootWater * 0.35));
            CurrentHeatHaze = Mathf.InverseLerp(27f, 39f, CurrentAirTemperatureC)
                              * drySurface
                              * (float)solar.Daylight;
            var weather = weatherSystem.CurrentAtFocus;
            CurrentHumidityHaze = Mathf.Clamp01((float)(
                weather.CloudWater * 0.48
                + weather.RainIntensity * 0.72
                + ecology.SurfaceWater * 0.28));

            var daylight = (float)solar.Daylight;
            var baseDensity = Mathf.Lerp(0.0003f, 0.00018f, daylight);
            RenderSettings.fogDensity = baseDensity
                                        + CurrentHumidityHaze * 0.00012f
                                        + CurrentHeatHaze * 0.000035f;
            var warmHorizon = new Color(0.86f, 0.75f, 0.58f);
            RenderSettings.fogColor = Color.Lerp(
                RenderSettings.fogColor,
                warmHorizon,
                CurrentHeatHaze * 0.18f);
            Shader.SetGlobalFloat(HeatHazeId, CurrentHeatHaze);
            Shader.SetGlobalFloat(AirHumidityId, CurrentHumidityHaze);
        }

        private void OnDestroy()
        {
            Shader.SetGlobalFloat(HeatHazeId, 0f);
            Shader.SetGlobalFloat(AirHumidityId, 0f);
        }
    }
}
