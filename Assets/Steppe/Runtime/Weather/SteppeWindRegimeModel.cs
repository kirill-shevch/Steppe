using System;
using System.Collections.Generic;
using Steppe.Settings;
using Steppe.Time;
using Steppe.World;
using UnityEngine;

namespace Steppe.Weather
{
    public readonly struct SteppeWindAdvection
    {
        public SteppeWindAdvection(double x, double z)
        {
            X = x;
            Z = z;
        }

        public double X { get; }
        public double Z { get; }

        public static SteppeWindAdvection operator -(
            SteppeWindAdvection left,
            SteppeWindAdvection right)
        {
            return new SteppeWindAdvection(left.X - right.X, left.Z - right.Z);
        }
    }

    public readonly struct SteppeWindRegimeSample
    {
        public SteppeWindRegimeSample(
            Vector2 cloudVelocity,
            Vector2 surfaceVelocity,
            SteppeWindAdvection cloudAdvection,
            SteppeWindAdvection surfaceAdvection,
            float gustiness)
        {
            CloudVelocity = cloudVelocity;
            SurfaceVelocity = surfaceVelocity;
            CloudAdvection = cloudAdvection;
            SurfaceAdvection = surfaceAdvection;
            Gustiness = gustiness;
        }

        public Vector2 CloudVelocity { get; }
        public Vector2 SurfaceVelocity { get; }
        public SteppeWindAdvection CloudAdvection { get; }
        public SteppeWindAdvection SurfaceAdvection { get; }
        public float Gustiness { get; }
    }

    /// <summary>
    /// Deterministic large-scale atmosphere. Neighboring target regimes are joined by a
    /// smooth velocity curve, and the matching analytic integral supplies continuous
    /// cloud and surface-field advection for arbitrary canonical timestamps.
    /// </summary>
    public sealed class SteppeWindRegimeModel
    {
        private readonly SteppeWorldSettings settings;
        private readonly float regimeDuration;
        private readonly List<WindTarget> targets = new List<WindTarget>();
        private readonly List<SteppeWindAdvection> cloudBoundaryAdvection =
            new List<SteppeWindAdvection>();
        private readonly List<SteppeWindAdvection> surfaceBoundaryAdvection =
            new List<SteppeWindAdvection>();

        private bool hasCachedSample;
        private double cachedWeatherSeconds;
        private SteppeWindRegimeSample cachedSample;

        public SteppeWindRegimeModel(SteppeWorldSettings worldSettings)
        {
            settings = worldSettings != null
                ? worldSettings
                : throw new ArgumentNullException(nameof(worldSettings));
            regimeDuration = settings.WindRegimeDuration;
            targets.Add(CreateTarget(0));
            cloudBoundaryAdvection.Add(default);
            surfaceBoundaryAdvection.Add(default);
        }

        public float RegimeDuration => regimeDuration;

        public SteppeWindRegimeSample Sample(double weatherSeconds)
        {
            var time = Math.Max(0.0, weatherSeconds);
            if (hasCachedSample && time.Equals(cachedWeatherSeconds))
            {
                return cachedSample;
            }

            var intervalIndex = (int)Math.Min(
                int.MaxValue - 2L,
                (long)Math.Floor(time / regimeDuration));
            EnsureBoundary(intervalIndex + 1);

            var intervalStart = intervalIndex * (double)regimeDuration;
            var normalizedTime = Math.Max(
                0.0,
                Math.Min(1.0, (time - intervalStart) / regimeDuration));
            var blend = SmoothStep(normalizedTime);
            var integralBlend = normalizedTime * normalizedTime * normalizedTime
                                - 0.5 * normalizedTime * normalizedTime
                                * normalizedTime * normalizedTime;
            var from = targets[intervalIndex];
            var to = targets[intervalIndex + 1];
            var cloudVelocity = Vector2.LerpUnclamped(from.CloudVelocity, to.CloudVelocity, (float)blend);
            var surfaceVelocity = Vector2.LerpUnclamped(
                from.SurfaceVelocity,
                to.SurfaceVelocity,
                (float)blend);
            var cloudAdvection = IntegratePartial(
                cloudBoundaryAdvection[intervalIndex],
                from.CloudVelocity,
                to.CloudVelocity,
                normalizedTime,
                integralBlend);
            var surfaceAdvection = IntegratePartial(
                surfaceBoundaryAdvection[intervalIndex],
                from.SurfaceVelocity,
                to.SurfaceVelocity,
                normalizedTime,
                integralBlend);

            cachedSample = new SteppeWindRegimeSample(
                cloudVelocity,
                surfaceVelocity,
                cloudAdvection,
                surfaceAdvection,
                Mathf.LerpUnclamped(from.Gustiness, to.Gustiness, (float)blend));
            cachedWeatherSeconds = time;
            hasCachedSample = true;
            return cachedSample;
        }

        private void EnsureBoundary(int boundaryIndex)
        {
            while (targets.Count <= boundaryIndex)
            {
                var nextIndex = targets.Count;
                var previous = targets[nextIndex - 1];
                var next = CreateTarget(nextIndex);
                targets.Add(next);
                cloudBoundaryAdvection.Add(IntegrateCompleteRegime(
                    cloudBoundaryAdvection[nextIndex - 1],
                    previous.CloudVelocity,
                    next.CloudVelocity));
                surfaceBoundaryAdvection.Add(IntegrateCompleteRegime(
                    surfaceBoundaryAdvection[nextIndex - 1],
                    previous.SurfaceVelocity,
                    next.SurfaceVelocity));
            }
        }

