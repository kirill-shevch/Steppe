using System;
using Steppe.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Steppe.Weather
{
    /// <summary>
    /// Sparse seeds and dry chaff make wind direction readable even where grass is
    /// short and no dust is being lifted. The cue follows the physical surface wind.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SteppeWindCuePresentation : MonoBehaviour
    {
        private SteppeWeatherSystem weatherSystem;
        private FloatingOriginSystem floatingOrigin;
        private Transform focus;
        private ParticleSystem particles;
        private Material material;

        public ParticleSystem Particles => particles;
        public Vector2 DisplayedWind { get; private set; }

        public void Configure(
            SteppeWeatherSystem weather,
            FloatingOriginSystem origin,
            Transform focusTransform)
        {
            weatherSystem = weather != null ? weather : throw new ArgumentNullException(nameof(weather));
            floatingOrigin = origin != null ? origin : throw new ArgumentNullException(nameof(origin));
            focus = focusTransform != null ? focusTransform : throw new ArgumentNullException(nameof(focusTransform));

            particles = GetComponent<ParticleSystem>();
            if (particles == null)
            {
                particles = gameObject.AddComponent<ParticleSystem>();
            }

            ConfigureParticles();
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
            main.maxParticles = 900;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 5.2f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.042f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.86f, 0.79f, 0.58f, 0.10f),
                new Color(0.96f, 0.92f, 0.73f, 0.24f));
            main.gravityModifier = -0.006f;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(170f, 22f, 170f);

            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.08f, 0.32f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var noise = particles.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.strength = 0.38f;
            noise.frequency = 0.18f;
            noise.scrollSpeed = 0.16f;
            noise.damping = true;

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.velocityScale = 0.075f;
            renderer.lengthScale = 10f;
            renderer.cameraVelocityScale = 0f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            var shader = Shader.Find("Steppe/Rain Streak");
            if (shader != null)
            {
                material = new Material(shader)
                {
                    name = "Steppe wind-borne seed material",
                    hideFlags = HideFlags.DontSave,
                };
                material.SetColor("_BaseColor", new Color(0.88f, 0.82f, 0.61f, 0.38f));
                renderer.sharedMaterial = material;
            }
        }

        private void Update()
        {
            if (weatherSystem == null || focus == null || particles == null)
            {
                return;
            }

            transform.position = focus.position + Vector3.up * 11f;
            var targetWind = weatherSystem.CurrentAtFocus.SurfaceWind;
            DisplayedWind = Vector2.Lerp(
                DisplayedWind,
                targetWind,
                1f - Mathf.Exp(-UnityEngine.Time.deltaTime * 1.8f));

            var speed = DisplayedWind.magnitude;
            var emission = particles.emission;
            emission.rateOverTime = Mathf.Lerp(5f, 190f, Mathf.InverseLerp(1.5f, 17f, speed));
            var velocity = particles.velocityOverLifetime;
            velocity.x = new ParticleSystem.MinMaxCurve(DisplayedWind.x, DisplayedWind.x);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.08f, 0.32f);
            velocity.z = new ParticleSystem.MinMaxCurve(DisplayedWind.y, DisplayedWind.y);
        }

        private void HandleOriginShift(Vector3 shift)
        {
            if (particles != null)
            {
                particles.Clear(true);
            }
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
