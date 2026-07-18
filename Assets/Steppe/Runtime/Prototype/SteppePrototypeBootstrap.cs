using Steppe.Ecology;
using Steppe.Player;
using Steppe.Rendering;
using Steppe.Settings;
using Steppe.Terrain;
using Steppe.Time;
using Steppe.Weather;
using Steppe.World;
using UnityEngine;

namespace Steppe.Prototype
{
    [DisallowMultipleComponent]
    public sealed class SteppePrototypeBootstrap : MonoBehaviour
    {
        [SerializeField] private SteppeWorldSettings settings;
        [SerializeField] private Material terrainMaterial;
        [SerializeField] private Material vegetationMaterial;
        [SerializeField] private Material grassMaterial;
        [SerializeField] private Material rainMaterial;

        private SteppeWorldSettings runtimeSettings;
        private bool initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (FindAnyObjectByType<SteppePrototypeBootstrap>() != null)
            {
                return;
            }

            var root = new GameObject("Steppe Prototype");
            root.AddComponent<SteppePrototypeBootstrap>();
        }

        private void Awake()
        {
            BuildPrototype();
        }

        private void BuildPrototype()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            runtimeSettings = settings != null ? settings : SteppeWorldSettings.CreateRuntimeDefaults();

            var camera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
            if (camera == null)
            {
                var cameraObject = new GameObject("Steppe Camera");
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }

            var cameraController = camera.GetComponent<FlyCameraController>();
            if (cameraController == null)
            {
                const float initialZ = -64f;
                var initialGroundHeight = (float)new TerrainHeightGenerator(runtimeSettings).SampleHeight(0.0, initialZ);
                var initialHeight = Mathf.Max(runtimeSettings.InitialCameraHeight, initialGroundHeight + 60f);
                camera.transform.position = new Vector3(0f, initialHeight, initialZ);
                camera.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
                cameraController = camera.gameObject.AddComponent<FlyCameraController>();
            }

            cameraController.Configure(
                runtimeSettings.CameraMoveSpeed,
                runtimeSettings.CameraBoostMultiplier,
                runtimeSettings.MouseSensitivity);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = runtimeSettings.CameraFarClip;
            camera.clearFlags = CameraClearFlags.Skybox;

            var sun = EnsureDirectionalLight();
            var moon = EnsureMoonLight();
            ConfigureAtmosphere();

            var timeSystem = gameObject.AddComponent<SteppeTimeSystem>();
            timeSystem.Configure(runtimeSettings);
            var celestialPresentation = gameObject.AddComponent<SteppeCelestialPresentation>();
            celestialPresentation.Configure(timeSystem, sun, moon, runtimeSettings.LatitudeDegrees);

            var worldSpaceObject = new GameObject("World Space");
            worldSpaceObject.transform.SetParent(transform, false);

            var floatingOrigin = gameObject.AddComponent<FloatingOriginSystem>();
            floatingOrigin.Configure(
                camera.transform,
                worldSpaceObject.transform,
                runtimeSettings.FloatingOriginThreshold,
                runtimeSettings.ChunkSize);

            var workScheduler = gameObject.AddComponent<WorldWorkScheduler>();
            workScheduler.Configure(runtimeSettings.WorldWorkBudgetMilliseconds);

            var weatherSystem = gameObject.AddComponent<SteppeWeatherSystem>();
            weatherSystem.Configure(runtimeSettings, timeSystem, floatingOrigin, camera.transform, workScheduler);

            var ecologySystem = gameObject.AddComponent<SteppeEcologySystem>();
            ecologySystem.Configure(
                runtimeSettings,
                timeSystem,
                weatherSystem,
                floatingOrigin,
                camera.transform,
                workScheduler);

            var cloudObject = new GameObject("Cloud Layer");
            cloudObject.transform.SetParent(worldSpaceObject.transform, false);
            var cloudLayer = cloudObject.AddComponent<SteppeCloudLayer>();
            cloudLayer.Configure(runtimeSettings, weatherSystem, floatingOrigin);

            var rainObject = new GameObject("Rain Volume");
            rainObject.transform.SetParent(worldSpaceObject.transform, false);
            var rainPresentation = rainObject.AddComponent<SteppeRainPresentation>();
            rainPresentation.Configure(runtimeSettings, weatherSystem, camera.transform, rainMaterial);

            var grassObject = new GameObject("Grass Field");
            grassObject.transform.SetParent(worldSpaceObject.transform, false);
            var grassRenderer = grassObject.AddComponent<SteppeGrassRenderer>();
            grassRenderer.Configure(runtimeSettings, floatingOrigin, camera.transform, workScheduler, grassMaterial);

            var chunkStreamer = gameObject.AddComponent<TerrainChunkStreamer>();
            chunkStreamer.Configure(
                runtimeSettings,
                floatingOrigin,
                camera.transform,
                worldSpaceObject.transform,
                workScheduler,
                terrainMaterial);

            if (!grassRenderer.IsRendering)
            {
                var legacyVegetationObject = new GameObject("Legacy Vegetation");
                legacyVegetationObject.transform.SetParent(worldSpaceObject.transform, false);
                var legacyVegetation = legacyVegetationObject.AddComponent<SteppeLegacyVegetationRenderer>();
                legacyVegetation.Configure(
                    runtimeSettings,
                    floatingOrigin,
                    chunkStreamer,
                    workScheduler,
                    vegetationMaterial);
            }

            var overlay = gameObject.AddComponent<WorldDebugOverlay>();
            overlay.Configure(
                runtimeSettings,
                floatingOrigin,
                chunkStreamer,
                cameraController,
                camera.transform,
                timeSystem,
                weatherSystem,
                ecologySystem,
                grassRenderer,
                workScheduler);

            var biomeNavigator = gameObject.AddComponent<BiomeDebugNavigator>();
            biomeNavigator.Configure(runtimeSettings, floatingOrigin, camera.transform);

            Application.targetFrameRate = 60;
        }

        private static Light EnsureDirectionalLight()
        {
            var namedSun = GameObject.Find("Steppe Sun");
            if (namedSun != null && namedSun.TryGetComponent<Light>(out var existingSun))
            {
                RenderSettings.sun = existingSun;
                return existingSun;
            }

            var lights = FindObjectsByType<Light>();
            for (var index = 0; index < lights.Length; index++)
            {
                if (lights[index].type == LightType.Directional && lights[index].name != "Steppe Moon")
                {
                    lights[index].name = "Steppe Sun";
                    RenderSettings.sun = lights[index];
                    return lights[index];
                }
            }

            var lightObject = new GameObject("Steppe Sun");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(1f, 0.93f, 0.82f);
            light.transform.rotation = Quaternion.Euler(42f, -28f, 0f);
            RenderSettings.sun = light;
            return light;
        }

        private static Light EnsureMoonLight()
        {
            var existingObject = GameObject.Find("Steppe Moon");
            if (existingObject != null && existingObject.TryGetComponent<Light>(out var existingMoon))
            {
                return existingMoon;
            }

            var lightObject = new GameObject("Steppe Moon");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0f;
            light.color = new Color(0.55f, 0.67f, 0.92f);
            light.shadows = LightShadows.None;
            light.enabled = false;
            return light;
        }

        private static void ConfigureAtmosphere()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.00018f;
            RenderSettings.fogColor = new Color(0.72f, 0.79f, 0.82f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
        }

        private void OnDestroy()
        {
            if (settings == null && runtimeSettings != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(runtimeSettings);
                }
                else
                {
                    DestroyImmediate(runtimeSettings);
                }
            }
        }
    }
}
