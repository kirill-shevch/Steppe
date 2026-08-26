using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Steppe.Simulation;
using UnityEngine;

namespace Steppe.UnitySimulation
{
    [DisallowMultipleComponent]
    public sealed class SteppeSimulationHost : MonoBehaviour
    {
        private sealed class ResourceExtractionCommand
        {
            public long RequestId;
            public int CellX;
            public int CellY;
            public float SurfaceWaterLiters;
            public float SnowWaterLiters;
            public float LiveBiomassKilograms;
            public float DryBiomassKilograms;
        }

        private static readonly SimulationLayer[] SnapshotLayers =
            StateCatalog.All.Select(descriptor => descriptor.Id).ToArray();
        private static readonly SimulationFlux[] SnapshotFluxes =
            FluxCatalog.All.Select(descriptor => descriptor.Id).ToArray();
        private static readonly VectorProcess[] SnapshotVectorProcesses =
            VectorProcessCatalog.All.Select(descriptor => descriptor.Process).ToArray();

        private readonly object pendingSync = new object();
        private readonly SimulationSnapshotBuffer snapshots = new SimulationSnapshotBuffer();
        private readonly ConcurrentQueue<ResourceExtractionCommand> extractionCommands =
            new ConcurrentQueue<ResourceExtractionCommand>();
        private readonly ConcurrentDictionary<long, SteppeSimulationResourceExtractionResult> extractionResults =
            new ConcurrentDictionary<long, SteppeSimulationResourceExtractionResult>();
        private AutoResetEvent workAvailable;
        private CancellationTokenSource cancellation;
        private Task worker;
        private WorldConfig config;
        private double pendingManualHours;
        private double requestedThroughHours;
        private double fixedStepHours;
        private long presentationSecondsBits;
        private int isReady;
        private int isRunning;
        private volatile string lastError;
        private bool errorReported;
        private long nextExtractionRequestId;
        private int processedExtractionCommandCount;
        private int successfulExtractionCommandCount;
        private int timePaused;
        private float timeMultiplier = 1f;

        public SteppeSimulationSnapshot LatestSnapshot => snapshots.Latest;
        public SteppeSimulationSnapshot PreviousSnapshot => snapshots.Current?.Previous;
        public WorldCoordinateMapper Coordinates { get; private set; }
        public bool IsReady => Volatile.Read(ref isReady) != 0;
        public bool IsRunning => Volatile.Read(ref isRunning) != 0;
        public string LastError => lastError;
        public int PendingExtractionCommandCount => extractionCommands.Count;
        public int ReadyExtractionResultCount => extractionResults.Count;
        public int ProcessedExtractionCommandCount =>
            Volatile.Read(ref processedExtractionCommandCount);
        public int SuccessfulExtractionCommandCount =>
            Volatile.Read(ref successfulExtractionCommandCount);
        public bool IsTimePaused => Volatile.Read(ref timePaused) != 0;
        public float TimeMultiplier => Volatile.Read(ref timeMultiplier);
        public double FixedStepHours => fixedStepHours;
        public double PresentationElapsedSimulationSeconds =>
            BitConverter.Int64BitsToDouble(Interlocked.Read(ref presentationSecondsBits));
        public double LatestMacroSimulationSeconds =>
            LatestSnapshot != null ? LatestSnapshot.Summary.ElapsedHours * 3600d : 0d;
        public float MacroInterpolationAlpha => CalculateInterpolationAlpha(
            snapshots.Current,
            PresentationElapsedSimulationSeconds / 3600d);

        public double ElapsedSimulationSeconds => PresentationElapsedSimulationSeconds;

        public bool TryGetTimeSample(out SteppeSimulationTimeSample sample)
        {
            if (LatestSnapshot == null)
            {
                sample = null;
                return false;
            }

            sample = CreateTimeSample(PresentationElapsedSimulationSeconds);
            return true;
        }

        /// <summary>
        /// Updates the continuously rendered world time and asks the worker to keep
        /// the next fixed macro boundary ready. Fixed steps remain an implementation
        /// detail; presentation samples are taken between the two boundary snapshots.
        /// </summary>
        public void SetPresentationTime(double elapsedSimulationSeconds)
        {
            if (double.IsNaN(elapsedSimulationSeconds)
                || double.IsInfinity(elapsedSimulationSeconds)
                || elapsedSimulationSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedSimulationSeconds));
            }

