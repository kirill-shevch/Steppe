using System;
using Steppe.Ecology;
using Steppe.Integration;
using Steppe.Settings;
using Steppe.Simulation;
using Steppe.Surface;
using Steppe.Terrain;
using Steppe.Time;
using Steppe.UnitySimulation;
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
        private static readonly int AirTemperatureSignalId =
            Shader.PropertyToID("_SteppeAirTemperatureSignal");
        private static readonly int SolarRadiationSignalId =
            Shader.PropertyToID("_SteppeSolarRadiationSignal");
        private static readonly int SoilThermalDeltaId =
            Shader.PropertyToID("_SteppeSoilThermalDelta");

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
        private FiniteWorldEnvironmentAdapter finiteWorld;
        private SteppeSimulationHost simulationHost;

        public bool UsesSemanticAtmosphere => simulationHost != null && simulationHost.IsReady;
        public int SemanticStateCount => 8;
        public float CurrentSolarRadiationWm2 { get; private set; }
        public float CurrentSurfaceTemperatureC { get; private set; }
        public float CurrentSoilTemperatureC { get; private set; }
        public float CurrentAirTemperatureC { get; private set; }
        public float CurrentPressureHpa { get; private set; } = 982.5f;
        public float CurrentHumidityMillimeters { get; private set; }
        public float CurrentCloudWaterMillimeters { get; private set; }
        public float CurrentPrecipitationMmPerHour { get; private set; }
        public float CurrentSolarDirectSignal { get; private set; }
        public float CurrentAirTemperatureSignal { get; private set; }
        public float CurrentSoilThermalDeltaC => CurrentSoilTemperatureC - CurrentAirTemperatureC;
        public float CurrentHeatHaze { get; private set; }
        public float CurrentHumidityHaze { get; private set; }
        public float CurrentCloudOpticalThickness { get; private set; }
        public float CurrentCloudShadow { get; private set; }
        public float CurrentCloudTransmission { get; private set; } = 1f;

        public void Configure(
            SteppeWorldSettings worldSettings,
            SteppeTimeSystem clock,
            SteppeWeatherSystem weather,
            SteppeEcologySystem ecology,
            FloatingOriginSystem origin,
            Transform focusTransform,
            SteppeCelestialPresentation celestial,
            FiniteWorldEnvironmentAdapter finiteEnvironment = null,
            SteppeSimulationHost host = null)
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
            finiteWorld = finiteEnvironment;
            simulationHost = host;
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
            SampleSemanticState(world, climate);

            var daylight = (float)solar.Daylight;
            CurrentHeatHaze = Mathf.SmoothStep(
                                  0f,
                                  1f,
                                  Mathf.InverseLerp(24f, 43f, CurrentSurfaceTemperatureC))
                              * daylight;
            CurrentAirTemperatureSignal = Mathf.Clamp(CurrentAirTemperatureC / 34f, -1f, 1f);
            CurrentHumidityHaze = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(2.5f, 20f, CurrentHumidityMillimeters));
            CurrentCloudOpticalThickness = 1f - Mathf.Exp(
                -Mathf.Max(0f, CurrentCloudWaterMillimeters) * 0.38f);
            CurrentCloudShadow = CurrentCloudOpticalThickness * daylight;
            CurrentCloudTransmission = Mathf.Lerp(
                1f,
                SteppeSolarExposureModel.MinimumStormTransmission,
                CurrentCloudOpticalThickness);
            CurrentSolarDirectSignal = Mathf.Pow(
                Mathf.Clamp01(CurrentSolarRadiationWm2 / 1000f),
                0.32f);

            // Humidity exclusively owns extinction. Day/night supplies only the
            // exposure baseline and AirTemperature supplies only spectral offset.
            var baseDensity = Mathf.Lerp(0.00022f, 0.000085f, daylight);
            RenderSettings.fogDensity = baseDensity
                                        + Mathf.Pow(CurrentHumidityHaze, 1.35f) * 0.00022f;
            var baseFogColor = RenderSettings.fogColor;
            var warmHorizon = new Color(0.86f, 0.75f, 0.58f);
            var coldHorizon = new Color(0.48f, 0.64f, 0.82f);
            RenderSettings.fogColor = CurrentAirTemperatureSignal >= 0f
                ? Color.Lerp(baseFogColor, warmHorizon, CurrentAirTemperatureSignal * 0.24f)
                : Color.Lerp(baseFogColor, coldHorizon, -CurrentAirTemperatureSignal * 0.24f);
            RenderSettings.ambientIntensity *= Mathf.Lerp(
                1f,
                0.62f,
                CurrentCloudOpticalThickness);
            if (celestialPresentation.SunLight != null)
            {
                celestialPresentation.SunLight.intensity = CurrentSolarDirectSignal * 1.18f;
            }
            Shader.SetGlobalFloat(HeatHazeId, CurrentHeatHaze);
            Shader.SetGlobalFloat(AirHumidityId, CurrentHumidityHaze);
            Shader.SetGlobalFloat(CloudShadowId, CurrentCloudShadow);
            Shader.SetGlobalFloat(CloudTransmissionId, CurrentCloudTransmission);
            Shader.SetGlobalFloat(AirTemperatureSignalId, CurrentAirTemperatureSignal);
            Shader.SetGlobalFloat(SolarRadiationSignalId, CurrentSolarDirectSignal);
            Shader.SetGlobalFloat(SoilThermalDeltaId, CurrentSoilThermalDeltaC);
        }

        private void SampleSemanticState(WorldPosition world, ClimateSnapshot fallbackClimate)
        {
            CurrentAirTemperatureC = (float)fallbackClimate.AirTemperatureC;
            CurrentSurfaceTemperatureC = CurrentAirTemperatureC;
            CurrentSoilTemperatureC = CurrentAirTemperatureC;
            CurrentSolarRadiationWm2 = 0f;
            CurrentHumidityMillimeters = 6f;
            CurrentCloudWaterMillimeters = 0f;
            CurrentPrecipitationMmPerHour = 0f;

            if (finiteWorld != null
                && finiteWorld.TrySamplePhysical(world.X, world.Z, out var physical))
            {
                CurrentSolarRadiationWm2 = physical.SolarRadiationWattsPerSquareMeter;
                CurrentSurfaceTemperatureC = physical.SurfaceTemperatureC;
                CurrentAirTemperatureC = physical.AirTemperatureC;
                CurrentHumidityMillimeters = physical.HumidityMillimeters;
                CurrentCloudWaterMillimeters = physical.CloudWaterMillimeters;
                CurrentPrecipitationMmPerHour = physical.PrecipitationMillimetersPerHour;
            }

            if (simulationHost == null)
            {
                return;
            }

            if (simulationHost.TrySampleState(
                    SimulationLayer.SoilTemperature,
                    world.X,
                    world.Z,
                    out var soilTemperature))
            {
                CurrentSoilTemperatureC = soilTemperature;
            }
            if (simulationHost.TrySampleState(
                    SimulationLayer.Pressure,
                    world.X,
                    world.Z,
                    out var pressure))
            {
                CurrentPressureHpa = pressure;
            }
        }

        private void OnDestroy()
        {
            Shader.SetGlobalFloat(HeatHazeId, 0f);
            Shader.SetGlobalFloat(AirHumidityId, 0f);
            Shader.SetGlobalFloat(CloudShadowId, 0f);
            Shader.SetGlobalFloat(CloudTransmissionId, 1f);
            Shader.SetGlobalFloat(AirTemperatureSignalId, 0f);
            Shader.SetGlobalFloat(SolarRadiationSignalId, 0f);
            Shader.SetGlobalFloat(SoilThermalDeltaId, 0f);
        }
    }
}
