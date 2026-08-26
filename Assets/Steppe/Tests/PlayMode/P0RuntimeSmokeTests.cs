using System.Collections;
using NUnit.Framework;
using Steppe.Caravan;
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
using UnityEngine.TestTools;

namespace Steppe.Tests
{
    public sealed class P0RuntimeSmokeTests
    {
        [UnityTest, Order(-100), Ignore("Caravan runtime is intentionally disabled while the steppe simulation is completed.")]
        public IEnumerator PrototypeCreatesPhysicalCaravanKeeperAndStreamsTerrain()
        {
            if (Object.FindAnyObjectByType<SteppePrototypeBootstrap>() == null)
            {
                new GameObject("P0 Test Bootstrap").AddComponent<SteppePrototypeBootstrap>();
            }

            yield return null;
            yield return null;

            var caravan = Object.FindAnyObjectByType<CaravanChassisController>();
            var firstPerson = Object.FindAnyObjectByType<CaravanFirstPersonController>();
            var sail = Object.FindAnyObjectByType<CaravanSailModule>();
            var windVane = Object.FindAnyObjectByType<CaravanWindVane>();
            var photovoltaic = Object.FindAnyObjectByType<CaravanPhotovoltaicModule>();
            var electricalNetwork = Object.FindAnyObjectByType<CaravanElectricalNetwork>();
            var fluidNetwork = Object.FindAnyObjectByType<CaravanFluidNetwork>();
            var materialNetworks =
                Object.FindObjectsByType<CaravanMaterialNetwork>(
                    FindObjectsInactive.Include);
            var biomassNetwork = System.Array.Find(
                materialNetworks,
                item => item.Kind == CaravanMaterialNetworkKind.Biomass);
            var mechanicalNetwork = System.Array.Find(
                materialNetworks,
                item => item.Kind == CaravanMaterialNetworkKind.Mechanical);
            var resourceSystem =
                Object.FindAnyObjectByType<CaravanResourceSystem>();
            var construction =
                Object.FindAnyObjectByType<CaravanConstructionService>();
            var battery = Object.FindAnyObjectByType<CaravanBatteryModule>();
            var electricMotor = Object.FindAnyObjectByType<CaravanElectricMotorModule>();
            var buildMode = Object.FindAnyObjectByType<CaravanBuildModeController>();
            var interactor = Object.FindAnyObjectByType<CaravanPlayerInteractor>();
            var controlStations = Object.FindObjectsByType<CaravanControlStation>(
                FindObjectsInactive.Include);
            var tracks = Object.FindAnyObjectByType<SteppeTrackSystem>();
            var streamer = Object.FindAnyObjectByType<TerrainChunkStreamer>();
            var grass = Object.FindAnyObjectByType<SteppeGrassRenderer>();
            var timeSystem = Object.FindAnyObjectByType<SteppeTimeSystem>();
            var weatherSystem = Object.FindAnyObjectByType<SteppeWeatherSystem>();
            var ecologySystem = Object.FindAnyObjectByType<SteppeEcologySystem>();
            var dust = Object.FindAnyObjectByType<SteppeDustPresentation>();
            var snow = Object.FindAnyObjectByType<SteppeSnowPresentation>();
            var workScheduler = Object.FindAnyObjectByType<WorldWorkScheduler>();

            Assert.That(firstPerson, Is.Not.Null);
            Assert.That(caravan, Is.Not.Null);
            Assert.That(sail, Is.Null);
            Assert.That(windVane, Is.Null);
            Assert.That(photovoltaic, Is.Not.Null);
            Assert.That(electricalNetwork, Is.Not.Null);
            Assert.That(fluidNetwork, Is.Not.Null);
            Assert.That(biomassNetwork, Is.Not.Null);
            Assert.That(mechanicalNetwork, Is.Not.Null);
            Assert.That(resourceSystem, Is.Not.Null);
            Assert.That(construction, Is.Not.Null);
            Assert.That(battery, Is.Not.Null);
            Assert.That(electricMotor, Is.Not.Null);
            Assert.That(electricalNetwork.IsClosed, Is.False);
            Assert.That(battery.StateOfCharge, Is.EqualTo(0.35f).Within(0.01f));
            Assert.That(electricMotor.RequestedThrottle, Is.Zero);
            var electricalPorts = Object.FindObjectsByType<CaravanElectricalPort>(
                FindObjectsInactive.Include);
            var electricalCables = Object.FindObjectsByType<CaravanElectricalCable>(
                FindObjectsInactive.Include);
            Assert.That(electricalPorts, Has.Length.EqualTo(3));
            Assert.That(electricalCables, Is.Empty);
            Assert.That(fluidNetwork.Ports, Has.Count.EqualTo(1));
            Assert.That(fluidNetwork.Pipes, Is.Empty);
            Assert.That(biomassNetwork.Ports, Is.Empty);
            Assert.That(biomassNetwork.Links, Is.Empty);
            Assert.That(mechanicalNetwork.Ports, Has.Count.EqualTo(1));
            Assert.That(mechanicalNetwork.Links, Is.Empty);
            var communicationPreview = GameObject.Find("Communication Cable Preview");
            Assert.That(communicationPreview, Is.Not.Null);
            Assert.That(communicationPreview.GetComponent<LineRenderer>(), Is.Not.Null);
            Assert.That(communicationPreview.GetComponent<Collider>(), Is.Null);
            Assert.That(communicationPreview.GetComponent<Rigidbody>(), Is.Null);
            Assert.That(
                System.Array.TrueForAll(
                    electricalPorts,
                    port => !port.IsBuildMarkerVisible),
                Is.True);
            Assert.That(
                System.Array.Find(electricalPorts, port => port.Kind == CaravanElectricalPortKind.Storage)
                    .ConnectedCableCount,
                Is.Zero);
            Assert.That(buildMode, Is.Not.Null);
            Assert.That(interactor, Is.Not.Null);
            Assert.That(controlStations, Has.Length.EqualTo(4));
            var steeringStation = System.Array.Find(
                controlStations,
                station => station.Kind == CaravanControlKind.Steering);
            var brakeStation = System.Array.Find(
                controlStations,
                station => station.Kind == CaravanControlKind.Brake);
            var trimStation = System.Array.Find(
                controlStations,
                station => station.Kind == CaravanControlKind.SailTrim);
            var motorStation = System.Array.Find(
                controlStations,
                station => station.Kind == CaravanControlKind.ElectricThrottle);
            var solarStation = System.Array.Find(
                controlStations,
                station => station.Kind == CaravanControlKind.SolarOrientation);
            Assert.That(steeringStation, Is.Not.Null);
            Assert.That(brakeStation, Is.Not.Null);
            Assert.That(trimStation, Is.Null);
            Assert.That(motorStation, Is.Not.Null);
            Assert.That(solarStation, Is.Not.Null);
            Assert.That(
                steeringStation.transform.Find("Control Visual/Wheel Rim 1"),
                Is.Not.Null);
            Assert.That(
                brakeStation.transform.Find("Control Visual/Lever Grip"),
                Is.Not.Null);
            Assert.That(steeringStation.transform.Find("Focus Indicator"), Is.Not.Null);
            var steeringCollider = steeringStation.GetComponent<SphereCollider>();
            var brakeCollider = brakeStation.GetComponent<SphereCollider>();
            Assert.That(steeringCollider, Is.Not.Null);
            Assert.That(brakeCollider, Is.Not.Null);
            Assert.That(
                steeringCollider.bounds.Intersects(brakeCollider.bounds),
                Is.False,
                "The brake interaction volume must not obscure the steering wheel.");
            steeringStation.SetNormalized(0.65f);
            Assert.That(caravan.SteeringNormalized, Is.EqualTo(0.65f));
            Assert.That(
                caravan.AppliedSteeringNormalized,
                Is.EqualTo(0.65f),
                "Steering must reach the vehicle immediately, without waiting for drive physics.");
            steeringStation.SetNormalized(0f);
            Assert.That(caravan.SteeringNormalized, Is.Zero);
            Assert.That(caravan.AppliedSteeringNormalized, Is.Zero);
            brakeStation.SetNormalized(1f);
            Assert.That(caravan.BrakeNormalized, Is.EqualTo(1f));
            brakeStation.SetNormalized(-1f);
            Assert.That(caravan.BrakeNormalized, Is.Zero);
            var steeringTarget = steeringCollider.bounds.center;
            var aimingOrigin = steeringTarget
                               - caravan.transform.forward * 2.2f
                               + caravan.transform.up * 0.15f;
            var steeringRay = new Ray(
                aimingOrigin,
                (steeringTarget - aimingOrigin).normalized);
            Assert.That(
                interactor.ResolveControlTarget(steeringRay),
                Is.SameAs(steeringStation),
                "Aiming at the steering wheel must not select the nearby brake lever.");
            Assert.That(interactor.TryBeginControl(steeringStation), Is.True);
            Assert.That(firstPerson.InteractionControl, Is.True);
            Assert.That(interactor.AdjustActiveControl(0.4f), Is.True);
            Assert.That(caravan.SteeringNormalized, Is.EqualTo(0.4f));
            Assert.That(caravan.AppliedSteeringNormalized, Is.EqualTo(0.4f));
            interactor.EndControl();
            Assert.That(firstPerson.InteractionControl, Is.False);
            steeringStation.SetNormalized(0f);
            Assert.That(firstPerson.GetComponent<CharacterController>(), Is.Not.Null);
            Assert.That(firstPerson.GetComponent<Rigidbody>(), Is.Null);
            Assert.That(firstPerson.CaravanContactIsolationEnabled, Is.True);
            Assert.That(firstPerson.CaravanCollisionProxyCount, Is.GreaterThan(0));
            var keeperCollisionProxy = GameObject.Find("Keeper Caravan Collision Proxy");
            Assert.That(keeperCollisionProxy, Is.Not.Null);
            Assert.That(keeperCollisionProxy.GetComponent<Rigidbody>().isKinematic, Is.True);
            Assert.That(keeperCollisionProxy.transform.IsChildOf(caravan.transform), Is.False);
            Assert.That(
                Physics.GetIgnoreLayerCollision(
                    CaravanFirstPersonController.CollisionProxyLayer,
                    caravan.gameObject.layer),
                Is.True);
            foreach (var caravanCollider in caravan.GetComponentsInChildren<Collider>(true))
            {
                if (caravanCollider.enabled && !caravanCollider.isTrigger)
                {
                    Assert.That(
                        Physics.GetIgnoreCollision(
                            firstPerson.GetComponent<CharacterController>(),
                            caravanCollider),
                        Is.True,
                        $"The keeper still has a physical contact with {caravanCollider.name}.");
                }
            }
            Assert.That(caravan.GetComponent<Rigidbody>(), Is.Not.Null);
            Assert.That(caravan.Body.mass, Is.InRange(3300f, 3400f));
            Assert.That(caravan.DefaultDriveEnabled, Is.False);
            var deckSurface = GameObject.Find("Deck Build Surface");
            Assert.That(deckSurface, Is.Not.Null);
            Assert.That(deckSurface.GetComponent<Rigidbody>(), Is.Null);
            Assert.That(
                photovoltaic.CurrentGenerationKilowatts,
                Is.InRange(0f, photovoltaic.GetComponent<CaravanPart>().Capacity));
            Assert.That(photovoltaic.CurrentIncidence, Is.InRange(0f, 1f));
            Assert.That(photovoltaic.CurrentCloudTransmission, Is.InRange(0.06f, 1f));
            Assert.That(
                Shader.GetGlobalFloat("_SteppeCloudTransmissionAtFocus"),
                Is.InRange(0.06f, 1f));
            var initialSurfaceWind = new Vector3(
                weatherSystem.CurrentAtFocus.SurfaceWind.x,
                0f,
                weatherSystem.CurrentAtFocus.SurfaceWind.y).normalized;
            if (weatherSystem.UsesFiniteWorld)
            {
                var simulationHost = Object.FindAnyObjectByType<SteppeSimulationHost>();
                var world = Object.FindAnyObjectByType<FloatingOriginSystem>()
                    .LocalToWorld(caravan.transform.position);
                Assert.That(
                    simulationHost.TryGetEnvironmentSample(world.X, world.Z, out var canonical),
                    Is.True);
                var canonicalWind = new Vector3(canonical.WindXMs, 0f, canonical.WindZMs).normalized;
                Assert.That(Vector3.Dot(initialSurfaceWind, canonicalWind), Is.GreaterThan(0.999f));
            }
            else
            {
                Assert.That(
                    Vector3.Dot(caravan.transform.forward, initialSurfaceWind),
                    Is.GreaterThan(0.98f),
                    "The caravan nose is not aligned with the legacy startup wind.");
            }
            var caravanParts = Object.FindObjectsByType<CaravanPart>();
            Assert.That(caravanParts.Length, Is.EqualTo(3));
            foreach (var kind in new[]
                     {
                         CaravanPartKind.PhotovoltaicLeaves,
                         CaravanPartKind.Battery,
                         CaravanPartKind.ElectricMotor
                     })
            {
                Assert.That(
                    System.Array.Exists(caravanParts, part => part.Kind == kind),
                    Is.True,
                    $"The initial caravan is missing the {kind} part.");
            }
            Assert.That(Object.FindAnyObjectByType<CaravanMountGrid>().Width, Is.EqualTo(10));
            Assert.That(Object.FindAnyObjectByType<CaravanMountGrid>().Length, Is.EqualTo(18));
            Assert.That(Object.FindObjectsByType<WheelCollider>().Length, Is.EqualTo(0));
            Assert.That(caravan.GetComponent("VPVehicleController"), Is.Not.Null);
            Assert.That(
                System.Array.FindAll(
                    caravan.GetComponentsInChildren<MonoBehaviour>(true),
                    component => component.GetType().Name == "VPWheelCollider"),
                Has.Length.EqualTo(4));
            Assert.That(tracks, Is.Not.Null);
            Assert.That(tracks.StateMap, Is.Not.Null);
            Assert.That(tracks.StateMap.width, Is.EqualTo(512));
            Assert.That(Shader.GetGlobalTexture("_SteppeTrackStateMap"), Is.SameAs(tracks.StateMap));
            var originSystem = Object.FindAnyObjectByType<FloatingOriginSystem>();
            var initialBallWorld = originSystem.LocalToWorld(caravan.transform.position);
            for (var frame = 0;
                 frame < 120 && !streamer.HasPhysicsSurfaceAt(initialBallWorld.X, initialBallWorld.Z);
                 frame++)
            {
                yield return null;
            }
            streamer.GetLodCounts(out var nearPhysicsLod, out var middlePhysicsLod, out var farPhysicsLod);
            Assert.That(
                streamer.HasPhysicsSurfaceAt(initialBallWorld.X, initialBallWorld.Z),
                Is.True,
                $"The near terrain chunk never exposed a physics collider. world={initialBallWorld}, "
                + $"center={streamer.CenterCoordinate}, loaded={streamer.LoadedCount}, "
                + $"lod={nearPhysicsLod}/{middlePhysicsLod}/{farPhysicsLod}, "
                + $"meshColliders={Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include).Length}");
            for (var frame = 0; frame < 40 && caravan.Body.isKinematic; frame++)
            {
                yield return new WaitForFixedUpdate();
            }
            Assert.That(caravan.Body.isKinematic, Is.False, "Caravan never attached to a streamed terrain collider.");
            Assert.That(Object.FindObjectsByType<MeshCollider>().Length, Is.GreaterThan(0));
            Assert.That(
                Vector3.Dot(caravan.transform.up, Vector3.up),
                Is.GreaterThan(0.88f),
                "The suspended chassis became unstable during straight-line driving.");

            caravan.Body.linearVelocity = Vector3.forward * 3f;
            for (var fixedStep = 0; fixedStep < 20; fixedStep++)
            {
                yield return new WaitForFixedUpdate();
            }
            Assert.That(tracks.StoredTrackCellCount, Is.GreaterThan(0), "The caravan did not leave a canonical track.");
            Assert.That(streamer, Is.Not.Null);
            Assert.That(streamer.LoadedCount, Is.GreaterThan(0));
            Assert.That(Object.FindAnyObjectByType<BiomeDebugNavigator>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<SteppeTimeSystem>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<SteppeCelestialPresentation>(), Is.Not.Null);
            Assert.That(
                System.Array.Exists(
                    Object.FindObjectsByType<MonoBehaviour>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None),
                    component => component.GetType().Name == "CaravanCoreRuntimeAdapter"
                                 || component.GetType().Name == "SteppeCaravanProfileHost"),
                Is.False,
                "The player caravan must not run the console caravan simulation.");
            Assert.That(Object.FindAnyObjectByType<SteppeWeatherSystem>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<SteppeCloudLayer>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<SteppeRainPresentation>(), Is.Not.Null);
            Assert.That(ecologySystem, Is.Not.Null);
            Assert.That(dust, Is.Not.Null);
            Assert.That(snow, Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<SteppeGrassRenderer>(), Is.Not.Null);
            Assert.That(workScheduler, Is.Not.Null);
            Assert.That(workScheduler.RegisteredSourceCount, Is.GreaterThanOrEqualTo(4));
            Assert.That(workScheduler.TotalStepsExecuted, Is.GreaterThan(0));
            Assert.That(ecologySystem.ActiveCellCount, Is.GreaterThan(0));
            Assert.That(ecologySystem.StoredCellCount, Is.GreaterThan(0));
            Assert.That(weatherSystem.UsesFiniteWorld, Is.True);
            Assert.That(ecologySystem.UsesFiniteWorld, Is.True);
            Assert.That(ecologySystem.IsStateMapReady, Is.True);
            Assert.That(ecologySystem.StateMap, Is.Not.Null);
            Assert.That(ecologySystem.StateMap.width, Is.EqualTo(128));
            Assert.That(ecologySystem.StateMap.height, Is.EqualTo(128));
            Assert.That(ecologySystem.CryosphereMap, Is.Not.Null);
            Assert.That(ecologySystem.CryosphereMap.width, Is.EqualTo(128));
            Assert.That(ecologySystem.CryosphereMap.height, Is.EqualTo(128));
            Assert.That(Shader.GetGlobalTexture("_SteppeEcologyStateMap"), Is.SameAs(ecologySystem.StateMap));
            Assert.That(
                Shader.GetGlobalTexture("_SteppeCryosphereStateMap"),
                Is.SameAs(ecologySystem.CryosphereMap));
            var ecologyMapParameters = Shader.GetGlobalVector("_SteppeEcologyMapParameters");
            Assert.That(ecologyMapParameters.x, Is.EqualTo(ecologySystem.StateMap.width));
            Assert.That(ecologyMapParameters.y, Is.EqualTo(1f));
            Assert.That(ecologyMapParameters.w, Is.EqualTo(65536f));
            var focusWorld = Object.FindAnyObjectByType<FloatingOriginSystem>()
                .LocalToWorld(caravan.transform.position);
            Assert.That(ecologySystem.TryGetState(focusWorld.X, focusWorld.Z, out var focusEcology), Is.True);
            Assert.That(focusEcology.RootWater, Is.InRange(0.0, 1.0));
            for (var frame = 0; frame < 40 && ecologySystem.MapRevision < 2; frame++)
            {
                yield return null;
            }
            Assert.That(ecologySystem.MapRevision, Is.GreaterThanOrEqualTo(2));
            Assert.That(
                ecologySystem.TryGetMapPixelCoordinate(ecologySystem.CenterCoordinate, out var mapX, out var mapZ),
                Is.True);
            SteppeEcoCellState mappedEcology = default;
            Color32 mapPixel = default;
            for (var frame = 0; frame < 80; frame++)
            {
                Assert.That(ecologySystem.TryGetState(ecologySystem.CenterCoordinate, out mappedEcology), Is.True);
                mapPixel = ecologySystem.StateMap.GetPixels32()[mapZ * ecologySystem.StateMap.width + mapX];
                if (System.Math.Abs(
                        SteppeEcologyMapEncoding.Decode(mapPixel.r) - mappedEcology.SurfaceWater)
                    <= 2.0 / 255.0)
                {
                    break;
                }

                yield return null;
            }
            Assert.That(
                SteppeEcologyMapEncoding.Decode(mapPixel.r),
                Is.EqualTo(mappedEcology.SurfaceWater).Within(2.0 / 255.0));
            Assert.That(
                SteppeEcologyMapEncoding.Decode(mapPixel.a),
                Is.EqualTo(mappedEcology.SurfaceCrust).Within(2.0 / 255.0));
            Assert.That(GameObject.Find("Steppe Sun"), Is.Not.Null);
            Assert.That(GameObject.Find("Steppe Moon"), Is.Not.Null);
            Assert.That(RenderSettings.skybox, Is.Not.Null);
            Assert.That(RenderSettings.skybox.shader.name, Is.EqualTo("Steppe/Skybox"));
            Assert.That(Shader.Find("Steppe/Terrain Surface"), Is.Not.Null);
            Assert.That(Shader.Find("Steppe/Terrain Surface").isSupported, Is.True);
            Assert.That(Shader.Find("Steppe/Grass Indirect"), Is.Not.Null);
            Assert.That(Shader.Find("Steppe/Grass Indirect").isSupported, Is.True);
            Assert.That(Shader.Find("Steppe/Dust Wisp"), Is.Not.Null);
            Assert.That(Shader.Find("Steppe/Dust Wisp").isSupported, Is.True);
            Assert.That(dust.Particles, Is.Not.Null);
            Assert.That(dust.Particles.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
            Assert.That(dust.Renderer.renderMode, Is.EqualTo(ParticleSystemRenderMode.Stretch));
            Assert.That(dust.Renderer.sharedMaterial.shader.name, Is.EqualTo("Steppe/Dust Wisp"));
            Assert.That(Shader.Find("Steppe/Snow Flake"), Is.Not.Null);
            Assert.That(Shader.Find("Steppe/Snow Flake").isSupported, Is.True);
            Assert.That(snow.Particles, Is.Not.Null);
            Assert.That(snow.Particles.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
            Assert.That(snow.Renderer.renderMode, Is.EqualTo(ParticleSystemRenderMode.Billboard));
            Assert.That(snow.Renderer.sharedMaterial.shader.name, Is.EqualTo("Steppe/Snow Flake"));
            Assert.That(timeSystem.DebugMultiplier, Is.EqualTo(1f));
            Assert.That(GameObject.Find("Cloud Layer"), Is.Not.Null);
            Assert.That(GameObject.Find("Rain Volume"), Is.Not.Null);
            Assert.That(GameObject.Find("Dust Field"), Is.Not.Null);
            Assert.That(GameObject.Find("Snow Volume"), Is.Not.Null);
            Assert.That(GameObject.Find("Grass Field"), Is.Not.Null);
            Assert.That(firstPerson.ViewCamera.GetComponent<Collider>(), Is.Null);
            Assert.That(firstPerson.ViewCamera.GetComponent<Rigidbody>(), Is.Null);
            if (grass.IsRendering)
            {
                Assert.That(grass.LoadedCellCount, Is.GreaterThan(0));
                Assert.That(grass.InstanceCount, Is.GreaterThan(0));
                Assert.That(grass.UsesAuthoredMesh, Is.True);
                Assert.That(grass.TuftVertexCount, Is.GreaterThan(12));
                Assert.That(grass.TuftTriangleCount, Is.EqualTo(72));
                var wind = Shader.GetGlobalVector("_SteppeWindVelocity");
                Assert.That(new Vector2(wind.x, wind.y).magnitude, Is.GreaterThan(0.1f));
                Assert.That(wind.z, Is.EqualTo(new Vector2(wind.x, wind.y).magnitude).Within(0.001f));
                Assert.That(
                    Vector2.Distance(new Vector2(wind.x, wind.y), weatherSystem.CurrentAtFocus.SurfaceWind),
                    Is.LessThan(0.001f));
                Assert.That(
                    Shader.GetGlobalFloat("_SteppeWindTime"),
                    Is.EqualTo((float)weatherSystem.WeatherSeconds).Within(0.05f));
                Assert.That(Shader.GetGlobalFloat("_SteppeWindAnimationTime"), Is.GreaterThan(0f));
                Assert.That(
                    Shader.GetGlobalFloat("_SteppeWindAnimationTime"),
                    Is.EqualTo((float)weatherSystem.WindAnimationSeconds).Within(0.01f));
                var windAdvection = Shader.GetGlobalVector("_SteppeSurfaceWindAdvection");
                Assert.That(windAdvection.x, Is.EqualTo((float)weatherSystem.CurrentWind.SurfaceAdvection.X).Within(0.01f));
                Assert.That(windAdvection.y, Is.EqualTo((float)weatherSystem.CurrentWind.SurfaceAdvection.Z).Within(0.01f));
                Assert.That(Shader.GetGlobalVector("_SteppeWindFieldBasis").magnitude, Is.GreaterThan(0.9f));
            }
            else
            {
                Assert.That(Object.FindAnyObjectByType<SteppeLegacyVegetationRenderer>(), Is.Not.Null);
            }
        }

        [UnityTest]
        public IEnumerator ContinuousClockDrivesInterpolatedWeatherAndPause()
        {
            if (Object.FindAnyObjectByType<SteppePrototypeBootstrap>() == null)
            {
                new GameObject("P1 Canonical Time Test Bootstrap").AddComponent<SteppePrototypeBootstrap>();
            }

            var timeSystem = Object.FindAnyObjectByType<SteppeTimeSystem>();
            var weatherSystem = Object.FindAnyObjectByType<SteppeWeatherSystem>();
            var simulationHost = Object.FindAnyObjectByType<SteppeSimulationHost>();
            for (var frame = 0; frame < 300 && !weatherSystem.UsesFiniteWorld; frame++)
            {
                yield return null;
            }

            Assert.That(timeSystem.UsesFiniteWorld, Is.True);
            Assert.That(timeSystem.DaysPerYear, Is.EqualTo(365));
            Assert.That(timeSystem.ElapsedSimulationSeconds,
                Is.EqualTo(weatherSystem.WeatherSeconds).Within(0.0001d));
            Assert.That(timeSystem.Current.DayOfYear, Is.InRange(1d, 366d));

            timeSystem.SetPaused(true);
            yield return null;
            Assert.That(simulationHost.IsTimePaused, Is.True);
            var pausedSeconds = timeSystem.ElapsedSimulationSeconds;
            for (var frame = 0; frame < 10; frame++)
            {
                yield return null;
            }

            Assert.That(timeSystem.ElapsedSimulationSeconds,
                Is.EqualTo(pausedSeconds).Within(0.0001d));

            var macroStepSeconds = simulationHost.FixedStepHours * 3600d;
            var currentBoundary = System.Math.Floor(pausedSeconds / macroStepSeconds)
                                  * macroStepSeconds;
            var midpoint = currentBoundary + macroStepSeconds * 0.5d;
            if (midpoint <= pausedSeconds)
            {
                midpoint += macroStepSeconds;
            }
            timeSystem.AdvanceSimulationSeconds(midpoint - pausedSeconds);
            for (var frame = 0;
                 frame < 300 && simulationHost.LatestMacroSimulationSeconds < midpoint + macroStepSeconds * 0.5d;
                 frame++)
            {
                yield return null;
            }

            // The worker can publish after the weather presentation Update of this
            // frame. Give shader globals one main-thread pass to consume the snapshot.
            yield return null;
            Assert.That(weatherSystem.UsesFiniteWorld, Is.True);
            Assert.That(
                weatherSystem.WeatherSeconds,
                Is.EqualTo(midpoint).Within(0.0001d));
            Assert.That(timeSystem.ElapsedSimulationSeconds,
                Is.EqualTo(weatherSystem.WeatherSeconds).Within(0.0001d));
            Assert.That(simulationHost.MacroInterpolationAlpha, Is.EqualTo(0.5f).Within(0.02f));
            Assert.That(
                Shader.GetGlobalFloat("_SteppeWindTime"),
                Is.EqualTo((float)weatherSystem.WeatherSeconds).Within(0.05f));

            Assert.That(simulationHost.TryGetEnvironmentSample(10d, 10d, out var firstLocal), Is.True);
            Assert.That(simulationHost.TryGetEnvironmentSample(45d, 10d, out var secondLocal), Is.True);
            Assert.That(
                Mathf.Abs(firstLocal.WindXMs - secondLocal.WindXMs)
                + Mathf.Abs(firstLocal.WindZMs - secondLocal.WindZMs),
                Is.GreaterThan(0.0001f));

            timeSystem.SetPaused(false);
            var continuousBefore = timeSystem.ElapsedSimulationSeconds;
            yield return null;
            Assert.That(timeSystem.ElapsedSimulationSeconds, Is.GreaterThan(continuousBefore));
            Assert.That(
                timeSystem.ElapsedSimulationSeconds - continuousBefore,
                Is.LessThan(macroStepSeconds * 0.1d),
                "The visible clock advanced by a macro tick instead of a frame delta.");
        }

        [UnityTest, Order(-90)]
        public IEnumerator PrototypeCreatesSteppeObserverAndCompleteSimulationVisualization()
        {
            if (Object.FindAnyObjectByType<SteppePrototypeBootstrap>() == null)
            {
                new GameObject("Steppe Observer Test Bootstrap").AddComponent<SteppePrototypeBootstrap>();
            }

            var host = Object.FindAnyObjectByType<SteppeSimulationHost>();
            for (var frame = 0; frame < 600 && (host == null || !host.IsReady); frame++)
            {
                yield return null;
                host = Object.FindAnyObjectByType<SteppeSimulationHost>();
            }

            Assert.That(host, Is.Not.Null);
            Assert.That(host.LastError, Is.Null.Or.Empty);
            Assert.That(host.LatestSnapshot, Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<FlyCameraController>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<CaravanChassisController>(), Is.Null);
            Assert.That(Object.FindAnyObjectByType<CaravanFirstPersonController>(), Is.Null);
            Assert.That(Object.FindAnyObjectByType<SteppeTrackSystem>(), Is.Null);
            var terrainStreamer = Object.FindAnyObjectByType<TerrainChunkStreamer>();
            Assert.That(terrainStreamer, Is.Not.Null);
            Assert.That(terrainStreamer.HasSurfaceWaterPresentation, Is.True);
            Assert.That(terrainStreamer.HasSnowPresentation, Is.True);
            Assert.That(Object.FindAnyObjectByType<SteppeWindCuePresentation>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<SteppeMacroProcessPresentation>(), Is.Not.Null);
            var grass = Object.FindAnyObjectByType<SteppeGrassRenderer>();
            Assert.That(grass, Is.Not.Null);
            Assert.That(grass.EffectiveTuftAssetHeight, Is.InRange(0.5f, 1.5f));
            Assert.That(grass.EffectiveTuftAssetWidth, Is.InRange(0.5f, 2.5f));
            var groundDetails = Object.FindAnyObjectByType<SteppeGroundDetailRenderer>();
            Assert.That(groundDetails, Is.Not.Null);
            Assert.That(groundDetails.IsRendering, Is.EqualTo(SteppeGrassRenderer.HardwareSupported));
            Assert.That(groundDetails.DetailKindCount, Is.EqualTo(11));
            Assert.That(groundDetails.UsesSemanticField, Is.True);
            if (groundDetails.IsRendering)
            {
                for (var frame = 0; frame < 240 && groundDetails.CandidateCount == 0; frame++)
                {
                    yield return null;
                }
                Assert.That(groundDetails.LoadedCellCount, Is.GreaterThan(0));
                Assert.That(groundDetails.CandidateCount, Is.GreaterThan(0));
            }

            var visualization = Object.FindAnyObjectByType<SteppeSimulationVisualization>();
            Assert.That(visualization, Is.Not.Null);
            Assert.That(visualization.StateCount, Is.EqualTo(StateCatalog.All.Count));
            Assert.That(visualization.FluxCount, Is.EqualTo(FluxCatalog.All.Count));
            Assert.That(visualization.VectorProcessCount, Is.EqualTo(VectorProcessCatalog.All.Count));
            for (var frame = 0; frame < 30 && visualization.NaturalMap == null; frame++)
            {
                yield return null;
            }

            Assert.That(visualization.NaturalMap, Is.Not.Null);
            Assert.That(
                Shader.GetGlobalTexture("_SteppeSimulationNaturalMap"),
                Is.SameAs(visualization.NaturalMap));

            var naturalAtlas = Object.FindAnyObjectByType<SteppeNaturalVisualFieldAtlas>();
            for (var frame = 0; frame < 60 && (naturalAtlas == null || !naturalAtlas.IsReady); frame++)
            {
                yield return null;
                naturalAtlas = Object.FindAnyObjectByType<SteppeNaturalVisualFieldAtlas>();
            }

            Assert.That(naturalAtlas, Is.Not.Null);
            Assert.That(naturalAtlas.IsReady, Is.True);
            Assert.That(naturalAtlas.Width, Is.EqualTo(host.LatestSnapshot.Summary.Width));
            Assert.That(naturalAtlas.Height, Is.EqualTo(host.LatestSnapshot.Summary.Height));
            Assert.That(
                Shader.GetGlobalTexture("_SteppeNaturalSoilAFrom"),
                Is.TypeOf<Texture2D>());
            Assert.That(
                ((Texture2D)Shader.GetGlobalTexture("_SteppeNaturalSoilAFrom")).format,
                Is.EqualTo(TextureFormat.RGBAHalf));
            Assert.That(
                Shader.GetGlobalTexture("_SteppeNaturalDrainageFrom"),
                Is.TypeOf<Texture2D>());
            var drainageGrid = Shader.GetGlobalVector("_SteppeNaturalVisualGrid");
            Assert.That(drainageGrid.x, Is.EqualTo(naturalAtlas.Width));
            Assert.That(drainageGrid.y, Is.EqualTo(naturalAtlas.Height));
            Assert.That(drainageGrid.z, Is.GreaterThan(0f));
            var processAtlas = Object.FindAnyObjectByType<SteppeNaturalProcessFieldAtlas>();
            for (var frame = 0; frame < 60 && (processAtlas == null || !processAtlas.IsReady); frame++)
            {
                yield return null;
                processAtlas = Object.FindAnyObjectByType<SteppeNaturalProcessFieldAtlas>();
            }
            Assert.That(processAtlas, Is.Not.Null);
            Assert.That(processAtlas.IsReady, Is.True);
            Assert.That(processAtlas.FluxCount, Is.EqualTo(56));
            Assert.That(processAtlas.VectorCount, Is.EqualTo(12));
            var hydrologicalVapor =
                Object.FindAnyObjectByType<SteppeHydrologicalVaporPresentation>();
            for (var frame = 0;
                 frame < 60 && (hydrologicalVapor == null || !hydrologicalVapor.IsReady);
                 frame++)
            {
                yield return null;
                hydrologicalVapor =
                    Object.FindAnyObjectByType<SteppeHydrologicalVaporPresentation>();
            }
            Assert.That(hydrologicalVapor, Is.Not.Null);
            Assert.That(hydrologicalVapor.IsReady, Is.True);
            Assert.That(hydrologicalVapor.SublimationParticles, Is.Not.Null);
            Assert.That(hydrologicalVapor.EvaporationParticles, Is.Not.Null);
            Assert.That(hydrologicalVapor.TranspirationParticles, Is.Not.Null);
            Assert.That(hydrologicalVapor.PercolationParticles, Is.Not.Null);
            Assert.That(hydrologicalVapor.CurrentSnowSublimationRate, Is.GreaterThanOrEqualTo(0f));
            Assert.That(hydrologicalVapor.CurrentSurfaceEvaporationRate, Is.GreaterThanOrEqualTo(0f));
            Assert.That(hydrologicalVapor.CurrentTranspirationRate, Is.GreaterThanOrEqualTo(0f));
            var atmosphericTransport =
                Object.FindAnyObjectByType<SteppeAtmosphericTransportPresentation>();
            for (var frame = 0;
                 frame < 60 && (atmosphericTransport == null || !atmosphericTransport.IsReady);
                 frame++)
            {
                yield return null;
                atmosphericTransport =
                    Object.FindAnyObjectByType<SteppeAtmosphericTransportPresentation>();
            }
            Assert.That(atmosphericTransport, Is.Not.Null);
            Assert.That(atmosphericTransport.IsReady, Is.True);
            Assert.That(atmosphericTransport.TemperatureParticles, Is.Not.Null);
            Assert.That(atmosphericTransport.HumidityParticles, Is.Not.Null);
            Assert.That(atmosphericTransport.CloudEvaporationParticles, Is.Not.Null);
            Assert.That(
                atmosphericTransport.CurrentAirTemperatureAdvectionGrossMagnitude,
                Is.GreaterThanOrEqualTo(0f));
            Assert.That(
                atmosphericTransport.CurrentHumidityAdvectionGrossMagnitude,
                Is.GreaterThanOrEqualTo(0f));
            Assert.That(
                Shader.GetGlobalTexture("_SteppeNaturalFlux00From"),
                Is.TypeOf<Texture2D>());
            Assert.That(
                Shader.GetGlobalTexture("_SteppeNaturalVectorSurfaceRunoffFrom"),
                Is.TypeOf<Texture2D>());
            var previousSnapshot = host.PreviousSnapshot ?? host.LatestSnapshot;
            Assert.That(previousSnapshot.TryGetLayer(SimulationLayer.Permeability, out var permeability), Is.True);
            Assert.That(previousSnapshot.TryGetLayer(SimulationLayer.MineralContent, out var mineral), Is.True);
            Assert.That(previousSnapshot.TryGetLayer(SimulationLayer.SoilCompaction, out var compaction), Is.True);
            Assert.That(previousSnapshot.TryGetLayer(SimulationLayer.RootWater, out var rootWater), Is.True);
            var packedSoil = ((Texture2D)Shader.GetGlobalTexture("_SteppeNaturalSoilBFrom"))
                .GetPixel(0, 0);
            Assert.That(packedSoil.r, Is.EqualTo(permeability.Values[0]).Within(0.02f));
            Assert.That(packedSoil.g, Is.EqualTo(mineral.Values[0]).Within(0.002f));
            Assert.That(packedSoil.b, Is.EqualTo(compaction.Values[0]).Within(0.002f));
            Assert.That(packedSoil.a, Is.EqualTo(rootWater.Values[0]).Within(0.08f));
            Assert.That(previousSnapshot.TryGetLayer(SimulationLayer.FrozenSoil, out var frozen), Is.True);
            Assert.That(previousSnapshot.TryGetLayer(SimulationLayer.LiveBiomass, out var liveBiomass), Is.True);
            Assert.That(previousSnapshot.TryGetLayer(SimulationLayer.DryBiomass, out var dryBiomass), Is.True);
            Assert.That(previousSnapshot.TryGetLayer(SimulationLayer.LitterBiomass, out var litter), Is.True);
            Assert.That(previousSnapshot.TryGetLayer(SimulationLayer.PlantNitrogen, out var plantNitrogen), Is.True);
            var packedLifeA = ((Texture2D)Shader.GetGlobalTexture("_SteppeNaturalLifeAFrom"))
                .GetPixel(0, 0);
            var packedLifeB = ((Texture2D)Shader.GetGlobalTexture("_SteppeNaturalLifeBFrom"))
                .GetPixel(0, 0);
            Assert.That(packedLifeA.r, Is.EqualTo(frozen.Values[0]).Within(0.002f));
            Assert.That(packedLifeA.g, Is.EqualTo(liveBiomass.Values[0]).Within(0.08f));
            Assert.That(packedLifeA.b, Is.EqualTo(dryBiomass.Values[0]).Within(0.08f));
            Assert.That(packedLifeA.a, Is.EqualTo(litter.Values[0]).Within(0.08f));
            Assert.That(packedLifeB.a, Is.EqualTo(plantNitrogen.Values[0]).Within(0.01f));
            Assert.That(
                Shader.GetGlobalFloat("_SteppeNaturalVisualBlend"),
                Is.InRange(0f, 1f));
            visualization.SetVisible(true);
            visualization.SetSelection(
                SteppeVisualizationKind.State,
                StateCatalog.All.Count - 1);
            yield return null;
            visualization.SetSelection(
                SteppeVisualizationKind.Flux,
                FluxCatalog.All.Count - 1);
            yield return null;
            visualization.SetSelection(
                SteppeVisualizationKind.VectorProcess,
                VectorProcessCatalog.All.Count - 1);
            yield return null;
            Assert.That(visualization.DiagnosticMap, Is.Not.Null);
            Assert.That(
                Shader.GetGlobalTexture("_SteppeSimulationDiagnosticMap"),
                Is.SameAs(visualization.DiagnosticMap));
            visualization.SetVisible(false);
            foreach (var descriptor in StateCatalog.All)
            {
                Assert.That(host.LatestSnapshot.TryGetLayer(descriptor.Id, out _), Is.True, descriptor.Title);
            }

            foreach (var descriptor in FluxCatalog.All)
            {
                Assert.That(host.LatestSnapshot.TryGetFlux(descriptor.Id, out _), Is.True, descriptor.Title);
            }

            foreach (var descriptor in VectorProcessCatalog.All)
            {
                Assert.That(
                    host.LatestSnapshot.TryGetVectorProcess(descriptor.Process, out _),
                    Is.True,
                    descriptor.Title);
            }
        }

        [UnityTest, Ignore("Caravan runtime is intentionally disabled.")]
        public IEnumerator BuildModeManuallyLaysAndRemovesElectricalCommunications()
        {
            if (Object.FindAnyObjectByType<SteppePrototypeBootstrap>() == null)
            {
                new GameObject("Caravan Build Test Bootstrap").AddComponent<SteppePrototypeBootstrap>();
            }

            yield return null;
            var caravan = Object.FindAnyObjectByType<CaravanChassisController>();
            var network = Object.FindAnyObjectByType<CaravanElectricalNetwork>();
            var build = Object.FindAnyObjectByType<CaravanBuildModeController>();
            Assert.That(caravan, Is.Not.Null);
            Assert.That(network, Is.Not.Null);
            Assert.That(build, Is.Not.Null);

            caravan.Body.linearVelocity = Vector3.zero;
            network.ClearConnections();
            Assert.That(build.TryEnterBuildMode(), Is.True);
            Assert.That(
                build.SetInteractionMode(CaravanBuildInteractionMode.Communications),
                Is.True);
            Assert.That(build.IsCommunicationMode, Is.True);
            Assert.That(network.PhotovoltaicPort.IsBuildMarkerVisible, Is.True);
            Assert.That(network.BatteryPort.IsBuildMarkerVisible, Is.True);
            Assert.That(network.MotorPort.IsBuildMarkerVisible, Is.True);
            var previewObject = GameObject.Find("Communication Cable Preview");
            Assert.That(previewObject, Is.Not.Null);
            var previewLine = previewObject.GetComponent<LineRenderer>();
            Assert.That(previewLine, Is.Not.Null);

            Assert.That(
                build.TrySelectCommunicationPort(network.PhotovoltaicPort),
                Is.True);
            Assert.That(previewLine.enabled, Is.True);
            Assert.That(
                build.SelectedCommunicationPort,
                Is.SameAs(network.PhotovoltaicPort));
            Assert.That(
                build.TrySelectCommunicationPort(network.MotorPort),
                Is.False,
                "A generator must not connect directly to a consumer.");
            Assert.That(network.CableCount, Is.Zero);
            Assert.That(
                build.TrySelectCommunicationPort(network.PhotovoltaicPort),
                Is.True,
                "Clicking the selected port should cancel the pending cable.");
            Assert.That(build.SelectedCommunicationPort, Is.Null);
            Assert.That(previewLine.enabled, Is.False);

            Assert.That(
                build.TrySelectCommunicationPort(network.PhotovoltaicPort),
                Is.True);
            Assert.That(
                build.TrySelectCommunicationPort(network.BatteryPort),
                Is.True);
            Assert.That(previewLine.enabled, Is.False);
            Assert.That(network.CableCount, Is.EqualTo(1));
            Assert.That(network.IsClosed, Is.False);

            Assert.That(
                build.TrySelectCommunicationPort(network.BatteryPort),
                Is.True);
            Assert.That(
                build.TrySelectCommunicationPort(network.MotorPort),
                Is.True);
            Assert.That(network.CableCount, Is.EqualTo(2));
            Assert.That(network.IsClosed, Is.True);
            foreach (var cable in network.Cables)
            {
                Assert.That(cable.GetComponentsInChildren<LineRenderer>(true), Has.Length.EqualTo(2));
                Assert.That(cable.GetComponentInChildren<Collider>(true), Is.Null);
                Assert.That(cable.GetComponentInChildren<Rigidbody>(true), Is.Null);
            }

            Assert.That(
                build.RemoveCommunicationConnections(network.MotorPort),
                Is.EqualTo(1));
            Assert.That(network.CableCount, Is.EqualTo(1));
            Assert.That(network.IsClosed, Is.False);
            Assert.That(
                build.TrySelectCommunicationPort(network.BatteryPort),
                Is.True);
            Assert.That(
                build.TrySelectCommunicationPort(network.MotorPort),
                Is.True);
            Assert.That(network.IsClosed, Is.True);
            build.ExitBuildMode();
            Assert.That(network.PhotovoltaicPort.IsBuildMarkerVisible, Is.False);
            yield return null;
        }

        [UnityTest, Ignore("Caravan runtime is intentionally disabled.")]
        public IEnumerator BuildModeDismantlesModuleAndCleansItsNetworks()
        {
            if (Object.FindAnyObjectByType<SteppePrototypeBootstrap>() == null)
            {
                new GameObject("Caravan Dismantling Test Bootstrap")
                    .AddComponent<SteppePrototypeBootstrap>();
            }

            yield return null;
            var caravan = Object.FindAnyObjectByType<CaravanChassisController>();
            var grid = Object.FindAnyObjectByType<CaravanMountGrid>();
            var build = Object.FindAnyObjectByType<CaravanBuildModeController>();
            var electrical = Object.FindAnyObjectByType<CaravanElectricalNetwork>();
            var resource = Object.FindAnyObjectByType<CaravanResourceSystem>();
            var materialNetworks =
                Object.FindObjectsByType<CaravanMaterialNetwork>(
                    FindObjectsInactive.Include);
            var biomass = System.Array.Find(
                materialNetworks,
                item => item.Kind == CaravanMaterialNetworkKind.Biomass);
            var mechanical = System.Array.Find(
                materialNetworks,
                item => item.Kind == CaravanMaterialNetworkKind.Mechanical);
            Assert.That(caravan, Is.Not.Null);
            Assert.That(grid, Is.Not.Null);
            Assert.That(build, Is.Not.Null);
            Assert.That(electrical, Is.Not.Null);
            Assert.That(resource, Is.Not.Null);
            Assert.That(biomass, Is.Not.Null);
            Assert.That(mechanical, Is.Not.Null);

            electrical.ClearConnections();
            var startingMass = caravan.Body.mass;
            var startingElectricalPorts = electrical.Ports.Count;
            var startingBiomassPorts = biomass.Ports.Count;
            var startingMechanicalPorts = mechanical.Ports.Count;
            var startingHarvesterCount = resource.Harvesters.Count;
            caravan.Body.linearVelocity = Vector3.zero;
            Assert.That(build.TryEnterBuildMode(), Is.True);
            Assert.That(
                build.TryCreateModule(CaravanPartKind.Harvester),
                Is.True);
            var module = build.HeldModule;
            Assert.That(
                TryFindFreePlacement(grid, module, out var placement),
                Is.True);
            Assert.That(build.TryPlaceHeldModule(placement), Is.True);
            var harvester = module.GetComponent<CaravanHarvesterModule>();
            var harvesterPort = module.GetComponentInChildren<
                CaravanElectricalPort>(true);
            Assert.That(
                resource.Harvesters,
                Has.Count.EqualTo(startingHarvesterCount + 1));
            Assert.That(electrical.Ports.Count, Is.EqualTo(startingElectricalPorts + 1));
            Assert.That(biomass.Ports.Count, Is.EqualTo(startingBiomassPorts + 1));
            Assert.That(mechanical.Ports.Count, Is.EqualTo(startingMechanicalPorts + 1));
            Assert.That(
                electrical.TryConnect(electrical.BatteryPort, harvesterPort),
                Is.True);
            Assert.That(electrical.CableCount, Is.EqualTo(1));
            Assert.That(caravan.Body.mass, Is.GreaterThan(startingMass));

            Assert.That(build.TryRemoveModule(module), Is.True);
            Assert.That(grid.CanPlace(module, placement), Is.True);
            Assert.That(electrical.CableCount, Is.Zero);
            Assert.That(electrical.Ports.Count, Is.EqualTo(startingElectricalPorts));
            Assert.That(biomass.Ports.Count, Is.EqualTo(startingBiomassPorts));
            Assert.That(mechanical.Ports.Count, Is.EqualTo(startingMechanicalPorts));
            Assert.That(
                resource.Harvesters,
                Has.Count.EqualTo(startingHarvesterCount));
            Assert.That(caravan.Body.mass, Is.EqualTo(startingMass).Within(0.1f));
            Assert.That(harvester, Is.Not.Null);
            yield return null;
            Assert.That(module == null, Is.True);
            build.ExitBuildMode();
        }

        [UnityTest, Ignore("Caravan runtime is intentionally disabled.")]
        public IEnumerator SteeringSurvivesRemovingAndRebuildingElectricMotor()
        {
            if (Object.FindAnyObjectByType<SteppePrototypeBootstrap>() == null)
            {
                new GameObject("Motor Rebuild Steering Test Bootstrap")
                    .AddComponent<SteppePrototypeBootstrap>();
            }

            yield return null;
            var caravan = Object.FindAnyObjectByType<CaravanChassisController>();
            var build = Object.FindAnyObjectByType<CaravanBuildModeController>();
            var interactor = Object.FindAnyObjectByType<CaravanPlayerInteractor>();
            var originalMotorModule = System.Array.Find(
                Object.FindObjectsByType<CaravanModule>(
                    FindObjectsInactive.Exclude),
                module => module.InstanceId == "starter-electric-motor");
            var originalMotor = originalMotorModule != null
                ? originalMotorModule.GetComponent<CaravanPart>()
                : null;
            var steering = System.Array.Find(
                Object.FindObjectsByType<CaravanControlStation>(
                    FindObjectsInactive.Include),
                station => station.Kind == CaravanControlKind.Steering);
            Assert.That(caravan, Is.Not.Null);
            Assert.That(build, Is.Not.Null);
            Assert.That(interactor, Is.Not.Null);
            Assert.That(originalMotor, Is.Not.Null);
            Assert.That(steering, Is.Not.Null);
            var steeringVisual = steering.transform.Find("Control Visual");
            Assert.That(steeringVisual, Is.Not.Null);
            var startingMotorCount = caravan.ElectricMotors.Count;

            caravan.Body.linearVelocity = Vector3.zero;
            Assert.That(build.TryEnterBuildMode(), Is.True);
            Assert.That(
                build.TryRemoveModule(
                    originalMotor.GetComponent<CaravanModule>()),
                Is.True);
            Assert.That(
                caravan.ElectricMotors,
                Has.Count.EqualTo(startingMotorCount - 1));
            Assert.That(
                build.TryCreateModule(CaravanPartKind.ElectricMotor),
                Is.True);
            var rebuiltMotor = build.HeldModule;
            Assert.That(
                build.TryPlaceHeldModule(
                    new CaravanGridPlacement(2, 4, 2, 2, 0)),
                Is.True);
            Assert.That(
                caravan.ElectricMotors,
                Has.Count.EqualTo(startingMotorCount));
            var rebuiltThrottle = rebuiltMotor.GetComponent<
                CaravanControlStation>();
            Assert.That(rebuiltThrottle, Is.Not.Null);
            Assert.That(
                rebuiltThrottle.Kind,
                Is.EqualTo(CaravanControlKind.ElectricThrottle));

            build.ExitBuildMode();
            Assert.That(build.IsActive, Is.False);
            Assert.That(
                interactor.TryBeginControl(steering),
                Is.True);
            Assert.That(
                interactor.AdjustActiveControl(0.55f),
                Is.True);
            Assert.That(caravan.SteeringNormalized, Is.EqualTo(0.55f));
            Assert.That(
                caravan.AppliedSteeringNormalized,
                Is.EqualTo(0.55f));
            Assert.That(
                Quaternion.Angle(
                    Quaternion.identity,
                    steeringVisual.localRotation),
                Is.GreaterThan(1f),
                "The physical steering wheel stopped animating after the motor was rebuilt.");
            interactor.EndControl();
            steering.SetNormalized(0f);
            yield return null;
        }

        [UnityTest, Ignore("Caravan runtime is intentionally disabled.")]
        public IEnumerator BuildModeConstructsAndPipesPoweredWaterCoolingLoop()
        {
            if (Object.FindAnyObjectByType<SteppePrototypeBootstrap>() == null)
            {
                new GameObject("Water Circuit Test Bootstrap")
                    .AddComponent<SteppePrototypeBootstrap>();
            }

            yield return null;
            var caravan = Object.FindAnyObjectByType<CaravanChassisController>();
            var electrical = Object.FindAnyObjectByType<CaravanElectricalNetwork>();
            var fluids = Object.FindAnyObjectByType<CaravanFluidNetwork>();
            var construction =
                Object.FindAnyObjectByType<CaravanConstructionService>();
            var build = Object.FindAnyObjectByType<CaravanBuildModeController>();
            Assert.That(caravan, Is.Not.Null);
            Assert.That(electrical, Is.Not.Null);
            Assert.That(fluids, Is.Not.Null);
            Assert.That(construction, Is.Not.Null);
            Assert.That(build, Is.Not.Null);
            Assert.That(fluids.Ports, Has.Count.EqualTo(1));
            Assert.That(fluids.Pipes, Is.Empty);
            Assert.That(
                Object.FindAnyObjectByType<CaravanWaterReservoirModule>(),
                Is.Null);

            caravan.Body.linearVelocity = Vector3.zero;
            electrical.ClearConnections();
            Assert.That(
                electrical.TryConnect(
                    electrical.PhotovoltaicPort,
                    electrical.BatteryPort),
                Is.True);
            Assert.That(
                electrical.TryConnect(
                    electrical.BatteryPort,
                    electrical.MotorPort),
                Is.True);
            Assert.That(
                electrical.BatteryPort.ConnectedCableCount,
                Is.EqualTo(2));
            Assert.That(electrical.BatteryPort.IsAtCapacity, Is.False);
            var startingMass = caravan.Body.mass;
            Assert.That(build.TryEnterBuildMode(), Is.True);

            Assert.That(
                build.TryCreateModule(CaravanPartKind.WaterReservoir),
                Is.True);
            Assert.That(
                build.TryPlaceHeldModule(
                    new CaravanGridPlacement(4, 8, 2, 3, 0)),
                Is.True);
            Assert.That(
                build.TryCreateModule(CaravanPartKind.DualModePump),
                Is.True);
            Assert.That(
                build.TryPlaceHeldModule(
                    new CaravanGridPlacement(7, 8, 1, 2, 0)),
                Is.True);
            Assert.That(
                build.TryCreateModule(CaravanPartKind.Radiator),
                Is.True);
            Assert.That(
                build.TryPlaceHeldModule(
                    new CaravanGridPlacement(4, 12, 2, 2, 0)),
                Is.True);

            Assert.That(
                Object.FindObjectsByType<CaravanPart>().Length,
                Is.EqualTo(6));
            Assert.That(fluids.Ports, Has.Count.EqualTo(7));
            Assert.That(electrical.Ports, Has.Count.EqualTo(4));
            Assert.That(caravan.Body.mass, Is.GreaterThan(startingMass + 1400f));
            Assert.That(fluids.Reservoir, Is.Not.Null);
            Assert.That(fluids.Pump, Is.Not.Null);
            Assert.That(fluids.Radiator, Is.Not.Null);

            Assert.That(
                build.SetInteractionMode(CaravanBuildInteractionMode.Fluids),
                Is.True);
            Assert.That(fluids.ReservoirSupplyPort.IsBuildMarkerVisible, Is.True);
            Assert.That(
                build.TrySelectFluidPort(fluids.ReservoirSupplyPort),
                Is.True);
            Assert.That(
                build.TrySelectFluidPort(fluids.RadiatorInletPort),
                Is.False,
                "A reservoir supply may only connect to the pump inlet.");
            Assert.That(fluids.PipeCount, Is.Zero);
            Assert.That(
                build.TrySelectFluidPort(fluids.PumpInletPort),
                Is.True);
            Assert.That(
                build.TrySelectFluidPort(fluids.PumpOutletPort),
                Is.True);
            Assert.That(
                build.TrySelectFluidPort(fluids.RadiatorInletPort),
                Is.True);
            Assert.That(
                build.TrySelectFluidPort(fluids.RadiatorOutletPort),
                Is.True);
            Assert.That(
                build.TrySelectFluidPort(fluids.ReservoirReturnPort),
                Is.True);
            Assert.That(fluids.IsClosed, Is.True);
            Assert.That(fluids.PipeCount, Is.EqualTo(3));
            foreach (var pipe in fluids.Pipes)
            {
                Assert.That(pipe.GetComponent<LineRenderer>(), Is.Not.Null);
                Assert.That(pipe.GetComponentInChildren<Collider>(true), Is.Null);
                Assert.That(pipe.GetComponentInChildren<Rigidbody>(true), Is.Null);
            }

            Assert.That(
                build.SetInteractionMode(CaravanBuildInteractionMode.Electrical),
                Is.True);
            var pumpElectricalPort = System.Array.Find(
                Object.FindObjectsByType<CaravanElectricalPort>(
                    FindObjectsInactive.Include),
                port => port.Part.Kind == CaravanPartKind.DualModePump);
            Assert.That(pumpElectricalPort, Is.Not.Null);
            Assert.That(
                build.TrySelectCommunicationPort(electrical.BatteryPort),
                Is.True);
            Assert.That(
                build.TrySelectCommunicationPort(pumpElectricalPort),
                Is.True);
            Assert.That(electrical.CableCount, Is.EqualTo(3));
            Assert.That(
                electrical.BatteryPort.ConnectedCableCount,
                Is.EqualTo(3));
            Assert.That(electrical.BatteryPort.IsAtCapacity, Is.False);
            Assert.That(electrical.IsClosed, Is.True);

            fluids.Reservoir.SetTemperature(70f);
            fluids.Simulate(1f);
            Assert.That(fluids.Pump.RequestedPowerKilowatts, Is.GreaterThan(0f));
            electrical.Simulate(1f);
            var temperatureBeforeCooling =
                fluids.Reservoir.TemperatureCelsius;
            fluids.Simulate(60f);

            Assert.That(
                fluids.Pump.DeliveredElectricalKilowatts,
                Is.GreaterThan(0f));
            Assert.That(fluids.CurrentFlowLitresPerSecond, Is.GreaterThan(0f));
            Assert.That(fluids.CurrentCoolingKilowatts, Is.GreaterThan(0f));
            Assert.That(
                fluids.Reservoir.TemperatureCelsius,
                Is.LessThan(temperatureBeforeCooling));
            foreach (var pipe in fluids.Pipes)
            {
                Assert.That(pipe.FlowLitresPerSecond, Is.GreaterThan(0f));
            }

            Assert.That(
                build.SetInteractionMode(CaravanBuildInteractionMode.Fluids),
                Is.True);
            Assert.That(
                build.RemoveFluidConnections(fluids.RadiatorInletPort),
                Is.EqualTo(1));
            Assert.That(fluids.PipeCount, Is.EqualTo(2));
            Assert.That(fluids.IsClosed, Is.False);
            Assert.That(
                build.TrySelectFluidPort(fluids.PumpOutletPort),
                Is.True);
            Assert.That(
                build.TrySelectFluidPort(fluids.RadiatorInletPort),
                Is.True);
            Assert.That(fluids.IsClosed, Is.True);

            build.ExitBuildMode();
            foreach (var port in fluids.Ports)
            {
                Assert.That(port.IsBuildMarkerVisible, Is.False);
            }
            yield return null;
        }

        [UnityTest, Ignore("Caravan runtime is intentionally disabled.")]
        public IEnumerator EveryCatalogModuleConstructsAndResourceChainsOperate()
        {
            if (Object.FindAnyObjectByType<SteppePrototypeBootstrap>() == null)
            {
                new GameObject("Complete Caravan Module Test Bootstrap")
                    .AddComponent<SteppePrototypeBootstrap>();
            }

            yield return null;
            yield return null;
            var caravan = Object.FindAnyObjectByType<CaravanChassisController>();
            var grid = Object.FindAnyObjectByType<CaravanMountGrid>();
            var build = Object.FindAnyObjectByType<CaravanBuildModeController>();
            var electrical = Object.FindAnyObjectByType<CaravanElectricalNetwork>();
            var fluids = Object.FindAnyObjectByType<CaravanFluidNetwork>();
            var resource = Object.FindAnyObjectByType<CaravanResourceSystem>();
            var materialNetworks =
                Object.FindObjectsByType<CaravanMaterialNetwork>(
                    FindObjectsInactive.Include);
            var biomass = System.Array.Find(
                materialNetworks,
                item => item.Kind == CaravanMaterialNetworkKind.Biomass);
            var mechanical = System.Array.Find(
                materialNetworks,
                item => item.Kind == CaravanMaterialNetworkKind.Mechanical);
            Assert.That(caravan, Is.Not.Null);
            Assert.That(grid, Is.Not.Null);
            Assert.That(build, Is.Not.Null);
            Assert.That(electrical, Is.Not.Null);
            Assert.That(fluids, Is.Not.Null);
            Assert.That(resource, Is.Not.Null);
            Assert.That(biomass, Is.Not.Null);
            Assert.That(mechanical, Is.Not.Null);

            var floatingOrigin = Object.FindAnyObjectByType<FloatingOriginSystem>();
            caravan.Teleport(floatingOrigin.WorldToLocal(
                32d,
                caravan.transform.position.y,
                -64d));
            for (var resetFrame = 0; resetFrame < 3; resetFrame++)
            {
                yield return null;
            }

            var startingPartCount = Object.FindObjectsByType<CaravanPart>(
                FindObjectsInactive.Exclude).Length;
            var startingElectricalPortCount = electrical.Ports.Count;
            var startingFluidPortCount = fluids.Ports.Count;
            var startingBiomassPortCount = biomass.Ports.Count;
            var startingMechanicalPortCount = mechanical.Ports.Count;
            var startingControlCount =
                Object.FindObjectsByType<CaravanControlStation>(
                    FindObjectsInactive.Include).Length;
            caravan.Body.linearVelocity = Vector3.zero;
            Assert.That(build.TryEnterBuildMode(), Is.True);
            foreach (var kind in CaravanConstructionService.AvailablePartKinds)
            {
                Assert.That(
                    build.TryCreateModule(kind),
                    Is.True,
                    $"Could not create the {kind} blueprint.");
                Assert.That(
                    TryFindFreePlacement(
                        grid,
                        build.HeldModule,
                        out var placement),
                    Is.True,
                    $"No free mount-grid placement remained for {kind}.");
                Assert.That(
                    build.TryPlaceHeldModule(placement),
                    Is.True,
                    $"Could not place the {kind} module.");
            }

            var parts = Object.FindObjectsByType<CaravanPart>(
                FindObjectsInactive.Exclude);
            Assert.That(
                parts,
                Has.Length.EqualTo(
                    startingPartCount
                    + CaravanConstructionService.AvailablePartKinds.Count));
            foreach (CaravanPartKind kind in
                     System.Enum.GetValues(typeof(CaravanPartKind)))
            {
                Assert.That(
                    System.Array.Exists(parts, part => part.Kind == kind),
                    Is.True,
                    $"The constructed caravan is missing {kind}.");
            }

            Assert.That(
                electrical.Ports.Count,
                Is.EqualTo(startingElectricalPortCount + 6));
            Assert.That(
                fluids.Ports.Count,
                Is.EqualTo(startingFluidPortCount + 10));
            Assert.That(
                biomass.Ports.Count,
                Is.EqualTo(startingBiomassPortCount + 7));
            Assert.That(
                mechanical.Ports.Count,
                Is.EqualTo(startingMechanicalPortCount + 7));
            Assert.That(resource.Harvesters, Has.Count.EqualTo(1));
            Assert.That(resource.Dryers, Has.Count.EqualTo(1));
            Assert.That(resource.Storages, Has.Count.EqualTo(1));
            Assert.That(resource.Furnaces, Has.Count.EqualTo(1));
            Assert.That(resource.Engines, Has.Count.EqualTo(1));
            Assert.That(
                Object.FindObjectsByType<CaravanControlStation>(
                    FindObjectsInactive.Include),
                Has.Length.EqualTo(startingControlCount + 10));

            var harvesterPart = FindPart(parts, CaravanPartKind.Harvester);
            var dryerPart = FindPart(parts, CaravanPartKind.GrassDryer);
            var storagePart = FindPart(parts, CaravanPartKind.BiomassStorage);
            var furnacePart = FindPart(parts, CaravanPartKind.Biofurnace);
            var enginePart = FindPart(parts, CaravanPartKind.BiofuelEngine);
            var pumpPart = FindPart(parts, CaravanPartKind.DualModePump);
            var transmissionPart = FindPart(parts, CaravanPartKind.Transmission);

            Assert.That(
                build.SetInteractionMode(CaravanBuildInteractionMode.Biomass),
                Is.True);
            AssertMaterialConnection(
                build,
                biomass,
                harvesterPart,
                CaravanMaterialPortRole.WetBiomassOutput,
                dryerPart,
                CaravanMaterialPortRole.WetBiomassInput);
            AssertMaterialConnection(
                build,
                biomass,
                dryerPart,
                CaravanMaterialPortRole.DryBiomassOutput,
                storagePart,
                CaravanMaterialPortRole.DryBiomassInput);
            AssertMaterialConnection(
                build,
                biomass,
                storagePart,
                CaravanMaterialPortRole.DryBiomassOutput,
                furnacePart,
                CaravanMaterialPortRole.DryBiomassInput);
            AssertMaterialConnection(
                build,
                biomass,
                storagePart,
                CaravanMaterialPortRole.DryBiomassOutput,
                enginePart,
                CaravanMaterialPortRole.DryBiomassInput);

            Assert.That(
                build.SetInteractionMode(CaravanBuildInteractionMode.Mechanical),
                Is.True);
            AssertMaterialConnection(
                build,
                mechanical,
                enginePart,
                CaravanMaterialPortRole.MechanicalSource,
                transmissionPart,
                CaravanMaterialPortRole.TransmissionInput);
            AssertMaterialConnection(
                build,
                mechanical,
                transmissionPart,
                CaravanMaterialPortRole.TransmissionOutput,
                pumpPart,
                CaravanMaterialPortRole.MechanicalConsumer);
            AssertMaterialConnection(
                build,
                mechanical,
                transmissionPart,
                CaravanMaterialPortRole.TransmissionOutput,
                harvesterPart,
                CaravanMaterialPortRole.MechanicalConsumer);

            var batteryPort = System.Array.Find(
                Object.FindObjectsByType<CaravanElectricalPort>(
                    FindObjectsInactive.Include),
                port => port.Part.Kind == CaravanPartKind.Battery
                        && port.ConnectedCableCount == 0);
            var harvesterElectrical = FindElectricalPort(
                electrical,
                CaravanPartKind.Harvester);
            var dryerElectrical = FindElectricalPort(
                electrical,
                CaravanPartKind.GrassDryer);
            Assert.That(
                electrical.TryConnect(batteryPort, harvesterElectrical),
                Is.True);
            Assert.That(
                electrical.TryConnect(batteryPort, dryerElectrical),
                Is.True);

            AssertFluidConnection(
                fluids,
                fluids.ReservoirSupplyPort,
                fluids.PumpInletPort);
            AssertFluidConnection(
                fluids,
                fluids.PumpOutletPort,
                fluids.RadiatorInletPort);
            AssertFluidConnection(
                fluids,
                fluids.RadiatorOutletPort,
                fluids.ReservoirReturnPort);
            Assert.That(
                fluids.TryConnect(
                    FindFluidPort(fluids, furnacePart),
                    fluids.ReservoirReturnPort),
                Is.True);
            Assert.That(
                fluids.TryConnect(
                    FindFluidPort(fluids, enginePart),
                    fluids.ReservoirReturnPort),
                Is.True);

            var harvester =
                harvesterPart.GetComponent<CaravanHarvesterModule>();
            var dryer = dryerPart.GetComponent<CaravanGrassDryerModule>();
            var furnace =
                furnacePart.GetComponent<CaravanBiofurnaceModule>();
            var engine =
                enginePart.GetComponent<CaravanBiofuelEngineModule>();
            var pump = pumpPart.GetComponent<CaravanElectricPumpModule>();
            harvester.SetControlNormalized(1f);
            dryer.SetControlNormalized(1f);
            furnace.SetControlNormalized(1f);
            engine.SetControlNormalized(1f);
            engine.SetRequestedThrottle(1f);
            pump.SetMode(CaravanPumpMode.Circulation);
            caravan.Body.linearVelocity = caravan.transform.forward * 2f;

            var storedFuelBefore =
                resource.Storages[0].StoredDryBiomassKilograms;
            for (var simulationPass = 0;
                 simulationPass < 180 && harvester.TotalHarvestedKilograms <= 0f;
                 simulationPass++)
            {
                electrical.Simulate(1f);
                resource.Simulate(0.5f);
                yield return null;
            }
            electrical.Simulate(1f);
            resource.Simulate(4f);
            yield return null;
            electrical.Simulate(1f);
            resource.Simulate(2f);
            yield return null;
            fluids.Simulate(2f);

            var extractionHost = Object.FindAnyObjectByType<SteppeSimulationHost>();
            var harvesterWorld = floatingOrigin.LocalToWorld(harvester.transform.position);
            var harvesterInsideFiniteWorld = extractionHost.TryGetEnvironmentSample(
                harvesterWorld.X,
                harvesterWorld.Z,
                out _);
            Assert.That(
                harvester.TotalHarvestedKilograms,
                Is.GreaterThan(0f),
                $"power={harvester.PowerAvailability:F3}, operating={harvester.OperatingLevel:F3}, "
                + $"hostReady={extractionHost.IsReady}, world={harvesterWorld.X:F1},{harvesterWorld.Z:F1}, "
                + $"inside={harvesterInsideFiniteWorld}, "
                + $"processed={extractionHost.ProcessedExtractionCommandCount}, "
                + $"succeeded={extractionHost.SuccessfulExtractionCommandCount}, "
                + $"pending={extractionHost.PendingExtractionCommandCount}, "
                + $"ready={extractionHost.ReadyExtractionResultCount}");
            Assert.That(
                dryer.TotalDriedWetKilograms,
                Is.GreaterThan(0f));
            Assert.That(
                harvester.TotalHarvestedKilograms,
                Is.GreaterThan(0f));
            Assert.That(
                dryer.TotalDriedWetKilograms,
                Is.GreaterThan(0f));
            Assert.That(
                furnace.TotalFuelConsumedKilograms,
                Is.GreaterThan(0f));
            Assert.That(
                engine.TotalFuelConsumedKilograms,
                Is.GreaterThan(0f));
            Assert.That(
                resource.Storages[0].StoredDryBiomassKilograms,
                Is.GreaterThanOrEqualTo(storedFuelBefore));
            Assert.That(engine.IsDriveCoupled, Is.True);
            Assert.That(
                transmissionPart
                    .GetComponent<CaravanTransmissionModule>()
                    .IsEngaged,
                Is.True);
            Assert.That(biomass.LinkCount, Is.EqualTo(4));
            Assert.That(mechanical.LinkCount, Is.EqualTo(3));
            foreach (var link in biomass.Links)
            {
                Assert.That(link.GetComponent<LineRenderer>(), Is.Not.Null);
                Assert.That(link.GetComponentInChildren<Collider>(true), Is.Null);
                Assert.That(link.GetComponentInChildren<Rigidbody>(true), Is.Null);
            }

            Assert.That(
                build.SetInteractionMode(CaravanBuildInteractionMode.Biomass),
                Is.True);
            var harvesterWetOutput = FindMaterialPort(
                biomass,
                harvesterPart,
                CaravanMaterialPortRole.WetBiomassOutput);
            Assert.That(
                build.RemoveMaterialConnections(harvesterWetOutput),
                Is.EqualTo(1));
            Assert.That(biomass.LinkCount, Is.EqualTo(3));
            AssertMaterialConnection(
                build,
                biomass,
                harvesterPart,
                CaravanMaterialPortRole.WetBiomassOutput,
                dryerPart,
                CaravanMaterialPortRole.WetBiomassInput);

            Assert.That(
                build.SetInteractionMode(CaravanBuildInteractionMode.Mechanical),
                Is.True);
            var harvesterMechanicalInput = FindMaterialPort(
                mechanical,
                harvesterPart,
                CaravanMaterialPortRole.MechanicalConsumer);
            Assert.That(
                build.RemoveMaterialConnections(harvesterMechanicalInput),
                Is.EqualTo(1));
            Assert.That(mechanical.LinkCount, Is.EqualTo(2));
            AssertMaterialConnection(
                build,
                mechanical,
                transmissionPart,
                CaravanMaterialPortRole.TransmissionOutput,
                harvesterPart,
                CaravanMaterialPortRole.MechanicalConsumer);

            build.ExitBuildMode();
            yield return null;
        }

        [UnityTest, Ignore("Caravan runtime is intentionally disabled.")]
        public IEnumerator ElectricalNetworkRegistersAdditionalStorageAndConsumersAtRuntime()
        {
            if (Object.FindAnyObjectByType<SteppePrototypeBootstrap>() == null)
            {
                new GameObject("Dynamic Electrical Test Bootstrap")
                    .AddComponent<SteppePrototypeBootstrap>();
            }

            yield return null;
            var caravan = Object.FindAnyObjectByType<CaravanChassisController>();
            var network = Object.FindAnyObjectByType<CaravanElectricalNetwork>();
            Assert.That(caravan, Is.Not.Null);
            Assert.That(network, Is.Not.Null);

            var batteryObject = new GameObject("Test Auxiliary Battery");
            batteryObject.transform.SetParent(caravan.transform, false);
            var batteryModule = batteryObject.AddComponent<CaravanModule>();
            batteryModule.Configure(
                "battery",
                batteryObject.transform,
                true,
                1,
                1,
                instanceId: "test-auxiliary-battery");
            var batteryPart = batteryObject.AddComponent<CaravanPart>();
            batteryPart.Configure(CaravanPartKind.Battery, 40f, 20f);
            var auxiliaryBattery = batteryObject.AddComponent<CaravanBatteryModule>();
            auxiliaryBattery.Configure(null, 12f, 20f);
            var batteryPort = batteryObject.AddComponent<CaravanElectricalPort>();
            batteryPort.Configure(
                batteryPart,
                CaravanElectricalPortKind.Storage,
                connectionCapacity: 4,
                portId: "test-auxiliary-battery:electrical");

            var motorObject = new GameObject("Test Auxiliary Motor");
            motorObject.transform.SetParent(caravan.transform, false);
            var motorModule = motorObject.AddComponent<CaravanModule>();
            motorModule.Configure(
                "electric-motor",
                motorObject.transform,
                true,
                1,
                1,
                instanceId: "test-auxiliary-motor");
            var motorPart = motorObject.AddComponent<CaravanPart>();
            motorPart.Configure(CaravanPartKind.ElectricMotor, 10f);
            var auxiliaryMotor = motorObject.AddComponent<CaravanElectricMotorModule>();
            auxiliaryMotor.Configure(null);
            var motorPort = motorObject.AddComponent<CaravanElectricalPort>();
            motorPort.Configure(
                motorPart,
                CaravanElectricalPortKind.Consumer,
                portId: "test-auxiliary-motor:electrical");

            Assert.That(network.RegisterPort(batteryPort), Is.True);
            Assert.That(network.RegisterPort(motorPort), Is.True);
            Assert.That(network.Batteries, Has.Count.EqualTo(2));
            Assert.That(network.Motors, Has.Count.EqualTo(2));
            Assert.That(caravan.ElectricMotors, Has.Count.EqualTo(2));
            Assert.That(network.TryConnect(batteryPort, motorPort), Is.True);

            auxiliaryMotor.SetRequestedThrottle(1f);
            var before = auxiliaryBattery.StoredEnergyKilowattHours;
            network.Simulate(60f);

            Assert.That(auxiliaryMotor.DeliveredElectricalKilowatts, Is.EqualTo(10f).Within(0.001f));
            Assert.That(auxiliaryMotor.PowerAvailability, Is.EqualTo(1f).Within(0.001f));
            Assert.That(auxiliaryBattery.StoredEnergyKilowattHours, Is.LessThan(before));
            Assert.That(network.ConnectedComponentCount, Is.GreaterThanOrEqualTo(1));

            Assert.That(network.UnregisterPort(motorPort), Is.True);
            Assert.That(network.UnregisterPort(batteryPort), Is.True);
            Assert.That(caravan.ElectricMotors, Has.Count.EqualTo(1));
            Assert.That(network.Motors, Has.Count.EqualTo(1));
            Object.Destroy(motorObject);
            Object.Destroy(batteryObject);
            yield return null;
        }

        [UnityTest, Ignore("Caravan runtime is intentionally disabled.")]
        public IEnumerator ChassisMaintainsForwardGripDuringSustainedTurn()
        {
            if (Object.FindAnyObjectByType<SteppePrototypeBootstrap>() == null)
            {
                new GameObject("Caravan Handling Test Bootstrap").AddComponent<SteppePrototypeBootstrap>();
            }

            var caravan = Object.FindAnyObjectByType<CaravanChassisController>();
            var battery = Object.FindAnyObjectByType<CaravanBatteryModule>();
            var electricMotor = Object.FindAnyObjectByType<CaravanElectricMotorModule>();
            var network = Object.FindAnyObjectByType<CaravanElectricalNetwork>();
            var steeringStation = System.Array.Find(
                Object.FindObjectsByType<CaravanControlStation>(
                    FindObjectsInactive.Include),
                station => station.Kind == CaravanControlKind.Steering);
            Assert.That(caravan, Is.Not.Null);
            Assert.That(battery, Is.Not.Null);
            Assert.That(electricMotor, Is.Not.Null);
            Assert.That(network, Is.Not.Null);
            Assert.That(steeringStation, Is.Not.Null);
            if (network.PanelToBatteryCable == null)
            {
                Assert.That(
                    network.TryConnect(network.PhotovoltaicPort, network.BatteryPort),
                    Is.True);
            }
            if (network.BatteryToMotorCable == null)
            {
                Assert.That(
                    network.TryConnect(network.BatteryPort, network.MotorPort),
                    Is.True);
            }
            Assert.That(network.IsClosed, Is.True);
            for (var frame = 0; frame < 60 && !caravan.PhysicsStarted; frame++)
            {
                yield return new WaitForFixedUpdate();
            }
            Assert.That(caravan.PhysicsStarted, Is.True);

            caravan.SetDefaultDriveEnabled(true);
            steeringStation.SetNormalized(0.82f);
            caravan.Body.linearVelocity = Vector3.zero;
            caravan.Body.angularVelocity = Vector3.zero;

            var maximumSlipAngle = 0f;
            var maximumSpeed = 0f;
            var maximumYawSpeed = 0f;
            var initialForward = caravan.transform.forward;
            var initialBatteryEnergy = battery.StoredEnergyKilowattHours;
            var peakMotorPower = 0f;
            var accumulatedSlipAngle = 0f;
            var measuredSteps = 0;
            for (var fixedStep = 0; fixedStep < 300; fixedStep++)
            {
                yield return new WaitForFixedUpdate();
                peakMotorPower = Mathf.Max(
                    peakMotorPower,
                    electricMotor.DeliveredMechanicalKilowatts);
                var planarVelocity = Vector3.ProjectOnPlane(caravan.Body.linearVelocity, Vector3.up);
                maximumSpeed = Mathf.Max(maximumSpeed, planarVelocity.magnitude);
                maximumYawSpeed = Mathf.Max(maximumYawSpeed, Mathf.Abs(caravan.Body.angularVelocity.y));
                if (fixedStep < 60 || planarVelocity.magnitude < 0.35f)
                {
                    continue;
                }

                var slipAngle = Vector3.Angle(caravan.transform.forward, planarVelocity);
                maximumSlipAngle = Mathf.Max(maximumSlipAngle, slipAngle);
                accumulatedSlipAngle += slipAngle;
                measuredSteps++;
            }

            steeringStation.SetNormalized(0f);
            Assert.That(
                measuredSteps,
                Is.GreaterThan(30),
                $"The chassis never reached a measurable turning speed. "
                + $"peakSpeed={maximumSpeed:F2}, finalSpeed={caravan.Speed:F2}, "
                + $"drive={caravan.CurrentDriveForce:F1} N, grounded={caravan.IsGrounded}, "
                + $"resistance={caravan.CurrentSurface.Resistance:F2}, "
                + $"peakYaw={maximumYawSpeed * Mathf.Rad2Deg:F1} deg/s, "
                + $"headingDelta={Vector3.Angle(initialForward, caravan.transform.forward):F1} degrees, "
                + $"inertia={caravan.Body.inertiaTensor}.");
            Assert.That(
                accumulatedSlipAngle / measuredSteps,
                Is.LessThan(24f),
                $"The chassis developed persistent side slip. Peak={maximumSlipAngle:F1} degrees.");
            Assert.That(
                maximumSlipAngle,
                Is.LessThan(48f),
                "The chassis rotated across its direction of travel during a sustained turn.");
            Assert.That(
                peakMotorPower,
                Is.GreaterThan(1f),
                "The electric motor never received usable power from the circuit.");
            Assert.That(
                battery.StoredEnergyKilowattHours,
                Is.LessThan(initialBatteryEnergy),
                "Powered driving did not discharge the battery.");
        }

        [UnityTest, Ignore("Caravan runtime is intentionally disabled.")]
        public IEnumerator FirstPersonKeeperFollowsASettledMovingDeck()
        {
            if (Object.FindAnyObjectByType<SteppePrototypeBootstrap>() == null)
            {
                new GameObject("Caravan Keeper Test Bootstrap").AddComponent<SteppePrototypeBootstrap>();
            }

            var caravan = Object.FindAnyObjectByType<CaravanChassisController>();
            var keeper = Object.FindAnyObjectByType<CaravanFirstPersonController>();
            Assert.That(caravan, Is.Not.Null);
            Assert.That(keeper, Is.Not.Null);
            caravan.Body.isKinematic = true;
            caravan.Body.linearVelocity = Vector3.zero;
            var character = keeper.GetComponent<CharacterController>();
            character.enabled = false;
            keeper.transform.position = caravan.transform.TransformPoint(0f, 0.06f, -2.55f);
            character.enabled = true;
            Physics.SyncTransforms();
            for (var frame = 0; frame < 30 && !keeper.IsOnCaravan; frame++)
            {
                yield return null;
            }
            Assert.That(keeper.IsOnCaravan, Is.True);

            var relativeBefore = caravan.transform.InverseTransformPoint(keeper.transform.position);
            caravan.Body.position += new Vector3(0.8f, 0f, 0.35f);
            Physics.SyncTransforms();
            yield return null;
            var relativeAfter = caravan.transform.InverseTransformPoint(keeper.transform.position);
            Assert.That(Vector3.Distance(relativeAfter, relativeBefore), Is.LessThan(0.08f));
            caravan.Body.isKinematic = !caravan.PhysicsStarted;
        }

        [UnityTest]
        public IEnumerator EcologyKeepsSamplingMacroWorldOutsideTheActiveHorizon()
        {
            if (Object.FindAnyObjectByType<SteppePrototypeBootstrap>() == null)
            {
                new GameObject("P7 Macro Ecology Test Bootstrap").AddComponent<SteppePrototypeBootstrap>();
            }

            var ecology = Object.FindAnyObjectByType<SteppeEcologySystem>();
            var observer = Object.FindAnyObjectByType<FlyCameraController>();
            var oldCoordinate = ecology.CenterCoordinate;
            var oldMapOrigin = ecology.MapOriginCoordinate;
            for (var frame = 0; frame < 10 && !ecology.TryGetState(oldCoordinate, out _); frame++)
            {
                yield return null;
            }

            Assert.That(ecology.TryGetState(oldCoordinate, out var before), Is.True);
            observer.Teleport(observer.transform.position + Vector3.right * 8000f);
            for (var frame = 0; frame < 20 && ecology.IsActive(oldCoordinate); frame++)
            {
                yield return null;
            }

            Assert.That(ecology.IsActive(oldCoordinate), Is.False);
            Assert.That(ecology.TryGetState(oldCoordinate, out var after), Is.True);
            Assert.That(after.LastSimulationSeconds, Is.GreaterThan(before.LastSimulationSeconds));
            Assert.That(ecology.MapOriginCoordinate, Is.Not.EqualTo(oldMapOrigin));
            Assert.That(Shader.GetGlobalTexture("_SteppeEcologyStateMap"), Is.SameAs(ecology.StateMap));
        }

        [UnityTest]
        public IEnumerator NightRevealsMoonStarsAndReadableDirectionalLight()
        {
            if (Object.FindAnyObjectByType<SteppePrototypeBootstrap>() == null)
            {
                new GameObject("P2 Night Test Bootstrap").AddComponent<SteppePrototypeBootstrap>();
            }

            var timeSystem = Object.FindAnyObjectByType<SteppeTimeSystem>();
            var celestial = Object.FindAnyObjectByType<SteppeCelestialPresentation>();
            var hoursToMidnight = (24.0 - timeSystem.Current.Hour) % 24.0;
            var midnightSeconds = timeSystem.ElapsedSimulationSeconds + hoursToMidnight * 3600d;
            timeSystem.AdvanceSimulationSeconds(hoursToMidnight * 3600.0);
            for (var frame = 0;
                 frame < 600 && timeSystem.ElapsedSimulationSeconds < midnightSeconds;
                 frame++)
            {
                yield return null;
            }
            yield return null;

            Assert.That(celestial.CurrentSolarState.Daylight, Is.LessThan(0.01));
            Assert.That(celestial.MoonVisibility, Is.GreaterThan(0.9f));
            Assert.That(celestial.MoonLight.enabled, Is.True);
            Assert.That(celestial.MoonLight.intensity, Is.InRange(0.22f, 0.40f));
            Assert.That(RenderSettings.ambientIntensity, Is.GreaterThanOrEqualTo(0.50f));
            Assert.That(RenderSettings.sun, Is.SameAs(celestial.MoonLight));
            Assert.That(Shader.GetGlobalFloat("_SteppeNightAmount"), Is.GreaterThan(0.95f));
            Assert.That(Shader.GetGlobalFloat("_SteppeMoonVisibility"), Is.GreaterThan(0.9f));

            var fixedMoonRotation = celestial.MoonLight.transform.rotation;
            var fixedMoonDirection = Shader.GetGlobalVector("_SteppeMoonDirection");
            var twoHoursLater = timeSystem.ElapsedSimulationSeconds + 2.0 * 3600.0;
            timeSystem.AdvanceSimulationSeconds(2.0 * 3600.0);
            for (var frame = 0;
                 frame < 300 && timeSystem.ElapsedSimulationSeconds < twoHoursLater;
                 frame++)
            {
                yield return null;
            }
            yield return null;

            Assert.That(Quaternion.Angle(celestial.MoonLight.transform.rotation, fixedMoonRotation), Is.LessThan(0.001f));
            Assert.That(
                Vector3.Distance(Shader.GetGlobalVector("_SteppeMoonDirection"), fixedMoonDirection),
                Is.LessThan(0.0001f));
        }

        [UnityTest]
        public IEnumerator SemanticAtmosphereReadsEightAuthoritativeStatesWithoutChannelOverlap()
        {
            if (Object.FindAnyObjectByType<SteppePrototypeBootstrap>() == null)
            {
                new GameObject("Atmosphere Semantic Test Bootstrap")
                    .AddComponent<SteppePrototypeBootstrap>();
            }

            var host = Object.FindAnyObjectByType<SteppeSimulationHost>();
            var atmosphere = Object.FindAnyObjectByType<SteppeAtmospherePresentation>();
            var clouds = Object.FindAnyObjectByType<SteppeCloudLayer>();
            var soilBreath = Object.FindAnyObjectByType<SteppeSoilBreathPresentation>();
            var origin = Object.FindAnyObjectByType<FloatingOriginSystem>();
            var camera = Camera.main;
            var deadline = UnityEngine.Time.realtimeSinceStartup + 12f;
            while ((!host.IsReady || !soilBreath.IsReady)
                   && string.IsNullOrEmpty(host.LastError)
                   && UnityEngine.Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            for (var frame = 0; frame < 12; frame++)
            {
                yield return null;
            }

            Assert.That(host.LastError, Is.Null.Or.Empty);
            Assert.That(atmosphere.UsesSemanticAtmosphere, Is.True);
            Assert.That(atmosphere.SemanticStateCount, Is.EqualTo(8));
            Assert.That(soilBreath.IsReady, Is.True);
            Assert.That(camera, Is.Not.Null);
            var world = origin.LocalToWorld(camera.transform.position);
            Assert.That(host.TryGetEnvironmentSample(world.X, world.Z, out var physical), Is.True);
            Assert.That(
                atmosphere.CurrentSurfaceTemperatureC,
                Is.EqualTo(physical.SurfaceTemperatureC).Within(0.02f));
            Assert.That(
                atmosphere.CurrentAirTemperatureC,
                Is.EqualTo(physical.AirTemperatureC).Within(0.02f));
            Assert.That(
                atmosphere.CurrentSolarRadiationWm2,
                Is.EqualTo(physical.SolarRadiationWattsPerSquareMeter).Within(0.05f));
            Assert.That(
                atmosphere.CurrentHumidityMillimeters,
                Is.EqualTo(physical.HumidityMillimeters).Within(0.02f));
            Assert.That(
                atmosphere.CurrentCloudWaterMillimeters,
                Is.EqualTo(physical.CloudWaterMillimeters).Within(0.02f));
            Assert.That(
                atmosphere.CurrentPrecipitationMmPerHour,
                Is.EqualTo(physical.PrecipitationMillimetersPerHour).Within(0.02f));
            Assert.That(
                host.TrySampleState(
                    SimulationLayer.SoilTemperature,
                    world.X,
                    world.Z,
                    out var soilTemperature),
                Is.True);
            Assert.That(
                atmosphere.CurrentSoilTemperatureC,
                Is.EqualTo(soilTemperature).Within(0.02f));
            Assert.That(
                soilBreath.CurrentSoilTemperatureC,
                Is.EqualTo(soilTemperature).Within(0.02f));
            Assert.That(
                host.TrySampleState(
                    SimulationLayer.Pressure,
                    world.X,
                    world.Z,
                    out var pressure),
                Is.True);
            Assert.That(atmosphere.CurrentPressureHpa, Is.EqualTo(pressure).Within(0.02f));
            Assert.That(clouds.CurrentPressureHpa, Is.EqualTo(pressure).Within(0.02f));
            Assert.That(clouds.CurrentBaseHeight, Is.GreaterThanOrEqualTo(220f));
            Assert.That(
                Shader.GetGlobalFloat("_SteppeAirTemperatureSignal"),
                Is.EqualTo(atmosphere.CurrentAirTemperatureSignal).Within(0.0001f));
            Assert.That(
                Shader.GetGlobalFloat("_SteppeSolarRadiationSignal"),
                Is.EqualTo(atmosphere.CurrentSolarDirectSignal).Within(0.0001f));
            Assert.That(Shader.Find("Hidden/Steppe/Surface Heat Haze"), Is.Not.Null);
            Assert.That(soilBreath.Particles.main.simulationSpace,
                Is.EqualTo(ParticleSystemSimulationSpace.World));
            if (Mathf.Abs(soilBreath.CurrentThermalDeltaC) > 0.6f)
            {
                Assert.That(soilBreath.DisplayedEmissionRate, Is.GreaterThan(0f));
                Assert.That(
                    Mathf.Sign(soilBreath.DisplayedVerticalVelocity),
                    Is.EqualTo(Mathf.Sign(soilBreath.CurrentThermalDeltaC)));
            }
        }

        [UnityTest]
        public IEnumerator WeatherMapPublishesVisibleWaterBearingClouds()
        {
            if (Object.FindAnyObjectByType<SteppePrototypeBootstrap>() == null)
            {
                new GameObject("P3 Weather Test Bootstrap").AddComponent<SteppePrototypeBootstrap>();
            }

            var weather = Object.FindAnyObjectByType<SteppeWeatherSystem>();
            var clouds = Object.FindAnyObjectByType<SteppeCloudLayer>();
            var rain = Object.FindAnyObjectByType<SteppeRainPresentation>();
            for (var frame = 0; frame < 20 && !weather.IsWeatherMapReady; frame++)
            {
                yield return null;
            }

            Assert.That(weather.IsWeatherMapReady, Is.True);
            if (weather.UsesFiniteWorld)
            {
                var canonicalRevision = weather.MapRevision;
                for (var frame = 0; frame < 120 && weather.MapRevision <= canonicalRevision; frame++)
                {
                    yield return null;
                }

                Assert.That(weather.MapRevision, Is.GreaterThan(canonicalRevision));
                Assert.That(weather.MapMaximumCoverage, Is.GreaterThan(0.1f));
                Assert.That(weather.MapMaximumWater, Is.GreaterThan(0.01f));
                Assert.That(weather.MapMaximumRain, Is.InRange(0f, 1f));
                Assert.That(weather.MapMaximumGust, Is.InRange(0f, 1f));
            }
            else
            {
                Assert.That(weather.MapMaximumCoverage, Is.GreaterThan(0.7f));
                Assert.That(weather.MapMaximumWater, Is.GreaterThan(0.7f));
                Assert.That(weather.MapMaximumRain, Is.GreaterThan(0.01f));
                Assert.That(weather.MapMaximumGust, Is.GreaterThan(0.2f));
            }
            Assert.That(clouds.IsReady, Is.True);
            Assert.That(clouds.GetComponent<MeshRenderer>(), Is.Null, "Volumetric clouds must not use a visible dome mesh");
            Assert.That(clouds.GetComponent<MeshFilter>(), Is.Null, "Volumetric clouds must not use a carrier mesh");
            Assert.That(clouds.NoiseTexture, Is.Not.Null);
            Assert.That(clouds.NoiseTexture.dimension, Is.EqualTo(UnityEngine.Rendering.TextureDimension.Tex3D));
            Assert.That(clouds.NoiseTexture.width, Is.EqualTo(32));
            Assert.That(
                Shader.GetGlobalTexture("_SteppeCloudWeatherMap"),
                Is.SameAs(weather.WeatherMap));
            Assert.That(Shader.GetGlobalFloat("_SteppeCloudRendererActive"), Is.EqualTo(1f));
            Assert.That(Shader.Find("Hidden/Steppe/Volumetric Clouds"), Is.Not.Null);
            var layer = Shader.GetGlobalVector("_SteppeCloudLayerParameters");
            Assert.That(layer.x, Is.GreaterThan(1000f));
            Assert.That(layer.y, Is.GreaterThan(1000f));
            Assert.That(SteppeVolumetricCloudRendererFeature.PresentationActive, Is.True);
            Assert.That(rain, Is.Not.Null);
            Assert.That(rain.Particles.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
            Assert.That(rain.Renderer.renderMode, Is.EqualTo(ParticleSystemRenderMode.Stretch));
            Assert.That(rain.Renderer.sharedMaterial.shader.name, Is.EqualTo("Steppe/Rain Streak"));
            var rainVelocity = rain.Particles.velocityOverLifetime;
            Assert.That(rainVelocity.x.mode, Is.EqualTo(rainVelocity.y.mode));
            Assert.That(rainVelocity.y.mode, Is.EqualTo(rainVelocity.z.mode));
        }

        [UnityTest]
        public IEnumerator FloatingOriginPreservesAbsoluteFocusPosition()
        {
            var systemObject = new GameObject("Floating Origin Test");
            var focusObject = new GameObject("Focus");
            var worldRoot = new GameObject("World Root");
            var worldChild = new GameObject("World Child");
            worldChild.transform.SetParent(worldRoot.transform, false);
            worldChild.transform.position = new Vector3(50f, 0f, 50f);
            focusObject.transform.position = new Vector3(300f, 10f, -260f);

            var system = systemObject.AddComponent<FloatingOriginSystem>();
            system.Configure(focusObject.transform, worldRoot.transform, 128f, 64f);
            var before = system.LocalToWorld(focusObject.transform.position);

            yield return null;

            var after = system.LocalToWorld(focusObject.transform.position);
            Assert.That(after.X, Is.EqualTo(before.X).Within(0.0001));
            Assert.That(after.Y, Is.EqualTo(before.Y).Within(0.0001));
            Assert.That(after.Z, Is.EqualTo(before.Z).Within(0.0001));
            Assert.That(Mathf.Abs(focusObject.transform.position.x), Is.LessThan(128f));
            Assert.That(Mathf.Abs(focusObject.transform.position.z), Is.LessThan(128f));
            var shaderOrigin = Shader.GetGlobalVector("_SteppeWorldOriginXZ");
            Assert.That(shaderOrigin.x, Is.EqualTo(PositiveModulo(system.OriginX, 65536.0)).Within(0.001f));
            Assert.That(shaderOrigin.z, Is.EqualTo(PositiveModulo(system.OriginZ, 65536.0)).Within(0.001f));

            Object.Destroy(systemObject);
            Object.Destroy(focusObject);
            Object.Destroy(worldRoot);
        }

        private static float PositiveModulo(double value, double modulus)
        {
            return (float)(value - System.Math.Floor(value / modulus) * modulus);
        }

        private static bool TryFindFreePlacement(
            CaravanMountGrid grid,
            CaravanModule module,
            out CaravanGridPlacement placement)
        {
            for (var quarterTurns = 0; quarterTurns < 2; quarterTurns++)
            {
                for (var z = 0; z < grid.Length; z++)
                {
                    for (var x = 0; x < grid.Width; x++)
                    {
                        var candidate = new CaravanGridPlacement(
                            x,
                            z,
                            module.FootprintWidth,
                            module.FootprintLength,
                            quarterTurns);
                        if (!grid.CanPlace(module, candidate))
                        {
                            continue;
                        }

                        placement = candidate;
                        return true;
                    }
                }
            }

            placement = default;
            return false;
        }

        private static CaravanPart FindPart(
            CaravanPart[] parts,
            CaravanPartKind kind)
        {
            var part = System.Array.Find(parts, item => item.Kind == kind);
            Assert.That(part, Is.Not.Null, $"Could not find {kind}.");
            return part;
        }

        private static void AssertMaterialConnection(
            CaravanBuildModeController build,
            CaravanMaterialNetwork network,
            CaravanPart firstPart,
            CaravanMaterialPortRole firstRole,
            CaravanPart secondPart,
            CaravanMaterialPortRole secondRole)
        {
            var first = FindMaterialPort(network, firstPart, firstRole);
            var second = FindMaterialPort(network, secondPart, secondRole);
            Assert.That(build.TrySelectMaterialPort(first), Is.True);
            Assert.That(
                build.TrySelectMaterialPort(second),
                Is.True,
                $"Could not connect {firstRole} to {secondRole}.");
        }

        private static CaravanMaterialPort FindMaterialPort(
            CaravanMaterialNetwork network,
            CaravanPart part,
            CaravanMaterialPortRole role)
        {
            for (var index = 0; index < network.Ports.Count; index++)
            {
                var port = network.Ports[index];
                if (port.Part == part && port.Role == role)
                {
                    return port;
                }
            }

            Assert.Fail($"Could not find {role} on {part.Kind}.");
            return null;
        }

        private static CaravanElectricalPort FindElectricalPort(
            CaravanElectricalNetwork network,
            CaravanPartKind kind)
        {
            for (var index = 0; index < network.Ports.Count; index++)
            {
                var port = network.Ports[index];
                if (port.Part.Kind == kind)
                {
                    return port;
                }
            }

            Assert.Fail($"Could not find an electrical port on {kind}.");
            return null;
        }

        private static CaravanFluidPort FindFluidPort(
            CaravanFluidNetwork network,
            CaravanPart part)
        {
            for (var index = 0; index < network.Ports.Count; index++)
            {
                var port = network.Ports[index];
                if (port.Part == part
                    && port.Role == CaravanFluidPortRole.ThermalTap)
                {
                    return port;
                }
            }

            Assert.Fail($"Could not find a thermal tap on {part.Kind}.");
            return null;
        }

        private static void AssertFluidConnection(
            CaravanFluidNetwork network,
            CaravanFluidPort first,
            CaravanFluidPort second)
        {
            for (var index = 0; index < network.Pipes.Count; index++)
            {
                var pipe = network.Pipes[index];
                if ((pipe.Start == first && pipe.End == second)
                    || (pipe.Start == second && pipe.End == first))
                {
                    Assert.That(pipe.IsConductive, Is.True);
                    return;
                }
            }

            Assert.That(
                network.TryConnect(first, second),
                Is.True,
                $"Could not connect {first.Role} to {second.Role}.");
        }
    }
}