            Interlocked.Exchange(
                ref presentationSecondsBits,
                BitConverter.DoubleToInt64Bits(elapsedSimulationSeconds));
            if (fixedStepHours <= 0d || workAvailable == null)
            {
                return;
            }

            var presentationHours = elapsedSimulationSeconds / 3600d;
            var nextBoundary =
                (Math.Floor(presentationHours / fixedStepHours + 1e-9d) + 1d)
                * fixedStepHours;
            lock (pendingSync)
            {
                requestedThroughHours = Math.Max(requestedThroughHours, nextBoundary);
            }

            workAvailable.Set();
        }

        public void SetTimeControl(bool paused, float multiplier)
        {
            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(multiplier));
            }

            Volatile.Write(ref timeMultiplier, multiplier);
            Volatile.Write(ref timePaused, paused ? 1 : 0);
        }

        public bool TryGetEnvironmentSample(
            double worldX,
            double worldZ,
            out SteppeSimulationEnvironmentSample sample)
        {
            sample = null;
            var pair = snapshots.Current;
            var presentationSeconds = PresentationElapsedSimulationSeconds;
            var presentationHours = presentationSeconds / 3600d;
            var blend = CalculateInterpolationAlpha(pair, presentationHours);
            var latest = pair?.Latest;
            if (latest == null
                || !latest.Coordinates.TryWorldToCell(worldX, worldZ, out var cellX, out var cellY)
                || !TrySample(pair, SimulationLayer.SurfaceTemperature, worldX, worldZ, blend, out var surfaceTemperature)
                || !TrySample(pair, SimulationLayer.AirTemperature, worldX, worldZ, blend, out var airTemperature)
                || !TrySample(pair, SimulationLayer.SolarRadiation, worldX, worldZ, blend, out var solarRadiation)
                || !TrySampleVector(pair, SimulationLayer.Wind, worldX, worldZ, blend, out var windX, out var windZ)
                || !TrySample(pair, SimulationLayer.Humidity, worldX, worldZ, blend, out var humidity)
                || !TrySample(pair, SimulationLayer.CloudWater, worldX, worldZ, blend, out var cloudWater)
                || !TrySample(pair, SimulationLayer.Precipitation, worldX, worldZ, blend, out var precipitation)
                || !TrySample(pair, SimulationLayer.SurfaceWater, worldX, worldZ, blend, out var surfaceWater)
                || !TrySample(pair, SimulationLayer.RootWater, worldX, worldZ, blend, out var rootWater)
                || !TrySample(pair, SimulationLayer.Snow, worldX, worldZ, blend, out var snow)
                || !TrySample(pair, SimulationLayer.FrozenSoil, worldX, worldZ, blend, out var frozenSoil)
                || !TrySample(pair, SimulationLayer.LiveBiomass, worldX, worldZ, blend, out var liveBiomass)
                || !TrySample(pair, SimulationLayer.DryBiomass, worldX, worldZ, blend, out var dryBiomass)
                || !TrySample(pair, SimulationLayer.SurfaceCrust, worldX, worldZ, blend, out var surfaceCrust)
                || !TrySample(pair, SimulationLayer.Dust, worldX, worldZ, blend, out var dust)
                || !TrySample(pair, SimulationLayer.BurnScar, worldX, worldZ, blend, out var burnScar))
            {
                return false;
            }

            ApplyLocalAtmosphereVariation(
                worldX,
                worldZ,
                presentationSeconds,
                ref surfaceTemperature,
                ref airTemperature,
                ref windX,
                ref windZ,
                ref humidity,
                ref cloudWater,
                ref precipitation);

            sample = new SteppeSimulationEnvironmentSample(
                cellX,
                cellY,
                presentationHours,
                surfaceTemperature,
                airTemperature,
                solarRadiation,
                windX,
                windZ,
                humidity,
                cloudWater,
                precipitation,
                surfaceWater,
                rootWater,
                snow,
                frozenSoil,
                liveBiomass,
                dryBiomass,
                surfaceCrust,
                dust,
                burnScar);
            return true;
        }

        /// <summary>
        /// Read-only presentation access to one authoritative scalar state at the
        /// continuously rendered time. This does not mutate or advance the model.
        /// </summary>
        public bool TrySampleState(
            SimulationLayer layer,
            double worldX,
            double worldZ,
            out float value)
        {
            var pair = snapshots.Current;
            var blend = CalculateInterpolationAlpha(
                pair,
                PresentationElapsedSimulationSeconds / 3600d);
            return TrySample(pair, layer, worldX, worldZ, blend, out value);
        }

        /// <summary>
        /// Read-only presentation access to an authoritative accumulated flux,
        /// converted to its per-hour rate before temporal interpolation.
        /// </summary>
        public bool TrySampleFluxRate(
            SimulationFlux flux,
            double worldX,
            double worldZ,
            out float ratePerHour)
        {
            ratePerHour = default;
            var pair = snapshots.Current;
            var blend = CalculateInterpolationAlpha(
                pair,
                PresentationElapsedSimulationSeconds / 3600d);
            if (pair == null
                || !pair.Previous.TrySampleFlux(
                    flux,
                    worldX,
                    worldZ,
                    out var previous,
                    out var previousPeriodHours)
                || !pair.Latest.TrySampleFlux(
                    flux,
                    worldX,
                    worldZ,
                    out var latest,
                    out var latestPeriodHours))
            {
                return false;
            }

            var previousRate = previous / Mathf.Max(0.0001f, (float)previousPeriodHours);
            var latestRate = latest / Mathf.Max(0.0001f, (float)latestPeriodHours);
            ratePerHour = Mathf.LerpUnclamped(previousRate, latestRate, blend);
            return true;
        }

        /// <summary>
        /// Samples one canonical vector process at presentation time. Accumulated
        /// transfers and transport moments are converted to per-hour values before
        /// interpolation; instantaneous and direction fields retain their units.
        /// </summary>
        public bool TrySampleVectorProcess(
            VectorProcess process,
            double worldX,
            double worldZ,
            out float x,
            out float y,
            out float magnitude,
            out float grossMagnitude)
        {
            x = y = magnitude = grossMagnitude = default;
            var pair = snapshots.Current;
            var blend = CalculateInterpolationAlpha(
                pair,
                PresentationElapsedSimulationSeconds / 3600d);
            if (pair == null
                || !pair.Previous.TrySampleVectorProcess(
                    process,
                    worldX,
                    worldZ,
                    out var previousX,
                    out var previousY,
                    out var previousMagnitude,
                    out var previousGrossMagnitude,
                    out var previousPeriodHours)
                || !pair.Latest.TrySampleVectorProcess(
                    process,
                    worldX,
                    worldZ,
                    out var latestX,
                    out var latestY,
                    out var latestMagnitude,
                    out var latestGrossMagnitude,
                    out var latestPeriodHours))
            {
                return false;
            }

            var descriptor = VectorProcessCatalog.Get(process);
            var previousScale = VectorProcessScale(descriptor.Measure, previousPeriodHours);
            var latestScale = VectorProcessScale(descriptor.Measure, latestPeriodHours);
            x = Mathf.LerpUnclamped(previousX * previousScale, latestX * latestScale, blend);
            y = Mathf.LerpUnclamped(previousY * previousScale, latestY * latestScale, blend);
            magnitude = Mathf.LerpUnclamped(
                previousMagnitude * previousScale,
                latestMagnitude * latestScale,
                blend);
            grossMagnitude = Mathf.LerpUnclamped(
                previousGrossMagnitude * previousScale,
                latestGrossMagnitude * latestScale,
                blend);
            return true;
        }

        private static float VectorProcessScale(
            VectorProcessMeasure measure,
            double periodHours) =>
            measure == VectorProcessMeasure.AccumulatedTransfer
            || measure == VectorProcessMeasure.TransportMoment
                ? 1f / Mathf.Max(0.0001f, (float)periodHours)
                : 1f;

        public long QueueResourceExtraction(
            double worldX,
            double worldZ,
            float surfaceWaterLiters,
            float snowWaterLiters,
            float liveBiomassKilograms,
            float dryBiomassKilograms)
        {
            ValidateExtractionAmount(surfaceWaterLiters, nameof(surfaceWaterLiters));
            ValidateExtractionAmount(snowWaterLiters, nameof(snowWaterLiters));
            ValidateExtractionAmount(liveBiomassKilograms, nameof(liveBiomassKilograms));
            ValidateExtractionAmount(dryBiomassKilograms, nameof(dryBiomassKilograms));
            if (Coordinates == null
                || !Coordinates.TryWorldToCell(worldX, worldZ, out var cellX, out var cellY))
            {
                return 0L;
            }

            var requestId = Interlocked.Increment(ref nextExtractionRequestId);
            extractionCommands.Enqueue(new ResourceExtractionCommand
            {
                RequestId = requestId,
                CellX = cellX,
                CellY = cellY,
                SurfaceWaterLiters = surfaceWaterLiters,
                SnowWaterLiters = snowWaterLiters,
                LiveBiomassKilograms = liveBiomassKilograms,
                DryBiomassKilograms = dryBiomassKilograms,
            });
            workAvailable?.Set();
            return requestId;
        }

        public bool TryTakeResourceExtractionResult(
            long requestId,
            out SteppeSimulationResourceExtractionResult result)
        {
            if (requestId <= 0L)
            {
                result = null;
                return false;
            }

            return extractionResults.TryRemove(requestId, out result);
        }

        public bool TryGetDebugSample(
            double worldX,
            double worldZ,
            out SteppeSimulationDebugSample sample)
        {
            sample = null;
            if (!TryGetTimeSample(out var time)
                || !TryGetEnvironmentSample(worldX, worldZ, out var environment))
            {
                return false;
            }

            sample = new SteppeSimulationDebugSample(
                environment.CellX,
                environment.CellY,
                time.Year,
                time.DayOfYear,
                time.HourOfDay,
                time.Season,
                environment.SurfaceTemperatureC,
                Mathf.Sqrt(environment.WindXMs * environment.WindXMs + environment.WindZMs * environment.WindZMs),
                environment.PrecipitationMillimetersPerHour,
                environment.SurfaceWaterMillimeters,
                environment.RootWaterMillimeters,
                environment.LiveBiomassGramsPerSquareMeter,
                environment.DustGramsPerSquareMeter);
            return true;
        }

        public void Configure(
            int seed,
            float simulationRate,
            int width = 96,
            int height = 96,
            float cellSizeMeters = 250f,
            double latitudeDegrees = 48d,
            int baseStepMinutes = 60)
        {
            if (worker != null)
            {
                throw new InvalidOperationException("The simulation host can only be configured once.");
            }

            if (float.IsNaN(simulationRate) || float.IsInfinity(simulationRate) || simulationRate < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(simulationRate));
            }

            config = WorldConfig.Create(
                seed,
                width,
                height,
                cellSizeMeters,
                latitudeDegrees,
                baseStepMinutes);
            Coordinates = new WorldCoordinateMapper(width, height, cellSizeMeters);
            fixedStepHours = baseStepMinutes / 60d;
            Interlocked.Exchange(ref presentationSecondsBits, BitConverter.DoubleToInt64Bits(0d));
            workAvailable = new AutoResetEvent(false);
            cancellation = new CancellationTokenSource();
            Volatile.Write(ref isRunning, 1);
            worker = Task.Run(() => RunWorker(config, Coordinates, cancellation.Token));
        }

        public void RequestAdvanceHours(double hours)
        {
            if (double.IsNaN(hours) || double.IsInfinity(hours) || hours < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(hours));
            }

            if (hours == 0d || workAvailable == null)
            {
                return;
            }

            lock (pendingSync)
            {
                pendingManualHours += hours;
            }

            workAvailable.Set();
        }

        private void Update()
        {
            if (!errorReported && !string.IsNullOrEmpty(lastError))
            {
                errorReported = true;
                Debug.LogError("Finite steppe simulation stopped: " + lastError, this);
            }
        }

        private void RunWorker(
            WorldConfig workerConfig,
            WorldCoordinateMapper coordinates,
            CancellationToken token)
        {
            try
            {
                var world = new FiniteWorld(workerConfig);
                snapshots.Publish(SteppeSimulationSnapshot.Capture(
                    world,
                    coordinates,
                    SnapshotLayers,
                    SnapshotFluxes,
                    SnapshotVectorProcesses));
                Volatile.Write(ref isReady, 1);

                var waitHandles = new WaitHandle[] { workAvailable, token.WaitHandle };
                while (!token.IsCancellationRequested)
                {
                    if (ProcessResourceExtractions(world))
                    {
                        snapshots.Publish(SteppeSimulationSnapshot.Capture(
                            world,
                            coordinates,
                            SnapshotLayers,
                            SnapshotFluxes,
                            SnapshotVectorProcesses));
                    }

                    if (TryTakeStep(world.GetSummary().ElapsedHours, out var hours))
                    {
                        world.AdvanceHours(hours, token);
                        snapshots.Publish(SteppeSimulationSnapshot.Capture(
                            world,
                            coordinates,
                            SnapshotLayers,
                            SnapshotFluxes,
                            SnapshotVectorProcesses));
                        continue;
                    }

                    WaitHandle.WaitAny(waitHandles, 250);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                lastError = exception.ToString();
            }
            finally
            {
                Volatile.Write(ref isRunning, 0);
            }
        }

        private bool TryTakeStep(double worldHours, out double hours)
        {
            lock (pendingSync)
            {
                if (pendingManualHours + 1e-9d >= fixedStepHours)
                {
                    pendingManualHours -= fixedStepHours;
                    hours = fixedStepHours;
                    return true;
                }

                if (worldHours + 1e-9d >= requestedThroughHours)
                {
                    hours = 0d;
                    return false;
                }

                hours = fixedStepHours;
                return true;
            }
        }

        private static float CalculateInterpolationAlpha(
            SimulationSnapshotPair pair,
            double presentationHours)
        {
            if (pair == null)
            {
                return 0f;
            }

            var from = pair.Previous.Summary.ElapsedHours;
            var to = pair.Latest.Summary.ElapsedHours;
            var duration = to - from;
            if (duration <= 1e-9d)
            {
                return 1f;
            }

            return Mathf.Clamp01((float)((presentationHours - from) / duration));
        }

        private static bool TrySample(
            SimulationSnapshotPair pair,
            SimulationLayer layer,
            double worldX,
            double worldZ,
            float blend,
            out float value)
        {
            value = default;
            if (pair == null
                || !pair.Previous.TrySample(layer, worldX, worldZ, out var previous)
                || !pair.Latest.TrySample(layer, worldX, worldZ, out var latest))
            {
                return false;
            }

            value = Mathf.LerpUnclamped(previous, latest, blend);
            return true;
        }

        private static bool TrySampleVector(
            SimulationSnapshotPair pair,
            SimulationLayer layer,
            double worldX,
            double worldZ,
            float blend,
            out float x,
            out float y)
        {
            x = y = default;
            if (pair == null
                || !pair.Previous.TrySampleVector(layer, worldX, worldZ, out var previousX, out var previousY)
                || !pair.Latest.TrySampleVector(layer, worldX, worldZ, out var latestX, out var latestY))
            {
                return false;
            }

            x = Mathf.LerpUnclamped(previousX, latestX, blend);
            y = Mathf.LerpUnclamped(previousY, latestY, blend);
            return true;
        }

        private void ApplyLocalAtmosphereVariation(
            double worldX,
            double worldZ,
            double presentationSeconds,
            ref float surfaceTemperature,
            ref float airTemperature,
            ref float windX,
            ref float windZ,
            ref float humidity,
            ref float cloudWater,
            ref float precipitation)
        {
            var advectedX = worldX - windX * presentationSeconds * 0.18d;
            var advectedZ = worldZ - windZ * presentationSeconds * 0.18d;
            var broad = SteppeLocalField.SampleSigned(
                advectedX,
                advectedZ,
                presentationSeconds,
                config.Seed + 3109,
                110d,
                720d);
            var detail = SteppeLocalField.SampleSigned(
                advectedX,
                advectedZ,
                presentationSeconds,
                config.Seed + 7919,
                48d,
                240d);
            var cross = SteppeLocalField.SampleSigned(
                advectedX + 137.5d,
                advectedZ - 83.25d,
                presentationSeconds,
                config.Seed + 12161,
                72d,
                360d);

            var speed = Mathf.Sqrt(windX * windX + windZ * windZ);
            var directionX = speed > 0.05f ? windX / speed : 1f;
            var directionZ = speed > 0.05f ? windZ / speed : 0f;
            var parallelScale = 1f + broad * 0.12f + detail * 0.05f;
            var crossSpeed = (0.25f + speed * 0.075f) * cross;
            windX = windX * parallelScale - directionZ * crossSpeed;
            windZ = windZ * parallelScale + directionX * crossSpeed;
            airTemperature += broad * 0.35f + detail * 0.12f;
            surfaceTemperature += broad * 0.65f + detail * 0.28f;
            humidity = Mathf.Max(0f, humidity * (1f + broad * 0.035f));
            cloudWater = Mathf.Max(0f, cloudWater * (1f + broad * 0.07f));
            precipitation = Mathf.Max(0f, precipitation * (1f + broad * 0.24f + detail * 0.12f));
        }

        private static SteppeSimulationTimeSample CreateTimeSample(double elapsedSeconds)
        {
            var elapsedHours = Math.Max(0d, elapsedSeconds) / 3600d;
            var wholeDays = (int)Math.Floor(elapsedHours / 24d);
            var dayOfYear = wholeDays % 365 + 1;
            var year = wholeDays / 365 + 1;
            var hour = elapsedHours % 24d;
            var season = dayOfYear >= 80 && dayOfYear < 172
                ? "spring"
                : dayOfYear >= 172 && dayOfYear < 266
                    ? "summer"
                    : dayOfYear >= 266 && dayOfYear < 355
                        ? "autumn"
                        : "winter";
            return new SteppeSimulationTimeSample(
                Math.Max(0d, elapsedSeconds),
                year,
                dayOfYear,
                hour,
                season);
        }

        private bool ProcessResourceExtractions(FiniteWorld world)
        {
            const int maximumCommandsPerPass = 64;
            var processed = false;
            var cellAreaSquareMeters = config.CellSizeMeters * config.CellSizeMeters;
            for (var index = 0;
                 index < maximumCommandsPerPass && extractionCommands.TryDequeue(out var command);
                 index++)
            {
                processed = true;
                Interlocked.Increment(ref processedExtractionCommandCount);
                try
                {
                    var surfaceWater = command.SurfaceWaterLiters > 0f
                        ? world.WithdrawSurfaceWater(
                            command.CellX,
                            command.CellY,
                            command.SurfaceWaterLiters / cellAreaSquareMeters)
                        : null;
                    var snow = command.SnowWaterLiters > 0f
                        ? world.MeltAndWithdrawSnow(
                            command.CellX,
                            command.CellY,
                            command.SnowWaterLiters / cellAreaSquareMeters)
                        : null;
                    var biomass = command.LiveBiomassKilograms > 0f
                                  || command.DryBiomassKilograms > 0f
                        ? world.HarvestBiomass(
                            command.CellX,
                            command.CellY,
                            command.LiveBiomassKilograms * 1000f / cellAreaSquareMeters,
                            command.DryBiomassKilograms * 1000f / cellAreaSquareMeters)
                        : null;
                    extractionResults[command.RequestId] =
                        new SteppeSimulationResourceExtractionResult(
                            command.RequestId,
                            true,
                            surfaceWater != null
                                ? surfaceWater.SurfaceWaterMm * cellAreaSquareMeters
                                : 0f,
                            snow != null
                                ? snow.SnowWaterEquivalentMm * cellAreaSquareMeters
                                : 0f,
                            biomass != null
                                ? biomass.LiveBiomassGm2 * cellAreaSquareMeters / 1000f
                                : 0f,
                            biomass != null
                                ? biomass.DryBiomassGm2 * cellAreaSquareMeters / 1000f
                                : 0f,
                            null);
                    Interlocked.Increment(ref successfulExtractionCommandCount);
                }
                catch (Exception exception)
                {
                    extractionResults[command.RequestId] =
                        new SteppeSimulationResourceExtractionResult(
                            command.RequestId,
                            false,
                            0f,
                            0f,
                            0f,
                            0f,
                            exception.Message);
                }
            }

            if (!extractionCommands.IsEmpty)
            {
                workAvailable.Set();
            }

            return processed;
        }

        private static void ValidateExtractionAmount(float amount, string parameterName)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount < 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private void OnDestroy()
        {
            cancellation?.Cancel();
            workAvailable?.Set();
        }
    }
}
