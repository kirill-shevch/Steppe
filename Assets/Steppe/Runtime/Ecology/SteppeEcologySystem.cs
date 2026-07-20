using System;
using System.Collections.Generic;
using Steppe.Settings;
using Steppe.Surface;
using Steppe.Terrain;
using Steppe.Time;
using Steppe.Weather;
using Steppe.World;
using UnityEngine;

namespace Steppe.Ecology
{
    /// <summary>
    /// Sparse, session-persistent ecology grid. Active cells share the world-work
    /// budget, while records remain in memory when terrain and grass chunks unload.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class SteppeEcologySystem : MonoBehaviour, IWorldWorkSource
    {
        private const double TimeEpsilon = 0.001;
        private const double ShaderCoordinatePeriod = 65536.0;
        private static readonly int StateMapId = Shader.PropertyToID("_SteppeEcologyStateMap");
        private static readonly int CryosphereMapId = Shader.PropertyToID("_SteppeCryosphereStateMap");
        private static readonly int MapOriginSizeId = Shader.PropertyToID("_SteppeEcologyMapOriginSize");
        private static readonly int MapParametersId = Shader.PropertyToID("_SteppeEcologyMapParameters");

        private sealed class EcoCellRecord
        {
            public EcoCellRecord(SurfaceSample surface, SteppeEcoCellState state)
            {
                Surface = surface;
                State = state;
            }

            public SurfaceSample Surface { get; }
            public SteppeEcoCellState State { get; set; }
        }

        private readonly Dictionary<EcoCellCoordinate, EcoCellRecord> records =
            new Dictionary<EcoCellCoordinate, EcoCellRecord>();
        private readonly HashSet<EcoCellCoordinate> activeCells = new HashSet<EcoCellCoordinate>();
        private readonly Queue<EcoCellCoordinate> priorityWorkQueue = new Queue<EcoCellCoordinate>();
        private readonly Queue<EcoCellCoordinate> workQueue = new Queue<EcoCellCoordinate>();
        private readonly HashSet<EcoCellCoordinate> queuedCells = new HashSet<EcoCellCoordinate>();

        private SteppeWorldSettings settings;
        private SteppeTimeSystem timeSystem;
        private SteppeWeatherSystem weatherSystem;
        private FloatingOriginSystem floatingOrigin;
        private Transform focus;
        private WorldWorkScheduler workScheduler;
        private TerrainHeightGenerator terrainGenerator;
        private SteppeSurfaceGenerator surfaceGenerator;
        private SteppeClimateModel climateModel;
        private SteppeEcologyModel ecologyModel;
        private EcoCellCoordinate centerCoordinate;
        private bool hasCenterCoordinate;
        private double targetSimulationSeconds;
        private Texture2D stateMap;
        private Color32[] statePixels;
        private Texture2D cryosphereMap;
        private Color32[] cryospherePixels;
        private EcoCellCoordinate mapOriginCoordinate;
        private bool hasMapOrigin;
        private bool stateMapDirty;
        private float secondsUntilStateMapUpload;

        public int ActiveCellCount => activeCells.Count;
        public int StoredCellCount => records.Count;
        public int PendingCellCount => priorityWorkQueue.Count + workQueue.Count;
        public double TargetSimulationSeconds => targetSimulationSeconds;
        public EcoCellCoordinate CenterCoordinate => centerCoordinate;
        public Texture2D StateMap => stateMap;
        public Texture2D CryosphereMap => cryosphereMap;
        public bool IsStateMapReady => MapRevision > 0;
        public int MapRevision { get; private set; }
        public EcoCellCoordinate MapOriginCoordinate => mapOriginCoordinate;
        public float MapWorldSize => settings != null ? settings.EcologyStateMapWorldSize : 0f;
        public float MapMaximumSurfaceWater { get; private set; }
        public float MapMaximumCrust { get; private set; }
        public float MapMaximumSnow { get; private set; }
        public float MapMaximumFrozen { get; private set; }
        public bool HasPendingWorldWork => priorityWorkQueue.Count > 0 || workQueue.Count > 0;

        public void Configure(
            SteppeWorldSettings worldSettings,
            SteppeTimeSystem clock,
            SteppeWeatherSystem weather,
            FloatingOriginSystem origin,
            Transform focusTransform,
            WorldWorkScheduler scheduler)
        {
            settings = worldSettings != null ? worldSettings : throw new ArgumentNullException(nameof(worldSettings));
            timeSystem = clock != null ? clock : throw new ArgumentNullException(nameof(clock));
            weatherSystem = weather != null ? weather : throw new ArgumentNullException(nameof(weather));
            floatingOrigin = origin != null ? origin : throw new ArgumentNullException(nameof(origin));
            focus = focusTransform != null ? focusTransform : throw new ArgumentNullException(nameof(focusTransform));
            workScheduler = scheduler != null ? scheduler : throw new ArgumentNullException(nameof(scheduler));

            terrainGenerator = new TerrainHeightGenerator(settings);
            surfaceGenerator = new SteppeSurfaceGenerator(settings);
            climateModel = new SteppeClimateModel(settings);
            ecologyModel = new SteppeEcologyModel(settings);
            targetSimulationSeconds = AlignToCompletedStep(timeSystem.ElapsedSimulationSeconds);
            CreateStateMap();

            var focusWorld = floatingOrigin.LocalToWorld(focus.position);
            RefreshActiveCells(focusWorld.X, focusWorld.Z);
            workScheduler.Register(this);
        }

        public bool TryGetState(double worldX, double worldZ, out SteppeEcoCellState state)
        {
            if (settings == null)
            {
                state = default;
                return false;
            }

            return TryGetState(EcoCellCoordinate.FromWorld(worldX, worldZ, settings.EcologyCellSize), out state);
        }

        public bool TryGetState(EcoCellCoordinate coordinate, out SteppeEcoCellState state)
        {
            if (records.TryGetValue(coordinate, out var record))
            {
                state = record.State;
                return true;
            }

            state = default;
            return false;
        }

        public bool TryGetCell(
            EcoCellCoordinate coordinate,
            out SurfaceSample surface,
            out SteppeEcoCellState state)
        {
            if (records.TryGetValue(coordinate, out var record))
            {
                surface = record.Surface;
                state = record.State;
                return true;
            }

            surface = default;
            state = default;
            return false;
        }

        public bool IsActive(EcoCellCoordinate coordinate)
        {
            return activeCells.Contains(coordinate);
        }

        public bool TryGetMapPixelCoordinate(EcoCellCoordinate coordinate, out int x, out int z)
        {
            if (!hasMapOrigin || settings == null)
            {
                x = 0;
                z = 0;
                return false;
            }

            var relativeX = coordinate.X - mapOriginCoordinate.X;
            var relativeZ = coordinate.Z - mapOriginCoordinate.Z;
            var resolution = settings.EcologyStateMapResolution;
            if (relativeX < 0 || relativeZ < 0 || relativeX >= resolution || relativeZ >= resolution)
            {
                x = 0;
                z = 0;
                return false;
            }

            x = (int)relativeX;
            z = (int)relativeZ;
            return true;
        }

        private void Update()
        {
            if (settings == null || timeSystem == null || floatingOrigin == null || focus == null)
            {
                return;
            }

            var focusWorld = floatingOrigin.LocalToWorld(focus.position);
            var nextCenter = EcoCellCoordinate.FromWorld(
                focusWorld.X,
                focusWorld.Z,
                settings.EcologyCellSize);
            if (!hasCenterCoordinate || nextCenter != centerCoordinate)
            {
                RefreshActiveCells(focusWorld.X, focusWorld.Z);
            }

            var nextTarget = AlignToCompletedStep(timeSystem.ElapsedSimulationSeconds);
            if (nextTarget > targetSimulationSeconds + TimeEpsilon)
            {
                targetSimulationSeconds = nextTarget;
                QueuePriorityCellIfNeeded(centerCoordinate);
                QueueAllActiveCells();
            }

            secondsUntilStateMapUpload -= UnityEngine.Time.deltaTime;
        }

        private void LateUpdate()
        {
            if (stateMapDirty && secondsUntilStateMapUpload <= 0f)
            {
                UploadStateMap();
            }
        }

        public void ExecuteWorldWorkStep()
        {
            if (priorityWorkQueue.Count == 0 && workQueue.Count == 0)
            {
                return;
            }

            var coordinate = priorityWorkQueue.Count > 0
                ? priorityWorkQueue.Dequeue()
                : workQueue.Dequeue();
            queuedCells.Remove(coordinate);
            if (!activeCells.Contains(coordinate))
            {
                return;
            }

            if (!records.TryGetValue(coordinate, out var record))
            {
                record = CreateRecord(coordinate);
                records.Add(coordinate, record);
                WriteRecordToStateMap(coordinate, record);
            }

            if (record.State.LastSimulationSeconds + TimeEpsilon < targetSimulationSeconds)
            {
                AdvanceOneStep(coordinate, record);
            }

            if (record.State.LastSimulationSeconds + TimeEpsilon < targetSimulationSeconds)
            {
                QueueCellForCatchup(coordinate);
            }
        }

        private EcoCellRecord CreateRecord(EcoCellCoordinate coordinate)
        {
            var cellSize = settings.EcologyCellSize;
            var worldX = coordinate.CenterX(cellSize);
            var worldZ = coordinate.CenterZ(cellSize);
            var height = terrainGenerator.SampleHeight(worldX, worldZ);
            var normal = terrainGenerator.SampleNormal(worldX, worldZ, Math.Max(2.0, cellSize * 0.04));
            var surface = surfaceGenerator.Sample(worldX, worldZ, height, normal.y);
            var completeWarmupSteps = Math.Floor(
                settings.EcologyWarmupSimulationSeconds
                / settings.EcologySimulationStepSeconds);
            var warmupSeconds = completeWarmupSteps * settings.EcologySimulationStepSeconds;
            var initialSeconds = Math.Max(0.0, targetSimulationSeconds - warmupSeconds);
            var initialTime = SteppeTimeSystem.CreateSnapshot(settings, initialSeconds);
            var initialClimate = climateModel.Evaluate(surface, initialTime);
            var state = ecologyModel.CreateInitialState(
                surface,
                initialClimate.AirTemperatureC,
                initialSeconds);
            return new EcoCellRecord(surface, state);
        }

        private void AdvanceOneStep(EcoCellCoordinate coordinate, EcoCellRecord record)
        {
            var startSeconds = record.State.LastSimulationSeconds;
            var endSeconds = Math.Min(
                targetSimulationSeconds,
                startSeconds + settings.EcologySimulationStepSeconds);
            var durationSeconds = endSeconds - startSeconds;
            var midpointSeconds = startSeconds + durationSeconds * 0.5;
            var midpointTime = SteppeTimeSystem.CreateSnapshot(settings, midpointSeconds);
            var climate = climateModel.Evaluate(record.Surface, midpointTime);
            var solar = SteppeAstronomy.Evaluate(midpointTime, settings.LatitudeDegrees);
            var weatherSeconds = SteppeWeatherTime.FromSimulationSeconds(settings, midpointSeconds);
            var weather = weatherSystem.Model.Sample(
                coordinate.CenterX(settings.EcologyCellSize),
                coordinate.CenterZ(settings.EcologyCellSize),
                weatherSeconds);
            var forcing = new SteppeEcologyForcing(
                weather.RainIntensity,
                weather.SurfaceWind.magnitude,
                climate.AirTemperatureC,
                solar.Daylight);
            record.State = ecologyModel.Advance(
                record.State,
                record.Surface,
                forcing,
                durationSeconds);
            WriteRecordToStateMap(coordinate, record);
        }

        private void RefreshActiveCells(double focusWorldX, double focusWorldZ)
        {
            centerCoordinate = EcoCellCoordinate.FromWorld(
                focusWorldX,
                focusWorldZ,
                settings.EcologyCellSize);
            hasCenterCoordinate = true;
            activeCells.Clear();
            EnsureStateMapCoverage();

            var cellSize = settings.EcologyCellSize;
            var cellRadius = Mathf.CeilToInt(settings.EcologyActiveRadius / cellSize) + 1;
            var inclusionRadius = settings.EcologyActiveRadius + cellSize * 0.72;
            var inclusionRadiusSquared = (double)inclusionRadius * inclusionRadius;
            for (var z = -cellRadius; z <= cellRadius; z++)
            {
                for (var x = -cellRadius; x <= cellRadius; x++)
                {
                    var coordinate = new EcoCellCoordinate(centerCoordinate.X + x, centerCoordinate.Z + z);
                    var offsetX = coordinate.CenterX(cellSize) - focusWorldX;
                    var offsetZ = coordinate.CenterZ(cellSize) - focusWorldZ;
                    if (offsetX * offsetX + offsetZ * offsetZ > inclusionRadiusSquared)
                    {
                        continue;
                    }

                    activeCells.Add(coordinate);
                }
            }

            // Work outside the new horizon is deferred until the player returns. All
            // active cells are requeued, so no relevant catch-up work is lost.
            priorityWorkQueue.Clear();
            workQueue.Clear();
            queuedCells.Clear();

            // The cell under the player is useful to both gameplay and diagnostics,
            // so it is reconstructed first; the remaining horizon stays budgeted.
            QueuePriorityCellIfNeeded(centerCoordinate);
            foreach (var coordinate in activeCells)
            {
                QueueCellIfNeeded(coordinate);
            }
        }

        private void QueueAllActiveCells()
        {
            foreach (var coordinate in activeCells)
            {
                QueueCell(coordinate);
            }
        }

        private void QueueCell(EcoCellCoordinate coordinate)
        {
            if (!queuedCells.Add(coordinate))
            {
                return;
            }

            workQueue.Enqueue(coordinate);
        }

        private void QueueCellIfNeeded(EcoCellCoordinate coordinate)
        {
            if (!records.TryGetValue(coordinate, out var record)
                || record.State.LastSimulationSeconds + TimeEpsilon < targetSimulationSeconds)
            {
                QueueCell(coordinate);
            }
        }

        private void QueuePriorityCellIfNeeded(EcoCellCoordinate coordinate)
        {
            if (records.TryGetValue(coordinate, out var record)
                && record.State.LastSimulationSeconds + TimeEpsilon >= targetSimulationSeconds)
            {
                return;
            }

            if (queuedCells.Add(coordinate))
            {
                priorityWorkQueue.Enqueue(coordinate);
            }
        }

        private void QueueCellForCatchup(EcoCellCoordinate coordinate)
        {
            if (coordinate == centerCoordinate)
            {
                QueuePriorityCellIfNeeded(coordinate);
            }
            else
            {
                QueueCell(coordinate);
            }
        }

        private double AlignToCompletedStep(double simulationSeconds)
        {
            var step = settings.EcologySimulationStepSeconds;
            return Math.Floor(Math.Max(0.0, simulationSeconds) / step) * step;
        }

        private void CreateStateMap()
        {
            var resolution = settings.EcologyStateMapResolution;
            stateMap = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true)
            {
                name = "Steppe Ecology State Map (water, biomass, green, crust)",
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 1
            };
            statePixels = new Color32[resolution * resolution];
            cryosphereMap = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true)
            {
                name = "Steppe Cryosphere State Map (snow, compaction, frozen)",
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 1
            };
            cryospherePixels = new Color32[resolution * resolution];
        }

        private void EnsureStateMapCoverage()
        {
            var resolution = settings.EcologyStateMapResolution;
            if (!hasMapOrigin)
            {
                RecenterStateMap();
                return;
            }

            var mapCenterX = mapOriginCoordinate.X + resolution / 2;
            var mapCenterZ = mapOriginCoordinate.Z + resolution / 2;
            var activeRadiusInCells = Mathf.CeilToInt(settings.EcologyActiveRadius / settings.EcologyCellSize);
            var recenterThreshold = Mathf.Max(1, resolution / 2 - activeRadiusInCells - 2);
            if (Math.Abs(centerCoordinate.X - mapCenterX) >= recenterThreshold
                || Math.Abs(centerCoordinate.Z - mapCenterZ) >= recenterThreshold)
            {
                RecenterStateMap();
            }
        }

        private void RecenterStateMap()
        {
            var resolution = settings.EcologyStateMapResolution;
            mapOriginCoordinate = new EcoCellCoordinate(
                centerCoordinate.X - resolution / 2,
                centerCoordinate.Z - resolution / 2);
            hasMapOrigin = true;
            for (var z = 0; z < resolution; z++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    var coordinate = new EcoCellCoordinate(
                        mapOriginCoordinate.X + x,
                        mapOriginCoordinate.Z + z);
                    var pixelIndex = z * resolution + x;
                    if (records.TryGetValue(coordinate, out var record))
                    {
                        statePixels[pixelIndex] = SteppeEcologyMapEncoding.Encode(record.State, record.Surface);
                        cryospherePixels[pixelIndex] = SteppeCryosphereMapEncoding.Encode(record.State);
                    }
                    else
                    {
                        statePixels[pixelIndex] = SteppeEcologyMapEncoding.Neutral;
                        cryospherePixels[pixelIndex] = SteppeCryosphereMapEncoding.Neutral;
                    }
                }
            }

            stateMapDirty = true;
            UploadStateMap();
        }

        private void WriteRecordToStateMap(EcoCellCoordinate coordinate, EcoCellRecord record)
        {
            if (!TryGetMapPixelCoordinate(coordinate, out var x, out var z))
            {
                return;
            }

            var pixelIndex = z * settings.EcologyStateMapResolution + x;
            statePixels[pixelIndex] = SteppeEcologyMapEncoding.Encode(record.State, record.Surface);
            cryospherePixels[pixelIndex] = SteppeCryosphereMapEncoding.Encode(record.State);
            stateMapDirty = true;
        }

        private void UploadStateMap()
        {
            if (stateMap == null
                || statePixels == null
                || cryosphereMap == null
                || cryospherePixels == null)
            {
                return;
            }

            var maximumWater = 0f;
            var maximumCrust = 0f;
            var maximumSnow = 0f;
            var maximumFrozen = 0f;
            for (var index = 0; index < statePixels.Length; index++)
            {
                maximumWater = Mathf.Max(maximumWater, statePixels[index].r / 255f);
                maximumCrust = Mathf.Max(maximumCrust, statePixels[index].a / 255f);
                maximumSnow = Mathf.Max(maximumSnow, cryospherePixels[index].r / 255f);
                maximumFrozen = Mathf.Max(maximumFrozen, cryospherePixels[index].b / 255f);
            }

            stateMap.SetPixels32(statePixels);
            stateMap.Apply(false, false);
            cryosphereMap.SetPixels32(cryospherePixels);
            cryosphereMap.Apply(false, false);
            MapMaximumSurfaceWater = maximumWater;
            MapMaximumCrust = maximumCrust;
            MapMaximumSnow = maximumSnow;
            MapMaximumFrozen = maximumFrozen;
            MapRevision++;
            stateMapDirty = false;
            secondsUntilStateMapUpload = settings.EcologyStateMapUploadInterval;
            PublishStateMapShaderState();
        }

        private void PublishStateMapShaderState()
        {
            var originX = mapOriginCoordinate.X * (double)settings.EcologyCellSize;
            var originZ = mapOriginCoordinate.Z * (double)settings.EcologyCellSize;
            var worldSize = settings.EcologyStateMapWorldSize;
            Shader.SetGlobalTexture(StateMapId, stateMap);
            Shader.SetGlobalTexture(CryosphereMapId, cryosphereMap);
            Shader.SetGlobalVector(MapOriginSizeId, new Vector4(
                PositiveModulo(originX, ShaderCoordinatePeriod),
                PositiveModulo(originZ, ShaderCoordinatePeriod),
                1f / worldSize,
                worldSize));
            Shader.SetGlobalVector(MapParametersId, new Vector4(
                settings.EcologyStateMapResolution,
                1f,
                settings.EcologyCellSize,
                (float)ShaderCoordinatePeriod));
        }

        private static float PositiveModulo(double value, double modulus)
        {
            return (float)(value - Math.Floor(value / modulus) * modulus);
        }

        private void OnDestroy()
        {
            if (workScheduler != null)
            {
                workScheduler.Unregister(this);
            }
            Shader.SetGlobalVector(MapParametersId, Vector4.zero);
            if (stateMap != null && Application.isPlaying)
            {
                Destroy(stateMap);
            }
            else if (stateMap != null)
            {
                DestroyImmediate(stateMap);
            }

            if (cryosphereMap != null && Application.isPlaying)
            {
                Destroy(cryosphereMap);
            }
            else if (cryosphereMap != null)
            {
                DestroyImmediate(cryosphereMap);
            }
        }
    }
}
