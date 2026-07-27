using System.Collections.Generic;
using NUnit.Framework;
using Steppe.Caravan;
using UnityEngine;

namespace Steppe.Tests
{
    public sealed class CaravanTestGenerator :
        MonoBehaviour,
        ICaravanElectricalGenerator
    {
        public CaravanPart ElectricalPart { get; private set; }
        public float AvailableGenerationKilowatts { get; private set; }

        public void Configure(CaravanPart part, float generationKilowatts)
        {
            ElectricalPart = part;
            AvailableGenerationKilowatts = Mathf.Max(0f, generationKilowatts);
        }
    }

    public sealed class CaravanTestConsumer :
        MonoBehaviour,
        ICaravanElectricalConsumer
    {
        public CaravanPart ElectricalPart { get; private set; }
        public float RequestedPowerKilowatts { get; private set; }
        public float DeliveredElectricalKilowatts { get; private set; }

        public void Configure(CaravanPart part, float requestKilowatts)
        {
            ElectricalPart = part;
            RequestedPowerKilowatts = Mathf.Max(0f, requestKilowatts);
        }

        public void ApplyDeliveredPower(float electricalKilowatts)
        {
            DeliveredElectricalKilowatts = Mathf.Clamp(
                electricalKilowatts,
                0f,
                RequestedPowerKilowatts);
        }
    }

    public sealed class CaravanNetworkBehaviorTests
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
        public void ElectricalNetworkBalancesConnectedComponentAndIgnoresOpenCable()
        {
            var positive = CreateMaterial(Color.red);
            var negative = CreateMaterial(Color.black);
            var chassisObject = CreateObject("Chassis");
            var chassis =
                chassisObject.AddComponent<CaravanChassisController>();

            var generatorPart = CreatePart(
                CaravanPartKind.PhotovoltaicLeaves,
                20f);
            var generator =
                generatorPart.gameObject.AddComponent<CaravanTestGenerator>();
            generator.Configure(generatorPart, 20f);
            var generatorPort = CreateElectricalPort(
                generatorPart,
                CaravanElectricalPortKind.Generator);

            var batteryPart = CreatePart(
                CaravanPartKind.Battery,
                100f,
                10f);
            var battery =
                batteryPart.gameObject.AddComponent<CaravanBatteryModule>();
            battery.Configure(null, 100f, 100f, 1f, 1f);
            var batteryPort = CreateElectricalPort(
                batteryPart,
                CaravanElectricalPortKind.Storage,
                2);

            var consumerPart = CreatePart(
                CaravanPartKind.ElectricMotor,
                30f);
            var consumer =
                consumerPart.gameObject.AddComponent<CaravanTestConsumer>();
            consumer.Configure(consumerPart, 30f);
            var consumerPort = CreateElectricalPort(
                consumerPart,
                CaravanElectricalPortKind.Consumer);

            var networkObject = CreateObject("Electrical Network");
            var network =
                networkObject.AddComponent<CaravanElectricalNetwork>();
            network.Configure(
                chassis,
                new[] { generatorPort, batteryPort, consumerPort },
                positive,
                negative);

            Assert.That(network.RegisterPort(generatorPort), Is.False);
            Assert.That(network.CanConnect(generatorPort, consumerPort), Is.False);
            Assert.That(network.CanConnect(generatorPort, generatorPort), Is.False);
            Assert.That(
                network.TryConnect(generatorPort, batteryPort, out var sourceCable),
                Is.True);
            Assert.That(
                network.TryConnect(batteryPort, consumerPort, out var loadCable),
                Is.True);
            Assert.That(
                network.TryConnect(generatorPort, batteryPort),
                Is.False);

            network.Simulate(3600f);
            Assert.That(network.ConnectedComponentCount, Is.EqualTo(1));
            Assert.That(network.GeneratedKilowatts, Is.EqualTo(20f));
            Assert.That(network.RequestedKilowatts, Is.EqualTo(30f));
            Assert.That(network.DeliveredKilowatts, Is.EqualTo(30f));
            Assert.That(network.DeficitKilowatts, Is.Zero);
            Assert.That(consumer.DeliveredElectricalKilowatts, Is.EqualTo(30f));
            Assert.That(battery.StoredEnergyKilowattHours, Is.Zero);
            Assert.That(sourceCable.CurrentKilowatts, Is.EqualTo(20f));
            Assert.That(loadCable.CurrentKilowatts, Is.EqualTo(30f));

            loadCable.gameObject.SetActive(false);
            network.Simulate(3600f);
            Assert.That(network.ConnectedComponentCount, Is.EqualTo(1));
            Assert.That(consumer.DeliveredElectricalKilowatts, Is.Zero);
            Assert.That(network.DeficitKilowatts, Is.EqualTo(30f));
            Assert.That(battery.StoredEnergyKilowattHours, Is.EqualTo(20f));
            Assert.That(loadCable.CurrentKilowatts, Is.Zero);
        }

