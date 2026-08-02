using System;
using System.Collections.Generic;
using NUnit.Framework;
using Steppe.Caravan;
using Steppe.Presentation;
using UnityEngine;

namespace Steppe.Tests
{
    public sealed class CaravanModuleBehaviorTests
    {
        private readonly List<UnityEngine.Object> cleanup =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (var index = cleanup.Count - 1; index >= 0; index--)
            {
                if (cleanup[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(cleanup[index]);
                }
            }
            cleanup.Clear();
        }

        [Test]
        public void ModuleStateClampsMutationsAndIgnoresNegativeMaintenance()
        {
            var state = new CaravanModuleState();

            state.AccumulateDust(2f);
            state.Damage(2f);
            state.SetLoad(2f);
            Assert.That(state.Dust, Is.EqualTo(1f));
            Assert.That(state.Integrity, Is.Zero);
            Assert.That(state.Load, Is.EqualTo(1f));
            Assert.That(state.Efficiency, Is.EqualTo(0.2688f).Within(0.0001f));

            state.Clean(-1f);
            state.Repair(-1f);
            Assert.That(state.Dust, Is.EqualTo(1f));
            Assert.That(state.Integrity, Is.Zero);

            state.Clean(2f);
            state.Repair(2f);
            Assert.That(state.Dust, Is.Zero);
            Assert.That(state.Integrity, Is.EqualTo(1f));
            Assert.That(state.Efficiency, Is.EqualTo(1f));
        }

        [Test]
        public void PartAndModuleClampResourceMassAndOutput()
        {
            var part = CreatePart(CaravanPartKind.Battery, 100f, 150f);
            var module = part.GetComponent<CaravanModule>();

            Assert.That(part.StoredAmount, Is.EqualTo(100f));
            part.SetStoredAmount(-10f);
            part.SetCurrentOutput(-20f);
            module.SetPayloadMass(-30f);
            Assert.That(part.StoredAmount, Is.Zero);
            Assert.That(part.CurrentOutput, Is.Zero);
            Assert.That(module.PayloadMassKilograms, Is.Zero);

            module.SetPayloadMass(45f);
            Assert.That(
                module.MassKilograms,
                Is.EqualTo(module.BaseMassKilograms + 45f));
        }

        [Test]
        public void PartDefinitionValidatesIdentityAndClampsPhysicalValues()
        {
            Assert.Throws<ArgumentException>(
                () => new CaravanPartDefinition(
                    CaravanPartKind.Sail,
                    " ",
                    "Sail",
                    1,
                    1,
                    1f,
                    1f));

            var definition = new CaravanPartDefinition(
                CaravanPartKind.Sail,
                "test-sail",
                string.Empty,
                -1,
                0,
                -10f,
                -20f);
            Assert.That(definition.DisplayName, Is.EqualTo("test-sail"));
            Assert.That(definition.FootprintWidth, Is.EqualTo(1));
            Assert.That(definition.FootprintLength, Is.EqualTo(1));
            Assert.That(definition.MassKilograms, Is.EqualTo(0.1f));
            Assert.That(definition.Capacity, Is.Zero);
        }

        [Test]
        public void ConstructionCatalogueProvidesRussianNamesAndDescriptions()
        {
            foreach (var definition in CaravanPartCatalog.All)
            {
                Assert.That(definition.DisplayName, Is.Not.Empty);
                Assert.That(definition.Description, Is.Not.Empty);
                Assert.That(
                    System.Text.RegularExpressions.Regex.IsMatch(
                        definition.DisplayName,
                        "[А-Яа-яЁё]"),
                    Is.True,
                    definition.Id);
                Assert.That(
                    System.Text.RegularExpressions.Regex.IsMatch(
                        definition.Description,
                        "[А-Яа-яЁё]"),
                    Is.True,
                    definition.Id);
            }
        }

