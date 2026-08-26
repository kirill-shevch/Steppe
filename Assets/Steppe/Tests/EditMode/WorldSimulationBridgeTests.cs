using NUnit.Framework;
using Steppe.Integration;
using Steppe.Simulation;
using Steppe.UnitySimulation;
using Steppe.Weather;
using UnityEngine;

namespace Steppe.Tests
{
    public sealed class WorldSimulationBridgeTests
    {
        [Test]
        public void CoordinateMapper_RoundTripsEveryCornerCell()
        {
            var mapper = new WorldCoordinateMapper(96, 64, 250f);
            var cells = new[] { (0, 0), (95, 0), (0, 63), (95, 63), (48, 32) };

            foreach (var cell in cells)
            {
                mapper.CellCenterToWorld(cell.Item1, cell.Item2, out var worldX, out var worldZ);

                Assert.That(mapper.TryWorldToCell(worldX, worldZ, out var mappedX, out var mappedY), Is.True);
                Assert.That(mappedX, Is.EqualTo(cell.Item1));
                Assert.That(mappedY, Is.EqualTo(cell.Item2));
            }
        }

        [Test]
        public void CoordinateMapper_UsesFiniteHalfOpenBounds()
        {
            var mapper = new WorldCoordinateMapper(8, 8, 250f);

            Assert.That(mapper.TryWorldToCell(mapper.MinimumWorldX, mapper.MinimumWorldZ, out _, out _), Is.True);
            Assert.That(mapper.TryWorldToCell(mapper.MaximumWorldX, 0d, out _, out _), Is.False);
            Assert.That(mapper.TryWorldToCell(0d, mapper.MaximumWorldZ, out _, out _), Is.False);
        }

        [Test]
        public void CoordinateMapper_ProvidesContinuousCellCentreBlending()
        {
            var mapper = new WorldCoordinateMapper(2, 2, 250f);

            Assert.That(mapper.TryGetBilinearSample(
                0d,
                0d,
                out var x0,
                out var y0,
                out var x1,
                out var y1,
                out var blendX,
                out var blendY), Is.True);
            Assert.That((x0, y0), Is.EqualTo((0, 0)));
            Assert.That((x1, y1), Is.EqualTo((1, 1)));
            Assert.That(blendX, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(blendY, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void LocalField_IsDeterministicAndContinuousWithinAMacroCell()
        {
            var first = SteppeLocalField.SampleSigned(32d, -71d, 120d, 42, 64d, 300d);
            var repeated = SteppeLocalField.SampleSigned(32d, -71d, 120d, 42, 64d, 300d);
            var nearby = SteppeLocalField.SampleSigned(32.1d, -70.9d, 120.1d, 42, 64d, 300d);
            var distant = SteppeLocalField.SampleSigned(96d, -7d, 420d, 42, 64d, 300d);

            Assert.That(repeated, Is.EqualTo(first));
            Assert.That(nearby, Is.EqualTo(first).Within(0.01f));
            Assert.That(Mathf.Abs(distant - first), Is.GreaterThan(0.001f));
        }

        [Test]
        public void UnityFactory_CreatesValidatedCoreConfiguration()
        {
            var config = WorldConfig.Create(42, 16, 24, 125f, 51d, 30);

            Assert.That(config.Seed, Is.EqualTo(42));
            Assert.That(config.CellCount, Is.EqualTo(384));
            Assert.That(config.BaseStepMinutes, Is.EqualTo(30));
        }

        [Test]
        public void EnvironmentAdapter_ConvertsPhysicalWeatherUnits()
        {
            var physical = CreatePhysicalSample();

            var weather = FiniteWorldEnvironmentAdapter.ConvertWeather(physical);

            Assert.That(weather.SurfaceWind.x, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(weather.SurfaceWind.y, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(weather.RainIntensity, Is.EqualTo(0.5d).Within(0.0001d));
            Assert.That(weather.CloudCoverage, Is.InRange(0d, 1d));
            Assert.That(weather.CloudWater, Is.InRange(0d, 1d));
        }

        [Test]
        public void EnvironmentAdapter_CloudCoverageBelongsOnlyToCloudWater()
        {
            var dryAir = FiniteWorldEnvironmentAdapter.ConvertWeather(
                CreatePhysicalSample(humidityMillimeters: 0.1f, cloudWaterMillimeters: 4f));
            var saturatedAir = FiniteWorldEnvironmentAdapter.ConvertWeather(
                CreatePhysicalSample(humidityMillimeters: 40f, cloudWaterMillimeters: 4f));

            Assert.That(
                saturatedAir.CloudCoverage,
                Is.EqualTo(dryAir.CloudCoverage).Within(0.000001d));
            Assert.That(
                saturatedAir.CloudWater,
                Is.EqualTo(dryAir.CloudWater).Within(0.000001d));
        }

        [Test]
        public void CloudBaseHeight_IsMonotonicPressureCarrier()
        {
            const float configuredBase = 1350f;
            var lowPressureBase = SteppeCloudLayer.EvaluateCloudBaseHeight(940f, configuredBase);
            var normalPressureBase = SteppeCloudLayer.EvaluateCloudBaseHeight(982.5f, configuredBase);
            var highPressureBase = SteppeCloudLayer.EvaluateCloudBaseHeight(1025f, configuredBase);

            Assert.That(lowPressureBase, Is.LessThan(normalPressureBase));
            Assert.That(normalPressureBase, Is.LessThan(highPressureBase));
            Assert.That(lowPressureBase, Is.GreaterThanOrEqualTo(220f));
        }

        [Test]
        public void EnvironmentAdapter_ConvertsPhysicalEcologyWithoutChangingRatios()
        {
            var physical = CreatePhysicalSample();

            var ecology = FiniteWorldEnvironmentAdapter.ConvertEcology(physical);

            Assert.That(ecology.SurfaceWater, Is.EqualTo(0.5d).Within(0.0001d));
            Assert.That(ecology.RootWater, Is.EqualTo(0.5d).Within(0.0001d));
            Assert.That(ecology.Biomass, Is.EqualTo(0.5d).Within(0.0001d));
            Assert.That(ecology.GreenFraction, Is.EqualTo(2d / 3d).Within(0.0001d));
            Assert.That(ecology.FrozenFraction, Is.EqualTo(0.25d).Within(0.0001d));
            Assert.That(ecology.LastSimulationSeconds, Is.EqualTo(5400d).Within(0.0001d));
        }

        private static SteppeSimulationEnvironmentSample CreatePhysicalSample(
            float humidityMillimeters = 15f,
            float cloudWaterMillimeters = 4f)
        {
            return new SteppeSimulationEnvironmentSample(
                4,
                5,
                1.5d,
                18f,
                16f,
                640f,
                3f,
                4f,
                humidityMillimeters,
                cloudWaterMillimeters,
                2f,
                12f,
                90f,
                60f,
                0.25f,
                240f,
                120f,
                0.4f,
                0.08f,
                0.12f);
        }
    }
}