        [Test]
        public void FluidNetworkRequiresExactClosedLoopAndPoweredPump()
        {
            var material = CreateMaterial(Color.cyan);
            var reservoirPart = CreatePart(
                CaravanPartKind.WaterReservoir,
                100f,
                100f,
                70f);
            var reservoir =
                reservoirPart.gameObject
                    .AddComponent<CaravanWaterReservoirModule>();
            reservoir.Configure();
            var supply = CreateFluidPort(
                reservoirPart,
                CaravanFluidPortRole.ReservoirSupply);
            var returnPort = CreateFluidPort(
                reservoirPart,
                CaravanFluidPortRole.ReservoirReturn);

            var pumpPart = CreatePart(CaravanPartKind.DualModePump, 24f);
            var pump =
                pumpPart.gameObject.AddComponent<CaravanElectricPumpModule>();
            pump.Configure();
            pump.SetMechanicalPowerAvailability(1f);
            var pumpInlet = CreateFluidPort(
                pumpPart,
                CaravanFluidPortRole.PumpInlet);
            var pumpOutlet = CreateFluidPort(
                pumpPart,
                CaravanFluidPortRole.PumpOutlet);

            var radiatorPart = CreatePart(CaravanPartKind.Radiator, 32f);
            var radiator =
                radiatorPart.gameObject.AddComponent<CaravanRadiatorModule>();
            radiator.Configure();
            var radiatorInlet = CreateFluidPort(
                radiatorPart,
                CaravanFluidPortRole.RadiatorInlet);
            var radiatorOutlet = CreateFluidPort(
                radiatorPart,
                CaravanFluidPortRole.RadiatorOutlet);

            var networkObject = CreateObject("Fluid Network");
            var network = networkObject.AddComponent<CaravanFluidNetwork>();
            network.Configure(material);
            Assert.That(
                network.RegisterModule(reservoirPart.GetComponent<CaravanModule>()),
                Is.EqualTo(2));
            Assert.That(
                network.RegisterModule(pumpPart.GetComponent<CaravanModule>()),
                Is.EqualTo(2));
            Assert.That(
                network.RegisterModule(radiatorPart.GetComponent<CaravanModule>()),
                Is.EqualTo(2));
            Assert.That(network.CanConnect(supply, radiatorInlet), Is.False);
            Assert.That(network.TryConnect(supply, pumpInlet), Is.True);
            Assert.That(network.TryConnect(pumpOutlet, radiatorInlet), Is.True);
            Assert.That(network.IsClosed, Is.False);
            Assert.That(network.TryConnect(radiatorOutlet, returnPort), Is.True);
            Assert.That(network.IsClosed, Is.True);

            network.Simulate(10f);
            Assert.That(network.CurrentFlowLitresPerSecond, Is.EqualTo(24f));
            Assert.That(network.CurrentCoolingKilowatts, Is.GreaterThan(0f));
            Assert.That(reservoir.TemperatureCelsius, Is.LessThan(70f));
            Assert.That(pump.CurrentFlowLitresPerSecond, Is.EqualTo(24f));
            Assert.That(radiator.CurrentCoolingKilowatts, Is.GreaterThan(0f));

            network.Pipes[1].gameObject.SetActive(false);
            network.Simulate(10f);
            Assert.That(network.IsClosed, Is.False);
            Assert.That(network.CurrentFlowLitresPerSecond, Is.Zero);
            Assert.That(pump.CurrentFlowLitresPerSecond, Is.Zero);
        }

