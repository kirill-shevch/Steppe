using NUnit.Framework;
using Steppe.Ecology;
using Steppe.Settings;
using Steppe.Surface;
using Steppe.Terrain;
using UnityEngine;

namespace Steppe.Tests
{
    public sealed class EcologySimulationTests
    {
        private SteppeWorldSettings settings;
        private SteppeEcologyModel model;
        private SurfaceSample surface;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<SteppeWorldSettings>();
            model = new SteppeEcologyModel(settings);
            var terrain = new TerrainHeightGenerator(settings);
            var surfaceGenerator = new SteppeSurfaceGenerator(settings);
            var height = terrain.SampleHeight(0.0, 0.0);
            surface = surfaceGenerator.Sample(0.0, 0.0, height, terrain.SampleNormal(0.0, 0.0, 4.0).y);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void CellCoordinatesUseFloorDivisionAcrossTheWorldOrigin()
        {
            Assert.That(EcoCellCoordinate.FromWorld(0.0, 0.0, 128.0), Is.EqualTo(new EcoCellCoordinate(0, 0)));
            Assert.That(EcoCellCoordinate.FromWorld(127.99, 255.9, 128.0), Is.EqualTo(new EcoCellCoordinate(0, 1)));
            Assert.That(EcoCellCoordinate.FromWorld(-0.01, -128.0, 128.0), Is.EqualTo(new EcoCellCoordinate(-1, -1)));
            Assert.That(EcoCellCoordinate.FromWorld(-128.01, -128.01, 128.0), Is.EqualTo(new EcoCellCoordinate(-2, -2)));
        }

        [Test]
        public void RainFirstFillsTheSurfaceAndThenTheRootLayer()
        {
            var initial = model.CreateInitialState(surface, 18.0, 0.0);
            var rainy = new SteppeEcologyForcing(1.0, 5.0, 18.0, 0.5);
            var after = model.Advance(initial, surface, rainy, settings.EcologySimulationStepSeconds);

            Assert.That(after.SurfaceWater, Is.GreaterThan(initial.SurfaceWater));
            Assert.That(after.RootWater, Is.GreaterThan(initial.RootWater));
            Assert.That(after.LastSimulationSeconds, Is.EqualTo(settings.EcologySimulationStepSeconds));
        }

        [Test]
        public void ProlongedWarmWindyWeatherEventuallyDrainsBothWaterLayers()
        {
            var initial = model.CreateInitialState(surface, 20.0, 0.0);
            var wet = model.Advance(
                initial,
                surface,
                new SteppeEcologyForcing(1.0, 4.0, 18.0, 0.4),
                4.0 * 3600.0);
            var dry = wet;
            var dryForcing = new SteppeEcologyForcing(0.0, 16.0, 34.0, 1.0);
            for (var step = 0; step < 7 * 48; step++)
            {
                dry = model.Advance(dry, surface, dryForcing, settings.EcologySimulationStepSeconds);
            }

            Assert.That(dry.SurfaceWater, Is.LessThan(wet.SurfaceWater));
            Assert.That(dry.RootWater, Is.LessThan(wet.RootWater));
        }

        [Test]
        public void RainSoftensAnExistingDryCrust()
        {
            var crusted = new SteppeEcoCellState(
                0.0,
                0.35,
                0.4,
                0.2,
                0.85,
                0.0,
                0.0,
                0.0,
                0.0);
            var after = model.Advance(
                crusted,
                surface,
                new SteppeEcologyForcing(1.0, 3.0, 12.0, 0.3),
                2.0 * 3600.0);

            Assert.That(after.SurfaceCrust, Is.LessThan(crusted.SurfaceCrust));
        }

        [Test]
        public void FixedForcingProducesDeterministicState()
        {
            var first = model.CreateInitialState(surface, 15.0, 0.0);
            var second = first;
            var forcing = new SteppeEcologyForcing(0.42, 8.5, 15.0, 0.62);

            for (var index = 0; index < 24; index++)
            {
                first = model.Advance(first, surface, forcing, settings.EcologySimulationStepSeconds);
            }

            var secondModel = new SteppeEcologyModel(settings);
            for (var index = 0; index < 24; index++)
            {
                second = secondModel.Advance(second, surface, forcing, settings.EcologySimulationStepSeconds);
            }

            Assert.That(second.SurfaceWater, Is.EqualTo(first.SurfaceWater));
            Assert.That(second.RootWater, Is.EqualTo(first.RootWater));
            Assert.That(second.SurfaceCrust, Is.EqualTo(first.SurfaceCrust));
            Assert.That(second.LastSimulationSeconds, Is.EqualTo(first.LastSimulationSeconds));
        }

        [Test]
        public void StateMapEncodingUsesRelativeBiomassAndStableChannels()
        {
            var state = new SteppeEcoCellState(
                0.37,
                0.61,
                surface.VegetationPotential * 0.42,
                0.73,
                0.28,
                0.0,
                0.0,
                0.0,
                123.0);
            var encoded = SteppeEcologyMapEncoding.Encode(state, surface);

            Assert.That(SteppeEcologyMapEncoding.Decode(encoded.r), Is.EqualTo(0.37).Within(1.0 / 255.0));
            Assert.That(SteppeEcologyMapEncoding.Decode(encoded.g), Is.EqualTo(0.42).Within(1.0 / 255.0));
            Assert.That(SteppeEcologyMapEncoding.Decode(encoded.b), Is.EqualTo(0.73).Within(1.0 / 255.0));
            Assert.That(SteppeEcologyMapEncoding.Decode(encoded.a), Is.EqualTo(0.28).Within(1.0 / 255.0));
            Assert.That(SteppeEcologyMapEncoding.Neutral.g, Is.EqualTo(255));
        }

        [Test]
        public void StateMapCoversTheCompleteActiveEcologyDiameter()
        {
            Assert.That(
                settings.EcologyStateMapWorldSize,
                Is.GreaterThan(settings.EcologyActiveRadius * 2f));
            Assert.That(settings.EcologyStateMapResolution, Is.EqualTo(128));
        }
    }
}
