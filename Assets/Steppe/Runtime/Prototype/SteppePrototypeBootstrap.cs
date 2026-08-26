using Steppe.Ecology;
using Steppe.Integration;
using Steppe.Player;
using Steppe.Rendering;
using Steppe.Settings;
using Steppe.Terrain;
using Steppe.Time;
using Steppe.UnitySimulation;
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
        [SerializeField] private Material dustMaterial;
        [SerializeField] private Material snowMaterial;

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

            var simulationHost = gameObject.AddComponent<SteppeSimulationHost>();
            simulationHost.Configure(
                runtimeSettings.WorldSeed,
                runtimeSettings.SimulationSecondsPerRealSecond,
                latitudeDegrees: runtimeSettings.LatitudeDegrees);
            var finiteEnvironment = new FiniteWorldEnvironmentAdapter(simulationHost);

            var camera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
            if (camera == null)
            {
                var cameraObject = new GameObject("Steppe Camera");
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }

            const float initialX = 32f;
            const float initialZ = -64f;
            var initialGroundHeight = (float)new TerrainHeightGenerator(runtimeSettings).SampleHeight(initialX, initialZ);
            camera.transform.position = new Vector3(
                initialX,
                initialGroundHeight + runtimeSettings.InitialCameraHeight,
                initialZ);
            camera.transform.rotation = Quaternion.Euler(12f, 24f, 0f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = runtimeSettings.CameraFarClip;
            camera.clearFlags = CameraClearFlags.Skybox;

            var existingBallCamera = camera.GetComponent<SteppeBallCameraController>();
            if (existingBallCamera != null)
            {
                existingBallCamera.enabled = false;
            }

            var flyCamera = camera.GetComponent<FlyCameraController>();
            if (flyCamera == null)
            {
                flyCamera = camera.gameObject.AddComponent<FlyCameraController>();
            }

            flyCamera.enabled = true;
            flyCamera.Configure(
                runtimeSettings.CameraMoveSpeed,
                runtimeSettings.CameraBoostMultiplier,
                runtimeSettings.MouseSensitivity);
            var focus = camera.transform;

            var sun = EnsureDirectionalLight();
            var moon = EnsureMoonLight();
            ConfigureAtmosphere();

            var timeSystem = gameObject.AddComponent<SteppeTimeSystem>();
            timeSystem.Configure(runtimeSettings, simulationHost);
            var celestialPresentation = gameObject.AddComponent<SteppeCelestialPresentation>();
            celestialPresentation.Configure(timeSystem, sun, moon, runtimeSettings.LatitudeDegrees);

            var worldSpaceObject = new GameObject("World Space");
            worldSpaceObject.transform.SetParent(transform, false);

            var floatingOrigin = gameObject.AddComponent<FloatingOriginSystem>();
            floatingOrigin.Configure(
                focus,
                worldSpaceObject.transform,
                runtimeSettings.FloatingOriginThreshold,
                runtimeSettings.ChunkSize);

            var workScheduler = gameObject.AddComponent<WorldWorkScheduler>();
            workScheduler.Configure(runtimeSettings.WorldWorkBudgetMilliseconds);

            var weatherSystem = gameObject.AddComponent<SteppeWeatherSystem>();
            weatherSystem.Configure(
                runtimeSettings,
                timeSystem,
                floatingOrigin,
                focus,
                workScheduler,
                finiteEnvironment);

            var ecologySystem = gameObject.AddComponent<SteppeEcologySystem>();
            ecologySystem.Configure(
                runtimeSettings,
                timeSystem,
                weatherSystem,
                floatingOrigin,
                focus,
                workScheduler,
                finiteEnvironment);

            var atmospherePresentation = gameObject.AddComponent<SteppeAtmospherePresentation>();
            atmospherePresentation.Configure(
                runtimeSettings,
                timeSystem,
                weatherSystem,
                ecologySystem,
                floatingOrigin,
                focus,
                celestialPresentation,
                finiteEnvironment,
                simulationHost);

            var cloudObject = new GameObject("Cloud Layer");
            cloudObject.transform.SetParent(worldSpaceObject.transform, false);
            var cloudLayer = cloudObject.AddComponent<SteppeCloudLayer>();
            cloudLayer.Configure(
                runtimeSettings,
                weatherSystem,
                floatingOrigin,
                simulationHost,
                focus);

            var rainObject = new GameObject("Rain Volume");
            rainObject.transform.SetParent(worldSpaceObject.transform, false);
            var rainPresentation = rainObject.AddComponent<SteppeRainPresentation>();
            rainPresentation.Configure(
                runtimeSettings,
                weatherSystem,
                timeSystem,
                floatingOrigin,
                camera.transform,
                rainMaterial,
                finiteEnvironment);

            var snowObject = new GameObject("Snow Volume");
            snowObject.transform.SetParent(worldSpaceObject.transform, false);
            var snowPresentation = snowObject.AddComponent<SteppeSnowPresentation>();
            snowPresentation.Configure(
                runtimeSettings,
                weatherSystem,
                timeSystem,
                floatingOrigin,
                camera.transform,
                snowMaterial,
                finiteEnvironment);

            var dustObject = new GameObject("Dust Field");
            dustObject.transform.SetParent(worldSpaceObject.transform, false);
            var dustPresentation = dustObject.AddComponent<SteppeDustPresentation>();
            dustPresentation.Configure(
                runtimeSettings,
                weatherSystem,
                ecologySystem,
                floatingOrigin,
                camera.transform,
                dustMaterial);

            var windCueObject = new GameObject("Wind-borne Seeds");
            windCueObject.transform.SetParent(worldSpaceObject.transform, false);
            var windCue = windCueObject.AddComponent<SteppeWindCuePresentation>();
            windCue.Configure(weatherSystem, floatingOrigin, focus);

            var grassObject = new GameObject("Grass Field");
            grassObject.transform.SetParent(worldSpaceObject.transform, false);
            var grassRenderer = grassObject.AddComponent<SteppeGrassRenderer>();
            grassRenderer.Configure(runtimeSettings, floatingOrigin, focus, workScheduler, grassMaterial);

            var groundDetailObject = new GameObject("Semantic Ground Details");
            groundDetailObject.transform.SetParent(worldSpaceObject.transform, false);
            var groundDetailRenderer = groundDetailObject.AddComponent<SteppeGroundDetailRenderer>();
            groundDetailRenderer.Configure(runtimeSettings, floatingOrigin, focus, workScheduler);

            var soilBreathObject = new GameObject("Soil Thermal Breath");
            soilBreathObject.transform.SetParent(worldSpaceObject.transform, false);
            var soilBreath = soilBreathObject.AddComponent<SteppeSoilBreathPresentation>();
            soilBreath.Configure(runtimeSettings, simulationHost, floatingOrigin, focus);

            var hydrologicalVaporObject = new GameObject("Hydrological Vapor Processes");
            hydrologicalVaporObject.transform.SetParent(worldSpaceObject.transform, false);
            var hydrologicalVapor =
                hydrologicalVaporObject.AddComponent<SteppeHydrologicalVaporPresentation>();
            hydrologicalVapor.Configure(
                runtimeSettings,
                simulationHost,
                floatingOrigin,
                focus);

            var atmosphericTransportObject = new GameObject("Atmospheric Transport Processes");
            atmosphericTransportObject.transform.SetParent(worldSpaceObject.transform, false);
            var atmosphericTransport =
                atmosphericTransportObject.AddComponent<SteppeAtmosphericTransportPresentation>();
            atmosphericTransport.Configure(
                simulationHost,
                floatingOrigin,
                focus,
                cloudLayer);

            var chunkStreamer = gameObject.AddComponent<TerrainChunkStreamer>();
            chunkStreamer.Configure(
                runtimeSettings,
                floatingOrigin,
                focus,
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
                flyCamera,
                null,
                focus,
                timeSystem,
                weatherSystem,
                ecologySystem,
                dustPresentation,
                snowPresentation,
                grassRenderer,
                workScheduler,
                simulationHost);

            var simulationVisualization = gameObject.AddComponent<SteppeSimulationVisualization>();
            simulationVisualization.Configure(simulationHost, floatingOrigin, focus);

            var naturalVisualAtlas = gameObject.AddComponent<SteppeNaturalVisualFieldAtlas>();
            naturalVisualAtlas.Configure(simulationHost);

            var naturalProcessAtlas = gameObject.AddComponent<SteppeNaturalProcessFieldAtlas>();
            naturalProcessAtlas.Configure(simulationHost);

            var macroPresentation = gameObject.AddComponent<SteppeMacroProcessPresentation>();
            macroPresentation.Configure(
                simulationHost,
                floatingOrigin,
                weatherSystem,
                focus,
                worldSpaceObject.transform,
                new TerrainHeightGenerator(runtimeSettings));

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
