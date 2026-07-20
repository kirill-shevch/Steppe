using System;
using Steppe.Settings;
using Steppe.Terrain;
using Steppe.Weather;
using Steppe.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Steppe.Ecology
{
    /// <summary>
    /// Camera-local presentation of world-anchored dust sources. Candidate points are
    /// quantized in canonical coordinates, while P7 soil records remain authoritative.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SteppeDustPresentation : MonoBehaviour
    {
        private SteppeWorldSettings settings;
        private SteppeWeatherSystem weatherSystem;
        private SteppeEcologySystem ecologySystem;
        private FloatingOriginSystem floatingOrigin;
        private Transform focus;
        private TerrainHeightGenerator terrainGenerator;
        private ParticleSystem dustParticles;
        private ParticleSystem.Particle[] shiftBuffer;
        private Material dustMaterial;
        private bool ownsMaterial;
        private bool rainSuppressed;
        private float candidateAccumulator;
        private float displayedEmission;
        private uint randomState;

        public ParticleSystem Particles => dustParticles;
        public ParticleSystemRenderer Renderer { get; private set; }
        public SteppeDustState CurrentAtFocus { get; private set; }
        public float DisplayedEmission => displayedEmission;

        public void Configure(
            SteppeWorldSettings worldSettings,
            SteppeWeatherSystem weather,
            SteppeEcologySystem ecology,
            FloatingOriginSystem origin,
            Transform focusTransform,
            Material material = null)
        {
            settings = worldSettings != null ? worldSettings : throw new ArgumentNullException(nameof(worldSettings));
            weatherSystem = weather != null ? weather : throw new ArgumentNullException(nameof(weather));
            ecologySystem = ecology != null ? ecology : throw new ArgumentNullException(nameof(ecology));
            floatingOrigin = origin != null ? origin : throw new ArgumentNullException(nameof(origin));
            focus = focusTransform != null ? focusTransform : throw new ArgumentNullException(nameof(focusTransform));
            terrainGenerator = new TerrainHeightGenerator(settings);
            randomState = unchecked((uint)settings.WorldSeed * 747796405u + 2891336453u);

            dustParticles = GetComponent<ParticleSystem>();
            if (dustParticles == null)
            {
                dustParticles = gameObject.AddComponent<ParticleSystem>();
            }

            Renderer = GetComponent<ParticleSystemRenderer>();
            shiftBuffer = new ParticleSystem.Particle[settings.DustMaxParticles];
            ConfigureParticles();
            ConfigureMaterial(material);
            floatingOrigin.Shifted += HandleOriginShift;
            dustParticles.Play(true);
        }

        private void ConfigureParticles()
        {
            dustParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = dustParticles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.maxParticles = settings.DustMaxParticles;
            main.startLifetime = 3.5f;
            main.startSpeed = 0f;
            main.startSize = 1f;
            main.startColor = Color.white;
            main.gravityModifier = 0f;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

            var emission = dustParticles.emission;
            emission.enabled = false;

            var shape = dustParticles.shape;
            shape.enabled = false;

            var colorOverLifetime = dustParticles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.9f, 0.18f),
                    new GradientAlphaKey(0.55f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = fade;

            var sizeOverLifetime = dustParticles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.42f),
                    new Keyframe(0.35f, 1f),
                    new Keyframe(1f, 1.75f)));

            var noise = dustParticles.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.strength = 0.42f;
            noise.frequency = 0.16f;
            noise.scrollSpeed = 0.11f;
            noise.damping = true;

            Renderer.renderMode = ParticleSystemRenderMode.Stretch;
            Renderer.alignment = ParticleSystemRenderSpace.View;
            Renderer.cameraVelocityScale = 0f;
            Renderer.velocityScale = 0.11f;
            Renderer.lengthScale = 1.6f;
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
                dustMaterial = material;
                ownsMaterial = false;
            }
            else
            {
                var shader = Shader.Find("Steppe/Dust Wisp");
                if (shader == null)
                {
                    throw new InvalidOperationException("Steppe/Dust Wisp shader was not found.");
                }

                dustMaterial = new Material(shader)
                {
                    name = "Steppe P9 Dust Material",
                    hideFlags = HideFlags.DontSave
                };
                ownsMaterial = true;
            }

            Renderer.sharedMaterial = dustMaterial;
        }

        private void Update()
        {
            if (settings == null
                || weatherSystem == null
                || ecologySystem == null
                || floatingOrigin == null
                || focus == null
                || dustParticles == null)
            {
                return;
            }

            var focusWorld = floatingOrigin.LocalToWorld(focus.position);
            var focusCoordinate = EcoCellCoordinate.FromWorld(
                focusWorld.X,
                focusWorld.Z,
                settings.EcologyCellSize);
            var weather = weatherSystem.CurrentAtFocus;
            var hasFocusState = ecologySystem.TryGetCell(
                focusCoordinate,
                out var focusSurface,
                out var focusEcology);
            CurrentAtFocus = hasFocusState
                ? SteppeDustModel.Evaluate(
                    settings,
                    focusSurface,
                    focusEcology,
                    weather.SurfaceWind.magnitude,
                    weather.RainIntensity)
                : default;

            var targetEmission = hasFocusState ? (float)CurrentAtFocus.Emission : 0f;
            var response = targetEmission > displayedEmission ? 0.75f : 2.8f;
            displayedEmission = Mathf.MoveTowards(
                displayedEmission,
                targetEmission,
                response * UnityEngine.Time.deltaTime);

            var raining = weather.RainIntensity > 0.05;
            if (raining && !rainSuppressed)
            {
                dustParticles.Clear(false);
            }
            rainSuppressed = raining;

            if (displayedEmission <= 0.0001f || raining)
            {
                candidateAccumulator = Mathf.Min(candidateAccumulator, 1f);
                return;
            }

            candidateAccumulator += settings.DustMaximumCandidateRate * UnityEngine.Time.deltaTime;
            var attempts = Mathf.Min(96, Mathf.FloorToInt(candidateAccumulator));
            candidateAccumulator -= attempts;
            for (var index = 0; index < attempts; index++)
            {
                TryEmitCandidate(focusWorld.X, focusWorld.Z, weather);
            }
        }

        private void TryEmitCandidate(
            double focusWorldX,
            double focusWorldZ,
            SteppeWeatherSample weather)
        {
            var angle = Next01() * Math.PI * 2.0;
            var distance = Math.Sqrt(Next01()) * settings.DustEmissionRadius;
            var rawWorldX = focusWorldX + Math.Cos(angle) * distance;
            var rawWorldZ = focusWorldZ + Math.Sin(angle) * distance;
            var spacing = settings.DustSourceSpacing;
            var sourceX = (long)Math.Floor(rawWorldX / spacing);
            var sourceZ = (long)Math.Floor(rawWorldZ / spacing);
            var jitterX = 0.18 + Hash01(sourceX, sourceZ, settings.WorldSeed + 3907) * 0.64;
            var jitterZ = 0.18 + Hash01(sourceX, sourceZ, settings.WorldSeed + 10513) * 0.64;
            var worldX = (sourceX + jitterX) * spacing;
            var worldZ = (sourceZ + jitterZ) * spacing;
            var coordinate = EcoCellCoordinate.FromWorld(worldX, worldZ, settings.EcologyCellSize);
            if (!ecologySystem.TryGetCell(coordinate, out var surface, out var ecology))
            {
                return;
            }

            var dust = SteppeDustModel.Evaluate(
                settings,
                surface,
                ecology,
                weather.SurfaceWind.magnitude,
                weather.RainIntensity);
            if (Next01() > dust.Emission)
            {
                return;
            }

            var height = terrainGenerator.SampleHeight(worldX, worldZ) + settings.DustSpawnHeight;
            var localPosition = floatingOrigin.WorldToLocal(worldX, height, worldZ);
            var wind = weather.SurfaceWind;
            var windDirection = wind.sqrMagnitude > 0.001f ? wind.normalized : Vector2.right;
            var lateral = new Vector2(-windDirection.y, windDirection.x);
            var velocityVariation = Mathf.Lerp(0.70f, 1.05f, (float)Next01());
            var lateralVelocity = ((float)Next01() - 0.5f) * 0.8f;
            var horizontalVelocity = wind * (settings.DustWindVelocityRatio * velocityVariation)
                                     + lateral * lateralVelocity;
            var groundColor = (Color)surface.GroundColor;
            var particleColor = Color.Lerp(groundColor, new Color(0.58f, 0.49f, 0.36f), 0.28f);
            particleColor.a = Mathf.Lerp(0.12f, 0.24f, (float)dust.Emission);

            var emit = new ParticleSystem.EmitParams
            {
                applyShapeToPosition = false,
                position = localPosition,
                velocity = new Vector3(
                    horizontalVelocity.x,
                    Mathf.Lerp(0.16f, 0.62f, (float)Next01()),
                    horizontalVelocity.y),
                startColor = particleColor,
                startLifetime = Mathf.Lerp(2.6f, 4.8f, (float)Next01()),
                startSize = Mathf.Lerp(0.65f, 1.8f, (float)Next01()),
                randomSeed = NextUInt()
            };
            dustParticles.Emit(emit, 1);
        }

        private void HandleOriginShift(Vector3 shift)
        {
            if (dustParticles == null || shiftBuffer == null)
            {
                return;
            }

            var count = dustParticles.GetParticles(shiftBuffer);
            for (var index = 0; index < count; index++)
            {
                shiftBuffer[index].position -= shift;
            }
            dustParticles.SetParticles(shiftBuffer, count);
        }

        private uint NextUInt()
        {
            randomState = unchecked(randomState * 1664525u + 1013904223u);
            return randomState;
        }

        private double Next01()
        {
            return (NextUInt() & 0x00ffffffu) / 16777216.0;
        }

        private static double Hash01(long x, long z, int seed)
        {
            unchecked
            {
                var hash = (uint)seed;
                hash ^= (uint)x * 0x9e3779b9u;
                hash = (hash ^ (hash >> 16)) * 0x85ebca6bu;
                hash ^= (uint)z * 0xc2b2ae35u;
                hash = (hash ^ (hash >> 13)) * 0x27d4eb2du;
                hash ^= hash >> 15;
                return (hash & 0x00ffffffu) / 16777216.0;
            }
        }

        private void OnDestroy()
        {
            if (floatingOrigin != null)
            {
                floatingOrigin.Shifted -= HandleOriginShift;
            }

            if (!ownsMaterial || dustMaterial == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(dustMaterial);
            }
            else
            {
                DestroyImmediate(dustMaterial);
            }
        }
    }
}
