using System;
using Steppe.Settings;
using Steppe.Time;
using Steppe.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Steppe.Weather
{
    /// <summary>
    /// Camera-local presentation of the canonical rain field. One world-space particle
    /// volume follows the observer, so visual cost is independent of streamed chunk count.
    /// The weather model remains the sole authority for whether and how hard it rains.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SteppeRainPresentation : MonoBehaviour
    {
        private SteppeWorldSettings settings;
        private SteppeWeatherSystem weatherSystem;
        private Transform focus;
        private SteppeTimeSystem timeSystem;
        private FloatingOriginSystem floatingOrigin;
        private SteppeLocalClimateSampler climateSampler;
        private ParticleSystem rainParticles;
        private Material rainMaterial;
        private bool ownsMaterial;
        private float displayedIntensity;

        public ParticleSystem Particles => rainParticles;
        public ParticleSystemRenderer Renderer { get; private set; }
        public float DisplayedIntensity => displayedIntensity;
        public double CurrentAirTemperatureC { get; private set; }
        public float CurrentRainFraction { get; private set; } = 1f;

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

            rainParticles = GetComponent<ParticleSystem>();
            if (rainParticles == null)
            {
                rainParticles = gameObject.AddComponent<ParticleSystem>();
            }

            Renderer = GetComponent<ParticleSystemRenderer>();
            ConfigureParticles();
            ConfigureMaterial(material);
            transform.position = focus.position + Vector3.up * settings.RainSpawnHeight;
            rainParticles.Play(true);
        }

        private void ConfigureParticles()
        {
            rainParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = rainParticles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.maxParticles = settings.RainMaxParticles;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.35f, 1.8f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.045f);
            main.startColor = new Color(0.74f, 0.82f, 0.9f, 0.68f);
            main.gravityModifier = 0f;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

            var emission = rainParticles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            var shape = rainParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(settings.RainEmissionArea, 1f, settings.RainEmissionArea);

            var velocity = rainParticles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            ApplyVelocity(velocity);

            Renderer.renderMode = ParticleSystemRenderMode.Stretch;
            Renderer.alignment = ParticleSystemRenderSpace.View;
            Renderer.cameraVelocityScale = 0f;
            Renderer.velocityScale = 0.035f;
            Renderer.lengthScale = 6f;
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
                rainMaterial = material;
                ownsMaterial = false;
            }
            else
            {
                var shader = Shader.Find("Steppe/Rain Streak");
                if (shader == null)
                {
                    throw new InvalidOperationException("Steppe/Rain Streak shader was not found.");
                }

                rainMaterial = new Material(shader)
                {
                    name = "Steppe P5 Rain Material",
                    hideFlags = HideFlags.DontSave
                };
                ownsMaterial = true;
            }

            Renderer.sharedMaterial = rainMaterial;
        }

        private void Update()
        {
            if (settings == null
                || weatherSystem == null
                || timeSystem == null
                || floatingOrigin == null
                || focus == null
                || rainParticles == null)
            {
                return;
            }

            transform.position = focus.position + Vector3.up * settings.RainSpawnHeight;

            var focusWorld = floatingOrigin.LocalToWorld(focus.position);
            CurrentAirTemperatureC = climateSampler.SampleTemperature(
                focusWorld.X,
                focusWorld.Z,
                timeSystem.Current);
            CurrentRainFraction = (float)SteppePrecipitationPhase.RainFraction(CurrentAirTemperatureC);
            var targetIntensity = Mathf.Clamp01(
                (float)weatherSystem.CurrentAtFocus.RainIntensity * CurrentRainFraction);
            var response = targetIntensity > displayedIntensity ? 1.8f : 0.75f;
            displayedIntensity = Mathf.MoveTowards(
                displayedIntensity,
                targetIntensity,
                response * UnityEngine.Time.deltaTime);

            var emission = rainParticles.emission;
            emission.rateOverTime = settings.RainMaximumEmissionRate
                                    * displayedIntensity
                                    * displayedIntensity;

            var velocity = rainParticles.velocityOverLifetime;
            ApplyVelocity(velocity);
        }

        private void ApplyVelocity(ParticleSystem.VelocityOverLifetimeModule velocity)
        {
            var wind = weatherSystem != null ? weatherSystem.CurrentAtFocus.SurfaceWind : Vector2.zero;
            velocity.x = wind.x * settings.RainWindInfluence;
            // Unity requires all three velocity axes to use the same MinMaxCurve mode.
            // X and Z are constants, so Y must be a constant as well; mixing their
            // modes emitted one Console error every frame and prevented clean testing.
            velocity.y = -settings.RainFallSpeed;
            velocity.z = wind.y * settings.RainWindInfluence;
        }

        private void OnDestroy()
        {
            if (!ownsMaterial || rainMaterial == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(rainMaterial);
            }
            else
            {
                DestroyImmediate(rainMaterial);
            }
        }
    }
}
