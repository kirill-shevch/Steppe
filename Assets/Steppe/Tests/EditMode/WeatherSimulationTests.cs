using NUnit.Framework;
using Steppe.Settings;
using Steppe.Weather;
using UnityEngine;

namespace Steppe.Tests
{
    public sealed class WeatherSimulationTests
    {
        private SteppeWorldSettings settings;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<SteppeWorldSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void CompleteWeatherPatternAdvectsWithPrevailingWind()
        {
            var model = new SteppeWeatherModel(settings);
            const double startTime = 37.0;
            const double duration = 123.0;
            const double startX = 812.5;
            const double startZ = -1704.25;
            var before = model.Sample(startX, startZ, startTime);
            var displacement = model.WindVelocity * (float)duration;
            var after = model.Sample(startX + displacement.x, startZ + displacement.y, startTime + duration);

            Assert.That(after.CloudCoverage, Is.EqualTo(before.CloudCoverage).Within(0.000001));
            Assert.That(after.CloudWater, Is.EqualTo(before.CloudWater).Within(0.000001));
            Assert.That(after.RainIntensity, Is.EqualTo(before.RainIntensity).Within(0.000001));
            Assert.That(after.SignedFrontDistance, Is.EqualTo(before.SignedFrontDistance).Within(0.001));
        }

        [Test]
        public void WetFrontProducesRainWhileTheInterfrontSkyRemainsCloudyButDrier()
        {
            var model = new SteppeWeatherModel(settings);
            var direction = model.WindDirection;
            var center = settings.InitialFrontDistanceAlongWind;
            var wettest = model.Sample(direction.x * center, direction.y * center, 0.0);

            for (var index = -10; index <= 10; index++)
            {
                var offset = index / 10.0 * settings.FrontHalfWidth;
                var sample = model.Sample(
                    direction.x * (center + offset),
                    direction.y * (center + offset),
                    0.0);
                if (sample.CloudWater > wettest.CloudWater)
                {
                    wettest = sample;
                }
            }

            var interfront = model.Sample(
                direction.x * (center + settings.WeatherFrontSpacing * 0.5),
                direction.y * (center + settings.WeatherFrontSpacing * 0.5),
                0.0);
            for (var index = 30; index <= 70; index++)
            {
                var distance = center + settings.WeatherFrontSpacing * index / 100.0;
                var sample = model.Sample(direction.x * distance, direction.y * distance, 0.0);
                if (sample.CloudWater < interfront.CloudWater)
                {
                    interfront = sample;
                }
            }

            Assert.That(wettest.CloudCoverage, Is.GreaterThan(0.7));
            Assert.That(wettest.CloudWater, Is.GreaterThan(settings.RainWaterThreshold));
            Assert.That(wettest.RainIntensity, Is.GreaterThan(0.0));
            Assert.That(interfront.CloudCoverage, Is.GreaterThan(0.08));
            Assert.That(interfront.CloudWater, Is.LessThan(settings.RainWaterThreshold));
            Assert.That(interfront.RainIntensity, Is.EqualTo(0.0).Within(0.000001));
        }

        [Test]
        public void SuccessiveFrontsKeepReturningAlongTheWindAxis()
        {
            var model = new SteppeWeatherModel(settings);
            var direction = model.WindDirection;
            var firstCenter = settings.InitialFrontDistanceAlongWind;
            var nextCenter = firstCenter + settings.WeatherFrontSpacing;
            var first = model.Sample(direction.x * firstCenter, direction.y * firstCenter, 0.0);
            var next = model.Sample(direction.x * nextCenter, direction.y * nextCenter, 0.0);

            Assert.That(first.CloudWater, Is.GreaterThan(0.5));
            Assert.That(next.CloudWater, Is.GreaterThan(0.5));
        }

