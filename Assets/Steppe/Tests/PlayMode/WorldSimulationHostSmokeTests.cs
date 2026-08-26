using System.Collections;
using NUnit.Framework;
using Steppe.Simulation;
using Steppe.UnitySimulation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Steppe.Tests
{
    public sealed class WorldSimulationHostSmokeTests
    {
        [UnityTest]
        public IEnumerator Host_PublishesAndAdvancesFiniteWorldSnapshot()
        {
            var hostObject = new GameObject("Finite World Host Test");
            var host = hostObject.AddComponent<SteppeSimulationHost>();
            host.Configure(123, 0f, width: 8, height: 8, baseStepMinutes: 60);

            var deadline = UnityEngine.Time.realtimeSinceStartup + 10f;
            while (!host.IsReady
                && string.IsNullOrEmpty(host.LastError)
                && UnityEngine.Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(host.LastError, Is.Null.Or.Empty);
            Assert.That(host.LatestSnapshot, Is.Not.Null);
            Assert.That(host.LatestSnapshot.Summary.Width, Is.EqualTo(8));
            Assert.That(host.TryGetTimeSample(out var initialTime), Is.True);
            Assert.That(initialTime.Year, Is.EqualTo(1));
            Assert.That(initialTime.DayOfYear, Is.EqualTo(1));
            Assert.That(initialTime.HourOfDay, Is.EqualTo(0d).Within(1e-9d));
            host.SetTimeControl(true, 10f);
            Assert.That(host.IsTimePaused, Is.True);
            Assert.That(host.TimeMultiplier, Is.EqualTo(10f));
            host.SetTimeControl(false, 1f);
            Assert.That(host.TryGetEnvironmentSample(0d, 0d, out var environment), Is.True);
            Assert.That(environment.CellX, Is.InRange(0, 7));
            Assert.That(environment.CellY, Is.InRange(0, 7));
            Assert.That(float.IsNaN(environment.AirTemperatureC), Is.False);
            Assert.That(
                host.TrySampleState(
                    SimulationLayer.Pressure,
                    0d,
                    0d,
                    out var pressure),
                Is.True);
            Assert.That(pressure, Is.InRange(850f, 1100f));

            var biomassBefore = environment.LiveBiomassGramsPerSquareMeter
                                + environment.DryBiomassGramsPerSquareMeter;
            var extractionRequest = host.QueueResourceExtraction(
                0d,
                0d,
                0f,
                0f,
                0.5f,
                0.5f);
            SteppeSimulationResourceExtractionResult extraction = null;
            while (!host.TryTakeResourceExtractionResult(extractionRequest, out extraction)
                && string.IsNullOrEmpty(host.LastError)
                && UnityEngine.Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(extraction, Is.Not.Null);
            Assert.That(extraction.Succeeded, Is.True, extraction.Error);
            Assert.That(
                extraction.LiveBiomassKilograms + extraction.DryBiomassKilograms,
                Is.GreaterThan(0f));
            while (host.TryGetEnvironmentSample(0d, 0d, out environment)
                && environment.LiveBiomassGramsPerSquareMeter
                   + environment.DryBiomassGramsPerSquareMeter >= biomassBefore - 0.0001f
                && UnityEngine.Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(
                environment.LiveBiomassGramsPerSquareMeter
                + environment.DryBiomassGramsPerSquareMeter,
                Is.LessThan(biomassBefore));

            host.RequestAdvanceHours(1d);
            while (host.LatestSnapshot.Summary.ElapsedHours < 1d
                && string.IsNullOrEmpty(host.LastError)
                && UnityEngine.Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(host.LastError, Is.Null.Or.Empty);
            Assert.That(host.LatestSnapshot.Summary.ElapsedHours, Is.EqualTo(1d).Within(1e-9d));
            host.SetPresentationTime(1800d);
            Assert.That(host.TryGetTimeSample(out var advancedTime), Is.True);
            Assert.That(advancedTime.ElapsedSimulationSeconds, Is.EqualTo(1800d).Within(1e-9d));
            Assert.That(advancedTime.HourOfDay, Is.EqualTo(0.5d).Within(1e-9d));
            Assert.That(host.MacroInterpolationAlpha, Is.EqualTo(0.5f).Within(0.001f));
            Object.Destroy(hostObject);
        }
    }
}
