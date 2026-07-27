using System.Collections;
using NUnit.Framework;
using Steppe.Caravan;
using Steppe.Ecology;
using Steppe.Player;
using Steppe.Prototype;
using Steppe.Rendering;
using Steppe.Terrain;
using Steppe.Time;
using Steppe.Weather;
using Steppe.World;
using UnityEngine;
using UnityEngine.TestTools;

namespace Steppe.Tests
{
    public sealed class P0RuntimeSmokeTests
    {
        [UnityTest, Order(-100)]
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
            Assert.That(controlStations, Has.Length.EqualTo(3));
            var steeringStation = System.Array.Find(
                controlStations,
                station => station.Kind == CaravanControlKind.Steering);
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
            Assert.That(trimStation, Is.Null);
            Assert.That(motorStation, Is.Not.Null);
            Assert.That(solarStation, Is.Not.Null);
            Assert.That(
                steeringStation.transform.Find("Control Visual/Wheel Rim 1"),
                Is.Not.Null);
            Assert.That(steeringStation.transform.Find("Focus Indicator"), Is.Not.Null);
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
            Assert.That(
                Vector3.Dot(caravan.transform.forward, initialSurfaceWind),
                Is.GreaterThan(0.98f),
                "The caravan nose is not aligned with the initial surface wind.");
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
        public IEnumerator WeatherTimeTracksCanonicalClockAfterManualAdvance()
        {
            if (Object.FindAnyObjectByType<SteppePrototypeBootstrap>() == null)
            {
                new GameObject("P1 Canonical Time Test Bootstrap").AddComponent<SteppePrototypeBootstrap>();
            }

            yield return null;

            var timeSystem = Object.FindAnyObjectByType<SteppeTimeSystem>();
            var weatherSystem = Object.FindAnyObjectByType<SteppeWeatherSystem>();
            timeSystem.AdvanceSimulationSeconds(12345.678);

            yield return null;

            Assert.That(weatherSystem.WeatherSeconds, Is.GreaterThan(0.0));
            Assert.That(
                Shader.GetGlobalFloat("_SteppeWindTime"),
                Is.EqualTo((float)weatherSystem.WeatherSeconds).Within(0.05f));
        }

        [UnityTest]
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

        [UnityTest]
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

        [UnityTest]
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
            resource.Simulate(0.1f);
            electrical.Simulate(1f);
            resource.Simulate(4f);
            resource.Simulate(0.1f);
            electrical.Simulate(1f);
            resource.Simulate(2f);
            fluids.Simulate(2f);

            Assert.That(
                harvester.CurrentHarvestKilogramsPerSecond,
                Is.GreaterThan(0f));
            Assert.That(
                dryer.CurrentDryingKilogramsPerSecond,
                Is.GreaterThan(0f));
            Assert.That(furnace.ThermalOutputKilowatts, Is.GreaterThan(0f));
            Assert.That(
                engine.DeliveredMechanicalKilowatts,
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
            Assert.That(fluids.CurrentHeatingKilowatts, Is.GreaterThan(0f));
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

        [UnityTest]
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

        [UnityTest]
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
            Assert.That(caravan, Is.Not.Null);
            Assert.That(battery, Is.Not.Null);
            Assert.That(electricMotor, Is.Not.Null);
            Assert.That(network, Is.Not.Null);
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
            caravan.SetSteeringNormalized(0.82f);
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

            caravan.SetSteeringNormalized(0f);
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

        [UnityTest]
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
        public IEnumerator EcologyRetainsARecordAfterItLeavesTheActiveHorizon()
        {
            if (Object.FindAnyObjectByType<SteppePrototypeBootstrap>() == null)
            {
                new GameObject("P7 Persistence Test Bootstrap").AddComponent<SteppePrototypeBootstrap>();
            }

            var ecology = Object.FindAnyObjectByType<SteppeEcologySystem>();
            var caravan = Object.FindAnyObjectByType<CaravanChassisController>();
            var oldCoordinate = ecology.CenterCoordinate;
            var oldMapOrigin = ecology.MapOriginCoordinate;
            for (var frame = 0; frame < 10 && !ecology.TryGetState(oldCoordinate, out _); frame++)
            {
                yield return null;
            }

            Assert.That(ecology.TryGetState(oldCoordinate, out var before), Is.True);
            caravan.Teleport(caravan.transform.position + Vector3.right * 12000f);
            for (var frame = 0; frame < 20 && ecology.IsActive(oldCoordinate); frame++)
            {
                yield return null;
            }

            Assert.That(ecology.IsActive(oldCoordinate), Is.False);
            Assert.That(ecology.TryGetState(oldCoordinate, out var after), Is.True);
            Assert.That(after.LastSimulationSeconds, Is.EqualTo(before.LastSimulationSeconds));
            Assert.That(ecology.MapOriginCoordinate, Is.Not.EqualTo(oldMapOrigin));
            Assert.That(Shader.GetGlobalTexture("_SteppeEcologyStateMap"), Is.SameAs(ecology.StateMap));
        }

        [UnityTest]
        public IEnumerator NightRevealsMoonStarsAndWeakDirectionalLight()
        {
            if (Object.FindAnyObjectByType<SteppePrototypeBootstrap>() == null)
            {
                new GameObject("P2 Night Test Bootstrap").AddComponent<SteppePrototypeBootstrap>();
            }

            var timeSystem = Object.FindAnyObjectByType<SteppeTimeSystem>();
            var celestial = Object.FindAnyObjectByType<SteppeCelestialPresentation>();
            var hoursToMidnight = (24.0 - timeSystem.Current.Hour) % 24.0;
            timeSystem.AdvanceSimulationSeconds(hoursToMidnight * 3600.0);
            yield return null;

            Assert.That(celestial.CurrentSolarState.Daylight, Is.LessThan(0.01));
            Assert.That(celestial.MoonVisibility, Is.GreaterThan(0.9f));
            Assert.That(celestial.MoonLight.enabled, Is.True);
            Assert.That(celestial.MoonLight.intensity, Is.GreaterThan(0.01f));
            Assert.That(celestial.MoonLight.intensity, Is.LessThan(0.1f));
            Assert.That(RenderSettings.sun, Is.SameAs(celestial.MoonLight));
            Assert.That(Shader.GetGlobalFloat("_SteppeNightAmount"), Is.GreaterThan(0.95f));
            Assert.That(Shader.GetGlobalFloat("_SteppeMoonVisibility"), Is.GreaterThan(0.9f));

            var fixedMoonRotation = celestial.MoonLight.transform.rotation;
            var fixedMoonDirection = Shader.GetGlobalVector("_SteppeMoonDirection");
            timeSystem.AdvanceSimulationSeconds(2.0 * 3600.0);
            yield return null;

            Assert.That(Quaternion.Angle(celestial.MoonLight.transform.rotation, fixedMoonRotation), Is.LessThan(0.001f));
            Assert.That(
                Vector3.Distance(Shader.GetGlobalVector("_SteppeMoonDirection"), fixedMoonDirection),
                Is.LessThan(0.0001f));
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
            Assert.That(weather.MapMaximumCoverage, Is.GreaterThan(0.7f));
            Assert.That(weather.MapMaximumWater, Is.GreaterThan(0.7f));
            Assert.That(weather.MapMaximumRain, Is.GreaterThan(0.01f));
            Assert.That(weather.MapMaximumGust, Is.GreaterThan(0.2f));
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