        [Test]
        public void ImmediateModeUiScalesFromReferenceResolution()
        {
            Assert.That(SteppeGuiScale.CalculateScale(1920, 1080), Is.EqualTo(1f));
            Assert.That(SteppeGuiScale.CalculateScale(2560, 1440), Is.EqualTo(4f / 3f));
            Assert.That(SteppeGuiScale.CalculateScale(3840, 2160), Is.EqualTo(2f));
            Assert.That(
                SteppeGuiScale.CalculateScale(3440, 1440),
                Is.EqualTo(4f / 3f));
            Assert.That(
                SteppeGuiScale.CalculateScale(1280, 720),
                Is.EqualTo(SteppeGuiScale.MinimumScale));
        }

        [Test]
        public void GridSupportsRotationReplacementRemovalAndNegativeTurns()
        {
            var grid = new CaravanMountGridModel(4, 5);
            var module = new object();
            var normalized = new CaravanGridPlacement(0, 0, 2, 3, -1);

            Assert.That(normalized.QuarterTurns, Is.EqualTo(3));
            Assert.That(normalized.RotatedWidth, Is.EqualTo(3));
            Assert.That(normalized.RotatedLength, Is.EqualTo(2));
            Assert.That(grid.TryPlace(module, normalized), Is.True);
            Assert.That(
                grid.TryPlace(
                    module,
                    new CaravanGridPlacement(2, 2, 2, 3, 0)),
                Is.True);
            Assert.That(grid.Count, Is.EqualTo(1));
            Assert.That(grid.TryGetPlacement(module, out var moved), Is.True);
            Assert.That(moved.X, Is.EqualTo(2));
            Assert.That(grid.Remove(module, out var removed), Is.True);
            Assert.That(removed.X, Is.EqualTo(2));
            Assert.That(grid.Count, Is.Zero);
            Assert.That(grid.Remove(module, out _), Is.False);
        }

        [Test]
        public void BatteryHonoursPowerLimitsEfficienciesAndCapacity()
        {
            var part = CreatePart(CaravanPartKind.Battery, 100f, 50f);
            var battery = part.gameObject.AddComponent<CaravanBatteryModule>();
            battery.Configure(null, 20f, 30f, 0.5f, 0.5f);

            battery.BeginPowerStep();
            var accepted = battery.AcceptCharge(100f, 1f);
            var supplied = battery.SupplyPower(100f, 1f);
            battery.CompletePowerStep();

            Assert.That(accepted, Is.EqualTo(20f));
            Assert.That(supplied, Is.EqualTo(30f));
            Assert.That(battery.StoredEnergyKilowattHours, Is.Zero);
            Assert.That(battery.CurrentChargeKilowatts, Is.EqualTo(20f));
            Assert.That(battery.CurrentDischargeKilowatts, Is.EqualTo(30f));
            Assert.That(part.CurrentOutput, Is.EqualTo(30f));
            Assert.That(part.GetComponent<CaravanModule>().State.Load, Is.EqualTo(1f));
            Assert.That(battery.SupplyPower(10f, 0f), Is.Zero);
            Assert.That(battery.AcceptCharge(-10f, 1f), Is.Zero);
        }

        [Test]
        public void ElectricMotorClampsThrottlePowerAndConvertsMechanicalOutput()
        {
            var part = CreatePart(CaravanPartKind.ElectricMotor, 55f);
            var motor = part.gameObject.AddComponent<CaravanElectricMotorModule>();
            motor.Configure(null, 0.8f);

            motor.SetRequestedThrottle(2f);
            motor.ApplyDeliveredPower(100f);

            Assert.That(motor.RequestedThrottle, Is.EqualTo(1f));
            Assert.That(motor.RequestedPowerKilowatts, Is.EqualTo(55f));
            Assert.That(motor.DeliveredElectricalKilowatts, Is.EqualTo(55f));
            Assert.That(motor.DeliveredMechanicalKilowatts, Is.EqualTo(44f));
            Assert.That(motor.PowerAvailability, Is.EqualTo(1f));
            Assert.That(part.CurrentOutput, Is.EqualTo(44f));

            motor.SetRequestedThrottle(-1f);
            motor.ApplyDeliveredPower(10f);
            Assert.That(motor.DeliveredElectricalKilowatts, Is.Zero);
            Assert.That(motor.PowerAvailability, Is.Zero);
        }

