using System;
using NUnit.Framework;
using Steppe.Caravan;

namespace Steppe.Tests
{
    public sealed class CaravanDomainModelTests
    {
        [Test]
        public void ElectricalCompatibilityIsSymmetricAndRequiresStorageEndpoint()
        {
            var roles = (CaravanElectricalPortKind[])Enum.GetValues(
                typeof(CaravanElectricalPortKind));
            for (var firstIndex = 0; firstIndex < roles.Length; firstIndex++)
            {
                for (var secondIndex = 0;
                     secondIndex < roles.Length;
                     secondIndex++)
                {
                    var first = roles[firstIndex];
                    var second = roles[secondIndex];
                    var expected =
                        first == CaravanElectricalPortKind.Storage
                        ^ second == CaravanElectricalPortKind.Storage;

                    Assert.That(
                        CaravanConnectionRules.AreCompatible(first, second),
                        Is.EqualTo(expected),
                        $"{first} -> {second}");
                    Assert.That(
                        CaravanConnectionRules.AreCompatible(second, first),
                        Is.EqualTo(expected),
                        $"{second} -> {first}");
                }
            }
        }

        [TestCase(
            CaravanFluidPortRole.ReservoirSupply,
            CaravanFluidPortRole.PumpInlet)]
        [TestCase(
            CaravanFluidPortRole.PumpOutlet,
            CaravanFluidPortRole.RadiatorInlet)]
        [TestCase(
            CaravanFluidPortRole.RadiatorOutlet,
            CaravanFluidPortRole.ReservoirReturn)]
        [TestCase(
            CaravanFluidPortRole.ThermalTap,
            CaravanFluidPortRole.ReservoirReturn)]
        public void FluidCompatibilityAllowsEveryDeclaredPairInBothDirections(
            CaravanFluidPortRole first,
            CaravanFluidPortRole second)
        {
            Assert.That(
                CaravanConnectionRules.AreCompatible(first, second),
                Is.True);
            Assert.That(
                CaravanConnectionRules.AreCompatible(second, first),
                Is.True);
        }

        [Test]
        public void FluidCompatibilityRejectsEveryUndeclaredPair()
        {
            var roles = (CaravanFluidPortRole[])Enum.GetValues(
                typeof(CaravanFluidPortRole));
            var compatibleDirectedPairs = 0;
            for (var firstIndex = 0; firstIndex < roles.Length; firstIndex++)
            {
                for (var secondIndex = 0;
                     secondIndex < roles.Length;
                     secondIndex++)
                {
                    if (CaravanConnectionRules.AreCompatible(
                            roles[firstIndex],
                            roles[secondIndex]))
                    {
                        compatibleDirectedPairs++;
                    }
                }
            }

            Assert.That(compatibleDirectedPairs, Is.EqualTo(8));
        }

        [TestCase(
            CaravanMaterialNetworkKind.Biomass,
            CaravanMaterialPortRole.WetBiomassOutput,
            CaravanMaterialPortRole.WetBiomassInput)]
        [TestCase(
            CaravanMaterialNetworkKind.Biomass,
            CaravanMaterialPortRole.DryBiomassOutput,
            CaravanMaterialPortRole.DryBiomassInput)]
        [TestCase(
            CaravanMaterialNetworkKind.Mechanical,
            CaravanMaterialPortRole.MechanicalSource,
            CaravanMaterialPortRole.TransmissionInput)]
        [TestCase(
            CaravanMaterialNetworkKind.Mechanical,
            CaravanMaterialPortRole.MechanicalSource,
            CaravanMaterialPortRole.MechanicalConsumer)]
        [TestCase(
            CaravanMaterialNetworkKind.Mechanical,
            CaravanMaterialPortRole.TransmissionOutput,
            CaravanMaterialPortRole.MechanicalConsumer)]
        [TestCase(
            CaravanMaterialNetworkKind.Mechanical,
            CaravanMaterialPortRole.CouplingEndpoint,
            CaravanMaterialPortRole.CouplingEndpoint)]
        public void MaterialCompatibilityAllowsEveryDeclaredPairInBothDirections(
            CaravanMaterialNetworkKind kind,
            CaravanMaterialPortRole first,
            CaravanMaterialPortRole second)
        {
            Assert.That(
                CaravanConnectionRules.AreCompatible(kind, first, second),
                Is.True);
            Assert.That(
                CaravanConnectionRules.AreCompatible(kind, second, first),
                Is.True);
        }

