using System;
using Steppe.Settings;
using Steppe.World;
using UnityEngine;

namespace Steppe.Weather
{
    public readonly struct SteppeWeatherSample
    {
        public SteppeWeatherSample(
            Vector2 cloudWind,
            Vector2 surfaceWind,
            double stormGust,
            double signedFrontDistance,
            double cloudCoverage,
            double cloudWater,
            double rainIntensity)
        {
            CloudWind = cloudWind;
            SurfaceWind = surfaceWind;
            StormGust = stormGust;
            SignedFrontDistance = signedFrontDistance;
            CloudCoverage = cloudCoverage;
            CloudWater = cloudWater;
            RainIntensity = rainIntensity;
        }

        public Vector2 CloudWind { get; }
        public Vector2 SurfaceWind { get; }
        public Vector2 Wind => SurfaceWind;
        public double StormGust { get; }
        public double SignedFrontDistance { get; }
        public double CloudCoverage { get; }
        public double CloudWater { get; }
        public double RainIntensity { get; }
    }

    /// <summary>
    /// Canonical, chunk-independent P3 weather field. The first prototype is a long wet
    /// front carried by a prevailing wind. Rendering and future precipitation both sample
    /// this same model rather than maintaining their own decorative cloud state.
    /// </summary>
    public sealed class SteppeWeatherModel
    {
        private readonly SteppeWorldSettings settings;
        private readonly Vector2 frontDirection;
        private readonly SteppeWindRegimeModel windModel;

        public SteppeWeatherModel(SteppeWorldSettings worldSettings)
        {
            settings = worldSettings != null ? worldSettings : throw new ArgumentNullException(nameof(worldSettings));
            var radians = settings.PrevailingWindDirectionDegrees * Mathf.Deg2Rad;
            frontDirection = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians)).normalized;
            windModel = new SteppeWindRegimeModel(settings);
        }

        public Vector2 FrontDirection => frontDirection;
        public SteppeWindRegimeModel WindModel => windModel;

        public SteppeWindRegimeSample SampleWind(double weatherSeconds)
        {
            return windModel.Sample(weatherSeconds);
        }

        public SteppeWeatherSample Sample(double worldX, double worldZ, double weatherSeconds)
        {
            var clampedTime = Math.Max(0.0, weatherSeconds);
            var wind = windModel.Sample(clampedTime);
            var advectedX = worldX - wind.CloudAdvection.X;
            var advectedZ = worldZ - wind.CloudAdvection.Z;
            var halfWidth = settings.FrontHalfWidth;

            // The broad perturbation keeps the front from becoming a ruler-straight band.
            // Because it is evaluated in advected coordinates, the complete pattern is
            // transported by exactly the same vector as the clouds.
            var broadShape = DeterministicNoise.FractalBrownianMotion(
                advectedX / 10500.0,
                advectedZ / 10500.0,
                settings.WorldSeed + 8701,
                3,
                2.03,
                0.52);
            var cloudShape = DeterministicNoise.FractalBrownianMotion(
                advectedX / 2400.0,
                advectedZ / 2400.0,
                settings.WorldSeed + 18713,
                4,
                2.11,
                0.54);
            var fineBreakup = DeterministicNoise.FractalBrownianMotion(
                advectedX / 720.0,
                advectedZ / 720.0,
                settings.WorldSeed + 29303,
                2,
                2.17,
                0.48);

            var alongWind = advectedX * frontDirection.x + advectedZ * frontDirection.y;
            var unwrappedDistance = alongWind
                                    - settings.InitialFrontDistanceAlongWind
                                    + broadShape * halfWidth * 0.42;
            var signedDistance = RepeatSigned(unwrappedDistance, settings.WeatherFrontSpacing);
            var normalizedFrontDistance = signedDistance / halfWidth;
            var cloudEnvelope = 1.0 - SmoothStep(0.72, 1.48, Math.Abs(normalizedFrontDistance));

            // A front is a coherent lifecycle rather than a collection of unrelated
            // dark spots. Moisture condenses gradually on the windward (positive)
            // side, reaches a broad saturated core, then drains faster than the cloud
            // cover clears on the leeward (negative) side. A fixed observer therefore
            // sees white cloud thicken, darken into rain, and brighten again afterwards.
            var condensation = 1.0 - SmoothStep(0.18, 1.30, normalizedFrontDistance);
            var postRainRetention = SmoothStep(-1.24, -0.16, normalizedFrontDistance);
            var saturatedCore = condensation * postRainRetention;

            var shapedCloud = Saturate(0.54 + cloudShape * 0.72 + fineBreakup * 0.22);
            var fairWeather = Saturate(0.34 + cloudShape * 0.25 + fineBreakup * 0.08);
            var stormCoverage = Saturate(0.76 + shapedCloud * 0.24);
            var coverage = Lerp(fairWeather, stormCoverage, cloudEnvelope);
            var saturation = SmoothStep(0.10, 0.86, saturatedCore);
            var water = Saturate(cloudEnvelope * saturation * (0.72 + shapedCloud * 0.28));
            var rain = SmoothStep(settings.RainWaterThreshold, 0.96, water)
                       * SmoothStep(0.52, 0.92, coverage);
            var leadingEdge = 1.0 - SmoothStep(
                0.18,
                0.92,
                Math.Abs(normalizedFrontDistance - 0.34));
            var stormGust = Saturate(
                cloudEnvelope
                * leadingEdge
                * (0.58 + water * 0.42));
            var surfaceWind = wind.SurfaceVelocity
                              + frontDirection * (settings.StormGustSpeed * (float)stormGust);

            return new SteppeWeatherSample(
                wind.CloudVelocity,
                surfaceWind,
                stormGust,
                signedDistance,
                coverage,
                water,
                Saturate(rain));
        }

        private static double Saturate(double value)
        {
            return Math.Max(0.0, Math.Min(1.0, value));
        }

        private static double SmoothStep(double from, double to, double value)
        {
            var t = Saturate((value - from) / (to - from));
            return t * t * (3.0 - 2.0 * t);
        }

        private static double Lerp(double from, double to, double value)
        {
            return from + (to - from) * value;
        }

        private static double RepeatSigned(double value, double period)
        {
            return value - Math.Floor(value / period + 0.5) * period;
        }
    }
}
