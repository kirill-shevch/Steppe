using Steppe.Player;
using Steppe.Settings;
using Steppe.Terrain;
using Steppe.World;
using UnityEngine;

namespace Steppe.Prototype
{
    [DisallowMultipleComponent]
    public sealed class SteppePrototypeBootstrap : MonoBehaviour
    {
        [SerializeField] private SteppeWorldSettings settings;
        [SerializeField] private Material terrainMaterial;

        private SteppeWorldSettings runtimeSettings;
        private bool initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (FindAnyObjectByType<SteppePrototypeBootstrap>() != null)
            {
                return;
            }

            var root = new GameObject("Steppe P0 Prototype");
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

            EnsureDirectionalLight();
            ConfigureAtmosphere();

            var worldSpaceObject = new GameObject("World Space");
            worldSpaceObject.transform.SetParent(transform, false);

            var floatingOrigin = gameObject.AddComponent<FloatingOriginSystem>();
            floatingOrigin.Configure(
                camera.transform,
                worldSpaceObject.transform,
                runtimeSettings.FloatingOriginThreshold,
                runtimeSettings.ChunkSize);

            var chunkStreamer = gameObject.AddComponent<TerrainChunkStreamer>();
            chunkStreamer.Configure(
                runtimeSettings,
                floatingOrigin,
                camera.transform,
                worldSpaceObject.transform,
                terrainMaterial);

            var overlay = gameObject.AddComponent<WorldDebugOverlay>();
            overlay.Configure(
                runtimeSettings,
                floatingOrigin,
                chunkStreamer,
                cameraController,
                camera.transform);

            Application.targetFrameRate = 60;
        }

        private static void EnsureDirectionalLight()
        {
            var lights = FindObjectsByType<Light>();
            for (var index = 0; index < lights.Length; index++)
            {
                if (lights[index].type == LightType.Directional)
                {
                    return;
                }
            }

            var lightObject = new GameObject("Steppe Sun");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(1f, 0.93f, 0.82f);
            light.transform.rotation = Quaternion.Euler(42f, -28f, 0f);
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
