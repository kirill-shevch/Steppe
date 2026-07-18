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
        private static readonly int SurfaceWindAdvectionId = Shader.PropertyToID("_SteppeSurfaceWindAdvection");
        private static readonly int WindFieldBasisId = Shader.PropertyToID("_SteppeWindFieldBasis");
        private static readonly int WindRegimeId = Shader.PropertyToID("_SteppeWindRegime");

        private SteppeWorldSettings settings;
        private SteppeTimeSystem timeSystem;
        private FloatingOriginSystem floatingOrigin;
        private Transform focus;
        private WorldWorkScheduler workScheduler;
        private SteppeWeatherModel model;
        private Texture2D weatherMap;
        private Color32[] weatherPixels;
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
        private float buildMaximumGust;

        public SteppeWeatherModel Model => model;
        public Texture2D WeatherMap => weatherMap;
        public double WeatherSeconds => settings != null && timeSystem != null
            ? SteppeWeatherTime.FromSimulationSeconds(settings, timeSystem.ElapsedSimulationSeconds)
            : 0.0;
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
        public float MapMaximumGust { get; private set; }
        public SteppeWindRegimeSample CurrentWind { get; private set; }
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
            windAnimationSeconds = 0.0;
            CreateWeatherMap();
            var focusWorld = floatingOrigin.LocalToWorld(focus.position);
            mapCenterX = focusWorld.X;
            mapCenterZ = focusWorld.Z;
            CurrentWind = model.SampleWind(WeatherSeconds);
            CurrentAtFocus = model.Sample(focusWorld.X, focusWorld.Z, WeatherSeconds);
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

            return model.Sample(worldX, worldZ, WeatherSeconds);
        }

        private void Update()
        {
            if (settings == null || timeSystem == null || floatingOrigin == null || focus == null)
            {
                return;
            }

            if (!timeSystem.IsPaused)
            {
                // Canonical weather time comes from SteppeTimeSystem. This presentation
                // clock remains separate so x100 does not alias blade sway into noise.
                windAnimationSeconds += UnityEngine.Time.deltaTime
                                        * Mathf.Pow(timeSystem.DebugMultiplier, 0.35f);
            }

            var focusWorld = floatingOrigin.LocalToWorld(focus.position);
            CurrentWind = model.SampleWind(WeatherSeconds);
            CurrentAtFocus = model.Sample(focusWorld.X, focusWorld.Z, WeatherSeconds);
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
                name = "Steppe Weather Map (coverage, water, rain, gust)",
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
            buildWeatherSeconds = WeatherSeconds;
            nextMapBuildRow = 0;
            buildMaximumCoverage = 0f;
            buildMaximumWater = 0f;
            buildMaximumRain = 0f;
            buildMaximumGust = 0f;
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
                    buildMaximumGust = Mathf.Max(buildMaximumGust, (float)sample.StormGust);
                    weatherPixels[z * resolution + x] = new Color32(
                        ToByte(sample.CloudCoverage),
                        ToByte(sample.CloudWater),
                        ToByte(sample.RainIntensity),
                        ToByte(sample.StormGust));
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
            MapMaximumGust = buildMaximumGust;
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
            var velocity = CurrentAtFocus.SurfaceWind;
            var speed = velocity.magnitude;
            Shader.SetGlobalVector(WindVelocityId, new Vector4(
                velocity.x,
                velocity.y,
                speed,
                Mathf.Clamp01(speed / 12f)));
            Shader.SetGlobalFloat(WindTimeId, (float)WeatherSeconds);
            Shader.SetGlobalFloat(WindAnimationTimeId, (float)windAnimationSeconds);
            Shader.SetGlobalVector(SurfaceWindAdvectionId, new Vector4(
                (float)CurrentWind.SurfaceAdvection.X,
                (float)CurrentWind.SurfaceAdvection.Z,
                0f,
                0f));
            var basis = model.FrontDirection;
            Shader.SetGlobalVector(WindFieldBasisId, new Vector4(basis.x, basis.y, 0f, 0f));
            Shader.SetGlobalVector(WindRegimeId, new Vector4(
                CurrentWind.Gustiness,
                (float)CurrentAtFocus.StormGust,
                0f,
                0f));
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
