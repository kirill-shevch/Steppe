using System;
using System.Collections.Generic;
using System.Threading;
using Steppe.Simulation;

namespace Steppe.UnitySimulation
{
    public sealed class SteppeSimulationTimeSample
    {
        public SteppeSimulationTimeSample(
            double elapsedSimulationSeconds,
            int year,
            int dayOfYear,
            double hourOfDay,
            string season)
        {
            ElapsedSimulationSeconds = elapsedSimulationSeconds;
            Year = year;
            DayOfYear = dayOfYear;
            HourOfDay = hourOfDay;
            Season = season;
        }

        public double ElapsedSimulationSeconds { get; }
        public int Year { get; }
        public int DayOfYear { get; }
        public double HourOfDay { get; }
        public string Season { get; }
    }

    public sealed class SteppeSimulationEnvironmentSample
    {
        public SteppeSimulationEnvironmentSample(
            int cellX,
            int cellY,
            double elapsedHours,
            float surfaceTemperatureC,
            float airTemperatureC,
            float solarRadiationWattsPerSquareMeter,
            float windXMs,
            float windZMs,
            float humidityMillimeters,
            float cloudWaterMillimeters,
            float precipitationMillimetersPerHour,
            float surfaceWaterMillimeters,
            float rootWaterMillimeters,
            float snowWaterEquivalentMillimeters,
            float frozenSoilFraction,
            float liveBiomassGramsPerSquareMeter,
            float dryBiomassGramsPerSquareMeter,
            float surfaceCrustFraction,
            float dustGramsPerSquareMeter,
            float burnScarFraction)
        {
            CellX = cellX;
            CellY = cellY;
            ElapsedHours = elapsedHours;
            SurfaceTemperatureC = surfaceTemperatureC;
            AirTemperatureC = airTemperatureC;
            SolarRadiationWattsPerSquareMeter = solarRadiationWattsPerSquareMeter;
            WindXMs = windXMs;
            WindZMs = windZMs;
            HumidityMillimeters = humidityMillimeters;
            CloudWaterMillimeters = cloudWaterMillimeters;
            PrecipitationMillimetersPerHour = precipitationMillimetersPerHour;
            SurfaceWaterMillimeters = surfaceWaterMillimeters;
            RootWaterMillimeters = rootWaterMillimeters;
            SnowWaterEquivalentMillimeters = snowWaterEquivalentMillimeters;
            FrozenSoilFraction = frozenSoilFraction;
            LiveBiomassGramsPerSquareMeter = liveBiomassGramsPerSquareMeter;
            DryBiomassGramsPerSquareMeter = dryBiomassGramsPerSquareMeter;
            SurfaceCrustFraction = surfaceCrustFraction;
            DustGramsPerSquareMeter = dustGramsPerSquareMeter;
            BurnScarFraction = burnScarFraction;
        }

        public int CellX { get; }
        public int CellY { get; }
        public double ElapsedHours { get; }
        public float SurfaceTemperatureC { get; }
        public float AirTemperatureC { get; }
        public float SolarRadiationWattsPerSquareMeter { get; }
        public float WindXMs { get; }
        public float WindZMs { get; }
        public float HumidityMillimeters { get; }
        public float CloudWaterMillimeters { get; }
        public float PrecipitationMillimetersPerHour { get; }
        public float SurfaceWaterMillimeters { get; }
        public float RootWaterMillimeters { get; }
        public float SnowWaterEquivalentMillimeters { get; }
        public float FrozenSoilFraction { get; }
        public float LiveBiomassGramsPerSquareMeter { get; }
        public float DryBiomassGramsPerSquareMeter { get; }
        public float SurfaceCrustFraction { get; }
        public float DustGramsPerSquareMeter { get; }
        public float BurnScarFraction { get; }
    }

    public sealed class SteppeSimulationResourceExtractionResult
    {
        internal SteppeSimulationResourceExtractionResult(
            long requestId,
            bool succeeded,
            float surfaceWaterLiters,
            float snowWaterLiters,
            float liveBiomassKilograms,
            float dryBiomassKilograms,
            string error)
        {
            RequestId = requestId;
            Succeeded = succeeded;
            SurfaceWaterLiters = surfaceWaterLiters;
            SnowWaterLiters = snowWaterLiters;
            LiveBiomassKilograms = liveBiomassKilograms;
            DryBiomassKilograms = dryBiomassKilograms;
            Error = error;
        }