        [Test]
        public void WaterReservoirTracksPayloadCapacityAndTemperature()
        {
            var part = CreatePart(
                CaravanPartKind.WaterReservoir,
                100f,
                25f,
                40f);
            var reservoir =
                part.gameObject.AddComponent<CaravanWaterReservoirModule>();
            reservoir.Configure();

            reservoir.SetStoredWater(150f);
            reservoir.SetTemperature(72f);

            Assert.That(reservoir.StoredWaterLitres, Is.EqualTo(100f));
            Assert.That(reservoir.TemperatureCelsius, Is.EqualTo(72f));
            Assert.That(
                part.GetComponent<CaravanModule>().PayloadMassKilograms,
                Is.EqualTo(100f));
            Assert.That(
                part.GetComponent<CaravanModule>().State.Load,
                Is.EqualTo(1f));
        }

        [Test]
        public void PumpMapsControlsAndCombinesMechanicalWithElectricalPower()
        {
            var part = CreatePart(CaravanPartKind.DualModePump, 24f);
            var pump = part.gameObject.AddComponent<CaravanElectricPumpModule>();
            pump.Configure(6f);

            pump.SetCircuitDemand(true);
            Assert.That(pump.Mode, Is.EqualTo(CaravanPumpMode.Circulation));
            Assert.That(pump.RequestedPowerKilowatts, Is.EqualTo(6f));

            pump.SetMechanicalPowerAvailability(0.5f);
            Assert.That(pump.RequestedPowerKilowatts, Is.EqualTo(3f));
            Assert.That(pump.PowerAvailability, Is.EqualTo(0.5f));
            pump.ApplyDeliveredPower(1.5f);
            Assert.That(pump.DeliveredElectricalKilowatts, Is.EqualTo(1.5f));
            Assert.That(pump.PowerAvailability, Is.EqualTo(0.5f));

            pump.SetControlNormalized(-1f);
            Assert.That(pump.Mode, Is.EqualTo(CaravanPumpMode.Extraction));
            pump.SetExtractionDemand(true);
            Assert.That(pump.RequestedPowerKilowatts, Is.EqualTo(3f));

            pump.SetControlNormalized(0f);
            Assert.That(pump.Mode, Is.EqualTo(CaravanPumpMode.Off));
            Assert.That(pump.RequestedPowerKilowatts, Is.Zero);
            Assert.That(pump.DeliveredElectricalKilowatts, Is.Zero);
        }

        [Test]
        public void RadiatorControlAndDamageReduceAvailableCooling()
        {
            var part = CreatePart(CaravanPartKind.Radiator, 32f);
            var module = part.GetComponent<CaravanModule>();
            var radiator = part.gameObject.AddComponent<CaravanRadiatorModule>();
            radiator.Configure();

            radiator.SetControlNormalized(-2f);
            Assert.That(radiator.ControlNormalized, Is.EqualTo(-1f));
            Assert.That(radiator.Opening, Is.Zero);

            radiator.SetControlNormalized(2f);
            module.Damage(0.5f);
            Assert.That(radiator.ControlNormalized, Is.EqualTo(1f));
            Assert.That(radiator.Opening, Is.EqualTo(1f));
            Assert.That(radiator.Efficiency, Is.LessThan(1f));
        }

