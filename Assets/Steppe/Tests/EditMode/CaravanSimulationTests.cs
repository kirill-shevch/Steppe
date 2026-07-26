using NUnit.Framework;
using Steppe.Caravan;
using Steppe.Time;
using Steppe.Weather;
using UnityEngine;

namespace Steppe.Tests
{
    public sealed class CaravanSimulationTests
    {
        [Test]
        public void BroadsideSailReceivesWindAndEdgeOnSailDoesNot()
        {
            var broadside = CaravanSailAerodynamics.Evaluate(
                Vector3.forward * 8f,
                Vector3.zero,
                Vector3.forward,
                12f,
                1f,
                10000f);
            var edgeOn = CaravanSailAerodynamics.Evaluate(
                Vector3.forward * 8f,
                Vector3.zero,
                Vector3.right,
                12f,
                1f,
                10000f);

            Assert.That(broadside.Force.z, Is.GreaterThan(100f));
            Assert.That(broadside.Effectiveness, Is.GreaterThan(0.99f));
            Assert.That(edgeOn.Force.magnitude, Is.LessThan(0.001f));
        }

        [Test]
        public void VehicleVelocityReducesApparentWindAndSailForce()
        {
            var stationary = CaravanSailAerodynamics.Evaluate(
                Vector3.forward * 9f,
                Vector3.zero,
                Vector3.forward,
                10f,
                1f,
                10000f);
            var moving = CaravanSailAerodynamics.Evaluate(
                Vector3.forward * 9f,
                Vector3.forward * 6f,
                Vector3.forward,
                10f,
                1f,
                10000f);

            Assert.That(moving.ApparentWind.magnitude, Is.LessThan(stationary.ApparentWind.magnitude));
            Assert.That(moving.Force.magnitude, Is.LessThan(stationary.Force.magnitude));
        }

        [Test]
        public void DemoSailProducesReadableStartingForce()
        {
            var sailNormal = Quaternion.Euler(0f, 28f, 0f) * Vector3.forward;
            var sample = CaravanSailAerodynamics.Evaluate(
                Vector3.back * 6f,
                Vector3.zero,
                sailNormal,
                12f,
                0.7f,
                9500f);

            Assert.That(sample.Force.magnitude, Is.GreaterThan(160f));
        }