        public long RequestId { get; }
        public bool Succeeded { get; }
        public float SurfaceWaterLiters { get; }
        public float SnowWaterLiters { get; }
        public float LiveBiomassKilograms { get; }
        public float DryBiomassKilograms { get; }
        public string Error { get; }
    }

    public sealed class SteppeSimulationDebugSample
    {
        internal SteppeSimulationDebugSample(
            int cellX,
            int cellY,
            int year,
            int dayOfYear,
            double hourOfDay,
            string season,
            float surfaceTemperatureC,
            float windMetersPerSecond,
            float precipitationMillimetersPerHour,
            float surfaceWaterMillimeters,
            float rootWaterMillimeters,
            float liveBiomassGramsPerSquareMeter,
            float dustGramsPerSquareMeter)
        {
            CellX = cellX;
            CellY = cellY;
            Year = year;
            DayOfYear = dayOfYear;
            HourOfDay = hourOfDay;
            Season = season;
            SurfaceTemperatureC = surfaceTemperatureC;
            WindMetersPerSecond = windMetersPerSecond;
            PrecipitationMillimetersPerHour = precipitationMillimetersPerHour;
            SurfaceWaterMillimeters = surfaceWaterMillimeters;
            RootWaterMillimeters = rootWaterMillimeters;
            LiveBiomassGramsPerSquareMeter = liveBiomassGramsPerSquareMeter;
            DustGramsPerSquareMeter = dustGramsPerSquareMeter;
        }

        public int CellX { get; }
        public int CellY { get; }
        public int Year { get; }
        public int DayOfYear { get; }
        public double HourOfDay { get; }
        public string Season { get; }
        public float SurfaceTemperatureC { get; }
        public float WindMetersPerSecond { get; }
        public float PrecipitationMillimetersPerHour { get; }
        public float SurfaceWaterMillimeters { get; }
        public float RootWaterMillimeters { get; }
        public float LiveBiomassGramsPerSquareMeter { get; }
        public float DustGramsPerSquareMeter { get; }
    }

    public sealed class SteppeSimulationSnapshot
    {
        private readonly Dictionary<SimulationLayer, LayerSnapshot> layers;
        private readonly Dictionary<SimulationFlux, FluxSnapshot> fluxes;
        private readonly Dictionary<VectorProcess, VectorProcessSnapshot> vectorProcesses;

        private SteppeSimulationSnapshot(
            WorldSummary summary,
            WorldCoordinateMapper coordinates,
            Dictionary<SimulationLayer, LayerSnapshot> layers,
            Dictionary<SimulationFlux, FluxSnapshot> fluxes,
            Dictionary<VectorProcess, VectorProcessSnapshot> vectorProcesses,
            GiantHarvesterPopulationSnapshot giantHarvesters)
        {
            Summary = summary;
            Coordinates = coordinates;
            this.layers = layers;
            this.fluxes = fluxes;
            this.vectorProcesses = vectorProcesses;
            GiantHarvesters = giantHarvesters;
        }

        public WorldSummary Summary { get; }
        public WorldCoordinateMapper Coordinates { get; }
        public IEnumerable<SimulationLayer> AvailableLayers => layers.Keys;
        public IEnumerable<SimulationFlux> AvailableFluxes => fluxes.Keys;
        public IEnumerable<VectorProcess> AvailableVectorProcesses => vectorProcesses.Keys;
        public GiantHarvesterPopulationSnapshot GiantHarvesters { get; }

        public bool TryGetLayer(SimulationLayer layer, out LayerSnapshot snapshot) =>
            layers.TryGetValue(layer, out snapshot);

        public bool TryGetFlux(SimulationFlux flux, out FluxSnapshot snapshot) =>
            fluxes.TryGetValue(flux, out snapshot);

        public bool TryGetVectorProcess(
            VectorProcess process,
            out VectorProcessSnapshot snapshot) =>
            vectorProcesses.TryGetValue(process, out snapshot);

        public bool TrySample(SimulationLayer layer, double worldX, double worldZ, out float value)
        {
            value = default;
            if (!Coordinates.TryGetBilinearSample(
                    worldX,
                    worldZ,
                    out var x0,
                    out var y0,
                    out var x1,
                    out var y1,
                    out var blendX,
                    out var blendY)
                || !layers.TryGetValue(layer, out var snapshot))
            {
                return false;
            }

            if (!TryGetIndices(snapshot, x0, y0, x1, y1, out var i00, out var i10, out var i01, out var i11))
            {
                return false;
            }

            value = Bilinear(
                snapshot.Values[i00],
                snapshot.Values[i10],
                snapshot.Values[i01],
                snapshot.Values[i11],
                blendX,
                blendY);
            return true;
        }