        [Test]
        public void FluidNetworkStopsFrozenWaterAndAppliesHeatOnlyToClosedCircuit()
        {
            var material = CreateMaterial(Color.cyan);
            var reservoirPart = CreatePart(
                CaravanPartKind.WaterReservoir,
                100f,
                100f,
                -4f);
            var reservoir =
                reservoirPart.gameObject
                    .AddComponent<CaravanWaterReservoirModule>();
            reservoir.Configure();
            var supply = CreateFluidPort(
                reservoirPart,
                CaravanFluidPortRole.ReservoirSupply);
            var returnPort = CreateFluidPort(
                reservoirPart,
                CaravanFluidPortRole.ReservoirReturn);

            var pumpPart = CreatePart(CaravanPartKind.DualModePump, 24f);
            var pump =
                pumpPart.gameObject.AddComponent<CaravanElectricPumpModule>();
            pump.Configure();
            pump.SetMechanicalPowerAvailability(1f);
            var pumpInlet = CreateFluidPort(
                pumpPart,
                CaravanFluidPortRole.PumpInlet);
            var pumpOutlet = CreateFluidPort(
                pumpPart,
                CaravanFluidPortRole.PumpOutlet);

            var radiatorPart = CreatePart(CaravanPartKind.Radiator, 32f);
            radiatorPart.gameObject.AddComponent<CaravanRadiatorModule>()
                .Configure();
            var radiatorInlet = CreateFluidPort(
                radiatorPart,
                CaravanFluidPortRole.RadiatorInlet);
            var radiatorOutlet = CreateFluidPort(
                radiatorPart,
                CaravanFluidPortRole.RadiatorOutlet);

            var networkObject = CreateObject("Frozen Fluid Network");
            var network = networkObject.AddComponent<CaravanFluidNetwork>();
            network.Configure(material);
            network.RegisterModule(
                reservoirPart.GetComponent<CaravanModule>());
            network.RegisterModule(pumpPart.GetComponent<CaravanModule>());
            network.RegisterModule(radiatorPart.GetComponent<CaravanModule>());
            network.TryConnect(supply, pumpInlet);
            network.TryConnect(pumpOutlet, radiatorInlet);
            network.TryConnect(radiatorOutlet, returnPort);
            network.SetExternalHeatingKilowatts(80f);

            network.Simulate(10f);
            Assert.That(network.IsClosed, Is.True);
            Assert.That(network.LiquidFraction, Is.Zero);
            Assert.That(network.CurrentFlowLitresPerSecond, Is.Zero);
            Assert.That(network.CurrentHeatingKilowatts, Is.Zero);
            Assert.That(reservoir.TemperatureCelsius, Is.EqualTo(-4f));
        }

        [Test]
        public void MaterialNetworkBuildsPathAcrossTransmissionAndDetectsBreak()
        {
            var material = CreateMaterial(Color.yellow);
            var sourcePart = CreatePart(CaravanPartKind.BiofuelEngine, 55f);
            var source = CreateMaterialPort(
                sourcePart,
                CaravanMaterialPortRole.MechanicalSource);

            var transmissionPart = CreatePart(
                CaravanPartKind.Transmission,
                8f);
            var transmissionInput = CreateMaterialPort(
                transmissionPart,
                CaravanMaterialPortRole.TransmissionInput);
            var transmissionOutput = CreateMaterialPort(
                transmissionPart,
                CaravanMaterialPortRole.TransmissionOutput);

            var consumerPart = CreatePart(CaravanPartKind.Harvester, 90f);
            var consumer = CreateMaterialPort(
                consumerPart,
                CaravanMaterialPortRole.MechanicalConsumer);

            var wrongPart = CreatePart(CaravanPartKind.GrassDryer, 10f);
            var wrongPort = CreateMaterialPort(
                wrongPart,
                CaravanMaterialPortRole.WetBiomassInput);

            var networkObject = CreateObject("Mechanical Network");
            var network = networkObject.AddComponent<CaravanMaterialNetwork>();
            network.Configure(
                CaravanMaterialNetworkKind.Mechanical,
                material);
            Assert.That(network.RegisterPort(wrongPort), Is.False);
            Assert.That(network.RegisterPort(source), Is.True);
            Assert.That(network.RegisterPort(transmissionInput), Is.True);
            Assert.That(network.RegisterPort(transmissionOutput), Is.True);
            Assert.That(network.RegisterPort(consumer), Is.True);
            Assert.That(network.RegisterPort(source), Is.False);
            Assert.That(
                network.CanConnect(transmissionInput, transmissionOutput),
                Is.False);
            Assert.That(network.TryConnect(source, transmissionInput), Is.True);
            Assert.That(
                network.TryConnect(transmissionOutput, consumer),
                Is.True);

            Assert.That(network.LinkCount, Is.EqualTo(2));
            Assert.That(
                network.AreDirectlyConnected(sourcePart, transmissionPart),
                Is.True);
            Assert.That(
                network.AreDirectlyConnected(sourcePart, consumerPart),
                Is.False);
            Assert.That(network.HasPath(sourcePart, consumerPart), Is.True);
            network.SetLinkActivity(sourcePart, transmissionPart, 2f);
            Assert.That(network.Links[0].Activity, Is.EqualTo(1f));

            network.Links[0].gameObject.SetActive(false);
            Assert.That(network.HasPath(sourcePart, consumerPart), Is.False);
        }

