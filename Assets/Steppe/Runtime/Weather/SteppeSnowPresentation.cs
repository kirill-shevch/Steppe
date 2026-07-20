using System;
using Steppe.Settings;
using Steppe.Time;
using Steppe.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Steppe.Weather
{
    /// <summary>
    /// Camera-local snowfall presentation driven by the same precipitation field as rain.
    /// Temperature selects the phase; accumulation remains authoritative in ecology cells.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SteppeSnowPresentation : MonoBehaviour
    {
        private SteppeWorldSettings settings;
        private SteppeWeatherSystem weatherSystem;
        private SteppeTimeSystem timeSystem;
        private FloatingOriginSystem floatingOrigin;
        private Transform focus;
        private SteppeLocalClimateSampler climateSampler;
        private ParticleSystem snowParticles;
        private ParticleSystem.Particle[] shiftBuffer;
        private Material snowMaterial;
        private bool ownsMaterial;
        private float displayedIntensity;

        public ParticleSystem Particles => snowParticles;
        public ParticleSystemRenderer Renderer { get; private set; }
        public float DisplayedIntensity => displayedIntensity;
        public double CurrentAirTemperatureC { get; private set; }
        public float CurrentSnowFraction { get; private set; }

        public void Configure(
            SteppeWorldSettings worldSettings,
            SteppeWeatherSystem weather,
            SteppeTimeSystem clock,
            FloatingOriginSystem origin,
            Transform focusTransform,
            Material material = null)
        {
            settings = worldSettings != null ? worldSettings : throw new ArgumentNullException(nameof(worldSettings));
            weatherSystem = weather != null ? weather : throw new ArgumentNullException(nameof(weather));
            timeSystem = clock != null ? clock : throw new ArgumentNullException(nameof(clock));
            floatingOrigin = origin != null ? origin : throw new ArgumentNullException(nameof(origin));
            focus = focusTransform != null ? focusTransform : throw new ArgumentNullException(nameof(focusTransform));
            climateSampler = new SteppeLocalClimateSampler(settings);

            snowParticles = GetComponent<ParticleSystem>();
            if (snowParticles == null)
            {
                snowParticles = gameObject.AddComponent<ParticleSystem>();
            }

            Renderer = GetComponent<ParticleSystemRenderer>();
            shiftBuffer = new ParticleSystem.Particle[settings.SnowMaxParticles];
            ConfigureParticles();
            ConfigureMaterial(material);
            floatingOrigin.Shifted += HandleOriginShift;
            transform.position = focus.position + Vector3.up * settings.SnowSpawnHeight;
            snowParticles.Play(true);
        }

        private void ConfigureParticles()
        {
            snowParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = snowParticles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.maxParticles = settings.SnowMaxParticles;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6.5f, 10f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.13f);
            main.startColor = new Color(0.9f, 0.94f, 0.97f, 0.92f);
            main.gravityModifier = 0f;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

            var emission = snowParticles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            var shape = snowParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(settings.SnowEmissionArea, 1f, settings.SnowEmissionArea);

            var velocity = snowParticles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            ApplyVelocity(velocity);

            var noise = snowParticles.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.strength = 0.48f;
            noise.frequency = 0.34f;
            noise.scrollSpeed = 0.12f;
            noise.damping = true;

            Renderer.renderMode = ParticleSystemRenderMode.Billboard;
            Renderer.alignment = ParticleSystemRenderSpace.Facing;
            Renderer.sortMode = ParticleSystemSortMode.Distance;
            Renderer.shadowCastingMode = ShadowCastingMode.Off;
            Renderer.receiveShadows = false;
            Renderer.lightProbeUsage = LightProbeUsage.Off;
            Renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private void ConfigureMaterial(Material material)
        {
            if (material != null)
            {
                snowMaterial = material;
                ownsMaterial = false;
            }
            else
            {
                var shader = Shader.Find("Steppe/Snow Flake");
                if (shader == null)
                {
                    throw new InvalidOperationException("Steppe/Snow Flake shader was not found.");
                }

                snowMaterial = new Material(shader)
                {
                    name = "Steppe P11 Snow Material",
                    hideFlags = HideFlags.DontSave
                };
                ownsMaterial = true;
            }
            Renderer.sharedMaterial = snowMaterial;
        }

        private void Update()
        {
            if (settings == null
                || weatherSystem == null
                || timeSystem == null
                || floatingOrigin == null
                || focus == null
                || snowParticles == null)
            {
                return;
            }

            transform.position = focus.position + Vector3.up * settings.SnowSpawnHeight;
            var focusWorld = floatingOrigin.LocalToWorld(focus.position);
            CurrentAirTemperatureC = climateSampler.SampleTemperature(
                focusWorld.X,
                focusWorld.Z,
                timeSystem.Current);
            CurrentSnowFraction = (float)SteppePrecipitationPhase.SnowFraction(CurrentAirTemperatureC);
            var targetIntensity = Mathf.Clamp01(
                (float)weatherSystem.CurrentAtFocus.RainIntensity * CurrentSnowFraction);
            var response = targetIntensity > displayedIntensity ? 1.4f : 0.8f;
            displayedIntensity = Mathf.MoveTowards(
                displayedIntensity,
                targetIntensity,
                response * UnityEngine.Time.deltaTime);

            var emission = snowParticles.emission;
            emission.rateOverTime = settings.SnowMaximumEmissionRate
                                    * displayedIntensity
                                    * displayedIntensity;
            var velocity = snowParticles.velocityOverLifetime;
            ApplyVelocity(velocity);
        }

        private void ApplyVelocity(ParticleSystem.VelocityOverLifetimeModule velocity)
        {
            var wind = weatherSystem != null ? weatherSystem.CurrentAtFocus.SurfaceWind : Vector2.zero;
            velocity.x = wind.x * settings.SnowWindInfluence;
            velocity.y = -settings.SnowFallSpeed;
            velocity.z = wind.y * settings.SnowWindInfluence;
        }

        private void HandleOriginShift(Vector3 shift)
        {
            if (snowParticles == null || shiftBuffer == null)
            {
                return;
            }

            var count = snowParticles.GetParticles(shiftBuffer);
            for (var index = 0; index < count; index++)
            {
                shiftBuffer[index].position -= shift;
            }
            snowParticles.SetParticles(shiftBuffer, count);
        }

        private void OnDestroy()
        {
            if (floatingOrigin != null)
            {
                floatingOrigin.Shifted -= HandleOriginShift;
            }
            if (!ownsMaterial || snowMaterial == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                Destroy(snowMaterial);
            }
            else
            {
                DestroyImmediate(snowMaterial);
            }
        }
    }
}
