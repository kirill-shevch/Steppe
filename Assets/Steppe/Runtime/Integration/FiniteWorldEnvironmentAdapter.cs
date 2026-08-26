using System;
using System.Collections.Generic;
using Steppe.Ecology;
using Steppe.UnitySimulation;
using Steppe.Weather;
using UnityEngine;

namespace Steppe.Integration
{
    /// <summary>
    /// Converts the physical units of the headless finite world to the normalized
    /// contracts still consumed by Unity presentation and vehicle modules.
    /// </summary>
    public sealed class FiniteWorldEnvironmentAdapter
    {
        private readonly struct ExtractionKey : IEquatable<ExtractionKey>
        {
            public ExtractionKey(int cellX, int cellY, int resourceMask)
            {
                CellX = cellX;
                CellY = cellY;
                ResourceMask = resourceMask;
            }

            public int CellX { get; }
            public int CellY { get; }
            public int ResourceMask { get; }

            public bool Equals(ExtractionKey other) =>
                CellX == other.CellX
                && CellY == other.CellY
                && ResourceMask == other.ResourceMask;

            public override bool Equals(object other) =>
                other is ExtractionKey key && Equals(key);

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((CellX * 397) ^ CellY) * 397 ^ ResourceMask;
                }
            }
        }

        public const float MaximumPresentationRainMillimetersPerHour = 4f;
        public const float SaturatedSurfaceWaterMillimeters = 24f;
        public const float SaturatedRootWaterMillimeters = 180f;
        public const float FullSnowCoverMillimeters = 120f;
        public const float MaximumStandingBiomassGramsPerSquareMeter = 720f;

        private readonly SteppeSimulationHost host;
        private readonly Dictionary<ExtractionKey, long> pendingExtractions =
            new Dictionary<ExtractionKey, long>();

        public FiniteWorldEnvironmentAdapter(SteppeSimulationHost simulationHost)
        {
            host = simulationHost != null
                ? simulationHost
                : throw new ArgumentNullException(nameof(simulationHost));
        }

        public bool IsReady => host.IsReady && host.LatestSnapshot != null;
        public string LastError => host.LastError;
        public double ElapsedSimulationSeconds => host.ElapsedSimulationSeconds;
        public string LastExtractionError { get; private set; }

        public bool TrySamplePhysical(
            double worldX,
            double worldZ,
            out SteppeSimulationEnvironmentSample sample) =>
            host.TryGetEnvironmentSample(worldX, worldZ, out sample);

        public bool TrySampleWeather(double worldX, double worldZ, out SteppeWeatherSample weather)
        {
            if (!TrySamplePhysical(worldX, worldZ, out var sample))
            {
                weather = default;
                return false;
            }

            weather = ConvertWeather(sample);
            return true;
        }

        public bool TrySampleEcology(double worldX, double worldZ, out SteppeEcoCellState ecology)
        {
            if (!TrySamplePhysical(worldX, worldZ, out var sample))
            {
                ecology = default;
                return false;
            }

            ecology = ConvertEcology(sample);
            return true;
        }

        public bool TrySampleAirTemperature(double worldX, double worldZ, out double temperatureC)
        {
            if (!TrySamplePhysical(worldX, worldZ, out var sample))
            {
                temperatureC = default;
                return false;
            }

            temperatureC = sample.AirTemperatureC;
            return true;
        }

        public bool TryExtractResources(
            double worldX,
            double worldZ,
            double requestedSurfaceWater,
            double requestedRootWater,
            double requestedSnowWater,
            double requestedBiomass,
            out SteppeEcoExtraction extraction)
        {
            extraction = default;
            if (!IsReady
                || !host.Coordinates.TryWorldToCell(worldX, worldZ, out var cellX, out var cellY))
            {
                return false;
            }

            var surfaceRequest = Math.Max(0d, requestedSurfaceWater);
            var snowRequest = Math.Max(0d, requestedSnowWater);
            var biomassRequest = Math.Max(0d, requestedBiomass);
            var resourceMask = (surfaceRequest > 0d || requestedRootWater > 0d || snowRequest > 0d ? 1 : 0)
                               | (biomassRequest > 0d ? 2 : 0);
            if (resourceMask == 0)
            {
                return true;
            }

            var key = new ExtractionKey(cellX, cellY, resourceMask);
            if (pendingExtractions.TryGetValue(key, out var pendingRequestId))
            {
                if (!host.TryTakeResourceExtractionResult(pendingRequestId, out var result))
                {
                    return false;
                }

                pendingExtractions.Remove(key);
                if (!result.Succeeded)
                {
                    LastExtractionError = result.Error;
                    return false;
                }

                LastExtractionError = null;
                var liveAndDry = result.LiveBiomassKilograms + result.DryBiomassKilograms;
                extraction = new SteppeEcoExtraction(
                    result.SurfaceWaterLiters / 500d,
                    0d,
                    result.SnowWaterLiters / 500d,
                    liveAndDry / 120d,
                    liveAndDry > 0.000001f
                        ? result.LiveBiomassKilograms / liveAndDry
                        : 0d);
                return true;
            }

            if (!TrySamplePhysical(worldX, worldZ, out var physical))
            {
                return false;
            }

            var totalBiomass = Math.Max(
                0f,
                physical.LiveBiomassGramsPerSquareMeter
                + physical.DryBiomassGramsPerSquareMeter);
            var liveFraction = totalBiomass > 0.0001f
                ? physical.LiveBiomassGramsPerSquareMeter / totalBiomass
                : 0f;
            var requestedBiomassKilograms = (float)(biomassRequest * 120d);
            var requestId = host.QueueResourceExtraction(
                worldX,
                worldZ,
                (float)(surfaceRequest * 500d),
                (float)(snowRequest * 500d),
                requestedBiomassKilograms * liveFraction,
                requestedBiomassKilograms * (1f - liveFraction));
            if (requestId <= 0L)
            {
                return false;
            }

            pendingExtractions.Add(key, requestId);
            return false;
        }

        public static SteppeWeatherSample ConvertWeather(SteppeSimulationEnvironmentSample sample)
        {
            if (sample == null)
            {
                throw new ArgumentNullException(nameof(sample));
            }

            var surfaceWind = new Vector2(sample.WindXMs, sample.WindZMs);
            var windSpeed = surfaceWind.magnitude;
            var cloudWater = Mathf.Clamp01(sample.CloudWaterMillimeters / 8f);
            // CloudWater exclusively owns cloud coverage/optical thickness. Humidity
            // owns atmospheric extinction and may not create a second cloud channel.
            var cloudCoverage = Mathf.Sqrt(cloudWater);
            var rain = Mathf.Clamp01(
                sample.PrecipitationMillimetersPerHour
                / MaximumPresentationRainMillimetersPerHour);
            var gust = Mathf.Max(
                Mathf.InverseLerp(8f, 20f, windSpeed),
                rain * 0.55f);

            return new SteppeWeatherSample(
                surfaceWind,
                surfaceWind,
                gust,
                0d,
                cloudCoverage,
                cloudWater,
                rain);
        }

        public static SteppeEcoCellState ConvertEcology(SteppeSimulationEnvironmentSample sample)
        {
            if (sample == null)
            {
                throw new ArgumentNullException(nameof(sample));
            }

            var live = Math.Max(0f, sample.LiveBiomassGramsPerSquareMeter);
            var dry = Math.Max(0f, sample.DryBiomassGramsPerSquareMeter);
            var biomass = Mathf.Clamp01(
                (live + dry) / MaximumStandingBiomassGramsPerSquareMeter);
            var greenFraction = live + dry > 0.0001f ? live / (live + dry) : 0f;
            var snow = Mathf.Clamp01(
                sample.SnowWaterEquivalentMillimeters / FullSnowCoverMillimeters);
            var snowCompaction = Mathf.Clamp01(
                sample.SnowWaterEquivalentMillimeters / (FullSnowCoverMillimeters * 2.5f));

            return new SteppeEcoCellState(
                Mathf.Clamp01(sample.SurfaceWaterMillimeters / SaturatedSurfaceWaterMillimeters),
                Mathf.Clamp01(sample.RootWaterMillimeters / SaturatedRootWaterMillimeters),
                biomass,
                Mathf.Clamp01(greenFraction),
                Mathf.Clamp01(sample.SurfaceCrustFraction),
                snow,
                snowCompaction,
                Mathf.Clamp01(sample.FrozenSoilFraction),
                Math.Max(0d, sample.ElapsedHours * 3600d));
        }
    }
}
