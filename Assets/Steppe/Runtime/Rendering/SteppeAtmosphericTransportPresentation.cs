using System;
using Steppe.Simulation;
using Steppe.UnitySimulation;
using Steppe.Weather;
using Steppe.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Steppe.Rendering
{
    /// <summary>
    /// Near-field carriers for temperature and humidity advection. Scalar transport
    /// controls the signed internal pulse; the matching vector process controls
    /// travel direction, speed and population. Presentation is read-only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SteppeAtmosphericTransportPresentation : MonoBehaviour
    {
        private sealed class Carrier
        {
            public Transform Transform;
            public ParticleSystem Particles;
            public Material Material;
            public float DisplayedFlux;
            public float DisplayedGrossMagnitude;
        }

        private SteppeSimulationHost host;
        private FloatingOriginSystem floatingOrigin;
        private Transform focus;
        private SteppeCloudLayer cloudLayer;
        private Carrier temperature;
        private Carrier humidity;
        private Carrier cloudEvaporation;

        public bool IsReady => host != null
                               && host.IsReady
                               && temperature?.Particles != null
                               && humidity?.Particles != null
                               && cloudEvaporation?.Particles != null;
        public ParticleSystem TemperatureParticles => temperature?.Particles;
        public ParticleSystem HumidityParticles => humidity?.Particles;
        public ParticleSystem CloudEvaporationParticles => cloudEvaporation?.Particles;
        public float CurrentAirTemperatureTransportRate => temperature?.DisplayedFlux ?? 0f;
        public float CurrentHumidityTransportRate => humidity?.DisplayedFlux ?? 0f;
        public float CurrentAirTemperatureAdvectionGrossMagnitude =>
            temperature?.DisplayedGrossMagnitude ?? 0f;
        public float CurrentHumidityAdvectionGrossMagnitude =>
            humidity?.DisplayedGrossMagnitude ?? 0f;
        public float CurrentCloudEvaporationRate => cloudEvaporation?.DisplayedFlux ?? 0f;

        public void Configure(
            SteppeSimulationHost simulationHost,
            FloatingOriginSystem origin,
            Transform focusTransform,
            SteppeCloudLayer cloudPresentation)
        {
            host = simulationHost != null
                ? simulationHost
                : throw new ArgumentNullException(nameof(simulationHost));
            floatingOrigin = origin != null
                ? origin
                : throw new ArgumentNullException(nameof(origin));
            focus = focusTransform != null
                ? focusTransform
                : throw new ArgumentNullException(nameof(focusTransform));
            cloudLayer = cloudPresentation != null
                ? cloudPresentation
                : throw new ArgumentNullException(nameof(cloudPresentation));

            temperature = CreateCarrier(
                "Air Temperature Advection Lenses",
                carrierKind: 0f,
                maximumParticles: 360,
                lifetime: new Vector2(3.4f, 6.2f),
                size: new Vector2(0.22f, 0.55f),
                area: new Vector3(84f, 5f, 84f),
                stretched: true,
                fixedColor: new Color(0.90f, 0.91f, 0.88f, 0.12f));
            humidity = CreateCarrier(
                "Humidity Advection Beads",
                carrierKind: 1f,
                maximumParticles: 520,
                lifetime: new Vector2(3.0f, 5.4f),
                size: new Vector2(0.075f, 0.19f),
                area: new Vector3(92f, 8f, 92f),
                stretched: true,
                fixedColor: new Color(0.82f, 0.88f, 0.88f, 0.24f));
            cloudEvaporation = CreateCarrier(
                "Cloud Evaporation Expanding Rims",
                carrierKind: 2f,
                maximumParticles: 180,
                lifetime: new Vector2(2.8f, 4.8f),
                size: new Vector2(50f, 116f),
                area: new Vector3(920f, 180f, 920f),
                stretched: false,
                fixedColor: new Color(0.44f, 0.50f, 0.54f, 0.52f));
            floatingOrigin.Shifted += HandleOriginShift;
        }

        private Carrier CreateCarrier(
            string carrierName,
            float carrierKind,
            int maximumParticles,
            Vector2 lifetime,
            Vector2 size,
            Vector3 area,
            bool stretched,
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
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0f;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = area;
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = velocity.y = velocity.z = 0f;
            var noise = particles.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.strength = carrierKind < 0.5f ? 0.045f : 0.075f;
            noise.frequency = carrierKind < 0.5f ? 0.16f : 0.31f;
            noise.scrollSpeed = 0.08f;
            noise.damping = true;

            if (carrierKind > 1.5f)
            {
                var sizeOverLifetime = particles.sizeOverLifetime;
                sizeOverLifetime.enabled = true;
                sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                    1f,
                    new AnimationCurve(
                        new Keyframe(0f, 0.18f),
                        new Keyframe(0.35f, 0.72f),
                        new Keyframe(1f, 1.48f)));
            }

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
                    new GradientAlphaKey(1f, 0.2f),
                    new GradientAlphaKey(0.82f, 0.72f),
                    new GradientAlphaKey(0f, 1f),
                });
            colorOverLifetime.color = gradient;

            var shader = Shader.Find("Steppe/Atmospheric Transport Carriers");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "Steppe/Atmospheric Transport Carriers shader was not found.");
            }

            var material = new Material(shader)
            {
                name = $"Steppe {carrierName} Material",
                hideFlags = HideFlags.DontSave,
            };
            material.SetFloat("_CarrierKind", carrierKind);
            material.SetColor("_BaseColor", fixedColor);
            material.SetFloat("_FluxSignal", 0f);
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = stretched
                ? ParticleSystemRenderMode.Stretch
                : ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.velocityScale = stretched
                ? (carrierKind < 0.5f ? 0.18f : 0.08f)
                : 0f;
            renderer.lengthScale = stretched
                ? (carrierKind < 0.5f ? 5.4f : 2.2f)
                : 1f;
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
            if (!IsReady || focus == null || floatingOrigin == null)
            {
                return;
            }

            var world = floatingOrigin.LocalToWorld(focus.position);
            if (!TryReadProcess(
                    SimulationFlux.AirTemperatureTransport,
                    VectorProcess.AirTemperatureAdvection,
                    world.X,
                    world.Z,
                    out var temperatureFlux,
                    out var temperatureVector,
                    out var temperatureGross)
                || !TryReadProcess(
                    SimulationFlux.HumidityTransport,
                    VectorProcess.HumidityAdvection,
                    world.X,
                    world.Z,
                    out var humidityFlux,
                    out var humidityVector,
                    out var humidityGross)
                || !host.TrySampleFluxRate(
                    SimulationFlux.CloudEvaporation,
                    world.X,
                    world.Z,
                    out var cloudEvaporationRate))
            {
                return;
            }

            var response = 1f - Mathf.Exp(-UnityEngine.Time.deltaTime * 1.8f);
            UpdateCarrier(
                temperature,
                temperatureFlux,
                temperatureVector,
                temperatureGross,
                fullFlux: 0.08f,
                fullGrossMagnitude: 3.2f,
                maximumEmission: 92f,
                visualSpeed: new Vector2(0.34f, 2.1f),
                response);
            UpdateCarrier(
                humidity,
                humidityFlux,
                humidityVector,
                humidityGross,
                fullFlux: 0.10f,
                fullGrossMagnitude: 3.4f,
                maximumEmission: 145f,
                visualSpeed: new Vector2(0.28f, 1.75f),
                response);
            UpdateScalarCarrier(
                cloudEvaporation,
                Mathf.Max(0f, cloudEvaporationRate),
                fullRate: 0.006f,
                maximumEmission: 22f,
                response);

            temperature.Transform.position = focus.position + Vector3.up * 0.4f;
            humidity.Transform.position = focus.position + Vector3.up * 1.9f;
            cloudEvaporation.Transform.position = new Vector3(
                focus.position.x,
                cloudLayer.CurrentBaseHeight + 240f,
                focus.position.z);
        }

        private bool TryReadProcess(
            SimulationFlux flux,
            VectorProcess vectorProcess,
            double worldX,
            double worldZ,
            out float fluxRate,
            out Vector2 vector,
            out float grossMagnitude)
        {
            vector = default;
            grossMagnitude = default;
            if (!host.TrySampleFluxRate(flux, worldX, worldZ, out fluxRate)
                || !host.TrySampleVectorProcess(
                    vectorProcess,
                    worldX,
                    worldZ,
                    out var vectorX,
                    out var vectorY,
                    out _,
                    out grossMagnitude))
            {
                return false;
            }

            vector = new Vector2(vectorX, vectorY);
            return true;
        }

        private static void UpdateCarrier(
            Carrier carrier,
            float flux,
            Vector2 processVector,
            float grossMagnitude,
            float fullFlux,
            float fullGrossMagnitude,
            float maximumEmission,
            Vector2 visualSpeed,
            float response)
        {
            carrier.DisplayedFlux = Mathf.Lerp(carrier.DisplayedFlux, flux, response);
            carrier.DisplayedGrossMagnitude = Mathf.Lerp(
                carrier.DisplayedGrossMagnitude,
                Mathf.Max(0f, grossMagnitude),
                response);
            var vectorStrength = Mathf.Sqrt(Mathf.Clamp01(
                carrier.DisplayedGrossMagnitude / fullGrossMagnitude));
            var fluxStrength = Mathf.Clamp(
                carrier.DisplayedFlux / fullFlux,
                -1f,
                1f);
            var emission = carrier.Particles.emission;
            emission.rateOverTime = maximumEmission * vectorStrength;
            var direction = processVector.sqrMagnitude > 0.0000001f
                ? processVector.normalized
                : Vector2.zero;
            var speed = Mathf.Lerp(visualSpeed.x, visualSpeed.y, vectorStrength);
            var velocity = carrier.Particles.velocityOverLifetime;
            velocity.x = direction.x * speed;
            velocity.y = 0f;
            velocity.z = direction.y * speed;
            carrier.Material.SetFloat("_FluxSignal", fluxStrength);
        }

        private static void UpdateScalarCarrier(
            Carrier carrier,
            float fluxRate,
            float fullRate,
            float maximumEmission,
            float response)
        {
            carrier.DisplayedFlux = Mathf.Lerp(carrier.DisplayedFlux, fluxRate, response);
            var strength = Mathf.Sqrt(Mathf.Clamp01(carrier.DisplayedFlux / fullRate));
            var emission = carrier.Particles.emission;
            emission.rateOverTime = maximumEmission * strength;
            carrier.Material.SetFloat("_FluxSignal", strength);
        }

        private void HandleOriginShift(Vector3 shift)
        {
            temperature?.Particles?.Clear(true);
            humidity?.Particles?.Clear(true);
            cloudEvaporation?.Particles?.Clear(true);
        }

        private void OnDestroy()
        {
            if (floatingOrigin != null)
            {
                floatingOrigin.Shifted -= HandleOriginShift;
            }

            DestroyMaterial(temperature);
            DestroyMaterial(humidity);
            DestroyMaterial(cloudEvaporation);
        }

        private static void DestroyMaterial(Carrier carrier)
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