        public bool TrySampleFlux(
            SimulationFlux flux,
            double worldX,
            double worldZ,
            out float value,
            out double periodHours)
        {
            value = default;
            periodHours = default;
            if (!Coordinates.TryGetBilinearSample(
                    worldX,
                    worldZ,
                    out var x0,
                    out var y0,
                    out var x1,
                    out var y1,
                    out var blendX,
                    out var blendY)
                || !fluxes.TryGetValue(flux, out var snapshot)
                || !TryGetIndices(
                    snapshot.Width,
                    snapshot.Height,
                    snapshot.Values.Length,
                    x0,
                    y0,
                    x1,
                    y1,
                    out var i00,
                    out var i10,
                    out var i01,
                    out var i11))
            {
                return false;
            }

            value = Bilinear(
                snapshot.Values[i00],
                snapshot.Values[i10],
                snapshot.Values[i01],
                snapshot.Values[i11],
                blendX,
                blendY);
            periodHours = snapshot.PeriodHours;
            return true;
        }

        public bool TrySampleVector(
            SimulationLayer layer,
            double worldX,
            double worldZ,
            out float x,
            out float y)
        {
            x = default;
            y = default;
            if (!Coordinates.TryGetBilinearSample(
                    worldX,
                    worldZ,
                    out var x0,
                    out var y0,
                    out var x1,
                    out var y1,
                    out var blendX,
                    out var blendY)
                || !layers.TryGetValue(layer, out var snapshot)
                || snapshot.VectorX == null
                || snapshot.VectorY == null)
            {
                return false;
            }

            if (!TryGetIndices(snapshot, x0, y0, x1, y1, out var i00, out var i10, out var i01, out var i11)
                || i11 >= snapshot.VectorX.Length
                || i11 >= snapshot.VectorY.Length)
            {
                return false;
            }

            x = Bilinear(
                snapshot.VectorX[i00],
                snapshot.VectorX[i10],
                snapshot.VectorX[i01],
                snapshot.VectorX[i11],
                blendX,
                blendY);
            y = Bilinear(
                snapshot.VectorY[i00],
                snapshot.VectorY[i10],
                snapshot.VectorY[i01],
                snapshot.VectorY[i11],
                blendX,
                blendY);
            return true;
        }

        public bool TrySampleVectorProcess(
            VectorProcess process,
            double worldX,
            double worldZ,
            out float x,
            out float y,
            out float magnitude,
            out float grossMagnitude,
            out double periodHours)
        {
            x = y = magnitude = grossMagnitude = default;
            periodHours = default;
            if (!Coordinates.TryGetBilinearSample(
                    worldX,
                    worldZ,
                    out var x0,
                    out var y0,
                    out var x1,
                    out var y1,
                    out var blendX,
                    out var blendY)
                || !vectorProcesses.TryGetValue(process, out var snapshot)
                || !TryGetIndices(
                    snapshot.Width,
                    snapshot.Height,
                    snapshot.VectorX.Length,
                    x0,
                    y0,
                    x1,
                    y1,
                    out var i00,
                    out var i10,
                    out var i01,
                    out var i11)
                || i11 >= snapshot.VectorY.Length
                || i11 >= snapshot.Magnitude.Length
                || i11 >= snapshot.GrossMagnitude.Length)
            {
                return false;
            }

            x = Bilinear(
                snapshot.VectorX[i00],
                snapshot.VectorX[i10],
                snapshot.VectorX[i01],
                snapshot.VectorX[i11],
                blendX,
                blendY);
            y = Bilinear(
                snapshot.VectorY[i00],
                snapshot.VectorY[i10],
                snapshot.VectorY[i01],
                snapshot.VectorY[i11],
                blendX,
                blendY);
            magnitude = Bilinear(
                snapshot.Magnitude[i00],
                snapshot.Magnitude[i10],
                snapshot.Magnitude[i01],
                snapshot.Magnitude[i11],
                blendX,
                blendY);
            grossMagnitude = Bilinear(
                snapshot.GrossMagnitude[i00],
                snapshot.GrossMagnitude[i10],
                snapshot.GrossMagnitude[i01],
                snapshot.GrossMagnitude[i11],
                blendX,
                blendY);
            periodHours = snapshot.PeriodHours;
            return true;
        }