        [Test]
        public void HarvesterPreservesMixtureAndNeverExceedsCapacity()
        {
            var part = CreatePart(CaravanPartKind.Harvester, 10f);
            var harvester = part.gameObject.AddComponent<CaravanHarvesterModule>();
            harvester.Configure(10f);
            harvester.SetControlNormalized(1f);
            harvester.SetProcessDemand(true);
            harvester.SetMechanicalPowerAvailability(0.5f);
            harvester.ApplyDeliveredPower(100f);
            harvester.AcceptHarvest(8f, 8f);

            Assert.That(harvester.RequestedPowerKilowatts, Is.EqualTo(5f));
            Assert.That(harvester.DeliveredElectricalKilowatts, Is.EqualTo(5f));
            Assert.That(harvester.PowerAvailability, Is.EqualTo(1f));
            Assert.That(harvester.StoredWetBiomassKilograms, Is.EqualTo(10f));
            Assert.That(harvester.MoistureFraction, Is.EqualTo(0.5f));
            Assert.That(harvester.TotalHarvestedKilograms, Is.EqualTo(10f));

            var transfer = harvester.TransferWet(4f);
            Assert.That(transfer.ProcessedWetKilograms, Is.EqualTo(4f));
            Assert.That(transfer.DryBiomassKilograms, Is.EqualTo(2f));
            Assert.That(transfer.RecoveredWaterLitres, Is.EqualTo(2f));
            Assert.That(harvester.StoredWetBiomassKilograms, Is.EqualTo(6f));
        }

        [Test]
        public void DryerConservesMatterAndKeepsReturnedOverflow()
        {
            var part = CreatePart(CaravanPartKind.GrassDryer, 10f);
            var dryer = part.gameObject.AddComponent<CaravanGrassDryerModule>();
            dryer.Configure(10f, 2f);
            dryer.SetControlNormalized(1f);
            dryer.AcceptWet(new CaravanBiomassConversion(10f, 5f, 5f));
            dryer.SetProcessDemand(true);
            dryer.ApplyDeliveredPower(10f);

            var result = dryer.SimulateDrying(0f, 2f);
            Assert.That(result.ProcessedWetKilograms, Is.EqualTo(4f));
            Assert.That(result.DryBiomassKilograms, Is.EqualTo(2f));
            Assert.That(result.RecoveredWaterLitres, Is.EqualTo(2f));
            Assert.That(dryer.StoredWetBiomassKilograms, Is.EqualTo(6f));
            Assert.That(dryer.DryOutputKilograms, Is.EqualTo(2f));

            Assert.That(dryer.TransferDry(10f), Is.EqualTo(2f));
            dryer.ReturnDry(10f);
            Assert.That(dryer.DryOutputKilograms, Is.EqualTo(4f));
            Assert.That(
                dryer.StoredWetBiomassKilograms + dryer.DryOutputKilograms,
                Is.EqualTo(10f));
        }

        [Test]
        public void BiomassStorageAcceptsAndSuppliesWithoutCreatingMatter()
        {
            var part = CreatePart(CaravanPartKind.BiomassStorage, 10f);
            var storage =
                part.gameObject.AddComponent<CaravanBiomassStorageModule>();
            storage.Configure();

            Assert.That(storage.Accept(15f), Is.EqualTo(10f));
            Assert.That(storage.Accept(1f), Is.Zero);
            Assert.That(storage.Supply(4f), Is.EqualTo(4f));
            Assert.That(storage.Supply(10f), Is.EqualTo(6f));
            Assert.That(storage.Supply(-1f), Is.Zero);
            Assert.That(storage.StoredDryBiomassKilograms, Is.Zero);
            Assert.That(
                part.GetComponent<CaravanModule>().PayloadMassKilograms,
                Is.Zero);
        }

