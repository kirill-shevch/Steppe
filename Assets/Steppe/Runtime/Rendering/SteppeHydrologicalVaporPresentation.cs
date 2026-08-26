using System;
using Steppe.Settings;
using Steppe.Simulation;
using Steppe.Terrain;
using Steppe.UnitySimulation;
using Steppe.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Steppe.Rendering
{
    /// <summary>
    /// Three spatially and graphically separate atmospheric water carriers. Their
    /// emission is driven only by canonical macro fluxes; this class never advances
    /// or mutates the simulation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SteppeHydrologicalVaporPresentation : MonoBehaviour
    {
        private sealed class Carrier
        {
            public Transform Transform;
            public ParticleSystem Particles;
            public Material Material;
            public float DisplayedRate;
            public float DisplayedEmission;
        }

        private SteppeSimulationHost host;
        private FloatingOriginSystem floatingOrigin;
        private Transform focus;
        private TerrainHeightGenerator terrain;
        private Carrier sublimation;
        private Carrier evaporation;
        private Carrier transpiration;
        private Carrier percolation;

        public bool IsReady => host != null
                               && host.IsReady
                               && sublimation?.Particles != null
                               && evaporation?.Particles != null
                               && transpiration?.Particles != null
                               && percolation?.Particles != null;
        public ParticleSystem SublimationParticles => sublimation?.Particles;
        public ParticleSystem EvaporationParticles => evaporation?.Particles;
        public ParticleSystem TranspirationParticles => transpiration?.Particles;
        public ParticleSystem PercolationParticles => percolation?.Particles;
        public float CurrentSnowSublimationRate => sublimation?.DisplayedRate ?? 0f;
        public float CurrentSurfaceEvaporationRate => evaporation?.DisplayedRate ?? 0f;
        public float CurrentTranspirationRate => transpiration?.DisplayedRate ?? 0f;
        public float CurrentPercolationRate => percolation?.DisplayedRate ?? 0f;
        public float DisplayedSnowSublimationEmission => sublimation?.DisplayedEmission ?? 0f;
        public float DisplayedSurfaceEvaporationEmission => evaporation?.DisplayedEmission ?? 0f;
        public float DisplayedTranspirationEmission => transpiration?.DisplayedEmission ?? 0f;
        public float DisplayedPercolationEmission => percolation?.DisplayedEmission ?? 0f;

        public void Configure(
            SteppeWorldSettings settings,
            SteppeSimulationHost simulationHost,
            FloatingOriginSystem origin,
            Transform focusTransform)
        {
            terrain = new TerrainHeightGenerator(
                settings != null ? settings : throw new ArgumentNullException(nameof(settings)));
            host = simulationHost != null
                ? simulationHost
                : throw new ArgumentNullException(nameof(simulationHost));
            floatingOrigin = origin != null
                ? origin
                : throw new ArgumentNullException(nameof(origin));
            focus = focusTransform != null
                ? focusTransform
                : throw new ArgumentNullException(nameof(focusTransform));

            sublimation = CreateCarrier(
                "Snow Sublimation Crystals",
                carrierKind: 0f,
                maximumParticles: 720,
                lifetime: new Vector2(1.8f, 3.8f),
                size: new Vector2(0.035f, 0.095f),
                area: 78f,
                stretched: false,
                noiseStrength: 0.055f,
                fixedColor: new Color(0.86f, 0.94f, 1f, 0.72f));
            evaporation = CreateCarrier(
                "Surface Evaporation Ribbons",
                carrierKind: 1f,
                maximumParticles: 340,
                lifetime: new Vector2(4.2f, 7.4f),
                size: new Vector2(0.48f, 1.05f),
                area: 88f,
                stretched: false,
                noiseStrength: 0.13f,
                fixedColor: new Color(0.76f, 0.84f, 0.86f, 0.16f));
            transpiration = CreateCarrier(
                "Canopy Transpiration Filaments",
                carrierKind: 2f,
                maximumParticles: 560,
                lifetime: new Vector2(2.6f, 5.2f),
                size: new Vector2(0.075f, 0.17f),
                area: 72f,
                stretched: true,
                noiseStrength: 0.085f,
                fixedColor: new Color(0.76f, 0.88f, 0.70f, 0.28f));
            percolation = CreateCarrier(
                "Percolation Capillary Beads",
                carrierKind: 3f,
                maximumParticles: 1100,
                lifetime: new Vector2(0.55f, 0.95f),
                size: new Vector2(0.075f, 0.145f),
                area: 42f,
                stretched: true,
                noiseStrength: 0.012f,
                fixedColor: new Color(0.32f, 0.69f, 0.75f, 0.86f));

            floatingOrigin.Shifted += HandleOriginShift;
        }

        private Carrier CreateCarrier(
            string carrierName,
            float carrierKind,
            int maximumParticles,
            Vector2 lifetime,
            Vector2 size,
            float area,
            bool stretched,
            float noiseStrength,
            Color fixedColor)
        {
            var carrierObject = new GameObject(carrierName);
            carrierObject.transform.SetParent(transform, false);
            var particles = carrierObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.maxParticles = maximumParticles;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = Color.white;
            main.gravityModifier = 0f;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(area, 0.08f, area);

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = 0f;
            velocity.y = 0f;
            velocity.z = 0f;

            var noise = particles.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.strength = noiseStrength;
            noise.frequency = carrierKind < 0.5f ? 0.48f : 0.22f;
            noise.scrollSpeed = carrierKind < 0.5f ? 0.22f : 0.09f;
            noise.damping = true;

            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.16f),
                    new GradientAlphaKey(0.72f, 0.68f),
                    new GradientAlphaKey(0f, 1f),
                });
            colorOverLifetime.color = gradient;

            var shader = Shader.Find("Steppe/Hydrological Vapor Carriers");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "Steppe/Hydrological Vapor Carriers shader was not found.");
            }

            var material = new Material(shader)
            {
                name = $"Steppe {carrierName} Material",
                hideFlags = HideFlags.DontSave,
            };
            material.SetFloat("_CarrierKind", carrierKind);
            material.SetColor("_BaseColor", fixedColor);

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = stretched
                ? ParticleSystemRenderMode.Stretch
                : ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.velocityScale = stretched ? 0.11f : 0f;
            renderer.lengthScale = stretched ? 3.8f : 1f;
            renderer.cameraVelocityScale = 0f;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            particles.Play(true);

            return new Carrier
            {
                Transform = carrierObject.transform,
                Particles = particles,
                Material = material,
            };
        }

        private void Update()
        {
            if (host == null || floatingOrigin == null || focus == null || !IsReady)
            {
                return;
            }

            var world = floatingOrigin.LocalToWorld(focus.position);
            if (!host.TrySampleFluxRate(
                    SimulationFlux.SnowSublimation,
                    world.X,
                    world.Z,
                    out var snowSublimationRate)
                || !host.TrySampleFluxRate(
                    SimulationFlux.SurfaceEvaporation,
                    world.X,
                    world.Z,
                    out var surfaceEvaporationRate)
                || !host.TrySampleFluxRate(
                    SimulationFlux.Transpiration,
                    world.X,
                    world.Z,
                    out var transpirationRate)
                || !host.TrySampleFluxRate(
                    SimulationFlux.Percolation,
                    world.X,
                    world.Z,
                    out var percolationRate))
            {
                return;
            }

            host.TrySampleState(SimulationLayer.Snow, world.X, world.Z, out var snow);
            host.TrySampleState(SimulationLayer.SurfaceWater, world.X, world.Z, out var water);
            host.TrySampleState(SimulationLayer.LiveBiomass, world.X, world.Z, out var biomass);
            var wind = Vector2.zero;
            if (host.TryGetEnvironmentSample(world.X, world.Z, out var environment))
            {
                wind = new Vector2(environment.WindXMs, environment.WindZMs);
            }

            var response = 1f - Mathf.Exp(-UnityEngine.Time.deltaTime * 2.1f);
            UpdateCarrier(
                sublimation,
                Mathf.Max(0f, snowSublimationRate),
                fullRate: 0.006f,
                maximumEmission: 320f,
                verticalVelocity: new Vector2(0.42f, 1.35f),
                horizontalVelocity: wind * 0.055f,
                response);
            UpdateCarrier(
                evaporation,
                Mathf.Max(0f, surfaceEvaporationRate),
                fullRate: 0.08f,
                maximumEmission: 72f,
                verticalVelocity: new Vector2(0.08f, 0.34f),
                horizontalVelocity: wind * 0.022f,
                response);
            UpdateCarrier(
                transpiration,
                Mathf.Max(0f, transpirationRate),
                fullRate: 0.006f,
                maximumEmission: 190f,
                verticalVelocity: new Vector2(0.24f, 0.78f),
                horizontalVelocity: wind * 0.032f,
                response);
            UpdateCarrier(
                percolation,
                Mathf.Max(0f, percolationRate),
                fullRate: 0.12f,
                maximumEmission: 560f,
                verticalVelocity: new Vector2(-0.32f, -0.92f),
                horizontalVelocity: Vector2.zero,
                response);

            var groundHeight = (float)terrain.SampleHeight(world.X, world.Z);
            sublimation.Transform.position = new Vector3(
                focus.position.x,
                groundHeight + 0.10f + Mathf.Clamp(snow * 0.012f, 0f, 0.42f),
                focus.position.z);
            evaporation.Transform.position = new Vector3(
                focus.position.x,
                groundHeight + 0.12f + Mathf.Clamp(water * 0.01f, 0f, 0.22f),
                focus.position.z);
            transpiration.Transform.position = new Vector3(
                focus.position.x,
                groundHeight + Mathf.Lerp(
                    0.28f,
                    1.12f,
                    Mathf.Clamp01(biomass / 650f)),
                focus.position.z);
            percolation.Transform.position = new Vector3(
                focus.position.x,
                groundHeight + 0.48f,
                focus.position.z);
        }

        private static void UpdateCarrier(
            Carrier carrier,
            float rate,
            float fullRate,
            float maximumEmission,
            Vector2 verticalVelocity,
            Vector2 horizontalVelocity,
            float response)
        {
            carrier.DisplayedRate = Mathf.Lerp(carrier.DisplayedRate, rate, response);
            var strength = Mathf.Sqrt(Mathf.Clamp01(carrier.DisplayedRate / fullRate));
            carrier.DisplayedEmission = Mathf.Lerp(
                carrier.DisplayedEmission,
                maximumEmission * strength,
                response);
            var emission = carrier.Particles.emission;
            emission.rateOverTime = carrier.DisplayedEmission;
            var velocity = carrier.Particles.velocityOverLifetime;
            velocity.x = horizontalVelocity.x;
            velocity.y = Mathf.Lerp(verticalVelocity.x, verticalVelocity.y, strength);
            velocity.z = horizontalVelocity.y;
        }

        private void HandleOriginShift(Vector3 shift)
        {
            sublimation?.Particles?.Clear(true);
            evaporation?.Particles?.Clear(true);
            transpiration?.Particles?.Clear(true);
            percolation?.Particles?.Clear(true);
        }

        private void OnDestroy()
        {
            if (floatingOrigin != null)
            {
                floatingOrigin.Shifted -= HandleOriginShift;
            }

            DestroyCarrierMaterial(sublimation);
            DestroyCarrierMaterial(evaporation);
            DestroyCarrierMaterial(transpiration);
            DestroyCarrierMaterial(percolation);
        }

        private static void DestroyCarrierMaterial(Carrier carrier)
        {
            if (carrier?.Material == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(carrier.Material);
            }
            else
            {
                DestroyImmediate(carrier.Material);
            }
        }
    }
}
