using Steppe.Caravan;
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

            var camera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
            if (camera == null)
            {
                var cameraObject = new GameObject("Steppe Camera");
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }

            var existingFlyController = camera.GetComponent<FlyCameraController>();
            if (existingFlyController != null)
            {
                existingFlyController.enabled = false;
            }

            const float initialX = 32f;
            const float initialZ = -64f;
            var initialGroundHeight = (float)new TerrainHeightGenerator(runtimeSettings).SampleHeight(initialX, initialZ);
            var caravanRig = CaravanDemoFactory.Create(new Vector3(
                initialX,
                initialGroundHeight + 1.05f,
                initialZ));
            var initialWind = new SteppeWeatherModel(runtimeSettings)
                .SampleWind(0.0)
                .SurfaceVelocity;
            var initialDownwind = new Vector3(initialWind.x, 0f, initialWind.y);
            if (initialDownwind.sqrMagnitude > 0.01f)
            {
                caravanRig.Root.transform.rotation = Quaternion.LookRotation(
                    initialDownwind.normalized,
                    Vector3.up);
            }
            caravanRig.Root.transform.SetParent(transform, true);

            camera.transform.position = caravanRig.Root.transform.position + new Vector3(0f, 2.2f, -3f);
            camera.transform.rotation = Quaternion.identity;
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
                caravanRig.Root.transform,
                worldSpaceObject.transform,
                runtimeSettings.FloatingOriginThreshold,
                runtimeSettings.ChunkSize);

            var workScheduler = gameObject.AddComponent<WorldWorkScheduler>();
            workScheduler.Configure(runtimeSettings.WorldWorkBudgetMilliseconds);

            var weatherSystem = gameObject.AddComponent<SteppeWeatherSystem>();
            weatherSystem.Configure(runtimeSettings, timeSystem, floatingOrigin, caravanRig.Root.transform, workScheduler);

            var ecologySystem = gameObject.AddComponent<SteppeEcologySystem>();
            ecologySystem.Configure(
                runtimeSettings,
                timeSystem,
                weatherSystem,
                floatingOrigin,
                caravanRig.Root.transform,
                workScheduler);

            var environment = new CaravanEnvironmentSampler(
                runtimeSettings,
                floatingOrigin,
                weatherSystem,
                ecologySystem);
            caravanRig.Configure(runtimeSettings, floatingOrigin, environment);

            var existingBallCamera = camera.GetComponent<SteppeBallCameraController>();
            if (existingBallCamera != null)
            {
                existingBallCamera.enabled = false;
            }

            var playerObject = new GameObject("Steppe Caravan Keeper");
            playerObject.transform.SetParent(transform, true);
            playerObject.transform.position = caravanRig.PlayerSpawn.position;
            playerObject.AddComponent<CharacterController>();
            var firstPerson = playerObject.AddComponent<CaravanFirstPersonController>();
            firstPerson.Configure(runtimeSettings, floatingOrigin, caravanRig.Chassis, camera);
            var buildMode = playerObject.AddComponent<CaravanBuildModeController>();
            buildMode.Configure(camera, firstPerson, caravanRig.Chassis, caravanRig.MountGrid);
            var interactor = playerObject.AddComponent<CaravanPlayerInteractor>();
            interactor.Configure(camera, firstPerson, buildMode);

            var trackSystem = gameObject.AddComponent<SteppeTrackSystem>();
            trackSystem.Configure(
                runtimeSettings,
                timeSystem,
                floatingOrigin,
                caravanRig.Root.transform,
                caravanRig.Chassis);

            var atmospherePresentation = gameObject.AddComponent<SteppeAtmospherePresentation>();
            atmospherePresentation.Configure(
                runtimeSettings,
                timeSystem,
                weatherSystem,
                ecologySystem,
                floatingOrigin,
                caravanRig.Root.transform);

            var cloudObject = new GameObject("Cloud Layer");
            cloudObject.transform.SetParent(worldSpaceObject.transform, false);
            var cloudLayer = cloudObject.AddComponent<SteppeCloudLayer>();
            cloudLayer.Configure(runtimeSettings, weatherSystem, floatingOrigin);

            var rainObject = new GameObject("Rain Volume");
            rainObject.transform.SetParent(worldSpaceObject.transform, false);
            var rainPresentation = rainObject.AddComponent<SteppeRainPresentation>();
            rainPresentation.Configure(
                runtimeSettings,
                weatherSystem,
                timeSystem,
                floatingOrigin,
                camera.transform,
                rainMaterial);

            var snowObject = new GameObject("Snow Volume");
            snowObject.transform.SetParent(worldSpaceObject.transform, false);
            var snowPresentation = snowObject.AddComponent<SteppeSnowPresentation>();
            snowPresentation.Configure(
                runtimeSettings,
                weatherSystem,
                timeSystem,
                floatingOrigin,
                camera.transform,
                snowMaterial);

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

            var grassObject = new GameObject("Grass Field");
            grassObject.transform.SetParent(worldSpaceObject.transform, false);
            var grassRenderer = grassObject.AddComponent<SteppeGrassRenderer>();
            grassRenderer.Configure(runtimeSettings, floatingOrigin, caravanRig.Root.transform, workScheduler, grassMaterial);

            var chunkStreamer = gameObject.AddComponent<TerrainChunkStreamer>();
            chunkStreamer.Configure(
                runtimeSettings,
                floatingOrigin,
                caravanRig.Root.transform,
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
                caravanRig.Chassis,
                trackSystem,
                caravanRig.Root.transform,
                timeSystem,
                weatherSystem,
                ecologySystem,
                dustPresentation,
                snowPresentation,
                grassRenderer,
                workScheduler);

            var biomeNavigator = gameObject.AddComponent<BiomeDebugNavigator>();
            biomeNavigator.Configure(
                runtimeSettings,
                floatingOrigin,
                caravanRig.Root.transform,
                caravanRig.Chassis);

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