        [Test]
        public void BiofurnaceConvertsExactlyRequestedFuelAndRespectsDamage()
        {
            var part = CreatePart(CaravanPartKind.Biofurnace, 80f);
            var module = part.GetComponent<CaravanModule>();
            var furnace = part.gameObject.AddComponent<CaravanBiofurnaceModule>();
            furnace.Configure();
            furnace.SetControlNormalized(1f);

            var fuel = furnace.RequestedFuelKilograms(60f);
            furnace.SimulateFuel(fuel, 60f);
            Assert.That(furnace.ThermalOutputKilowatts, Is.EqualTo(80f).Within(0.001f));
            Assert.That(
                furnace.TotalFuelConsumedKilograms,
                Is.EqualTo(fuel).Within(0.0001f));

            module.Damage(0.5f);
            furnace.SimulateFuel(fuel, 60f);
            Assert.That(furnace.ThermalOutputKilowatts, Is.LessThan(80f));
            Assert.That(part.TemperatureCelsius, Is.GreaterThan(18f));
        }

        [Test]
        public void BiofuelEngineRequiresControlThrottleCouplingAndFuel()
        {
            var part = CreatePart(CaravanPartKind.BiofuelEngine, 55f);
            var engine =
                part.gameObject.AddComponent<CaravanBiofuelEngineModule>();
            engine.Configure();
            engine.SetControlNormalized(1f);
            engine.SetDriveCoupled(true);
            engine.SetRequestedThrottle(1f);

            var fuel = engine.RequestedFuelKilograms(60f);
            engine.SimulateFuel(fuel, 60f);
            Assert.That(engine.RequestedMechanicalKilowatts, Is.EqualTo(55f));
            Assert.That(
                engine.DeliveredMechanicalKilowatts,
                Is.EqualTo(55f).Within(0.001f));
            Assert.That(engine.WasteHeatKilowatts, Is.GreaterThan(55f));

            engine.SetDriveCoupled(false);
            engine.SimulateFuel(0f, 60f);
            Assert.That(engine.RequestedMechanicalKilowatts, Is.Zero);
            Assert.That(engine.DeliveredMechanicalKilowatts, Is.Zero);
            Assert.That(engine.WasteHeatKilowatts, Is.Zero);
        }

        [Test]
        public void TransmissionMapsFullGearRangeAndEngagement()
        {
            var part = CreatePart(CaravanPartKind.Transmission, 8f);
            var transmission =
                part.gameObject.AddComponent<CaravanTransmissionModule>();
            transmission.Configure();

            transmission.SetControlNormalized(-2f);
            Assert.That(transmission.ControlNormalized, Is.EqualTo(-1f));
            Assert.That(transmission.TorqueMultiplier, Is.EqualTo(1.65f));
            Assert.That(transmission.SpeedMultiplier, Is.EqualTo(0.62f));

            transmission.SetEngaged(true);
            Assert.That(transmission.IsEngaged, Is.True);
            Assert.That(part.CurrentOutput, Is.EqualTo(0.62f));

            transmission.SetControlNormalized(2f);
            Assert.That(transmission.TorqueMultiplier, Is.EqualTo(0.72f));
            Assert.That(transmission.SpeedMultiplier, Is.EqualTo(1.38f));
            transmission.SetEngaged(false);
            Assert.That(part.CurrentOutput, Is.Zero);
        }

        [Test]
        public void CouplingRopesLinkBreakRepairAndUnlinkSymmetrically()
        {
            var firstPart = CreatePart(CaravanPartKind.CouplingRope, 24f);
            var secondPart = CreatePart(CaravanPartKind.CouplingRope, 24f);
            var first =
                firstPart.gameObject.AddComponent<CaravanCouplingRopeModule>();
            var second =
                secondPart.gameObject.AddComponent<CaravanCouplingRopeModule>();
            first.Configure(10f, 5000f);
            second.Configure(10f, 5000f);

            first.LinkTo(second);
            Assert.That(first.LinkedRope, Is.SameAs(second));
            Assert.That(second.LinkedRope, Is.SameAs(first));

            var broken = first.EvaluateDistance(20f);
            Assert.That(broken.Broken, Is.True);
            Assert.That(first.IsBroken, Is.True);
            Assert.That(
                firstPart.GetComponent<CaravanModule>().State.Integrity,
                Is.EqualTo(0.75f));

            first.RepairRope();
            Assert.That(first.IsBroken, Is.False);
            Assert.That(first.CurrentTensionNewtons, Is.Zero);
            first.Unlink();
            Assert.That(first.IsLinked, Is.False);
            Assert.That(second.IsLinked, Is.False);
        }