        private WindTarget CreateTarget(int regimeIndex)
        {
            var targetWeatherSeconds = regimeIndex * (double)regimeDuration;
            GetSeasonFactors(
                targetWeatherSeconds,
                out var directionFactor,
                out var speedFactor,
                out var gustFactor);

            var directionNoise = regimeIndex == 0
                ? 0.0
                : SignedHash(regimeIndex, 11, settings.WorldSeed + 52103);
            var speedNoise = regimeIndex == 0
                ? 0.0
                : SignedHash(regimeIndex, 23, settings.WorldSeed + 52103);
            var surfaceTurnNoise = regimeIndex == 0
                ? 0.0
                : SignedHash(regimeIndex, 37, settings.WorldSeed + 52103);
            var surfaceSpeedNoise = Hash01(DeterministicNoise.Hash(
                regimeIndex,
                47,
                settings.WorldSeed + 52103));
            var gustNoise = Hash01(DeterministicNoise.Hash(
                regimeIndex,
                59,
                settings.WorldSeed + 52103));

            var cloudDegrees = settings.PrevailingWindDirectionDegrees
                               + settings.WindDirectionVariationDegrees
                               * directionFactor
                               * (float)directionNoise;
            var cloudSpeed = settings.PrevailingWindSpeed
                             * speedFactor
                             * (1f + settings.WindSpeedVariation * (float)speedNoise);
            cloudSpeed = Mathf.Max(0.35f, cloudSpeed);
            var surfaceDegrees = cloudDegrees
                                 + settings.SurfaceWindDirectionVariationDegrees
                                 * directionFactor
                                 * (float)surfaceTurnNoise;
            var surfaceSpeed = cloudSpeed
                               * settings.SurfaceWindSpeedRatio
                               * Mathf.Lerp(0.86f, 1.14f, (float)surfaceSpeedNoise);
            var gustiness = Mathf.Clamp01(
                Mathf.Lerp(0.22f, 0.92f, (float)gustNoise) * gustFactor);

            return new WindTarget(
                Direction(cloudDegrees) * cloudSpeed,
                Direction(surfaceDegrees) * Mathf.Max(0.2f, surfaceSpeed),
                gustiness);
        }

        private void GetSeasonFactors(
            double weatherSeconds,
            out float directionFactor,
            out float speedFactor,
            out float gustFactor)
        {
            var simulationSeconds = SteppeWeatherTime.ToSimulationSeconds(settings, weatherSeconds);
            var season = SteppeTimeSystem.CreateSnapshot(settings, simulationSeconds).Season;
            switch (season)
            {
                case SteppeSeason.Winter:
                    directionFactor = 0.58f;
                    speedFactor = 1.18f;
                    gustFactor = 0.82f;
                    break;
                case SteppeSeason.Spring:
                    directionFactor = 1.12f;
                    speedFactor = 1.04f;
                    gustFactor = 1.14f;
                    break;
                case SteppeSeason.Summer:
                    directionFactor = 0.76f;
                    speedFactor = 0.82f;
                    gustFactor = 0.72f;
                    break;
                default:
                    directionFactor = 0.88f;
                    speedFactor = 0.98f;
                    gustFactor = 0.94f;
                    break;
            }
        }

        private SteppeWindAdvection IntegrateCompleteRegime(
            SteppeWindAdvection start,
            Vector2 from,
            Vector2 to)
        {
            var scale = regimeDuration * 0.5;
            return new SteppeWindAdvection(
                start.X + (from.x + to.x) * scale,
                start.Z + (from.y + to.y) * scale);
        }

        private SteppeWindAdvection IntegratePartial(
            SteppeWindAdvection start,
            Vector2 from,
            Vector2 to,
            double normalizedTime,
            double integralBlend)
        {
            var deltaX = to.x - from.x;
            var deltaZ = to.y - from.y;
            return new SteppeWindAdvection(
                start.X + regimeDuration * (from.x * normalizedTime + deltaX * integralBlend),
                start.Z + regimeDuration * (from.y * normalizedTime + deltaZ * integralBlend));
        }

        private static Vector2 Direction(float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
        }

        private static double SmoothStep(double value)
        {
            return value * value * (3.0 - 2.0 * value);
        }

        private static double SignedHash(long x, long z, int seed)
        {
            return Hash01(DeterministicNoise.Hash(x, z, seed)) * 2.0 - 1.0;
        }

        private static double Hash01(ulong hash)
        {
            return (hash >> 11) * (1.0 / 9007199254740992.0);
        }

        private readonly struct WindTarget
        {
            public WindTarget(Vector2 cloudVelocity, Vector2 surfaceVelocity, float gustiness)
            {
                CloudVelocity = cloudVelocity;
                SurfaceVelocity = surfaceVelocity;
                Gustiness = gustiness;
            }

            public Vector2 CloudVelocity { get; }
            public Vector2 SurfaceVelocity { get; }
            public float Gustiness { get; }
        }
    }
}
