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
        private static readonly int CloudShadowId = Shader.PropertyToID("_SteppeCloudShadowAtFocus");
        private static readonly int CloudTransmissionId =
            Shader.PropertyToID("_SteppeCloudTransmissionAtFocus");

        private SteppeWorldSettings settings;
        private SteppeTimeSystem timeSystem;
        private SteppeWeatherSystem weatherSystem;
        private SteppeEcologySystem ecologySystem;
        private FloatingOriginSystem floatingOrigin;
        private Transform focus;
        private SteppeCelestialPresentation celestialPresentation;
        private TerrainHeightGenerator terrainGenerator;
        private SteppeSurfaceGenerator surfaceGenerator;
        private SteppeClimateModel climateModel;

        public float CurrentAirTemperatureC { get; private set; }
        public float CurrentHeatHaze { get; private set; }
        public float CurrentHumidityHaze { get; private set; }
        public float CurrentCloudShadow { get; private set; }
        public float CurrentCloudTransmission { get; private set; } = 1f;

        public void Configure(
            SteppeWorldSettings worldSettings,
            SteppeTimeSystem clock,
            SteppeWeatherSystem weather,
            SteppeEcologySystem ecology,
            FloatingOriginSystem origin,
            Transform focusTransform,
            SteppeCelestialPresentation celestial)
        {
            settings = worldSettings != null ? worldSettings : throw new ArgumentNullException(nameof(worldSettings));
            timeSystem = clock != null ? clock : throw new ArgumentNullException(nameof(clock));
            weatherSystem = weather != null ? weather : throw new ArgumentNullException(nameof(weather));
            ecologySystem = ecology != null ? ecology : throw new ArgumentNullException(nameof(ecology));
            floatingOrigin = origin != null ? origin : throw new ArgumentNullException(nameof(origin));
            focus = focusTransform != null ? focusTransform : throw new ArgumentNullException(nameof(focusTransform));
            celestialPresentation = celestial != null
                ? celestial
                : throw new ArgumentNullException(nameof(celestial));
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
            var cloudOffset = SteppeSolarExposureModel.CalculateCloudProjectionOffset(
                focus.position.y,
                solar,
                settings.CloudBaseHeight,
                settings.CloudLayerRadius);
            var weather = weatherSystem.Sample(
                world.X + cloudOffset.x,
                world.Z + cloudOffset.y);
            CurrentCloudShadow = SteppeSolarExposureModel.EvaluateCloudShadow(weather)
                                 * (float)solar.Daylight;
            CurrentCloudTransmission = Mathf.Lerp(
                1f,
                SteppeSolarExposureModel.MinimumStormTransmission,
                CurrentCloudShadow);
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
            RenderSettings.fogColor = Color.Lerp(
                RenderSettings.fogColor,
                RenderSettings.fogColor * new Color(0.63f, 0.70f, 0.78f),
                CurrentCloudShadow * 0.42f);
            RenderSettings.ambientIntensity *= Mathf.Lerp(1f, 0.58f, CurrentCloudShadow);
            if (celestialPresentation.SunLight != null)
            {
                celestialPresentation.SunLight.intensity *= CurrentCloudTransmission;
            }
            Shader.SetGlobalFloat(HeatHazeId, CurrentHeatHaze);
            Shader.SetGlobalFloat(AirHumidityId, CurrentHumidityHaze);
            Shader.SetGlobalFloat(CloudShadowId, CurrentCloudShadow);
            Shader.SetGlobalFloat(CloudTransmissionId, CurrentCloudTransmission);
        }

        private void OnDestroy()
        {
            Shader.SetGlobalFloat(HeatHazeId, 0f);
            Shader.SetGlobalFloat(AirHumidityId, 0f);
            Shader.SetGlobalFloat(CloudShadowId, 0f);
            Shader.SetGlobalFloat(CloudTransmissionId, 1f);
        }
    }
}
