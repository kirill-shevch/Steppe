using NUnit.Framework;
using Steppe.Ecology;
using Steppe.Player;
using Steppe.Settings;
using Steppe.Surface;
using Steppe.Terrain;
using Steppe.Weather;
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

        [Test]
        public void DustStartsOnlyAfterWindExceedsTheDynamicSoilThreshold()
        {
            var dustSurface = CreateDustSurface();
            var dry = CreateDustState(dustSurface, 0.0, 0.0);
            var calm = SteppeDustModel.Evaluate(settings, dustSurface, dry, 4.0, 0.0);
            var windy = SteppeDustModel.Evaluate(settings, dustSurface, dry, 18.0, 0.0);

            Assert.That(calm.Emission, Is.EqualTo(0.0));
            Assert.That(windy.Emission, Is.GreaterThan(0.25));
            Assert.That(windy.ThresholdWindSpeed, Is.GreaterThan(settings.DustBaseWindThreshold));
        }

        [Test]
        public void WaterRainAndCrustIndependentlySuppressDust()
        {
            var dustSurface = CreateDustSurface();
            var dry = CreateDustState(dustSurface, 0.0, 0.0);
            var wet = CreateDustState(dustSurface, 0.55, 0.0);
            var crusted = CreateDustState(dustSurface, 0.0, 0.92);
            var baseline = SteppeDustModel.Evaluate(settings, dustSurface, dry, 18.0, 0.0);
            var afterRain = SteppeDustModel.Evaluate(settings, dustSurface, wet, 18.0, 0.0);
            var whileRaining = SteppeDustModel.Evaluate(settings, dustSurface, dry, 18.0, 0.9);
            var protectedByCrust = SteppeDustModel.Evaluate(settings, dustSurface, crusted, 18.0, 0.0);

            Assert.That(afterRain.Emission, Is.LessThan(baseline.Emission * 0.05));
            Assert.That(whileRaining.Emission, Is.LessThan(baseline.Emission * 0.01));
            Assert.That(protectedByCrust.Emission, Is.LessThan(baseline.Emission * 0.25));
            Assert.That(protectedByCrust.ThresholdWindSpeed, Is.GreaterThan(baseline.ThresholdWindSpeed));
        }

        [Test]
        public void WarmRootWaterGreensPlantsBeforeBiomassRecovers()
        {
            var depleted = new SteppeEcoCellState(
                0.0,
                0.72,
                surface.VegetationPotential * 0.42,
                0.08,
                0.1,
                0.0,
                0.0,
                0.0,
                0.0);
            var afterHalfDay = model.Advance(
                depleted,
                surface,
                new SteppeEcologyForcing(0.0, 4.0, 20.0, 0.8),
                12.0 * 3600.0);

            Assert.That(afterHalfDay.GreenFraction, Is.GreaterThan(depleted.GreenFraction + 0.2));
            Assert.That(afterHalfDay.Biomass, Is.GreaterThan(depleted.Biomass));
            Assert.That(
                afterHalfDay.Biomass - depleted.Biomass,
                Is.LessThan(surface.VegetationPotential * 0.08));
        }

        [Test]
        public void HotDroughtCuresGrassFasterThanStandingBiomassDisappears()
        {
            var mature = new SteppeEcoCellState(
                0.0,
                0.03,
                surface.VegetationPotential,
                0.95,
                0.2,
                0.0,
                0.0,
                0.0,
                0.0);
            var afterTwoDays = model.Advance(
                mature,
                surface,
                new SteppeEcologyForcing(0.0, 14.0, 38.0, 1.0),
                2.0 * 86400.0);

            Assert.That(afterTwoDays.GreenFraction, Is.LessThan(0.1));
            Assert.That(afterTwoDays.Biomass, Is.LessThan(mature.Biomass));
            Assert.That(afterTwoDays.Biomass, Is.GreaterThan(mature.Biomass * 0.85));
        }

        [Test]
        public void FifteenDayDrySeasonVisiblyReducesStandingDryMass()
        {
            var matureDry = new SteppeEcoCellState(
                0.0,
                0.03,
                surface.VegetationPotential,
                0.0,
                0.2,
                0.0,
                0.0,
                0.0,
                0.0);
            var after = AdvanceRepeated(
                matureDry,
                new SteppeEcologyForcing(0.0, 15.0, 32.0, 1.0),
                settings.DaysPerSeason);
            var relativeMass = after.Biomass / surface.VegetationPotential;

            Assert.That(relativeMass, Is.LessThan(0.82));
            Assert.That(relativeMass, Is.GreaterThan(0.55));
            Assert.That(after.DryBiomass, Is.GreaterThan(after.LiveBiomass));
        }

        [Test]
        public void FifteenDayWetGrowingSeasonRecoversLiveBiomass()
        {
            var depleted = new SteppeEcoCellState(
                0.0,
                0.28,
                surface.VegetationPotential * 0.35,
                0.08,
                0.1,
                0.0,
                0.0,
                0.0,
                0.0);
            var after = AdvanceRepeated(
                depleted,
                new SteppeEcologyForcing(0.30, 4.0, 18.0, 0.8),
                settings.DaysPerSeason);
            var relativeMass = after.Biomass / surface.VegetationPotential;

            Assert.That(relativeMass, Is.GreaterThan(0.78));
            Assert.That(after.LiveBiomass, Is.GreaterThan(after.DryBiomass));
        }

        [Test]
        public void ColdPrecipitationAccumulatesSnowInsteadOfWetSurfaceWater()
        {
            var initial = new SteppeEcoCellState(
                0.0,
                0.25,
                surface.VegetationPotential,
                0.1,
                0.2,
                0.0,
                0.0,
                0.0,
                0.0);
            var after = model.Advance(
                initial,
                surface,
                new SteppeEcologyForcing(1.0, 8.0, -8.0, 0.2),
                4.0 * 3600.0);

            Assert.That(SteppePrecipitationPhase.SnowFraction(-8.0), Is.EqualTo(1.0));
            Assert.That(after.SnowWater, Is.GreaterThan(0.35));
            Assert.That(after.SurfaceWater, Is.LessThan(0.01));
            Assert.That(after.FrozenFraction, Is.GreaterThan(0.15));
        }

        [Test]
        public void WarmDayMeltsSnowIntoSoilWaterAndThawsFrozenGround()
        {
            var snowbound = new SteppeEcoCellState(
                0.0,
                0.2,
                surface.VegetationPotential,
                0.05,
                0.0,
                0.72,
                0.28,
                0.9,
                0.0);
            var after = model.Advance(
                snowbound,
                surface,
                new SteppeEcologyForcing(0.0, 4.0, 10.0, 1.0),
                6.0 * 3600.0);

            Assert.That(after.SnowWater, Is.LessThan(snowbound.SnowWater));
            Assert.That(after.SurfaceWater + after.RootWater, Is.GreaterThan(snowbound.RootWater));
            Assert.That(after.FrozenFraction, Is.LessThan(snowbound.FrozenFraction));
        }

        [Test]
        public void SnowCoverPreventsDustEvenInStrongWind()
        {
            var dustSurface = CreateDustSurface();
            var bare = CreateDustState(dustSurface, 0.0, 0.0);
            var snowCovered = new SteppeEcoCellState(
                bare.SurfaceWater,
                bare.RootWater,
                bare.Biomass,
                bare.GreenFraction,
                bare.SurfaceCrust,
                0.45,
                0.2,
                0.8,
                0.0);

            var bareDust = SteppeDustModel.Evaluate(settings, dustSurface, bare, 22.0, 0.0);
            var snowDust = SteppeDustModel.Evaluate(settings, dustSurface, snowCovered, 22.0, 0.0);

            Assert.That(bareDust.Emission, Is.GreaterThan(0.2));
            Assert.That(snowDust.Emission, Is.LessThan(0.001));
        }

        [Test]
        public void CryosphereMapKeepsSnowCompactionAndFrozenChannelsSeparate()
        {
            var state = new SteppeEcoCellState(
                0.2,
                0.4,
                surface.VegetationPotential,
                0.3,
                0.1,
                0.62,
                0.37,
                0.81,
                0.0);
            var encoded = SteppeCryosphereMapEncoding.Encode(state);

            Assert.That(SteppeCryosphereMapEncoding.Decode(encoded.r), Is.EqualTo(0.62).Within(1.0 / 255.0));
            Assert.That(SteppeCryosphereMapEncoding.Decode(encoded.g), Is.EqualTo(0.37).Within(1.0 / 255.0));
            Assert.That(SteppeCryosphereMapEncoding.Decode(encoded.b), Is.EqualTo(0.81).Within(1.0 / 255.0));
            Assert.That(encoded.a, Is.EqualTo(0));
        }

        [Test]
        public void SaturatedClayIsHarderToCrossThanTheSameDryCrustedSoil()
        {
            var claySurface = CreateTraversalSurface(0.9, 0.45);
            var wet = new SteppeEcoCellState(
                0.78, 0.72, claySurface.VegetationPotential, 0.4, 0.05, 0.0, 0.0, 0.0, 0.0);
            var dryCrust = new SteppeEcoCellState(
                0.0, 0.12, claySurface.VegetationPotential, 0.1, 0.92, 0.0, 0.0, 0.0, 0.0);

            var mud = SteppeTraversalModel.Evaluate(settings, claySurface, wet);
            var firm = SteppeTraversalModel.Evaluate(settings, claySurface, dryCrust);

            Assert.That(mud.Mud, Is.GreaterThan(0.6));
            Assert.That(mud.Resistance, Is.GreaterThan(firm.Resistance + 0.35));
            Assert.That(mud.SinkDepth, Is.GreaterThan(firm.SinkDepth + 0.1));
        }

        [Test]
        public void FreshSnowSinksMoreThanCompactedOrFrozenSnow()
        {
            var snowSurface = CreateTraversalSurface(0.45, 0.35);
            var fresh = new SteppeEcoCellState(
                0.0, 0.22, snowSurface.VegetationPotential, 0.05, 0.0, 0.72, 0.02, 0.25, 0.0);
            var compacted = new SteppeEcoCellState(
                0.0, 0.22, snowSurface.VegetationPotential, 0.05, 0.0, 0.72, 0.94, 0.92, 0.0);

            var soft = SteppeTraversalModel.Evaluate(settings, snowSurface, fresh);
            var hard = SteppeTraversalModel.Evaluate(settings, snowSurface, compacted);

            Assert.That(soft.SnowSink, Is.GreaterThan(hard.SnowSink + 0.5));
            Assert.That(soft.Resistance, Is.GreaterThan(hard.Resistance + 0.35));
            Assert.That(soft.SinkDepth, Is.GreaterThan(hard.SinkDepth + 0.2));
        }

        [Test]
        public void TrackCoordinatesRemainStableAcrossNegativeWorldSpace()
        {
            Assert.That(
                SteppeTrackCellCoordinate.FromWorld(-0.01, -1.5, 1.5),
                Is.EqualTo(new SteppeTrackCellCoordinate(-1, -1)));
            Assert.That(
                SteppeTrackCellCoordinate.FromWorld(1.49, 1.5, 1.5),
                Is.EqualTo(new SteppeTrackCellCoordinate(0, 1)));
        }

        private static SurfaceSample CreateDustSurface()
        {
            return new SurfaceSample(
                180.0,
                15.0,
                new BiomeWeights(0.0, 0.0, 0.2, 0.8),
                0.2,
                0.22,
                0.2,
                0.12,
                0.18,
                0.82,
                0.22,
                0.12,
                0.75,
                0.9,
                new Color32(132, 105, 69, 255),
                new Color32(104, 96, 58, 255));
        }

        private static SurfaceSample CreateTraversalSurface(double clay, double exposedGround)
        {
            return new SurfaceSample(
                320.0,
                12.0,
                new BiomeWeights(0.0, 0.25, 0.65, 0.10),
                clay,
                0.55,
                0.62,
                0.45,
                1.0 - exposedGround,
                exposedGround,
                0.55,
                0.45,
                0.72,
                0.6,
                new Color32(132, 105, 69, 255),
                new Color32(104, 96, 58, 255));
        }

        private static SteppeEcoCellState CreateDustState(
            SurfaceSample dustSurface,
            double surfaceWater,
            double crust)
        {
            return new SteppeEcoCellState(
                surfaceWater,
                0.18,
                dustSurface.VegetationPotential,
                0.15,
                crust,
                0.0,
                0.0,
                0.0,
                0.0);
        }

        private SteppeEcoCellState AdvanceRepeated(
            SteppeEcoCellState initial,
            SteppeEcologyForcing forcing,
            int days)
        {
            var state = initial;
            var steps = days * 86400 / (int)settings.EcologySimulationStepSeconds;
            for (var index = 0; index < steps; index++)
            {
                state = model.Advance(
                    state,
                    surface,
                    forcing,
                    settings.EcologySimulationStepSeconds);
            }

            return state;
        }
    }
}
