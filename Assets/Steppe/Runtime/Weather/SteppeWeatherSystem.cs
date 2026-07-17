using System;
using Steppe.Settings;
using Steppe.Time;
using Steppe.World;
using UnityEngine;

namespace Steppe.Weather
{
    [DisallowMultipleComponent]
    public sealed class SteppeWeatherSystem : MonoBehaviour, IWorldWorkSource
    {
        private const int WeatherMapRowsPerWorkStep = 32;
        private static readonly int WindVelocityId = Shader.PropertyToID("_SteppeWindVelocity");
        private static readonly int WindTimeId = Shader.PropertyToID("_SteppeWindTime");
        private static readonly int WindAnimationTimeId = Shader.PropertyToID("_SteppeWindAnimationTime");
        private static readonly int GustScalesId = Shader.PropertyToID("_SteppeGustScales");

        private SteppeWorldSettings settings;
        private SteppeTimeSystem timeSystem;
        private FloatingOriginSystem floatingOrigin;
        private Transform focus;
        private WorldWorkScheduler workScheduler;
        private SteppeWeatherModel model;
        private Texture2D weatherMap;
        private Color32[] weatherPixels;
        private double weatherSeconds;
        private double windAnimationSeconds;
        private double mapCenterX;
        private double mapCenterZ;
        private float secondsUntilMapUpdate;
        private bool mapBuildInProgress;
        private int nextMapBuildRow;
        private double buildCenterX;
        private double buildCenterZ;
        private double buildWeatherSeconds;
        private double publishedMapWeatherSeconds;
        private float buildMaximumCoverage;
        private float buildMaximumWater;
        private float buildMaximumRain;

        public SteppeWeatherModel Model => model;
        public Texture2D WeatherMap => weatherMap;
        public double WeatherSeconds => weatherSeconds;
        public double WindAnimationSeconds => windAnimationSeconds;
        public double MapCenterX => mapCenterX;
        public double MapCenterZ => mapCenterZ;
        public float MapWorldSize => settings != null ? settings.WeatherMapWorldSize : 0f;
        public double PublishedMapWeatherSeconds => publishedMapWeatherSeconds;
        public int MapRevision { get; private set; }
        public bool IsWeatherMapReady => MapRevision > 0;
        public float MapMaximumCoverage { get; private set; }
        public float MapMaximumWater { get; private set; }
        public float MapMaximumRain { get; private set; }
        public SteppeWeatherSample CurrentAtFocus { get; private set; }
        public bool HasPendingWorldWork => mapBuildInProgress;

        public void Configure(
            SteppeWorldSettings worldSettings,
            SteppeTimeSystem clock,
            FloatingOriginSystem origin,
            Transform focusTransform,
            WorldWorkScheduler scheduler)
        {
            settings = worldSettings != null ? worldSettings : throw new ArgumentNullException(nameof(worldSettings));
            timeSystem = clock != null ? clock : throw new ArgumentNullException(nameof(clock));
            floatingOrigin = origin != null ? origin : throw new ArgumentNullException(nameof(origin));
            focus = focusTransform != null ? focusTransform : throw new ArgumentNullException(nameof(focusTransform));
            workScheduler = scheduler != null ? scheduler : throw new ArgumentNullException(nameof(scheduler));
            model = new SteppeWeatherModel(settings);
            weatherSeconds = 0.0;
            windAnimationSeconds = 0.0;
            CreateWeatherMap();
            var focusWorld = floatingOrigin.LocalToWorld(focus.position);
            mapCenterX = focusWorld.X;
            mapCenterZ = focusWorld.Z;
            CurrentAtFocus = model.Sample(focusWorld.X, focusWorld.Z, weatherSeconds);
            PublishWindShaderState();
            BeginWeatherMapBuild(focusWorld.X, focusWorld.Z);
            workScheduler.Register(this);
        }

        public SteppeWeatherSample Sample(double worldX, double worldZ)
        {
            if (model == null)
            {
                throw new InvalidOperationException("The weather system has not been configured.");
            }

            return model.Sample(worldX, worldZ, weatherSeconds);
        }