        [Test]
        public void FrontBuildsABroadRainCoreAndDrainsBeforeCloudCoverClears()
        {
            var model = new SteppeWeatherModel(settings);
            var direction = model.WindDirection;
            var center = settings.InitialFrontDistanceAlongWind;
            var leading = AverageAcrossFront(model, direction, center, 0.95);
            var core = AverageAcrossFront(model, direction, center, 0.0);
            var trailing = AverageAcrossFront(model, direction, center, -0.95);

            Assert.That(core.CloudWater, Is.GreaterThan(settings.RainWaterThreshold));
            Assert.That(core.RainIntensity, Is.GreaterThan(0.05));
            Assert.That(core.CloudWater, Is.GreaterThan(leading.CloudWater + 0.2));
            Assert.That(core.CloudWater, Is.GreaterThan(trailing.CloudWater + 0.2));
            Assert.That(trailing.CloudCoverage, Is.GreaterThan(0.45));
            Assert.That(trailing.CloudWater, Is.LessThan(trailing.CloudCoverage * 0.65));
        }

        [Test]
        public void RainBelongsToAContinuousFrontInsteadOfAnIsolatedSample()
        {
            var model = new SteppeWeatherModel(settings);
            var direction = model.WindDirection;
            var center = settings.InitialFrontDistanceAlongWind;
            var rainySamples = 0;

            for (var index = -8; index <= 8; index++)
            {
                var normalizedOffset = index / 16.0;
                var distance = center + normalizedOffset * settings.FrontHalfWidth;
                var sample = model.Sample(direction.x * distance, direction.y * distance, 0.0);
                if (sample.RainIntensity > 0.01)
                {
                    rainySamples++;
                }
            }

            Assert.That(rainySamples, Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void CoverageNeverFallsIntoAWorldWideClearBandBetweenFronts()
        {
            var model = new SteppeWeatherModel(settings);
            var direction = model.WindDirection;
            var center = settings.InitialFrontDistanceAlongWind;

            for (var index = 0; index <= 64; index++)
            {
                var distance = center + settings.WeatherFrontSpacing * index / 64.0;
                var sample = model.Sample(direction.x * distance, direction.y * distance, 0.0);
                Assert.That(sample.CloudCoverage, Is.GreaterThan(0.08), $"Clear band at sample {index}");
            }
        }

        [Test]
        public void WeatherMapAlwaysCoversTheStreamedTerrainDiameter()
        {
            var terrainDiameter = settings.FarRadius * settings.ChunkSize * 2f;
            Assert.That(settings.WeatherMapWorldSize, Is.GreaterThan(terrainDiameter));
            Assert.That(settings.CloudLayerRadius, Is.GreaterThan(settings.FarRadius * settings.ChunkSize));
            Assert.That(settings.CloudLayerRadius, Is.LessThanOrEqualTo(settings.WeatherMapWorldSize * 0.5f));
        }

        [Test]
        public void OnlyOneWetFrontCanOccupyTheVisibleCloudHorizon()
        {
            var visibleDiameter = settings.CloudLayerRadius * 2f;
            Assert.That(
                settings.WeatherFrontSpacing,
                Is.GreaterThan(visibleDiameter + settings.FrontHalfWidth * 2f));
        }

        private SteppeWeatherSample AverageAcrossFront(
            SteppeWeatherModel model,
            Vector2 direction,
            double center,
            double normalizedAlongFront)
        {
            var crossDirection = new Vector2(-direction.y, direction.x);
            var coverage = 0.0;
            var water = 0.0;
            var rain = 0.0;
            const int sampleCount = 9;

            for (var index = 0; index < sampleCount; index++)
            {
                var crossOffset = (index - (sampleCount - 1) * 0.5) * 320.0;
                var along = center + normalizedAlongFront * settings.FrontHalfWidth;
                var sample = model.Sample(
                    direction.x * along + crossDirection.x * crossOffset,
                    direction.y * along + crossDirection.y * crossOffset,
                    0.0);
                coverage += sample.CloudCoverage;
                water += sample.CloudWater;
                rain += sample.RainIntensity;
            }

            return new SteppeWeatherSample(
                model.WindVelocity,
                0.0,
                coverage / sampleCount,
                water / sampleCount,
                rain / sampleCount);
        }
    }
}
