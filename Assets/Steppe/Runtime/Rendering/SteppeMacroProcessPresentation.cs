using System;
using System.Collections.Generic;
using Steppe.Simulation;
using Steppe.Terrain;
using Steppe.UnitySimulation;
using Steppe.Weather;
using Steppe.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Steppe.Rendering
{
    /// <summary>
    /// Turns the two non-field macro processes into world objects: giant harvester
    /// migrations become smoothly moving creatures, while active fire cells emit a
    /// wind-sheared flame/smoke volume. Their trails remain authoritative field data.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SteppeMacroProcessPresentation : MonoBehaviour
    {
        private sealed class HarvesterVisual
        {
            public GameObject Root;
            public Renderer[] Renderers;
        }

        private sealed class FireVisual
        {
            public GameObject Root;
            public ParticleSystem Particles;
            public int CellIndex;
        }

        private readonly Dictionary<int, HarvesterVisual> harvesters =
            new Dictionary<int, HarvesterVisual>();
        private readonly List<FireVisual> fires = new List<FireVisual>();
        private SteppeSimulationHost host;
        private FloatingOriginSystem floatingOrigin;
        private SteppeWeatherSystem weatherSystem;
        private Transform focus;
        private Transform worldRoot;
        private TerrainHeightGenerator terrain;
        private Material harvesterMaterial;
        private Material fireMaterial;
        private SteppeSimulationSnapshot lastFireSnapshot;

        public int VisibleHarvesterCount { get; private set; }
        public int VisibleFireCellCount => fires.Count;

        public void Configure(
            SteppeSimulationHost simulationHost,
            FloatingOriginSystem origin,
            SteppeWeatherSystem weather,
            Transform focusTransform,
            Transform worldSpaceRoot,
            TerrainHeightGenerator terrainGenerator)
        {
            host = simulationHost != null ? simulationHost : throw new ArgumentNullException(nameof(simulationHost));
            floatingOrigin = origin != null ? origin : throw new ArgumentNullException(nameof(origin));
            weatherSystem = weather != null ? weather : throw new ArgumentNullException(nameof(weather));
            focus = focusTransform != null ? focusTransform : throw new ArgumentNullException(nameof(focusTransform));
            worldRoot = worldSpaceRoot != null ? worldSpaceRoot : throw new ArgumentNullException(nameof(worldSpaceRoot));
            terrain = terrainGenerator ?? throw new ArgumentNullException(nameof(terrainGenerator));
            CreateMaterials();
        }

        private void Update()
        {
            var latest = host != null ? host.LatestSnapshot : null;
            if (latest == null)
            {
                return;
            }

            UpdateHarvesters(host.PreviousSnapshot ?? latest, latest, host.MacroInterpolationAlpha);
            if (latest != lastFireSnapshot)
            {
                RebuildFireVisuals(latest);
                lastFireSnapshot = latest;
            }

            UpdateFires(latest);
        }

        private void UpdateHarvesters(
            SteppeSimulationSnapshot previous,
            SteppeSimulationSnapshot latest,
            float blend)
        {
            var population = latest.GiantHarvesters;
            if (population == null)
            {
                return;
            }

            var seen = new HashSet<int>();
            VisibleHarvesterCount = 0;
            for (var index = 0; index < population.Harvesters.Length; index++)
            {
                var current = population.Harvesters[index];
                var prior = FindHarvester(previous.GiantHarvesters, current.Id) ?? current;
                if (!harvesters.TryGetValue(current.Id, out var visual))
                {
                    visual = CreateHarvesterVisual(current.Id);
                    harvesters.Add(current.Id, visual);
                }

                seen.Add(current.Id);
                var cellX = Mathf.LerpUnclamped(prior.X, current.X, blend);
                var cellY = Mathf.LerpUnclamped(prior.Y, current.Y, blend);
                latest.Coordinates.GridPositionToWorld(cellX, cellY, out var worldX, out var worldZ);
                var focusWorld = floatingOrigin.LocalToWorld(focus.position);
                var distance = Math.Sqrt(
                    (worldX - focusWorld.X) * (worldX - focusWorld.X)
                    + (worldZ - focusWorld.Z) * (worldZ - focusWorld.Z));
                var isVisible = distance < 5200d;
                visual.Root.SetActive(isVisible);
                if (!isVisible)
                {
                    continue;
                }

                VisibleHarvesterCount++;
                var height = terrain.SampleHeight(worldX, worldZ);
                visual.Root.transform.position = floatingOrigin.WorldToLocal(worldX, height + 4.2, worldZ);
                var heading = Vector3.Lerp(
                    new Vector3(prior.HeadingX, 0f, prior.HeadingY),
                    new Vector3(current.HeadingX, 0f, current.HeadingY),
                    blend);
                if (heading.sqrMagnitude > 0.0001f)
                {
                    visual.Root.transform.rotation = Quaternion.LookRotation(heading.normalized, Vector3.up);
                }

                var activityColor = ActivityColor(current.Activity);
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", activityColor);
                for (var rendererIndex = 0; rendererIndex < visual.Renderers.Length; rendererIndex++)
                {
                    visual.Renderers[rendererIndex].SetPropertyBlock(block);
                }

                var stride = Mathf.Sin((float)host.ElapsedSimulationSeconds * 0.22f + current.Id) * 1.8f;
                visual.Root.transform.localScale = new Vector3(1f, 1f + stride * 0.018f, 1f);
            }

            foreach (var pair in harvesters)
            {
                if (!seen.Contains(pair.Key))
                {
                    pair.Value.Root.SetActive(false);
                }
            }
        }

        private HarvesterVisual CreateHarvesterVisual(int id)
        {
            var root = new GameObject($"Giant Harvester {id}");
            root.transform.SetParent(worldRoot, false);
            var renderers = new List<Renderer>();

            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Armoured body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 4.5f, 0f);
            body.transform.localScale = new Vector3(8.5f, 5.8f, 14f);
            DisableCollider(body);
            var bodyRenderer = body.GetComponent<Renderer>();
            bodyRenderer.sharedMaterial = harvesterMaterial;
            bodyRenderer.shadowCastingMode = ShadowCastingMode.On;
            renderers.Add(bodyRenderer);

            for (var side = -1; side <= 1; side += 2)
            {
                for (var legIndex = 0; legIndex < 3; legIndex++)
                {
                    var leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    leg.name = "Walking leg";
                    leg.transform.SetParent(root.transform, false);
                    leg.transform.localPosition = new Vector3(side * 5.2f, 0.6f, (legIndex - 1) * 5f);
                    leg.transform.localRotation = Quaternion.Euler(0f, 0f, side * 34f);
                    leg.transform.localScale = new Vector3(0.58f, 4.8f, 0.58f);
                    DisableCollider(leg);
                    var renderer = leg.GetComponent<Renderer>();
                    renderer.sharedMaterial = harvesterMaterial;
                    renderers.Add(renderer);
                }
            }

            return new HarvesterVisual
            {
                Root = root,
                Renderers = renderers.ToArray(),
            };
        }

        private void RebuildFireVisuals(SteppeSimulationSnapshot snapshot)
        {
            if (!snapshot.TryGetLayer(SimulationLayer.FireIntensity, out var fireLayer))
            {
                return;
            }

            var focusWorld = floatingOrigin.LocalToWorld(focus.position);
            var candidates = new List<(int Index, float Score)>();
            for (var index = 0; index < fireLayer.Values.Length; index++)
            {
                var intensity = fireLayer.Values[index];
                if (intensity < 0.003f)
                {
                    continue;
                }

                var x = index % fireLayer.Width;
                var y = index / fireLayer.Width;
                snapshot.Coordinates.CellCenterToWorld(x, y, out var worldX, out var worldZ);
                var distanceSquared = (worldX - focusWorld.X) * (worldX - focusWorld.X)
                                      + (worldZ - focusWorld.Z) * (worldZ - focusWorld.Z);
                if (distanceSquared > 4200d * 4200d)
                {
                    continue;
                }

                candidates.Add((index, intensity * 1000000f - (float)distanceSquared * 0.00001f));
            }

            candidates.Sort((left, right) => right.Score.CompareTo(left.Score));
            var count = Mathf.Min(24, candidates.Count);
            while (fires.Count < count)
            {
                fires.Add(CreateFireVisual(fires.Count));
            }

            while (fires.Count > count)
            {
                Destroy(fires[fires.Count - 1].Root);
                fires.RemoveAt(fires.Count - 1);
            }

            for (var index = 0; index < count; index++)
            {
                fires[index].CellIndex = candidates[index].Index;
            }
        }

        private FireVisual CreateFireVisual(int id)
        {
            var root = new GameObject($"Wildfire cell {id}");
            root.transform.SetParent(worldRoot, false);
            var particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 900;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.4f, 6.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(3f, 13f);
            main.startSpeed = 0f;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.18f, 0.015f, 0.78f),
                new Color(0.17f, 0.14f, 0.12f, 0.42f));
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(185f, 1f, 185f);

            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(3f, 10f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var noise = particles.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.strength = 1.6f;
            noise.frequency = 0.15f;

            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = fireMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            particles.Play(true);
            return new FireVisual { Root = root, Particles = particles };
        }

        private void UpdateFires(SteppeSimulationSnapshot snapshot)
        {
            if (!snapshot.TryGetLayer(SimulationLayer.FireIntensity, out var fireLayer))
            {
                return;
            }

            var wind = weatherSystem.CurrentAtFocus.SurfaceWind;
            for (var index = 0; index < fires.Count; index++)
            {
                var visual = fires[index];
                var cellX = visual.CellIndex % fireLayer.Width;
                var cellY = visual.CellIndex / fireLayer.Width;
                snapshot.Coordinates.CellCenterToWorld(cellX, cellY, out var worldX, out var worldZ);
                var height = terrain.SampleHeight(worldX, worldZ);
                visual.Root.transform.position = floatingOrigin.WorldToLocal(worldX, height + 0.4, worldZ);
                var intensity = Mathf.Sqrt(Mathf.Clamp01(fireLayer.Values[visual.CellIndex]));
                var emission = visual.Particles.emission;
                emission.rateOverTime = 18f + intensity * 310f;
                var velocity = visual.Particles.velocityOverLifetime;
                velocity.x = new ParticleSystem.MinMaxCurve(wind.x * 0.58f, wind.x * 0.58f);
                velocity.z = new ParticleSystem.MinMaxCurve(wind.y * 0.58f, wind.y * 0.58f);
            }
        }

        private void CreateMaterials()
        {
            var litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (litShader != null)
            {
                harvesterMaterial = new Material(litShader)
                {
                    name = "Giant harvester carapace",
                    hideFlags = HideFlags.DontSave,
                    color = new Color(0.32f, 0.27f, 0.18f),
                };
            }

            var fireShader = Shader.Find("Steppe/Dust Wisp");
            if (fireShader != null)
            {
                fireMaterial = new Material(fireShader)
                {
                    name = "Macro wildfire flames and smoke",
                    hideFlags = HideFlags.DontSave,
                };
                fireMaterial.SetColor("_BaseColor", new Color(1f, 0.32f, 0.045f, 0.72f));
            }
        }

        private static GiantHarvesterSnapshot FindHarvester(
            GiantHarvesterPopulationSnapshot population,
            int id)
        {
            if (population == null)
            {
                return null;
            }

            for (var index = 0; index < population.Harvesters.Length; index++)
            {
                if (population.Harvesters[index].Id == id)
                {
                    return population.Harvesters[index];
                }
            }

            return null;
        }

        private static Color ActivityColor(GiantHarvesterActivity activity)
        {
            switch (activity)
            {
                case GiantHarvesterActivity.Grazing:
                    return new Color(0.38f, 0.32f, 0.18f);
                case GiantHarvesterActivity.Drinking:
                    return new Color(0.25f, 0.32f, 0.29f);
                case GiantHarvesterActivity.Molting:
                    return new Color(0.48f, 0.31f, 0.19f);
                default:
                    return new Color(0.31f, 0.27f, 0.20f);
            }
        }

        private static void DisableCollider(GameObject target)
        {
            var collider = target.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (harvesterMaterial != null)
            {
                Destroy(harvesterMaterial);
            }

            if (fireMaterial != null)
            {
                Destroy(fireMaterial);
            }
        }
    }
}