        [Test]
        public void DirtyDamagedModuleDegradesGraduallyAndCanBeMaintained()
        {
            var state = new CaravanModuleState();
            state.SetForTests(0.8f, 0.2f, 0.9f);
            var degradedEfficiency = state.Efficiency;

            Assert.That(degradedEfficiency, Is.GreaterThan(0.05f));
            Assert.That(degradedEfficiency, Is.LessThan(0.6f));
            state.Clean(0.5f);
            state.Repair(0.5f);
            Assert.That(state.Dust, Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(state.Integrity, Is.EqualTo(0.7f).Within(0.0001f));
            Assert.That(state.Efficiency, Is.GreaterThan(degradedEfficiency));
        }

        [Test]
        public void MountGridRejectsOverlapAndHonoursRotatedFootprints()
        {
            var grid = new CaravanMountGridModel(4, 8);
            var first = new object();
            var second = new object();
            var third = new object();

            Assert.That(
                grid.TryPlace(first, new CaravanGridPlacement(0, 0, 2, 3, 0)),
                Is.True);
            Assert.That(
                grid.CanPlace(second, new CaravanGridPlacement(1, 2, 2, 2, 0)),
                Is.False);
            Assert.That(
                grid.TryPlace(second, new CaravanGridPlacement(2, 0, 2, 2, 0)),
                Is.True);
            Assert.That(
                grid.TryPlace(third, new CaravanGridPlacement(0, 3, 2, 3, 1)),
                Is.True);
            Assert.That(
                grid.TryPlace(new object(), new CaravanGridPlacement(3, 7, 2, 2, 0)),
                Is.False);
        }

        [Test]
        public void PhotovoltaicExposureUsesPanelAngleAndStormShadow()
        {
            var solar = new SolarState(
                new Vector3(0.6f, 0.8f, 0f).normalized,
                53.13,
                0.0,
                1.0);
            var clearWeather = new SteppeWeatherSample(
                Vector2.zero,
                Vector2.zero,
                0.0,
                0.0,
                0.22,
                0.0,
                0.0);
            var stormWeather = new SteppeWeatherSample(
                Vector2.zero,
                Vector2.zero,
                1.0,
                0.0,
                0.96,
                0.94,
                0.82);

            var facingSun = SteppeSolarExposureModel.Evaluate(
                solar,
                clearWeather,
                solar.Direction);
            var horizontal = SteppeSolarExposureModel.Evaluate(
                solar,
                clearWeather,
                Vector3.up);
            var edgeOn = SteppeSolarExposureModel.Evaluate(
                solar,
                clearWeather,
                Vector3.forward);
            var underStorm = SteppeSolarExposureModel.Evaluate(
                solar,
                stormWeather,
                solar.Direction);

            Assert.That(facingSun.Incidence, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(horizontal.Incidence, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(edgeOn.AvailableIrradiance, Is.LessThan(0.0001f));
            Assert.That(facingSun.CloudTransmission, Is.GreaterThan(0.95f));
            Assert.That(underStorm.CloudTransmission, Is.LessThan(0.1f));
            Assert.That(
                underStorm.AvailableIrradiance,
                Is.LessThan(facingSun.AvailableIrradiance * 0.12f));
        }

        [Test]
        public void SolarSurplusChargesBatteryWithinChargeLimit()
        {
            var flow = CaravanElectricalModel.Evaluate(
                18f,
                0f,
                10f,
                120f,
                12f,
                65f,
                0.94f,
                0.93f,
                1f);

            Assert.That(flow.BatteryChargeKilowatts, Is.EqualTo(12f).Within(0.0001f));
            Assert.That(flow.SpilledKilowatts, Is.EqualTo(6f).Within(0.0001f));
            Assert.That(flow.StoredKilowattHours, Is.EqualTo(21.28f).Within(0.0001f));
            Assert.That(flow.DeliveredKilowatts, Is.Zero);
        }

        [Test]
        public void ElectricMotorDrawsSolarFirstAndBatteryCoversDeficit()
        {
            var flow = CaravanElectricalModel.Evaluate(
                18f,
                55f,
                40f,
                120f,
                36f,
                65f,
                0.94f,
                0.93f,
                0.25f);

            Assert.That(flow.DeliveredKilowatts, Is.EqualTo(55f).Within(0.0001f));
            Assert.That(flow.BatteryDischargeKilowatts, Is.EqualTo(37f).Within(0.0001f));
            Assert.That(flow.DeficitKilowatts, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(
                flow.StoredKilowattHours,
                Is.EqualTo(40f - 37f / 0.93f * 0.25f).Within(0.0001f));
        }

        [Test]
        public void DisconnectedMotorReceivesNoPowerWhileBatteryCanStillCharge()
        {
            var flow = CaravanElectricalModel.Evaluate(
                18f,
                55f,
                10f,
                120f,
                36f,
                65f,
                0.94f,
                0.93f,
                0.5f,
                true,
                false);

            Assert.That(flow.DeliveredKilowatts, Is.Zero);
            Assert.That(flow.DeficitKilowatts, Is.EqualTo(55f).Within(0.0001f));
            Assert.That(flow.BatteryChargeKilowatts, Is.EqualTo(18f).Within(0.0001f));
            Assert.That(flow.StoredKilowattHours, Is.EqualTo(18.46f).Within(0.0001f));
        }

        [Test]
        public void LowSunProjectsCloudShadowAwayFromReceiver()
        {
            var solar = new SolarState(
                new Vector3(0.8f, 0.2f, 0f).normalized,
                14.0,
                0.0,
                1.0);
            var projected = SteppeSolarExposureModel.ProjectReceiverToCloud(
                Vector2.zero,
                0f,
                solar,
                1800f,
                9000f);

            Assert.That(projected.x, Is.GreaterThan(6000f));
            Assert.That(projected.y, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void WindVaneArrowPointsIntoSurfaceWind()
        {
            var arrow = CaravanWindVane.GetArrowDirection(new Vector2(3f, 4f));

            Assert.That(arrow.x, Is.EqualTo(-0.6f).Within(0.0001f));
            Assert.That(arrow.z, Is.EqualTo(-0.8f).Within(0.0001f));
            Assert.That(
                CaravanWindVane.GetArrowDirection(Vector2.zero),
                Is.EqualTo(Vector3.zero));
        }
    }
}