        [Test]
        public void TypedModulesRejectPartsWithWrongTechnicalKind()
        {
            var batteryPart = CreatePart(CaravanPartKind.Sail, 10f);
            var motorPart = CreatePart(CaravanPartKind.Sail, 10f);
            var reservoirPart = CreatePart(CaravanPartKind.Sail, 10f);
            var pumpPart = CreatePart(CaravanPartKind.Sail, 10f);
            var radiatorPart = CreatePart(CaravanPartKind.Sail, 10f);

            Assert.Throws<InvalidOperationException>(
                () => batteryPart.gameObject
                    .AddComponent<CaravanBatteryModule>()
                    .Configure(null));
            Assert.Throws<InvalidOperationException>(
                () => motorPart.gameObject
                    .AddComponent<CaravanElectricMotorModule>()
                    .Configure(null));
            Assert.Throws<InvalidOperationException>(
                () => reservoirPart.gameObject
                    .AddComponent<CaravanWaterReservoirModule>()
                    .Configure());
            Assert.Throws<InvalidOperationException>(
                () => pumpPart.gameObject
                    .AddComponent<CaravanElectricPumpModule>()
                    .Configure());
            Assert.Throws<InvalidOperationException>(
                () => radiatorPart.gameObject
                    .AddComponent<CaravanRadiatorModule>()
                    .Configure());
        }

        [Test]
        public void OnboardingAdvancesThroughConnectionsThrottleAndTravel()
        {
            var onboarding = new CaravanOnboardingModel();

            onboarding.Update(false, true, 1f, 500f);
            Assert.That(
                onboarding.Stage,
                Is.EqualTo(CaravanOnboardingStage.ConnectSolarToBattery));

            onboarding.Update(true, false, 0f, 0f);
            Assert.That(
                onboarding.Stage,
                Is.EqualTo(CaravanOnboardingStage.ConnectBatteryToMotor));

            onboarding.Update(true, true, 0f, -10f);
            Assert.That(
                onboarding.Stage,
                Is.EqualTo(CaravanOnboardingStage.SetThrottle));
            Assert.That(onboarding.TravelledMetres, Is.Zero);

            onboarding.Update(true, true, 0.2f, 0f);
            Assert.That(
                onboarding.Stage,
                Is.EqualTo(CaravanOnboardingStage.StartMoving));

            onboarding.Update(true, true, 0.2f, 119f);
            Assert.That(
                onboarding.Stage,
                Is.EqualTo(CaravanOnboardingStage.StartMoving));

            onboarding.Update(true, true, 0.2f, 1f);
            Assert.That(
                onboarding.Stage,
                Is.EqualTo(CaravanOnboardingStage.Complete));
            Assert.That(onboarding.GetTitle(), Is.Not.Empty);
            Assert.That(onboarding.GetInstruction(), Is.Not.Empty);
        }

        [Test]
        public void BatteryPortDefaultsToDistributionCapacity()
        {
            var batteryPart = CreatePart(CaravanPartKind.Battery, 120f);
            var batteryPortObject = new GameObject("Battery Electrical Port");
            cleanup.Add(batteryPortObject);
            batteryPortObject.transform.SetParent(batteryPart.transform, false);
            var batteryPort =
                batteryPortObject.AddComponent<CaravanElectricalPort>();
            batteryPort.Configure(
                batteryPart,
                CaravanElectricalPortKind.Storage);

            Assert.That(
                batteryPort.MaximumConnections,
                Is.EqualTo(
                    CaravanElectricalPort.DefaultStorageConnectionCapacity));
            Assert.That(batteryPort.MaximumConnections, Is.GreaterThan(2));

            var motorPart = CreatePart(CaravanPartKind.ElectricMotor, 55f);
            var motorPortObject = new GameObject("Motor Electrical Port");
            cleanup.Add(motorPortObject);
            motorPortObject.transform.SetParent(motorPart.transform, false);
            var motorPort =
                motorPortObject.AddComponent<CaravanElectricalPort>();
            motorPort.Configure(
                motorPart,
                CaravanElectricalPortKind.Consumer);

            Assert.That(motorPort.MaximumConnections, Is.EqualTo(1));
        }