        [Test]
        public void MaterialCompatibilityRejectsCrossNetworkAndUndeclaredPairs()
        {
            Assert.That(
                CaravanConnectionRules.AreCompatible(
                    CaravanMaterialNetworkKind.Biomass,
                    CaravanMaterialPortRole.WetBiomassOutput,
                    CaravanMaterialPortRole.MechanicalConsumer),
                Is.False);
            Assert.That(
                CaravanConnectionRules.AreCompatible(
                    CaravanMaterialNetworkKind.Mechanical,
                    CaravanMaterialPortRole.TransmissionInput,
                    CaravanMaterialPortRole.TransmissionOutput),
                Is.False);
            Assert.That(
                CaravanConnectionRules.AreCompatible(
                    CaravanMaterialNetworkKind.Biomass,
                    CaravanMaterialPortRole.WetBiomassInput,
                    CaravanMaterialPortRole.DryBiomassOutput),
                Is.False);
        }

        [Test]
        public void MaterialRolesMapToOneStableNetworkKind()
        {
            var roles = (CaravanMaterialPortRole[])Enum.GetValues(
                typeof(CaravanMaterialPortRole));
            for (var index = 0; index < roles.Length; index++)
            {
                var expected = roles[index]
                               <= CaravanMaterialPortRole.DryBiomassInput
                    ? CaravanMaterialNetworkKind.Biomass
                    : CaravanMaterialNetworkKind.Mechanical;
                Assert.That(
                    CaravanConnectionRules.GetNetworkKind(roles[index]),
                    Is.EqualTo(expected));
            }

            Assert.Throws<ArgumentOutOfRangeException>(
                () => CaravanConnectionRules.GetNetworkKind(
                    (CaravanMaterialPortRole)999));
        }

        [TestCase(-2f, -1f, 0f)]
        [TestCase(-1f, -1f, 0f)]
        [TestCase(0f, 0f, 0.5f)]
        [TestCase(1f, 1f, 1f)]
        [TestCase(2f, 1f, 1f)]
        public void ControlModelClampsAndMapsToOperatingLevel(
            float input,
            float expectedControl,
            float expectedLevel)
        {
            Assert.That(
                CaravanControlModel.Clamp(input),
                Is.EqualTo(expectedControl).Within(0.0001f));
            Assert.That(
                CaravanControlModel.ToOperatingLevel(input),
                Is.EqualTo(expectedLevel).Within(0.0001f));
        }

        [TestCase(-1f, CaravanPumpMode.Extraction)]
        [TestCase(-0.251f, CaravanPumpMode.Extraction)]
        [TestCase(-0.25f, CaravanPumpMode.Off)]
        [TestCase(0f, CaravanPumpMode.Off)]
        [TestCase(0.25f, CaravanPumpMode.Off)]
        [TestCase(0.251f, CaravanPumpMode.Circulation)]
        [TestCase(1f, CaravanPumpMode.Circulation)]
        public void ControlModelMapsPumpDeadZone(
            float control,
            CaravanPumpMode expected)
        {
            Assert.That(CaravanControlModel.ToPumpMode(control), Is.EqualTo(expected));
            Assert.That(
                CaravanControlModel.ToPumpMode(
                    CaravanControlModel.FromPumpMode(expected)),
                Is.EqualTo(expected));
        }

        [Test]
        public void FuelModelRoundTripsRequestedEnginePower()
        {
            const float requestedKilowatts = 55f;
            const float seconds = 90f;
            var requiredFuel = CaravanFuelModel.RequiredFuelKilograms(
                requestedKilowatts,
                CaravanFuelModel.EngineEfficiency,
                seconds);
            var flow = CaravanFuelModel.Evaluate(
                requiredFuel,
                seconds,
                CaravanFuelModel.EngineEfficiency,
                requestedKilowatts,
                wasteHeatFraction:
                CaravanFuelModel.EngineWasteHeatFraction);

            Assert.That(
                flow.UsefulOutputKilowatts,
                Is.EqualTo(requestedKilowatts).Within(0.0001f));
            Assert.That(
                flow.WasteHeatKilowatts,
                Is.EqualTo(
                    flow.FuelPowerKilowatts
                    * CaravanFuelModel.EngineWasteHeatFraction)
                    .Within(0.0001f));
            Assert.That(
                flow.ConsumedFuelKilograms,
                Is.EqualTo(requiredFuel).Within(0.0001f));
        }