        private void Update()
        {
            if (settings == null || timeSystem == null || floatingOrigin == null || focus == null)
            {
                return;
            }

            if (!timeSystem.IsPaused)
            {
                weatherSeconds += UnityEngine.Time.deltaTime
                                  * settings.WeatherSecondsPerRealSecond
                                  * timeSystem.DebugMultiplier;
                // Broad weather still follows the exact debug multiplier. Local grass
                // oscillation uses a compressed multiplier so x100 remains readable
                // instead of aliasing into frame-to-frame noise.
                windAnimationSeconds += UnityEngine.Time.deltaTime
                                        * Mathf.Pow(timeSystem.DebugMultiplier, 0.35f);
            }

            var focusWorld = floatingOrigin.LocalToWorld(focus.position);
            CurrentAtFocus = model.Sample(focusWorld.X, focusWorld.Z, weatherSeconds);
            PublishWindShaderState();

            if (mapBuildInProgress)
            {
                return;
            }

            secondsUntilMapUpdate -= UnityEngine.Time.deltaTime;
            var texelSize = settings.WeatherMapWorldSize / settings.WeatherMapResolution;
            var movedX = focusWorld.X - mapCenterX;
            var movedZ = focusWorld.Z - mapCenterZ;
            var movedFarEnough = movedX * movedX + movedZ * movedZ > texelSize * texelSize * 0.25;
            if (secondsUntilMapUpdate <= 0f || movedFarEnough)
            {
                BeginWeatherMapBuild(focusWorld.X, focusWorld.Z);
            }
        }

        private void CreateWeatherMap()
        {
            var resolution = settings.WeatherMapResolution;
            weatherMap = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true)
            {
                name = "Steppe P3 Weather Map (coverage, water, rain)",
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 1
            };
            weatherPixels = new Color32[resolution * resolution];
        }

        private void BeginWeatherMapBuild(double centerX, double centerZ)
        {
            buildCenterX = centerX;
            buildCenterZ = centerZ;
            buildWeatherSeconds = weatherSeconds;
            nextMapBuildRow = 0;
            buildMaximumCoverage = 0f;
            buildMaximumWater = 0f;
            buildMaximumRain = 0f;
            mapBuildInProgress = true;
        }

        public void ExecuteWorldWorkStep()
        {
            if (mapBuildInProgress)
            {
                BuildWeatherMapRows();
            }
        }

        private void BuildWeatherMapRows()
        {
            var resolution = settings.WeatherMapResolution;
            var worldSize = settings.WeatherMapWorldSize;
            var step = worldSize / resolution;
            var minimumX = buildCenterX - worldSize * 0.5 + step * 0.5;
            var minimumZ = buildCenterZ - worldSize * 0.5 + step * 0.5;
            var endRow = Mathf.Min(resolution, nextMapBuildRow + WeatherMapRowsPerWorkStep);

            for (var z = nextMapBuildRow; z < endRow; z++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    var sample = model.Sample(minimumX + x * step, minimumZ + z * step, buildWeatherSeconds);
                    buildMaximumCoverage = Mathf.Max(buildMaximumCoverage, (float)sample.CloudCoverage);
                    buildMaximumWater = Mathf.Max(buildMaximumWater, (float)sample.CloudWater);
                    buildMaximumRain = Mathf.Max(buildMaximumRain, (float)sample.RainIntensity);
                    weatherPixels[z * resolution + x] = new Color32(
                        ToByte(sample.CloudCoverage),
                        ToByte(sample.CloudWater),
                        ToByte(sample.RainIntensity),
                        255);
                }
            }

            nextMapBuildRow = endRow;
            if (nextMapBuildRow < resolution)
            {
                return;
            }

            weatherMap.SetPixels32(weatherPixels);
            weatherMap.Apply(false, false);
            mapCenterX = buildCenterX;
            mapCenterZ = buildCenterZ;
            publishedMapWeatherSeconds = buildWeatherSeconds;
            MapMaximumCoverage = buildMaximumCoverage;
            MapMaximumWater = buildMaximumWater;
            MapMaximumRain = buildMaximumRain;
            MapRevision++;
            mapBuildInProgress = false;
            secondsUntilMapUpdate = settings.WeatherMapUpdateInterval;
        }

        private static byte ToByte(double value)
        {
            return (byte)Mathf.RoundToInt(Mathf.Clamp01((float)value) * 255f);
        }

        private void PublishWindShaderState()
        {
            var velocity = model.WindVelocity;
            var speed = velocity.magnitude;
            Shader.SetGlobalVector(WindVelocityId, new Vector4(
                velocity.x,
                velocity.y,
                speed,
                Mathf.Clamp01(speed / 12f)));
            Shader.SetGlobalFloat(WindTimeId, (float)weatherSeconds);
            Shader.SetGlobalFloat(WindAnimationTimeId, (float)windAnimationSeconds);
            Shader.SetGlobalVector(GustScalesId, new Vector4(
                settings.WindGustWavelength,
                settings.WindGustCrossScale,
                settings.WindFineScale,
                settings.WindGustAdvection));
        }

        private void OnDestroy()
        {
            if (workScheduler != null)
            {
                workScheduler.Unregister(this);
            }
            if (weatherMap == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(weatherMap);
            }
            else
            {
                DestroyImmediate(weatherMap);
            }
        }
    }
}
