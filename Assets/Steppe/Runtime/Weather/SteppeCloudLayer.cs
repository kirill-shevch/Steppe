using System;
using Steppe.Rendering;
using Steppe.Settings;
using Steppe.Simulation;
using Steppe.UnitySimulation;
using Steppe.World;
using UnityEngine;

namespace Steppe.Weather
{
    /// <summary>
    /// Publishes the simulation-backed textures and coordinate transforms consumed by
    /// the URP volumetric cloud renderer. No visible cloud geometry lives in the scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SteppeCloudLayer : MonoBehaviour
    {
        private const int NoiseResolution = 32;
        private const float NoiseHorizontalScale = 4800f;

        private static readonly int ActiveId = Shader.PropertyToID("_SteppeCloudRendererActive");
        private static readonly int WeatherMapId = Shader.PropertyToID("_SteppeCloudWeatherMap");
        private static readonly int WeatherMapCenterLocalId = Shader.PropertyToID("_SteppeCloudMapCenterLocal");
        private static readonly int WeatherMapWorldSizeId = Shader.PropertyToID("_SteppeCloudMapWorldSize");
        private static readonly int WeatherMapAdvectionLocalId = Shader.PropertyToID("_SteppeCloudMapAdvectionLocal");
        private static readonly int NoiseId = Shader.PropertyToID("_SteppeCloudNoise3D");
        private static readonly int NoiseWorldPhaseId = Shader.PropertyToID("_SteppeCloudNoiseWorldPhase");
        private static readonly int NoiseAdvectionLocalId = Shader.PropertyToID("_SteppeCloudNoiseAdvectionLocal");
        private static readonly int LayerParametersId = Shader.PropertyToID("_SteppeCloudLayerParameters");

        private SteppeWorldSettings settings;
        private SteppeWeatherSystem weatherSystem;
        private FloatingOriginSystem floatingOrigin;
        private SteppeSimulationHost simulationHost;
        private Transform focus;
        private Texture3D cloudNoise;

        public bool IsReady => settings != null && weatherSystem != null && cloudNoise != null;
        public Texture3D NoiseTexture => cloudNoise;
        public Texture WeatherTexture => weatherSystem != null ? weatherSystem.WeatherMap : null;
        public float CurrentPressureHpa { get; private set; } = 982.5f;
        public float CurrentBaseHeight { get; private set; }

        public void Configure(
            SteppeWorldSettings worldSettings,
            SteppeWeatherSystem weather,
            FloatingOriginSystem origin,
            SteppeSimulationHost host = null,
            Transform focusTransform = null)
        {
            settings = worldSettings != null ? worldSettings : throw new ArgumentNullException(nameof(worldSettings));
            weatherSystem = weather != null ? weather : throw new ArgumentNullException(nameof(weather));
            floatingOrigin = origin != null ? origin : throw new ArgumentNullException(nameof(origin));
            simulationHost = host;
            focus = focusTransform;

            cloudNoise = BuildCloudNoise(NoiseResolution, settings.WorldSeed + 40933);
            CurrentBaseHeight = settings.CloudBaseHeight;
            SteppeVolumetricCloudRendererFeature.SetPresentationActive(true);
            ApplyShaderState();
        }

        private void LateUpdate()
        {
            if (!IsReady || floatingOrigin == null)
            {
                return;
            }

            ApplyShaderState();
        }

        private void ApplyShaderState()
        {
            UpdatePressurePresentation();
            var mapCenter = floatingOrigin.WorldToLocal(
                weatherSystem.MapCenterX,
                0.0,
                weatherSystem.MapCenterZ);
            var currentWind = weatherSystem.Model.SampleWind(weatherSystem.WeatherSeconds);
            var publishedWind = weatherSystem.Model.SampleWind(weatherSystem.PublishedMapWeatherSeconds);
            var mapAdvection = currentWind.CloudAdvection - publishedWind.CloudAdvection;
            var noiseAdvection = currentWind.CloudAdvection;

            Shader.SetGlobalFloat(ActiveId, 1f);
            Shader.SetGlobalTexture(WeatherMapId, weatherSystem.WeatherMap);
            Shader.SetGlobalVector(
                WeatherMapCenterLocalId,
                new Vector4(mapCenter.x, mapCenter.z, 0f, 0f));
            Shader.SetGlobalFloat(WeatherMapWorldSizeId, weatherSystem.MapWorldSize);
            Shader.SetGlobalVector(
                WeatherMapAdvectionLocalId,
                new Vector4((float)mapAdvection.X, (float)mapAdvection.Z, 0f, 0f));
            Shader.SetGlobalTexture(NoiseId, cloudNoise);
            Shader.SetGlobalVector(
                NoiseWorldPhaseId,
                new Vector4(
                    PositiveModulo(weatherSystem.MapCenterX, NoiseHorizontalScale),
                    PositiveModulo(weatherSystem.MapCenterZ, NoiseHorizontalScale),
                    0f,
                    0f));
            Shader.SetGlobalVector(
                NoiseAdvectionLocalId,
                new Vector4(
                    PositiveModulo(noiseAdvection.X, NoiseHorizontalScale),
                    PositiveModulo(noiseAdvection.Z, NoiseHorizontalScale),
                    0f,
                    0f));
            Shader.SetGlobalVector(
                LayerParametersId,
                new Vector4(
                    CurrentBaseHeight,
                    settings.CloudLayerThickness,
                    settings.CloudLayerRadius,
                    1f / Mathf.Max(settings.CloudLayerRadius, 1f)));
        }

        private void UpdatePressurePresentation()
        {
            if (simulationHost == null || focus == null || !simulationHost.IsReady)
            {
                CurrentBaseHeight = settings.CloudBaseHeight;
                return;
            }

            var world = floatingOrigin.LocalToWorld(focus.position);
            if (!simulationHost.TrySampleState(
                    SimulationLayer.Pressure,
                    world.X,
                    world.Z,
                    out var pressure))
            {
                return;
            }

            CurrentPressureHpa = pressure;
            var target = EvaluateCloudBaseHeight(pressure, settings.CloudBaseHeight);
            var response = Application.isPlaying
                ? 1f - Mathf.Exp(-UnityEngine.Time.deltaTime * 0.72f)
                : 1f;
            CurrentBaseHeight = Mathf.Lerp(CurrentBaseHeight, target, response);
        }

        public static float EvaluateCloudBaseHeight(float pressureHpa, float configuredBaseHeight)
        {
            var pressure = Mathf.InverseLerp(930f, 1035f, pressureHpa);
            return Mathf.Lerp(
                Mathf.Max(220f, configuredBaseHeight - 520f),
                configuredBaseHeight + 520f,
                pressure);
        }

        private static Texture3D BuildCloudNoise(int size, int seed)
        {
            var texture = new Texture3D(size, size, size, TextureFormat.RGBA32, false)
            {
                name = "Steppe Tileable Volumetric Cloud Noise",
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 0
            };
            var pixels = new Color32[size * size * size];

            for (var z = 0; z < size; z++)
            {
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var broad = PeriodicValueNoise(x, y, z, size, 4, seed);
                        var middle = PeriodicValueNoise(x, y, z, size, 9, seed + 1013);
                        var fine = PeriodicValueNoise(x, y, z, size, 17, seed + 2027);
                        var erosion = PeriodicValueNoise(x, y, z, size, 13, seed + 3041);
                        var shape = Mathf.Clamp01(broad * 0.55f + middle * 0.30f + fine * 0.15f);
                        var index = x + size * (y + size * z);
                        pixels[index] = new Color32(
                            ToByte(shape),
                            ToByte(middle),
                            ToByte(fine),
                            ToByte(erosion));
                    }
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static float PeriodicValueNoise(
            int pixelX,
            int pixelY,
            int pixelZ,
            int textureSize,
            int period,
            int seed)
        {
            var sampleX = pixelX / (float)textureSize * period;
            var sampleY = pixelY / (float)textureSize * period;
            var sampleZ = pixelZ / (float)textureSize * period;
            var x0 = Mathf.FloorToInt(sampleX);
            var y0 = Mathf.FloorToInt(sampleY);
            var z0 = Mathf.FloorToInt(sampleZ);
            var x1 = (x0 + 1) % period;
            var y1 = (y0 + 1) % period;
            var z1 = (z0 + 1) % period;
            x0 = PositiveModulo(x0, period);
            y0 = PositiveModulo(y0, period);
            z0 = PositiveModulo(z0, period);
            var tx = Smooth(sampleX - Mathf.Floor(sampleX));
            var ty = Smooth(sampleY - Mathf.Floor(sampleY));
            var tz = Smooth(sampleZ - Mathf.Floor(sampleZ));

            var lower0 = Mathf.Lerp(Hash01(x0, y0, z0, seed), Hash01(x1, y0, z0, seed), tx);
            var lower1 = Mathf.Lerp(Hash01(x0, y1, z0, seed), Hash01(x1, y1, z0, seed), tx);
            var upper0 = Mathf.Lerp(Hash01(x0, y0, z1, seed), Hash01(x1, y0, z1, seed), tx);
            var upper1 = Mathf.Lerp(Hash01(x0, y1, z1, seed), Hash01(x1, y1, z1, seed), tx);
            var lower = Mathf.Lerp(lower0, lower1, ty);
            var upper = Mathf.Lerp(upper0, upper1, ty);
            return Mathf.Lerp(lower, upper, tz);
        }

        private static float Hash01(int x, int y, int z, int seed)
        {
            unchecked
            {
                var combinedX = x + (long)z * 73856093L;
                var combinedY = y + (long)z * 19349663L;
                var hash = DeterministicNoise.Hash(combinedX, combinedY, seed);
                return (hash & 0x00ffffffUL) / 16777215f;
            }
        }

        private static int PositiveModulo(int value, int modulus)
        {
            return (value % modulus + modulus) % modulus;
        }

        private static float PositiveModulo(double value, double modulus)
        {
            return (float)(value - Math.Floor(value / modulus) * modulus);
        }

        private static float Smooth(float value)
        {
            return value * value * (3f - 2f * value);
        }

        private static byte ToByte(float value)
        {
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
        }

        private void OnDestroy()
        {
            SteppeVolumetricCloudRendererFeature.SetPresentationActive(false);
            Shader.SetGlobalFloat(ActiveId, 0f);
            Shader.SetGlobalTexture(WeatherMapId, null);
            Shader.SetGlobalTexture(NoiseId, null);

            if (cloudNoise == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(cloudNoise);
            }
            else
            {
                DestroyImmediate(cloudNoise);
            }
        }
    }
}
