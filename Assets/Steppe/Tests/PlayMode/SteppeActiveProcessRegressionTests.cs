using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Steppe.Ecology;
using Steppe.Player;
using Steppe.Prototype;
using Steppe.Rendering;
using Steppe.Simulation;
using Steppe.Terrain;
using Steppe.Time;
using Steppe.UnitySimulation;
using Steppe.Weather;
using Steppe.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Steppe.Tests
{
    /// <summary>
    /// Natural seasonal process capture. Time is sought through the production clock,
    /// so the finite simulation executes every intermediate macro step. State is never
    /// authored and the observer reaches selected active cells exclusively via WASD.
    /// </summary>
    public sealed class SteppeActiveProcessRegressionTests : InputTestFixture
    {
        private const int CaptureWidth = 1280;
        private const int CaptureHeight = 720;
        private const double SecondsPerDay = 86400d;
        private const double PositionToleranceMeters = 64d;

        [UnityTest, Explicit, Category("VisualRegression"), Timeout(1800000)]
        public IEnumerator CapturesNaturallyEvolvedHydrologicalAndCloudProcesses()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            if (UnityEngine.Object.FindAnyObjectByType<SteppePrototypeBootstrap>() == null)
            {
                new GameObject("Active Process Regression Bootstrap")
                    .AddComponent<SteppePrototypeBootstrap>();
            }

            var host = UnityEngine.Object.FindAnyObjectByType<SteppeSimulationHost>();
            var time = UnityEngine.Object.FindAnyObjectByType<SteppeTimeSystem>();
            var visualization = UnityEngine.Object.FindAnyObjectByType<SteppeSimulationVisualization>();
            var streamer = UnityEngine.Object.FindAnyObjectByType<TerrainChunkStreamer>();
            var grass = UnityEngine.Object.FindAnyObjectByType<SteppeGrassRenderer>();
            var groundDetails = UnityEngine.Object.FindAnyObjectByType<SteppeGroundDetailRenderer>();
            var hydrologicalVapor =
                UnityEngine.Object.FindAnyObjectByType<SteppeHydrologicalVaporPresentation>();
            var atmosphericTransport =
                UnityEngine.Object.FindAnyObjectByType<SteppeAtmosphericTransportPresentation>();
            var ecology = UnityEngine.Object.FindAnyObjectByType<SteppeEcologySystem>();
            var weather = UnityEngine.Object.FindAnyObjectByType<SteppeWeatherSystem>();
            var floatingOrigin = UnityEngine.Object.FindAnyObjectByType<FloatingOriginSystem>();
            var observer = UnityEngine.Object.FindAnyObjectByType<FlyCameraController>();
            var camera = Camera.main;
            var readyDeadline = UnityEngine.Time.realtimeSinceStartup + 240f;
            while ((host == null || !host.IsReady || streamer == null || streamer.LoadedCount == 0)
                   && string.IsNullOrEmpty(host?.LastError)
                   && UnityEngine.Time.realtimeSinceStartup < readyDeadline)
            {
                yield return null;
                host = UnityEngine.Object.FindAnyObjectByType<SteppeSimulationHost>();
                streamer = UnityEngine.Object.FindAnyObjectByType<TerrainChunkStreamer>();
            }

            Assert.That(host, Is.Not.Null);
            Assert.That(host.IsReady, Is.True);
            Assert.That(host.LastError, Is.Null.Or.Empty);
            Assert.That(time, Is.Not.Null);
            Assert.That(visualization, Is.Not.Null);
            Assert.That(streamer, Is.Not.Null);
            Assert.That(grass, Is.Not.Null);
            Assert.That(groundDetails, Is.Not.Null);
            Assert.That(hydrologicalVapor, Is.Not.Null);
            Assert.That(atmosphericTransport, Is.Not.Null);
            Assert.That(ecology, Is.Not.Null);
            Assert.That(weather, Is.Not.Null);
            Assert.That(floatingOrigin, Is.Not.Null);
            Assert.That(observer, Is.Not.Null);
            Assert.That(camera, Is.Not.Null);
            visualization.SetVisible(false);
            Screen.SetResolution(CaptureWidth, CaptureHeight, false);
            if (!time.IsPaused)
            {
                yield return Tap(keyboard.f5Key);
            }

            Assert.That(time.IsPaused, Is.True);
            yield return WaitForWorldToSettle(
                streamer,
                grass,
                groundDetails,
                ecology,
                weather,
                "initial active-process world");
            yield return MoveToViewingHeight(keyboard, observer, "initial active-process height");

            var outputDirectory = CreateOutputDirectory();
            var manifest = new StringBuilder();
            manifest.AppendLine("STEPPE NATURALLY ACTIVE PROCESS REGRESSION");
            manifest.AppendLine($"UTC: {DateTime.UtcNow:O}");
            manifest.AppendLine(
                "Production bootstrap and every intermediate macro step are used; "
                + "the observer travels only through WASD and frames with normal mouse look. "
                + "No state is forced and no teleport occurs.");
            manifest.AppendLine();

            var unresolved = new HashSet<SimulationFlux>
            {
                SimulationFlux.SurfaceEvaporation,
                SimulationFlux.Percolation,
                SimulationFlux.CloudEvaporation,
            };
            var captured = new List<SimulationFlux>();
            var currentDays = time.ElapsedSimulationSeconds / SecondsPerDay;
            var targetStartDays = 75.5d;
            if (currentDays < targetStartDays)
            {
                time.AdvanceSimulationSeconds((targetStartDays - currentDays) * SecondsPerDay);
                yield return WaitForMacroCatchup(host, time, "spring approach");
            }

            const int finalSearchDay = 180;
            while (unresolved.Count > 0 && time.Current.DayOfYear <= finalSearchDay)
            {
                var foundThisBoundary = new List<ActiveFluxTarget>();
                foreach (var flux in unresolved)
                {
                    if (TryFindClosestActiveFlux(
                            host.LatestSnapshot,
                            flux,
                            floatingOrigin.LocalToWorld(observer.transform.position),
                            out var target))
                    {
                        foundThisBoundary.Add(target);
                    }
                }

                if (foundThisBoundary.Count > 0)
                {
                    // Targets come from LatestSnapshot. Move presentation close to
                    // that already-computed boundary while staying inside the same
                    // macro interval, so the game's normal interpolation actually
                    // displays the selected signal instead of the previous snapshot.
                    var secondsUntilLatest = host.LatestMacroSimulationSeconds
                                             - time.ElapsedSimulationSeconds;
                    var interpolationMarginSeconds = Math.Min(
                        host.FixedStepHours * 3600d * 0.02d,
                        180d);
                    if (secondsUntilLatest > interpolationMarginSeconds)
                    {
                        time.AdvanceSimulationSeconds(
                            secondsUntilLatest - interpolationMarginSeconds);
                        yield return null;
                    }
                }

                for (var index = 0; index < foundThisBoundary.Count; index++)
                {
                    var target = foundThisBoundary[index];
                    // Long transfers remain horizontal, then the same player mouse-look
                    // control frames the physical carrier: water surface, exposed soil
                    // profile, or cloud edge. Camera transforms are never authored here.
                    yield return AimPitchThroughMouse(
                        mouse,
                        observer,
                        0f,
                        $"{target.Flux} travel framing");
                    yield return DriveToWorldPosition(
                        keyboard,
                        observer,
                        floatingOrigin,
                        target.WorldX,
                        target.WorldZ,
                        target.Flux.ToString());
                    yield return WaitForWorldToSettle(
                        streamer,
                        grass,
                        groundDetails,
                        ecology,
                        weather,
                        target.Flux.ToString());
                    yield return MoveToViewingHeight(
                        keyboard,
                        observer,
                        target.Flux.ToString(),
                        ViewingHeightFor(target.Flux));
                    yield return AimPitchThroughMouse(
                        mouse,
                        observer,
                        ViewingPitchFor(target.Flux),
                        $"{target.Flux} capture framing");

                    // Allow world-space particles to repopulate around the new focus.
                    for (var warmupFrame = 0; warmupFrame < 90; warmupFrame++)
                    {
                        yield return null;
                    }

                    var visibleParticleCount = target.Flux switch
                    {
                        SimulationFlux.SurfaceEvaporation =>
                            hydrologicalVapor.EvaporationParticles.particleCount,
                        SimulationFlux.Percolation =>
                            hydrologicalVapor.PercolationParticles.particleCount,
                        SimulationFlux.CloudEvaporation =>
                            atmosphericTransport.CloudEvaporationParticles.particleCount,
                        _ => 0,
                    };
                    Assert.That(
                        visibleParticleCount,
                        Is.GreaterThan(0),
                        $"{target.Flux} physical carrier must be populated at capture.");

                    var prefix = target.Flux.ToString();
                    var firstFile = $"{prefix}-a.png";
                    var secondFile = $"{prefix}-b.png";
                    yield return CapturePng(camera, Path.Combine(outputDirectory, firstFile));
                    for (var temporalFrame = 0; temporalFrame < 45; temporalFrame++)
                    {
                        yield return null;
                    }
                    yield return CapturePng(camera, Path.Combine(outputDirectory, secondFile));

                    var arrived = floatingOrigin.LocalToWorld(observer.transform.position);
                    host.TrySampleFluxRate(
                        target.Flux,
                        arrived.X,
                        arrived.Z,
                        out var arrivedRate);
                    Assert.That(
                        arrivedRate,
                        Is.GreaterThan(0f),
                        $"{target.Flux} must be active in presentation time at capture.");
                    manifest.AppendLine(
                        $"{prefix}\tday={time.Current.DayOfYear}\thour={F(time.Current.Hour)}\t"
                        + $"world=({F(arrived.X)},{F(arrived.Z)})\t"
                        + $"selectedRate={F(target.RatePerHour)}\tarrivedRate={F(arrivedRate)}\t"
                        + $"selectedCarrier={F(target.CarrierValue)}\t"
                        + $"visibleParticles={visibleParticleCount}\t"
                        + $"pair={firstFile},{secondFile}");
                    unresolved.Remove(target.Flux);
                    captured.Add(target.Flux);
                }

                if (unresolved.Count == 0)
                {
                    break;
                }

                // Daily noon samples cover solar evaporation/cloud erosion while
                // preserving every intermediate three-hour macro transition.
                time.AdvanceSimulationSeconds(SecondsPerDay);
                yield return WaitForMacroCatchup(
                    host,
                    time,
                    $"seasonal day {time.Current.DayOfYear}");
            }

            File.WriteAllText(Path.Combine(outputDirectory, "manifest.txt"), manifest.ToString());
            File.WriteAllText(
                Path.Combine(Path.GetDirectoryName(outputDirectory), "latest-run.txt"),
                outputDirectory);
            Assert.That(
                captured,
                Does.Contain(SimulationFlux.SurfaceEvaporation),
                "No naturally active surface evaporation was found by midsummer.");
            Assert.That(
                captured,
                Does.Contain(SimulationFlux.Percolation),
                "No naturally active percolation was found by midsummer.");
            Assert.That(
                captured,
                Does.Contain(SimulationFlux.CloudEvaporation),
                "No naturally active cloud evaporation was found by midsummer.");
            Assert.That(
                Directory.GetFiles(outputDirectory, "*.png").Length,
                Is.EqualTo(captured.Count * 2));
        }

        private static bool TryFindClosestActiveFlux(
            SteppeSimulationSnapshot snapshot,
            SimulationFlux flux,
            WorldPosition observer,
            out ActiveFluxTarget target)
        {
            target = default;
            if (snapshot == null
                || !snapshot.TryGetFlux(flux, out var fluxSnapshot)
                || fluxSnapshot.PeriodHours <= 0d)
            {
                return false;
            }

            LayerSnapshot carrierState = null;
            if (flux == SimulationFlux.CloudEvaporation)
            {
                snapshot.TryGetLayer(SimulationLayer.CloudWater, out carrierState);
            }

            var found = false;
            var bestScore = double.NegativeInfinity;
            for (var index = 0; index < fluxSnapshot.Values.Length; index++)
            {
                var rate = Math.Max(0d, fluxSnapshot.Values[index] / fluxSnapshot.PeriodHours);
                if (rate <= 0.000001d)
                {
                    continue;
                }

                var carrierValue = carrierState != null
                                   && index < carrierState.Values.Length
                    ? Math.Max(0d, carrierState.Values[index])
                    : 0d;
                if (flux == SimulationFlux.CloudEvaporation && carrierValue <= 0.0001d)
                {
                    continue;
                }

                var cellX = index % fluxSnapshot.Width;
                var cellY = index / fluxSnapshot.Width;
                snapshot.Coordinates.CellCenterToWorld(
                    cellX,
                    cellY,
                    out var worldX,
                    out var worldZ);
                var dx = worldX - observer.X;
                var dz = worldZ - observer.Z;
                var distance = Math.Sqrt(dx * dx + dz * dz);
                var carrierScore = flux == SimulationFlux.CloudEvaporation
                    ? Math.Log10(1d + carrierValue * 20d) * 0.55d
                    : 0d;
                var score = Math.Log10(1d + rate * 100000d)
                            + carrierScore
                            - distance / 12000d;
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                target = new ActiveFluxTarget(
                    flux,
                    worldX,
                    worldZ,
                    (float)rate,
                    (float)carrierValue);
                found = true;
            }

            return found;
        }

        private static IEnumerator WaitForMacroCatchup(
            SteppeSimulationHost host,
            SteppeTimeSystem time,
            string label)
        {
            var presentationHours = time.ElapsedSimulationSeconds / 3600d;
            var requiredMacroHours =
                (Math.Floor(presentationHours / host.FixedStepHours + 1e-9d) + 1d)
                * host.FixedStepHours;
            var deadline = UnityEngine.Time.realtimeSinceStartup + 1200f;
            while (host.LatestMacroSimulationSeconds + 0.001d < requiredMacroHours * 3600d
                   && string.IsNullOrEmpty(host.LastError)
                   && UnityEngine.Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(host.LastError, Is.Null.Or.Empty, label);
            Assert.That(
                host.LatestMacroSimulationSeconds,
                Is.GreaterThanOrEqualTo(requiredMacroHours * 3600d - 0.001d),
                label);
        }

        private static IEnumerator DriveToWorldPosition(
            Keyboard keyboard,
            FlyCameraController observer,
            FloatingOriginSystem floatingOrigin,
            double targetWorldX,
            double targetWorldZ,
            string label)
        {
            var deadline = UnityEngine.Time.realtimeSinceStartup + 150f;
            while (UnityEngine.Time.realtimeSinceStartup < deadline)
            {
                var current = floatingOrigin.LocalToWorld(observer.transform.position);
                var error = new Vector2(
                    (float)(targetWorldX - current.X),
                    (float)(targetWorldZ - current.Z));
                if (error.magnitude <= 18f)
                {
                    break;
                }

                var forward = new Vector2(
                    observer.transform.forward.x,
                    observer.transform.forward.z).normalized;
                var right = new Vector2(
                    observer.transform.right.x,
                    observer.transform.right.z).normalized;
                var forwardError = Vector2.Dot(error, forward);
                var rightError = Vector2.Dot(error, right);
                var keys = new List<Key>(3);
                if (Mathf.Abs(forwardError) > 7f)
                {
                    keys.Add(forwardError >= 0f ? Key.W : Key.S);
                }
                if (Mathf.Abs(rightError) > 7f)
                {
                    keys.Add(rightError >= 0f ? Key.D : Key.A);
                }
                if (error.magnitude > 90f)
                {
                    keys.Add(Key.LeftShift);
                }

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys.ToArray()));
                InputSystem.Update();
                yield return null;
            }

            for (var releaseFrame = 0; releaseFrame < 3; releaseFrame++)
            {
                InputSystem.ResetDevice(keyboard, alsoResetDontResetControls: true);
                yield return null;
            }

            var arrived = floatingOrigin.LocalToWorld(observer.transform.position);
            var dx = arrived.X - targetWorldX;
            var dz = arrived.Z - targetWorldZ;
            Assert.That(
                Math.Sqrt(dx * dx + dz * dz),
                Is.LessThanOrEqualTo(PositionToleranceMeters),
                label);
        }

        private static IEnumerator MoveToViewingHeight(
            Keyboard keyboard,
            FlyCameraController observer,
            string label,
            float targetHeight = 5f)
        {
            var deadline = UnityEngine.Time.realtimeSinceStartup + 60f;
            while (UnityEngine.Time.realtimeSinceStartup < deadline)
            {
                Assert.That(TryGetGroundHeight(observer.transform.position, out var groundHeight),
                    Is.True, label);
                var error = targetHeight - (observer.transform.position.y - groundHeight);
                if (Mathf.Abs(error) <= 1.25f)
                {
                    break;
                }

                var verticalKey = error > 0f ? Key.E : Key.Q;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(verticalKey));
                InputSystem.Update();
                yield return null;
            }

            for (var releaseFrame = 0; releaseFrame < 3; releaseFrame++)
            {
                InputSystem.ResetDevice(keyboard, alsoResetDontResetControls: true);
                yield return null;
            }

            Assert.That(TryGetGroundHeight(observer.transform.position, out var finalGroundHeight),
                Is.True, label);
            Assert.That(
                observer.transform.position.y - finalGroundHeight,
                Is.InRange(0.5f, 12f),
                label);
        }

        private static IEnumerator AimPitchThroughMouse(
            Mouse mouse,
            FlyCameraController observer,
            float targetPitch,
            string label)
        {
            Assert.That(mouse, Is.Not.Null, label);
            var deadline = UnityEngine.Time.realtimeSinceStartup + 12f;
            while (UnityEngine.Time.realtimeSinceStartup < deadline)
            {
                var currentPitch = NormalizePitch(observer.transform.eulerAngles.x);
                var error = targetPitch - currentPitch;
                if (Mathf.Abs(error) <= 1.25f)
                {
                    yield break;
                }

                // FlyCameraController applies pitch -= mouseDeltaY * sensitivity.
                // Queueing the delta (without touching the Transform) lets the normal
                // InputSystem/player Update consume exactly the same look path as play.
                var deltaY = Mathf.Clamp(-error / 0.08f, -72f, 72f);
                InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(0f, deltaY));
                yield return null;
            }

            Assert.That(
                NormalizePitch(observer.transform.eulerAngles.x),
                Is.EqualTo(targetPitch).Within(2f),
                label);
        }

        private static float ViewingHeightFor(SimulationFlux flux) => flux switch
        {
            SimulationFlux.SurfaceEvaporation => 3.5f,
            SimulationFlux.Percolation => 4.5f,
            SimulationFlux.CloudEvaporation => 6f,
            _ => 5f,
        };

        private static float ViewingPitchFor(SimulationFlux flux) => flux switch
        {
            SimulationFlux.SurfaceEvaporation => 8f,
            SimulationFlux.Percolation => 20f,
            // Looking almost vertically upward keeps the cloud raymarch footprint
            // inside the active simulation cell selected below the observer.
            SimulationFlux.CloudEvaporation => -78f,
            _ => 0f,
        };

        private static float NormalizePitch(float pitch) =>
            pitch > 180f ? pitch - 360f : pitch;

        private static bool TryGetGroundHeight(Vector3 localPosition, out float groundHeight)
        {
            // Long W/S flights follow the camera pitch and can temporarily place the
            // observer far above or below the surface. Query from a stable world-space
            // altitude; camera correction itself still happens only through Q/E input.
            var rayOrigin = new Vector3(localPosition.x, 5000f, localPosition.z);
            if (Physics.Raycast(
                    rayOrigin,
                    Vector3.down,
                    out var hit,
                    10000f,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
            {
                groundHeight = hit.point.y;
                return true;
            }

            groundHeight = 0f;
            return false;
        }

        private static IEnumerator WaitForWorldToSettle(
            TerrainChunkStreamer streamer,
            SteppeGrassRenderer grass,
            SteppeGroundDetailRenderer groundDetails,
            SteppeEcologySystem ecology,
            SteppeWeatherSystem weather,
            string label)
        {
            var deadline = UnityEngine.Time.realtimeSinceStartup + 300f;
            var stableFrames = 0;
            while (UnityEngine.Time.realtimeSinceStartup < deadline)
            {
                var ready = streamer.PendingCount == 0
                            && grass.PendingCount == 0
                            && grass.LoadedCellCount > 0
                            && grass.InstanceCount > 0
                            && groundDetails.PendingCount == 0
                            && groundDetails.LoadedCellCount > 0
                            && groundDetails.CandidateCount > 0
                            && ecology.PendingCellCount == 0
                            && !weather.HasPendingWorldWork;
                stableFrames = ready ? stableFrames + 1 : 0;
                if (stableFrames >= 3)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail($"World did not settle for {label}.");
        }

        private IEnumerator Tap(ButtonControl button)
        {
            Press(button);
            yield return null;
            Release(button);
            yield return null;
        }

        private static IEnumerator CapturePng(Camera camera, string path)
        {
            var target = RenderTexture.GetTemporary(
                CaptureWidth,
                CaptureHeight,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false, false);
            image.ReadPixels(new Rect(0f, 0f, CaptureWidth, CaptureHeight), 0, 0, false);
            image.Apply(false, false);
            File.WriteAllBytes(path, image.EncodeToPNG());
            UnityEngine.Object.Destroy(image);
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);
            yield return null;
        }

        private static string CreateOutputDirectory()
        {
            var root = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "Artifacts",
                "SteppeActiveProcesses"));
            var path = Path.Combine(root, $"run-{DateTime.UtcNow:yyyyMMdd-HHmmss}");
            Directory.CreateDirectory(path);
            return path;
        }

        private static string F(double value) =>
            value.ToString("0.######", CultureInfo.InvariantCulture);

        private readonly struct ActiveFluxTarget
        {
            public ActiveFluxTarget(
                SimulationFlux flux,
                double worldX,
                double worldZ,
                float ratePerHour,
                float carrierValue)
            {
                Flux = flux;
                WorldX = worldX;
                WorldZ = worldZ;
                RatePerHour = ratePerHour;
                CarrierValue = carrierValue;
            }

            public SimulationFlux Flux { get; }
            public double WorldX { get; }
            public double WorldZ { get; }
            public float RatePerHour { get; }
            public float CarrierValue { get; }
        }
    }
}