        [Test]
        public void MotorFeedbackExplainsDisconnectedPowerAndMaintenance()
        {
            var part = CreatePart(CaravanPartKind.ElectricMotor, 55f);
            var module = part.GetComponent<CaravanModule>();
            var motor = part.gameObject.AddComponent<CaravanElectricMotorModule>();
            motor.Configure(null);
            var portObject = new GameObject("Electrical Port");
            cleanup.Add(portObject);
            portObject.transform.SetParent(part.transform, false);
            portObject.AddComponent<CaravanElectricalPort>().Configure(
                part,
                CaravanElectricalPortKind.Consumer);

            motor.SetRequestedThrottle(0.8f);
            var disconnected = CaravanModuleFeedbackBuilder.Evaluate(module);
            Assert.That(
                disconnected.State,
                Is.EqualTo(CaravanOperationalState.Starved));
            Assert.That(disconnected.Reason, Does.Contain("не подключён"));

            module.Damage(0.8f);
            var damaged = CaravanModuleFeedbackBuilder.Evaluate(module);
            Assert.That(
                damaged.State,
                Is.EqualTo(CaravanOperationalState.Damaged));
            Assert.That(damaged.Reason, Does.Contain("R"));
        }

        [Test]
        public void FirstExpeditionRequiresRealResourcesAndEnvironmentalWindows()
        {
            var expedition = new SteppeFirstExpeditionModel();

            expedition.Update(ExpeditionSignals(introComplete: true));
            Assert.That(
                expedition.Stage,
                Is.EqualTo(SteppeExpeditionStage.AssembleWaterKit));

            expedition.Update(ExpeditionSignals(
                introComplete: true,
                hasReservoir: true,
                hasPump: true,
                hasRadiator: true));
            Assert.That(
                expedition.Stage,
                Is.EqualTo(SteppeExpeditionStage.ConnectWaterLoop));

            expedition.Update(ExpeditionSignals(
                introComplete: true,
                hasReservoir: true,
                hasPump: true,
                hasRadiator: true,
                waterLoopClosed: true));
            expedition.Update(ExpeditionSignals(
                introComplete: true,
                hasReservoir: true,
                hasPump: true,
                hasRadiator: true,
                waterLoopClosed: true,
                pumpPowered: true));
            expedition.Update(ExpeditionSignals(
                introComplete: true,
                hasReservoir: true,
                hasPump: true,
                hasRadiator: true,
                waterLoopClosed: true,
                pumpPowered: true,
                pumpExtracting: true));
            Assert.That(
                expedition.Stage,
                Is.EqualTo(SteppeExpeditionStage.FindWetGround));

            expedition.Update(ExpeditionSignals(
                introComplete: true,
                hasReservoir: true,
                hasPump: true,
                hasRadiator: true,
                waterLoopClosed: true,
                pumpPowered: true,
                pumpExtracting: true,
                wetGround: true,
                storedWaterLitres: 315f));
            Assert.That(
                expedition.Stage,
                Is.EqualTo(SteppeExpeditionStage.CollectWater));

            expedition.Update(ExpeditionSignals(
                storedWaterLitres: 344.9f));
            Assert.That(
                expedition.Stage,
                Is.EqualTo(SteppeExpeditionStage.CollectWater));
            expedition.Update(ExpeditionSignals(
                storedWaterLitres: 345f));
            Assert.That(
                expedition.Stage,
                Is.EqualTo(SteppeExpeditionStage.AssembleBiomassKit));

            expedition.Update(ExpeditionSignals(
                hasHarvester: true,
                hasDryer: true,
                hasBiomassStorage: true));
            expedition.Update(ExpeditionSignals(
                hasHarvester: true,
                hasDryer: true,
                hasBiomassStorage: true,
                biomassChainConnected: true));
            expedition.Update(ExpeditionSignals(
                harvesterPowered: true));
            expedition.Update(ExpeditionSignals(
                harvesterEnabled: true));
            Assert.That(
                expedition.Stage,
                Is.EqualTo(SteppeExpeditionStage.FindGrass));

            expedition.Update(ExpeditionSignals(
                grassAvailable: true,
                harvestedKilograms: 10f));
            expedition.Update(ExpeditionSignals(
                harvestedKilograms: 13.9f));
            Assert.That(
                expedition.Stage,
                Is.EqualTo(SteppeExpeditionStage.HarvestGrass));
            expedition.Update(ExpeditionSignals(
                harvestedKilograms: 14f));
            Assert.That(
                expedition.Stage,
                Is.EqualTo(SteppeExpeditionStage.FindDryingWind));

            expedition.Update(ExpeditionSignals(
                dryingWeather: true,
                storedDryBiomassKilograms: 5f));
            expedition.Update(ExpeditionSignals(
                storedDryBiomassKilograms: 6.9f));
            Assert.That(
                expedition.Stage,
                Is.EqualTo(SteppeExpeditionStage.DryBiomass));
            expedition.Update(ExpeditionSignals(
                storedDryBiomassKilograms: 7f));
            Assert.That(
                expedition.Stage,
                Is.EqualTo(SteppeExpeditionStage.ReturnToLandmark));

            expedition.Update(ExpeditionSignals(atLandmark: true));
            Assert.That(
                expedition.Stage,
                Is.EqualTo(SteppeExpeditionStage.Complete));
        }