        private static bool TryGetIndices(
            LayerSnapshot snapshot,
            int x0,
            int y0,
            int x1,
            int y1,
            out int i00,
            out int i10,
            out int i01,
            out int i11)
            => TryGetIndices(
                snapshot.Width,
                snapshot.Height,
                snapshot.Values.Length,
                x0,
                y0,
                x1,
                y1,
                out i00,
                out i10,
                out i01,
                out i11);

        private static bool TryGetIndices(
            int width,
            int height,
            int valueCount,
            int x0,
            int y0,
            int x1,
            int y1,
            out int i00,
            out int i10,
            out int i01,
            out int i11)
        {
            i00 = y0 * width + x0;
            i10 = y0 * width + x1;
            i01 = y1 * width + x0;
            i11 = y1 * width + x1;
            return width > 0
                   && height > 0
                   && x0 >= 0
                   && y0 >= 0
                   && x1 < width
                   && y1 < height
                   && i00 >= 0
                   && i11 < valueCount;
        }

        private static float Bilinear(
            float lowerLeft,
            float lowerRight,
            float upperLeft,
            float upperRight,
            float blendX,
            float blendY)
        {
            var lower = lowerLeft + (lowerRight - lowerLeft) * blendX;
            var upper = upperLeft + (upperRight - upperLeft) * blendX;
            return lower + (upper - lower) * blendY;
        }

        internal static SteppeSimulationSnapshot Capture(
            FiniteWorld world,
            WorldCoordinateMapper coordinates,
            IReadOnlyList<SimulationLayer> requestedLayers,
            IReadOnlyList<SimulationFlux> requestedFluxes,
            IReadOnlyList<VectorProcess> requestedVectorProcesses)
        {
            var capturedLayers = new Dictionary<SimulationLayer, LayerSnapshot>(requestedLayers.Count);
            for (var index = 0; index < requestedLayers.Count; index++)
            {
                var layer = requestedLayers[index];
                capturedLayers[layer] = world.CaptureLayer(layer);
            }

            var capturedFluxes = new Dictionary<SimulationFlux, FluxSnapshot>(requestedFluxes.Count);
            for (var index = 0; index < requestedFluxes.Count; index++)
            {
                var flux = requestedFluxes[index];
                capturedFluxes[flux] = world.CaptureFlux(flux);
            }

            var capturedVectors =
                new Dictionary<VectorProcess, VectorProcessSnapshot>(requestedVectorProcesses.Count);
            for (var index = 0; index < requestedVectorProcesses.Count; index++)
            {
                var process = requestedVectorProcesses[index];
                capturedVectors[process] = world.CaptureVectorProcess(process);
            }

            return new SteppeSimulationSnapshot(
                world.GetSummary(),
                coordinates,
                capturedLayers,
                capturedFluxes,
                capturedVectors,
                world.CaptureGiantHarvesters());
        }
    }

    public sealed class SimulationSnapshotPair
    {
        internal SimulationSnapshotPair(
            SteppeSimulationSnapshot previous,
            SteppeSimulationSnapshot latest)
        {
            Previous = previous;
            Latest = latest;
        }

        public SteppeSimulationSnapshot Previous { get; }
        public SteppeSimulationSnapshot Latest { get; }
    }

    public sealed class SimulationSnapshotBuffer
    {
        private SimulationSnapshotPair current;

        public SimulationSnapshotPair Current => Volatile.Read(ref current);
        public SteppeSimulationSnapshot Latest => Current?.Latest;

        internal void Publish(SteppeSimulationSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            while (true)
            {
                var observed = Current;
                SimulationSnapshotPair replacement;
                if (observed == null)
                {
                    replacement = new SimulationSnapshotPair(snapshot, snapshot);
                }
                else if (snapshot.Summary.ElapsedHours
                         > observed.Latest.Summary.ElapsedHours + 1e-9d)
                {
                    replacement = new SimulationSnapshotPair(observed.Latest, snapshot);
                }
                else
                {
                    // Resource commands can revise the current macro boundary.
                    // Keep the temporal origin intact and replace only the target.
                    replacement = new SimulationSnapshotPair(observed.Previous, snapshot);
                }

                if (ReferenceEquals(
                    Interlocked.CompareExchange(ref current, replacement, observed),
                    observed))
                {
                    return;
                }
            }
        }
    }
}
