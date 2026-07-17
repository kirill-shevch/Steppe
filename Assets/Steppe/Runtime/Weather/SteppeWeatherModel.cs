using System;
using Steppe.Settings;
using Steppe.World;
using UnityEngine;

namespace Steppe.Weather
{
    public readonly struct SteppeWeatherSample
    {
        public SteppeWeatherSample(
            Vector2 wind,
            double signedFrontDistance,
            double cloudCoverage,
            double cloudWater,
            double rainIntensity)
        {
            Wind = wind;
            SignedFrontDistance = signedFrontDistance;
            CloudCoverage = cloudCoverage;
            CloudWater = cloudWater;
            RainIntensity = rainIntensity;
        }

        public Vector2 Wind { get; }
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
        private readonly Vector2 windDirection;
        private readonly Vector2 windVelocity;

        public SteppeWeatherModel(SteppeWorldSettings worldSettings)
        {
            settings = worldSettings != null ? worldSettings : throw new ArgumentNullException(nameof(worldSettings));
            var radians = settings.PrevailingWindDirectionDegrees * Mathf.Deg2Rad;
            windDirection = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians)).normalized;
            windVelocity = windDirection * settings.PrevailingWindSpeed;
        }

        public Vector2 WindDirection => windDirection;
        public Vector2 WindVelocity => windVelocity;

        public SteppeWeatherSample Sample(double worldX, double worldZ, double weatherSeconds)
        {
            var clampedTime = Math.Max(0.0, weatherSeconds);
            var advectedX = worldX - windVelocity.x * clampedTime;
            var advectedZ = worldZ - windVelocity.y * clampedTime;
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

            var centerAlongWind = settings.InitialFrontDistanceAlongWind
                                  + settings.PrevailingWindSpeed * clampedTime;
            var alongWind = worldX * windDirection.x + worldZ * windDirection.y;
            var unwrappedDistance = alongWind - centerAlongWind + broadShape * halfWidth * 0.42;
            var signedDistance = RepeatSigned(unwrappedDistance, settings.WeatherFrontSpacing);
            var normalizedDistance = Math.Abs(signedDistance) / halfWidth;
            var wetEnvelope = 1.0 - SmoothStep(0.62, 1.52, normalizedDistance);

            var shapedCloud = Saturate(0.54 + cloudShape * 0.72 + fineBreakup * 0.22);
            var fairWeather = Saturate(0.34 + cloudShape * 0.25 + fineBreakup * 0.08);
            var stormCoverage = Saturate(0.68 + shapedCloud * 0.32);
            var coverage = Lerp(fairWeather, stormCoverage, wetEnvelope);
            var water = Saturate(wetEnvelope * (0.57 + shapedCloud * 0.43));
            var rain = SmoothStep(settings.RainWaterThreshold, 0.96, water)
                       * SmoothStep(0.48, 0.9, coverage);

            return new SteppeWeatherSample(
                windVelocity,
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
