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
    /// Explicit visual-regression journey through the normally bootstrapped world.
    /// The test never selects diagnostic fields or authors artificial weather/ecology
    /// states. It drives the real WASD camera, lets the normal streaming queues settle,
    /// and revisits the same world positions by day and by night.
    /// </summary>
    public sealed class SteppeVisualRegressionTests : InputTestFixture
    {
        private const int CaptureWidth = 1280;
        private const int CaptureHeight = 720;
        private const int WaypointCount = 9;
        private const double ChunkSizeMeters = 512d;
        private const double PositionToleranceMeters = 64d;

        [UnityTest, Explicit, Category("VisualRegression"), Timeout(1800000)]
        public IEnumerator CapturesRunningWorldJourneyByDayAndNight()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            if (UnityEngine.Object.FindAnyObjectByType<SteppePrototypeBootstrap>() == null)
            {
                new GameObject("Traversal Visual Regression Bootstrap")
                    .AddComponent<SteppePrototypeBootstrap>();
            }

            SteppeSimulationHost host = null;
            SteppeTimeSystem time = null;
            SteppeSimulationVisualization visualization = null;
            TerrainChunkStreamer streamer = null;
            SteppeGrassRenderer grass = null;
            SteppeGroundDetailRenderer groundDetails = null;
            SteppeEcologySystem ecology = null;
            SteppeWeatherSystem weather = null;
            FloatingOriginSystem floatingOrigin = null;
            FlyCameraController observer = null;
            Camera camera = null;

            for (var frame = 0; frame < 1800; frame++)
            {
                host = UnityEngine.Object.FindAnyObjectByType<SteppeSimulationHost>();
                time = UnityEngine.Object.FindAnyObjectByType<SteppeTimeSystem>();
                visualization = UnityEngine.Object.FindAnyObjectByType<SteppeSimulationVisualization>();
                streamer = UnityEngine.Object.FindAnyObjectByType<TerrainChunkStreamer>();
                grass = UnityEngine.Object.FindAnyObjectByType<SteppeGrassRenderer>();
                groundDetails = UnityEngine.Object.FindAnyObjectByType<SteppeGroundDetailRenderer>();
                ecology = UnityEngine.Object.FindAnyObjectByType<SteppeEcologySystem>();
                weather = UnityEngine.Object.FindAnyObjectByType<SteppeWeatherSystem>();
                floatingOrigin = UnityEngine.Object.FindAnyObjectByType<FloatingOriginSystem>();
                observer = UnityEngine.Object.FindAnyObjectByType<FlyCameraController>();
                camera = Camera.main;
                if (host != null
                    && host.IsReady
                    && time != null
                    && visualization != null
                    && streamer != null
                    && grass != null
                    && groundDetails != null
                    && ecology != null
                    && weather != null
                    && floatingOrigin != null
                    && observer != null
                    && camera != null)
                {
                    break;
                }

                yield return null;
            }

            Assert.That(host, Is.Not.Null);
            Assert.That(host.IsReady, Is.True);
            Assert.That(host.LastError, Is.Null.Or.Empty);
            Assert.That(time, Is.Not.Null);
            Assert.That(visualization, Is.Not.Null);
            Assert.That(streamer, Is.Not.Null);
            Assert.That(grass, Is.Not.Null);
            Assert.That(grass.IsRendering, Is.True,
                "The traversal must render the production grass path.");
            Assert.That(groundDetails, Is.Not.Null);
            Assert.That(groundDetails.IsRendering, Is.True,
                "The traversal must render the production semantic ground-detail path.");
            Assert.That(ecology, Is.Not.Null);
            Assert.That(weather, Is.Not.Null);
            Assert.That(floatingOrigin, Is.Not.Null);
            Assert.That(observer, Is.Not.Null);
            Assert.That(camera, Is.Not.Null);
            Assert.That(camera.transform, Is.EqualTo(observer.transform));

            visualization.SetVisible(false);
            Screen.SetResolution(CaptureWidth, CaptureHeight, false);
            if (!time.IsPaused)
            {
                yield return Tap(keyboard.f5Key);
            }

            Assert.That(time.IsPaused, Is.True);
            yield return WaitForRunningWorldToSettle(
                streamer, grass, groundDetails, ecology, weather, "initial");
            yield return MoveToViewingHeight(keyboard, observer, "initial");

            var outputDirectory = CreateOutputDirectory();
            var manifest = new StringBuilder();
            var samples = new StringBuilder();
            WriteHeaders(manifest, samples);

            var route = new List<RoutePoint>(WaypointCount);
            var initial = floatingOrigin.LocalToWorld(observer.transform.position);
            var firstBoundaryX = Math.Ceiling(initial.X / ChunkSizeMeters) * ChunkSizeMeters;
            var initialRotation = observer.transform.rotation;

            for (var index = 0; index < WaypointCount; index++)
            {
                if (index > 0)
                {
                    var targetX = firstBoundaryX + (index - 1) * ChunkSizeMeters;
                    yield return DriveToWorldX(
                        keyboard,
                        observer,
                        floatingOrigin,
                        targetX,
                        $"night outbound waypoint {index + 1}");
                    yield return WaitForRunningWorldToSettle(
                        streamer,
                        grass,
                        groundDetails,
                        ecology,
                        weather,
                        $"night waypoint {index + 1}");
                    yield return MoveToViewingHeight(
                        keyboard,
                        observer,
                        $"night waypoint {index + 1}");
                }

                var position = floatingOrigin.LocalToWorld(observer.transform.position);
                var point = new RoutePoint(position, observer.transform.rotation);
                route.Add(point);
                var fileName = $"waypoint-{index + 1:00}-night.png";
                yield return CaptureAndRecord(
                    camera,
                    host,
                    time,
                    streamer,
                    grass,
                    groundDetails,
                    ecology,
                    floatingOrigin,
                    point,
                    index,
                    "night",
                    fileName,
                    outputDirectory,
                    manifest,
                    samples);
            }

            var outboundDistance = Math.Abs(route[route.Count - 1].Position.X - route[0].Position.X);
            Assert.That(outboundDistance, Is.GreaterThan(3500d));
            Assert.That(Math.Abs(floatingOrigin.OriginX), Is.GreaterThan(0d),
                "The route must cross a floating-origin boundary.");
            var finiteShaderOrigin = Shader.GetGlobalVector("_SteppeFiniteWorldOriginXZ");
            Assert.That(finiteShaderOrigin.x, Is.EqualTo((float)floatingOrigin.OriginX).Within(0.01f));
            Assert.That(finiteShaderOrigin.z, Is.EqualTo((float)floatingOrigin.OriginZ).Within(0.01f));
            Assert.That(
                observer.transform.position.x + finiteShaderOrigin.x,
                Is.EqualTo((float)route[route.Count - 1].Position.X).Within(0.05f));
            Assert.That(
                observer.transform.position.z + finiteShaderOrigin.z,
                Is.EqualTo((float)route[route.Count - 1].Position.Z).Within(0.05f));

            yield return AdvanceRunningWorldToDay(keyboard, time, host);
            yield return WaitForRunningWorldToSettle(
                streamer, grass, groundDetails, ecology, weather, "day transition");

            for (var index = route.Count - 1; index >= 0; index--)
            {
                if (index < route.Count - 1)
                {
                    yield return DriveToWorldX(
                        keyboard,
                        observer,
                        floatingOrigin,
                        route[index].Position.X,
                        $"day return waypoint {index + 1}");
                    yield return WaitForRunningWorldToSettle(
                        streamer,
                        grass,
                        groundDetails,
                        ecology,
                        weather,
                        $"day waypoint {index + 1}");
                }

                yield return MoveToViewingHeight(
                    keyboard,
                    observer,
                    $"day waypoint {index + 1}");

                var current = floatingOrigin.LocalToWorld(observer.transform.position);
                Assert.That(
                    HorizontalDistance(current, route[index].Position),
                    Is.LessThanOrEqualTo(PositionToleranceMeters));
                Assert.That(
                    Quaternion.Angle(observer.transform.rotation, route[index].Rotation),
                    Is.LessThan(0.01f));

                var fileName = $"waypoint-{index + 1:00}-day.png";
                yield return CaptureAndRecord(
                    camera,
                    host,
                    time,
                    streamer,
                    grass,
                    groundDetails,
                    ecology,
                    floatingOrigin,
                    route[index],
                    index,
                    "day",
                    fileName,
                    outputDirectory,
                    manifest,
                    samples);
            }

            Assert.That(Quaternion.Angle(observer.transform.rotation, initialRotation), Is.LessThan(0.01f));
            var finalPosition = floatingOrigin.LocalToWorld(observer.transform.position);
            Assert.That(
                HorizontalDistance(finalPosition, route[0].Position),
                Is.LessThanOrEqualTo(PositionToleranceMeters));

            File.WriteAllText(Path.Combine(outputDirectory, "manifest.txt"), manifest.ToString());
            File.WriteAllText(Path.Combine(outputDirectory, "samples.tsv"), samples.ToString());
            File.WriteAllText(
                Path.Combine(Path.GetDirectoryName(outputDirectory), "latest-run.txt"),
                outputDirectory);

            Assert.That(
                Directory.GetFiles(outputDirectory, "*.png").Length,
                Is.EqualTo(WaypointCount * 2));
        }

        private IEnumerator DriveToWorldX(
            Keyboard keyboard,
            FlyCameraController observer,
            FloatingOriginSystem floatingOrigin,
            double targetWorldX,
            string label)
        {
            var deadline = UnityEngine.Time.realtimeSinceStartup + 45f;
            var previousError = double.NaN;

            while (UnityEngine.Time.realtimeSinceStartup < deadline)
            {
                var current = floatingOrigin.LocalToWorld(observer.transform.position);
                var error = targetWorldX - current.X;
                if (Math.Abs(error) <= 1.5d
                    || !double.IsNaN(previousError) && Math.Sign(error) != Math.Sign(previousError))
                {
                    break;
                }

                var shouldBoost = Math.Abs(error) > 45d;
                var direction = error > 0d ? Key.D : Key.A;
                if (shouldBoost)
                {
                    InputSystem.QueueStateEvent(
                        keyboard,
                        new KeyboardState(direction, Key.LeftShift));
                }
                else
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(direction));
                }

                InputSystem.Update();
                previousError = error;
                yield return null;
            }

            for (var releaseFrame = 0; releaseFrame < 3; releaseFrame++)
            {
                InputSystem.ResetDevice(keyboard, alsoResetDontResetControls: true);
                yield return null;
            }

            Assert.That(keyboard.dKey.isPressed, Is.False, label);
            Assert.That(keyboard.aKey.isPressed, Is.False, label);
            Assert.That(keyboard.leftShiftKey.isPressed, Is.False, label);
            var arrived = floatingOrigin.LocalToWorld(observer.transform.position);
            Assert.That(
                Math.Abs(arrived.X - targetWorldX),
                Is.LessThanOrEqualTo(PositionToleranceMeters),
                label);
        }

        private static IEnumerator MoveToViewingHeight(
            Keyboard keyboard,
            FlyCameraController observer,
            string label)
        {
            const float targetHeight = 6f;
            const float tolerance = 1.5f;
            var deadline = UnityEngine.Time.realtimeSinceStartup + 45f;
            var previousError = float.NaN;
            while (UnityEngine.Time.realtimeSinceStartup < deadline)
            {
                Assert.That(TryGetGroundHeight(observer.transform.position, out var groundHeight),
                    Is.True, label);
                var height = observer.transform.position.y - groundHeight;
                var error = targetHeight - height;
                if (Mathf.Abs(error) <= tolerance
                    || !float.IsNaN(previousError) && Mathf.Sign(error) != Mathf.Sign(previousError))
                {
                    break;
                }

                var verticalKey = error > 0f ? Key.E : Key.Q;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(verticalKey));
                InputSystem.Update();
                previousError = error;
                yield return null;
            }

            for (var releaseFrame = 0; releaseFrame < 3; releaseFrame++)
            {
                InputSystem.ResetDevice(keyboard, alsoResetDontResetControls: true);
                yield return null;
            }

            Assert.That(TryGetGroundHeight(observer.transform.position, out var finalGroundHeight),
                Is.True, label);
            var finalHeight = observer.transform.position.y - finalGroundHeight;
            Assert.That(finalHeight, Is.InRange(0.5f, 14f), label);
        }

        private static bool TryGetGroundHeight(Vector3 localPosition, out float groundHeight)
        {
            var rayOrigin = new Vector3(localPosition.x, localPosition.y + 1000f, localPosition.z);
            if (Physics.Raycast(
                    rayOrigin,
                    Vector3.down,
                    out var hit,
                    2000f,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
            {
                groundHeight = hit.point.y;
                return true;
            }

            groundHeight = 0f;
            return false;
        }

        private static IEnumerator WaitForRunningWorldToSettle(
            TerrainChunkStreamer streamer,
            SteppeGrassRenderer grass,
            SteppeGroundDetailRenderer groundDetails,
            SteppeEcologySystem ecology,
            SteppeWeatherSystem weather,
            string label)
        {
            var deadline = UnityEngine.Time.realtimeSinceStartup + 240f;
            var stableFrames = 0;
            var waitedFrames = 0;
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

                waitedFrames++;
                if (waitedFrames % 600 == 0)
                {
                    Debug.Log(
                        $"Traversal settle {label}: terrain={streamer.PendingCount}, "
                        + $"grass={grass.PendingCount}/{grass.LoadedCellCount}/{grass.InstanceCount}, "
                        + $"details={groundDetails.PendingCount}/{groundDetails.LoadedCellCount}/"
                        + $"{groundDetails.CandidateCount}, "
                        + $"ecology={ecology.PendingCellCount}, weather={weather.HasPendingWorldWork}.");
                }

                yield return null;
            }

            Assert.Fail(
                $"World did not settle at {label}: terrain={streamer.PendingCount}, "
                + $"grass={grass.PendingCount}/{grass.LoadedCellCount}/{grass.InstanceCount}, "
                + $"details={groundDetails.PendingCount}/{groundDetails.LoadedCellCount}/"
                + $"{groundDetails.CandidateCount}, "
                + $"ecology={ecology.PendingCellCount}, weather={weather.HasPendingWorldWork}.");
        }

        private IEnumerator AdvanceRunningWorldToDay(
            Keyboard keyboard,
            SteppeTimeSystem time,
            SteppeSimulationHost host)
        {
            Assert.That(time.IsPaused, Is.True);
            yield return Tap(keyboard.f6Key);
            yield return Tap(keyboard.f6Key);
            Assert.That(time.DebugMultiplier, Is.EqualTo(100f));
            yield return Tap(keyboard.f5Key);
            Assert.That(time.IsPaused, Is.False);

            var presentationDeadline = UnityEngine.Time.realtimeSinceStartup + 30f;
            while (time.Current.Hour < 12d
                   && UnityEngine.Time.realtimeSinceStartup < presentationDeadline)
            {
                yield return null;
            }

            yield return Tap(keyboard.f5Key);
            Assert.That(time.IsPaused, Is.True);
            Assert.That(time.Current.Hour, Is.InRange(12d, 13d));

            var presentationHours = time.ElapsedSimulationSeconds / 3600d;
            var requiredMacroHours =
                (Math.Floor(presentationHours / host.FixedStepHours + 1e-9d) + 1d)
                * host.FixedStepHours;
            var requiredMacroSeconds = requiredMacroHours * 3600d;
            var macroDeadline = UnityEngine.Time.realtimeSinceStartup + 1200f;
            while (host.LatestMacroSimulationSeconds + 0.001d < requiredMacroSeconds
                   && string.IsNullOrEmpty(host.LastError)
                   && UnityEngine.Time.realtimeSinceStartup < macroDeadline)
            {
                yield return null;
            }

            Assert.That(host.LastError, Is.Null.Or.Empty);
            Assert.That(
                host.LatestMacroSimulationSeconds,
                Is.GreaterThanOrEqualTo(requiredMacroSeconds - 0.001d),
                "The day captures require the running macro world to catch up.");
        }

        private IEnumerator Tap(ButtonControl button)
        {
            Press(button);
            yield return null;
            Release(button);
            yield return null;
        }

        private static IEnumerator CaptureAndRecord(
            Camera camera,
            SteppeSimulationHost host,
            SteppeTimeSystem time,
            TerrainChunkStreamer streamer,
            SteppeGrassRenderer grass,
            SteppeGroundDetailRenderer groundDetails,
            SteppeEcologySystem ecology,
            FloatingOriginSystem floatingOrigin,
            RoutePoint expected,
            int waypointIndex,
            string phase,
            string fileName,
            string outputDirectory,
            StringBuilder manifest,
            StringBuilder samples)
        {
            var position = floatingOrigin.LocalToWorld(camera.transform.position);
            Assert.That(
                HorizontalDistance(position, expected.Position),
                Is.LessThanOrEqualTo(PositionToleranceMeters));

            yield return CapturePng(camera, Path.Combine(outputDirectory, fileName));
            streamer.GetLodCounts(out var near, out var middle, out var far);
            host.TryGetEnvironmentSample(position.X, position.Z, out var environment);

            manifest.AppendLine(
                $"{fileName}\tphase={phase}\twaypoint={waypointIndex + 1}\t"
                + $"world=({F(position.X)},{F(position.Y)},{F(position.Z)})\t"
                + $"hour={F(time.Current.Hour)}\tchunk={streamer.CenterCoordinate}\t"
                + $"lod={near}/{middle}/{far}\tgrassCells={grass.LoadedCellCount}\t"
                + $"grassInstances={grass.InstanceCount}\tdetailCells={groundDetails.LoadedCellCount}\t"
                + $"detailCandidates={groundDetails.CandidateCount}\t"
                + $"detailKinds={groundDetails.DetailKindCount}\tecoCells={ecology.StoredCellCount}");

            samples.Append(fileName).Append('\t')
                .Append(phase).Append('\t')
                .Append(waypointIndex + 1).Append('\t')
                .Append(F(position.X)).Append('\t')
                .Append(F(position.Z)).Append('\t')
                .Append(F(time.Current.Hour)).Append('\t')
                .Append(F(host.LatestMacroSimulationSeconds / 3600d)).Append('\t');
            AppendEnvironmentSample(samples, environment);

            var snapshot = host.LatestSnapshot;
            foreach (var descriptor in StateCatalog.All)
            {
                samples.Append('\t');
                if (snapshot != null
                    && snapshot.TrySample(descriptor.Id, position.X, position.Z, out var value))
                {
                    samples.Append(F(value));
                }
            }

            foreach (var descriptor in FluxCatalog.All.Where(item => item.Order < 600))
            {
                samples.Append('\t');
                if (snapshot != null
                    && snapshot.TrySampleFlux(
                        descriptor.Id,
                        position.X,
                        position.Z,
                        out var amount,
                        out var periodHours))
                {
                    samples.Append(F(amount / Math.Max(0.0001d, periodHours)));
                }
            }

            foreach (var descriptor in VectorProcessCatalog.All)
            {
                if (snapshot != null
                    && snapshot.TrySampleVectorProcess(
                        descriptor.Process,
                        position.X,
                        position.Z,
                        out var vectorX,
                        out var vectorY,
                        out var magnitude,
                        out var grossMagnitude,
                        out var periodHours))
                {
                    var rateScale = descriptor.Measure == VectorProcessMeasure.AccumulatedTransfer
                                    || descriptor.Measure == VectorProcessMeasure.TransportMoment
                        ? 1d / Math.Max(0.0001d, periodHours)
                        : 1d;
                    samples.Append('\t').Append(F(vectorX * rateScale))
                        .Append('\t').Append(F(vectorY * rateScale))
                        .Append('\t').Append(F(magnitude * rateScale))
                        .Append('\t').Append(F(grossMagnitude * rateScale));
                }
                else
                {
                    samples.Append("\t\t\t\t");
                }
            }

            samples.AppendLine();
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
            // Production indirect presenters submit per camera from
            // RenderPipelineManager.beginCameraRendering. Manual capture therefore
            // exercises the same draw path as the game camera instead of racing a
            // LateUpdate-only submission.
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

        private static void WriteHeaders(StringBuilder manifest, StringBuilder samples)
        {
            manifest.AppendLine("STEPPE RUNNING-WORLD TRAVERSAL VISUAL REGRESSION");
            manifest.AppendLine($"UTC: {DateTime.UtcNow:O}");
            manifest.AppendLine($"Resolution: {CaptureWidth}x{CaptureHeight}");
            manifest.AppendLine(
                "The normal bootstrap, WASD camera, floating origin, simulation, streaming, "
                + "ecology, grass, semantic ground details and weather systems remain active.");
            manifest.AppendLine(
                "No diagnostic layer, synthetic scene, forced parameter value or direct teleport is used.");
            manifest.AppendLine();

            samples.Append(
                "file\tphase\twaypoint\tworld_x_m\tworld_z_m\thour\tmacro_hour\t"
                + "surface_temp_c\tair_temp_c\twind_x_ms\twind_z_ms\thumidity_mm\t"
                + "precipitation_mm_h\tsurface_water_mm\troot_water_mm\tsnow_mm\t"
                + "live_biomass_g_m2\tdry_biomass_g_m2\tcrust_fraction\tdust_g_m2\t"
                + "burn_fraction");
            foreach (var descriptor in StateCatalog.All)
            {
                samples.Append('\t').Append(descriptor.Id);
            }

            foreach (var descriptor in FluxCatalog.All.Where(item => item.Order < 600))
            {
                samples.Append('\t').Append("flux_").Append(descriptor.Id).Append("_per_h");
            }

            foreach (var descriptor in VectorProcessCatalog.All)
            {
                var prefix = "vector_" + descriptor.Process;
                samples.Append('\t').Append(prefix).Append("_x")
                    .Append('\t').Append(prefix).Append("_y")
                    .Append('\t').Append(prefix).Append("_net")
                    .Append('\t').Append(prefix).Append("_gross");
            }

            samples.AppendLine();
        }

        private static void AppendEnvironmentSample(
            StringBuilder samples,
            SteppeSimulationEnvironmentSample environment)
        {
            if (environment == null)
            {
                samples.Append("NA\tNA\tNA\tNA\tNA\tNA\tNA\tNA\tNA\tNA\tNA\tNA\tNA\tNA");
                return;
            }

            samples.Append(F(environment.SurfaceTemperatureC)).Append('\t')
                .Append(F(environment.AirTemperatureC)).Append('\t')
                .Append(F(environment.WindXMs)).Append('\t')
                .Append(F(environment.WindZMs)).Append('\t')
                .Append(F(environment.HumidityMillimeters)).Append('\t')
                .Append(F(environment.PrecipitationMillimetersPerHour)).Append('\t')
                .Append(F(environment.SurfaceWaterMillimeters)).Append('\t')
                .Append(F(environment.RootWaterMillimeters)).Append('\t')
                .Append(F(environment.SnowWaterEquivalentMillimeters)).Append('\t')
                .Append(F(environment.LiveBiomassGramsPerSquareMeter)).Append('\t')
                .Append(F(environment.DryBiomassGramsPerSquareMeter)).Append('\t')
                .Append(F(environment.SurfaceCrustFraction)).Append('\t')
                .Append(F(environment.DustGramsPerSquareMeter)).Append('\t')
                .Append(F(environment.BurnScarFraction));
        }

        private static string CreateOutputDirectory()
        {
            var root = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "Artifacts",
                "SteppeWorldTraversal"));
            var path = Path.Combine(root, $"run-{DateTime.UtcNow:yyyyMMdd-HHmmss}");
            Directory.CreateDirectory(path);
            return path;
        }

        private static double HorizontalDistance(WorldPosition left, WorldPosition right)
        {
            var dx = left.X - right.X;
            var dz = left.Z - right.Z;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        private static string F(double value) =>
            value.ToString("0.######", CultureInfo.InvariantCulture);

        private readonly struct RoutePoint
        {
            public RoutePoint(WorldPosition position, Quaternion rotation)
            {
                Position = position;
                Rotation = rotation;
            }

            public WorldPosition Position { get; }
            public Quaternion Rotation { get; }
        }
    }
}
