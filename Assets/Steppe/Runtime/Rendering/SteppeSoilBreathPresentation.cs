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
    /// Sparse neutral pore-vapour wisps expose the slow soil thermal reservoir.
    /// Soil warmer than air exhales upward; soil colder than air produces descending
    /// near-ground curls. The component is presentation-only and never mutates state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SteppeSoilBreathPresentation : MonoBehaviour
    {
        private SteppeSimulationHost host;
        private FloatingOriginSystem floatingOrigin;
        private Transform focus;
        private TerrainHeightGenerator terrain;
        private ParticleSystem particles;
        private Material material;

        public bool IsReady => host != null && host.IsReady && particles != null;
        public float CurrentSoilTemperatureC { get; private set; }
        public float CurrentAirTemperatureC { get; private set; }
        public float CurrentThermalDeltaC => CurrentSoilTemperatureC - CurrentAirTemperatureC;
        public float DisplayedVerticalVelocity { get; private set; }
        public float DisplayedEmissionRate { get; private set; }
        public ParticleSystem Particles => particles;

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

            particles = gameObject.AddComponent<ParticleSystem>();
            ConfigureParticles();
            material = CreateMaterial();
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            floatingOrigin.Shifted += HandleOriginShift;
            particles.Play(true);
        }

        private void ConfigureParticles()
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 520;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.2f, 6.8f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.78f, 0.82f, 0.80f, 0.055f),
                new Color(0.90f, 0.91f, 0.87f, 0.14f));
            main.gravityModifier = 0f;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(92f, 0.08f, 92f);

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = 0f;
            velocity.y = 0f;
            velocity.z = 0f;

            var noise = particles.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.strength = 0.16f;
            noise.frequency = 0.21f;
            noise.scrollSpeed = 0.09f;
            noise.damping = true;

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.velocityScale = 0.08f;
            renderer.lengthScale = 2.8f;
            renderer.cameraVelocityScale = 0f;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static Material CreateMaterial()
        {
            var shader = Shader.Find("Steppe/Soil Pore Vapor");
            if (shader == null)
            {
                throw new InvalidOperationException("Steppe/Soil Pore Vapor shader was not found.");
            }

            return new Material(shader)
            {
                name = "Steppe Soil Thermal Breath Material",
                hideFlags = HideFlags.DontSave,
            };
        }

        private void Update()
        {
            if (host == null || floatingOrigin == null || focus == null || particles == null)
            {
                return;
            }

            var world = floatingOrigin.LocalToWorld(focus.position);
            if (!host.TrySampleState(
                    SimulationLayer.SoilTemperature,
                    world.X,
                    world.Z,
                    out var soilTemperature)
                || !host.TrySampleState(
                    SimulationLayer.AirTemperature,
                    world.X,
                    world.Z,
                    out var airTemperature))
            {
                return;
            }

            CurrentSoilTemperatureC = soilTemperature;
            CurrentAirTemperatureC = airTemperature;
            var delta = CurrentThermalDeltaC;
            var magnitude = Mathf.Abs(delta);
            var strength = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 10f, magnitude));
            var targetVelocity = delta >= 0f
                ? Mathf.Lerp(0.11f, 0.62f, strength)
                : -Mathf.Lerp(0.08f, 0.34f, strength);
            var response = 1f - Mathf.Exp(-UnityEngine.Time.deltaTime * 1.5f);
            DisplayedVerticalVelocity = Mathf.Lerp(
                DisplayedVerticalVelocity,
                targetVelocity,
                response);
            DisplayedEmissionRate = Mathf.Lerp(
                DisplayedEmissionRate,
                strength * 58f,
                response);

            var groundHeight = (float)terrain.SampleHeight(world.X, world.Z);
            var emitterHeight = DisplayedVerticalVelocity >= 0f ? 0.16f : 2.1f;
            transform.position = new Vector3(
                focus.position.x,
                groundHeight + emitterHeight,
                focus.position.z);

            var emission = particles.emission;
            emission.rateOverTime = DisplayedEmissionRate;
            var velocity = particles.velocityOverLifetime;
            velocity.x = 0f;
            velocity.y = DisplayedVerticalVelocity;
            velocity.z = 0f;
        }

        private void HandleOriginShift(Vector3 shift)
        {
            particles?.Clear(true);
        }

        private void OnDestroy()
        {
            if (floatingOrigin != null)
            {
                floatingOrigin.Shifted -= HandleOriginShift;
            }

            if (material == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(material);
            }
            else
            {
                DestroyImmediate(material);
            }
        }
    }
}