        [Test]
        public void CouplingMaterialLinkStopsConductingWhenRopeBreaks()
        {
            var material = CreateMaterial(Color.yellow);
            var firstPart = CreatePart(CaravanPartKind.CouplingRope, 24f);
            var secondPart = CreatePart(CaravanPartKind.CouplingRope, 24f);
            var firstRope =
                firstPart.gameObject.AddComponent<CaravanCouplingRopeModule>();
            var secondRope =
                secondPart.gameObject.AddComponent<CaravanCouplingRopeModule>();
            firstRope.Configure(10f, 5000f);
            secondRope.Configure(10f, 5000f);
            var firstPort = CreateMaterialPort(
                firstPart,
                CaravanMaterialPortRole.CouplingEndpoint);
            var secondPort = CreateMaterialPort(
                secondPart,
                CaravanMaterialPortRole.CouplingEndpoint);
            var networkObject = CreateObject("Coupling Network");
            var network = networkObject.AddComponent<CaravanMaterialNetwork>();
            network.Configure(
                CaravanMaterialNetworkKind.Mechanical,
                material);
            network.RegisterPort(firstPort);
            network.RegisterPort(secondPort);

            Assert.That(network.TryConnect(firstPort, secondPort), Is.True);
            Assert.That(firstRope.LinkedRope, Is.SameAs(secondRope));
            Assert.That(network.HasPath(firstPart, secondPart), Is.True);

            firstRope.EvaluateDistance(20f);
            Assert.That(firstRope.IsBroken, Is.True);
            Assert.That(network.Links[0].IsConductive, Is.False);
            Assert.That(network.HasPath(firstPart, secondPart), Is.False);
        }

        private Material CreateMaterial(Color color)
        {
            var shader = Shader.Find("Hidden/Internal-Colored");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader)
            {
                color = color
            };
            cleanup.Add(material);
            return material;
        }

        private GameObject CreateObject(string name)
        {
            var gameObject = new GameObject(name);
            cleanup.Add(gameObject);
            return gameObject;
        }

        private CaravanPart CreatePart(
            CaravanPartKind kind,
            float capacity,
            float storedAmount = 0f,
            float temperatureCelsius = 18f)
        {
            var gameObject = CreateObject($"{kind} Network Test Module");
            var definition = CaravanPartCatalog.Get(kind);
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

        private CaravanElectricalPort CreateElectricalPort(
            CaravanPart part,
            CaravanElectricalPortKind kind,
            int capacity = 1)
        {
            var portObject = new GameObject($"{kind} Port");
            portObject.transform.SetParent(part.transform, false);
            var port = portObject.AddComponent<CaravanElectricalPort>();
            port.Configure(part, kind, connectionCapacity: capacity);
            return port;
        }

        private CaravanFluidPort CreateFluidPort(
            CaravanPart part,
            CaravanFluidPortRole role)
        {
            var portObject = new GameObject($"{role} Port");
            portObject.transform.SetParent(part.transform, false);
            var port = portObject.AddComponent<CaravanFluidPort>();
            port.Configure(part, role);
            return port;
        }

        private CaravanMaterialPort CreateMaterialPort(
            CaravanPart part,
            CaravanMaterialPortRole role)
        {
            var portObject = new GameObject($"{role} Port");
            portObject.transform.SetParent(part.transform, false);
            var port = portObject.AddComponent<CaravanMaterialPort>();
            port.Configure(part, role);
            return port;
        }
    }
}