        private static SteppeExpeditionSignals ExpeditionSignals(
            bool introComplete = false,
            bool hasReservoir = false,
            bool hasPump = false,
            bool hasRadiator = false,
            bool waterLoopClosed = false,
            bool pumpPowered = false,
            bool pumpExtracting = false,
            bool wetGround = false,
            float storedWaterLitres = 0f,
            bool hasHarvester = false,
            bool hasDryer = false,
            bool hasBiomassStorage = false,
            bool biomassChainConnected = false,
            bool harvesterPowered = false,
            bool harvesterEnabled = false,
            bool grassAvailable = false,
            float harvestedKilograms = 0f,
            bool dryingWeather = false,
            float storedDryBiomassKilograms = 0f,
            bool atLandmark = false)
        {
            return new SteppeExpeditionSignals(
                introComplete,
                hasReservoir,
                hasPump,
                hasRadiator,
                waterLoopClosed,
                pumpPowered,
                pumpExtracting,
                wetGround,
                storedWaterLitres,
                hasHarvester,
                hasDryer,
                hasBiomassStorage,
                biomassChainConnected,
                harvesterPowered,
                harvesterEnabled,
                grassAvailable,
                harvestedKilograms,
                dryingWeather,
                storedDryBiomassKilograms,
                atLandmark);
        }

        private CaravanPart CreatePart(
            CaravanPartKind kind,
            float capacity = 0f,
            float storedAmount = 0f,
            float temperatureCelsius = 18f)
        {
            var definition = CaravanPartCatalog.Get(kind);
            var gameObject = new GameObject($"{kind} Test Module");
            cleanup.Add(gameObject);
            var module = gameObject.AddComponent<CaravanModule>();
            module.Configure(
                definition.Id,
                gameObject.transform,
                true,
                definition.FootprintWidth,
                definition.FootprintLength,
                massKilograms: definition.MassKilograms);
            var part = gameObject.AddComponent<CaravanPart>();
            part.Configure(
                kind,
                capacity,
                storedAmount,
                temperatureCelsius);
            return part;
        }
    }
}
