using NUnit.Framework;
using Steppe.Settings;
using Steppe.Surface;
using Steppe.Terrain;
using Steppe.Time;
using UnityEngine;

namespace Steppe.Tests
{
    public sealed class TimeSimulationTests
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
        public void ClockAdvancesDaysAndWrapsTheLongYearDeterministically()
        {
            var start = SteppeTimeSystem.CreateSnapshot(settings, 0.0);
            var nextDay = SteppeTimeSystem.CreateSnapshot(settings, 86400.0);
            var nextYear = SteppeTimeSystem.CreateSnapshot(settings, settings.DaysPerYear * 86400.0);

            Assert.That(start.Hour, Is.EqualTo(settings.StartingHour).Within(0.000001));
            Assert.That(start.DayOfYear, Is.EqualTo(settings.StartingDayOfYear + settings.StartingHour / 24.0).Within(0.000001));
            Assert.That(start.Season, Is.EqualTo(SteppeSeason.Spring));
            Assert.That(nextDay.DayOfYear, Is.EqualTo(start.DayOfYear + 1.0).Within(0.000001));
            Assert.That(nextDay.Hour, Is.EqualTo(start.Hour).Within(0.000001));
            Assert.That(nextYear.DayOfYear, Is.EqualTo(start.DayOfYear).Within(0.000001));
            Assert.That(nextYear.Year, Is.EqualTo(start.Year + 1));
        }

        [Test]
        public void RuntimeClockStartsAtNormalSpeed()
        {
            var gameObject = new GameObject("Time test");
            var clock = gameObject.AddComponent<SteppeTimeSystem>();
            clock.Configure(settings);

            Assert.That(clock.DebugMultiplier, Is.EqualTo(1f));
            Assert.That(clock.CurrentSimulationRate, Is.EqualTo(settings.SimulationSecondsPerRealSecond));
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void SunIsAboveTheHorizonAtNoonAndBelowItAtMidnight()
        {
            var noon = SteppeTimeSystem.CreateSnapshot(settings, 4.0 * 3600.0);
            var midnight = SteppeTimeSystem.CreateSnapshot(settings, 16.0 * 3600.0);
            var noonSun = SteppeAstronomy.Evaluate(noon, settings.LatitudeDegrees);
            var midnightSun = SteppeAstronomy.Evaluate(midnight, settings.LatitudeDegrees);

            Assert.That(noon.Hour, Is.EqualTo(12.0).Within(0.000001));
            Assert.That(midnight.Hour, Is.EqualTo(0.0).Within(0.000001));
            Assert.That(noonSun.ElevationDegrees, Is.GreaterThan(20.0));
            Assert.That(noonSun.Daylight, Is.GreaterThan(0.99));
            Assert.That(midnightSun.ElevationDegrees, Is.LessThan(0.0));
            Assert.That(midnightSun.Daylight, Is.LessThan(0.01));
        }

        [Test]
        public void ClearSkyTemperaturePeaksAfterSolarNoonWithoutChangingBiome()
        {
            var terrain = new TerrainHeightGenerator(settings);
            var surfaceGenerator = new SteppeSurfaceGenerator(settings);
            var height = terrain.SampleHeight(0.0, 0.0);
            var normal = terrain.SampleNormal(0.0, 0.0, 2.0);
            var surface = surfaceGenerator.Sample(0.0, 0.0, height, normal.y);
            var climate = new SteppeClimateModel(settings);
            var afternoon = new SteppeTimeSnapshot(0.0, 35.0, 15.0, 0.3, 0, SteppeSeason.Spring);
            var night = new SteppeTimeSnapshot(0.0, 35.0, 3.0, 0.3, 0, SteppeSeason.Spring);
            var warm = climate.Evaluate(surface, afternoon);
            var cold = climate.Evaluate(surface, night);

            Assert.That(warm.AirTemperatureC, Is.GreaterThan(cold.AirTemperatureC + 10.0));
            Assert.That(warm.Biome, Is.EqualTo(surface.DominantBiome));
            Assert.That(cold.Biome, Is.EqualTo(surface.DominantBiome));
        }
    }
}
