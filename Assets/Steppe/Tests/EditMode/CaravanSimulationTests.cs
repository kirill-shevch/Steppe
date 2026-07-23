using NUnit.Framework;
using Steppe.Caravan;
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
    }
}
