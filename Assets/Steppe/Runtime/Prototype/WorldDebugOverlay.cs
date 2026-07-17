using Steppe.Player;
using Steppe.Rendering;
using Steppe.Settings;
using Steppe.Surface;
using Steppe.Terrain;
using Steppe.Time;
using Steppe.Weather;
using Steppe.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Steppe.Prototype
{
    public sealed class WorldDebugOverlay : MonoBehaviour
    {
        private SteppeWorldSettings settings;
        private FloatingOriginSystem floatingOrigin;
        private TerrainChunkStreamer chunkStreamer;
        private FlyCameraController cameraController;
        private Transform focus;
        private TerrainHeightGenerator terrainGenerator;
        private SteppeSurfaceGenerator surfaceGenerator;
        private SteppeTimeSystem timeSystem;
        private SteppeClimateModel climateModel;
        private SteppeWeatherSystem weatherSystem;
        private SteppeGrassRenderer grassRenderer;
        private bool visible = true;

        public void Configure(
            SteppeWorldSettings worldSettings,
            FloatingOriginSystem origin,
            TerrainChunkStreamer streamer,
            FlyCameraController controller,
            Transform focusTransform,
            SteppeTimeSystem clock,
            SteppeWeatherSystem weather,
            SteppeGrassRenderer grass)
        {
            settings = worldSettings;
            floatingOrigin = origin;
            chunkStreamer = streamer;
            cameraController = controller;
            focus = focusTransform;
            timeSystem = clock;
            weatherSystem = weather;
            grassRenderer = grass;
            terrainGenerator = new TerrainHeightGenerator(settings);
            surfaceGenerator = new SteppeSurfaceGenerator(settings);
            climateModel = new SteppeClimateModel(settings);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
            {
                visible = !visible;
            }
        }

        private void OnGUI()
        {
            if (!visible || settings == null || floatingOrigin == null || chunkStreamer == null || focus == null || timeSystem == null || weatherSystem == null)
            {
                return;
            }

            var worldPosition = floatingOrigin.LocalToWorld(focus.position);
            var groundHeight = terrainGenerator.SampleHeight(worldPosition.X, worldPosition.Z);
            var groundNormal = terrainGenerator.SampleNormal(worldPosition.X, worldPosition.Z, 2.0);
            var surface = surfaceGenerator.Sample(
                worldPosition.X,
                worldPosition.Z,
                groundHeight,
                groundNormal.y);
            var time = timeSystem.Current;
            var climate = climateModel.Evaluate(surface, time);
            var solar = SteppeAstronomy.Evaluate(time, settings.LatitudeDegrees);
            var weather = weatherSystem.CurrentAtFocus;
            chunkStreamer.GetLodCounts(out var near, out var middle, out var far);

            const float width = 470f;
            const float height = 350f;
            var area = new Rect(12f, 12f, width, height);
            GUI.Box(area, GUIContent.none);

            GUILayout.BeginArea(new Rect(area.x + 12f, area.y + 10f, width - 24f, height - 20f));
            GUILayout.Label("STEPPE - P4 VEGETATION RENDERER");
            GUILayout.Label($"World XZ: {worldPosition.X:F1}, {worldPosition.Z:F1} m    Altitude: {worldPosition.Y:F1} m");
            GUILayout.Label($"Biome: {surface.DominantBiome}    Climate: {surface.MeanAnnualPrecipitationMm:F0} mm/y, {surface.MeanAnnualTemperatureC:F1} C");
            GUILayout.Label($"Mix: meadow {surface.Biomes.Meadow:P0} / feather {surface.Biomes.FeatherGrass:P0} / dry {surface.Biomes.Dry:P0} / desert {surface.Biomes.Desert:P0}");
            GUILayout.Label($"Cover: {surface.VegetationPotential:P0}    Dust: {surface.DustPotential:P0}    Wind coherence: {surface.WindCoherence:P0}");
            GUILayout.Label($"Year {time.Year + 1}, day {time.DayOfYear:F1}/{settings.DaysPerYear}    {time.Hour:00.00} h    {time.Season}");
            GUILayout.Label($"Air: {climate.AirTemperatureC:F1} C    Sun: {solar.ElevationDegrees:F1} deg    Clock: x{timeSystem.DebugMultiplier:F0}{(timeSystem.IsPaused ? " PAUSED" : string.Empty)}");
            GUILayout.Label($"Wind: {weather.Wind.x:F1}, {weather.Wind.y:F1} m/s    Clouds: {weather.CloudCoverage:P0}    Water: {weather.CloudWater:P0}    Rain: {weather.RainIntensity:P0}");
            GUILayout.Label($"Weather map: {(weatherSystem.IsWeatherMapReady ? $"ready v{weatherSystem.MapRevision}" : "building")}    Max clouds: {weatherSystem.MapMaximumCoverage:P0}    Max water: {weatherSystem.MapMaximumWater:P0}");
            GUILayout.Label($"Chunk: {chunkStreamer.CenterCoordinate}    Origin XZ: {floatingOrigin.OriginX:F0}, {floatingOrigin.OriginZ:F0}");
            GUILayout.Label($"Chunks: {chunkStreamer.LoadedCount} loaded / {chunkStreamer.PendingCount} queued    LOD: {near}/{middle}/{far}");
            GUILayout.Label(grassRenderer != null && grassRenderer.IsRendering
                ? $"Grass: {grassRenderer.InstanceCount:N0} tufts / {grassRenderer.LoadedCellCount} cells / {grassRenderer.PendingCount} queued / {grassRenderer.TuftVertexCount}v {grassRenderer.TuftTriangleCount}t / {(grassRenderer.UsesAuthoredMesh ? "Nobiax CC0 mesh" : "procedural fallback")}"
                : "Grass: P1 CPU fallback");
            GUILayout.Label($"Seed: {settings.WorldSeed}    Terrain: v{settings.GeneratorVersion}    Surface: v{settings.SurfaceVersion}    Speed: {cameraController.CurrentMoveSpeed:F0} m/s");
            GUILayout.Space(4f);
            GUILayout.Label("WASD move - mouse look - Q/E down/up - Shift boost - wheel speed");
            GUILayout.Label("1-4 biomes - F3 panel - F5 pause time - F6 accelerate time");
            GUILayout.EndArea();
        }
    }
}
