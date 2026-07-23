using Steppe.Ecology;
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
        private ISteppeTravelFocus travelFocus;
        private SteppeTrackSystem trackSystem;
        private Transform focus;
        private TerrainHeightGenerator terrainGenerator;
        private SteppeSurfaceGenerator surfaceGenerator;
        private SteppeTimeSystem timeSystem;
        private SteppeClimateModel climateModel;
        private SteppeWeatherSystem weatherSystem;
        private SteppeEcologySystem ecologySystem;
        private SteppeDustPresentation dustPresentation;
        private SteppeSnowPresentation snowPresentation;
        private SteppeGrassRenderer grassRenderer;
        private WorldWorkScheduler workScheduler;
        private bool visible;

        public void Configure(
            SteppeWorldSettings worldSettings,
            FloatingOriginSystem origin,
            TerrainChunkStreamer streamer,
            ISteppeTravelFocus traveller,
            SteppeTrackSystem tracks,
            Transform focusTransform,
            SteppeTimeSystem clock,
            SteppeWeatherSystem weather,
            SteppeEcologySystem ecology,
            SteppeDustPresentation dust,
            SteppeSnowPresentation snow,
            SteppeGrassRenderer grass,
            WorldWorkScheduler scheduler)
        {
            settings = worldSettings;
            floatingOrigin = origin;
            chunkStreamer = streamer;
            travelFocus = traveller;
            trackSystem = tracks;
            focus = focusTransform;
            timeSystem = clock;
            weatherSystem = weather;
            ecologySystem = ecology;
            dustPresentation = dust;
            snowPresentation = snow;
            grassRenderer = grass;
            workScheduler = scheduler;
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
            const float height = 552f;
            var area = new Rect(12f, 12f, width, height);
            GUI.Box(area, GUIContent.none);

            GUILayout.BeginArea(new Rect(area.x + 12f, area.y + 10f, width - 24f, height - 20f));
            GUILayout.Label("STEPPE - P12 PHYSICAL LIVING SURFACE");
            GUILayout.Label($"World XZ: {worldPosition.X:F1}, {worldPosition.Z:F1} m    Altitude: {worldPosition.Y:F1} m");
            GUILayout.Label($"Biome: {surface.DominantBiome}    Climate: {surface.MeanAnnualPrecipitationMm:F0} mm/y, {surface.MeanAnnualTemperatureC:F1} C");
            GUILayout.Label($"Mix: meadow {surface.Biomes.Meadow:P0} / feather {surface.Biomes.FeatherGrass:P0} / dry {surface.Biomes.Dry:P0} / desert {surface.Biomes.Desert:P0}");
            GUILayout.Label($"Cover: {surface.VegetationPotential:P0}    Dust: {surface.DustPotential:P0}    Wind coherence: {surface.WindCoherence:P0}");
            GUILayout.Label($"Year {time.Year + 1}, day {time.DayOfYear:F1}/{settings.DaysPerYear}    {time.Hour:00.00} h    {time.Season}");
            GUILayout.Label($"Air: {climate.AirTemperatureC:F1} C    Sun: {solar.ElevationDegrees:F1} deg    Clock: x{timeSystem.DebugMultiplier:F0}{(timeSystem.IsPaused ? " PAUSED" : string.Empty)}");
            GUILayout.Label(
                $"Wind surface: {weather.SurfaceWind.x:F1}, {weather.SurfaceWind.y:F1} m/s    "
                + $"cloud: {weather.CloudWind.x:F1}, {weather.CloudWind.y:F1} m/s    gust: {weather.StormGust:P0}");
            GUILayout.Label($"Clouds: {weather.CloudCoverage:P0}    Water: {weather.CloudWater:P0}    Precip: {weather.RainIntensity:P0}");
            GUILayout.Label($"Weather map: {(weatherSystem.IsWeatherMapReady ? $"ready v{weatherSystem.MapRevision}" : "building")}    Max clouds: {weatherSystem.MapMaximumCoverage:P0}    Max water: {weatherSystem.MapMaximumWater:P0}    Max rain: {weatherSystem.MapMaximumRain:P0}    Max gust: {weatherSystem.MapMaximumGust:P0}");
            if (ecologySystem != null && ecologySystem.TryGetState(worldPosition.X, worldPosition.Z, out var ecology))
            {
                var lagHours = System.Math.Max(
                    0.0,
                    ecologySystem.TargetSimulationSeconds - ecology.LastSimulationSeconds) / 3600.0;
                var biomassCapacity = System.Math.Max(0.001, surface.VegetationPotential);
                var relativeBiomass = ecology.Biomass / biomassCapacity;
                var relativeLiveBiomass = ecology.LiveBiomass / biomassCapacity;
                var relativeDryBiomass = ecology.DryBiomass / biomassCapacity;
                GUILayout.Label(
                    $"Soil: surface {ecology.SurfaceWater:P0} / root {ecology.RootWater:P0} / crust {ecology.SurfaceCrust:P0}    "
                    + $"Plants: mass {relativeBiomass:P0} / live {relativeLiveBiomass:P0} / dry {relativeDryBiomass:P0}    "
                    + $"Eco: {ecologySystem.StoredCellCount} stored / {ecologySystem.ActiveCellCount} active / {ecologySystem.PendingCellCount} queued / {lagHours:F1}h lag");
                GUILayout.Label(
                    $"Snow: water {ecology.SnowWater:P0} / compact {ecology.SnowCompaction:P0} / frozen soil {ecology.FrozenFraction:P0}    "
                    + (snowPresentation != null
                        ? $"fall shown {snowPresentation.DisplayedIntensity:P0} / snow phase {snowPresentation.CurrentSnowFraction:P0}"
                        : "no snow presentation"));
            }
            else if (ecologySystem != null)
            {
                GUILayout.Label(
                    $"Soil: reconstructing    Eco: {ecologySystem.StoredCellCount} stored / {ecologySystem.ActiveCellCount} active / {ecologySystem.PendingCellCount} queued");
            }
            if (ecologySystem != null)
            {
                GUILayout.Label(
                    $"Eco map: {(ecologySystem.IsStateMapReady ? $"ready v{ecologySystem.MapRevision}" : "building")}    "
                    + $"{ecologySystem.StateMap.width}x{ecologySystem.StateMap.height} / {ecologySystem.MapWorldSize / 1000f:F1} km    "
                    + $"max wet {ecologySystem.MapMaximumSurfaceWater:P0} / crust {ecologySystem.MapMaximumCrust:P0} / "
                    + $"snow {ecologySystem.MapMaximumSnow:P0} / frozen {ecologySystem.MapMaximumFrozen:P0}");
            }
            if (dustPresentation != null)
            {
                var dust = dustPresentation.CurrentAtFocus;
                GUILayout.Label(
                    $"Dust now: {dust.Emission:P0} / shown {dustPresentation.DisplayedEmission:P0}    "
                    + $"threshold {dust.ThresholdWindSpeed:F1} m/s / dry {dust.SurfaceDryness:P0} / loose {dust.Looseness:P0}    "
                    + $"particles {dustPresentation.Particles.particleCount}");
            }
            if (travelFocus != null)
            {
                var traversal = travelFocus.CurrentSurface;
                GUILayout.Label(
                    $"Traversal: resistance {traversal.Resistance:P0} / mud {traversal.Mud:P0} / loose {traversal.LooseGround:P0} / "
                    + $"snow sink {traversal.SnowSink:P0} / frozen firm {traversal.FrozenFirmness:P0} / depth {traversal.SinkDepth:F2} m");
            }
            if (trackSystem != null)
            {
                var track = trackSystem.CurrentAtFocus;
                GUILayout.Label(
                    $"Tracks: plants {track.VegetationFlattening:P0} / snow {track.SnowCompression:P0} / rut {track.SoilRut:P0} / wet {track.WetPrint:P0}    "
                    + $"{trackSystem.StoredTrackCellCount} stored / map v{trackSystem.MapRevision}");
            }
            GUILayout.Label($"Chunk: {chunkStreamer.CenterCoordinate}    Origin XZ: {floatingOrigin.OriginX:F0}, {floatingOrigin.OriginZ:F0}");
            GUILayout.Label($"Chunks: {chunkStreamer.LoadedCount} loaded / {chunkStreamer.PendingCount} queued    LOD: {near}/{middle}/{far}");
            GUILayout.Label(grassRenderer != null && grassRenderer.IsRendering
                ? $"Grass: {grassRenderer.InstanceCount:N0} tufts / {grassRenderer.LoadedCellCount} cells / {grassRenderer.PendingCount} queued / {grassRenderer.TuftVertexCount}v {grassRenderer.TuftTriangleCount}t / {(grassRenderer.UsesAuthoredMesh ? "Nobiax CC0 mesh" : "procedural fallback")}"
                : "Grass: P1 CPU fallback");
            GUILayout.Label($"World work: {workScheduler.LastFrameWorkMilliseconds:F2} ms / {workScheduler.LastFrameStepCount} steps / {workScheduler.RegisteredSourceCount} sources");
            GUILayout.Label($"Seed: {settings.WorldSeed}    Terrain: v{settings.GeneratorVersion}    Surface: v{settings.SurfaceVersion}    Speed: {(travelFocus != null ? travelFocus.Speed : 0f):F1} m/s");
            GUILayout.Space(4f);
            GUILayout.Label("WASD walk - mouse look - Shift run - Space jump - E use");
            GUILayout.Label("C clean - R repair - B build mode");
            GUILayout.Label("1-4 biomes - F3 panel - F5 pause time - F6 accelerate time");
            GUILayout.EndArea();
        }
    }
}
