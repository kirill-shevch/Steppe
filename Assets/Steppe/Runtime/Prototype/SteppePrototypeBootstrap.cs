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
        private Material runtimeBallMaterial;
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
            var playerObject = new GameObject("Steppe Player Ball");
            playerObject.transform.SetParent(transform, false);
            playerObject.transform.position = new Vector3(
                initialX,
                initialGroundHeight + runtimeSettings.PlayerBallRadius + 3f,
                initialZ);
            var playerCollider = playerObject.AddComponent<SphereCollider>();
            playerCollider.radius = runtimeSettings.PlayerBallRadius;
            var playerBody = playerObject.AddComponent<Rigidbody>();
            playerBody.isKinematic = true;

            var ballVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballVisual.name = "Ball Visual";
            ballVisual.transform.SetParent(playerObject.transform, false);
            ballVisual.transform.localScale = Vector3.one * (runtimeSettings.PlayerBallRadius * 2f);
            var visualCollider = ballVisual.GetComponent<Collider>();
            visualCollider.enabled = false;
            Destroy(visualCollider);
            runtimeBallMaterial = CreateBallMaterial();
            ballVisual.GetComponent<MeshRenderer>().sharedMaterial = runtimeBallMaterial;

            camera.transform.position = playerObject.transform.position
                                        + new Vector3(0f, runtimeSettings.PlayerCameraHeight, -runtimeSettings.PlayerCameraDistance);
            camera.transform.rotation = Quaternion.Euler(16f, 0f, 0f);
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
                playerObject.transform,
                worldSpaceObject.transform,
                runtimeSettings.FloatingOriginThreshold,
                runtimeSettings.ChunkSize);

            var workScheduler = gameObject.AddComponent<WorldWorkScheduler>();
            workScheduler.Configure(runtimeSettings.WorldWorkBudgetMilliseconds);

            var weatherSystem = gameObject.AddComponent<SteppeWeatherSystem>();
            weatherSystem.Configure(runtimeSettings, timeSystem, floatingOrigin, playerObject.transform, workScheduler);

            var ecologySystem = gameObject.AddComponent<SteppeEcologySystem>();
            ecologySystem.Configure(
                runtimeSettings,
                timeSystem,
                weatherSystem,
                floatingOrigin,
                playerObject.transform,
                workScheduler);

            var ballController = playerObject.AddComponent<SteppeBallController>();
            ballController.Configure(
                runtimeSettings,
                floatingOrigin,
                ecologySystem,
                camera.transform,
                ballVisual.transform);
            var cameraController = camera.GetComponent<SteppeBallCameraController>();
            if (cameraController == null)
            {
                cameraController = camera.gameObject.AddComponent<SteppeBallCameraController>();
            }
            cameraController.Configure(runtimeSettings, playerObject.transform, floatingOrigin);

            var trackSystem = gameObject.AddComponent<SteppeTrackSystem>();
            trackSystem.Configure(
                runtimeSettings,
                timeSystem,
                floatingOrigin,
                playerObject.transform,
                ballController);

            var atmospherePresentation = gameObject.AddComponent<SteppeAtmospherePresentation>();
            atmospherePresentation.Configure(
                runtimeSettings,
                timeSystem,
                weatherSystem,
                ecologySystem,
                floatingOrigin,
                playerObject.transform);

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
            grassRenderer.Configure(runtimeSettings, floatingOrigin, playerObject.transform, workScheduler, grassMaterial);

            var chunkStreamer = gameObject.AddComponent<TerrainChunkStreamer>();
            chunkStreamer.Configure(
                runtimeSettings,
                floatingOrigin,
                playerObject.transform,
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
                ballController,
                trackSystem,
                playerObject.transform,
                timeSystem,
                weatherSystem,
                ecologySystem,
                dustPresentation,
                snowPresentation,
                grassRenderer,
                workScheduler);

            var biomeNavigator = gameObject.AddComponent<BiomeDebugNavigator>();
            biomeNavigator.Configure(runtimeSettings, floatingOrigin, playerObject.transform, ballController);

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

        private static Material CreateBallMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                         ?? Shader.Find("Standard");
            if (shader == null)
            {
                throw new System.InvalidOperationException("No supported player-ball shader was found.");
            }

            var material = new Material(shader)
            {
                name = "Steppe Player Ball Material",
                hideFlags = HideFlags.DontSave
            };
            var color = new Color(0.18f, 0.22f, 0.24f, 1f);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.34f);
            }

            return material;
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

            if (runtimeBallMaterial != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(runtimeBallMaterial);
                }
                else
                {
                    DestroyImmediate(runtimeBallMaterial);
                }
            }
        }
    }
}