        [Test]
        public void FuelModelClampsInvalidInputsAndOutput()
        {
            var empty = CaravanFuelModel.Evaluate(
                -2f,
                -1f,
                -1f,
                -10f,
                -1f,
                2f);
            var limited = CaravanFuelModel.Evaluate(
                1f,
                1f,
                1f,
                10f);

            Assert.That(empty.ConsumedFuelKilograms, Is.Zero);
            Assert.That(empty.UsefulOutputKilowatts, Is.Zero);
            Assert.That(empty.WasteHeatKilowatts, Is.Zero);
            Assert.That(
                CaravanFuelModel.RequiredFuelKilograms(-10f, 0f, -5f),
                Is.Zero);
            Assert.That(limited.UsefulOutputKilowatts, Is.EqualTo(10f));
        }

        [Test]
        public void ElectricalModelClampsEnergyAtEmptyAndFullBoundaries()
        {
            var empty = CaravanElectricalModel.Evaluate(
                0f,
                80f,
                -20f,
                120f,
                36f,
                65f,
                0.94f,
                0.93f,
                1f);
            var full = CaravanElectricalModel.Evaluate(
                50f,
                0f,
                200f,
                120f,
                36f,
                65f,
                0.94f,
                0.93f,
                1f);

            Assert.That(empty.DeliveredKilowatts, Is.Zero);
            Assert.That(empty.StoredKilowattHours, Is.Zero);
            Assert.That(empty.DeficitKilowatts, Is.EqualTo(80f));
            Assert.That(full.BatteryChargeKilowatts, Is.Zero);
            Assert.That(full.StoredKilowattHours, Is.EqualTo(120f));
            Assert.That(full.SpilledKilowatts, Is.EqualTo(50f));
        }

        [Test]
        public void ElectricalModelDoesNotMoveStoredEnergyAtZeroDuration()
        {
            var flow = CaravanElectricalModel.Evaluate(
                20f,
                50f,
                40f,
                120f,
                36f,
                65f,
                0.94f,
                0.93f,
                0f);

            Assert.That(flow.BatteryChargeKilowatts, Is.Zero);
            Assert.That(flow.BatteryDischargeKilowatts, Is.Zero);
            Assert.That(flow.DeliveredKilowatts, Is.EqualTo(20f));
            Assert.That(flow.StoredKilowattHours, Is.EqualTo(40f));
        }

        [Test]
        public void WaterModelCannotCoolBelowAmbient()
        {
            var flow = CaravanWaterCircuitModel.Evaluate(
                1f,
                18.1f,
                18f,
                100f,
                1f,
                1000f,
                20f,
                600f,
                true);

            Assert.That(flow.FlowLitresPerSecond, Is.EqualTo(100f));
            Assert.That(flow.TemperatureCelsius, Is.EqualTo(18f));
            Assert.That(flow.CoolingKilowatts, Is.GreaterThan(0f));
        }

        [Test]
        public void BiomassModelClampsMoistureRateAndElapsedTime()
        {
            var dry = CaravanBiomassModel.Dry(
                5f,
                -1f,
                100f,
                2f,
                -1f,
                1f);
            var noTime = CaravanBiomassModel.Dry(
                5f,
                1f,
                100f,
                1f,
                1f,
                -1f);

            Assert.That(dry.ProcessedWetKilograms, Is.EqualTo(5f));
            Assert.That(dry.DryBiomassKilograms, Is.EqualTo(5f));
            Assert.That(dry.RecoveredWaterLitres, Is.Zero);
            Assert.That(noTime.ProcessedWetKilograms, Is.Zero);
        }

        [Test]
        public void RopeModelClampsNegativeParametersAndBreaksOnlyAboveLimit()
        {
            var negative = CaravanCouplingRopeModel.Evaluate(
                -5f,
                -1f,
                -100f,
                -10f);
            var atLimit = CaravanCouplingRopeModel.Evaluate(
                2f,
                1f,
                1000f,
                1000f);

            Assert.That(negative.TensionNewtons, Is.Zero);
            Assert.That(negative.Broken, Is.False);
            Assert.That(atLimit.TensionNewtons, Is.EqualTo(1000f));
            Assert.That(atLimit.Broken, Is.False);
        }
    }
}
