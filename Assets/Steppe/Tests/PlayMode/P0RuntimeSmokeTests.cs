using System.Collections;
using NUnit.Framework;
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
        [UnityTest]
        public IEnumerator PrototypeCreatesPhysicalBallAndStreamsTerrain()
        {
            if (Object.FindAnyObjectByType<SteppePrototypeBootstrap>() == null)
            {
                new GameObject("P0 Test Bootstrap").AddComponent<SteppePrototypeBootstrap>();
            }

            yield return null;
            yield return null;

            var ballController = Object.FindAnyObjectByType<SteppeBallController>();
            var cameraController = Object.FindAnyObjectByType<SteppeBallCameraController>();
            var tracks = Object.FindAnyObjectByType<SteppeTrackSystem>();
            var streamer = Object.FindAnyObjectByType<TerrainChunkStreamer>();
            var grass = Object.FindAnyObjectByType<SteppeGrassRenderer>();
            var timeSystem = Object.FindAnyObjectByType<SteppeTimeSystem>();
            var weatherSystem = Object.FindAnyObjectByType<SteppeWeatherSystem>();
            var ecologySystem = Object.FindAnyObjectByType<SteppeEcologySystem>();
            var dust = Object.FindAnyObjectByType<SteppeDustPresentation>();
            var snow = Object.FindAnyObjectByType<SteppeSnowPresentation>();
            var workScheduler = Object.FindAnyObjectByType<WorldWorkScheduler>();

            Assert.That(cameraController, Is.Not.Null);
            Assert.That(ballController, Is.Not.Null);
            Assert.That(ballController.GetComponent<SphereCollider>(), Is.Not.Null);
            Assert.That(ballController.GetComponent<Rigidbody>(), Is.Not.Null);
            Assert.That(tracks, Is.Not.Null);
            Assert.That(tracks.StateMap, Is.Not.Null);
            Assert.That(tracks.StateMap.width, Is.EqualTo(512));
            Assert.That(Shader.GetGlobalTexture("_SteppeTrackStateMap"), Is.SameAs(tracks.StateMap));
            var originSystem = Object.FindAnyObjectByType<FloatingOriginSystem>();
            var initialBallWorld = originSystem.LocalToWorld(ballController.transform.position);
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
            for (var frame = 0; frame < 40 && ballController.Body.isKinematic; frame++)
            {
                yield return new WaitForFixedUpdate();
            }
            Assert.That(ballController.Body.isKinematic, Is.False, "Ball never attached to a streamed terrain collider.");
            Assert.That(Object.FindObjectsByType<MeshCollider>().Length, Is.GreaterThan(0));
            ballController.Body.linearVelocity = Vector3.forward * 3f;
            for (var fixedStep = 0; fixedStep < 5; fixedStep++)
            {
                yield return new WaitForFixedUpdate();
            }
            Assert.That(tracks.StoredTrackCellCount, Is.GreaterThan(0), "Rolling ball did not leave a canonical track.");
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
                .LocalToWorld(ballController.transform.position);
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
            Assert.That(cameraController.GetComponent<Collider>(), Is.Null);
            Assert.That(cameraController.GetComponent<Rigidbody>(), Is.Null);
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
        public IEnumerator EcologyRetainsARecordAfterItLeavesTheActiveHorizon()
        {
            if (Object.FindAnyObjectByType<SteppePrototypeBootstrap>() == null)
            {
                new GameObject("P7 Persistence Test Bootstrap").AddComponent<SteppePrototypeBootstrap>();
            }

            var ecology = Object.FindAnyObjectByType<SteppeEcologySystem>();
            var ballController = Object.FindAnyObjectByType<SteppeBallController>();
            var oldCoordinate = ecology.CenterCoordinate;
            var oldMapOrigin = ecology.MapOriginCoordinate;
            for (var frame = 0; frame < 10 && !ecology.TryGetState(oldCoordinate, out _); frame++)
            {
                yield return null;
            }

            Assert.That(ecology.TryGetState(oldCoordinate, out var before), Is.True);
            ballController.Teleport(ballController.transform.position + Vector3.right * 12000f);
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
    }
}
